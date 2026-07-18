using System.Collections;
using Protocol;
using UnityEngine;

namespace UnityClientSharp.Net
{
    /// <summary>
    /// 网络冒烟测试 — 移植 Godot LoginScene 的 "Test Direct" 直通流程：
    /// 连接 → AccountLogin(testdev/testdev) → SelectServer(第一个服) →
    /// EnterGame(已有角色)/CreateRole(无角色) → 等 MapInfoSync → 打印结果并断连。
    /// 每步日志，任一步 10s 超时即失败中止。
    /// </summary>
    public class NetworkSmokeTest : MonoBehaviour
    {
        public bool RunOnStart;

        private const float StepTimeout = 10f;

        private void Start()
        {
            if (RunOnStart)
                Run();
        }

        [ContextMenu("Run Smoke Test")]
        public void Run()
        {
            if (NetworkManager.Instance == null)
            {
                Debug.LogError("[SmokeTest] NetworkManager 不存在");
                return;
            }
            StartCoroutine(SmokeRoutine(NetworkManager.Instance));
        }

        private IEnumerator SmokeRoutine(NetworkManager nm)
        {
            Debug.Log("[SmokeTest] === 开始冒烟流程 ===");
            var done = new bool[1];
            var ok = new bool[1];

            // 1) 连接（已连接则跳过：登录页启动即自动连接，而 Connected 只对新建连接触发，
            //    已连接时 ConnectRoutine 直接 yield break 不发事件，等下去必超时）
            if (!nm.IsServerConnected())
            {
                System.Action onConnected = () => { ok[0] = true; done[0] = true; };
                System.Action<string> onConnErr = (err) => { ok[0] = false; done[0] = true; };
                nm.Connected += onConnected;
                nm.ConnectionError += onConnErr;
                nm.ConnectToServer();
                yield return WaitStep(done, "Connect");
                nm.Connected -= onConnected;
                nm.ConnectionError -= onConnErr;
                if (!ok[0]) { Fail(nm, "连接失败"); yield break; }
            }

            // 2) 登录
            done[0] = false; ok[0] = false;
            System.Action<Login.AccountLoginResponse> onLogin = (rsp) =>
            {
                ok[0] = rsp.Code == Common.ErrorCode.Success;
                if (!ok[0]) Debug.LogError($"[SmokeTest] 登录被拒: {rsp.Code} {rsp.Message}");
                done[0] = true;
            };
            nm.LoginResponse += onLogin;
            nm.SendPacket(MessageId.LoginAccountLoginReq, new Login.AccountLoginRequest
            {
                Username = "testdev",
                Password = "testdev",
                Platform = "pc",
                DeviceId = SystemInfo.deviceUniqueIdentifier,
                ClientVersion = "1.0.0",
            });
            yield return WaitStep(done, "AccountLogin");
            nm.LoginResponse -= onLogin;
            if (!ok[0]) { Fail(nm, "登录失败"); yield break; }

            Debug.Log($"[SmokeTest] 登录成功 account={nm.AccountId} servers={nm.Servers.Count}");
            if (nm.Servers.Count == 0) { Fail(nm, "无可用服务器"); yield break; }

            // 3) 选服
            done[0] = false; ok[0] = false;
            System.Action<Login.SelectServerResponse> onSelect = (rsp) =>
            {
                ok[0] = rsp.Code == Common.ErrorCode.Success;
                if (!ok[0]) Debug.LogError($"[SmokeTest] 选服被拒: {rsp.Code} {rsp.Message}");
                done[0] = true;
            };
            nm.SelectServerResponse += onSelect;
            nm.SendPacket(MessageId.LoginSelectServerReq, new Login.SelectServerRequest
            {
                AccountToken = nm.AccountToken,
                ServerId = nm.Servers[0].ServerId,
            });
            yield return WaitStep(done, "SelectServer");
            nm.SelectServerResponse -= onSelect;
            if (!ok[0]) { Fail(nm, "选服失败"); yield break; }

            Debug.Log($"[SmokeTest] 选服成功 roles={nm.Roles.Count}");

            // 服务器可能先发 MapInfoSync 再回 EnterGameRsp（已实证）——提前订阅，两种次序都接住
            var mapDone = new bool[1];
            System.Action<Game.MapInfoSyncNotify> onMap = (notify) => { mapDone[0] = true; };
            nm.MapInfoReceived += onMap;

            // 4) 进游戏（有角色进角色，无角色建角色）
            done[0] = false; ok[0] = false;
            System.Action<Game.EnterGameResponse> onEnter = (rsp) =>
            {
                ok[0] = rsp.Code == Common.ErrorCode.Success;
                done[0] = true;
            };
            System.Action<Game.CreateRoleResponse> onCreate = (rsp) =>
            {
                ok[0] = rsp.Code == Common.ErrorCode.Success;
                done[0] = true;
            };
            nm.EnterGameResponse += onEnter;
            nm.CreateRoleResponse += onCreate;
            if (nm.Roles.Count > 0)
            {
                nm.SendPacket(MessageId.GameEnterGameReq, new Game.EnterGameRequest { RoleId = nm.Roles[0].RoleId });
                yield return WaitStep(done, "EnterGame");
            }
            else
            {
                nm.SendPacket(MessageId.GameCreateRoleReq, new Game.CreateRoleRequest { RoleName = "smoke" });
                yield return WaitStep(done, "CreateRole");
            }
            nm.EnterGameResponse -= onEnter;
            nm.CreateRoleResponse -= onCreate;
            if (!ok[0]) { Fail(nm, "进入游戏失败"); nm.MapInfoReceived -= onMap; yield break; }

            // 5) 等地图同步（若已先到则立即通过）
            yield return WaitStep(mapDone, "MapInfoSync");
            nm.MapInfoReceived -= onMap;

            var role = nm.CachedRoleInfo;
            Debug.Log($"[SmokeTest] === 冒烟通过 === role={role?.RoleName} map={nm.CurrentMapName} " +
                      $"pos=({role?.GridX},{role?.GridY}) chests={nm.Chests.Count} monsters={nm.Monsters.Count} npcs={nm.Npcs.Count}");

            // 保持连接（对齐 Godot Test Direct：直通后正常游玩）；断连只用于纯自动化验证场景
        }

        private IEnumerator WaitStep(bool[] done, string stepName)
        {
            float deadline = Time.realtimeSinceStartup + StepTimeout;
            while (!done[0] && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!done[0])
                Debug.LogError($"[SmokeTest] 步骤超时: {stepName}");
        }

        private void Fail(NetworkManager nm, string msg)
        {
            Debug.LogError($"[SmokeTest] === 冒烟失败: {msg} ===");
            nm.DisconnectFromServer();
        }
    }
}
