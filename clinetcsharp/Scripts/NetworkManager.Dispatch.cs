using System;
using Google.Protobuf;
using Godot;
using Protocol;

namespace ClinetCSharp
{
    public partial class NetworkManager
    {
        private void DispatchMessage(int msgId, ByteString data)
        {
            try
            {
                switch ((MessageId)msgId)
                {
                    case MessageId.GatewayHeartbeatRsp:
                        HandleGatewayHeartbeatResponse(data);
                        break;
                    case MessageId.GatewayKickNotify:
                        HandleGatewayKickNotify(data);
                        break;
                    case MessageId.GatewayDisconnectNotify:
                        HandleGatewayDisconnectNotify(data);
                        break;
                    case MessageId.LoginAccountLoginRsp:
                        HandleAccountLoginResponse(data);
                        break;
                    case MessageId.LoginSelectServerRsp:
                        HandleSelectServerResponse(data);
                        break;
                    case MessageId.GameEnterGameRsp:
                        HandleEnterGameResponse(data);
                        break;
                    case MessageId.GameCreateRoleRsp:
                        HandleCreateRoleResponse(data);
                        break;
                    case MessageId.GameMapInfoSyncNotify:
                        HandleMapInfoSyncNotify(data);
                        break;
                    case MessageId.GameChangeMapRsp:
                        HandleChangeMapResponse(data);
                        break;
                    case MessageId.GameChestUpdateNotify:
                        HandleChestUpdateNotify(data);
                        break;
                    case MessageId.GameDropSpawnNotify:
                        HandleDropSpawnNotify(data);
                        break;
                    case MessageId.GameDropPickupNotify:
                        HandleDropPickupNotify(data);
                        break;
                    case MessageId.GameDropRemoveNotify:
                        HandleDropRemoveNotify(data);
                        break;
                    case MessageId.GameNpcInteractNotify:
                        HandleNpcInteractNotify(data);
                        break;
                    case MessageId.GameNpcCombatRsp:
                        HandleNpcCombatResponse(data);
                        break;
                    case MessageId.GameMoveRsp:
                        HandleMoveResponse(data);
                        break;
                    case MessageId.GameMoveCancelNotify:
                        HandleMoveCancelNotify(data);
                        break;
                    case MessageId.GameMonsterMoveNotify:
                        HandleMonsterMoveNotify(data);
                        break;
                    case MessageId.GameMonsterMoveCancelNotify:
                        HandleMonsterMoveCancelNotify(data);
                        break;
                    case MessageId.GameMonsterDeathNotify:
                        HandleMonsterDeathNotify(data);
                        break;
                    case MessageId.GameMonsterRespawnNotify:
                        HandleMonsterRespawnNotify(data);
                        break;
                    case MessageId.GameCombatLogNotify:
                        HandleCombatLogNotify(data);
                        break;
                    case MessageId.GameCombatStateNotify:
                        HandleCombatStateNotify(data);
                        break;
                    case MessageId.GameBuffUpdateNotify:
                        HandleBuffUpdateNotify(data);
                        break;
                    case MessageId.GameCombatStartNotify:
                        HandleCombatStartNotify(data);
                        break;
                    case MessageId.GameCombatEndNotify:
                        HandleCombatEndNotify(data);
                        break;
                    case MessageId.GameCastStartNotify:
                        HandleCastStartNotify(data);
                        break;
                    case MessageId.GameCastResultNotify:
                        HandleCastResultNotify(data);
                        break;
                    case MessageId.GameCombatEventNotify:
                        HandleCombatEventNotify(data);
                        break;
                    case MessageId.GameProjectileSpawnNotify:
                        HandleProjectileSpawnNotify(data);
                        break;
                    case MessageId.GameProjectileHitNotify:
                        HandleProjectileHitNotify(data);
                        break;
                    case MessageId.GameDisengageNotify:
                        HandleDisengageNotify(data);
                        break;
                    case MessageId.GamePlayerDeathNotify:
                        HandlePlayerDeathNotify(data);
                        break;
                    case MessageId.GameLevelUpNotify:
                        HandleLevelUpNotify(data);
                        break;
                    case MessageId.GameRoleAttrNotify:
                        HandleRoleAttrNotify(data);
                        break;
                    case MessageId.GameOpenChestRsp:
                        HandleOpenChestResponse(data);
                        break;
                    case MessageId.GameUseItemRsp:
                        HandleUseItemResponse(data);
                        break;
                    case MessageId.GameDropItemRsp:
                        HandleDropItemResponse(data);
                        break;
                    case MessageId.GameGmRsp:
                        HandleGmResponse(data);
                        break;
                    case MessageId.GameEquipSkillRsp:
                        HandleEquipSkillResponse(data);
                        break;
                    case MessageId.GameUnequipSkillRsp:
                        HandleUnequipSkillResponse(data);
                        break;
                    case MessageId.GameSetPreferredSkillRsp:
                        HandleSetPreferredSkillResponse(data);
                        break;
                    case MessageId.GameCastRsp:
                        HandleCastResponse(data);
                        break;
                    case MessageId.GameChangeJobRsp:
                        HandleChangeJobResponse(data);
                        break;
                    default:
                        GD.Print($"[NetworkManager] Unhandled msgId={msgId}");
                        break;
                }
            }
            catch (Exception e)
            {
                GD.PushError($"[NetworkManager] DispatchMessage failed for msgId={msgId}: {e.Message}");
            }
        }
    }
}
