# Unity 客户端（Brave-World）工作指南

本项目是从 Godot 客户端 `clinetcsharp` 迁移而来。以下是迁移中已踩过坑的约束，新增/移植代码时必须遵守。

## Godot → Unity 2D 移植陷阱

1. **Y 轴翻转只能做一次。** Godot 2D 逻辑 Y 朝下，本项目统一由地图 Quad 的 `scale.y = -1` 在渲染层翻转。着色器内**禁止**再做 `uv.y = 1.0 - uv.y` 之类的二次翻转——两次翻转相互抵消，整张地图会上下镜像且内部自洽、不易察觉。任何新增按格子定位的渲染（标签、实体、特效）都要与 `MapRenderer.CellWorldPos` 的换算保持一致。
2. **2D 相机必须放在 z=-10。** Unity 相机沿局部 +Z 方向观察；放 z=+10 会把 z=0 的地图平面甩到相机背后，直接黑屏。
3. **屏幕空间线宽传给 shader 前确认单位链。** `GridOverlay` shader 内部用 `fwidth(grid_coord)` 把线宽换算回屏幕像素，因此 `_LineWidth` 必须传**屏幕像素**单位（`lineWidthWorld * zoom`），与 Godot 版 `GridManager` 的约定一致。移植其它 shader 参数时同样先确认 Godot 端传的是什么单位。
4. **TextMeshPro 默认字体无中文字形。** 任何会显示中文的 TMP 文本（地形名、面板、HUD）必须使用带 CJK 字形的 Font Asset，不能直接用了默认 LiberationSans 就完事。
5. **运行时 `Shader.Find` 的材质在打包时会被剥离。** 打 player 包前，必须把 shader 加入 Always Included Shaders 或改为序列化资产引用。
6. **场景/资产 YAML 中的 GUID 不能加引号。** 团结引擎的 YAML 解析器要求 guid 是裸标量；写成 `guid: "xxx"` 会报 "Could not extract GUID"，引用退化为零 GUID（典型症状：MonoBehaviour 脚本丢失 + "Broken text PPtr"）。场景文件尽量用 `BraveWorld > Create Startup Scene` 菜单（引擎 API）生成；手写/手改 YAML 时 guid 一律不加引号。
7. **实体定位只走 `GridMath.LogicToWorld` / `FootprintCenterWorld`，实体外观只走 `EntityBodySprite`。** 逻辑坐标（y 向下）→ 世界坐标（y 向上）的翻转全项目只有 `GridMath` 一处，禁止在实体/渲染代码里手写 `-y`；实体身体（圆角色块）用 `EntityBodySprite.Get` 程序生成并缓存，不引入美术资源，尺寸公式必须与 Godot `EntityBase`（VisualOuterSize/BorderWidth）保持一致。
8. **中文文本一律用 `FontUtil.ApplyCjkFont`。** 新增任何 TMP 文本组件（实体名称、HUD、面板）都要过 `FontUtil`，不要直接 `new TextMeshPro()` 就用。
9. **asmdef 引用规则（团结这版的实测结论）。** ① 自定义运行时 asmdef 必须显式引用包程序集，名字是程序集名而非包名：`Unity.TextMeshPro`、`UnityEngine.UI`（uGUI）。② 测试 asmdef 引用运行时 asmdef 用 `GUID:<32位hex>` 格式，hex 从批量编译日志的 `importing xxx.asmdef using Guid(<hex>)` 处取——不是 `.meta` 里的 base64；按名字引用在这版不生效。③ asmdef 不能引用预定义程序集（Assembly-CSharp），所以测试必须配运行时 asmdef。④ 引用配置改动后可能要跑两轮导入才生效（第一轮注册 guid，第二轮才真正进 .rsp）。⑤ 同一文件夹不允许同时存在 .asmdef 和 .asmref（报 "contains multiple assembly definition files"）——测试辅助代码直接放运行时程序集目录内即可（默认就属该程序集），不要画蛇添足加 asmref。
10. **网络收发只准单线程轮询。** `TcpTransport`/`NetworkManager` 在 `Update` 中轮询接收、同步发送，与 Godot 端语义一致；禁止起后台线程读写 socket 或直接改游戏状态。
11. **协议代码只拷不改。** `Assets/Scripts/Protos/*.cs` 是 `protocols/proto/*.proto` 的 protoc 生成物，保持与 `clinetcsharp/Scripts/protos/` 逐字节一致；协议变更走 `protocols/` 重新生成再拷贝，禁止手改。封包格式：`[4字节小端 uint32 长度][Common.Packet(msg_id/session/data/timestamp)]`。
12. **实体 Z 序与生成顺序固定。** sortingOrder：装饰 1 < 玩家 2 < 宝箱 3 < 怪物 4 < NPC 5 < 掉落 6（标签/条在同实体之上）；在线实体生成顺序固定为 宝箱→怪物→NPC→掉落→装饰（对齐 Godot `MapManager.SpawnMapEntities`），新实体类型必须插入到既定序位而不是随意追加。
13. **怪物血量没有 proto 字段。** 平时血条常满，血量只在战斗时由 `CombatStateNotify` 推送——显示层禁止自行发明血量来源；宝箱/掉落不是 EntityBase（Godot 亦然），永远单格，不套 footprint。
14. **移动三铁律。** ①逻辑先行视觉追赶：起步/矫正/死亡都是先改 `GridPos`，`Position` 由 tween 追赶，禁止反过来。②移动/回弹/矫正动画共用一个 tween handle，新动画必须先 `KillMoveTween` 杀旧（静止吸附只在非矫正非移动时启用）。③禁止假设服务器必有响应：`MoveResponse` 有超时兜底看门狗（`MoveDuration×2+1s` 无响应强制 `RollbackTo`），新增网络等待逻辑必须自带超时。
15. **服务器消息语义以服务器源码为准。** Godot 端的历史怪癖不可照抄——已知案例：`MoveCancelNotify.EntityId` 在 Godot 端错拿节点 InstanceId 比较（实为 AccountId，服务器 `MoveHandler.cs` 实证）；移植过滤/比较逻辑时先到 `servercsharp/` 查证。
16. **网络消息不保证业务次序。** 已实证：服务器可能先发 `MapInfoSyncNotify` 再回 `EnterGameRsp`。等待响应的订阅必须先于发送动作；依赖多消息的生成逻辑（如玩家生成）必须容忍任意到达次序（双触发 + 幂等检查），禁止假设"先回响应再推同步"。
17. **战斗显示一律由 `CombatStateNotify` 驱动。** 禁止监听 `CombatStartNotify` 做显示（Godot 端也无订阅）；空 units 的 CombatStateNotify = 全员脱战复位。实体判定：unit.IsPlayer 对 NPC 也为 true，玩家匹配必须 `IsPlayer && EntityId == AccountId` 双重判定，怪物/NPC 按 InstanceId 匹配。读条用本地 Tween（cast_time 取自 CastStartNotify），禁止逐帧吃服务器 cast_progress。
18. **离线行为与在线一致。** 网络请求发送前置（连接/GatewayToken）不满足时必须静默降级为纯本地表现且不计数不报警（对齐 Godot 无看门狗行为）；在线等待逻辑才需要超时兜底。
19. **UI 可交互三件套 + 相机防护。** 场景必须存在唯一 `EventSystem`（`UiEventSystemUtil.EnsureExists()`，`MapBootstrap` 已接入）——缺了它 Button/`IDragHandler` 全部静默失效。可交互 Canvas 需 `GraphicRaycaster` 且底板 Image 开 raycastTarget。任何"鼠标按下拖动"逻辑（相机平移等）在 `GetMouseButtonDown` 时必须查 `EventSystem.current.IsPointerOverGameObject()`，否则点击穿透 UI 拖动底层地图。
20. **中文字体只能用工程内置字体资产。** 这版引擎的 TMP FontEngine 拒绝一切 OS 来源字体（`CreateDynamicFontFromOSFont`、`new Font(OS路径)` 全部 `LoadFontFace` 失败，报 "Unable to load font face"），只有 Assets 内的字体资产（`Assets/Resources/Fonts/`，当前 NotoSansSC-VF 优先、SimHei 兜底）能创建 FontAsset。且 `CreateFontAsset` 内部要 `new Material(TMP SDF shader)`，工程必须已导入 **TMP Essentials**（TMP 包 `Package Resources` 下的 unitypackage，已导入到 `Assets/TextMesh Pro/`），否则 `Shader.Find` 为 null 抛 `ArgumentNullException`。症状都是中文 TMP 全不可见但无报错。注意：SimHei 是 Windows 商业字体，对外发布只保留 OFL 的 Noto。
21. **晚创建组件错过网络事件时必须缓存重放。** 登录路径下 MapInfo/EnterGame 先于地图对象（GridManager/NetworkMapSync）创建到达，"订阅先于发送"救不了已错过的事件——`NetworkMapSync.SyncFromCache()` 用 NetworkManager 缓存重放（缓存为空自动跳过）。新增"事件先到、组件后建"的链路一律配重放或双触发幂等。同理：跨登录会话存活的相机上挂组件要 `GetComponent ?? AddComponent`，防重复 AddComponent 双重 Update。
22. **等待事件前先检查目标状态是否已达成。** 事件只对状态跃迁触发：`Connected` 在已连接时不再发（`ConnectRoutine` 直接 return），冒烟流程曾因此在第一步死等超时。等待方要么先查状态（`IsServerConnected()`）、要么接受"已就绪"作为同等到达，禁止裸等事件。

