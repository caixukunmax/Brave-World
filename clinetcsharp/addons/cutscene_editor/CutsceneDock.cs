using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ClinetCSharp.Cutscene;

namespace ClinetCSharp
{
    /// <summary>
    /// 演出编辑器主面板（挂在编辑器底部 Dock）。
    /// 第 1 列：演出切换（浓缩一行：下拉 + ⚙ 管理入口）+ cue 时间轴（步骤=执行组，组内并行、组间串行）；
    /// 第 2 列：参数表单；
    /// 第 3 列：地图视图（坐标拾取 / 触发区框选 / 走位示意）。
    ///
    /// 布局约定：根容器用 FullRect 锚点充满 Dock（本类是普通 Control 不是容器，
    /// 子节点 SizeFlags 不生效，不锚会停留在最小尺寸、周围全是死黑区）；
    /// 三列固定紧凑最小宽度，多余横向空间全部留给第 3 列；
    /// 地图视口经 AspectRatioContainer 锁游戏视口宽高比（display/window/size），
    /// 等比缩放 + 多余空间留边，保证预览构图与游戏内镜头构图一致。
    ///
    /// 时间轴模型：插件内部把 (seq, group) 执行组称为"步骤"。步骤即并行组——
    /// 同一步骤内的 cue 并行，步骤之间串行。作者不手写 seq/group 数字：
    /// 任何结构改动后由 ApplySteps 统一重排为 seq=步骤号、group=0，
    /// 与运行时 BuildExecutionGroups 的语义一一对应。
    /// </summary>
    [Tool]
    public partial class CutsceneDock : Control
    {
        private const string CutsceneDir = "res://data/cutscenes/";

        // ============ 数据状态 ============
        private CutsceneScript _script;
        private string _currentFileName;            // 形如 "1.json"
        private bool _dirty;
        private CutsceneCue _selectedCue;
        private readonly HashSet<int> _errorSeqs = new();  // 校验失败的步骤号（标红用）
        private List<string> _cutsceneFiles = new();

        // ============ UI 引用 ============
        private OptionButton _cutsceneOption;
        private PopupPanel _managePanel;
        private CheckBox _letterboxCheck;
        private CheckBox _skippableCheck;
        private CheckBox _triggerEnableCheck;
        private VBoxContainer _triggerFields;
        private OptionButton _triggerMapOption;
        private Label _triggerRectLabel;
        private CheckBox _triggerOnceCheck;
        private Tree _tree;
        private Button _actorHeader;          // 演员表折叠入口（只占一行）
        private VBoxContainer _actorPanel;    // 点击展开后的演员表编辑面板（单向展开不收起）
        private CueParamForm _form;
        private Label _status;
        private CutsceneMapView _mapView;
        private CutsceneMapRenderer _renderer;
        private SubViewport _sub;
        private Camera2D _camera;
        private OptionButton _viewMapOption;
        private Label _camPosLabel;
        private string _camPosText = "";
        // 游戏视口宽高比（_Ready 时读 project.godot display/window/size），地图预览锁比例用
        private float _gameAspect = 16f / 9f;

        // 运行时同款地图渲染（§5.5）：GridManager（GPU shader 地形/网格/图外灰）+ 真实 MapDecoration 装饰层
        private GridManager _grid;
        private Node2D _decorationLayer;
        private Dictionary<int, EntityProfile> _profiles = new();
        private string _decorationSignature = "";

        private List<string> _mapNames = new();
        private Action<Vector2I> _pickCallback;

        // 添加 cue 内联工具栏（替代弹窗）的状态。
        // 注意：必须与 CutsceneScriptIO.KnownCueTypes 保持一致（第一期的真实类型），
        // 否则会选到运行时不认识、表单也不渲染的过时类型（如旧 say/text）。
        private static readonly string[] _cueTypes =
            { "wait", "story_play", "move", "face", "camera_focus", "camera_set", "camera_reset", "camera_shake", "fade", "actor_enter", "actor_leave", "sfx", "bgm" };
        private OptionButton _addTypeOpt;
        private OptionButton _addModeOpt;
        private Label _addStepLabel;
        private SpinBox _addStepSpin;
        private bool _addStepTouched;

        // ============ 模拟预览（§5.4） ============
        private CutscenePreviewPlayer _previewPlayer;
        private Button _playBtn;
        private Button _pauseBtn;
        private Button _stopBtn;
        private Button _stepBtn;
        private bool _previewActive;

        /// <summary>预览播放器（冒烟测试/自动化驱动用）</summary>
        public CutscenePreviewPlayer PreviewPlayer => _previewPlayer;

        // SubViewport 尺寸同步（容器构建时只有 2x2，必须等 Resized 拿到真实尺寸再同步，见 AGENTS.md）
        private bool _subSizeSynced;

        public override void _Ready()
        {
            // 游戏视口宽高比：锁地图预览比例用（与运行时画面构图一致）
            int gw = ProjectSettings.GetSetting("display/window/size/viewport_width", 1280).AsInt32();
            int gh = ProjectSettings.GetSetting("display/window/size/viewport_height", 720).AsInt32();
            if (gw > 0 && gh > 0) _gameAspect = (float)gw / gh;

            // Dock 本体在两个方向吃满 EditorDock 分到的区域（兜底：EditorDock 若非单子容器也能撑满）
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;
            // 裁剪越界内容：Dock 高度小于内容最小高度时子节点会向下溢出，
            // 盖住 Godot 底部“输出/调试器/音频/动画”标签栏并抢走点击
            ClipContents = true;

            // 怪物表（演员表怪物下拉用）：只读 res://data/monster_config.json，编辑器安全
            CutsceneActorCatalog.Load();
            // 地形颜色来自 Luban 导出的 JSON，编辑器安全（同地图编辑器插件的做法）
            TerrainConfigUtil.Load();
            // 装饰/角色外观配置：编辑器安全纯数据 I/O（同 addons/map_editor_editor 的做法，§5.5）
            _profiles = ProfileConfigIO.LoadFromFile();
            DecorationConfigUtil.LoadFromProfiles(_profiles);

            BuildUi();
            RefreshCutsceneList();
            RefreshMapList();

            if (_cutsceneFiles.Count > 0)
            {
                _cutsceneOption.Select(0);
                LoadCutscene(_cutsceneFiles[0]);
            }
            else
            {
                RebuildAll();
                SetStatus("data/cutscenes 下暂无演出，点 ⚙ → 新建 创建");
            }

            if (_mapNames.Count > 0)
                LoadViewMap(_mapNames[0]);
        }

        public override void _ExitTree()
        {
            // Dock 销毁时兜底停止预览，避免 Tween/令牌残留到已释放节点
            _previewPlayer?.StopPreview();
        }

        public override void _Process(double delta)
        {
            // 镜头中心世界坐标 -> 复合镜头值（x/y/缩放 混合整数），仅变化时刷文本
            if (_camera == null || _camPosLabel == null) return;
            int value = CutsceneCameraCode.EncodeFromCamera(_camera, CutsceneMapRenderer.GridSize);
            string text = value.ToString();
            if (text == _camPosText) return;
            _camPosText = text;
            CutsceneCameraCode.Decode(value, out int cx, out int cy, out float cz);
            _camPosLabel.Text = $"镜头值 {value}";
            _camPosLabel.TooltipText = $"镜头中心 ({cx}, {cy})，缩放 {cz:0.##}\n点击复制，可粘贴到 cue 镜头值";
        }

        /// <summary>Ctrl+S 保存当前演出（当焦点在面板内时触发）。</summary>
        public override void _ShortcutInput(InputEvent @event)
        {
            if (@event is InputEventKey key
                && key.Pressed
                && !key.Echo
                && key.Keycode == Key.S
                && key.CtrlPressed
                && !key.AltPressed
                && !key.MetaPressed)
            {
                OnSave();
                GetViewport()?.SetInputAsHandled();
            }
        }

        // ============ UI 构建 ============

        private void BuildUi()
        {
            var root = new HBoxContainer();
            // 本类是普通 Control 而非容器，子节点 SizeFlags 不生效——根容器必须靠
            // FullRect 锚点充满 Dock，否则布局停留在最小尺寸，专注模式下周围全是死黑区
            root.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(root);

            BuildTimelinePanel(root);
            BuildParamPanel(root);
            BuildRightPanel(root);
        }

