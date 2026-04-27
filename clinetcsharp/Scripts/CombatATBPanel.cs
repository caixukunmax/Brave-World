using Godot;
using System.Collections.Generic;
using System;
using Protocol;

namespace ClinetCSharp
{
    public partial class CombatATBPanel : Control
    {
        private Control _line;
        private Control _markersContainer;
        private readonly List<Marker> _markers = new();
        private NetworkManager _network;

        private const float LINE_WIDTH_RATIO = 0.5f;
        private const float LERP_SPEED = 15f;
        private float _lineWidth = 600f;

        private class UnitState
        {
            public ulong EntityId;
            public string Name;
            public float TargetAtb;
            public float CurrentAtb;
            public bool IsPlayer;
            public string CastingSkill = "";
            public float CastProgress;
            public int Mp;
            public int MaxMp;
        }

        private readonly Dictionary<ulong, UnitState> _units = new();

        private bool _lastVisible;

        public override void _Ready()
        {
            _line = GetNodeOrNull<Control>("Line");
            _markersContainer = GetNodeOrNull<Control>("MarkersContainer");

            _network = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (_network != null)
            {
                _network.CombatStateNotify += OnCombatStateNotify;
                _network.CombatEndNotify += OnCombatEndNotify;
            }

            // 纯展示面板，鼠标事件穿透，不影响其他面板拖拽
            UiUtils.SetMousePassthrough(this);
            Modulate = Colors.White;
            Visible = false;
            _lineWidth = _line != null ? _line.Size.X : 600f;
        }

        public override void _ExitTree()
        {
            if (_network != null)
            {
                _network.CombatStateNotify -= OnCombatStateNotify;
                _network.CombatEndNotify -= OnCombatEndNotify;
            }
        }

        public override void _Process(double delta)
        {
            bool anyVisible = false;
            foreach (var marker in _markers)
            {
                if (!marker.IsValid) continue;
                if (_units.TryGetValue(marker.EntityId, out var state))
                {
                    marker.Node.Visible = true;
                    anyVisible = true;

                    // 检测是否刚刚行动（从 >=95 掉到 <10）
                    if (state.CurrentAtb >= 95f && state.TargetAtb < 10f)
                    {
                        marker.TriggerFlash();
                    }

                    state.CurrentAtb = Mathf.Lerp(state.CurrentAtb, state.TargetAtb, (float)delta * LERP_SPEED);

                    // 如果差距很小，直接同步
                    if (Mathf.Abs(state.CurrentAtb - state.TargetAtb) < 0.5f)
                        state.CurrentAtb = state.TargetAtb;

                    float t = Mathf.Clamp(state.CurrentAtb / 100f, 0f, 1f);
                    float x = t * _lineWidth;
                    marker.Node.Position = new Vector2(x - marker.Node.Size.X * 0.5f, marker.Node.Position.Y);
                }
                else
                {
                    marker.Node.Visible = false;
                }
            }

            if (anyVisible != _lastVisible)
            {
                Visible = anyVisible;
                _lastVisible = anyVisible;
            }
        }

        private readonly Queue<Game.CombatStateNotify> _pendingStates = new();

        private void OnCombatStateNotify(Game.CombatStateNotify notify)
        {
            lock (_pendingStates)
                _pendingStates.Enqueue(notify);
            CallDeferred(nameof(ApplyStateDeferred));
        }

        private void OnCombatEndNotify(Game.CombatEndNotify notify)
        {
            lock (_pendingStates)
                _pendingStates.Enqueue(new Game.CombatStateNotify()); // 空 notify 清除面板
            CallDeferred(nameof(ApplyStateDeferred));
        }

        private void ApplyStateDeferred()
        {
            while (true)
            {
                Game.CombatStateNotify notify;
                lock (_pendingStates)
                {
                    if (_pendingStates.Count == 0) return;
                    notify = _pendingStates.Dequeue();
                }
                ApplyState(notify);
            }
        }

        private void ApplyState(Game.CombatStateNotify notify)
        {
            if (notify.Units.Count == 0)
            {
                _units.Clear();
                return;
            }

            var receivedIds = new HashSet<ulong>();
            foreach (var u in notify.Units)
            {
                ulong entityId = u.EntityId;
                receivedIds.Add(entityId);
                if (!_units.TryGetValue(entityId, out var state))
                {
                    state = new UnitState { EntityId = entityId };
                    _units[entityId] = state;
                }
                state.Name = u.EntityName;
                state.TargetAtb = u.Atb;
                state.IsPlayer = u.IsPlayer;
                state.CastingSkill = u.CastingSkill;
                state.CastProgress = u.CastProgress;
                state.Mp = u.Mp;
                state.MaxMp = u.MaxMp;
            }

            // 移除不再存在的单位
            var toRemove = new List<ulong>();
            foreach (var id in _units.Keys)
            {
                if (!receivedIds.Contains(id))
                    toRemove.Add(id);
            }
            foreach (var id in toRemove)
                _units.Remove(id);

            SyncMarkers();
        }

