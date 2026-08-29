using System.Collections.Generic;
using Godot;

namespace ClinetCSharp
{
	/// <summary>
	/// 技能特效预览面板（编辑器窗口内容）：列出注册表里全部技能，选一个点“施放”
	/// 就在舞台上从施法者朝目标播放其特效；可开自动循环连续看手感。
	/// </summary>
	public partial class SkillPreviewPanel : Control
	{
		private Node2D _stage = null!;
		private OptionButton _picker = null!;
		private CheckBox _auto = null!;
		private SpinBox _interval = null!;
		private Label _status = null!;
		private Timer? _timer;

		private readonly List<int> _ids = new();
		private readonly Vector2 _caster = new(240, 320);
		private readonly Vector2 _target = new(700, 320);

		public override void _Ready()
		{
			_stage = new Node2D();
			AddChild(_stage);
			AddMarker(_stage, _caster, new Color(0.35f, 0.6f, 1f, 0.9f));
			AddMarker(_stage, _target, new Color(1f, 0.4f, 0.35f, 0.9f));

			var bar = new HBoxContainer { Position = new Vector2(10, 8) };
			bar.AddChild(MkLabel("技能"));
			_picker = new OptionButton();
			var keys = new List<int>(SkillEffectRegistry.All.Keys);
			keys.Sort();
			foreach (var id in keys)
			{
				_ids.Add(id);
				_picker.AddItem($"{id} {SkillEffectRegistry.All[id].Name}");
			}
			if (_ids.Count > 0) _picker.Selected = 0;
			bar.AddChild(_picker);

			var play = new Button { Text = "▶ 施放" };
			play.Pressed += Play;
			bar.AddChild(play);

			_auto = new CheckBox { Text = "自动循环" };
			_auto.Toggled += OnAuto;
			bar.AddChild(_auto);
			bar.AddChild(MkLabel("间隔"));
			_interval = new SpinBox { MinValue = 0.3, MaxValue = 5, Step = 0.1, Value = 1.0 };
			bar.AddChild(_interval);
			AddChild(bar);

			_status = MkLabel("选一个技能点“施放”，或勾“自动循环”。");
			_status.Position = new Vector2(12, 44);
			AddChild(_status);
		}

		private void Play()
		{
			int idx = (int)_picker.Selected;
			if (idx < 0 || idx >= _ids.Count) return;
			int id = _ids[idx];
			var spec = SkillEffectRegistry.Get(id);
			if (spec == null) return;
			SkillEffect.Play(_stage, _caster, _target, spec);
			_status.Text = $"施放：{id} {spec.Name}（{spec.Archetype}）";
		}

		private void OnAuto(bool on)
		{
			if (on)
			{
				_timer = new Timer { WaitTime = (float)_interval.Value, Autostart = true };
				_timer.Timeout += Play;
				AddChild(_timer);
				Play();
			}
			else
			{
				_timer?.QueueFree();
				_timer = null;
			}
		}

		private static void AddMarker(Node2D parent, Vector2 pos, Color color)
		{
			float s = 44f;
			parent.AddChild(new ColorRect
			{
				Color = color,
				Size = new Vector2(s, s),
				Position = pos - new Vector2(s / 2, s / 2),
				MouseFilter = Control.MouseFilterEnum.Ignore,
			});
		}

		private static Label MkLabel(string t) => new() { Text = t };
	}
}
