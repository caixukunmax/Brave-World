using System.Collections;
using NUnit.Framework;
using UnityClientSharp.Entity;
using UnityClientSharp.Map.Rendering;
using UnityClientSharp.Net;
using UnityEngine;
using UnityEngine.TestTools;

namespace BraveWorld.Tests.PlayMode
{
    /// <summary>
    /// 移动诊断：复现"玩家按 WASD 无反应"现场。
    /// 需要 servercsharp 服务器在线（127.0.0.1:8889）。
    /// </summary>
    public class PlayerSpawnDiagnostics
    {
        [UnityTest]
        public IEnumerator PlayerSpawns_AndMovementGateOpen()
        {
            var go = new GameObject("Bootstrap");
            var bs = go.AddComponent<MapBootstrap>();
            bs.RunNetworkSmokeTest = true;

            // 等玩家生成（冒烟链路：连接→登录→选服→进游戏→地图同步）
            float deadline = Time.realtimeSinceStartup + 25f;
            while (PlayerEntity.Instance == null && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.NotNull(PlayerEntity.Instance, "玩家未生成（SpawnPlayerIfNeeded 未生效）");
            var player = PlayerEntity.Instance;
            Debug.Log($"[Diag] 玩家已生成 pos={player.GridPos}");

            // 等矫正落地
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(player.IsServerGridCorrectionActive(), "服务器矫正卡死（输入门常闭）");
            Assert.IsFalse(player.IsMoving, "IsMoving 滞留 true");
            Debug.Log($"[Diag] 矫正完成 pos={player.GridPos} moving={player.IsMoving}");

            // Walkability 门面状态
            Assert.NotNull(Walkability.Grid, "Walkability.Grid 未注册");
            Assert.NotNull(Walkability.Decorations, "Walkability.Decorations 未注册");
            Assert.NotNull(Walkability.Chests, "Walkability.Chests 未注册");
            Assert.NotNull(Walkability.Monsters, "Walkability.Monsters 未注册");

            // 四邻格可走性（出生点附近应至少一格可走）
            var deltas = new[] { new Vector2Int(0, -1), new Vector2Int(0, 1), new Vector2Int(-1, 0), new Vector2Int(1, 0) };
            int walkableCount = 0;
            foreach (var d in deltas)
            {
                var p = player.GridPos + d;
                bool w = Walkability.IsWalkable(p);
                if (w) walkableCount++;
                Debug.Log($"[Diag] 邻格 {p} walkable={w}");
            }
            Assert.Greater(walkableCount, 0, "出生点四邻格全部不可走（Walkability 链断裂）");

            // 移动输入门状态（注意：自动战斗下 CastingSkill 可能非空，属正常状态，不再断言为空）

            // 相机跟随
            Assert.NotNull(MapCameraController.Instance, "MapCameraController 不存在");
            Assert.NotNull(MapCameraController.Instance.FollowTarget, "相机未跟随玩家");

            // === 连续移动阶段：驱动移动 6 秒，看门狗一次都不应触发 ===
            int watchdogFires = 0;
            Application.LogCallback logCb = (msg, st, type) =>
            {
                if (msg.Contains("MoveResponse 超时")) watchdogFires++;
            };
            Application.logMessageReceived += logCb;

            int responses = 0;
            System.Action<Game.MoveResponse> onRsp = (rsp) => responses++;
            NetworkManager.Instance.MoveResponse += onRsp;

            var moveTo = typeof(PlayerEntity).GetMethod("MoveTo",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(moveTo, "反射 MoveTo 失败");

            var dirs = new[]
            {
                new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0),
            };
            int di = 0;
            float moveEnd = Time.time + 6f;
            while (Time.time < moveEnd)
            {
                if (!player.IsMoving && !player.IsServerGridCorrectionActive())
                {
                    var target = player.GridPos + dirs[di % dirs.Length];
                    if (Walkability.IsWalkable(target))
                    {
                        moveTo.Invoke(player, new object[] { target });
                        di++;
                    }
                    else
                    {
                        di++; // 换方向
                    }
                }
                yield return null;
            }

            Application.logMessageReceived -= logCb;
            NetworkManager.Instance.MoveResponse -= onRsp;
            Debug.Log($"[Diag] 连续移动 6s: steps={di} responses={responses} watchdogFires={watchdogFires}");
            Assert.Greater(responses, 0, "6 秒移动未收到任何 MoveResponse（连接已断？）");
            Assert.AreEqual(0, watchdogFires, "正常移动中看门狗误触发");
        }
    }
}
