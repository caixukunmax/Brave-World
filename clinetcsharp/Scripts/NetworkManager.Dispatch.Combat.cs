using Google.Protobuf;
using Godot;
using Protocol;

namespace ClinetCSharp
{
    public partial class NetworkManager
    {
        private void HandleMoveResponse(ByteString data)
        {
            MoveResponse?.Invoke(Game.MoveResponse.Parser.ParseFrom(data));
        }

        private void HandleMoveCancelNotify(ByteString data)
        {
            MoveCancelNotify?.Invoke(Game.MoveCancelNotify.Parser.ParseFrom(data));
        }

        private void HandleMonsterMoveNotify(ByteString data)
        {
            MonsterMoveNotify?.Invoke(Game.MonsterMoveNotify.Parser.ParseFrom(data));
        }

        private void HandleMonsterMoveCancelNotify(ByteString data)
        {
            MonsterMoveCancelNotify?.Invoke(Game.MonsterMoveCancelNotify.Parser.ParseFrom(data));
        }

        private void HandleCombatLogNotify(ByteString data)
        {
            var notify = Game.CombatLogNotify.Parser.ParseFrom(data);
            CombatLogNotify?.Invoke(notify);

            // 攻击者抖动效果
            foreach (var entry in notify.Entries)
            {
                if (entry.LogType == Game.CombatLogType.CombatLogDamage)
                {
                    var attacker = FindEntityByName(entry.ActorName);
                    if (attacker != null)
                    {
                        var target = FindEntityByName(entry.TargetName);
                        if (target != null)
                            attacker.PlayAttackShake(target.GridPos);
                        else
                            attacker.PlayAttackShake();
                    }
                }
            }
        }

        private EntityBase? FindEntityByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var player = GetTree()?.GetFirstNodeInGroup("player") as Player;
            if (player != null && player.CharacterName == name)
                return player;

            var mm = GetTree()?.GetFirstNodeInGroup("monster_manager") as MonsterManager;
            if (mm != null)
            {
                foreach (var m in mm.GetMonsters())
                {
                    if (m.MonsterName == name)
                        return m;
                }
            }

            return null;
        }

        private void HandleCombatStateNotify(ByteString data)
        {
            CombatStateNotify?.Invoke(Game.CombatStateNotify.Parser.ParseFrom(data));
        }

        private void HandleBuffUpdateNotify(ByteString data)
        {
            BuffUpdateNotify?.Invoke(Game.BuffUpdateNotify.Parser.ParseFrom(data));
        }

        private void HandleCombatStartNotify(ByteString data)
        {
            CombatStartNotify?.Invoke(Game.CombatStartNotify.Parser.ParseFrom(data));
        }

        private void HandleCombatEndNotify(ByteString data)
        {
            CombatEndNotify?.Invoke(Game.CombatEndNotify.Parser.ParseFrom(data));
        }

        private void HandlePlayerDeathNotify(ByteString data)
        {
            PlayerDeathNotify?.Invoke(Game.PlayerDeathNotify.Parser.ParseFrom(data));
        }

        private void HandleLevelUpNotify(ByteString data)
        {
            LevelUpNotify?.Invoke(Game.LevelUpNotify.Parser.ParseFrom(data));
        }

        private void HandleCastResponse(ByteString data)
        {
            var rsp = Game.CastResponse.Parser.ParseFrom(data);
            CastResponse?.Invoke(rsp);
            if (!rsp.Success)
            {
                GD.PrintErr($"[Cast] Failed: {rsp.Error}");
            }
        }

        private void HandleCastStartNotify(ByteString data)
        {
            CastStartNotify?.Invoke(Game.CastStartNotify.Parser.ParseFrom(data));
        }

        private void HandleCastResultNotify(ByteString data)
        {
            CastResultNotify?.Invoke(Game.CastResultNotify.Parser.ParseFrom(data));
        }

        private void HandleCombatEventNotify(ByteString data)
        {
            CombatEventNotify?.Invoke(Game.CombatEventNotify.Parser.ParseFrom(data));
        }

        private void HandleProjectileSpawnNotify(ByteString data)
        {
            var notify = Game.ProjectileSpawnNotify.Parser.ParseFrom(data);
            ProjectileSpawnNotify?.Invoke(notify);
            GD.Print($"[Projectile] Spawn id={notify.ProjectileId} skill={notify.SkillId} from=({notify.FromX},{notify.FromY}) to=({notify.ToX},{notify.ToY}) speed={notify.Speed}");
        }

        private void HandleProjectileHitNotify(ByteString data)
        {
            var notify = Game.ProjectileHitNotify.Parser.ParseFrom(data);
            ProjectileHitNotify?.Invoke(notify);
            GD.Print($"[Projectile] Hit id={notify.ProjectileId} target={notify.TargetId}");
        }

        private void HandleDisengageNotify(ByteString data)
        {
            DisengageNotify?.Invoke(Game.DisengageNotify.Parser.ParseFrom(data));
        }
    }
}
