using Render = ClinetCSharp.RenderComponents;
using Godot;
using Protocol;
using System.Collections.Generic;
using System.Linq;

namespace ClinetCSharp
{
    /// <summary>
    /// 地图装饰实体（房舍等静态摆件）。
    /// 作为 entityType="decoration" 的 EntityProfile 运行时表现，通过 ProfileId 驱动外观/标签/障碍属性。
    /// </summary>
    public partial class MapDecoration : EntityBase
    {
        private int _gridSize = 111;
        private int _gridX;
        private int _gridY;

        /// <summary>建筑配置 ID（即旧的 DecorationTypeId / ProfileId）</summary>
        public int BuildCfgId => ProfileId;

        /// <summary>建筑实例唯一 UID</summary>
        public int BuildingUid { get; private set; } = -1;

        public int GridX => _gridX;
        public int GridY => _gridY;

        /// <summary>占地宽度（格子数）</summary>
        public int SizeX => GridSizeX;
        /// <summary>占地高度（格子数）</summary>
        public int SizeY => GridSizeY;

        /// <summary>是否阻塞移动（由 obstacle 组件控制）</summary>
        public bool BlockMovement { get; set; } = false;

        /// <summary>是否为编辑模式下的可拖动摆件</summary>
        public bool IsEditable { get; set; } = false;

        /// <summary>编辑模式下请求开始拖动本摆件</summary>
        public static event System.Action<MapDecoration>? DecorationDragRequested;

        // ========== 共享传送门交互 ==========
        private VBoxContainer? _portalMenu;
        private bool _isPortal;
        private NetworkManager? _network;

        // ========== 酒馆进入交互 ==========
        private Button? _tavernEnterButton;
        private bool _isTavern;

        protected override Vector2I GetGridPos() => new Vector2I(_gridX, _gridY);
        protected override int GetGridSize() => _gridSize;
        protected override void SetGridSizeValue(int value) => _gridSize = value;

        /// <param name="registerInDecorationGroup">
        /// 是否加入 map_decoration / decoration 组。真实放置在地图上的建筑应加入，
        /// 以便 ApplyProfileToAll 等按组逻辑命中；预览实体必须传 false，
        /// 否则会被当成“已放置的建筑”计入 applied，并可能干扰组相关的真实逻辑。
        /// </param>
        public void Setup(int profileId, int gridX, int gridY, int gridSize, int buildingUid = -1, int sizeX = 1, int sizeY = 1, bool registerInDecorationGroup = true)
        {
            ProfileId = profileId;
            BuildingUid = buildingUid;
            _gridX = gridX;
            _gridY = gridY;
            _gridSize = gridSize;
            GridSizeX = sizeX > 0 ? sizeX : 1;
            GridSizeY = sizeY > 0 ? sizeY : 1;

            if (registerInDecorationGroup)
            {
                AddToGroup("map_decoration");
                AddToGroup("decoration");
            }

            Name = $"MapDecoration_{gridX}_{gridY}_{profileId}_{buildingUid}";
            VisualSizeScale = 1.0f; // 填满整个 footprint
            CornerRadius = 8f;
            TextColor = new Color(1, 1, 0.9f);

            // 从 EntityProfileManager 应用配置（会设置 GridSizeX/Y 和视觉属性）
            var profileMgr = EntityProfileManager.Instance;
            if (profileMgr != null)
            {
                profileMgr.ApplyProfile(this, profileId);

                // 防御性 fallback：如果指定 profile 不存在（例如旧数据用了未注册的子配置 id），
                // 尝试回退到同建筑类型的 base id，避免渲染成默认深灰色方块。
                if (profileMgr.GetProfile(profileId) == null)
                {
                    int fallbackId = BuildingType.GetConfigBaseId(BuildingType.GetTypeFromConfigId(profileId));
                    if (fallbackId != profileId && profileMgr.GetProfile(fallbackId) != null)
                    {
                        GD.PushWarning($"[MapDecoration] Profile {profileId} 不存在，fallback 到 {fallbackId} (grid={gridX},{gridY})");
                        ProfileId = fallbackId;
                        profileMgr.ApplyProfile(this, fallbackId);
                    }
                }
            }
            else
            {
                GD.PushError($"[MapDecoration] EntityProfileManager 未就绪，无法应用 Profile {profileId}");
            }

            // 应用配置后重新计算占地中心位置（_gridX/_gridY 是左上角锚点）
            Position = GetWorldPositionForGridAnchor(new Vector2I(_gridX, _gridY));

            // 判断是否为共享传送门
            _isPortal = BuildingType.GetTypeFromConfigId(ProfileId) == BuildingType.Portal;
            if (_isPortal && !IsEditable)
            {
                var tree = GetTree();
                if (tree != null)
                {
                    _network = tree.GetFirstNodeInGroup("network_manager") as NetworkManager;
                    if (_network == null)
                        _network = UiServices.GetNetworkManager(this);
                }
            }

            // 判断是否为酒馆
            _isTavern = BuildingType.GetTypeFromConfigId(ProfileId) == BuildingType.Tavern;

            EnsureRenderComponents();
            QueueRedraw();
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            if (IsEditable)
                return;

            if (_isPortal)
                UpdatePortalMenu();
            else if (_isTavern)
                UpdateTavernButton();
        }

