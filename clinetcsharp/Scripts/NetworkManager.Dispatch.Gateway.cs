using Google.Protobuf;
using Godot;
using Protocol;

namespace ClinetCSharp
{
    public partial class NetworkManager
    {
        private void HandleGatewayHeartbeatResponse(ByteString data)
        {
            var rsp = Gateway.HeartbeatResponse.Parser.ParseFrom(data);
            ServerTime = (uint)rsp.ServerTime;
        }

        private void HandleGatewayKickNotify(ByteString data)
        {
            var notify = Gateway.DisconnectNotify.Parser.ParseFrom(data);
            GD.Print($"[NetworkManager] Kicked by server: reason={notify.Reason}");
            _connected = false;
            Kicked?.Invoke(notify.Reason);
        }

        private void HandleGatewayDisconnectNotify(ByteString data)
        {
            var notify = Gateway.DisconnectNotify.Parser.ParseFrom(data);
            GD.Print($"[NetworkManager] Server disconnect: reason={notify.Reason}");
            _connected = false;
            Disconnected?.Invoke();
        }
    }
}