        // 演出管理弹出面板：承载除演出切换外的全部管理控件
        private void BuildManagePanel()
        {
            _managePanel = new PopupPanel();
            AddChild(_managePanel);

            var panel = new VBoxContainer();
            panel.CustomMinimumSize = new Vector2I(280, 0);
            _managePanel.AddChild(panel);

            panel.AddChild(MakeLabel("演出管理"));

            var refreshBtn = new Button { Text = "刷新列表" };
            refreshBtn.Pressed += () =>
            {
                if (BlockEditDuringPreview()) return;
                string cur = _currentFileName;
                RefreshCutsceneList();
                int idx = _cutsceneFiles.IndexOf(cur);
                if (idx >= 0) _cutsceneOption.Select(idx);
            };
            panel.AddChild(refreshBtn);

            var btnRow = new HBoxContainer();
            btnRow.AddChild(MakeButton("新建", OnNewCutscene));
            btnRow.AddChild(MakeButton("重命名", OnRenameCutscene));
            btnRow.AddChild(MakeButton("删除", OnDeleteCutscene));
            panel.AddChild(btnRow);

            var saveRow = new HBoxContainer();
            saveRow.AddChild(MakeButton("保存", OnSave));
            panel.AddChild(saveRow);

            panel.AddChild(new HSeparator());
            _letterboxCheck = new CheckBox { Text = "电影黑边 (letterbox)" };
            _letterboxCheck.Toggled += on =>
            {
                if (BlockEditDuringPreview()) { _letterboxCheck.SetPressedNoSignal(_script?.Letterbox ?? false); return; }
                if (_script != null) { _script.letterbox = on; MarkDirty(); }
            };
            panel.AddChild(_letterboxCheck);
            _skippableCheck = new CheckBox { Text = "允许 ESC 跳过 (skippable)" };
            _skippableCheck.Toggled += on =>
            {
                if (BlockEditDuringPreview()) { _skippableCheck.SetPressedNoSignal(_script?.Skippable ?? false); return; }
                if (_script != null) { _script.skippable = on; MarkDirty(); }
            };
            panel.AddChild(_skippableCheck);

            panel.AddChild(new HSeparator());
            _triggerEnableCheck = new CheckBox { Text = "启用区域触发器 (trigger)" };
            _triggerEnableCheck.Toggled += OnTriggerEnableToggled;
            panel.AddChild(_triggerEnableCheck);

            _triggerFields = new VBoxContainer();
            panel.AddChild(_triggerFields);

            BuildTriggerFields();
        }

