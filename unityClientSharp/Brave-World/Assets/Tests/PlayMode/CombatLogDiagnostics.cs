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
    /// 战斗日志链路诊断：撞怪进战斗 → CombatLogNotify 必须收到条目 → HUD 必须有行。
    /// 需要 servercsharp 服务器在线。
    /// </summary>
    public class CombatLogDiagnostics
    {
        [UnityTest]
        public IEnumerator Combat_ProducesLogEntries()
        {
            var go = new GameObject("Bootstrap");
            var bs = go.AddComponent<MapBootstrap>();
            bs.RunNetworkSmokeTest = true;

            float deadline = Time.realtimeSinceStartup + 25f;
            while (PlayerEntity.Instance == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.NotNull(PlayerEntity.Instance, "玩家未生成");
            var player = PlayerEntity.Instance;
            var nm = NetworkManager.Instance;
            Assert.Greater(nm.Monsters.Count, 0, "没有怪物可打");

            int logEntries = 0;
            System.Action<Game.CombatLogNotify> onLog = (n) => logEntries += n.Entries.Count;
            nm.CombatLogNotify += onLog;

            int statePackets = 0;
            System.Action<Game.CombatStateNotify> onState = (n) => statePackets++;
            nm.CombatStateNotify += onState;

            var moveTo = typeof(PlayerEntity).GetMethod("MoveTo",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(moveTo);

            // 持续走向最近的怪物并撞上去
            float end = Time.time + 30f;
            while (Time.time < end && logEntries == 0)
            {
                Vector2Int? mpos = null;
                if (MonsterManager.Instance != null)
                {
                    foreach (var m in MonsterManager.Instance.GetMonsters())
                    {
                        var d = m.GridPos - player.GridPos;
                        if (mpos == null || Mathf.Abs(d.x) + Mathf.Abs(d.y) < Mathf.Abs(mpos.Value.x - player.GridPos.x) + Mathf.Abs(mpos.Value.y - player.GridPos.y))
                            mpos = m.GridPos;
                    }
                }
                if (mpos == null) break;

                if (!player.IsMoving && !player.IsServerGridCorrectionActive())
                {
                    var delta = mpos.Value - player.GridPos;
                    int manhattan = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
                    Vector2Int target;
                    if (manhattan <= 1)
                    {
                        target = mpos.Value; // 相邻：直接撞
                    }
                    else
                    {
                        var step = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                            ? new Vector2Int((int)Mathf.Sign(delta.x), 0)
                            : new Vector2Int(0, (int)Mathf.Sign(delta.y));
                        target = player.GridPos + step;
                        if (!Walkability.IsWalkable(target) &&
                            !(Walkability.Monsters != null && Walkability.Monsters.IsBlockedByMonster(target)))
                        target = mpos.Value; // 绕不过去就硬撞（碰撞移动）
                    }
                    moveTo.Invoke(player, new object[] { target });
                }
                yield return null;
            }

            nm.CombatLogNotify -= onLog;
            nm.CombatStateNotify -= onState;
            Debug.Log($"[Diag] 战斗结果: statePackets={statePackets} logEntries={logEntries}");
            Assert.Greater(logEntries, 0, "战斗发生但 CombatLogNotify 未收到");
        }
    }
}