        private void SyncMarkers()
        {
            // 复用或创建标记
            int index = 0;
            foreach (var pair in _units)
            {
                var state = pair.Value;
                Marker marker;
                if (index < _markers.Count)
                {
                    marker = _markers[index];
                }
                else
                {
                    marker = CreateMarker();
                    _markers.Add(marker);
                }
                marker.EntityId = state.EntityId;
                marker.SetStyle(state.IsPlayer, state.Name);
                marker.UpdateCastBar(state.CastingSkill, state.CastProgress);
                marker.Node.Visible = true;
                index++;
            }

            // 移除多余标记，防止内存泄漏
            while (_markers.Count > index)
            {
                _markers[^1].Node.QueueFree();
                _markers.RemoveAt(_markers.Count - 1);
            }
        }

        private Marker CreateMarker()
        {
            var node = new Control();
            node.CustomMinimumSize = new Vector2(80, 30);
            node.Size = new Vector2(80, 30);

            var shape = new ColorRect();
            shape.Size = new Vector2(10, 10);
            shape.Position = new Vector2(35, 10);
            node.AddChild(shape);

            var label = new Label();
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.Size = new Vector2(80, 14);
            label.Position = new Vector2(0, -2);
            label.AddThemeFontSizeOverride("font_size", 12);
            node.AddChild(label);

            // 蓄力进度条背景
            var castBarBg = new ColorRect();
            castBarBg.Size = new Vector2(60, 4);
            castBarBg.Position = new Vector2(10, 22);
            castBarBg.Color = new Color(UiStyles.TextDimColor, 0.8f);
            castBarBg.Visible = false;
            node.AddChild(castBarBg);

            // 蓄力进度条填充
            var castBarFill = new ColorRect();
            castBarFill.Size = new Vector2(60, 4);
            castBarFill.Position = new Vector2(10, 22);
            castBarFill.Color = UiStyles.GoldColor;
            castBarFill.Visible = false;
            node.AddChild(castBarFill);

            // 蓄力技能名
            var castLabel = new Label();
            castLabel.HorizontalAlignment = HorizontalAlignment.Center;
            castLabel.Size = new Vector2(80, 12);
            castLabel.Position = new Vector2(0, 25);
            castLabel.AddThemeFontSizeOverride("font_size", 10);
            castLabel.Visible = false;
            node.AddChild(castLabel);

            _markersContainer.AddChild(node);
            UiUtils.SetMousePassthrough(node);
            return new Marker { Node = node, Shape = shape, Label = label, CastBarBg = castBarBg, CastBarFill = castBarFill, CastLabel = castLabel };
        }

        private class Marker
        {
            public Control Node;
            public ColorRect Shape;
            public Label Label;
            public ColorRect CastBarBg;
            public ColorRect CastBarFill;
            public Label CastLabel;
            public ulong EntityId;
            public bool IsValid => Node != null && GodotObject.IsInstanceValid(Node);

            public void SetStyle(bool isPlayer, string name)
            {
                Label.Text = name;
                if (isPlayer)
                {
                    Shape.Color = new Color("#00FF00");
                    Shape.Size = new Vector2(10, 10);
                    Label.Position = new Vector2(0, -14);
                }
                else
                {
                    Shape.Color = new Color("#FF4444");
                    Shape.Size = new Vector2(10, 10);
                    Label.Position = new Vector2(0, 12);
                }
                var style = new StyleBoxFlat
                {
                    BgColor = Shape.Color,
                    CornerRadiusTopLeft = isPlayer ? 0 : 5,
                    CornerRadiusTopRight = isPlayer ? 0 : 5,
                    CornerRadiusBottomLeft = isPlayer ? 0 : 5,
                    CornerRadiusBottomRight = isPlayer ? 0 : 5,
                };
                Shape.AddThemeStyleboxOverride("panel", style);
                Shape.Position = new Vector2(35, 10);
            }

            public void UpdateCastBar(string castingSkill, float progress)
            {
                bool casting = !string.IsNullOrEmpty(castingSkill);
                CastBarBg.Visible = casting;
                CastBarFill.Visible = casting;
                CastLabel.Visible = casting;
                if (casting)
                {
                    CastBarFill.Size = new Vector2(60 * Mathf.Clamp(progress, 0f, 1f), 4);
                    CastLabel.Text = castingSkill;
                }
            }

            public void TriggerFlash()
            {
                if (!IsValid) return;
                var tween = Node.CreateTween();
                if (tween == null) return;
                tween.TweenProperty(Node, "scale", new Vector2(1.3f, 1.3f), 0.05f);
                tween.TweenProperty(Node, "scale", Vector2.One, 0.15f);
            }
        }
    }
}
