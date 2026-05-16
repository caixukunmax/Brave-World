using System.Collections.Generic;
using Google.Protobuf.Collections;
using Godot;
using Protocol;

namespace ClinetCSharp
{
    public partial class NetworkManager
    {
        private void CacheRoleAndMapData(
            Game.FullRoleInfo roleInfo,
            RepeatedField<Game.ItemInfo> items,
            RepeatedField<Game.ChestInfo> chests,
            uint serverTime)
        {
            CachedRoleInfo = roleInfo;
            ServerTime = serverTime;
            CurrentMapName = roleInfo.CurrentMap;
            SpawnGridX = roleInfo.GridX;
            SpawnGridY = roleInfo.GridY;
            CachedItems = new List<Game.ItemInfo>(items);
            Chests = new List<Game.ChestInfo>(chests);
            CachedLearnedSkills = new List<uint>(roleInfo.LearnedSkills);
            CachedEquippedSkills = new List<uint>(roleInfo.EquippedSkills);
            GD.Print($"[NetworkManager] Cached role={roleInfo.RoleName} map={roleInfo.CurrentMap} pos=({roleInfo.GridX},{roleInfo.GridY})");
        }
    }
}
