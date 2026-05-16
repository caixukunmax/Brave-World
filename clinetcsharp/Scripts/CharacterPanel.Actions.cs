using Protocol;

namespace ClinetCSharp
{
    public partial class CharacterPanel
    {
        private void RefreshFromPlayer()
        {
            var player = GetTree()?.GetFirstNodeInGroup("player") as Player;
            if (player == null || player.CombatAttrs.Count == 0)
                return;

            foreach (var definition in AttrDefs)
            {
                if (_spinBoxes.TryGetValue(definition.Key, out var spinBox) &&
                    player.CombatAttrs.TryGetValue(definition.Key, out var value))
                {
                    spinBox.Value = value;
                }
            }
        }

        private void OnApplyAttr(string gmName, int value)
        {
            if (_network == null)
                return;

            var request = new Game.GmCommandRequest
            {
                Command = $"setattr,{gmName},{value}",
            };
            _network.SendPacket(MessageId.GameGmReq, request);
        }

        private void OnApplyAll()
        {
            if (_network == null)
                return;

            foreach (var definition in AttrDefs)
            {
                if (!_spinBoxes.TryGetValue(definition.Key, out var spinBox))
                    continue;

                var request = new Game.GmCommandRequest
                {
                    Command = $"setattr,{definition.GmName},{(int)spinBox.Value}",
                };
                _network.SendPacket(MessageId.GameGmReq, request);
            }
        }

        private void OnRoleAttrUpdated(Game.FullRoleInfo roleInfo)
        {
            RefreshFromPlayer();
        }
    }
}
