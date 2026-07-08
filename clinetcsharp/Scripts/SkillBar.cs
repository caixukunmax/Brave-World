using Godot;
using Protocol;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 固定技能栏 — 屏幕右下角，显示已装备技能 + CD 遮罩
    /// 订阅 CombatStateNotify 获取服务器推送的 CD 数据
    /// </summary>
    public partial class SkillBar : HBoxContainer
    {
        [Export] public int IconSize { get; set; } = 44;
        public int SlotWidth => IconSize + 12; // 图标 + 两侧边距
        [Export] public int SlotSpacing { get; set; } = 6;
        [Export] public int MarginRight { get; set; } = 20;
        [Export] public int MarginBottom { get; set; } = 20;
        [Export] public int NameFontSize { get; set; } = 10;
        private const int MaxSlots = 4;
        private const int CdRowHeight = 16;
        private const float DefaultDoubleClickTimeout = 0.4f;
        public int NameRowHeight => Mathf.Max(NameFontSize + 8, 18);

        private NetworkManager _network;
        private readonly SkillSlot[] _slots = new SkillSlot[MaxSlots];
        private int _selectedSlot = -1;
        private int _pendingClickSlot = -1;
        private Timer _clickTimer;

        public override void _Ready()
        {
            AddToGroup("skill_bar");
            BuildSlots();

            float doubleClickSpeed = (float)ProjectSettings.GetSetting("input_devices/pointing/double_click_speed");
            if (doubleClickSpeed <= 0f)
                doubleClickSpeed = DefaultDoubleClickTimeout;
            _clickTimer = new Timer { WaitTime = doubleClickSpeed, OneShot = true };
            AddChild(_clickTimer);
            _clickTimer.Timeout += OnPendingClick;

            ApplyLayout();

            _network = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (_network != null)
            {
                _network.CombatStateNotify += OnCombatStateNotify;
                _network.CastStartNotify += OnCastStartNotify;
                _network.CastResultNotify += OnCastResultNotify;
                _network.RoleAttrUpdated += OnRoleAttrUpdated;
                _network.SetPreferredSkillResponse += OnSetPreferredSkillResponse;
                _network.CastStartNotify += OnCastStartNotifyForSelection;
                _network.CombatEndNotify += OnCombatEndForSelection;
                _network.PlayerDeathNotify += OnPlayerDeathForSelection;
            }

            RefreshSlots();
        }

        public override void _ExitTree()
        {
            if (_clickTimer != null)
            {
                _clickTimer.Timeout -= OnPendingClick;
                _clickTimer.Stop();
                RemoveChild(_clickTimer);
                _clickTimer.QueueFree();
                _clickTimer = null;
            }

            if (_network != null)
            {
                _network.CombatStateNotify -= OnCombatStateNotify;
                _network.CastStartNotify -= OnCastStartNotify;
                _network.CastResultNotify -= OnCastResultNotify;
                _network.RoleAttrUpdated -= OnRoleAttrUpdated;
                _network.SetPreferredSkillResponse -= OnSetPreferredSkillResponse;
                _network.CastStartNotify -= OnCastStartNotifyForSelection;
                _network.CombatEndNotify -= OnCombatEndForSelection;
                _network.PlayerDeathNotify -= OnPlayerDeathForSelection;
            }
        }

        /// <summary>
        /// 重新计算布局（由 DebugPanel 调用）
        /// </summary>
        public void RebuildLayout()
        {
            float slotHeight = CdRowHeight + IconSize + NameRowHeight;
            AddThemeConstantOverride("separation", SlotSpacing);
            foreach (var slot in _slots)
            {
                if (slot != null)
                {
                    slot.CustomMinimumSize = new Vector2(SlotWidth, slotHeight);
                    slot.Size = new Vector2(SlotWidth, slotHeight);
                    slot.SyncIconSize(IconSize);
                    slot.SyncNameFontSize(NameFontSize);
                }
            }
            ApplyLayout();
        }

        private void BuildSlots()
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                _slots[i] = new SkillSlot(i, this);
                AddChild(_slots[i]);
            }
        }

        private void ApplyLayout()
        {
            AddThemeConstantOverride("separation", SlotSpacing);

            float slotHeight = CdRowHeight + IconSize + NameRowHeight;
            float totalW = MaxSlots * SlotWidth + (MaxSlots - 1) * SlotSpacing;
            CustomMinimumSize = new Vector2(totalW, slotHeight);

            AnchorLeft = 1.0f;
            AnchorRight = 1.0f;
            AnchorTop = 1.0f;
            AnchorBottom = 1.0f;
            OffsetLeft = -totalW - MarginRight;
            OffsetTop = -slotHeight - MarginBottom;
            OffsetRight = -MarginRight;
            OffsetBottom = -MarginBottom;
            GrowHorizontal = Control.GrowDirection.Begin;
            GrowVertical = Control.GrowDirection.Begin;
        }

        private void OnRoleAttrUpdated(Game.FullRoleInfo _)
        {
            CallDeferred(nameof(RefreshSlots));
        }

        private void OnCombatStateNotify(Game.CombatStateNotify notify)
        {
            if (_network == null) return;
            ulong myId = _network.AccountId;

            foreach (var unit in notify.Units)
            {
                if (unit.EntityId == myId && unit.IsPlayer)
                {
                    UpdateCds(unit.SkillCds);
                    return;
                }
            }

            ClearAllCds();
        }

        private void OnCastStartNotify(Game.CastStartNotify notify)
        {
            // 读条开始：可在此处添加读条动画或音效提示
            GD.Print($"[SkillBar] CastStart: caster={notify.CasterId} skill={notify.SkillId} time={notify.CastTime:F1}s");
        }

        private void OnCastResultNotify(Game.CastResultNotify notify)
        {
            if (notify.IsMiss)
            {
                GD.Print($"[SkillBar] CastResult: caster={notify.CasterId} skill={notify.SkillId} MISS");
            }
            else
            {
                GD.Print($"[SkillBar] CastResult: caster={notify.CasterId} skill={notify.SkillId} targets=[{string.Join(",", notify.TargetIds)}]");
            }
        }

        private void OnSetPreferredSkillResponse(Game.SetPreferredSkillResponse rsp)
        {
            if (rsp.Code != Common.ErrorCode.Success) return;

            int slotIndex = -1;
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i].SkillId == rsp.PreferredSkillId)
                {
                    slotIndex = i;
                    break;
                }
            }
            SetSelectedSlot(slotIndex);
        }

        private void OnCastStartNotifyForSelection(Game.CastStartNotify notify)
        {
            if (_network == null) return;
            if (notify.CasterId != _network.AccountId) return;

            // 释放的是当前高亮技能，清除高亮
            if (_selectedSlot >= 0 && _selectedSlot < MaxSlots)
            {
                if (_slots[_selectedSlot].SkillId == notify.SkillId)
                    SetSelectedSlot(-1);
            }
        }

        private void OnCombatEndForSelection(Game.CombatEndNotify notify)
        {
            if (_network == null) return;
            if (notify.EntityIds.Contains(_network.AccountId))
                SetSelectedSlot(-1);
        }

        private void OnPlayerDeathForSelection(Game.PlayerDeathNotify notify)
        {
            SetSelectedSlot(-1);
        }

        private void RefreshSlots()
        {
            if (_network == null) return;
            var equipped = _network.CachedEquippedSkills;

            for (int i = 0; i < MaxSlots; i++)
            {
                uint skillId = i < equipped.Count ? equipped[i] : 0;
                _slots[i].SetSkill(skillId);
            }
        }

        private void UpdateCds(Google.Protobuf.Collections.RepeatedField<Game.CombatStateNotify.Types.SkillCdEntry> cds)
        {
            var cdMap = new Dictionary<uint, (float remaining, float total)>();
            foreach (var entry in cds)
                cdMap[entry.SkillId] = (entry.RemainingCd, entry.TotalCd);

            for (int i = 0; i < MaxSlots; i++)
            {
                uint skillId = _slots[i].SkillId;
                if (skillId > 0 && cdMap.TryGetValue(skillId, out var cd))
                    _slots[i].SetCd(cd.remaining, cd.total);
                else
                    _slots[i].ClearCd();
            }
        }

        private void ClearAllCds()
        {
            foreach (var slot in _slots)
                slot.ClearCd();
        }

        internal void OnSlotClicked(int slotIndex)
        {
            var slot = _slots[slotIndex];
            if (slot.SkillId <= 0) return;

            if (_network == null) return;

            // 点击已高亮的槽位 = 取消优先
            uint requestSkillId = _selectedSlot == slotIndex ? 0 : slot.SkillId;

            var req = new Game.SetPreferredSkillRequest { SkillId = requestSkillId };
            _network.SendPacket(Protocol.MessageId.GameSetPreferredSkillReq, req);
        }

        internal void OnSlotDoubleClicked(int slotIndex)
        {
            var slot = _slots[slotIndex];
            if (slot.SkillId <= 0) return;
            if (slot.IsOnCooldown) return;
            if (_network == null) return;

            var req = new Game.CastRequest
            {
                SkillId = slot.SkillId,
                Interrupt = true,
            };
            _network.SendPacket(Protocol.MessageId.GameCastReq, req);
        }

        private void SetSelectedSlot(int slotIndex)
        {
            if (_selectedSlot >= 0 && _selectedSlot < MaxSlots)
                _slots[_selectedSlot].SetSelected(false);

            _selectedSlot = slotIndex;

            if (_selectedSlot >= 0 && _selectedSlot < MaxSlots)
                _slots[_selectedSlot].SetSelected(true);
        }

        internal void ScheduleClick(int slotIndex)
        {
            // 快速点击不同槽位时，先把上一个未决的单击下发，避免丢失
            if (_pendingClickSlot >= 0 && _pendingClickSlot != slotIndex)
                OnSlotClicked(_pendingClickSlot);

            _pendingClickSlot = slotIndex;
            _clickTimer?.Start();
        }

        internal void CancelPendingClick()
        {
            _pendingClickSlot = -1;
            _clickTimer?.Stop();
        }

        private void OnPendingClick()
        {
            if (_pendingClickSlot >= 0)
            {
                OnSlotClicked(_pendingClickSlot);
                _pendingClickSlot = -1;
            }
        }

        // ============ SkillSlot (inner control) ============

        private partial class SkillSlot : PanelContainer
        {
            private readonly int _index;
            private readonly SkillBar _bar;

            private VBoxContainer _vbox;
            private Label _cdLabel;
            private PanelContainer _iconBox;
            private SkillIconView _iconView;
            private Label _nameLabel;
            private ColorRect _cdMask;
            private PanelContainer _highlight;

            public uint SkillId { get; private set; }

            public SkillSlot(int index, SkillBar bar)
            {
                _index = index;
                _bar = bar;
                float slotHeight = CdRowHeight + _bar.IconSize + _bar.NameRowHeight;
                CustomMinimumSize = new Vector2(_bar.SlotWidth, slotHeight);
                Size = new Vector2(_bar.SlotWidth, slotHeight);

                // 外层无背景无边框，只有图标框有框
                AddThemeStyleboxOverride("panel", new StyleBoxFlat
                {
                    BgColor = new Color(0, 0, 0, 0),
                    BorderWidthBottom = 0,
                    BorderWidthLeft = 0,
                    BorderWidthRight = 0,
                    BorderWidthTop = 0,
                });

                BuildChildren();
            }

            private void BuildChildren()
            {
                // --- 上中下垂直布局 ---
                _vbox = new VBoxContainer
                {
                    Name = "VBox",
                    AnchorLeft = 0,
                    AnchorTop = 0,
                    AnchorRight = 1,
                    AnchorBottom = 1,
                    OffsetLeft = 2,
                    OffsetTop = 2,
                    OffsetRight = -2,
                    OffsetBottom = -2,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                _vbox.AddThemeConstantOverride("separation", 0);
                AddChild(_vbox);

                // 上: CD 行
                _cdLabel = new Label
                {
                    Name = "CdLabel",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    CustomMinimumSize = new Vector2(0, CdRowHeight),
                    Text = "",
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                _cdLabel.AddThemeFontSizeOverride("font_size", 11);
                _cdLabel.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.6f));
                _vbox.AddChild(_cdLabel);

                // 中: 技能图标框 (暂空，画一个边框占位)
                _iconBox = new PanelContainer
                {
                    Name = "IconBox",
                    CustomMinimumSize = new Vector2(_bar.IconSize, _bar.IconSize),
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                _iconBox.AddThemeStyleboxOverride("panel", UiStyles.CreateSlotStyle());
                _vbox.AddChild(_iconBox);

                _iconView = new SkillIconView
                {
                    Name = "IconView",
                    AnchorLeft = 0,
                    AnchorTop = 0,
                    AnchorRight = 1,
                    AnchorBottom = 1,
                    OffsetLeft = 4,
                    OffsetTop = 4,
                    OffsetRight = -4,
                    OffsetBottom = -4,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                _iconBox.AddChild(_iconView);

                // CD 遮罩 — 覆盖图标框，从下往上填充
                _cdMask = new ColorRect
                {
                    Name = "CdMask",
                    Color = new Color(0, 0, 0, 0.55f),
                    Visible = false,
                    MouseFilter = MouseFilterEnum.Ignore,
                    AnchorLeft = 0,
                    AnchorRight = 1,
                    AnchorBottom = 1,
                    AnchorTop = 0,
                };
                _iconBox.AddChild(_cdMask);

                // 下: 技能名
                _nameLabel = new Label
                {
                    Name = "NameLabel",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    CustomMinimumSize = new Vector2(0, _bar.NameRowHeight),
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                _nameLabel.AddThemeFontSizeOverride("font_size", 10);
                _nameLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
                _vbox.AddChild(_nameLabel);

                // 选中高亮框 — 覆盖整个 slot
                _highlight = new PanelContainer
                {
                    Name = "Highlight",
                    Visible = false,
                    MouseFilter = MouseFilterEnum.Ignore,
                    AnchorLeft = 0,
                    AnchorTop = 0,
                    AnchorRight = 1,
                    AnchorBottom = 1,
                };
                _highlight.AddThemeStyleboxOverride("panel", UiStyles.CreateHighlightStyle());
                AddChild(_highlight);
            }

            public override void _GuiInput(InputEvent @event)
            {
                if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                {
                    if (SkillId <= 0) return;

                    if (mb.DoubleClick)
                    {
                        _bar.CancelPendingClick();
                        _bar.OnSlotDoubleClicked(_index);
                    }
                    else
                    {
                        _bar.ScheduleClick(_index);
                    }

                    AcceptEvent();
                }
            }

            public void SetSkill(uint skillId)
            {
                SkillId = skillId;
                if (skillId > 0)
                {
                    _nameLabel.Text = SkillDataUtil.GetName(skillId);
                    _nameLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
                    Modulate = Colors.White;
                    _iconView.SetSkill(skillId);
                }
                else
                {
                    _nameLabel.Text = "空";
                    _nameLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.4f, 0.4f));
                    Modulate = new Color(1, 1, 1, 0.5f);
                    _iconView.SetSkill(0);
                }
                ClearCd();
            }

            public void SetCd(float remaining, float total)
            {
                _cdMask.Visible = true;
                float ratio = total > 0 ? remaining / total : 0f;
                _cdMask.AnchorTop = 1f - ratio;
                _cdLabel.Text = remaining >= 1f ? $"{remaining:F0}s" : $"{remaining:F1}s";
            }

            public void ClearCd()
            {
                _cdMask.Visible = false;
                _cdLabel.Text = "";
            }

            public bool IsOnCooldown => _cdMask.Visible;

            public void SetSelected(bool selected)
            {
                _highlight.Visible = selected;
            }

            public void SyncIconSize(int iconSize)
            {
                _iconBox.CustomMinimumSize = new Vector2(iconSize, iconSize);
                if (_iconView != null)
                    _iconView.CustomMinimumSize = new Vector2(Mathf.Max(8, iconSize - 8), Mathf.Max(8, iconSize - 8));
            }

            public void SyncNameFontSize(int fontSize)
            {
                _nameLabel.AddThemeFontSizeOverride("font_size", fontSize);
            }
        }
    }
}
