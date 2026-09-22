using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 玩家战斗状态 — 移植自 Godot Player.NetworkSync.cs 的 CombatStateNotify/CastStartNotify/CombatEndNotify 处理。
    /// 一切战斗显示由 CombatStateNotify 驱动（AGENTS.md：禁止监听 CombatStartNotify 做显示）。
    /// </summary>
    public partial class PlayerEntity
    {
        /// <summary>是否战斗中（光环显示依据）</summary>
        public bool IsInCombat { get; private set; }

        private uint _nextSkillId;
        private float _nextSkillReadyIn;
        private float _castProgress;
        private Coroutine _castRoutine;

        private void SubscribeCombatEvents()
        {
            var nm = Net.NetworkManager.Instance;
            if (nm == null) return;
            nm.CombatStateNotify += OnCombatState;
            nm.CombatEndNotify += OnCombatEnd;
            nm.CastStartNotify += OnCastStartNotify;
            nm.LevelUpNotify += OnLevelUpNotify;
        }

        private void UnsubscribeCombatEvents()
        {
            var nm = Net.NetworkManager.Instance;
            if (nm == null) return;
            nm.CombatStateNotify -= OnCombatState;
            nm.CombatEndNotify -= OnCombatEnd;
            nm.CastStartNotify -= OnCastStartNotify;
            nm.LevelUpNotify -= OnLevelUpNotify;
        }

        // ============ CombatStateNotify ============

        private void OnCombatState(Game.CombatStateNotify notify)
        {
            var nm = Net.NetworkManager.Instance;
            Game.CombatStateNotify.Types.CombatUnit self = null;
            if (nm != null)
            {
                foreach (var unit in notify.Units)
                {
                    // 注意 NPC unit 的 IsPlayer 也是 true，必须 AccountId 双重判定（AGENTS.md 第 17 条）
                    if (unit.IsPlayer && unit.EntityId == (ulong)nm.AccountId)
                    {
                        self = unit;
                        break;
                    }
                }
            }

            if (self == null)
            {
                // 空 units 或自己不在列表 = 脱战复位
                ResetCombatState();
                return;
            }

            // 血蓝同步（掉血 → 受击反馈）
            int oldMax = Mathf.Max(1, self.MaxHp);
            if (SetHpFill((float)self.Hp / oldMax))
                PlayHitEffect();
            SetMpFill((float)self.Mp / Mathf.Max(1, self.MaxMp));

            IsInCombat = self.InCombat;
            SetCombatAuraVisible(IsInCombat);
            _nextSkillId = self.NextSkillId;
            _nextSkillReadyIn = self.NextSkillReadyIn;

            // CastingSkill 兜底校正：服务器说没在读条 → 清空停动画
            if (string.IsNullOrEmpty(self.CastingSkill) && !string.IsNullOrEmpty(CastingSkill))
            {
                CastingSkill = "";
                StopCastAnimation();
            }

            RefreshCastingVisuals();
        }

        private void ResetCombatState()
        {
            IsInCombat = false;
            SetCombatAuraVisible(false);
            _nextSkillId = 0;
            _nextSkillReadyIn = 0f;
            if (!string.IsNullOrEmpty(CastingSkill))
            {
                CastingSkill = "";
                StopCastAnimation();
            }
            RefreshCastingVisuals();
        }

        // ============ CastStartNotify（读条起点）============

        private void OnCastStartNotify(Game.CastStartNotify notify)
        {
            var nm = Net.NetworkManager.Instance;
            if (nm == null || notify.CasterId != (ulong)nm.AccountId) return;

            CastingSkill = SkillDataUtil.GetName(notify.SkillId);
            StartCastAnimation(notify.CastTime);
            RefreshCastingVisuals();
        }

        /// <summary>本地读条动画：castTime 秒线性 0→1（不吃服务器 cast_progress，对齐 Godot）。</summary>
        private void StartCastAnimation(float castTime)
        {
            StopCastAnimation();
            _castProgress = 0f;
            if (castTime <= 0f)
            {
                _castProgress = 1f;
                return;
            }
            _castRoutine = StartCoroutine(CastAnimationRoutine(castTime));
        }

        private System.Collections.IEnumerator CastAnimationRoutine(float castTime)
        {
            float t = 0f;
            while (t < castTime)
            {
                t += Time.deltaTime;
                _castProgress = Mathf.Clamp01(t / castTime);
                SetCastFill(_castProgress);
                yield return null;
            }
            _castProgress = 1f;
            SetCastFill(1f);
            _castRoutine = null;
        }

        private void StopCastAnimation()
        {
            if (_castRoutine != null)
            {
                StopCoroutine(_castRoutine);
                _castRoutine = null;
            }
            _castProgress = 0f;
            SetCastBarVisible(false);
        }

        // ============ 四态显示状态机（对齐 Godot RefreshCastingVisuals）============

        private void RefreshCastingVisuals()
        {
            if (!string.IsNullOrEmpty(CastingSkill))
            {
                // 1) 施法中：标签=技能名，读条显示
                SetLabel(3, CastingSkill);
                SetCastBarVisible(true);
                SetCastFill(_castProgress);
            }
            else if (IsInCombat && _nextSkillId > 0)
            {
                // 2) 战斗中且有下个技能："技能准备中" + 进度 1-ReadyIn/totalCd
                SetLabel(3, "技能准备中");
                SetCastBarVisible(true);
                double totalCd = SkillDataUtil.Get(_nextSkillId).cd;
                float fill = totalCd > 0 ? 1f - _nextSkillReadyIn / (float)totalCd : 0f;
                SetCastFill(Mathf.Clamp01(fill));
            }
            else if (IsInCombat)
            {
                // 3) 战斗中无下个技能：空标签 + 隐藏
                SetLabel(3, "");
                SetCastBarVisible(false);
            }
            else
            {
                // 4) 非战斗："闲逛中..." + 隐藏
                SetLabel(3, "闲逛中...");
                SetCastBarVisible(false);
            }
        }

        // ============ CombatEndNotify ============

        private void OnCombatEnd(Game.CombatEndNotify notify)
        {
            var nm = Net.NetworkManager.Instance;
            if (nm == null) return;
            foreach (var id in notify.EntityIds)
            {
                if (id == (ulong)nm.AccountId)
                {
                    ResetCombatState();
                    return;
                }
            }
        }

        private void OnLevelUpNotify(Game.LevelUpNotify notify)
        {
            Debug.Log($"[Player] 升级: LV.{notify.OldLevel} → LV.{notify.NewLevel}");
            // 标签/属性刷新由同时推送的 RoleAttrNotify 驱动（对齐 Godot）
        }
    }
}