        private void UpdatePortalMenu()
        {
            var player = GetTree()?.GetFirstNodeInGroup("player") as Player;
            if (player == null)
            {
                ClosePortalMenu();
                return;
            }

            // 玩家在传送门周围 1 格（8 邻域）内触发菜单
            int dx = Mathf.Abs(player.GridPos.X - _gridX);
            int dy = Mathf.Abs(player.GridPos.Y - _gridY);
            bool inRange = dx <= 1 && dy <= 1;

            if (inRange && _portalMenu == null)
                ShowPortalMenu();
            else if (!inRange && _portalMenu != null)
                ClosePortalMenu();
        }

        private void ShowPortalMenu()
        {
            if (_portalMenu != null || _network == null)
                return;

            var maps = GetDestinationMaps();
            if (maps.Count == 0)
                return;

            _portalMenu = new VBoxContainer();
            _portalMenu.Name = "PortalMenu";

            var panel = new PanelContainer();
            panel.Name = "PortalPanel";

            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.1f, 0.1f, 0.2f, 0.9f);
            styleBox.BorderColor = new Color(0.6f, 0.2f, 0.9f);
            styleBox.BorderWidthTop = 2;
            styleBox.BorderWidthBottom = 2;
            styleBox.BorderWidthLeft = 2;
            styleBox.BorderWidthRight = 2;
            styleBox.CornerRadiusTopLeft = 4;
            styleBox.CornerRadiusTopRight = 4;
            styleBox.CornerRadiusBottomLeft = 4;
            styleBox.CornerRadiusBottomRight = 4;
            panel.AddThemeStyleboxOverride("panel", styleBox);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);