## 编译验证

- 修改代码后用根目录 `check_compile_errors.ps1` 做批量编译检查（**运行前必须关闭团结引擎编辑器**，脚本会杀掉残留 Tuanjie 进程）。注意：该脚本对编辑器启动器立即返回的情况会误报失败，以 `%TEMP%/tuanjie_compile.log` 里的 `error CS` 计数与 `Exiting batchmode successfully` 为准；连跑多次时每次都要等 Tuanjie 进程真正结束再读日志（启动器秒退是假象，连发两轮会读到上一轮的旧日志）。
- 批量跑 EditMode 测试：`Tuanjie.exe -batchmode -projectPath <proj> -runTests -testPlatform EditMode -testResults <xml> -logFile <log>`。**不要加 `-nographics`**（`EntityBodySprite`/TMP 等会触图形设备报错）；启动器进程会提前退出，真实测试在后台继续，以结果 XML 为准。
- batchmode 下没有真实帧循环：`WaitForEndOfFrame`、`ScreenCapture.CaptureScreenshot(AsTexture)` 会挂死到 180s 超时或直接报错。渲染像素验证改用 `RenderTexture + cam.Render()` 同步读像素（需自建相机，画布切 `ScreenSpaceCamera`；见 `FontRenderVisualTests`）。
- EditMode 测试里运行时 MonoBehaviour 的 `Awake/Start` **不会执行**（无 `ExecuteAlways`），静态配置（如 `TerrainConfigUtil.Load()`）要在 `SetUp` 显式调用。
- 地图数据保持 `map.json` 原 schema（version 3，稀疏 cells，`"x_y"` uid 支持负坐标），与 Godot 端、服务器端共用；不要引入 Unity 专属格式。
