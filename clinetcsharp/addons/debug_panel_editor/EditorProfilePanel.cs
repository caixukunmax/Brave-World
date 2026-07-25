using Godot;
using System.Collections.Generic;
using System.Linq;
using ClinetCSharp;

namespace ClinetCSharp.Editor
{
    /// <summary>
    /// 编辑器内实体配置主面板（对齐 Unity DebugPanelWindow 的「实体配置」Tab）。
    /// 不开游戏即可：浏览/新建/删除 Profile、编辑各组件、落盘 res://debug_panel_config.cfg。
    /// </summary>
    [Tool]
    public partial class EditorProfilePanel : VBoxContainer
    {
        private Dictionary<int, EntityProfile> _profiles = new();
        private int _currentProfileId = -1;
        // 当组件编辑器尚未 Ready 时，SelectProfile 无法同步 Bind，先记录待绑定项，
        // 待 EditorProfileComponentList._Ready 完成后再由 OnComponentEditorReady 消费。
        private int _pendingProfileId = -1;

        private ItemList _profileList;
        private LineEdit _searchEdit;
        // 当前过滤后的列表显示顺序（搜索过滤用），供选中索引 -> Profile 映射
        private List<EntityProfile> _orderedProfiles = new();
        private string _listFilter = "";
        private LineEdit _nameEdit;
        private Button _saveButton;
        private Timer _saveTimer;
        private EditorProfileComponentList _componentEditor;
        private ConfirmationDialog _deleteDialog;

        // 预览：复用游戏同款 PreviewMap + SubViewport（运行时预览面板亦是如此），
        // 按 EntityType 分发真实实体类（对齐运行时 CreatePreviewEntity），与游戏 1:1 一致。
        private SubViewportContainer _previewContainer;
        private SubViewport _previewViewport;
        private PreviewMap _previewMap;
        private EntityBase _previewEntity;
        private HSlider _zoomSlider;
        // 预览渲染超采样倍率（编辑器显示缩放 → N× 渲染 / 1/N 显示），见 EditorPreviewEnvironment
        private int _previewRenderScale = 1;

        public override void _Ready()
        {
            BuildUi();
            LoadProfiles();
        }

        private void BuildUi()
        {
            // 让根面板填满 Dock 区域
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;
            // 裁剪越界内容：Dock 高度小于内容时子节点会向下溢出，盖住 Godot 底部
            // “输出/调试器/音频/动画”标签栏并抢走点击（与地图/演出编辑器一致处理）
            ClipContents = true;

            // 读取上次记忆的预览缩放倍率（重启编辑器/客户端后不丢失）
            float savedZoom = LoadPreviewZoom();

            var title = new Label { Text = "实体显示配置", HorizontalAlignment = HorizontalAlignment.Left };
            title.AddThemeFontSizeOverride("font_size", 18);
            AddChild(title);

            // 工具栏
            var toolbar = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var newBtn = new Button { Text = "新建" };
            var dupBtn = new Button { Text = "复制" };
            var delBtn = new Button { Text = "删除" };
            _saveButton = new Button { Text = "保存" };
            var reloadBtn = new Button { Text = "重载" };
            newBtn.Pressed += OnNewProfile;
            dupBtn.Pressed += OnDuplicateProfile;
            delBtn.Pressed += OnRequestDeleteProfile;
            _saveButton.Pressed += SaveNow;
            reloadBtn.Pressed += LoadProfiles;
            toolbar.AddChild(newBtn);
            toolbar.AddChild(dupBtn);
            toolbar.AddChild(delBtn);
            toolbar.AddChild(_saveButton);
            toolbar.AddChild(reloadBtn);
            AddChild(toolbar);

            // 主分割：左列（搜索栏 + 固定高度列表 + 左下角预览） + 右侧组件编辑
            var hsplit = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            hsplit.AddThemeConstantOverride("separation", 6);

            var left = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(300, 0),
                SizeFlagsVertical = SizeFlags.ExpandFill,
            };

            // 搜索栏：按 ID 或名称过滤下方列表
            _searchEdit = new LineEdit { PlaceholderText = "搜索 ID 或名称…" };
            _searchEdit.TextChanged += OnSearchChanged;
            left.AddChild(_searchEdit);

            // 实体列表：固定高度，超出部分用 ItemList 自带滚动条滚动
            _profileList = new ItemList
            {
                CustomMinimumSize = new Vector2(0, 280),
                SizeFlagsVertical = SizeFlags.ShrinkBegin,
            };
            _profileList.ItemSelected += OnProfileSelected;
            left.AddChild(_profileList);

            var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };

