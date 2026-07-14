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
            var rsp = Game.UseItemResponse.Parser.ParseFrom(data);
            if (rsp.Code == Common.ErrorCode.Success)
                CachedItems = new List<Game.ItemInfo>(rsp.Items);
            UseItemResponse?.Invoke(rsp);
        }

        private void HandleDropItemResponse(ByteString data)
        {
            var rsp = Game.DropItemResponse.Parser.ParseFrom(data);
            if (rsp.Code == Common.ErrorCode.Success)
                CachedItems = new List<Game.ItemInfo>(rsp.Items);
            DropItemResponse?.Invoke(rsp);
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
            var rsp = Game.EquipSkillResponse.Parser.ParseFrom(data);
            if (rsp.Code == Common.ErrorCode.Success)
                CachedEquippedSkills = new List<uint>(rsp.EquippedSkills);
            EquipSkillResponse?.Invoke(rsp);
        }

        private void HandleUnequipSkillResponse(ByteString data)
        {
            var rsp = Game.UnequipSkillResponse.Parser.ParseFrom(data);
            if (rsp.Code == Common.ErrorCode.Success)
                CachedEquippedSkills = new List<uint>(rsp.EquippedSkills);
            UnequipSkillResponse?.Invoke(rsp);
        }

        private void HandleSetPreferredSkillResponse(ByteString data)
        {
            var rsp = Game.SetPreferredSkillResponse.Parser.ParseFrom(data);
            GD.Print($"[Network] SetPreferredSkill: skill={rsp.PreferredSkillId} code={rsp.Code}");
            SetPreferredSkillResponse?.Invoke(rsp);
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

        private void HandleDirectionNotify(ByteString data)
        {
            DirectionNotify?.Invoke(Game.DirectionNotify.Parser.ParseFrom(data));
        }
    }
}
