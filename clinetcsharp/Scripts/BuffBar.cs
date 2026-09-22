using Godot;
using Game;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// Buff 栏 — 技能栏左侧，显示当前角色身上的 Buff/Debuff 列表
    /// 一排等大方块，不显示名字，按添加顺序排列
    /// </summary>
    public partial class BuffBar : HBoxContainer
    {
        [Export] public int IconSize { get; set; } = 36;
        [Export] public int SlotSpacing { get; set; } = 4;
        [Export] public int MarginRight { get; set; } = 20;
        [Export] public int MarginBottom { get; set; } = 20;
        [Export] public int OffsetX { get; set; } = 0;
        [Export] public int OffsetY { get; set; } = 0;
        [Export] public bool RightAlign { get; set; } = false; // 靠右对齐：新buff从右往左压栈

        private const int MaxBuffs = 10;
        private NetworkManager? _network;
        private readonly List<BuffSlot> _slots = new();

        public override void _Ready()
        {
            AddToGroup("buff_bar");
            AddThemeConstantOverride("separation", SlotSpacing);
            BuildSlots();
            ApplyLayout();

            _network = UiServices.GetNetworkManager(this);
            if (_network != null)
            {
                _network.CombatStateNotify += OnCombatStateNotify;
                _network.BuffUpdateNotify += OnBuffUpdateNotify;
            }
        }

        public override void _ExitTree()
        {
            if (_network != null)
            {
                _network.CombatStateNotify -= OnCombatStateNotify;
                _network.BuffUpdateNotify -= OnBuffUpdateNotify;
            }
        }

        /// <summary>重新计算布局（由 DebugPanel 调用）</summary>
        public void RebuildLayout()
        {
            AddThemeConstantOverride("separation", SlotSpacing);
            foreach (var slot in _slots)
                slot.SyncSize(IconSize);
            ApplyLayout();
        }

        /// <summary>强制显示所有 slot（调试用，方便调整位置）</summary>
        public void ForceShowAll()
        {
            for (int i = 0; i < MaxBuffs; i++)
            {
                _slots[i].SetBuff(1, "测试", 1, 10f, 0);
            }
        }

        private void BuildSlots()
        {
            for (int i = 0; i < MaxBuffs; i++)
            {
                var slot = new BuffSlot(IconSize);
                slot.SetEmpty(); // 透明占位，不用 Visible=false
                _slots.Add(slot);
                AddChild(slot);
            }
        }

        private void ApplyLayout()
        {
            float slotSize = IconSize + 4; // 图标 + 内边距
            float totalW = MaxBuffs * slotSize + (MaxBuffs - 1) * SlotSpacing;

            // 定位在技能栏左侧
            AnchorLeft = 1.0f;
            AnchorRight = 1.0f;
            AnchorTop = 1.0f;
            AnchorBottom = 1.0f;

            // 技能栏在右下角，Buff 栏在其左边
            float skillBarWidth = 234;
            OffsetLeft = -skillBarWidth - totalW - MarginRight + OffsetX;
            OffsetTop = -slotSize - MarginBottom + OffsetY;
            OffsetRight = -skillBarWidth - MarginRight + OffsetX;
            OffsetBottom = -MarginBottom + OffsetY;

            GrowHorizontal = Control.GrowDirection.Begin;
            GrowVertical = Control.GrowDirection.Begin;
        }

        private void OnCombatStateNotify(CombatStateNotify notify)
        {
            if (_network == null) return;
            ulong myId = _network.AccountId;

            foreach (var unit in notify.Units)
            {
                if (unit.EntityId == myId && unit.IsPlayer)
                {
                    UpdateBuffs(unit.Buffs);
                    return;
                }
            }

            // 不在战斗中 — 不清空 buff，等 BuffUpdateNotify 来决定
            // （脱战时服务端先推 BuffUpdateNotify 再推空 CombatStateNotify）
        }

        private void OnBuffUpdateNotify(Game.BuffUpdateNotify notify)
        {
            if (_network == null) return;
            ulong myId = _network.AccountId;

            if (notify.EntityId != myId) return;

            if (notify.Buffs.Count == 0)
            {
                ClearAll();
                return;
            }

            int count = Mathf.Min(notify.Buffs.Count, MaxBuffs);
            int startSlot = RightAlign ? MaxBuffs - count : 0;

            // 先清空所有
            for (int i = 0; i < MaxBuffs; i++)
                _slots[i].SetEmpty();

            // 填充
            for (int i = 0; i < count; i++)
            {
                var b = notify.Buffs[i];
                _slots[startSlot + i].SetBuff(b.BuffId, b.BuffName, b.Stacks, b.RemainingTime, b.ShieldAmount);
            }
        }

        private void UpdateBuffs(Google.Protobuf.Collections.RepeatedField<CombatStateNotify.Types.BuffInfo> buffs)
        {
            int count = Mathf.Min(buffs.Count, MaxBuffs);
            int startSlot = RightAlign ? MaxBuffs - count : 0;

            for (int i = 0; i < MaxBuffs; i++)
                _slots[i].SetEmpty();

            for (int i = 0; i < count; i++)
            {
                _slots[startSlot + i].SetBuff(buffs[i].BuffId, buffs[i].BuffName, buffs[i].Stacks, buffs[i].RemainingTime, buffs[i].ShieldAmount);
            }
        }

        private void ClearAll()
        {
            foreach (var slot in _slots)
                slot.SetEmpty();
        }

        // ============ BuffSlot (inner control) ============

        private partial class BuffSlot : PanelContainer
        {
            private int _iconSize;
            private int _buffId;
            private float _remainingTime; // 本地倒计时
            private bool _isPermanent;    // 永久 buff 不倒计时
            private ColorRect _iconRect;
            private Label _timeLabel;
            private Label _stackLabel;

            public BuffSlot(int iconSize)
            {
                _iconSize = iconSize;
                float size = iconSize + 4;
                CustomMinimumSize = new Vector2(size, size);
                Size = new Vector2(size, size);

                // 外框
                AddThemeStyleboxOverride("panel", UiStyles.CreateSlotStyle());

                BuildChildren();
            }

            private void BuildChildren()
            {
                // 品质色块占位
                _iconRect = new ColorRect
                {
                    Name = "IconRect",
                    AnchorLeft = 0.1f,
                    AnchorTop = 0.1f,
                    AnchorRight = 0.9f,
                    AnchorBottom = 0.9f,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                AddChild(_iconRect);

                // 剩余时间（右下角）
                _timeLabel = new Label
                {
                    Name = "TimeLabel",
                    AnchorLeft = 0,
                    AnchorBottom = 1,
                    AnchorRight = 1,
                    AnchorTop = 1,
                    OffsetTop = -14,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                _timeLabel.AddThemeFontSizeOverride("font_size", 9);
                _timeLabel.AddThemeColorOverride("font_color", Colors.White);
                AddChild(_timeLabel);

                // 叠加层数（左上角）
                _stackLabel = new Label
                {
                    Name = "StackLabel",
                    AnchorLeft = 0,
                    AnchorTop = 0,
                    AnchorRight = 0.5f,
                    AnchorBottom = 0.4f,
                    OffsetLeft = 2,
                    OffsetTop = 1,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                _stackLabel.AddThemeFontSizeOverride("font_size", 10);
                _stackLabel.AddThemeColorOverride("font_color", Colors.Yellow);
                AddChild(_stackLabel);
            }

            public void SetBuff(int buffId, string buffName, int stacks, float remainingTime, int shieldAmount)
            {
                _buffId = buffId;
                _remainingTime = remainingTime;
                _isPermanent = remainingTime < 0; // -1 = 永久
                _iconRect.Color = GetBuffColor(buffId);

                UpdateTimeDisplay();

                // 层数
                if (stacks > 1)
                {
                    _stackLabel.Text = $"x{stacks}";
                    _stackLabel.Visible = true;
                }
                else
                {
                    _stackLabel.Visible = false;
                }

                // 护盾特殊指示
                if (shieldAmount > 0)
                {
                    _iconRect.Color = new Color(0.3f, 0.6f, 1f); // 蓝色=护盾
                }

                // 有 buff 时不透明
                Modulate = new Color(1, 1, 1, 1f);

                // 非永久 buff 需要 _Process 来倒计时；永久 buff 不需要
                SetProcess(!_isPermanent);
            }

            /// <summary>
            /// 清空 slot 内容，但保持占位（透明）
            /// </summary>
            public void SetEmpty()
            {
                _buffId = 0;
                _remainingTime = 0;
                _isPermanent = false;
                _iconRect.Color = new Color(0, 0, 0, 0); // 透明
                _timeLabel.Visible = false;
                _stackLabel.Visible = false;
                Modulate = new Color(1, 1, 1, 0f); // 整体透明但占位
                SetProcess(false); // 空槽位不需要 _Process
            }

            public override void _Process(double delta)
            {
                if (_isPermanent || Modulate.A < 0.5f) return;
                if (!_timeLabel.Visible) return;

                _remainingTime -= (float)delta;
                if (_remainingTime <= 0)
                {
                    _remainingTime = 0;
                    // buff 过期：变透明（等服务端推送正式清除）
                    SetEmpty();
                    return;
                }
                UpdateTimeDisplay();
            }

            private void UpdateTimeDisplay()
            {
                if (_isPermanent)
                {
                    _timeLabel.Visible = false;
                    return;
                }
                _timeLabel.Visible = true;
                // 只在显示文本实际变化时才赋值，避免每帧触发 Label 重排
                string newText = _remainingTime >= 10f ? $"{_remainingTime:F0}s" : $"{_remainingTime:F1}s";
                if (_timeLabel.Text != newText)
                    _timeLabel.Text = newText;
            }

            public void SyncSize(int iconSize)
            {
                _iconSize = iconSize;
                float size = iconSize + 4;
                CustomMinimumSize = new Vector2(size, size);
                Size = new Vector2(size, size);
            }

            /// <summary>根据 Buff ID 生成颜色（无美术素材时的替代方案）</summary>
            private static Color GetBuffColor(int buffId)
            {
                // Buff: 绿/蓝/金系, Debuff: 红/紫系
                return buffId switch
                {
                    // Debuff (1-2, 6-7, 9-10)
                    1 => new Color(0.6f, 0.2f, 0.8f),   // 中毒-紫
                    2 => new Color(0.3f, 0.6f, 1f),     // 冰冻-冰蓝
                    6 => new Color(0.4f, 0.7f, 0.9f),   // 减速-浅蓝
                    7 => new Color(1f, 0.4f, 0.1f),     // 灼烧-橙红
                    9 => new Color(0.7f, 0.3f, 0.3f),   // 破甲-暗红
                    10 => new Color(0.9f, 0.9f, 0.1f),  // 眩晕-黄
                    // Buff (3-5, 8, 11)
                    3 => new Color(1f, 0.8f, 0.2f),     // 战吼-金
                    4 => new Color(0.3f, 0.6f, 1f),     // 护盾-蓝
                    5 => new Color(0.6f, 0.5f, 0.3f),   // 石肤-棕
                    8 => new Color(0.9f, 0.9f, 0.4f),   // 祝福-浅金
                    11 => new Color(1f, 0.3f, 0.1f),    // 狂暴-深红
                    _ => new Color(0.5f, 0.5f, 0.5f),  // 未知-灰
                };
            }
        }
    }
}