using Godot;
using System;
using System.Collections.Generic;
using ClinetCSharp.Cutscene;

namespace ClinetCSharp
{
    /// <summary>
    /// cue 参数表单：选中一条 cue 后按 type 动态生成固定字段表单（§4.3），
    /// 禁止作者手填 JSON。坐标类字段带"拾取"按钮，点击后进入地图拾取模式，
    /// 在地图视图上点格子即把坐标写回字段。
    ///
    /// 演员引用统一用"演员表 id"（整数），下拉展示"id（玩家/怪物名）"，
    /// 写入 cue.actor 的是纯数字字符串（不再有 actor: 前缀）。
    /// </summary>
    [Tool]
    public partial class CueParamForm : VBoxContainer
    {
        /// <summary>任意字段被修改（主面板用来刷新时间轴摘要并标脏）</summary>
        public event Action ParamChanged;
        /// <summary>请求进入地图坐标拾取模式；回调在拾取完成时被调用</summary>
        public event Action<Action<Vector2I>> PickRequested;

        private CutsceneCue _cue;
        private List<CutsceneActorDef> _actors = new();

        /// <summary>
        /// 数字框(SpinBox)列表 + 各自的写入回调。SpinBox 仅在失去焦点/回车时提交 value_changed，
        /// 用户在框内输入后直接 Ctrl+S 时 value 尚未提交，故保存前需主动把框内文本刷回 cue。
        /// </summary>
        private readonly List<(SpinBox spin, Action<double> write)> _spinners = new();

        /// <summary>
        /// 按 cue 的 type 重建表单。actors = 当前演出演员表（用于 actor 下拉选项）。
        /// </summary>
        public void BuildForm(CutsceneCue cue, List<CutsceneActorDef> actors)
        {
            _cue = cue;
            _actors = actors ?? new List<CutsceneActorDef>();
            _spinners.Clear();
            foreach (var c in GetChildren())
                c.QueueFree();

            if (cue == null)
            {
                AddChild(new Label { Text = "（未选中 cue）" });
                return;
            }

            string type = cue.Type.ToLowerInvariant();
            AddChild(new Label { Text = $"类型：{CutsceneScriptIO.GetCueDisplayName(type)}" });

            // actor 字段：对这些类型有意义（§4.3）
            switch (type)
            {
                case "story_play":
                    AddIntField("章节", "chapter", 1);
                    AddIntField("对话id起始(含)", "dialogue_start", 1);
                    AddIntField("对话id结束(含)", "dialogue_end", 1);
                    break;
                case "move":
                    AddActorField(optional: false);
                    AddCoordField("目标格子");
                    AddFloatField("时长(秒)", "sec", 1f);
                    break;
                case "face":
                    AddActorField(optional: false);
                    AddOptionField("朝向", "dir", new[] { "up", "down", "left", "right" }, "down");
                    break;
                case "camera_focus":
                    AddActorField(optional: true);
                    AddCameraFocusCoordField();
                    AddFloatField("时长(秒)", "sec", 1f);
                    break;
                case "camera_set":
                    AddCameraValueField();
                    break;
                case "wait":
                    AddFloatField("时长(秒)", "sec", 1f);
                    break;
                case "camera_reset":
                    AddFloatField("时长(秒)", "sec", 1f);
                    break;
                case "camera_shake":
                    AddFloatField("强度", "strength", 8f);
                    AddFloatField("时长(秒)", "sec", 0.4f);
                    break;
                case "fade":
                    AddOptionField("方向(out=黑场/in=恢复)", "dir", new[] { "out", "in" }, "out");
                    AddFloatField("时长(秒)", "sec", 0.8f);
                    break;
                case "actor_enter":
                    AddActorField(optional: false);
                    AddCoordField("登场落点");
                    AddOptionField("朝向", "dir", new[] { "up", "down", "left", "right" }, "down");
                    break;
                case "actor_leave":
                    AddActorField(optional: false);
                    break;
                case "sfx":
                case "bgm":
                    AddTextField("资源名", "name", "");
                    break;
                default:
                    AddChild(new Label { Text = "未知类型，无参数表单" });
                    break;
            }
        }

