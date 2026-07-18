using System.Collections.Generic;
using UnityEngine;
using UnityClientSharp.Map.Core;

namespace UnityClientSharp.Map.Rendering
{
    /// <summary>
    /// 地图渲染引导器 + 启动流转（三种模式）：
    /// - 离线：直接 StartGame(MapName)。
    /// - 冒烟（RunNetworkSmokeTest）：StartGame(MapName) + testdev 直通登录（开发通道，保持现状）。
    /// - 登录（ConnectNetwork 且非冒烟）：启动只建相机/NetworkManager/LoginPanel，不加载地图；
    ///   登录→选服→进角色后由首个 MapInfoReceived 触发 StartGame（地图名跟服务器推送）；
    ///   游戏中被踢/断线 → 销毁游戏内容回登录面板。
    /// 新建一个空场景挂上此组件即可运行；后续可改为编辑器手工搭场景后移除。
    /// </summary>
    public class MapBootstrap : MonoBehaviour
    {
        [Header("地图")]
        public string MapName = "落叶乡";
        public bool EditMode;

        [Header("地图外灰色（编辑态视觉）")]
        public bool ShowOutsideMapGray = true;

        [Header("网络（需 servercsharp 已启动）")]
        public bool ConnectNetwork;
        public bool RunNetworkSmokeTest;

        private readonly List<GameObject> _gameRoots = new();
        private bool _gameStarted;
        private UI.LoginPanel _loginPanel;
        private Net.NetworkSmokeTest _smoke;

        private void Start()
        {
            TerrainConfigUtil.Load();
            // UI 交互前提：EventSystem（按钮点击、面板拖拽、IsPointerOverGameObject 全依赖它）
            UnityClientSharp.UI.UiEventSystemUtil.EnsureExists();
            EnsureCamera();

            if (ConnectNetwork && !RunNetworkSmokeTest)
            {
                StartLoginFlow();
                return;
            }

            bool online = ConnectNetwork || RunNetworkSmokeTest;
            StartGame(MapName, online);
            if (online)
            {
                var netGo = EnsureNetworkObjects();
                if (RunNetworkSmokeTest)
                {
                    _smoke = netGo.AddComponent<Net.NetworkSmokeTest>();
                    _smoke.RunOnStart = true;
                }
                else
                {
                    Net.NetworkManager.Instance.ConnectToServer();
                }
            }
        }

        // ============ 相机 ============

        private Camera EnsureCamera()
        {
            // 2D 相机位于 z=-10，沿局部 +Z 看向 z=0 的地图平面（放 z=+10 会把地图甩到相机背后）
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
            }
            cam.orthographic = true;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            return cam;
        }

        // ============ 网络对象 ============

        /// <summary>NetworkManager 必须早于一切网络事件存在（MapInfo/EnterGame 都可能先于地图加载）。</summary>
        private GameObject EnsureNetworkObjects()
        {
            if (Net.NetworkManager.Instance != null)
                return Net.NetworkManager.Instance.gameObject;
            var netGo = new GameObject("Network");
            netGo.AddComponent<Net.NetworkManager>();
            return netGo;
        }

        // ============ 登录流 ============

        private void StartLoginFlow()
        {
            var netGo = EnsureNetworkObjects();
            _smoke = netGo.AddComponent<Net.NetworkSmokeTest>();
            _smoke.RunOnStart = false;
            var nm = Net.NetworkManager.Instance;
            nm.Kicked += OnKicked;
            nm.Disconnected += OnDisconnected;
            ShowLoginPanel(null);
        }

        private void ShowLoginPanel(string initialStatus)
        {
            var nm = Net.NetworkManager.Instance;
            // 首个 MapInfo = 进游戏完成信号（订阅先于发送：登录动作在面板之后发生）
            nm.MapInfoReceived += OnFirstMapInfo;
            _loginPanel = UI.LoginPanel.Create(_smoke);
            if (!string.IsNullOrEmpty(initialStatus))
                _loginPanel.SetInitialStatus(initialStatus);
        }

        private void OnFirstMapInfo(Game.MapInfoSyncNotify notify)
        {
            Net.NetworkManager.Instance.MapInfoReceived -= OnFirstMapInfo;
            if (_loginPanel != null)
            {
                Destroy(_loginPanel.gameObject);
                _loginPanel = null;
            }
            StartGame(notify.MapName, online: true);
        }

        private void OnKicked(string reason)
        {
            if (_gameStarted) BackToLogin("被服务器踢下线: " + reason);
        }

