using Godot;

namespace ClinetCSharp
{
    /// <summary>
    /// Centralized access to frequently used scene services for UI scripts.
    /// This keeps path assumptions in one place instead of scattering them across panels.
    /// </summary>
    public static class UiServices
    {
        public static NetworkManager GetNetworkManager(Node node)
        {
            return node?.GetTree()?.Root.GetNodeOrNull<NetworkManager>("NetworkManager");
        }

        public static InventoryManager GetInventoryManager(Node node)
        {
            var inventoryManager = node?.GetTree()?.GetFirstNodeInGroup("inventory_manager") as InventoryManager;
            if (inventoryManager != null)
                return inventoryManager;

            return node?.GetNodeOrNull<InventoryManager>("/root/Main/InventoryManager");
        }
    }
}