            _nameEdit = new LineEdit { PlaceholderText = "Profile 名称" };
            _nameEdit.TextChanged += OnNameChanged;
            right.AddChild(_nameEdit);

            // 预览区（左下角）：复用游戏同款 PreviewMap + SubViewport（与运行时预览面板一致），
            // 用 ApplyProfileToEntity + 同一套 IRenderComponent 渲染，保证与游戏 1:1 一致。
            _previewContainer = new SubViewportContainer
            {
                // 吃掉左列剩余高度（列表固定高度，多余空间全给预览），稳定在面板左下角
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 200),
                Stretch = true,
                // 必须拦截(Stop)鼠标输入：SubViewportContainer 收到 GUI 鼠标事件后才会把事件转发给
                // 内部 SubViewport，进而由 PreviewMap._UnhandledInput 处理滚轮缩放。
                // 若设为 Pass，事件会穿透到下方组件列表(ScrollContainer)，且 SubViewport 收不到滚轮，
                // 导致预览滚轮缩放完全无效。Stop 还会阻止滚轮滚到下方列表，正合预期。
                MouseFilter = Control.MouseFilterEnum.Stop,
            };
            _previewViewport = new SubViewport
            {
                Size = new Vector2I(640, 640),
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                // 透明背景，只显示实体本身，不要后面格子
                TransparentBg = true,
            };
            _previewContainer.AddChild(_previewViewport);
            // 宿主环境对齐（根治预览与游戏渲染不一致）：
            // 1) 编辑器显示缩放会把 SubViewport 纹理双线性放大到物理屏（整体发虚），
            //    改为 N× 超采样渲染 + StretchShrink=N 按 1/N 显示，texel 密度 ≥ 物理像素；
            // 2) SubViewport 内 Control 主题默认解析到编辑器主题（字体/字号漂移），
            //    改为解析游戏主题（项目主题 ?? 引擎默认主题）。
            _previewRenderScale = EditorPreviewEnvironment.ApplyTo(_previewContainer, _previewViewport);
            _previewMap = new PreviewMap
            {
                // 编辑器预览只渲染实体，关掉背景格子
                ShowGrid = false,
                // 放大实体，原整图自适应会让单格实体只占中心一格、显得很小
                PreviewZoom = 3.0f,
                // 编辑器用滑条控制缩放，不再用鼠标滚轮
                EnableWheelZoom = false,
                // 应用上次记忆的缩放倍率
                UserZoom = savedZoom,
            };
            _previewViewport.AddChild(_previewMap);
            // 视口尺寸跟随容器尺寸，保持与面板一致且无畸变地撑满预览区
            _previewContainer.Resized += OnPreviewResized;
            _previewMap.UserZoomChanged += OnPreviewUserZoomChanged;
            left.AddChild(_previewContainer);

            // 预览缩放滑条（替代原“滚轮缩放预览”提示文字；编辑器用滑条控制缩放）
            var zoomRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var zoomLabel = new Label
            {
                Text = "预览缩放",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _zoomSlider = new HSlider
            {
                MinValue = 0.2,
                MaxValue = 5.0,
                Step = 0.05,
                Value = savedZoom,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Stop,
            };
            _zoomSlider.ValueChanged += OnZoomSliderChanged;
            // 拖动结束（松手）时把缩放倍率写入用户目录，重启后不丢失
            _zoomSlider.DragEnded += OnZoomDragEnded;
            // 居中说明并入滑条行，省掉单独一行的高度
            var previewHint = new Label
            {
                Text = "2×2",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            zoomRow.AddChild(zoomLabel);
            zoomRow.AddChild(_zoomSlider);
            zoomRow.AddChild(previewHint);
            left.AddChild(zoomRow);

            _componentEditor = new EditorProfileComponentList { OwnerPanel = this, SizeFlagsVertical = SizeFlags.ExpandFill };
            _componentEditor.Changed += RefreshPreview;
            right.AddChild(_componentEditor);

            hsplit.AddChild(left);
            hsplit.AddChild(right);
            AddChild(hsplit);

            // 防抖保存定时器（编辑器内也保持处理）
            _saveTimer = new Timer
            {
                WaitTime = 1.0,
                OneShot = true,
                ProcessMode = ProcessModeEnum.Always,
            };
            _saveTimer.Timeout += SaveNow;
            AddChild(_saveTimer);

            // 删除确认弹窗（防止误删丢配置）
            _deleteDialog = new ConfirmationDialog
            {
                Title = "确认删除配置",
                DialogText = "确定删除当前配置？此操作不可撤销。",
            };
            _deleteDialog.Confirmed += OnDeleteConfirmed;
            AddChild(_deleteDialog);
        }

        private void LoadProfiles()
        {
            _profiles = ProfileConfigIO.LoadFromFile();
            RefreshProfileList();
            // 重载后强制重新绑定当前选中的 Profile：否则若当前 id 仍有效，
            // RefreshProfileList 不会再次 SelectProfile，组件编辑器（如占地宽/高）
            // 会停留在旧内存值，导致改了 cfg 点“重载”后数字仍不更新。
            if (_currentProfileId >= 0 && _profiles.ContainsKey(_currentProfileId))
                SelectProfile(_currentProfileId);
        }

        private void RefreshProfileList()
        {
            _profileList.Clear();
            var f = (_listFilter ?? "").Trim().ToLower();
            _orderedProfiles = _profiles.Values.OrderBy(p => p.Id)
                .Where(p => string.IsNullOrEmpty(f)
                    || p.Id.ToString().Contains(f)
                    || (p.Name ?? "").ToLower().Contains(f))
                .ToList();
            foreach (var p in _orderedProfiles)
                _profileList.AddItem($"[{p.Id}] {p.Name}");

            if (_currentProfileId < 0 || !_profiles.ContainsKey(_currentProfileId))
            {
                if (_orderedProfiles.Count > 0)
                    SelectProfile(_orderedProfiles[0].Id);
            }
            else
            {
                SelectProfile(_currentProfileId);
            }
        }

        /// <summary>搜索栏输入：按 ID 或名称过滤列表（大小写不敏感）。</summary>
        private void OnSearchChanged(string text)
        {
            _listFilter = text;
            RefreshProfileList();
        }

        private void OnProfileSelected(long index)
        {
            int i = (int)index;
            if (i < 0 || i >= _orderedProfiles.Count) return;
            SelectProfile(_orderedProfiles[i].Id);
        }

        private void SelectProfile(int id)
        {
            _currentProfileId = id;
            var profile = _profiles[id];
            _nameEdit.Text = profile.Name;
            // 只有当组件编辑器自身已 Ready 才能安全 Bind（其 _Ready 中才创建 _componentHolder）；
            // 否则（首次打开时父面板 _Ready 内刚刚 AddChild 它）会抛异常导致组件 UI 不刷新、数值停留旧值。
            // 未 Ready 时暂存，等 EditorProfileComponentList._Ready 完成后由 OnComponentEditorReady 绑定。
            if (_componentEditor != null && _componentEditor.IsReady)
                BindCurrent();
            else
                _pendingProfileId = id;
        }

        /// <summary>组件编辑器已就绪后绑定当前 Profile 并刷新预览。Bind 同步执行，
        /// 预览刷新延迟一帧（_previewMap 也需在本帧稍后 Ready）。</summary>
        private void BindCurrent()
        {
            if (_currentProfileId < 0 || !_profiles.TryGetValue(_currentProfileId, out var profile))
                return;
            _componentEditor.Bind(profile);
            CallDeferred(nameof(RefreshPreview));
        }

        /// <summary>由 EditorProfileComponentList._Ready 完成时回调：消费待绑定项。</summary>
        public void OnComponentEditorReady()
        {
            if (_pendingProfileId < 0) return;
            int id = _pendingProfileId;
            _pendingProfileId = -1;
            if (_profiles.ContainsKey(id))
                BindCurrent();
        }

        /// <summary>
        /// 用当前选中的 Profile 刷新预览实体。实体创建统一走 EntityPreviewFactory（与运行时
        /// 调试面板同一入口，禁止在此自行 new 预览实体，否则两边效果必然再次漂移），
        /// 渲染与游戏地图中的实体 1:1 一致；组件编辑/增删/启停后实时刷新。
        /// </summary>
        private void RefreshPreview()
        {
            if (_previewMap == null || _currentProfileId < 0) return;
            if (!_profiles.TryGetValue(_currentProfileId, out var profile)) return;

            // 先把组件控件当前值写回内存 profile（字段编辑是防抖保存才落盘，
            // 不先 Flush 的话预览会从旧数据重绘，实时编辑看不到变化）。
            _componentEditor?.Flush();

            // 每次都创建全新的实体，等价于游戏里一个全新实例。
            // 不能复用同一实体：ApplyProfileToEntity 在标签 ContentPreview 为空时会
            // 故意不覆盖实体标签文字（游戏中怪物名字由 Setup 设置），复用会导致上一个
            // Profile 的标签/状态残留到当前预览（例如显示出未配置的“战斗”等文字）。
            _previewEntity = EntityPreviewFactory.CreatePreviewEntity(profile);
            _previewMap.SetEntity(_previewEntity);
            // 按实体占地自动缩放，使 1x1 / 2x2 / 异形都稳定撑满预览并默认居中
            _previewMap.AutoFit(_previewEntity);
        }

        private void OnPreviewResized()
        {
            if (_previewViewport == null || _previewContainer == null) return;
            EditorPreviewEnvironment.SyncViewportSize(_previewContainer, _previewViewport, _previewRenderScale);
            if (_previewEntity != null)
                _previewMap?.AutoFit(_previewEntity);
        }

        private bool _suppressZoomSliderEvent;

        /// <summary>滑条拖动 → 调整预览缩放倍率（0.2~5）。</summary>
        private void OnZoomSliderChanged(double value)
        {
            if (_suppressZoomSliderEvent) return;
            _previewMap?.SetUserZoom((float)value);
        }

        /// <summary>预览缩放倍率变化（滑条或滚轮）时同步滑条显示，带标志防止回环触发。</summary>
        private void OnPreviewUserZoomChanged(float z)
        {
            if (_zoomSlider == null) return;
            _suppressZoomSliderEvent = true;
            _zoomSlider.Value = z;
            _suppressZoomSliderEvent = false;
        }

        /// <summary>滑条拖动结束（鼠标释放）时保存缩放倍率，避免每次值变化都写盘。</summary>
        private void OnZoomDragEnded(bool released)
        {
            if (released && _zoomSlider != null)
                SavePreviewZoom((float)_zoomSlider.Value);
        }

        // 预览缩放倍率的持久化位置：用户数据目录下，不进 git、跨重启保留。
        private static string PreviewZoomPrefPath
            => System.IO.Path.Combine(OS.GetUserDataDir(), "editor_preview_zoom.cfg");

        private static float LoadPreviewZoom()
        {
            try
            {
                string p = PreviewZoomPrefPath;
                if (System.IO.File.Exists(p))
                {
                    string s = System.IO.File.ReadAllText(p).Trim();
                    if (float.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var v))
                        return Mathf.Clamp(v, 0.2f, 5.0f);
                }
            }
            catch (System.Exception) { /* 读取失败则使用默认值 */ }
            return 1.0f;
        }

        private static void SavePreviewZoom(float z)
        {
            try
            {
                System.IO.File.WriteAllText(
                    PreviewZoomPrefPath,
                    z.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (System.Exception) { /* 写入失败（如权限问题）则忽略，不阻塞 UI */ }
        }

        private void OnNameChanged(string newName)
        {
            if (_currentProfileId < 0) return;
            _profiles[_currentProfileId].Name = newName;
            // 仅更新列表项文本，避免整页重建导致组件 UI 闪烁/焦点丢失
            int idx = _orderedProfiles.FindIndex(p => p.Id == _currentProfileId);
            if (idx >= 0)
                _profileList.SetItemText(idx, $"[{_currentProfileId}] {newName}");
            ScheduleSave();
        }

        private void OnNewProfile()
        {
            int newId = _profiles.Count > 0 ? _profiles.Keys.Max() + 1 : 1;
            var profile = EntityProfile.CreatePlayerDefault(newId);
            profile.Name = $"新配置 {newId}";
            _profiles[newId] = profile;
            SelectProfile(newId);
            SaveNow();
        }

        private void OnDuplicateProfile()
        {
            if (_currentProfileId < 0) return;
            int newId = _profiles.Count > 0 ? _profiles.Keys.Max() + 1 : 1;
            var src = _profiles[_currentProfileId];
            var copy = src.Clone(newId, src.Name + " 副本");
            _profiles[newId] = copy;
            SelectProfile(newId);
            SaveNow();
        }

        private void OnRequestDeleteProfile()
        {
            if (_currentProfileId < 0) return;
            var profile = _profiles[_currentProfileId];
            _deleteDialog.DialogText = $"确定删除 [{profile.Id}] {profile.Name}？此操作不可撤销。";
            _deleteDialog.PopupCentered();
        }

        private void OnDeleteConfirmed()
        {
            if (_currentProfileId < 0) return;
            _profiles.Remove(_currentProfileId);
            _currentProfileId = -1;
            RefreshProfileList();
            SaveNow();
        }

        /// <summary>防抖保存：标记脏并启动 1 秒定时器（内存 profile 始终最新，可随时手动点保存兜底）。</summary>
        public void ScheduleSave()
        {
            _saveButton.Text = "保存 *";
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        public void SaveNow()
        {
            _componentEditor?.Flush();
            ProfileConfigIO.SaveToFile(_profiles);
            _saveButton.Text = "保存";
            GD.Print("[DebugPanelEditor] Profiles saved");
        }
    }
}
