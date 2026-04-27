using Godot;
using System.Collections.Generic;
using System.Linq;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 技能管理面板 — 三栏布局：已装备 / 已学习 / 可学习
    /// F4 切换，继承 DraggablePanel
    /// </summary>
    public partial class SkillPanel : DraggablePanel
    {
        private NetworkManager _network;

        [Export] public float DefaultWidth { get; set; } = 520;
        [Export] public float DefaultHeight { get; set; } = 380;

        private const int MaxSlots = 4;

        // UI refs
        private VBoxContainer _equippedList;
        private VBoxContainer _learnedList;
        private VBoxContainer _learnableList;
        private RichTextLabel _detailLabel;
        private uint? _selectedSkillId;

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(DefaultWidth, DefaultHeight);
            Size = new Vector2(DefaultWidth, DefaultHeight);
            BuildSceneTree();
            base._Ready();
        }

        protected override void OnPanelInitialized()
        {
            SetToggleKey(Key.F4);

            _network = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (_network != null)
            {
                _network.RoleAttrUpdated += OnRoleUpdated;
                _network.ChangeJobResponse += OnChangeJobResponse;
                _network.EquipSkillResponse += OnEquipResponse;
                _network.UnequipSkillResponse += OnUnequipResponse;
                _network.GmResponse += OnGmResponse;
            }

            RefreshUI();
        }

        public override void _ExitTree()
        {
            if (_network != null)
            {
                _network.RoleAttrUpdated -= OnRoleUpdated;
                _network.ChangeJobResponse -= OnChangeJobResponse;
                _network.EquipSkillResponse -= OnEquipResponse;
                _network.UnequipSkillResponse -= OnUnequipResponse;
                _network.GmResponse -= OnGmResponse;
            }
            base._ExitTree();
        }

        protected override void OnClosed() => Visible = false;

        public new void Toggle()
        {
            base.Toggle();
            if (Visible) RefreshUI();
        }

        // ============ Scene Tree Construction ============

        private void BuildSceneTree()
        {
            var vbox = new VBoxContainer { Name = "VBoxContainer" };
            AddChild(vbox);

            // TitleBar
            var titleBar = new PanelContainer { Name = "TitleBar", CustomMinimumSize = new Vector2(0, 32) };
            var titleHBox = new HBoxContainer { Name = "HBoxContainer" };
            var minBtn = new Button { Name = "MinimizeButton", Text = "_" };
            var titleLabel = new Label { Name = "Label", Text = "技能面板" };
            var spacer = new Control { Name = "Spacer" };
            spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            var closeBtn = new Button { Name = "CloseButton", Text = "X" };
            titleHBox.AddChild(minBtn);
            titleHBox.AddChild(titleLabel);
            titleHBox.AddChild(spacer);
            titleHBox.AddChild(closeBtn);
            titleBar.AddChild(titleHBox);
            vbox.AddChild(titleBar);

            // Content area
            var content = new VBoxContainer { Name = "Content" };
            content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

            // Three columns
            var columns = new HBoxContainer { Name = "Columns" };
            columns.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            columns.AddThemeConstantOverride("separation", 6);

            // Column 1: Equipped
            _equippedList = BuildColumn(columns, "已装备(4)", "EquippedScroll");

            // Separator
            columns.AddChild(new VSeparator());

            // Column 2: Learned (not equipped)
            _learnedList = BuildColumn(columns, "已学习", "LearnedScroll");

            // Separator
            columns.AddChild(new VSeparator());

            // Column 3: Learnable (all - learned)
            _learnableList = BuildColumn(columns, "可学习", "LearnableScroll");

            content.AddChild(columns);

            // Bottom: detail
            _detailLabel = new RichTextLabel { Name = "DetailLabel", BbcodeEnabled = true, FitContent = true };
            _detailLabel.CustomMinimumSize = new Vector2(0, 50);
            _detailLabel.AddThemeColorOverride("default_color", Colors.White);
            content.AddChild(_detailLabel);

            vbox.AddChild(content);
        }

        private VBoxContainer BuildColumn(HBoxContainer parent, string title, string scrollName)
        {
            var col = new VBoxContainer { Name = title };
            col.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            col.AddChild(new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center });
            var scroll = new ScrollContainer { Name = scrollName, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            scroll.CustomMinimumSize = new Vector2(140, 150);
            var list = new VBoxContainer { Name = "List" };
            scroll.AddChild(list);
            col.AddChild(scroll);
            parent.AddChild(col);
            return list;
        }

        // ============ Refresh ============

        private void RefreshUI()
        {
            RefreshEquipped();
            RefreshLearned();
            RefreshLearnable();
            RefreshDetail();
        }

        private void RefreshEquipped()
        {
            foreach (var child in _equippedList.GetChildren())
                child.QueueFree();

            if (_network == null) return;
            var equipped = _network.CachedEquippedSkills;

            for (int i = 0; i < MaxSlots; i++)
            {
                uint sid = i < equipped.Count ? equipped[i] : 0;
                // 过滤掉普通攻击(id=1)和空位(id=0)
                if (sid == 1) sid = 0;
                var data = sid > 0 ? SkillDataUtil.Get(sid) : (name: "(空)", 0, 0.0, 0.0, 0, 0);

                var row = new HBoxContainer { Name = $"Slot{i}" };

                // Skill name button (click to select)
                var nameBtn = new Button
                {
                    Text = $"[{i}] {data.name}",
                    CustomMinimumSize = new Vector2(100, 26),
                };
                if (sid > 0)
                {
                    uint captured = sid;
                    nameBtn.Pressed += () => SelectSkill(captured);
                }
                row.AddChild(nameBtn);

                if (sid > 0)
                {
                    var unequipBtn = new Button { Text = "卸", CustomMinimumSize = new Vector2(30, 26) };
                    int slot = i;
                    unequipBtn.Pressed += () => SendUnequip(slot);
                    row.AddChild(unequipBtn);
                }

                _equippedList.AddChild(row);
            }
        }

        private void RefreshLearned()
        {
            foreach (var child in _learnedList.GetChildren())
                child.QueueFree();

            if (_network == null) return;

            var equippedSet = new HashSet<uint>(_network.CachedEquippedSkills);
            // Learned but not equipped
            var learnedNotEquipped = _network.CachedLearnedSkills
                .Where(id => id > 1 && !equippedSet.Contains(id))
                .ToList();

            foreach (uint skillId in learnedNotEquipped)
            {
                var data = SkillDataUtil.Get(skillId);
                var row = new HBoxContainer();

                var nameBtn = new Button
                {
                    Text = data.name,
                    CustomMinimumSize = new Vector2(80, 26),
                };
                uint captured = skillId;
                nameBtn.Pressed += () => SelectSkill(captured);
                row.AddChild(nameBtn);

                var equipBtn = new Button { Text = "装", CustomMinimumSize = new Vector2(30, 26) };
                equipBtn.Pressed += () => SendEquipToFirstEmpty(captured);
                row.AddChild(equipBtn);

                _learnedList.AddChild(row);
            }

            if (learnedNotEquipped.Count == 0)
                _learnedList.AddChild(new Label { Text = "（无）", HorizontalAlignment = HorizontalAlignment.Center });
        }

        private void RefreshLearnable()
        {
            foreach (var child in _learnableList.GetChildren())
                child.QueueFree();

            if (_network == null) return;

            var learnedSet = new HashSet<uint>(_network.CachedLearnedSkills);
            // 根据当前职业过滤可学习技能
            var currentJob = _network.CachedRoleInfo?.Job ?? "";
            int jobId = SkillDataUtil.JobNameToId(currentJob);
            var learnable = jobId > 0
                ? SkillDataUtil.GetLearnableIdsForJob(jobId)
                : SkillDataUtil.GetAllLearnableIds();
            learnable = learnable.Where(id => !learnedSet.Contains(id)).ToList();

            foreach (uint skillId in learnable)
            {
                var data = SkillDataUtil.Get(skillId);
                var row = new HBoxContainer();

                var nameBtn = new Button
                {
                    Text = data.name,
                    CustomMinimumSize = new Vector2(80, 26),
                };
                uint captured = skillId;
                nameBtn.Pressed += () => SelectSkill(captured);
                row.AddChild(nameBtn);

                var learnBtn = new Button { Text = "学", CustomMinimumSize = new Vector2(30, 26) };
                learnBtn.Pressed += () => SendLearnSkill(captured);
                row.AddChild(learnBtn);

                _learnableList.AddChild(row);
            }

            if (learnable.Count == 0)
                _learnableList.AddChild(new Label { Text = "（已全学）", HorizontalAlignment = HorizontalAlignment.Center });
        }

        private void RefreshDetail()
        {
            if (_detailLabel == null) return;
            if (!_selectedSkillId.HasValue || _selectedSkillId.Value == 0)
            {
                _detailLabel.Text = "[color=#888888]点击技能查看详情[/color]";
                return;
            }
            var d = SkillDataUtil.Get(_selectedSkillId.Value);
            _detailLabel.Text = $"[color=#FFD700]{d.name}[/color]  |  " +
                                $"范围:{d.range}  读条:{d.castTime:F1}s  CD:{d.cd:F1}s  MP:{d.mpCost}";
        }

        // ============ Actions ============

        private void SelectSkill(uint skillId)
        {
            _selectedSkillId = skillId;
            RefreshDetail();
        }

        private void SendEquipToFirstEmpty(uint skillId)
        {
            if (_network == null) return;
            var equipped = _network.CachedEquippedSkills;

            // Find first empty slot
            int slotIndex = -1;
            for (int i = 0; i < MaxSlots; i++)
            {
                uint sid = i < equipped.Count ? equipped[i] : 0;
                if (sid == 0)
                {
                    slotIndex = i;
                    break;
                }
            }

            if (slotIndex < 0)
            {
                GD.Print("[SkillPanel] No empty slot available");
                return;
            }

            var req = new Game.EquipSkillRequest { SkillId = skillId, SlotIndex = (uint)slotIndex };
            _network.SendPacket(Protocol.MessageId.GameEquipSkillReq, req);
        }

        private void SendUnequip(int slotIndex)
        {
            if (_network == null) return;
            var req = new Game.UnequipSkillRequest { SlotIndex = (uint)slotIndex };
            _network.SendPacket(Protocol.MessageId.GameUnequipSkillReq, req);
        }

        private void SendLearnSkill(uint skillId)
        {
            if (_network == null) return;
            // Use GM command to learn skill
            var req = new Game.GmCommandRequest { Command = $"learnskill,{skillId}" };
            _network.SendPacket(MessageId.GameGmReq, req);
        }

        // ============ Callbacks ============

        private void OnRoleUpdated(Game.FullRoleInfo info)
        {
            CallDeferred(nameof(RefreshUI));
        }

        private void OnChangeJobResponse(Game.ChangeJobResponse rsp)
        {
            if (rsp.Code == Common.ErrorCode.Success)
                CallDeferred(nameof(RefreshUI));
        }

        private void OnEquipResponse(Game.EquipSkillResponse rsp)
        {
            if (rsp.Code != Common.ErrorCode.Success)
                GD.Print($"[SkillPanel] Equip failed: {rsp.Message}");
            CallDeferred(nameof(RefreshUI));
        }

        private void OnUnequipResponse(Game.UnequipSkillResponse rsp)
        {
            if (rsp.Code != Common.ErrorCode.Success)
                GD.Print($"[SkillPanel] Unequip failed: {rsp.Message}");
            CallDeferred(nameof(RefreshUI));
        }

        private void OnGmResponse(Game.GmCommandResponse rsp)
        {
            CallDeferred(nameof(RefreshUI));
        }


    }
}
