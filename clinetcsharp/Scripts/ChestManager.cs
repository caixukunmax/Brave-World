using Godot;
using System.Collections.Generic;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 宝箱管理器 - 管理地图上所有宝箱实体
    /// 挂载到 Main 场景
    /// </summary>
    public partial class ChestManager : Node
    {
        private List<Chest> _chests = new();
        private int _gridSize = 111;
        private CanvasLayer _floatLayer;

        public override void _Ready()
        {
            AddToGroup("chest_manager");

            // 飘字专用 CanvasLayer（屏幕空间，不受相机影响）
            _floatLayer = new CanvasLayer { Layer = 10 };
            AddChild(_floatLayer);

            GD.Print("[ChestManager] _Ready");
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                var worldPos = mb.GlobalPosition;
                var player = GetTree()?.GetFirstNodeInGroup("player");
                if (player is Player p)
                {
                    if (TryInteractChest(worldPos, p.GridPos))
                        GetViewport().SetInputAsHandled();
                }
            }
        }

        public void SpawnChests(Godot.Collections.Array chestData, int gridSize)
        {
            foreach (var chest in _chests)
                chest.QueueFree();
            _chests.Clear();

            _gridSize = gridSize;
            if (chestData == null) return;

            var gridMgr = GetTree()?.GetFirstNodeInGroup("grid_manager") as GridManager;

            foreach (var entry in chestData)
            {
                var dict = entry.AsGodotDictionary();
                if (dict["opened"].AsBool()) continue; // 已开启的宝箱不显示

                var chest = new Chest();
                chest.Setup(
                    (uint)dict["chest_id"].AsInt32(),
                    dict["x"].AsInt32(),
                    dict["y"].AsInt32(),
                    dict["opened"].AsBool(),
                    gridSize
                );
                AddChild(chest);
                _chests.Add(chest);

                if (!chest.Opened && gridMgr != null)
                    gridMgr.BlockCell(new Vector2I(chest.GridX, chest.GridY));

                GD.Print($"[ChestManager] Spawned chest {chest.ChestId} at ({chest.GridX},{chest.GridY}) opened={chest.Opened}");
            }
        }

        public bool TryInteractChest(Vector2 worldPos, Vector2I playerGrid)
        {
            foreach (var chest in _chests)
            {
                if (chest.HitTest(worldPos))
                {
                    if (chest.Opened)
                    {
                        GD.Print("[ChestManager] Chest already opened");
                        return true;
                    }

                    int dx = Mathf.Abs(playerGrid.X - chest.GridX);
                    int dy = Mathf.Abs(playerGrid.Y - chest.GridY);
                    if (dx + dy > 1)
                    {
                        GD.Print("[ChestManager] Too far from chest");
                        return true;
                    }

                    SendOpenChest(chest);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 玩家朝宝箱走时自动触发开箱
        /// </summary>
        public bool TryOpenChestAt(Vector2I gridPos)
        {
            foreach (var chest in _chests)
            {
                if (!chest.Opened && chest.GridX == gridPos.X && chest.GridY == gridPos.Y)
                {
                    SendOpenChest(chest);
                    return true;
                }
            }
            return false;
        }

        private void SendOpenChest(Chest chest)
        {
            var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (nm == null || !nm.IsServerConnected()) return;

            nm.PendingOpenChestId = chest.ChestId;
            var req = new Game.OpenChestRequest { ChestId = chest.ChestId };
            nm.SendPacket(MessageId.GameOpenChestReq, req);
            GD.Print($"[ChestManager] Sent OpenChest req, chestId={chest.ChestId}");
        }

        public void OnOpenChestResponse(Game.OpenChestResponse rsp, uint chestId)
        {
            if (rsp.Code != Common.ErrorCode.Success)
            {
                GD.Print($"[ChestManager] OpenChest failed: {rsp.Message}");
                return;
            }

            var gridMgr = GetTree()?.GetFirstNodeInGroup("grid_manager") as GridManager;

            Chest openedChest = null;
            foreach (var chest in _chests)
            {
                if (chest.ChestId == chestId)
                {
                    openedChest = chest;
                    if (gridMgr != null)
                        gridMgr.UnblockCell(new Vector2I(chest.GridX, chest.GridY));
                    chest.MarkOpened();
                    break;
                }
            }
            if (openedChest != null)
                _chests.Remove(openedChest);

            // 飘字显示获得的道具（屏幕中央偏上）
            if (rsp.Items.Count > 0)
            {
                var screen = GetViewport().GetVisibleRect().Size;
                float centerX = screen.X / 2;
                float startY = screen.Y * 0.35f;

                var inv = GetTree()?.GetFirstNodeInGroup("inventory_manager") as InventoryManager;
                for (int i = 0; i < rsp.Items.Count; i++)
                {
                    var item = rsp.Items[i];
                    string name = inv?.GetItemName(item.ItemId) ?? $"物品{item.ItemId}";
                    var ft = new FloatingText();
                    ft.Setup($"+{item.Count} {name}", new Color(1, 0.9f, 0.2f), 16);
                    ft.Position = new Vector2(centerX - 60, startY + i * 28);
                    ft.CustomMinimumSize = new Vector2(120, 24);
                    _floatLayer.AddChild(ft);
                }

                if (inv != null)
                    inv.AddOrUpdateItemsFromProto(rsp.Items);
            }

            GD.Print($"[ChestManager] Opened chest, got {rsp.Items.Count} items");
        }
    }
}
