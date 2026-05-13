using Google.Protobuf;
using Godot;
using Protocol;

namespace ClinetCSharp
{
    public partial class NetworkManager
    {
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
            CombatLogNotify?.Invoke(Game.CombatLogNotify.Parser.ParseFrom(data));
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
    }
}
