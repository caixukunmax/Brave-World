using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityClientSharp.Map.Core;
using UnityClientSharp.Map.Rendering;
using UnityClientSharp.Net;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 掉落物实体（显示层）。
    /// 移植自 Godot DropItem.cs：图标（item_config icon，缺省 banana）缩放到 0.4×格、
    /// 品质色描边、sin(t×3)×3px 浮动、count>1 显示 xN。不是 EntityBase（Godot 亦然）。
    /// </summary>
    public class DropItemEntity : MonoBehaviour
    {
        public ulong DropId { get; private set; }
        public int ItemId { get; private set; }
        public int Count { get; private set; }
        public Vector2Int GridPos { get; private set; }

        private Transform _iconTf;
        private Vector3 _iconBaseLocalPos;
        private float _phase;

        public void Setup(ulong dropId, int itemId, int count, Vector2Int gridPos, int gridSize)
        {
            DropId = dropId;
            ItemId = itemId;
            Count = count;
            GridPos = gridPos;
            float box = gridSize * 0.4f;
            transform.localPosition = GridMath.FootprintCenterWorld(gridPos.x, gridPos.y, 1, 1, gridSize);
            _phase = (float)((dropId % 100) / 100.0) * Mathf.PI * 2f; // 相位错开，避免全场同步浮动

            // 底部光晕（白色 alpha 0.15，半径 0.6×box）
            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(transform, false);
            var glow = glowGo.AddComponent<SpriteRenderer>();
            glow.sprite = EntityVisualBase.UnitSprite;
            glow.color = new Color(1f, 1f, 1f, 0.15f);
            glowGo.transform.localScale = new Vector3(box * 1.2f, box * 1.2f, 1f);
            glow.sortingOrder = 6;

            // 品质描边（比图标大一圈的色块垫底）
            var borderGo = new GameObject("QualityBorder");
            borderGo.transform.SetParent(transform, false);
            var border = borderGo.AddComponent<SpriteRenderer>();
            border.sprite = EntityVisualBase.UnitSprite;
            border.color = ItemIconCatalog.GetQualityColor(ItemIconCatalog.GetQuality(itemId));
            borderGo.transform.localScale = new Vector3(box * 1.08f, box * 1.08f, 1f);
            border.sortingOrder = 6;

            // 图标
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(transform, false);
            _iconTf = iconGo.transform;
            _iconBaseLocalPos = Vector3.zero;
            var sr = iconGo.AddComponent<SpriteRenderer>();
            var tex = ItemIconCatalog.GetIcon(itemId);
            if (tex != null)
            {
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), tex.width / box);
            }
            sr.sortingOrder = 7;

            // 数量
            if (count > 1)
            {
                var cntGo = new GameObject("Count");
                cntGo.transform.SetParent(transform, false);
                cntGo.transform.localPosition = new Vector3(box * 0.4f, -box * 0.4f, 0);
                var tmp = cntGo.AddComponent<TextMeshPro>();
                tmp.text = $"x{count}";
                tmp.fontSize = 11;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Center;
                FontUtil.ApplyCjkFont(tmp);
                tmp.GetComponent<MeshRenderer>().sortingOrder = 8;
            }
        }

        private void Update()
        {
            if (_iconTf != null)
                _iconTf.localPosition = _iconBaseLocalPos + new Vector3(0, Mathf.Sin(Time.time * 3f + _phase) * 3f, 0);
        }
    }

    /// <summary>
    /// 掉落物管理器（显示层）。
    /// 移植自 Godot DropManager：Spawn/Remove/Pickup 通知处理；Pickup 时图标飞向玩家 0.35s 后删除。
    /// 裁剪：落地弹跳、背包满 toast、拾取飘字。
    /// </summary>
    public class DropManager : MonoBehaviour
    {
        public static DropManager Instance { get; private set; }

        private readonly Dictionary<ulong, DropItemEntity> _dropById = new();
        private int _gridSize = 111;

        public int GridSize { get => _gridSize; set => _gridSize = value; }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (NetworkManager.Instance == null) return;
            NetworkManager.Instance.DropSpawnNotify += OnDropSpawn;
            NetworkManager.Instance.DropRemoveNotify += OnDropRemove;
            NetworkManager.Instance.DropPickupNotify += OnDropPickup;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.DropSpawnNotify -= OnDropSpawn;
                NetworkManager.Instance.DropRemoveNotify -= OnDropRemove;
                NetworkManager.Instance.DropPickupNotify -= OnDropPickup;
            }
        }

        public void SpawnDrops(IEnumerable<Game.DropItemInfo> drops)
        {
            ClearDrops();
            if (drops == null) return;
            foreach (var info in drops)
                SpawnOne(info);
            Debug.Log($"[DropManager] 生成 {_dropById.Count} 个掉落物");
        }

        private DropItemEntity SpawnOne(Game.DropItemInfo info)
        {
            if (_dropById.ContainsKey(info.DropId))
                return _dropById[info.DropId];

            var go = new GameObject($"Drop_{info.DropId}_item{info.ItemId}");
            go.transform.SetParent(transform, false);
            var drop = go.AddComponent<DropItemEntity>();
            drop.Setup(info.DropId, (int)info.ItemId, (int)info.Count,
                new Vector2Int(info.X, info.Y), _gridSize);
            _dropById[info.DropId] = drop;
            return drop;
        }

        private void OnDropSpawn(Game.DropSpawnNotify notify)
        {
            foreach (var info in notify.Drops)
                SpawnOne(info);
        }

        private void OnDropRemove(Game.DropRemoveNotify notify)
        {
            foreach (var id in notify.DropIds)
            {
                if (!_dropById.TryGetValue(id, out var drop)) continue;
                _dropById.Remove(id);
                if (drop != null) Destroy(drop.gameObject);
            }
        }

        private void OnDropPickup(Game.DropPickupNotify notify)
        {
            if (!_dropById.TryGetValue(notify.DropId, out var drop)) return;
            _dropById.Remove(notify.DropId);
            if (drop == null) return;

            // 对齐 Godot：图标 0.35s 飞向玩家并缩到 0.2，结束后删除
            var player = PlayerEntity.Instance;
            if (player != null)
                StartCoroutine(FlyToPlayerRoutine(drop, player.transform));
            else
                Destroy(drop.gameObject);
        }

        private IEnumerator FlyToPlayerRoutine(DropItemEntity drop, Transform playerTf)
        {
            const float duration = 0.35f;
            float t = 0f;
            Vector3 from = drop.transform.position;
            Vector3 fromScale = drop.transform.localScale;
            while (t < duration && drop != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float e = 1f - (1f - k) * (1f - k); // Quad/Out
                drop.transform.position = Vector3.LerpUnclamped(from, playerTf.position, e);
                drop.transform.localScale = Vector3.LerpUnclamped(fromScale, fromScale * 0.2f, e);
                yield return null;
            }
            if (drop != null) Destroy(drop.gameObject);
        }

        public void ClearDrops()
        {
            foreach (var d in _dropById.Values)
                if (d != null) Destroy(d.gameObject);
            _dropById.Clear();
        }
    }
}
