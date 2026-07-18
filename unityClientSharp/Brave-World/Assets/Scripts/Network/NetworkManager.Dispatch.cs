using System.Collections.Generic;
using Google.Protobuf;
using Protocol;
using UnityEngine;

namespace UnityClientSharp.Net
{
    /// <summary>
    /// 消息分发 — 手写 switch 全量移植（对齐 Godot NetworkManager.Dispatch*.cs，含缓存写回副作用）。
    /// </summary>
    public partial class NetworkManager
    {
        private void DispatchMessage(int msgId, ByteString data, uint session)
        {
            try
            {
                switch ((MessageId)msgId)
                {
                    case MessageId.GameGetServerLogPathRsp: HandleServerLogPathResponse(data, session); break;
                    case MessageId.GatewayHeartbeatRsp: HandleGatewayHeartbeatResponse(data); break;
                    case MessageId.GatewayKickNotify: HandleGatewayKickNotify(data); break;
                    case MessageId.GatewayDisconnectNotify: HandleGatewayDisconnectNotify(data); break;
                    case MessageId.LoginAccountLoginRsp: HandleAccountLoginResponse(data); break;
                    case MessageId.LoginSelectServerRsp: HandleSelectServerResponse(data); break;
                    case MessageId.GameEnterGameRsp: HandleEnterGameResponse(data); break;
                    case MessageId.GameCreateRoleRsp: HandleCreateRoleResponse(data); break;
                    case MessageId.GameMapInfoSyncNotify: HandleMapInfoSyncNotify(data); break;
                    case MessageId.GameChangeMapRsp: HandleChangeMapResponse(data); break;
                    case MessageId.GameChestUpdateNotify: HandleChestUpdateNotify(data); break;
                    case MessageId.GameDropSpawnNotify: HandleDropSpawnNotify(data); break;
                    case MessageId.GameDropPickupNotify: HandleDropPickupNotify(data); break;
                    case MessageId.GameDropRemoveNotify: HandleDropRemoveNotify(data); break;
                    case MessageId.GameNpcInteractNotify: HandleNpcInteractNotify(data); break;
                    case MessageId.GameNpcCombatRsp: HandleNpcCombatResponse(data); break;
                    case MessageId.GameMoveRsp: HandleMoveResponse(data); break;
                    case MessageId.GameMoveCancelNotify: HandleMoveCancelNotify(data); break;
                    case MessageId.GameMonsterMoveNotify: HandleMonsterMoveNotify(data); break;
                    case MessageId.GameMonsterMoveCancelNotify: HandleMonsterMoveCancelNotify(data); break;
                    case MessageId.GameMonsterDeathNotify: HandleMonsterDeathNotify(data); break;
                    case MessageId.GameMonsterRespawnNotify: HandleMonsterRespawnNotify(data); break;
                    case MessageId.GameDirectionNotify: HandleDirectionNotify(data); break;
                    case MessageId.GameCombatLogNotify: HandleCombatLogNotify(data); break;
                    case MessageId.GameCombatStateNotify: HandleCombatStateNotify(data); break;
                    case MessageId.GameBuffUpdateNotify: HandleBuffUpdateNotify(data); break;
                    case MessageId.GameCombatStartNotify: HandleCombatStartNotify(data); break;
                    case MessageId.GameCombatEndNotify: HandleCombatEndNotify(data); break;
                    case MessageId.GameCastStartNotify: HandleCastStartNotify(data); break;
                    case MessageId.GameCastResultNotify: HandleCastResultNotify(data); break;
                    case MessageId.GameCombatEventNotify: HandleCombatEventNotify(data); break;
                    case MessageId.GameProjectileSpawnNotify: HandleProjectileSpawnNotify(data); break;
                    case MessageId.GameProjectileHitNotify: HandleProjectileHitNotify(data); break;
                    case MessageId.GameDisengageNotify: HandleDisengageNotify(data); break;
                    case MessageId.GamePlayerDeathNotify: HandlePlayerDeathNotify(data); break;
                    case MessageId.GameLevelUpNotify: HandleLevelUpNotify(data); break;
                    case MessageId.GameRoleAttrNotify: HandleRoleAttrNotify(data); break;
                    case MessageId.GameOpenChestRsp: HandleOpenChestResponse(data); break;
                    case MessageId.GameUseItemRsp: HandleUseItemResponse(data); break;
                    case MessageId.GameDropItemRsp: HandleDropItemResponse(data); break;
                    case MessageId.GameInventoryReorderRsp: HandleInventoryReorderResponse(data); break;
                    case MessageId.GameGmRsp: HandleGmResponse(data); break;
                    case MessageId.GameEquipSkillRsp: HandleEquipSkillResponse(data); break;
                    case MessageId.GameUnequipSkillRsp: HandleUnequipSkillResponse(data); break;
                    case MessageId.GameSetPreferredSkillRsp: HandleSetPreferredSkillResponse(data); break;
                    case MessageId.GameCastRsp: HandleCastResponse(data); break;
                    case MessageId.GameChangeJobRsp: HandleChangeJobResponse(data); break;
                    default:
                        Debug.Log($"[NetworkManager] Unhandled msgId={msgId}");
                        break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkManager] DispatchMessage failed for msgId={msgId}: {e.Message}");
            }
        }

