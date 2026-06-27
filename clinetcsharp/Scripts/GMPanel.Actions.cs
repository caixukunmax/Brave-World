using Godot;
using Protocol;

namespace ClinetCSharp
{
    public partial class GMPanel
    {
        private void OnExecPressed()
        {
            string commandLine = _cmdEdit.Text.StripEdges();
            if (commandLine == "")
                return;

            ExecuteGmCommand(commandLine);
            _cmdEdit.Text = "";
        }

        private void ExecuteGmCommand(string commandLine)
        {
            if (commandLine == "")
                return;

            if (_network == null || !_network.IsServerConnected())
            {
                AppendLog("[color=red]未连接服务器[/color]");
                return;
            }

            var request = new Game.GmCommandRequest
            {
                Command = commandLine,
                Args = "",
            };

            _network.SendPacket(MessageId.GameGmReq, request);
            AppendLog($"[color=cyan]> {commandLine}[/color]");
        }

        private void OnGmResponse(Game.GmCommandResponse response)
        {
            string color = response.Code == Common.ErrorCode.Success ? "green" : "red";
            AppendLog($"[color={color}]{response.Message}[/color]");

            if (response.Code != Common.ErrorCode.Success)
                return;

            HandleTeleportResponse(response.Message);
            HandleInventoryResponse(response);
        }

        private void HandleTeleportResponse(string message)
        {
            if (!message.StartsWith("TELEPORT:"))
                return;

            var parts = message.Split(':');
            if (parts.Length != 3 ||
                !int.TryParse(parts[1], out int targetX) ||
                !int.TryParse(parts[2], out int targetY))
            {
                return;
            }

            var player = GetTree()?.GetFirstNodeInGroup("player") as Player;
            if (player == null)
                return;

            player.TeleportToGrid(targetX, targetY);
            AppendLog($"[color=cyan]已瞬移到 ({targetX}, {targetY})[/color]");
        }

        private void HandleInventoryResponse(Game.GmCommandResponse response)
        {
            if (response.Items.Count == 0)
                return;

            var inventoryManager = UiServices.GetInventoryManager(this);
            inventoryManager?.UpdateFromProto(response.Items);
        }

        private void AppendLog(string bbcode)
        {
            GD.Print($"[GM] {bbcode}");
        }
    }
}
