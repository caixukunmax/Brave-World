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
                var player = GetTree()?.GetFirstNodeInGroup("player") as Node2D;
                if (player != null)
                {
                    PlayPickupFlyAnimation(drop, player.GlobalPosition, notify.Count, dropId);
                }
                else
                {
                    ShowPickupFloatingText(drop.ItemId, notify.Count);
                    RemoveDropItem(dropId);
                }
            }
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

        private void PlayPickupFlyAnimation(DropItem drop, Vector2 targetPos, uint count, long dropId)
        {
            var flyer = new Sprite2D
            {
                Texture = ItemIconCatalog.GetIcon((uint)drop.ItemId),
                GlobalPosition = drop.GlobalPosition,
                Scale = new Vector2(0.5f, 0.5f),
            };
            AddChild(flyer);

            // 立即隐藏原地面掉落物，但等飞行动画结束后再真正移除
            drop.Visible = false;

            var tween = CreateTween();
            tween.SetTrans(Tween.TransitionType.Quad);
            tween.SetEase(Tween.EaseType.Out);
            tween.TweenProperty(flyer, "global_position", targetPos, 0.35f);
            tween.Parallel().TweenProperty(flyer, "scale", new Vector2(0.2f, 0.2f), 0.35f);

            tween.Finished += () =>
            {
                ShowPickupFloatingText(drop.ItemId, count);
                RemoveDropItem(dropId);
                flyer.QueueFree();
            };
        }
    }
}