using Godot;
using System.Collections.Generic;
using System.Linq;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// Skill panel with three columns: equipped, learned, and learnable skills.
    /// The scene provides the shell; this script only handles behavior and refreshes.
    /// </summary>
    public partial class SkillPanel : DraggablePanel
    {
        private NetworkManager _network;

        [Export] public float DefaultWidth { get; set; } = 520;
        [Export] public float DefaultHeight { get; set; } = 380;

        private const int MaxSlots = 4;

        private VBoxContainer _equippedList;
        private VBoxContainer _learnedList;
        private VBoxContainer _learnableList;
        private RichTextLabel _detailLabel;
        private uint? _selectedSkillId;

        protected override void OnPanelInitialized()
        {
            SetToggleKey(Key.F4);
            CustomMinimumSize = new Vector2(DefaultWidth, DefaultHeight);

            DiscoverContentNodes();

            _network = UiServices.GetNetworkManager(this);
            if (_network != null)
            {
                _network.RoleAttrUpdated += OnRoleUpdated;
                _network.ChangeJobResponse += OnChangeJobResponse;
                _network.EquipSkillResponse += OnEquipResponse;
                _network.UnequipSkillResponse += OnUnequipResponse;
                _network.GmResponse += OnGmResponse;
            }

            RefreshUI();
            Visible = false;
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

        protected override void OnClosed()
        {
            Visible = false;
        }

        public new void Toggle()
        {
            base.Toggle();
            if (Visible)
                RefreshUI();
        }

        private void DiscoverContentNodes()
        {
            _equippedList = GetNodeOrNull<VBoxContainer>("VBoxContainer/Content/Columns/EquippedColumn/EquippedScroll/List");
            _learnedList = GetNodeOrNull<VBoxContainer>("VBoxContainer/Content/Columns/LearnedColumn/LearnedScroll/List");
            _learnableList = GetNodeOrNull<VBoxContainer>("VBoxContainer/Content/Columns/LearnableColumn/LearnableScroll/List");
            _detailLabel = GetNodeOrNull<RichTextLabel>("VBoxContainer/Content/DetailLabel");
            UiUtils.ConfigureTransientDragControlFocus(this);
        }

        private void RefreshUI()
        {
            RefreshEquipped();
            RefreshLearned();
            RefreshLearnable();
            RefreshDetail();
        }

        private void RefreshEquipped()
        {
            if (_equippedList == null)
                return;

            _equippedList.ClearChildren();

            if (_network == null)
                return;

            var equipped = _network.CachedEquippedSkills;
            for (int i = 0; i < MaxSlots; i++)
            {
                uint skillId = i < equipped.Count ? equipped[i] : 0;
                if (skillId == 1)
                    skillId = 0;

                var data = skillId > 0
                    ? SkillDataUtil.Get(skillId)
                    : (name: "(Empty)", range: 0, castTime: 0.0, cd: 0.0, mpCost: 0, job: 0);

                var row = new HBoxContainer { Name = $"Slot{i}" };
                row.AddChild(CreateSkillIcon(skillId));

                var nameButton = new Button
                {
                    Text = $"[{i}] {data.name}",
                    CustomMinimumSize = new Vector2(100, 26),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                };

                if (skillId > 0)
                {
                    uint capturedSkillId = skillId;
                    nameButton.Pressed += () => SelectSkill(capturedSkillId);
                }

                row.AddChild(nameButton);

                if (skillId > 0)
                {
                    int slotIndex = i;
                    var unequipButton = new Button
                    {
                        Text = "-",
                        CustomMinimumSize = new Vector2(30, 26),
                    };
                    unequipButton.Pressed += () => SendUnequip(slotIndex);
                    row.AddChild(unequipButton);
                }

                _equippedList.AddChild(row);
            }
        }

        private void RefreshLearned()
        {
            if (_learnedList == null)
                return;

            _learnedList.ClearChildren();

            if (_network == null)
                return;

            var equippedSet = new HashSet<uint>(_network.CachedEquippedSkills);
            var learnedNotEquipped = _network.CachedLearnedSkills
                .Where(id => id > 1 && !equippedSet.Contains(id))
                .ToList();

            foreach (uint skillId in learnedNotEquipped)
            {
                var data = SkillDataUtil.Get(skillId);
                var row = new HBoxContainer();
                row.AddChild(CreateSkillIcon(skillId));

                var nameButton = new Button
                {
                    Text = data.name,
                    CustomMinimumSize = new Vector2(80, 26),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                };
                uint capturedSkillId = skillId;
                nameButton.Pressed += () => SelectSkill(capturedSkillId);
                row.AddChild(nameButton);

                var equipButton = new Button
                {
                    Text = "+",
                    CustomMinimumSize = new Vector2(30, 26),
                };
                equipButton.Pressed += () => SendEquipToFirstEmpty(capturedSkillId);
                row.AddChild(equipButton);

                _learnedList.AddChild(row);
            }

            if (learnedNotEquipped.Count == 0)
            {
                _learnedList.AddChild(new Label
                {
                    Text = "(None)",
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
            }
        }

        private void RefreshLearnable()
        {
            if (_learnableList == null)
                return;

            _learnableList.ClearChildren();

            if (_network == null)
                return;

            var learnedSet = new HashSet<uint>(_network.CachedLearnedSkills);
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
                row.AddChild(CreateSkillIcon(skillId));

                var nameButton = new Button
                {
                    Text = data.name,
                    CustomMinimumSize = new Vector2(80, 26),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                };
                uint capturedSkillId = skillId;
                nameButton.Pressed += () => SelectSkill(capturedSkillId);
                row.AddChild(nameButton);

                var learnButton = new Button
                {
                    Text = "L",
                    CustomMinimumSize = new Vector2(30, 26),
                };
                learnButton.Pressed += () => SendLearnSkill(capturedSkillId);
                row.AddChild(learnButton);

                _learnableList.AddChild(row);
            }

            if (learnable.Count == 0)
            {
                _learnableList.AddChild(new Label
                {
                    Text = "(All learned)",
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
            }
        }

        private void RefreshDetail()
        {
            if (_detailLabel == null)
                return;

            if (!_selectedSkillId.HasValue || _selectedSkillId.Value == 0)
            {
                _detailLabel.Text = "[color=#888888]Select a skill to view details[/color]";
                return;
            }

            var data = SkillDataUtil.Get(_selectedSkillId.Value);
            _detailLabel.Text =
                $"[color=#FFD700]{data.name}[/color]  |  Range:{data.range}  Cast:{data.castTime:F1}s  CD:{data.cd:F1}s  MP:{data.mpCost}";
        }

        private SkillIconView CreateSkillIcon(uint skillId)
        {
            var icon = new SkillIconView
            {
                CustomMinimumSize = new Vector2(24, 24),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            icon.SetSkill(skillId);
            return icon;
        }

        private void SelectSkill(uint skillId)
        {
            _selectedSkillId = skillId;
            RefreshDetail();
        }

        private void SendEquipToFirstEmpty(uint skillId)
        {
            if (_network == null)
                return;

            var equipped = _network.CachedEquippedSkills;
            int slotIndex = -1;
            for (int i = 0; i < MaxSlots; i++)
            {
                uint equippedSkillId = i < equipped.Count ? equipped[i] : 0;
                if (equippedSkillId == 0)
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

            var request = new Game.EquipSkillRequest
            {
                SkillId = skillId,
                SlotIndex = (uint)slotIndex,
            };
            _network.SendPacket(MessageId.GameEquipSkillReq, request);
        }

        private void SendUnequip(int slotIndex)
        {
            if (_network == null)
                return;

            var request = new Game.UnequipSkillRequest
            {
                SlotIndex = (uint)slotIndex,
            };
            _network.SendPacket(MessageId.GameUnequipSkillReq, request);
        }

        private void SendLearnSkill(uint skillId)
        {
            if (_network == null)
                return;

            var request = new Game.GmCommandRequest
            {
                Command = $"learnskill,{skillId}",
            };
            _network.SendPacket(MessageId.GameGmReq, request);
        }

        private void OnRoleUpdated(Game.FullRoleInfo info)
        {
            CallDeferred(nameof(RefreshUI));
        }

        private void OnChangeJobResponse(Game.ChangeJobResponse response)
        {
            if (response.Code == Common.ErrorCode.Success)
                CallDeferred(nameof(RefreshUI));
        }

        private void OnEquipResponse(Game.EquipSkillResponse response)
        {
            if (response.Code != Common.ErrorCode.Success)
                GD.Print($"[SkillPanel] Equip failed: {response.Message}");

            CallDeferred(nameof(RefreshUI));
        }

        private void OnUnequipResponse(Game.UnequipSkillResponse response)
        {
            if (response.Code != Common.ErrorCode.Success)
                GD.Print($"[SkillPanel] Unequip failed: {response.Message}");

            CallDeferred(nameof(RefreshUI));
        }

        private void OnGmResponse(Game.GmCommandResponse response)
        {
            CallDeferred(nameof(RefreshUI));
        }
    }
}
