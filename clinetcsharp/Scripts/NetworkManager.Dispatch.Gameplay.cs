using System.Collections.Generic;
using Google.Protobuf;
using Godot;
using Protocol;

namespace ClinetCSharp
{
    public partial class NetworkManager
    {
        private void HandleRoleAttrNotify(ByteString data)
        {
            var roleInfo = Game.FullRoleInfo.Parser.ParseFrom(data);
            CachedRoleInfo = roleInfo;
            CachedLearnedSkills = new List<uint>(roleInfo.LearnedSkills);
            CachedEquippedSkills = new List<uint>(roleInfo.EquippedSkills);
            RoleAttrUpdated?.Invoke(roleInfo);
        }

        private void HandleOpenChestResponse(ByteString data)
        {
            OpenChestResponse?.Invoke(Game.OpenChestResponse.Parser.ParseFrom(data));
        }

        private void HandleUseItemResponse(ByteString data)
        {
            UseItemResponse?.Invoke(Game.UseItemResponse.Parser.ParseFrom(data));
        }

        private void HandleDropItemResponse(ByteString data)
        {
            DropItemResponse?.Invoke(Game.DropItemResponse.Parser.ParseFrom(data));
        }

        private void HandleGmResponse(ByteString data)
        {
            var rsp = Game.GmCommandResponse.Parser.ParseFrom(data);
            if (rsp.LearnedSkills.Count > 0)
                CachedLearnedSkills = new List<uint>(rsp.LearnedSkills);
            if (rsp.EquippedSkills.Count > 0)
                CachedEquippedSkills = new List<uint>(rsp.EquippedSkills);

            GmResponse?.Invoke(rsp);
        }

        private void HandleEquipSkillResponse(ByteString data)
        {
            EquipSkillResponse?.Invoke(Game.EquipSkillResponse.Parser.ParseFrom(data));
        }

        private void HandleUnequipSkillResponse(ByteString data)
        {
            UnequipSkillResponse?.Invoke(Game.UnequipSkillResponse.Parser.ParseFrom(data));
        }

        private void HandleSetPreferredSkillResponse(ByteString data)
        {
            var rsp = Game.SetPreferredSkillResponse.Parser.ParseFrom(data);
            GD.Print($"[Network] SetPreferredSkill: skill={rsp.PreferredSkillId} code={rsp.Code}");
        }

        private void HandleChangeJobResponse(ByteString data)
        {
            var rsp = Game.ChangeJobResponse.Parser.ParseFrom(data);
            if (rsp.Code == Common.ErrorCode.Success)
            {
                CachedLearnedSkills = new List<uint>(rsp.LearnedSkills);
                CachedEquippedSkills = new List<uint>(rsp.EquippedSkills);
            }

            ChangeJobResponse?.Invoke(rsp);
        }

        private void HandleMonsterDeathNotify(ByteString data)
        {
            MonsterDeathNotify?.Invoke(Game.MonsterDeathNotify.Parser.ParseFrom(data));
        }

        private void HandleMonsterRespawnNotify(ByteString data)
        {
            MonsterRespawnNotify?.Invoke(Game.MonsterRespawnNotify.Parser.ParseFrom(data));
        }
    }
}