        // ========== Gateway ==========

        private void HandleGatewayHeartbeatResponse(ByteString data)
        {
            var rsp = Gateway.HeartbeatResponse.Parser.ParseFrom(data);
            ServerTime = (uint)rsp.ServerTime;
        }

        private void HandleGatewayKickNotify(ByteString data)
        {
            var notify = Gateway.DisconnectNotify.Parser.ParseFrom(data);
            Debug.Log($"[NetworkManager] Kicked by server: reason={notify.Reason}");
            _connected = false;
            Kicked?.Invoke(notify.Reason);
        }

        private void HandleGatewayDisconnectNotify(ByteString data)
        {
            var notify = Gateway.DisconnectNotify.Parser.ParseFrom(data);
            Debug.Log($"[NetworkManager] Server disconnect: reason={notify.Reason}");
            _connected = false;
            Disconnected?.Invoke();
        }

        // ========== Login ==========

        private void HandleAccountLoginResponse(ByteString data)
        {
            var rsp = Login.AccountLoginResponse.Parser.ParseFrom(data);
            if (rsp.Code == Common.ErrorCode.Success)
            {
                AccountToken = rsp.AccountToken;
                AccountId = rsp.AccountId;
                LastServerId = rsp.LastServerId;
                LastRoleName = rsp.LastRoleName;
                Servers = new List<Server.ServerInfo>(rsp.Servers);
            }

            LoginResponse?.Invoke(rsp);
        }

        private void HandleSelectServerResponse(ByteString data)
        {
            var rsp = Login.SelectServerResponse.Parser.ParseFrom(data);
            if (rsp.Code == Common.ErrorCode.Success)
            {
                GatewayToken = rsp.GatewayToken;
                MaxRoleCount = rsp.MaxRoleCount;
                ServerTime = (uint)rsp.ServerTime;
                Roles = new List<Login.RoleBrief>(rsp.Roles);
            }

            SelectServerResponse?.Invoke(rsp);
        }

        private void HandleEnterGameResponse(ByteString data)
        {
            var rsp = Game.EnterGameResponse.Parser.ParseFrom(data);
            if (rsp.Code == Common.ErrorCode.Success && rsp.RoleInfo != null)
                CacheRoleAndMapData(rsp.RoleInfo, rsp.Items, rsp.Chests, rsp.ServerTime);

            EnterGameResponse?.Invoke(rsp);
        }

        private void HandleCreateRoleResponse(ByteString data)
        {
            var rsp = Game.CreateRoleResponse.Parser.ParseFrom(data);
            if (rsp.Code == Common.ErrorCode.Success && rsp.RoleInfo != null)
                CacheRoleAndMapData(rsp.RoleInfo, rsp.Items, rsp.Chests, rsp.ServerTime);

            CreateRoleResponse?.Invoke(rsp);
        }

        private void CacheRoleAndMapData(
            Game.FullRoleInfo roleInfo,
            Google.Protobuf.Collections.RepeatedField<Game.ItemInfo> items,
            Google.Protobuf.Collections.RepeatedField<Game.ChestInfo> chests,
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
            Debug.Log($"[NetworkManager] Cached role={roleInfo.RoleName} map={roleInfo.CurrentMap} pos=({roleInfo.GridX},{roleInfo.GridY})");
        }

        // ========== Map ==========

