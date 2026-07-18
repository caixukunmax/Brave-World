using UnityClientSharp.Map.Rendering;
using UnityClientSharp.Net;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 玩家实体（显示层）。
    /// 移植自 Godot Player 的显示部分：从 FullRoleInfo 应用名字/等级/职业/称号/位置/血蓝，
    /// RoleAttrUpdated 重刷。裁剪：移动同步、战斗视觉（光环/施法条/受击）、方向箭头、等级徽章。
    /// </summary>
    public partial class PlayerEntity : EntityVisualBase
    {
        public static PlayerEntity Instance { get; private set; }

        public string CharacterName { get; private set; } = "";
        public int Level { get; private set; } = 1;

        /// <summary>Attrs key 约定（对齐 Godot Player.Appearance）：1=hp 2=maxHp 3=mp 4=maxMp 10=move_speed</summary>
        private const int AttrHp = 1, AttrMaxHp = 2, AttrMp = 3, AttrMaxMp = 4, AttrMoveSpeed = 10;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.RoleAttrUpdated -= OnRoleAttrUpdated;
                NetworkManager.Instance.MoveResponse -= OnMoveResponse;
                NetworkManager.Instance.MoveCancelNotify -= OnMoveCancelReceived;
                NetworkManager.Instance.PlayerDeathNotify -= OnPlayerDeath;
            }
            UnsubscribeCombatEvents();
        }

        public void SetupFromRoleInfo(Game.FullRoleInfo roleInfo, int gridSize)
        {
            Setup(1, new Vector2Int(roleInfo.GridX, roleInfo.GridY), gridSize,
                roleInfo.SizeX > 0 ? roleInfo.SizeX : 1,
                roleInfo.SizeY > 0 ? roleInfo.SizeY : 1,
                sortingOrder: 2); // 装饰之上、宝箱/怪物/NPC/掉落之下（对齐 Godot 兄弟顺序）

            if (roleInfo.Direction >= 0)
                Direction = roleInfo.Direction;

            ApplyRoleInfo(roleInfo);

            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.RoleAttrUpdated += OnRoleAttrUpdated;
                NetworkManager.Instance.MoveResponse += OnMoveResponse;
                NetworkManager.Instance.MoveCancelNotify += OnMoveCancelReceived;
                NetworkManager.Instance.PlayerDeathNotify += OnPlayerDeath;
            }
            SubscribeCombatEvents();
        }

        /// <summary>移植 Godot Player.Appearance.ApplyRoleInfo 的显示部分。</summary>
        public void ApplyRoleInfo(Game.FullRoleInfo roleInfo)
        {
            CharacterName = roleInfo.RoleName ?? "";
            Level = (int)roleInfo.Level;

            SetLabel(0, $"LV.{Level} {CharacterName}");
            SetLabel(1, roleInfo.Job ?? "");
            SetLabel(2, roleInfo.Title ?? "");
            // 3 行（状态）由战斗阶段动态管理，本期留空

            int hp = GetAttr(roleInfo, AttrHp);
            int maxHp = Mathf.Max(1, GetAttr(roleInfo, AttrMaxHp));
            int mp = GetAttr(roleInfo, AttrMp);
            int maxMp = Mathf.Max(1, GetAttr(roleInfo, AttrMaxMp));
            SetHpFill((float)hp / maxHp);
            SetMpFill((float)mp / maxMp);

            // 移速 → 每格时长：durationMs = clamp(720 - moveSpeed, 120, 600)
            int moveSpeed = GetAttr(roleInfo, AttrMoveSpeed);
            if (moveSpeed > 0)
            {
                int durationMs = Mathf.Clamp(720 - moveSpeed, 120, 600);
                MoveDuration = durationMs / 1000f;
            }

            // 位置矫正：逻辑格更新 + 排队服务器矫正（对齐 Godot，不再直接吸附）
            QueueServerGridCorrection(new Vector2Int(roleInfo.GridX, roleInfo.GridY));
        }

        private void OnRoleAttrUpdated(Game.FullRoleInfo roleInfo) => ApplyRoleInfo(roleInfo);

        private static int GetAttr(Game.FullRoleInfo roleInfo, uint key)
        {
            foreach (var a in roleInfo.Attrs)
                if (a.Key == key) return a.Value;
            return 0;
        }
    }
}
