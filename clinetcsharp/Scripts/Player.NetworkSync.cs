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
            nm.PlayerDeathNotify += OnPlayerDeath;
            nm.LevelUpNotify += OnLevelUp;
        }

        private void UnsubscribeNetworkEvents(NetworkManager nm)
        {
            nm.MoveCancelNotify -= OnMoveCancelReceived;
            nm.MoveResponse -= OnMoveResponse;
            nm.RoleAttrUpdated -= OnRoleAttrUpdated;
            nm.CombatStateNotify -= OnCombatStateNotify;
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

            if (!found)
            {
                CastingSkill = "";
                CastProgress = 0;
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
