using Godot;

namespace ClinetCSharp
{
    public partial class Player
    {
        public override void _Ready()
        {
            LoadStyleConfig();
            ProfileId = 1;
            EnsureRenderComponents();

            Position = UiUtils.GridToWorld(_gridPos, GridSize);
            for (int i = 0; i < LabelCount; i++)
                _labelOffsets[i] = DefaultOffsets[i];

            SetupLabels();
            QueueRedraw();

            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm != null)
            {
                SubscribeNetworkEvents(nm);
                if (nm.CachedRoleInfo != null)
                    ApplyRoleInfo(nm.CachedRoleInfo);
            }
        }

        public override void _ExitTree()
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm != null)
                UnsubscribeNetworkEvents(nm);

            _checkTimer?.Stop();
            _checkTimer?.QueueFree();
            _checkTimer = null;
        }
    }
}
