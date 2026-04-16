using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 怪物管理器 - 管理地图上所有怪物实体
    /// 挂载到 Main 场景
    /// </summary>
    public partial class MonsterManager : Node
    {
        private List<Monster> _monsters = new();
        private int _gridSize = 111;

        // 默认样式配置（所有怪物共用）
        public int DefaultVisualSize { get; set; } = 111;
        public float DefaultVisualSizeScale { get; set; } = 1.0f;
        public float DefaultBorderWidth { get; set; } = 3.0f;
        public float DefaultBorderWidthScale { get; set; } = 3.0f / 111.0f;
        public float DefaultCornerRadius { get; set; } = 12.0f;
        public float DefaultBgOpacity { get; set; } = 0.9f;
        public int DefaultFontSize { get; set; } = 0;
        public Color DefaultBorderColor { get; set; } = new Color(0.9f, 0.3f, 0.3f);
        public Color DefaultBgColor { get; set; } = new Color(0.8f, 0.2f, 0.2f);
        public Color DefaultTextColor { get; set; } = new Color(1, 0.95f, 0.95f);
        public string[] DefaultLabelTexts { get; set; } = new string[4] { "", "", "", "" };
        public int[] DefaultLabelFontSizes { get; set; } = new int[4] { 0, 0, 0, 0 };
        public float[] DefaultLabelXOffsets { get; set; } = new float[4] { 0, 0, 0, 0 };
        public bool[] DefaultLabelCenterX { get; set; } = new bool[4] { true, true, true, true };
        public float[] DefaultLabelYOffsets { get; set; } = new float[4] { 0, 0, 0, 0 };

        public override void _Ready()
        {
            AddToGroup("monster_manager");
            GD.Print("[MonsterManager] _Ready");
        }

        public void SetGridSize(int size)
        {
            _gridSize = size;
            DefaultVisualSize = size;
            DefaultBorderWidth = Mathf.Clamp(size * DefaultBorderWidthScale, 1.0f, 20.0f);
            foreach (var m in _monsters)
                m.SetGridSize(size);
        }

        public void SpawnMonsters(Godot.Collections.Array monsterData, int gridSize)
        {
            foreach (var m in _monsters)
                m.QueueFree();
            _monsters.Clear();

            _gridSize = gridSize;
            DefaultVisualSize = gridSize;
            if (monsterData == null) return;

            foreach (var entry in monsterData)
            {
                var dict = entry.AsGodotDictionary();
                var monster = new Monster();
                var attrs = dict.ContainsKey("attrs") ? dict["attrs"].AsGodotArray() : new Godot.Collections.Array();
                monster.Setup(
                    (uint)dict["instance_id"].AsInt32(),
                    (uint)dict["monster_id"].AsInt32(),
                    dict["x"].AsInt32(),
                    dict["y"].AsInt32(),
                    dict["name"].AsString(),
                    (uint)dict["level"].AsInt32(),
                    gridSize,
                    attrs
                );
                ApplyDefaultStyle(monster);
                AddChild(monster);
                _monsters.Add(monster);

                GD.Print($"[MonsterManager] Spawned monster {monster.InstanceId}({monster.MonsterName}) at ({monster.GridX},{monster.GridY})");
            }
        }

        public void ApplyDefaultStyle(Monster monster)
        {
            if (monster == null) return;
            monster.SetVisualSize(DefaultVisualSize);
            monster.SetVisualSizeScale(DefaultVisualSizeScale);
            monster.SetBorderWidth(DefaultBorderWidth);
            monster.SetBorderWidthScale(DefaultBorderWidthScale);
            monster.SetCornerRadius(DefaultCornerRadius);
            monster.SetBgOpacity(DefaultBgOpacity);
            monster.SetFontSize(DefaultFontSize);
            monster.SetBorderColor(DefaultBorderColor);
            monster.SetBgColor(DefaultBgColor);
            monster.SetTextColor(DefaultTextColor);
            // 只有用户显式修改过（非空）时才覆盖文字，否则保留 Setup 的默认名字/等级/属性
            for (int i = 0; i < 4; i++)
            {
                if (!string.IsNullOrEmpty(DefaultLabelTexts[i]))
                    monster.SetLabelText(i, DefaultLabelTexts[i]);
                monster.SetLabelFontSize(i, DefaultLabelFontSizes[i]);
                monster.SetLabelXOffset(i, DefaultLabelXOffsets[i]);
                monster.SetLabelCenterX(i, DefaultLabelCenterX[i]);
                monster.SetLabelYOffset(i, DefaultLabelYOffsets[i]);
            }
        }

        public void ApplyStyleToAll()
        {
            foreach (var m in _monsters)
                ApplyDefaultStyle(m);
        }

        public Monster GetMonsterAt(Vector2I gridPos)
        {
            foreach (var m in _monsters)
            {
                if (m.GridX == gridPos.X && m.GridY == gridPos.Y)
                    return m;
            }
            return null;
        }

        public void OnMonsterMove(uint instanceId, Vector2I from, Vector2I to, string state)
        {
            var m = _monsters.Find(x => x.InstanceId == instanceId);
            if (m == null) return;
            m.CurrentState = state;
            m.MoveTo(to, 0.15f);
        }

        public bool IsBlockedByMonster(Vector2I gridPos)
        {
            return _monsters.Exists(m => m.GridPos == gridPos);
        }
    }
}
