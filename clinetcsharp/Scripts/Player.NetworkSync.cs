using Godot;
using Protocol;

namespace ClinetCSharp
{
    public partial class Player
    {
        private void SubscribeNetworkEvents(NetworkManager nm)
        {
            nm.MoveCancelNotify += OnMoveCancelReceived;
            nm.MoveResponse += OnMoveResponse;
            nm.RoleAttrUpdated += OnRoleAttrUpdated;
            nm.CombatStateNotify += OnCombatStateNotify;
            nm.CombatEndNotify += OnCombatEndNotify;
            nm.CastStartNotify += OnCastStartNotify;
            nm.CombatEventNotify += OnCombatEventNotify;
            nm.PlayerDeathNotify += OnPlayerDeath;
            nm.LevelUpNotify += OnLevelUp;
        }

        private void UnsubscribeNetworkEvents(NetworkManager nm)
        {
            nm.MoveCancelNotify -= OnMoveCancelReceived;
            nm.MoveResponse -= OnMoveResponse;
            nm.RoleAttrUpdated -= OnRoleAttrUpdated;
            nm.CombatStateNotify -= OnCombatStateNotify;
            nm.CombatEndNotify -= OnCombatEndNotify;
            nm.CastStartNotify -= OnCastStartNotify;
            nm.CombatEventNotify -= OnCombatEventNotify;
            nm.PlayerDeathNotify -= OnPlayerDeath;
            nm.LevelUpNotify -= OnLevelUp;
        }

        private void OnRoleAttrUpdated(Game.FullRoleInfo roleInfo)
        {
            ApplyRoleInfo(roleInfo);
        }

        private void OnCombatStateNotify(Game.CombatStateNotify notify)
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null || nm.AccountId == 0)
                return;

            bool found = false;
            foreach (var unit in notify.Units)
            {
                if (!unit.IsPlayer || unit.EntityId != nm.AccountId)
                    continue;

                if (unit.MaxHp > 0)
                {
                    if (SyncHp((int)unit.Hp, (int)unit.MaxHp))
                        PlayHitEffect();
                    CombatAttrs[1] = unit.Hp;
                    CombatAttrs[2] = unit.MaxHp;
                }

                if (unit.MaxMp > 0)
                {
                    MpBarFillPercent = (float)unit.Mp / unit.MaxMp;
                    CombatAttrs[3] = unit.Mp;
                    CombatAttrs[4] = unit.MaxMp;
                }

                CastingSkill = unit.CastingSkill;
                CastProgress = unit.CastProgress;
                found = true;
                break;
            }

            IsInCombat = found;
            if (!found)
            {
                CastingSkill = "";
                CastProgress = 0;
            }

            QueueRedraw();
        }

        private void OnCombatEndNotify(Game.CombatEndNotify notify)
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null || nm.AccountId == 0)
                return;

            foreach (var id in notify.EntityIds)
            {
                if (id == nm.AccountId)
                {
                    IsInCombat = false;
                    CastingSkill = "";
                    CastProgress = 0;
                    QueueRedraw();
                    break;
                }
            }
        }

        private void OnCastStartNotify(Game.CastStartNotify notify)
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null || nm.AccountId == 0)
                return;
            if (notify.CasterId != nm.AccountId)
                return;

            CastingSkill = SkillDataUtil.GetName((uint)notify.SkillId) ?? $"Skill{notify.SkillId}";
            CastProgress = 0f;
            QueueRedraw();
        }

        private void OnCombatEventNotify(Game.CombatEventNotify notify)
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null || nm.AccountId == 0)
                return;
            if (notify.TargetId != nm.AccountId)
                return;

            // 即时更新 HP/MP（作为 CombatStateNotify 的补充）
            if (notify.HpDelta != 0 && CurrentMaxHp > 0)
            {
                int newHp = Mathf.Clamp(CurrentHp + notify.HpDelta, 0, CurrentMaxHp);
                if (SyncHp(newHp, CurrentMaxHp))
                    PlayHitEffect();
                CombatAttrs[1] = newHp;
            }
            if (notify.MpDelta != 0)
            {
                int maxMp = CombatAttrs.TryGetValue(4, out var mm) ? mm : 100;
                int curMp = CombatAttrs.TryGetValue(3, out var cm) ? cm : maxMp;
                int newMp = Mathf.Clamp(curMp + notify.MpDelta, 0, maxMp);
                MpBarFillPercent = maxMp > 0 ? (float)newMp / maxMp : 1f;
                CombatAttrs[3] = newMp;
            }
            QueueRedraw();
        }

        private void OnLevelUp(Game.LevelUpNotify notify)
        {
            GD.Print("[Player] Level Up! ", notify.OldLevel, " -> ", notify.NewLevel);
            QueueRedraw();
        }

        private void OnPlayerDeath(Game.PlayerDeathNotify notify)
        {
            GD.Print("[Player] Death! Respawning at (", notify.SpawnX, ",", notify.SpawnY, ")");

            ClearPendingServerGridCorrection();
            _currentTween?.Kill();
            _currentTween = null;
            _checkTimer?.Stop();
            _checkTimer?.QueueFree();
            _checkTimer = null;
            IsMoving = false;
            _bouncingBack = false;
            _collisionMove = false;
            _moveSentCount = 0;

            _gridPos = new Vector2I(notify.SpawnX, notify.SpawnY);
            _moveFromPos = _gridPos;
            Position = UiUtils.GridToWorld(_gridPos, GridSize);

            HealthBarFillPercent = 1.0f;
            if (notify.MaxHp > 0)
            {
                CombatAttrs[1] = notify.Hp;
                CombatAttrs[2] = notify.MaxHp;
            }

            if (notify.MaxMp > 0)
            {
                CombatAttrs[3] = notify.Mp;
                CombatAttrs[4] = notify.MaxMp;
            }

            CastingSkill = "";
            CastProgress = 0;
            QueueRedraw();
        }
    }
}
