using Godot;
using System.Collections.Generic;
using Game;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 掉落物管理器 — 接收协议消息，创建/移除掉落物节点
    /// </summary>
    public partial class DropManager : Node
    {
        private readonly Dictionary<long, DropItem> _drops = new();
        private int _gridSize = 111;

        public void Init(int gridSize)
        {
            _gridSize = gridSize;
        }

        /// <summary>处理掉落物生成通知</summary>
        public void OnDropSpawnNotify(DropSpawnNotify notify)
        {
            foreach (var d in notify.Drops)
            {
                CreateDropItem(d);
            }
        }

        /// <summary>处理掉落物拾取通知</summary>
        public void OnDropPickupNotify(DropPickupNotify notify)
        {
            long dropId = (long)notify.DropId;
            if (_drops.TryGetValue(dropId, out var drop))
            {
                // 播放拾取浮动文字（按实际进入背包数量）
                ShowPickupFloatingText(drop.ItemId, notify.ActualCount);
                RemoveDropItem(dropId);
            }

            // 若自己是拾取者且有剩余，提示背包已满
            var network = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (notify.RemainingCount > 0 && network != null && notify.PickerId == network.AccountId)
            {
                var invManager = GetTree()?.GetFirstNodeInGroup("inventory_manager") as InventoryManager;
                string itemName = invManager?.GetItemName(notify.ItemId) ?? $"Item#{notify.ItemId}";
                ShowInventoryFullToast(itemName, notify.RemainingCount);
            }
        }

        private void ShowInventoryFullToast(string itemName, uint remainingCount)
        {
            var network = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
            if (network == null) return;

            var toast = new Label
            {
                Text = $"背包空间不足，剩余 {remainingCount} 个{itemName}无法拾取",
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(1f, 0.4f, 0.4f),
            };
            toast.AddThemeFontSizeOverride("font_size", 14);
            toast.Position = new Vector2(0, -80);

            var canvas = new CanvasLayer { Layer = 200 };
            canvas.AddChild(toast);
            network.AddChild(canvas);

            // 2 秒后淡出移除
            var tween = CreateTween();
            tween.SetEase(Tween.EaseType.Out);
            tween.TweenProperty(toast, "modulate:a", 0.0, 1.0).SetDelay(1.0);
            tween.TweenCallback(Callable.From(() => canvas.QueueFree()));
        }

        /// <summary>处理掉落物消失通知</summary>
        public void OnDropRemoveNotify(DropRemoveNotify notify)
        {
            foreach (var id in notify.DropIds)
            {
                RemoveDropItem((long)id);
            }
        }

        /// <summary>处理地图同步（进地图时批量创建掉落物）</summary>
        public void OnMapInfoSyncDrops(List<DropItemInfo> drops)
        {
            // 清除旧掉落物（先复制 Keys 再遍历，避免遍历字典时修改字典抛异常）
            var oldIds = new List<long>(_drops.Keys);
            foreach (var id in oldIds)
            {
                RemoveDropItem(id);
            }
            _drops.Clear();

            // 创建新掉落物
            foreach (var d in drops)
            {
                CreateDropItem(d);
            }
        }

        private void CreateDropItem(DropItemInfo info)
        {
            long dropId = (long)info.DropId;
            if (_drops.ContainsKey(dropId)) return;

            var drop = new DropItem();
            drop.Setup(dropId, (int)info.ItemId, (int)info.Count, (int)info.X, (int)info.Y, _gridSize);

            _drops[dropId] = drop;
            AddChild(drop);
        }

        private void RemoveDropItem(long dropId)
        {
            if (!_drops.TryGetValue(dropId, out var drop)) return;
            _drops.Remove(dropId);
            drop.QueueFree();
        }

        /// <summary>统一设置所有掉落物实例的可见性（地图编辑器用）</summary>
        public void SetAllDropsVisible(bool visible)
        {
            foreach (var drop in _drops.Values)
            {
                if (drop != null && IsInstanceValid(drop))
                    drop.Visible = visible;
            }
        }

        private void ShowPickupFloatingText(int itemId, uint count)
        {
            string text = $"+{count} Item#{itemId}";
            // 浮动文字
            var ft = new FloatingText();
            ft.Setup(text, new Color(0.1f, 1f, 0.1f));
            AddChild(ft);
        }
    }
}