        // ============ 字段控件 ============

        /// <summary>
        /// 保存前强制把数字框当前显示的文本刷回 cue。SpinBox 仅在放弃焦点/回车时提交 value_changed，
        /// 用户在框内键入后直接 Ctrl+S（焦点未离开），value 仍是旧值，若不主动回写就会把旧值保存掉。
        /// </summary>
        public void Commit()
        {
            if (_cue == null) return;
            foreach (var (spin, write) in _spinners)
            {
                // SpinBox 内部 LineEdit 命名为 "LineEdit"，读取框内当前文本（可能尚未提交到 value）
                var le = spin.GetNode<LineEdit>("LineEdit");
                if (le != null && double.TryParse(le.Text, out double d))
                    write(d);
                else
                    write(spin.Value);
            }
        }

        private HBoxContainer AddRow(string label)
        {
            var row = new HBoxContainer();
            row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var l = new Label { Text = label, CustomMinimumSize = new Vector2(150, 0) };
            row.AddChild(l);
            AddChild(row);
            return row;
        }

        private void AddFloatField(string label, string key, float defaultValue)
        {
            var row = AddRow(label);
            var spin = new SpinBox { MinValue = 0, MaxValue = 9999, Step = 0.1, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            float value = _cue.GetFloat(key, defaultValue);
            spin.SetValueNoSignal(value);
            // 缺省值也写回 params，避免表单未改动时校验报"缺少字段"（§4.3）
            _cue.SetFloat(key, value);
            spin.ValueChanged += v =>
            {
                _cue.SetFloat(key, (float)v);
                ParamChanged?.Invoke();
            };
            _spinners.Add((spin, v => _cue.SetFloat(key, (float)v)));
            row.AddChild(spin);
        }

        private void AddIntField(string label, string key, int defaultValue)
        {
            var row = AddRow(label);
            var spin = new SpinBox
            {
                MinValue = -9999,
                MaxValue = 9999,
                Step = 1,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            int value = _cue.GetInt(key, defaultValue);
            spin.SetValueNoSignal(value);
            // 缺省值也写回 params，避免表单未改动时校验报"缺少字段"（§4.3）
            _cue.SetInt(key, value);
            spin.ValueChanged += v =>
            {
                _cue.SetInt(key, (int)v);
                ParamChanged?.Invoke();
            };
            _spinners.Add((spin, v => _cue.SetInt(key, (int)v)));
            row.AddChild(spin);
        }

        private void AddTextField(string label, string key, string defaultValue)
        {
            var row = AddRow(label);
            string value = _cue.GetString(key, defaultValue);
            var edit = new LineEdit { Text = value, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            // 缺省值也写回 params，避免表单未改动时校验报"缺少字段"
            _cue.SetString(key, value);
            edit.TextChanged += text =>
            {
                _cue.SetString(key, text);
                ParamChanged?.Invoke();
            };
            row.AddChild(edit);
        }

        private void AddOptionField(string label, string key, string[] options, string defaultValue)
        {
            var row = AddRow(label);
            var opt = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            foreach (var o in options)
                opt.AddItem(o);
            int cur = Array.IndexOf(options, _cue.GetString(key, defaultValue));
            opt.Select(cur >= 0 ? cur : 0);
            // 缺省值也写回 params，保证保存出的 JSON 字段完整
            _cue.SetString(key, options[cur >= 0 ? cur : 0]);
            opt.ItemSelected += idx =>
            {
                _cue.SetString(key, options[(int)idx]);
                ParamChanged?.Invoke();
            };
            row.AddChild(opt);
        }

        /// <summary>x/y 坐标字段 + "拾取"按钮（点击后进入地图拾取模式，点地图落值）</summary>
        private void AddCoordField(string label)
        {
            var row = AddRow(label);
            var xSpin = new SpinBox { MinValue = -9999, MaxValue = 9999, Step = 1, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var ySpin = new SpinBox { MinValue = -9999, MaxValue = 9999, Step = 1, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            int x = _cue.GetInt("x");
            int y = _cue.GetInt("y");
            xSpin.SetValueNoSignal(x);
            ySpin.SetValueNoSignal(y);
            // 缺省值也写回 params，避免表单未改动时校验报"缺少字段"
            _cue.SetInt("x", x);
            _cue.SetInt("y", y);
            xSpin.ValueChanged += v => { _cue.SetInt("x", (int)v); ParamChanged?.Invoke(); };
            ySpin.ValueChanged += v => { _cue.SetInt("y", (int)v); ParamChanged?.Invoke(); };
            _spinners.Add((xSpin, v => _cue.SetInt("x", (int)v)));
            _spinners.Add((ySpin, v => _cue.SetInt("y", (int)v)));
            row.AddChild(new Label { Text = "x" });
            row.AddChild(xSpin);
            row.AddChild(new Label { Text = "y" });
            row.AddChild(ySpin);
            var pickBtn = new Button { Text = "拾取" };
            pickBtn.TooltipText = "进入拾取模式：在右侧地图上点击一个格子填入坐标（右键取消）";
            pickBtn.Pressed += () =>
            {
                PickRequested?.Invoke(pos =>
                {
                    xSpin.SetValueNoSignal(pos.X);
                    ySpin.SetValueNoSignal(pos.Y);
                    _cue.SetInt("x", pos.X);
                    _cue.SetInt("y", pos.Y);
                    ParamChanged?.Invoke();
                });
            };
            row.AddChild(pickBtn);
        }

        /// <summary>camera_focus 专用：坐标可选（不带坐标则聚焦 actor）；用单个"镜头值"复合整数填 x/y/缩放</summary>
        private void AddCameraFocusCoordField()
        {
            var row = AddRow("目标方式");
            var check = new CheckBox { Text = "使用坐标（否则聚焦 actor）" };
            check.SetPressedNoSignal(_cue.Has("x") && _cue.Has("y"));
            row.AddChild(check);

            var codeRow = AddRow("镜头值");
            var edit = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var hint = new Label();
            void Refresh(int v)
            {
                CutsceneCameraCode.Decode(v, out int x, out int y, out float zoom);
                _cue.SetInt("x", x);
                _cue.SetInt("y", y);
                _cue.SetFloat("zoom", zoom);
                hint.Text = $"(x={x}, y={y}, zoom={zoom:0.##})";
            }
            edit.TextChanged += text =>
            {
                if (int.TryParse(text, out int v)) { Refresh(v); ParamChanged?.Invoke(); }
            };
            edit.Visible = hint.Visible = check.ButtonPressed;
            codeRow.AddChild(edit);
            codeRow.AddChild(hint);

            var pickBtn = new Button { Text = "拾取", Visible = check.ButtonPressed };
            pickBtn.TooltipText = "进入拾取模式：在右侧地图点一个格子填坐标（保留当前缩放）";
            pickBtn.Pressed += () =>
            {
                PickRequested?.Invoke(pos =>
                {
                    float zoom = _cue.GetFloat("zoom", 1.5f);
                    _cue.SetInt("x", pos.X);
                    _cue.SetInt("y", pos.Y);
                    edit.Text = CutsceneCameraCode.Encode(pos.X, pos.Y, zoom).ToString();
                    Refresh(int.Parse(edit.Text));
                    ParamChanged?.Invoke();
                });
            };
            codeRow.AddChild(pickBtn);

            if (check.ButtonPressed)
            {
                edit.Text = CutsceneCameraCode.Encode(_cue.GetInt("x"), _cue.GetInt("y"), _cue.GetFloat("zoom", 1.5f)).ToString();
                Refresh(int.Parse(edit.Text));
            }

            check.Toggled += on =>
            {
                edit.Visible = hint.Visible = pickBtn.Visible = on;
                if (on)
                {
                    if (!int.TryParse(edit.Text, out int v))
                        v = CutsceneCameraCode.Encode(0, 0, _cue.GetFloat("zoom", 1.5f));
                    Refresh(v);
                }
                else
                {
                    _cue.RemoveParam("x");
                    _cue.RemoveParam("y");
                }
                ParamChanged?.Invoke();
            };
        }

        /// <summary>camera_set 专用：单个"镜头值"复合整数字段（含 x/y/缩放），可直接从预览右上角粘贴</summary>
        private void AddCameraValueField()
        {
            var row = AddRow("镜头值");
            var edit = new LineEdit
            {
                Text = CutsceneCameraCode.Encode(_cue.GetInt("x"), _cue.GetInt("y"), _cue.GetFloat("zoom", 0f)).ToString(),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2I(220, 0),
            };
            edit.TooltipText = "镜头机位复合整数（x/y/缩放 混合），可从预览右上角镜头值复制粘贴";
            row.AddChild(edit);

            // 解码提示单独占一行，让镜头值输入框占满整行宽度（向左延伸）
            var hint = new Label();
            void Refresh(int v)
            {
                CutsceneCameraCode.Decode(v, out int x, out int y, out float zoom);
                _cue.SetInt("x", x);
                _cue.SetInt("y", y);
                _cue.SetFloat("zoom", zoom);
                hint.Text = $"= (x={x}, y={y}, zoom={zoom:0.##}{(zoom == 0 ? " 不变" : "")})";
            }
            edit.TextChanged += text =>
            {
                if (int.TryParse(text, out int v)) { Refresh(v); ParamChanged?.Invoke(); }
            };
            Refresh(int.Parse(edit.Text));

            var hintRow = AddRow("");
            hintRow.AddChild(hint);
        }

        /// <summary>actor 字段：下拉选项来自演员表（id + 类型/名字），写入纯数字 id。可选时带"无"。</summary>
        private void AddActorField(bool optional)
        {
            var row = AddRow(optional ? "actor(可选)" : "actor(必填)");
            var opt = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };

            // 当前已选 id：cue.actor 是纯数字 id 字符串（默认值 -1 即"无"）
            int GetCurrentId()
                => (!string.IsNullOrEmpty(_cue.Actor) && int.TryParse(_cue.Actor, out int cur)) ? cur : -1;

            // 从当前演员表重新构建选项；保留外部传入的已选 id。
            void RebuildOptions()
            {
                CutsceneActorCatalog.Load(); // 刷新怪物名表，避免回退成"怪物0"
                int keep = GetCurrentId();
                opt.Clear();
                opt.AddItem("（无）", -1);
                foreach (var a in _actors)
                {
                    string label = a.type switch
                    {
                        1 => $"{a.id}（玩家）",
                        2 => $"{a.id}（{CutsceneActorCatalog.ResolveMonsterName(a.monsterConfigId)}）",
                        _ => $"{a.id}（类型{a.type}）",
                    };
                    opt.AddItem(label, a.id);
                }

                int idx = opt.GetItemIndex(keep);
                opt.Select(idx >= 0 ? idx : 0);
            }

            RebuildOptions();

            // 每次下拉打开时重新同步演员表（演员表增删/改怪物配置后无需重建整个表单即可生效）。
            // 注：4.6 的 OptionButton/PopupMenu 未暴露 AboutToPopup 事件，改用引擎级 about_to_popup 信号动态连接。
            opt.GetPopup().Connect("about_to_popup", Callable.From(RebuildOptions));

            opt.ItemSelected += _ =>
            {
                int id = opt.GetSelectedId();
                _cue.actor = id >= 0 ? id.ToString() : "";
                ParamChanged?.Invoke();
            };
            row.AddChild(opt);
        }
    }
}