        private void BuildTriggerFields()
        {
            // 触发地图（下拉枚举 maps/ 目录）
            var mapRow = new HBoxContainer();
            mapRow.AddChild(new Label { Text = "触发地图", CustomMinimumSize = new Vector2(80, 0) });
            _triggerMapOption = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _triggerMapOption.ItemSelected += idx =>
            {
                if (BlockEditDuringPreview()) { RebuildTriggerUi(); return; }
                if (_script?.Trigger == null || idx < 0 || idx >= _mapNames.Count) return;
                _script.Trigger.map = _mapNames[(int)idx];
                MarkDirty();
                LoadViewMap(_mapNames[(int)idx]);   // 切到触发地图方便框选
                _renderer.QueueRedraw();
            };
            mapRow.AddChild(_triggerMapOption);
            _triggerFields.AddChild(mapRow);

            // 触发矩形：显示 + 框选 + 清除
            var rectRow = new HBoxContainer();
            rectRow.AddChild(new Label { Text = "触发区", CustomMinimumSize = new Vector2(80, 0) });
            _triggerRectLabel = new Label { Text = "（未设置）", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            rectRow.AddChild(_triggerRectLabel);
            _triggerFields.AddChild(rectRow);

            var rectBtnRow = new HBoxContainer();
            var pickRectBtn = new Button { Text = "框选触发区", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            pickRectBtn.TooltipText = "在右侧地图上左键拖出矩形（右键取消）";
            pickRectBtn.Pressed += () =>
            {
                if (BlockEditDuringPreview()) return;
                if (_script?.Trigger == null) return;
                _mapView.Mode = CutsceneMapView.PickMode.Rect;
                SetStatus("框选模式：在地图上左键拖出触发矩形（右键取消）", Colors.Yellow);
            };
            rectBtnRow.AddChild(pickRectBtn);
            var clearRectBtn = new Button { Text = "清除", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            clearRectBtn.Pressed += () =>
            {
                if (BlockEditDuringPreview()) return;
                if (_script?.Trigger == null) return;
                _script.Trigger.rect = null;
                MarkDirty();
                UpdateTriggerRectLabel();
                _renderer.QueueRedraw();
            };
            rectBtnRow.AddChild(clearRectBtn);
            _triggerFields.AddChild(rectBtnRow);

            _triggerOnceCheck = new CheckBox { Text = "只播一次 (once)" };
            _triggerOnceCheck.Toggled += on =>
            {
                if (BlockEditDuringPreview()) { _triggerOnceCheck.SetPressedNoSignal(_script?.Trigger?.Once ?? false); return; }
                if (_script?.Trigger == null) return;
                _script.Trigger.once = on;
                MarkDirty();
            };
            _triggerFields.AddChild(_triggerOnceCheck);
        }

        // 第 1 列：顶部一行 = 演出切换下拉 + ⚙ 管理入口，下方是 cue 时间轴（步骤=并行组，步骤间串行）
        private void BuildTimelinePanel(Control root)
        {
            var mid = new VBoxContainer();
            mid.CustomMinimumSize = new Vector2I(400, 0);
            mid.SizeFlagsVertical = SizeFlags.ExpandFill;
            root.AddChild(mid);

            // 演出切换浓缩行：短下拉 + 小齿轮（齿轮弹出管理面板）
            var cutsceneRow = new HBoxContainer();
            _cutsceneOption = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _cutsceneOption.ItemSelected += idx =>
            {
                if (BlockEditDuringPreview())
                {
                    int cur = _cutsceneFiles.IndexOf(_currentFileName);
                    if (cur >= 0) _cutsceneOption.Select(cur);
                    return;
                }
                if (idx >= 0 && idx < _cutsceneFiles.Count)
                    LoadCutscene(_cutsceneFiles[(int)idx]);
            };
            cutsceneRow.AddChild(_cutsceneOption);
            var manageBtn = new Button { Text = "⚙" };
            manageBtn.TooltipText = "演出管理：新建/重命名/删除/保存、全局字段、区域触发器";
            manageBtn.Pressed += () => _managePanel?.PopupCentered();
            cutsceneRow.AddChild(manageBtn);
            mid.AddChild(cutsceneRow);

            // 演员表入口：只占一行，点击展开成编辑面板（§4.1 演员表 UI）
            _actorHeader = new Button { Text = "演员表 (0)", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _actorHeader.TooltipText = "点击展开演员表：演出开头声明演员（id/类型/怪物配置），后续 cue 用 id 引用";
            _actorPanel = new VBoxContainer { Visible = false };
            _actorHeader.Pressed += () =>
            {
                _actorHeader.Visible = false;
                _actorPanel.Visible = true;
                RebuildActorPanel();
            };
            mid.AddChild(_actorHeader);
            mid.AddChild(_actorPanel);

            _tree = new Tree();
            _tree.Columns = 3;
            _tree.HideRoot = true;
            _tree.AllowRmbSelect = false;
            _tree.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _tree.SizeFlagsVertical = SizeFlags.ExpandFill;
            _tree.SetColumnExpand(0, false);
            _tree.SetColumnCustomMinimumWidth(0, 130);
            _tree.SetColumnExpand(1, false);
            _tree.SetColumnCustomMinimumWidth(1, 110);
            _tree.SetColumnExpand(2, true);
            _tree.ItemSelected += OnTreeItemSelected;
            mid.AddChild(_tree);

            mid.AddChild(BuildAddCueBar());

            var opRow1 = new HBoxContainer();
            opRow1.AddChild(MakeButton("删除", OnDeleteCue));
            opRow1.AddChild(MakeButton("复制", OnDuplicateStep));
            opRow1.AddChild(MakeButton("上移", () => OnMoveStep(-1)));
            opRow1.AddChild(MakeButton("下移", () => OnMoveStep(1)));
            mid.AddChild(opRow1);

            var opRow2 = new HBoxContainer();
            var mergeBtn = MakeButton("并入上一步（并行）", OnMergeIntoPrev);
            mergeBtn.TooltipText = "把选中步骤与上一步骤合并为一个并行组";
            opRow2.AddChild(mergeBtn);
            var splitBtn = MakeButton("脱离并行组", OnDetachFromGroup);
            splitBtn.TooltipText = "把选中行从并行组中拆出，成为其后的独立步骤";
            opRow2.AddChild(splitBtn);
            mid.AddChild(opRow2);

            mid.AddChild(new HSeparator());
            _status = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
            mid.AddChild(_status);

            BuildManagePanel();
        }

        // 第 2 列：参数表单
        private void BuildParamPanel(Control root)
        {
            var paramCol = new VBoxContainer();
            // 固定紧凑宽度（不随窗口拉伸）：360 覆盖"标签 150 + 输入框"的常见行，减少横向滚动条
            paramCol.CustomMinimumSize = new Vector2I(360, 0);
            paramCol.SizeFlagsVertical = SizeFlags.ExpandFill;
            root.AddChild(paramCol);

            paramCol.AddChild(MakeLabel("参数"));
            var formScroll = new ScrollContainer();
            formScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            formScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            _form = new CueParamForm { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _form.ParamChanged += OnParamChanged;
            _form.PickRequested += OnPickRequested;
            formScroll.AddChild(_form);
            paramCol.AddChild(formScroll);
        }

        private void BuildRightPanel(Control root)
        {
            var right = new VBoxContainer();
            right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            right.SizeFlagsVertical = SizeFlags.ExpandFill;
            root.AddChild(right);

            var mapRow = new HBoxContainer();
            mapRow.AddChild(new Label { Text = "视图地图" });
            _viewMapOption = new OptionButton();
            _viewMapOption.ItemSelected += idx =>
            {
                // 预览中切地图会让令牌/镜头坐标系错乱：拦截并回退下拉选择
                if (BlockEditDuringPreview())
                {
                    int cur = _mapNames.IndexOf(_renderer.CurrentMapName);
                    if (cur >= 0) _viewMapOption.Select(cur);
                    return;
                }
                if (idx >= 0 && idx < _mapNames.Count)
                    LoadViewMap(_mapNames[(int)idx]);
            };
            mapRow.AddChild(_viewMapOption);
            mapRow.AddChild(new Label { Text = "  左键拖拽平移 / 滚轮缩放 / 右键取消拾取" });

            // 镜头当前机位复合值（x/y/缩放 混合整数），点击复制到剪贴板，可粘贴进 cue 镜头值
            var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            mapRow.AddChild(spacer);
            _camPosLabel = new Label { Text = "镜头值 —", MouseFilter = MouseFilterEnum.Stop };
            _camPosLabel.TooltipText = "镜头机位复合值（x/y/缩放 混合整数），点击复制";
            _camPosLabel.GuiInput += e =>
            {
                if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click
                    && !click.DoubleClick && !string.IsNullOrEmpty(_camPosText))
                {
                    DisplayServer.ClipboardSet(_camPosText);
                    SetStatus($"已复制镜头值 {_camPosText}");
                    GetViewport()?.SetInputAsHandled();
                }
            };
            mapRow.AddChild(_camPosLabel);
            right.AddChild(mapRow);

            // 模拟预览控制条（§5.4）：播放 / 暂停 / 停止 / 单步（单步=前进一个执行组）
            var previewRow = new HBoxContainer();
            previewRow.AddChild(MakeLabel("模拟预览"));
            _playBtn = MakeButton("▶ 播放", () => _previewPlayer?.Play());
            _playBtn.TooltipText = "从头播放整场演出（暂停中则继续）";
            previewRow.AddChild(_playBtn);
            _pauseBtn = MakeButton("⏸ 暂停", () => _previewPlayer?.PausePreview());
            previewRow.AddChild(_pauseBtn);
            _stopBtn = MakeButton("⏹ 停止", () => _previewPlayer?.StopPreview());
            _stopBtn.TooltipText = "停止预览并还原视图机位";
            previewRow.AddChild(_stopBtn);
            _stepBtn = MakeButton("⏭ 单步", () => _previewPlayer?.StepOnce());
            _stepBtn.TooltipText = "前进一个执行组（步骤）后暂停";
            previewRow.AddChild(_stepBtn);
            right.AddChild(previewRow);

            // 地图视口锁游戏画面宽高比：FIT 等比适配 + 水平居中/垂直顶对齐，
            // 放不下的空间留边而不是拉伸视口，预览构图 = 游戏内镜头构图
            var aspectWrap = new AspectRatioContainer
            {
                Ratio = _gameAspect,
                StretchMode = AspectRatioContainer.StretchModeEnum.Fit,
                AlignmentHorizontal = AspectRatioContainer.AlignmentMode.Center,
                AlignmentVertical = AspectRatioContainer.AlignmentMode.Begin,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
            };
            right.AddChild(aspectWrap);

            _mapView = new CutsceneMapView { GameAspect = _gameAspect };
            _mapView.MouseFilter = MouseFilterEnum.Stop;
            _mapView.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _mapView.SizeFlagsVertical = SizeFlags.ExpandFill;
            aspectWrap.AddChild(_mapView);

            // SubViewport：不用 Stretch（容器构建时 2x2 会塌缩渲染目标导致全黑，见 AGENTS.md），
            // 改为监听容器 Resized 手动同步尺寸。初始尺寸给小值，容器首次 Resized 即被真实尺寸覆盖
            // （给大初始值会把容器最小尺寸顶大、把整个 Dock 挤出屏幕）。
            _sub = new SubViewport();
            _sub.Size = new Vector2I(640, 360);
            _sub.Size2DOverride = Vector2I.Zero;
            _sub.Size2DOverrideStretch = false;
            _sub.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _mapView.AddChild(_sub);
            _mapView.Resized += OnMapViewResized;

            // 地图渲染复用运行时 GridManager（GPU shader 地形/网格/图外灰，GridSize=111）+
            // 真实 MapDecoration 装饰层（§5.5），与游戏运行时画面 1:1；
            // CutsceneMapRenderer 退化为纯叠加层，只画演出示意
            _grid = new GridManager { SkipAutoLoad = true };
            _sub.AddChild(_grid);

            _decorationLayer = new Node2D();
            _sub.AddChild(_decorationLayer);

            _renderer = new CutsceneMapRenderer();
            _sub.AddChild(_renderer);

            _camera = new Camera2D();
            _sub.AddChild(_camera);

            _mapView.Sub = _sub;
            _mapView.Camera = _camera;
            _mapView.Renderer = _renderer;
            _mapView.CellPicked += OnCellPicked;
            _mapView.RectPicked += OnRectPicked;
            _mapView.PickCancelled += () =>
            {
                _pickCallback = null;
                SetStatus("已取消拾取/框选");
            };

            // 模拟预览播放器：叠在地图视图上的透明覆盖层（MouseFilter=Ignore，不挡平移/缩放）
            _previewPlayer = new CutscenePreviewPlayer();
            _mapView.AddChild(_previewPlayer);
            _previewPlayer.Initialize(
                _sub, _camera, _renderer,
                () => _script, LoadViewMap, (msg, c) => SetStatus(msg, c),
                HighlightPreviewStep, OnPreviewActiveChanged);
            _previewPlayer.StateChanged += UpdatePreviewButtons;
            UpdatePreviewButtons();
        }

        // ============ 模拟预览（§5.4） ============

        /// <summary>时间轴高亮当前执行的步骤（选中该步骤第一行并滚动过去），-1 = 取消高亮</summary>
        private void HighlightPreviewStep(int stepIndex)
        {
            var root = _tree.GetRoot();
            if (root == null) return;
            _tree.DeselectAll();
            if (stepIndex < 0) return;

            int i = 0;
            for (var stepItem = root.GetFirstChild(); stepItem != null; stepItem = stepItem.GetNext(), i++)
            {
                if (i != stepIndex) continue;
                var cueItem = stepItem.GetFirstChild();
                if (cueItem != null)
                {
                    cueItem.Select(0);
                    _tree.ScrollToItem(cueItem);
                }
                return;
            }
        }

        /// <summary>预览开始/结束：切换时间轴与参数表单的只读状态（防串改）</summary>
        private void OnPreviewActiveChanged(bool active)
        {
            _previewActive = active;
            // 预览时锁定地图视图的手动相机操作（滚轮缩放/平移/拾取），仅对白点击继续
            _mapView.IsPreviewing = active;
            // 表单整块禁鼠标（键盘已聚焦字段的边界情况由 OnParamChanged 守卫兜底）
            _form.MouseFilter = active ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
            if (!active)
                _tree.DeselectAll();
        }

        private void UpdatePreviewButtons()
        {
            if (_playBtn == null || _previewPlayer == null) return;
            bool active = _previewPlayer.IsActive;
            bool playing = _previewPlayer.State == CutscenePreviewPlayer.PreviewState.Playing;
            _playBtn.Disabled = playing;
            _pauseBtn.Disabled = !playing;
            _stopBtn.Disabled = !active;
            _stepBtn.Disabled = playing;
        }

        /// <summary>预览期间禁止编辑：统一黄字提示，返回 true 表示已拦截（§5.4 防串改）</summary>
        private bool BlockEditDuringPreview()
        {
            if (!_previewActive) return false;
            SetStatus("预览期间禁止编辑，请先停止预览", Colors.Yellow);
            return true;
        }

        private void OnMapViewResized()
        {
            if (_sub == null || _mapView == null) return;
            var s = _mapView.Size;
            if (s.X < 8 || s.Y < 8) return; // 忽略布局前的塌缩尺寸
            _sub.Size = (Vector2I)s;
            if (!_subSizeSynced)
            {
                _subSizeSynced = true;
                GD.Print($"[CutsceneEditor] SubViewport 尺寸已同步容器: {_sub.Size}");
            }
        }

        // ============ 演出管理 ============

        private void RefreshCutsceneList()
        {
            _cutsceneFiles.Clear();
            if (!DirAccess.DirExistsAbsolute(CutsceneDir))
                DirAccess.MakeDirRecursiveAbsolute(CutsceneDir);

            var dir = DirAccess.Open(CutsceneDir);
            if (dir != null)
            {
                dir.ListDirBegin();
                string fileName = dir.GetNext();
                while (fileName != "")
                {
                    if (!dir.CurrentIsDir() && fileName.EndsWith(".json"))
                        _cutsceneFiles.Add(fileName);
                    fileName = dir.GetNext();
                }
                dir.ListDirEnd();
            }
            // 按文件名的数字 id 排序（无法解析的排最后）
            _cutsceneFiles.Sort((a, b) => FileIdOf(a).CompareTo(FileIdOf(b)));

            _cutsceneOption.Clear();
            foreach (var fileName in _cutsceneFiles)
            {
                string display = fileName;
                try
                {
                    using var f = FileAccess.Open(CutsceneDir + fileName, FileAccess.ModeFlags.Read);
                    if (f != null)
                    {
                        var s = CutsceneScriptIO.Parse(f.GetAsText());
                        display = $"{s.Id} - {s.Name}";
                    }
                }
                catch (CutsceneScriptException)
                {
                    display = fileName + "（解析失败）";
                }
                catch (Exception ex)
                {
                    // 读取/反序列化的非预期异常也不能静默（否则列表直接空白且无提示）
                    GD.PushError($"[CutsceneEditor] 读取 {fileName} 失败: {ex}");
                    display = fileName + "（读取失败）";
                }
                _cutsceneOption.AddItem(display);
            }
        }

        private static int FileIdOf(string fileName)
        {
            string baseName = fileName.Substring(0, fileName.Length - ".json".Length);
            return int.TryParse(baseName, out int id) ? id : int.MaxValue;
        }

        private void LoadCutscene(string fileName)
        {
            if (BlockEditDuringPreview()) return;
            string path = CutsceneDir + fileName;
            using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (f == null)
            {
                SetStatus($"打不开文件：{path}", Colors.Red);
                return;
            }

            CutsceneScript script;
            try
            {
                script = CutsceneScriptIO.Parse(f.GetAsText());
            }
            catch (CutsceneScriptException ex)
            {
                SetStatus($"解析失败：{ex.Message}", Colors.Red);
                return;
            }
            catch (Exception ex)
            {
                SetStatus($"读取失败：{ex.Message}", Colors.Red);
                GD.PushError($"[CutsceneEditor] LoadCutscene({fileName}) 异常: {ex}");
                return;
            }

            _script = script;
            _currentFileName = fileName;
            _selectedCue = null;
            _errorSeqs.Clear();
            ResetActorPanelUI();
            NormalizeAfterLoad();
            _dirty = false;
            RebuildAll();

            // 有触发器时切到触发地图，方便直接框选
            if (_script.Trigger != null && !string.IsNullOrEmpty(_script.Trigger.Map)
                && _mapNames.Contains(_script.Trigger.Map))
            {
                LoadViewMap(_script.Trigger.Map);
            }

            SetStatus($"已加载：{_currentFileName}");
        }

        private void OnNewCutscene()
        {
            if (BlockEditDuringPreview()) return;
            // 分配最小未占用 id（id 与文件名一致由插件保证）
            var used = new HashSet<int>();
            foreach (var fn in _cutsceneFiles)
            {
                int id = FileIdOf(fn);
                if (id != int.MaxValue) used.Add(id);
            }
            int newId = 1;
            while (used.Contains(newId)) newId++;

            var script = new CutsceneScript { id = newId, name = "新演出", cues = new List<CutsceneCue>() };
            string fileName = newId + ".json";
            if (!WriteScriptFile(fileName, script, out string error))
            {
                SetStatus($"新建失败：{error}", Colors.Red);
                return;
            }

            RefreshCutsceneList();
            int idx = _cutsceneFiles.IndexOf(fileName);
            if (idx >= 0) _cutsceneOption.Select(idx);
            LoadCutscene(fileName);
            SetStatus($"已新建演出 {newId}");
        }

        private void OnRenameCutscene()
        {
            if (BlockEditDuringPreview()) return;
            if (_script == null) return;
            ShowTextDialog("重命名演出", _script.Name, newName =>
            {
                if (string.IsNullOrWhiteSpace(newName) || newName == _script.Name) return;
                _script.name = newName;
                MarkDirty();
                SetStatus($"已重命名为「{newName}」（保存后生效）");
            });
        }

        private void OnDeleteCutscene()
        {
            if (BlockEditDuringPreview()) return;
            if (_script == null || string.IsNullOrEmpty(_currentFileName)) return;
            string fileName = _currentFileName;
            var d = new ConfirmationDialog();
            d.Title = "删除演出";
            d.DialogText = $"确定删除演出 '{fileName}'（{_script.Name}）？此操作不可恢复。";
            d.Confirmed += () =>
            {
                var err = DirAccess.RemoveAbsolute(CutsceneDir + fileName);
                if (err != Error.Ok)
                {
                    SetStatus($"删除失败：{err}", Colors.Red);
                }
                else
                {
                    _script = null;
                    _currentFileName = null;
                    _selectedCue = null;
                    RefreshCutsceneList();
                    RebuildAll();
                    if (_cutsceneFiles.Count > 0)
                    {
                        _cutsceneOption.Select(0);
                        LoadCutscene(_cutsceneFiles[0]);
                    }
                    SetStatus($"已删除：{fileName}");
                }
                d.QueueFree();
            };
            d.Canceled += () => d.QueueFree();
            AddChild(d);
            d.PopupCentered();
        }

        private void OnSave()
        {
            if (_script == null || string.IsNullOrEmpty(_currentFileName))
            {
                SetStatus("没有正在编辑的演出", Colors.Yellow);
                return;
            }

            _errorSeqs.Clear();
            // 保存前把数字框内未提交的编辑（如直接在框里键入后立刻 Ctrl+S）刷回 cue，
            // 否则 SpinBox 未失去焦点、value_changed 未触发，会保存成旧值。
            _form?.Commit();
            var errors = CutsceneScriptIO.Validate(_script, _currentFileName);
            if (errors.Count > 0)
            {
                // 从错误串里提取 "seq=N"，把对应步骤的行标红
                foreach (var e in errors)
                {
                    var m = Regex.Match(e, @"seq=(\d+)");
                    if (m.Success && int.TryParse(m.Groups[1].Value, out int seq))
                        _errorSeqs.Add(seq);
                }
                RebuildTree();
                ShowErrorDialog(errors);
                SetStatus($"校验失败：{errors.Count} 处错误，未保存", Colors.Red);
                return;
            }

            if (!WriteScriptFile(_currentFileName, _script, out string writeError))
            {
                SetStatus($"保存失败：{writeError}", Colors.Red);
                return;
            }

            _dirty = false;
            _errorSeqs.Clear();
            RebuildTree();
            RefreshCutsceneList();
            int idx = _cutsceneFiles.IndexOf(_currentFileName);
            if (idx >= 0) _cutsceneOption.Select(idx);
            SetStatus($"已保存：{_currentFileName}（游戏内 cutscene,{_script.Id} 热重载生效）");
        }

        private bool WriteScriptFile(string fileName, CutsceneScript script, out string error)
        {
            error = null;
            string text;
            try
            {
                text = CutsceneScriptIO.Serialize(script);
            }
            catch (Exception ex)
            {
                // 序列化失败（如属性撞名）必须可见，不能只靠 PushError
                error = $"序列化失败：{ex.Message}";
                GD.PushError($"[CutsceneEditor] {error}");
                return false;
            }

            using var f = FileAccess.Open(CutsceneDir + fileName, FileAccess.ModeFlags.Write);
            if (f == null)
            {
                error = $"打不开文件（err={FileAccess.GetOpenError()}）：{CutsceneDir + fileName}";
                GD.PushError($"[CutsceneEditor] {error}");
                return false;
            }
            f.StoreString(text);
            return true;
        }

        // ============ 触发器 ============

        private void OnTriggerEnableToggled(bool on)
        {
            if (BlockEditDuringPreview())
            {
                _triggerEnableCheck.SetPressedNoSignal(_script?.Trigger != null);
                return;
            }
            if (_script == null) return;
            if (on)
            {
                _script.trigger ??= new CutsceneTriggerConfig();
                if (string.IsNullOrEmpty(_script.Trigger.map) && _mapNames.Count > 0)
                    _script.Trigger.map = _mapNames[0];
                _script.Trigger.rect ??= new[] { 0, 0, 1, 1 };
            }
            else
            {
                _script.trigger = null;
            }
            MarkDirty();
            RebuildTriggerUi();
            _renderer.QueueRedraw();
        }

        private void RebuildTriggerUi()
        {
            bool hasTrigger = _script?.Trigger != null;
            _triggerEnableCheck.SetPressedNoSignal(hasTrigger);
            _triggerFields.Visible = hasTrigger;
            if (!hasTrigger) return;

            _triggerMapOption.Clear();
            int selected = 0;
            for (int i = 0; i < _mapNames.Count; i++)
            {
                _triggerMapOption.AddItem(_mapNames[i]);
                if (_mapNames[i] == _script.Trigger.Map) selected = i;
            }
            if (_mapNames.Count > 0) _triggerMapOption.Select(selected);
            _triggerOnceCheck.SetPressedNoSignal(_script.Trigger.Once);
            UpdateTriggerRectLabel();
        }

        private void UpdateTriggerRectLabel()
        {
            var rect = _script?.Trigger?.Rect;
            _triggerRectLabel.Text = rect != null && rect.Length == 4
                ? $"[{rect[0]}, {rect[1]}, {rect[2]}, {rect[3]}]"
                : "（未设置）";
        }

        // ============ 时间轴模型（步骤 = 执行组） ============

        /// <summary>把 flat cue 列表按 seq 分组成步骤（列表已保证按执行顺序排列）</summary>
        private List<List<CutsceneCue>> GetSteps()
        {
            var steps = new List<List<CutsceneCue>>();
            if (_script == null) return steps;
            foreach (var cue in _script.Cues)
            {
                if (steps.Count == 0 || steps[steps.Count - 1][0].Seq != cue.Seq)
                    steps.Add(new List<CutsceneCue> { cue });
                else
                    steps[steps.Count - 1].Add(cue);
            }
            return steps;
        }

        /// <summary>结构改动统一入口：重排 seq=步骤号、group=0 并写回 flat 列表</summary>
        private void ApplySteps(List<List<CutsceneCue>> steps)
        {
            var flat = new List<CutsceneCue>();
            for (int i = 0; i < steps.Count; i++)
            {
                foreach (var cue in steps[i])
                {
                    cue.seq = i + 1;
                    cue.group = 0;
                    flat.Add(cue);
                }
            }
            _script.Cues = flat;
            MarkDirty();
        }

        /// <summary>加载后归一化：按 (seq, group) 执行组排序后重排，语义不变</summary>
        private void NormalizeAfterLoad()
        {
            var sorted = new List<CutsceneCue>(_script.Cues);
            sorted.Sort((a, b) => a.Seq != b.Seq ? a.Seq.CompareTo(b.Seq) : a.Group.CompareTo(b.Group));

            var steps = new List<List<CutsceneCue>>();
            foreach (var cue in sorted)
            {
                var last = steps.Count > 0 ? steps[steps.Count - 1][0] : null;
                if (last == null || last.Seq != cue.Seq || last.Group != cue.Group)
                    steps.Add(new List<CutsceneCue> { cue });
                else
                    steps[steps.Count - 1].Add(cue);
            }

            var flat = new List<CutsceneCue>();
            for (int i = 0; i < steps.Count; i++)
            {
                foreach (var cue in steps[i])
                {
                    cue.seq = i + 1;
                    cue.group = 0;
                    flat.Add(cue);
                }
            }
            _script.Cues = flat;
        }

        // ============ 时间轴操作 ============

        private int SelectedStepIndex()
        {
            if (_selectedCue == null) return -1;
            var steps = GetSteps();
            for (int i = 0; i < steps.Count; i++)
                if (steps[i].Contains(_selectedCue)) return i;
            return -1;
        }

        /// <summary>构建"添加 cue"内联工具栏（替代弹窗）：类型 + 方式 + 目标步骤 + 添加按钮。</summary>
        private Control BuildAddCueBar()
        {
            var row = new HBoxContainer();

            row.AddChild(new Label { Text = "添加cue" });

            // 指令类型
            _addTypeOpt = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            foreach (var t in _cueTypes)
                _addTypeOpt.AddItem(CutsceneScriptIO.GetCueDisplayName(t));
            row.AddChild(_addTypeOpt);

            // 方式：插入为新步骤 / 并行加入步骤 / 追加为新步骤（串行，始终插到末尾）
            _addModeOpt = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _addModeOpt.AddItem("插入为新步骤");
            _addModeOpt.AddItem("并行加入步骤");
            _addModeOpt.AddItem("追加为新步骤");
            row.AddChild(_addModeOpt);

            // 目标步骤：串行=插入到该位置；并行=加入该步骤（与其中 cue 并行）
            _addStepLabel = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _addStepSpin = new SpinBox
            {
                MinValue = 1,
                Step = 1,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            _addStepSpin.ValueChanged += _ => _addStepTouched = true;
            row.AddChild(_addStepLabel);
            row.AddChild(_addStepSpin);

            var addBtn = new Button { Text = "添加", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            addBtn.Pressed += OnAddCueInline;
            row.AddChild(addBtn);

            // 切换方式时重置为默认（重新按选中行推导），并刷新标签/范围
            _addModeOpt.ItemSelected += _ =>
            {
                _addStepTouched = false;
                SyncAddStepUI();
            };

            SyncAddStepUI();
            return row;
        }

        /// <summary>
        /// 刷新目标步骤的标签/范围/默认值。
        /// 仅当用户未手填过（_addStepTouched=false）才覆盖默认值，避免选中切换时清掉用户自定义值。
        /// </summary>
        private void SyncAddStepUI()
        {
            if (_addStepSpin == null || _addModeOpt == null) return;
            int selIdx = SelectedStepIndex(); // 0-based
            int mode = _addModeOpt.Selected;
            bool parallel = mode == 1;
            bool append = mode == 2; // 追加为新步骤：始终插到末尾
            int n = GetSteps().Count;
            // 追加模式隐藏步骤输入框（没有意义），其余模式显示
            _addStepLabel.Visible = !append;
            _addStepSpin.Visible = !append;
            if (append)
            {
                _addStepSpin.Value = n + 1; // 强制末尾，AddCue 串行插入
                return;
            }
            if (parallel)
            {
                _addStepLabel.Text = "并行步骤";
                _addStepSpin.MaxValue = Math.Max(1, n);
                if (!_addStepTouched)
                    _addStepSpin.Value = selIdx >= 0 ? selIdx + 1 : 1; // 默认选中步骤
            }
            else
            {
                _addStepLabel.Text = "插入到步骤";
                _addStepSpin.MaxValue = n + 1;
                if (!_addStepTouched)
                    _addStepSpin.Value = selIdx >= 0 ? selIdx + 2 : n + 1; // 默认插到选中之后
            }
        }

        private void OnAddCueInline()
        {
            if (BlockEditDuringPreview()) return;
            if (_script == null)
            {
                SetStatus("请先新建或选择一个演出", Colors.Yellow);
                return;
            }
            if (_addTypeOpt == null || _addModeOpt == null || _addStepSpin == null) return;
            int mode = _addModeOpt.Selected;
            // 追加为新步骤：始终插到末尾，按当前步骤数实时计算，不依赖可能存在过期值的 spin
            int targetStep = mode == 2 ? GetSteps().Count + 1 : (int)_addStepSpin.Value;
            AddCue(_cueTypes[_addTypeOpt.Selected], targetStep, mode == 1);
        }

        /// <summary>弹出所有指令用法说明（"添加 cue"对话框的感叹号按钮触发）</summary>
        private void ShowCueHelp(Window owner)
        {
            var d = new Window { Title = "指令用法说明", Size = new Vector2I(520, 460) };
            var sc = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
            d.AddChild(sc);
            var lab = new Label
            {
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            var sb = new System.Text.StringBuilder();
            foreach (var t in CutsceneScriptIO.KnownCueTypes.OrderBy(t => t))
            {
                sb.AppendLine("• " + CutsceneScriptIO.GetCueDisplayName(t));
                sb.AppendLine("   " + CutsceneScriptIO.GetCueHelp(t));
                sb.AppendLine();
            }
            lab.Text = sb.ToString();
            sc.AddChild(lab);
            d.CloseRequested += () => d.QueueFree();
            owner.AddChild(d);
            d.PopupCentered();
        }

        /// <summary>
        /// 添加 cue。
        /// targetStep 为 1-based 步骤位置：
        ///  - parallel=false：在当前步骤列表的 targetStep 位置插入一个全新的串行步骤（后续步骤顺延）；
        ///  - parallel=true ：把 cue 加入第 targetStep 个步骤（与其中的 cue 并行执行）。
        /// </summary>
        private void AddCue(string type, int targetStep, bool parallel)
        {
            var steps = GetSteps();
            var cue = CreateDefaultCue(type);

            if (parallel)
            {
                int idx = Math.Clamp(targetStep - 1, 0, Math.Max(0, steps.Count - 1));
                if (steps.Count == 0) steps.Add(new List<CutsceneCue>());
                steps[idx].Add(cue);
            }
            else
            {
                int idx = Math.Clamp(targetStep - 1, 0, steps.Count);
                steps.Insert(idx, new List<CutsceneCue> { cue });
            }

            ApplySteps(steps);
            _selectedCue = cue;
            RebuildAll();
            SetStatus(parallel
                ? $"已并行加入第 {targetStep} 步"
                : $"已插入为新步骤（第 {targetStep} 步）", Colors.LightGreen);
        }

        /// <summary>新 cue 的默认参数（§4.3），保证落盘字段完整</summary>
        private CutsceneCue CreateDefaultCue(string type)
        {
            var cue = new CutsceneCue { type = type, actor = "" };
            // 默认 actor：引用演员表里第一个演员的 id（纯数字），无演员则留空
            string defaultActor = (_script != null && _script.Actors.Count > 0) ? _script.Actors[0].id.ToString() : "";

            switch (type)
            {
                case "wait": cue.SetFloat("sec", 1f); break;
                case "dialogue": cue.SetString("speaker", ""); cue.SetString("content", "……"); break;
                case "bubble": cue.actor = defaultActor; cue.SetString("text", "!"); cue.SetFloat("sec", 2f); break;
                case "move": cue.actor = defaultActor; cue.SetInt("x", 0); cue.SetInt("y", 0); cue.SetFloat("sec", 1f); break;
                case "face": cue.actor = defaultActor; cue.SetString("dir", "down"); break;
                case "camera_focus": cue.SetFloat("zoom", 1.5f); cue.SetFloat("sec", 1f); break;
                case "camera_set": cue.SetInt("x", 0); cue.SetInt("y", 0); cue.SetFloat("zoom", 0f); break;
                case "camera_reset": cue.SetFloat("sec", 1f); break;
                case "camera_shake": cue.SetFloat("strength", 8f); cue.SetFloat("sec", 0.4f); break;
                case "fade": cue.SetString("dir", "out"); cue.SetFloat("sec", 0.8f); break;
                case "actor_enter": cue.actor = defaultActor; cue.SetInt("x", 0); cue.SetInt("y", 0); break;
                case "actor_leave": cue.actor = defaultActor; break;
                case "sfx":
                case "bgm": cue.SetString("name", ""); break;
            }
            return cue;
        }

        private void OnDeleteCue()
        {
            if (BlockEditDuringPreview()) return;
            if (_selectedCue == null)
            {
                SetStatus("请先在时间轴选中一行", Colors.Yellow);
                return;
            }
            var steps = GetSteps();
            int sel = SelectedStepIndex();
            steps[sel].Remove(_selectedCue);
            if (steps[sel].Count == 0)
                steps.RemoveAt(sel);
            _selectedCue = null;
            ApplySteps(steps);
            RebuildAll();
        }

        private void OnDuplicateStep()
        {
            if (BlockEditDuringPreview()) return;
            int sel = SelectedStepIndex();
            if (sel < 0)
            {
                SetStatus("请先在时间轴选中一行（复制的是整组步骤）", Colors.Yellow);
                return;
            }
            var steps = GetSteps();
            var copy = steps[sel].Select(CloneCue).ToList();
            steps.Insert(sel + 1, copy);
            _selectedCue = copy[0];
            ApplySteps(steps);
            RebuildAll();
        }

        /// <summary>上移/下移以整组步骤为单位（组内并行的行一起动）</summary>
        private void OnMoveStep(int delta)
        {
            if (BlockEditDuringPreview()) return;
            int sel = SelectedStepIndex();
            if (sel < 0)
            {
                SetStatus("请先在时间轴选中一行（移动的是整组步骤）", Colors.Yellow);
                return;
            }
            var steps = GetSteps();
            int target = sel + delta;
            if (target < 0 || target >= steps.Count) return;
            (steps[sel], steps[target]) = (steps[target], steps[sel]);
            ApplySteps(steps);
            RebuildAll();
        }

        /// <summary>把选中步骤并入上一步骤（两组变并行）</summary>
        private void OnMergeIntoPrev()
        {
            if (BlockEditDuringPreview()) return;
            int sel = SelectedStepIndex();
            if (sel <= 0)
            {
                SetStatus(sel < 0 ? "请先在时间轴选中一行" : "第一步无法并入上一步", Colors.Yellow);
                return;
            }
            var steps = GetSteps();
            steps[sel - 1].AddRange(steps[sel]);
            steps.RemoveAt(sel);
            ApplySteps(steps);
            RebuildAll();
        }

        /// <summary>把选中行从并行组拆出，成为其后的独立步骤</summary>
        private void OnDetachFromGroup()
        {
            if (BlockEditDuringPreview()) return;
            int sel = SelectedStepIndex();
            if (sel < 0)
            {
                SetStatus("请先在时间轴选中一行", Colors.Yellow);
                return;
            }
            var steps = GetSteps();
            if (steps[sel].Count <= 1)
            {
                SetStatus("该行不在并行组中，无需拆分", Colors.Yellow);
                return;
            }
            steps[sel].Remove(_selectedCue);
            steps.Insert(sel + 1, new List<CutsceneCue> { _selectedCue });
            ApplySteps(steps);
            RebuildAll();
        }

        private static CutsceneCue CloneCue(CutsceneCue cue)
        {
            var copy = new CutsceneCue { seq = cue.Seq, group = cue.Group, type = cue.Type, actor = cue.Actor };
            foreach (var kv in cue.@params)
                copy.@params[kv.Key] = kv.Value.Clone();
            return copy;
        }

        // ============ 时间轴视图 ============

        private void RebuildTree()
        {
            _tree.Clear();
            if (_script == null) return;

            var root = _tree.CreateItem();
            var steps = GetSteps();
            int flatIndex = 0;
            for (int i = 0; i < steps.Count; i++)
            {
                var stepItem = _tree.CreateItem(root);
                stepItem.SetText(0, steps[i].Count > 1 ? $"步骤 {i + 1}（并行 ×{steps[i].Count}）" : $"步骤 {i + 1}");
                stepItem.SetSelectable(0, false);

                // 同一步骤的行同色，直观标出并行关系；不同步骤按序号轮换色相
                var bg = Color.FromHsv((i * 0.37f) % 1f, 0.45f, 0.45f, 0.30f);

                foreach (var cue in steps[i])
                {
                    var item = _tree.CreateItem(stepItem);
                    item.SetText(0, CutsceneScriptIO.GetCueDisplayName(cue.Type));
                    // 悬停显示英文 type（与 JSON 落盘字段对照用），主显示保持纯中文
                    item.SetTooltipText(0, cue.Type);
                    item.SetText(1, string.IsNullOrEmpty(cue.Actor) ? "—" : cue.Actor);
                    item.SetText(2, Summarize(cue));
                    item.SetMetadata(0, flatIndex);
                    for (int col = 0; col < 3; col++)
                        item.SetCustomBgColor(col, bg);
                    if (_errorSeqs.Contains(cue.Seq))
                        for (int col = 0; col < 3; col++)
                            item.SetCustomColor(col, Colors.Red);
                    item.SetCollapsed(false);
                    flatIndex++;
                }
                stepItem.SetCollapsed(false);
            }

            // 恢复选中
            if (_selectedCue != null)
            {
                int idx = _script.Cues.IndexOf(_selectedCue);
                if (idx >= 0)
                {
                    var item = FindItemByFlatIndex(root, idx);
                    if (item != null)
                    {
                        item.Select(0);
                        _tree.ScrollToItem(item);
                    }
                }
            }
        }

        private TreeItem FindItemByFlatIndex(TreeItem parent, int flatIndex)
        {
            for (var child = parent.GetFirstChild(); child != null; child = child.GetNext())
            {
                if (child.GetChildCount() == 0)
                {
                    if ((int)child.GetMetadata(0) == flatIndex)
                        return child;
                }
                else
                {
                    var found = FindItemByFlatIndex(child, flatIndex);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private void OnTreeItemSelected()
        {
            var item = _tree.GetSelected();
            if (item == null || _script == null) return;
            var meta = item.GetMetadata(0);
            if (meta.VariantType == Variant.Type.Nil) return;
            int idx = (int)meta;
            if (idx < 0 || idx >= _script.Cues.Count) return;

            _selectedCue = _script.Cues[idx];
            _form.BuildForm(_selectedCue, ActorDefsForForm());
            _renderer.SelectedCue = _selectedCue;
            _renderer.QueueRedraw();
            SyncAddStepUI();
        }

        private void OnParamChanged()
        {
            // 预览期间表单已禁鼠标；键盘仍聚焦在字段上的边界情况在这里兜底提示
            if (BlockEditDuringPreview()) return;
            MarkDirty();
            // 参数变了可能影响时间轴摘要与地图叠加（走位折线/落点），轻量刷新
            if (_selectedCue != null)
            {
                int idx = _script.Cues.IndexOf(_selectedCue);
                var item = FindItemByFlatIndex(_tree.GetRoot(), idx);
                if (item != null)
                {
                    item.SetText(1, string.IsNullOrEmpty(_selectedCue.Actor) ? "—" : _selectedCue.Actor);
                    item.SetText(2, Summarize(_selectedCue));
                }
            }
            _renderer.QueueRedraw();
        }

        /// <summary>参数摘要（时间轴第三列）</summary>
        private static string Summarize(CutsceneCue cue)
        {
            string DirCn(string d) => d.ToLowerInvariant() switch
            {
                "up" => "上",
                "down" => "下",
                "left" => "左",
                "right" => "右",
                _ => d,
            };
            switch (cue.Type.ToLowerInvariant())
            {
                case "wait": return $"等待 {cue.GetFloat("sec", 1f)} 秒";
                case "dialogue":
                    string speaker = cue.GetString("speaker");
                    string content = cue.GetString("content");
                    if (content.Length > 16) content = content.Substring(0, 16) + "…";
                    return (string.IsNullOrEmpty(speaker) ? "旁白" : speaker) + "：" + content;
                case "bubble": return $"{cue.GetString("text", "!")}（{cue.GetFloat("sec", 2f)} 秒）";
                case "move": return $"移动到 ({cue.GetInt("x")}, {cue.GetInt("y")})，{cue.GetFloat("sec", 1f)} 秒";
                case "face": return $"朝向 {DirCn(cue.GetString("dir", "down"))}";
                case "camera_focus":
                    return cue.Has("x")
                        ? $"聚焦到 ({cue.GetInt("x")}, {cue.GetInt("y")})，缩放 {cue.GetFloat("zoom", 1.5f)}"
                        : $"聚焦角色，缩放 {cue.GetFloat("zoom", 1.5f)}";
                case "camera_set":
                    return $"机位 ({cue.GetInt("x")}, {cue.GetInt("y")})" +
                        (cue.GetFloat("zoom", 0f) > 0f ? $"，缩放 {cue.GetFloat("zoom")}" : "");
                case "camera_reset": return $"镜头复位 {cue.GetFloat("sec", 1f)} 秒";
                case "camera_shake": return $"强度 {cue.GetFloat("strength", 8f)}，{cue.GetFloat("sec", 0.4f)} 秒";
                case "fade":
                    string dir = cue.GetString("dir", "out").ToLowerInvariant();
                    return $"{(dir == "in" ? "恢复" : "黑场")} {cue.GetFloat("sec", 0.8f)} 秒";
                case "actor_enter": return $"演员 {cue.Actor} 登场 ({cue.GetInt("x")}, {cue.GetInt("y")})";
                case "actor_leave": return $"演员 {cue.Actor} 退场";
                case "sfx": return $"音效 {cue.GetString("name")}";
                case "bgm": return $"音乐 {cue.GetString("name")}";
                default: return "";
            }
        }

        /// <summary>当前演出演员表（供 cue 表单的 actor 下拉使用）</summary>
        private List<CutsceneActorDef> ActorDefsForForm()
            => _script != null ? _script.Actors : new List<CutsceneActorDef>();

        /// <summary>演员表变更后刷新当前打开 cue 表单里的 actor 下拉，避免下拉与演员表不同步
        /// （下拉栏的标签由 _script.Actors 按 id/type 生成，而表单只在选中 cue 时 BuildForm 一次）。</summary>
        private void SyncCueFormActors()
        {
            if (_selectedCue != null && _form != null)
                _form.BuildForm(_selectedCue, ActorDefsForForm());
        }

        /// <summary>重新构建（或首次展开）演员表编辑面板（§4.1）</summary>
        private void RebuildActorPanel()
        {
            if (_actorPanel == null) return;
            UiUtils.ClearChildren(_actorPanel);
            _actorPanel.AddChild(MakeLabel("演员表（演出开头声明，后续 cue 用 id 引用）"));

            var header = new HBoxContainer();
            header.AddChild(LabelW("id", 40));
            header.AddChild(LabelW("类型", 70));
            header.AddChild(LabelW("怪物配置", 150));
            header.AddChild(LabelW("", 50));
            _actorPanel.AddChild(header);

            if (_script != null)
            {
                foreach (var a in _script.Actors)
                {
                    var row = new HBoxContainer();
                    row.AddChild(LabelW(a.id.ToString(), 40));

                    var typeOpt = new OptionButton { CustomMinimumSize = new Vector2I(70, 0) };
                    typeOpt.AddItem("玩家", 1);
                    typeOpt.AddItem("怪物", 2);
                    typeOpt.Select(a.type == 2 ? 1 : 0);
                    typeOpt.ItemSelected += _ =>
                    {
                        if (BlockEditDuringPreview()) { typeOpt.Select(a.type == 2 ? 1 : 0); return; }
                        a.type = typeOpt.GetSelectedId();
                        if (a.type != 2) a.monsterConfigId = 0;
                        MarkDirty();
                        RebuildActorPanel();
                    };
                    row.AddChild(typeOpt);

                    if (a.type == 2)
                    {
                        var monOpt = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                        foreach (var (id, name) in CutsceneActorCatalog.GetMonsterOptions())
                            monOpt.AddItem($"{id} {name}", id);
                        int sel = monOpt.GetItemIndex(a.monsterConfigId);
                        if (sel >= 0) monOpt.Select(sel);
                        else if (monOpt.ItemCount > 0) monOpt.Select(0);
                        // 关键：OptionButton.Select 不会触发 ItemSelected，需把"显示出来的默认项"同步回数据，
                        // 否则新加的怪物演员虽然下拉显示着某个怪物（如 1001），但 monsterConfigId 仍为 0，
                        // 登场 / actor 下拉会显示"怪物(0)"。仅在未设置（==0）时回写，避免覆盖已有合法值。
                        if (a.monsterConfigId == 0 && monOpt.ItemCount > 0)
                        {
                            a.monsterConfigId = monOpt.GetSelectedId();
                            MarkDirty();
                        }
                        monOpt.ItemSelected += _ =>
                        {
                            if (BlockEditDuringPreview()) { if (sel >= 0) monOpt.Select(sel); return; }
                            a.monsterConfigId = monOpt.GetSelectedId();
                            MarkDirty();
                            RebuildActorPanel();
                        };
                        row.AddChild(monOpt);
                    }
                    else
                    {
                        row.AddChild(LabelW("—", 150));
                    }

                    var del = new Button { Text = "✕", CustomMinimumSize = new Vector2I(50, 0) };
                    del.TooltipText = "删除该演员（id 不回填，已有引用不会失效）";
                    del.Pressed += () =>
                    {
                        if (BlockEditDuringPreview()) return;
                        _script.Actors.Remove(a);
                        MarkDirty();
                        RebuildActorPanel();
                        RebuildAll();
                    };
                    row.AddChild(del);

                    _actorPanel.AddChild(row);
                }
            }

            var addRow = new HBoxContainer();
            var addBtn = new Button { Text = "＋ 添加演员", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            addBtn.Pressed += () =>
            {
                if (BlockEditDuringPreview()) return;
                _script ??= new CutsceneScript();
                _script.Actors.Add(new CutsceneActorDef { id = NextActorId(), type = 1 });
                MarkDirty();
                RebuildActorPanel();
                RebuildAll();
            };
            addRow.AddChild(addBtn);
            _actorPanel.AddChild(addRow);

            if (_actorHeader != null)
                _actorHeader.Text = $"演员表 ({_script?.Actors.Count ?? 0})";

            // 演员表变更后，同步当前 cue 表单的 actor 下拉（杜绝下拉与演员表不同步）
            SyncCueFormActors();
        }

        /// <summary>演出加载/切换后刷新演员表计数与面板（若已展开）</summary>
        private void RefreshActorPanel()
        {
            if (_actorHeader != null)
                _actorHeader.Text = $"演员表 ({_script?.Actors.Count ?? 0})";
            if (_actorPanel != null && _actorPanel.Visible)
                RebuildActorPanel();
        }

        /// <summary>折叠演员表面板（每次加载/新建不同演出时调用，避免残留上一场的展开状态）</summary>
        private void ResetActorPanelUI()
        {
            if (_actorHeader != null)
            {
                _actorHeader.Visible = true;
                _actorHeader.Text = $"演员表 ({_script?.Actors.Count ?? 0})";
            }
            if (_actorPanel != null)
            {
                _actorPanel.Visible = false;
                UiUtils.ClearChildren(_actorPanel);
            }
        }

        /// <summary>下一个演员 id：当前最大 id + 1（删除不回填，保证引用稳定）</summary>
        private int NextActorId()
        {
            int max = 0;
            if (_script != null)
                foreach (var a in _script.Actors)
                    max = Math.Max(max, a.id);
            return max + 1;
        }

        private static Label LabelW(string text, int minW)
            => new() { Text = text, CustomMinimumSize = new Vector2I(minW, 0) };

        // ============ 地图视图 ============

        private void RefreshMapList()
        {
            _mapNames = MapDataManager.GetMapList();
            _viewMapOption.Clear();
            foreach (var name in _mapNames)
                _viewMapOption.AddItem(name);
            RebuildTriggerUi();
        }

        private void LoadViewMap(string mapName)
        {
            if (string.IsNullOrEmpty(mapName) || _grid == null) return;
            // 与运行时同一份地图加载路径：GridManager.LoadMap 自读 map.json（§5.5）
            if (!_grid.LoadMap(mapName))
            {
                SetStatus($"地图「{mapName}」加载失败", Colors.Red);
                return;
            }

            _renderer.GridData = _grid.GridData;
            _renderer.MapBounds = _grid.MapBounds;
            _renderer.CurrentMapName = mapName;
            _renderer.Script = _script;
            _renderer.QueueRedraw();

            RefreshDecorations();

            int idx = _mapNames.IndexOf(mapName);
            if (idx >= 0 && _viewMapOption.Selected != idx)
                _viewMapOption.Select(idx);

            // 相机居中到地图中心；默认 zoom 0.5（GridSize=111 下视野与旧 48@zoom1 相当）
            var bounds = _grid.MapBounds;
            var center = _renderer.GridToWorld(new Vector2I(
                bounds.Position.X + bounds.Size.X / 2,
                bounds.Position.Y + bounds.Size.Y / 2));
            _camera.GlobalPosition = center;
            _camera.Zoom = new Vector2(0.5f, 0.5f);
        }

        /// <summary>
        /// 用真实 MapDecoration 节点渲染装饰，与游戏运行时 1:1 一致
        /// （编辑器安全路径：ProfileConfigIO 纯数据 I/O + SetupFromProfile，同 addons/map_editor_editor 模式）。
        /// </summary>
        private void RefreshDecorations()
        {
            if (_decorationLayer == null || _grid?.GridData == null) return;

            // 仅在装饰数据变化时重建，避免无关刷新重复生成大量节点
            var sig = new System.Text.StringBuilder();
            foreach (var cell in _grid.GridData.Values)
            {
                if (cell.DecorationType != 0)
                    sig.Append(cell.Pos.X).Append(',').Append(cell.Pos.Y).Append(':').Append(cell.DecorationType).Append(';');
            }
            string signature = sig.ToString();
            if (signature == _decorationSignature) return;
            _decorationSignature = signature;

            foreach (var c in _decorationLayer.GetChildren().ToArray())
                c.QueueFree();

            var occupied = new HashSet<Vector2I>();
            int gs = _grid.GridSize;
            foreach (var cell in _grid.GridData.Values)
            {
                if (cell.DecorationType == 0) continue;
                if (occupied.Contains(cell.Pos)) continue;
                if (!_profiles.TryGetValue(cell.DecorationType, out var profile) || profile == null)
                    continue;

                var dec = new MapDecoration();
                dec.IsEditable = false;
                dec.SetProcessInput(false);
                dec.SetProcess(false);
                dec.SetupFromProfile(profile, registerInDecorationGroup: false, gridX: cell.Pos.X, gridY: cell.Pos.Y, gridSize: gs);
                _decorationLayer.AddChild(dec);

                int sx = dec.SizeX, sy = dec.SizeY;
                for (int dx = 0; dx < sx; dx++)
                    for (int dy = 0; dy < sy; dy++)
                        occupied.Add(new Vector2I(cell.Pos.X + dx, cell.Pos.Y + dy));
            }
        }

        private void OnPickRequested(Action<Vector2I> callback)
        {
            if (BlockEditDuringPreview()) return;
            _pickCallback = callback;
            _mapView.Mode = CutsceneMapView.PickMode.Cell;
            SetStatus("拾取模式：点击地图格子填入坐标（右键取消）", Colors.Yellow);
        }

        private void OnCellPicked(Vector2I pos)
        {
            var callback = _pickCallback;
            _pickCallback = null;
            callback?.Invoke(pos);
            SetStatus($"已拾取坐标 ({pos.X}, {pos.Y})");
        }

        private void OnRectPicked(Rect2I rect)
        {
            if (BlockEditDuringPreview()) return;
            if (_script?.Trigger == null) return;
            _script.Trigger.rect = new[] { rect.Position.X, rect.Position.Y, rect.Size.X, rect.Size.Y };
            MarkDirty();
            UpdateTriggerRectLabel();
            _renderer.QueueRedraw();
            SetStatus($"已设置触发区：[{rect.Position.X}, {rect.Position.Y}, {rect.Size.X}, {rect.Size.Y}]");
        }

        // ============ 通用 ============

        /// <summary>
        /// 归一化演员表：怪物演员若 monsterConfigId 缺省（0）且有可用怪物配置，则回填第一个，
        /// 避免"下拉显示某怪物但底层 monsterConfigId 仍为 0"（OptionButton.Select 不触发 ItemSelected）
        /// 导致登场 / actor 下拉显示"怪物(0)"。这是该陷阱的兜底，覆盖面板折叠时加载等路径。
        /// </summary>
        private void NormalizeActors()
        {
            if (_script == null) return;
            var options = CutsceneActorCatalog.GetMonsterOptions();
            if (options.Count == 0) return;
            int first = options[0].id;
            foreach (var a in _script.Actors)
            {
                if (a.type == 2 && a.monsterConfigId == 0)
                {
                    a.monsterConfigId = first;
                    MarkDirty();
                }
            }
        }

        private void RebuildAll()
        {
            NormalizeActors();
            bool hasScript = _script != null;
            _letterboxCheck.SetPressedNoSignal(hasScript && _script.Letterbox);
            _skippableCheck.SetPressedNoSignal(hasScript && _script.Skippable);
            RebuildTriggerUi();
            RebuildTree();
            RefreshActorPanel();
            _form.BuildForm(_selectedCue, ActorDefsForForm());
            _renderer.Script = _script;
            _renderer.SelectedCue = _selectedCue;
            _renderer.QueueRedraw();
        }

        private void MarkDirty()
        {
            if (!_dirty)
            {
                _dirty = true;
                SetStatus("有未保存修改");
            }
        }

        private void SetStatus(string text, Color? color = null)
        {
            if (_status == null) return;
            _status.Text = text;
            _status.Modulate = color ?? Colors.White;
        }

        private void ShowErrorDialog(List<string> errors)
        {
            var d = new AcceptDialog { Title = "校验失败", DialogText = string.Join("\n", errors) };
            d.Confirmed += () => d.QueueFree();
            d.Canceled += () => d.QueueFree();
            AddChild(d);
            d.PopupCentered();
        }

        private static Label MakeLabel(string text) => new() { Text = text };

        private Button MakeButton(string text, Action onPressed)
        {
            var b = new Button();
            b.Text = text;
            b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            // 统一兜底：按钮处理器里任何异常（序列化失败、空引用等）都不能静默吞掉，
            // 状态栏红字 + 控制台错误双通道反馈，避免"点了没反应"
            b.Pressed += () =>
            {
                try
                {
                    onPressed();
                }
                catch (Exception ex)
                {
                    GD.PushError($"[CutsceneEditor] 「{text}」操作失败: {ex}");
                    SetStatus($"「{text}」操作失败：{ex.Message}", Colors.Red);
                }
            };
            return b;
        }

        private void ShowTextDialog(string title, string initial, Action<string> onConfirm)
        {
            var w = new Window();
            w.Title = title;
            w.Size = new Vector2I(380, 150);
            var vbox = new VBoxContainer();
            w.AddChild(vbox);
            var le = new LineEdit { Text = initial };
            vbox.AddChild(le);
            var hbox = new HBoxContainer();
            vbox.AddChild(hbox);
            var ok = new Button(); ok.Text = "确定"; ok.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var cancel = new Button(); cancel.Text = "取消"; cancel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            hbox.AddChild(ok); hbox.AddChild(cancel);
            ok.Pressed += () => { onConfirm(le.Text.Trim()); w.QueueFree(); };
            cancel.Pressed += () => w.QueueFree();
            // 点窗口 X 关闭时同样释放（否则节点泄漏）
            w.CloseRequested += () => w.QueueFree();
            AddChild(w);
            w.PopupCentered();
        }
    }
}
