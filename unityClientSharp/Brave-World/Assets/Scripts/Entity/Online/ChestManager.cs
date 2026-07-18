using System.Collections.Generic;
using UnityClientSharp.Map.Core;
using UnityClientSharp.Net;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 宝箱实体（显示层）。
    /// 移植自 Godot Chest.cs：单格（box=0.6×格）、线稿宝箱贴图（程序生成）、opened 即消失。
    /// 注意：宝箱不是 EntityBase（Godot 亦然），不套 footprint/标签/条。
    /// </summary>
    public class ChestEntity : MonoBehaviour
    {
        public uint ChestId { get; private set; }
        public Vector2Int GridPos { get; private set; }

        private static readonly Dictionary<int, Sprite> _spriteCache = new();

        public void Setup(uint chestId, Vector2Int gridPos, int gridSize)
        {
            ChestId = chestId;
            GridPos = gridPos;
            transform.localPosition = GridMath.FootprintCenterWorld(gridPos.x, gridPos.y, 1, 1, gridSize);

            var go = new GameObject("Body");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetChestSprite(Mathf.RoundToInt(gridSize * 0.6f));
            sr.sortingOrder = 3; // 玩家之上、怪物之下
        }

        /// <summary>线稿宝箱贴图（对齐 Godot Chest._Draw：深灰箱体+白描边+箱盖+锁扣），按尺寸缓存。</summary>
        private static Sprite GetChestSprite(int boxSize)
        {
            if (_spriteCache.TryGetValue(boxSize, out var cached) && cached != null)
                return cached;

            var bodyColor = new Color(0.2f, 0.2f, 0.2f);
            var lidColor = new Color(0.3f, 0.3f, 0.3f);
            int border = 2;
            int bodyH = Mathf.RoundToInt(boxSize * 0.7f);
            int lidH = Mathf.RoundToInt(boxSize * 0.5f);
            int latch = Mathf.Max(2, Mathf.RoundToInt(boxSize * 0.15f));

            var tex = new Texture2D(boxSize, boxSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"Chest_{boxSize}",
            };

            for (int y = 0; y < boxSize; y++)
            {
                for (int x = 0; x < boxSize; x++)
                {
                    Color c = Color.clear;
                    // 箱体：底部 bodyH 高
                    if (y < bodyH)
                    {
                        bool isBorder = x < border || x >= boxSize - border || y < border || y >= bodyH - border;
                        c = isBorder ? Color.white : bodyColor;
                    }
                    // 箱盖：顶部 lidH 高（覆盖箱体上沿）
                    if (y >= boxSize - lidH)
                    {
                        bool isBorder = x < border || x >= boxSize - border || y >= boxSize - border || y < boxSize - lidH;
                        c = isBorder ? Color.white : lidColor;
                    }
                    // 金属横线：箱盖与箱体交界处
                    if (Mathf.Abs(y - (boxSize - lidH)) < border)
                        c = Color.white;
                    // 锁扣：中心白色方块
                    if (Mathf.Abs(x - boxSize / 2) <= latch / 2 && Mathf.Abs(y - (boxSize - lidH)) <= latch)
                        c = Color.white;

                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, boxSize, boxSize), new Vector2(0.5f, 0.5f), 1f);
            _spriteCache[boxSize] = sprite;
            return sprite;
        }
    }

    /// <summary>
    /// 宝箱管理器（显示层）。
    /// 移植自 Godot ChestManager：未开才生成、按位置去重、ChestUpdateNotify 增量处理。
    /// 裁剪：开箱交互/飘字。
    /// </summary>
    public class ChestManager : MonoBehaviour
    {
        public static ChestManager Instance { get; private set; }

        private readonly Dictionary<Vector2Int, ChestEntity> _chestByPos = new();
        private int _gridSize = 111;

        public int GridSize { get => _gridSize; set => _gridSize = value; }

        private void Awake()
        {
            Instance = this;
            Walkability.Chests = this;
        }

        private void Start()
        {
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.ChestUpdateNotify += OnChestUpdate;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Walkability.Chests == this) Walkability.Chests = null;
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.ChestUpdateNotify -= OnChestUpdate;
        }

        /// <summary>未开宝箱占格（供 Walkability；已开宝箱已从集合移除不阻挡）。</summary>
        public bool IsBlockedByChest(Vector2Int pos) => _chestByPos.ContainsKey(pos);

        /// <summary>取该格宝箱的 ChestId（无则 0，供开箱请求）。</summary>
        public uint GetChestIdAt(Vector2Int pos)
            => _chestByPos.TryGetValue(pos, out var chest) && chest != null ? chest.ChestId : 0;

        public void SpawnChests(IEnumerable<Game.ChestInfo> chests)
        {
            ClearChests();
            if (chests == null) return;
            foreach (var info in chests)
                SpawnOne(info);
            Debug.Log($"[ChestManager] 生成 {_chestByPos.Count} 个宝箱");
        }

        private void SpawnOne(Game.ChestInfo info)
        {
            if (info.Opened) return; // 已开不生成（对齐 Godot）
            var pos = new Vector2Int(info.X, info.Y);
            if (_chestByPos.ContainsKey(pos)) return;

            var go = new GameObject($"Chest_{info.ChestId}_{pos.x}_{pos.y}");
            go.transform.SetParent(transform, false);
            var chest = go.AddComponent<ChestEntity>();
            chest.Setup(info.ChestId, pos, _gridSize);
            _chestByPos[pos] = chest;
        }

        private void OnChestUpdate(Game.ChestUpdateNotify notify)
        {
            foreach (var info in notify.Chests)
            {
                var pos = new Vector2Int(info.X, info.Y);
                if (info.Opened)
                {
                    if (_chestByPos.TryGetValue(pos, out var chest))
                    {
                        _chestByPos.Remove(pos);
                        if (chest != null) Destroy(chest.gameObject);
                    }
                }
                else
                {
                    SpawnOne(info);
                }
            }
        }

        public void ClearChests()
        {
            foreach (var c in _chestByPos.Values)
                if (c != null) Destroy(c.gameObject);
            _chestByPos.Clear();
        }
    }
}
