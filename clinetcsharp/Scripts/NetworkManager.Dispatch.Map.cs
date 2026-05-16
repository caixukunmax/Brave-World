using System.Collections.Generic;
using Google.Protobuf;
using Godot;
using Protocol;

namespace ClinetCSharp
{
    public partial class NetworkManager
    {
        private void HandleMapInfoSyncNotify(ByteString data)
        {
            var notify = Game.MapInfoSyncNotify.Parser.ParseFrom(data);
            CurrentMapName = notify.MapName;
            Chests = new List<Game.ChestInfo>(notify.Chests);
            Monsters = new List<Game.MonsterInfo>(notify.Monsters);
            Npcs = new List<Game.NpcInfo>(notify.Npcs);
            Drops = new List<Game.DropItemInfo>(notify.Drops);
            Tiles = new List<Game.TileInfo>(notify.Tiles);
            GD.Print($"[NetworkManager] MapInfoSync map={notify.MapName} chests={Chests.Count} monsters={Monsters.Count} npcs={Npcs.Count} drops={Drops.Count} tiles={Tiles.Count}");
            MapInfoReceived?.Invoke(notify);
        }

        private void HandleChangeMapResponse(ByteString data)
        {
            var rsp = Game.ChangeMapResponse.Parser.ParseFrom(data);
            if (rsp.Code == Common.ErrorCode.Success)
            {
                CurrentMapName = rsp.MapName;
                SpawnGridX = (int)rsp.SpawnX;
                SpawnGridY = (int)rsp.SpawnY;
            }

            ChangeMapResponse?.Invoke(rsp);
        }

        private void HandleChestUpdateNotify(ByteString data)
        {
            var notify = Game.ChestUpdateNotify.Parser.ParseFrom(data);
            Chests = new List<Game.ChestInfo>(Chests);
            Chests.AddRange(notify.Chests);
            ChestUpdateNotify?.Invoke(notify);
        }

        private void HandleDropSpawnNotify(ByteString data)
        {
            DropSpawnNotify?.Invoke(Game.DropSpawnNotify.Parser.ParseFrom(data));
        }

        private void HandleDropPickupNotify(ByteString data)
        {
            DropPickupNotify?.Invoke(Game.DropPickupNotify.Parser.ParseFrom(data));
        }

        private void HandleDropRemoveNotify(ByteString data)
        {
            DropRemoveNotify?.Invoke(Game.DropRemoveNotify.Parser.ParseFrom(data));
        }

        private void HandleNpcInteractNotify(ByteString data)
        {
            NpcInteractNotify?.Invoke(Game.NpcInteractNotify.Parser.ParseFrom(data));
        }

        private void HandleNpcCombatResponse(ByteString data)
        {
            NpcCombatResponse?.Invoke(Game.NpcCombatResponse.Parser.ParseFrom(data));
        }
    }
}