        private void OnDisconnected()
        {
            if (_gameStarted) BackToLogin("与服务器断开连接");
        }

        /// <summary>销毁游戏内容回登录面板（NetworkManager 保留，登录面板会重连）。</summary>
        private void BackToLogin(string status)
        {
            foreach (var go in _gameRoots)
                if (go != null) Destroy(go);
            _gameRoots.Clear();
            _gameStarted = false;
            ShowLoginPanel(status);
        }

        // ============ 游戏内容 ============

        private void StartGame(string mapName, bool online)
        {
            if (_gameStarted) return;
            _gameStarted = true;

            Camera cam = EnsureCamera();

            // 地图根节点 + 各渲染组件
            var mapGo = new GameObject("MapRoot");
            _gameRoots.Add(mapGo);
            var gm = mapGo.AddComponent<GridManager>();
            gm.IsEditMode = EditMode;
            gm.ShowOutsideMapGray = ShowOutsideMapGray;
            mapGo.AddComponent<MapRenderer>();
            mapGo.AddComponent<MapLabelOverlay>();
            // 相机跨登录会话存活，控制器只加一次（重复 AddComponent 会双重 Update）
            var camCtrl = cam.gameObject.GetComponent<MapCameraController>();
            if (camCtrl == null) camCtrl = cam.gameObject.AddComponent<MapCameraController>();

            // 加载地图
            gm.LoadMap(mapName);

            // 生成装饰实体（建筑/树/草等，含出生点在非编辑模式下跳过）
            var decoMgr = mapGo.AddComponent<UnityClientSharp.Entity.MapDecorationManager>();
            decoMgr.GridSize = gm.GridSize;
            decoMgr.SpawnEditable = EditMode;
            decoMgr.SpawnDecorations(gm.GridData);

            if (online)
            {
                EnsureNetworkObjects();

                // 地图同步桥（挂在 MapRoot：RequireComponent(GridManager)）
                var mapSync = mapGo.AddComponent<Net.NetworkMapSync>();

                // 在线实体管理器（挂 MapRoot 下）
                var onlineGo = new GameObject("OnlineEntities");
                _gameRoots.Add(onlineGo);
                onlineGo.transform.SetParent(mapGo.transform, false);
                onlineGo.AddComponent<UnityClientSharp.Entity.ChestManager>().GridSize = gm.GridSize;
                onlineGo.AddComponent<UnityClientSharp.Entity.MonsterManager>().GridSize = gm.GridSize;
                onlineGo.AddComponent<UnityClientSharp.Entity.NpcManager>().GridSize = gm.GridSize;
                onlineGo.AddComponent<UnityClientSharp.Entity.DropManager>().GridSize = gm.GridSize;
                onlineGo.AddComponent<UnityClientSharp.Entity.CombatFeedbackRouter>();

                // 战斗 HUD：技能栏 + Buff 栏 + 战斗日志 + 技能面板（F4）
                var skillBar = UnityClientSharp.UI.SkillBarHud.Create();
                UnityClientSharp.UI.BuffBarHud.Create(skillBar.Canvas);
                var combatLog = UnityClientSharp.UI.CombatLogHud.Create();
                var skillPanel = UnityClientSharp.UI.SkillPanelHud.Create();
                _gameRoots.Add(skillBar.gameObject);
                _gameRoots.Add(combatLog.gameObject);
                _gameRoots.Add(skillPanel.gameObject);

                // 登录路径：MapInfo 先于本组件创建到达，用缓存重放一次（冒烟路径缓存为空，自然跳过）
                mapSync.SyncFromCache();
            }

            // 小地图 HUD（离线也可用：显示地形 + 相机框）
            var minimap = MinimapHud.Create(gm);
            _gameRoots.Add(minimap.gameObject);

            float w = gm.MapBounds.width * gm.GridSize;
            float h = gm.MapBounds.height * gm.GridSize;
            // 逻辑地图中心 -> 世界坐标（Y 翻转统一走 GridMath）
            var center = GridMath.LogicToWorld(
                (gm.MapBounds.x + gm.MapBounds.width / 2f) * gm.GridSize,
                (gm.MapBounds.y + gm.MapBounds.height / 2f) * gm.GridSize);
            camCtrl.FocusOn(new Vector3(center.x, center.y, cam.transform.position.z), w, h);

            Debug.Log($"[MapBootstrap] 已加载地图 '{mapName}' 格子数={gm.GridData.Count} bounds={gm.MapBounds} online={online}");
        }
    }
}
