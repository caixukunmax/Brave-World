using System.Collections.Generic;
using Google.Protobuf;
using Protocol;

namespace ClinetCSharp
{
    public partial class NetworkManager
    {
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
    }
}
