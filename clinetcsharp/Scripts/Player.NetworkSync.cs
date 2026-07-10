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

        public uint NextSkillId { get; set; }
        public float NextSkillReadyIn { get; set; }

        private void OnCombatStateNotify(Game.CombatStateNotify notify)
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null || nm.AccountId == 0)
                return;

            bool found = false;
            string serverCastingSkill = "";
            uint nextSkillId = 0;
            float nextSkillReadyIn = 0f;
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

                serverCastingSkill = unit.CastingSkill;
                nextSkillId = unit.NextSkillId;
                nextSkillReadyIn = unit.NextSkillReadyIn;
                found = unit.InCombat;
                break;
            }

            IsInCombat = found;
            NextSkillId = nextSkillId;
            NextSkillReadyIn = nextSkillReadyIn;

            if (!found)
            {
                CastingSkill = "";
                StopCastAnimation();
            }
            else
            {
                // 服务端未推送技能名时，保持本地已识别的技能名（本地动画更平滑）
                if (!string.IsNullOrEmpty(serverCastingSkill) && string.IsNullOrEmpty(CastingSkill))
                    CastingSkill = serverCastingSkill;
                else if (string.IsNullOrEmpty(serverCastingSkill) && !string.IsNullOrEmpty(CastingSkill))
                {
                    CastingSkill = "";
                    StopCastAnimation();
                }
            }

            RefreshCastingVisuals();
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
                    StopCastAnimation();
                    RefreshCastingVisuals();
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

            GD.Print($"[Player] OnCastStartNotify: skill={notify.SkillId}, castTime={notify.CastTime:F2}s");

            CastingSkill = SkillDataUtil.GetName((uint)notify.SkillId) ?? $"Skill{notify.SkillId}";
            StartCastAnimation(notify.CastTime);
            RefreshCastingVisuals();
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
            Position = GetWorldPositionForGridPos(_gridPos);

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
            StopCastAnimation();
            RefreshCastingVisuals();
        }

        private void RefreshCastingVisuals()
        {
            if (!string.IsNullOrEmpty(CastingSkill))
            {
                // 施法中：显示技能名 + 读条进度
                SetLabelText(3, CastingSkill);
                CastBarFillPercent = CastProgress;
                CastBarVisible = true;
            }
            else if (IsInCombat && NextSkillId > 0)
            {
                // 过渡期：显示“技能准备中” + 下一个技能 CD 倒计时
                SetLabelText(3, "技能准备中");
                var skillData = SkillDataUtil.Get(NextSkillId);
                double totalCd = skillData.cd > 0 ? skillData.cd : NextSkillReadyIn;
                CastBarFillPercent = totalCd > 0
                    ? 1f - Mathf.Clamp(NextSkillReadyIn / (float)totalCd, 0f, 1f)
                    : 1f;
                CastBarVisible = true;
            }
            else if (IsInCombat)
            {
                // 战斗中但无下一个技能：不显示
                SetLabelText(3, "");
                CastBarVisible = false;
            }
            else
            {
                // 非战斗：显示闲逛中
                SetLabelText(3, "闲逛中...");
                CastBarVisible = false;
            }

            QueueRedraw();
        }
    }
}