            var titleLabel = new Label();
            titleLabel.Text = "共享传送门";
            titleLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.4f, 1.0f));
            titleLabel.AddThemeFontSizeOverride("font_size", 14);
            vbox.AddChild(titleLabel);

            foreach (var (mapName, displayName) in maps)
            {
                var btn = new Button();
                btn.Text = displayName;
                btn.AddThemeFontSizeOverride("font_size", 13);
                string target = mapName;
                btn.Pressed += () => OnPortalDestinationSelected(target);
                vbox.AddChild(btn);
            }

            panel.AddChild(vbox);
            _portalMenu.AddChild(panel);

            AddChild(_portalMenu);
            _portalMenu.Position = new Vector2(0, -GridSize * 0.8f);
        }

        private void ClosePortalMenu()
        {
            if (_portalMenu != null && IsInstanceValid(_portalMenu))
            {
                _portalMenu.QueueFree();
                _portalMenu = null;
            }
        }

        // ========== 酒馆进入交互 ==========

        private void UpdateTavernButton()
        {
            var player = GetTree()?.GetFirstNodeInGroup("player") as Player;
            if (player == null)
            {
                CloseTavernButton();
                return;
            }

            bool inRange = IsPlayerInTavernRange(player.GridPos);

            if (inRange && _tavernEnterButton == null)
                ShowTavernButton();
            else if (!inRange && _tavernEnterButton != null)
                CloseTavernButton();
        }

        private bool IsPlayerInTavernRange(Vector2I playerGridPos)
        {
            int minX = _gridX - 1;
            int minY = _gridY - 1;
            int maxX = _gridX + SizeX;     // footprint 右侧外扩 1 格
            int maxY = _gridY + SizeY;     // footprint 下侧外扩 1 格

            return playerGridPos.X >= minX && playerGridPos.X <= maxX &&
                   playerGridPos.Y >= minY && playerGridPos.Y <= maxY;
        }

        private void ShowTavernButton()
        {
            if (_tavernEnterButton != null)
                return;

            _tavernEnterButton = new Button
            {
                Text = "进入酒馆",
                FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(80, 28),
            };
            _tavernEnterButton.AddThemeFontSizeOverride("font_size", 13);
            _tavernEnterButton.Pressed += OnEnterTavern;

            AddChild(_tavernEnterButton);
            // 放在建筑底部中央内部（建筑原点为中心，y 向下为正）
            _tavernEnterButton.Position = new Vector2(-40, GridSize * SizeY * 0.5f - 32);
        }

        private void CloseTavernButton()
        {
            if (_tavernEnterButton != null && IsInstanceValid(_tavernEnterButton))
            {
                _tavernEnterButton.QueueFree();
                _tavernEnterButton = null;
            }
        }

        private void OnEnterTavern()
        {
            CloseTavernButton();
            TavernInteriorPanel.Get()?.Enter();
            GD.Print("[MapDecoration] 进入酒馆");
        }

        private List<(string MapName, string DisplayName)> GetDestinationMaps()
        {
            var result = new List<(string, string)>();
            var currentMap = _network?.CurrentMapName ?? "";
            var mapNames = MapDataManager.GetMapList();

            foreach (var mapName in mapNames)
            {
                if (mapName == currentMap)
                    continue;

                try
                {
                    MapDataManager.LoadMapFromJson(mapName, out _, out _, out var displayName);
                    result.Add((mapName, string.IsNullOrEmpty(displayName) ? mapName : displayName));
                }
                catch
                {
                    result.Add((mapName, mapName));
                }
            }

            return result;
        }

        private void OnPortalDestinationSelected(string targetMap)
        {
            ClosePortalMenu();
            if (_network == null)
                return;

            var req = new Game.ChangeMapRequest
            {
                TargetMap = targetMap,
            };
            _network.SendPacket(MessageId.GameChangeMapReq, req);
            GD.Print($"[MapDecoration] 请求传送至 {targetMap}");
        }

        public override void _ExitTree()
        {
            ClosePortalMenu();
            CloseTavernButton();
            base._ExitTree();
        }

        public override void _Input(InputEvent @event)
        {
            if (IsEditable && @event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                // 编辑模式下：只在“放建筑”工具下才允许拖拽建筑；
                // 刷地形模式下不拦截，让框选可以从建筑上启动。
                var editor = GetTree()?.GetFirstNodeInGroup("map_editor") as MapEditor;
                if (editor != null && editor.CurrentTool == MapEditor.EditorTool.PlaceDecoration && HitTest(GetGlobalMousePosition()))
                {
                    DecorationDragRequested?.Invoke(this);
                    GetViewport()?.SetInputAsHandled();
                }
                // 编辑模式下不触发普通实体点击（传送门/酒馆等），避免拦截编辑器操作
                return;
            }

            // 如果鼠标正悬停在其他 UI 控件上（如酒馆进入按钮），让控件先处理，不吞掉点击
            var hovered = GetViewport()?.GuiGetHoveredControl();
            if (hovered != null && hovered.MouseFilter != Control.MouseFilterEnum.Ignore)
                return;

            if (CheckEntityClick(@event))
                GetViewport()?.SetInputAsHandled();
        }

        public override bool HitTest(Vector2 worldPos)
        {
            float halfW = GridSize * Mathf.Max(1, GridSizeX) / 2.0f;
            float halfH = GridSize * Mathf.Max(1, GridSizeY) / 2.0f;
            var worldCenter = GetWorldPositionForGridAnchor(new Vector2I(_gridX, _gridY));
            return Mathf.Abs(worldPos.X - worldCenter.X) < halfW &&
                   Mathf.Abs(worldPos.Y - worldCenter.Y) < halfH;
        }

        public override void OnGridSizeChanged()
        {
            Position = GetWorldPositionForGridAnchor(new Vector2I(_gridX, _gridY));
        }

        public override void _Draw()
        {
            foreach (var comp in _renderComponents)
                comp.Draw();
        }

        private void EnsureRenderComponents()
        {
            if (_renderComponents.Count > 0)
                return;

            AddRenderComponent(new Render.AppearanceComponent());
            AddRenderComponent(new Render.LabelComponent());
        }
    }
}