        private void HandleMapInfoSyncNotify(ByteString data)
        {
            var notify = Game.MapInfoSyncNotify.Parser.ParseFrom(data);
            CurrentMapName = notify.MapName;
            Chests = new List<Game.ChestInfo>(notify.Chests);
            Monsters = new List<Game.MonsterInfo>(notify.Monsters);
            Npcs = new List<Game.NpcInfo>(notify.Npcs);
            Drops = new List<Game.DropItemInfo>(notify.Drops);
            Tiles = new List<Game.TileInfo>(notify.Tiles);
            Debug.Log($"[NetworkManager] MapInfoSync map={notify.MapName} chests={Chests.Count} monsters={Monsters.Count} npcs={Npcs.Count} drops={Drops.Count} tiles={Tiles.Count}");
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

        // ========== Movement / Combat ==========

        private void HandleMoveResponse(ByteString data)
        {
            MoveResponse?.Invoke(Game.MoveResponse.Parser.ParseFrom(data));
        }

        private void HandleMoveCancelNotify(ByteString data)
        {
            MoveCancelNotify?.Invoke(Game.MoveCancelNotify.Parser.ParseFrom(data));
        }

        private void HandleMonsterMoveNotify(ByteString data)
        {
            MonsterMoveNotify?.Invoke(Game.MonsterMoveNotify.Parser.ParseFrom(data));
        }

        private void HandleMonsterMoveCancelNotify(ByteString data)
        {
            MonsterMoveCancelNotify?.Invoke(Game.MonsterMoveCancelNotify.Parser.ParseFrom(data));
        }

        private void HandleCombatLogNotify(ByteString data)
        {
            var notify = Game.CombatLogNotify.Parser.ParseFrom(data);
            CombatLogNotify?.Invoke(notify);
            // TODO(在线实体阶段): 攻击者抖动效果（Godot 端 FindEntityByName + PlayAttackShake）
        }

        private void HandleCombatStateNotify(ByteString data)
        {
            CombatStateNotify?.Invoke(Game.CombatStateNotify.Parser.ParseFrom(data));
        }

        private void HandleBuffUpdateNotify(ByteString data)
        {
            BuffUpdateNotify?.Invoke(Game.BuffUpdateNotify.Parser.ParseFrom(data));
        }

        private void HandleCombatStartNotify(ByteString data)
        {
            CombatStartNotify?.Invoke(Game.CombatStartNotify.Parser.ParseFrom(data));
        }

        private void HandleCombatEndNotify(ByteString data)
        {
            CombatEndNotify?.Invoke(Game.CombatEndNotify.Parser.ParseFrom(data));
        }

        private void HandlePlayerDeathNotify(ByteString data)
        {
            PlayerDeathNotify?.Invoke(Game.PlayerDeathNotify.Parser.ParseFrom(data));
        }

        private void HandleLevelUpNotify(ByteString data)
        {
            LevelUpNotify?.Invoke(Game.LevelUpNotify.Parser.ParseFrom(data));
        }

        private void HandleCastResponse(ByteString data)
        {
            var rsp = Game.CastResponse.Parser.ParseFrom(data);
            CastResponse?.Invoke(rsp);
            if (!rsp.Success)
                Debug.LogError($"[Cast] Failed: {rsp.Error}");
        }

        private void HandleCastStartNotify(ByteString data)
        {
            CastStartNotify?.Invoke(Game.CastStartNotify.Parser.ParseFrom(data));
        }

        private void HandleCastResultNotify(ByteString data)
        {
            CastResultNotify?.Invoke(Game.CastResultNotify.Parser.ParseFrom(data));
        }

        private void HandleCombatEventNotify(ByteString data)
        {
            CombatEventNotify?.Invoke(Game.CombatEventNotify.Parser.ParseFrom(data));
        }

        private void HandleProjectileSpawnNotify(ByteString data)
        {
            var notify = Game.ProjectileSpawnNotify.Parser.ParseFrom(data);
            ProjectileSpawnNotify?.Invoke(notify);
        }

        private void HandleProjectileHitNotify(ByteString data)
        {
            var notify = Game.ProjectileHitNotify.Parser.ParseFrom(data);
            ProjectileHitNotify?.Invoke(notify);
        }

        private void HandleDisengageNotify(ByteString data)
        {
            DisengageNotify?.Invoke(Game.DisengageNotify.Parser.ParseFrom(data));
        }

        // ========== Gameplay ==========

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

        private void HandleInventoryReorderResponse(ByteString data)
        {
            var rsp = Game.InventoryReorderResponse.Parser.ParseFrom(data);
            InventoryReorderResponse?.Invoke(rsp);
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
            Debug.Log($"[Network] SetPreferredSkill: skill={rsp.PreferredSkillId} code={rsp.Code}");
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

        // ========== Session 回调 ==========

        private void HandleServerLogPathResponse(ByteString data, uint session)
        {
            var rsp = Game.GetServerLogPathResponse.Parser.ParseFrom(data);
            if (_pendingLogPathCallbacks.TryGetValue(session, out var callback))
            {
                _pendingLogPathCallbacks.Remove(session);
                callback?.Invoke(rsp.LogPath ?? "");
            }
        }
    }
}
