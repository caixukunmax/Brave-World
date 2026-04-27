using Godot;
using System.Collections.Generic;
using System.Linq;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 怪物管理器 - 管理地图上所有怪物实体
    /// 挂载到 Main 场景
    /// </summary>
    public partial class MonsterManager : Node
    {
        private List<Monster> _monsters = new();
        private HashSet<Vector2I> _monsterPositions = new();
        private int _gridSize = 111;
        private NetworkManager _network;
        private readonly Dictionary<ulong, Game.CombatStateNotify.Types.CombatUnit> _combatUnits = new();

        // 多配置样式系统 — Key = 配置ID（MonsterId）
        public readonly Dictionary<int, EntityStyleConfig> StyleConfigs = new();

        public EntityStyleConfig GetStyleConfig(int id)
        {
            if (StyleConfigs.TryGetValue(id, out var cfg))
                return cfg;
            // 回退到任意已有配置
            if (StyleConfigs.Count > 0)
                return StyleConfigs.Values.First();
            // 空字典时自动创建默认
            StyleConfigs[1] = EntityStyleConfig.CreateMonsterDefault();
            return StyleConfigs[1];
        }

        public EntityStyleConfig GetOrCreateStyleConfig(int id)
        {
            if (StyleConfigs.TryGetValue(id, out var cfg))
                return cfg;
            cfg = GetStyleConfig(0).Clone();
            StyleConfigs[id] = cfg;
            return cfg;
        }

        public override void _Ready()
        {
            AddToGroup("monster_manager");

            var tree = GetTree();
            if (tree != null)
            {
                foreach (var child in tree.Root.GetChildren())
                {
                    if (child is NetworkManager nm)
                    {
                        _network = nm;
                        break;
                    }
                }
                if (_network == null)
                    _network = tree.Root.GetNodeOrNull<NetworkManager>("NetworkManager");
                if (_network != null)
                {
                    _network.CombatStateNotify += OnCombatStateNotify;
                    _network.MonsterMoveCancelNotify += OnMonsterMoveCancel;
                }
            }

            // 提前加载配置，确保 DefaultVisualSizeScale 等默认值在 SpawnMonsters 之前就绪
            LoadDefaultStyleConfig();

            GD.Print("[MonsterManager] _Ready");
        }

        public override void _ExitTree()
        {
            if (_network != null)
            {
                _network.CombatStateNotify -= OnCombatStateNotify;
                _network.MonsterMoveCancelNotify -= OnMonsterMoveCancel;
            }
        }

        /// <summary>
        /// 从配置文件加载样式配置，确保 SpawnMonsters 时使用已保存的值
        /// </summary>
        private void LoadDefaultStyleConfig()
        {
            var config = new ConfigFile();
            if (config.Load("user://debug_panel_config.cfg") != Error.Ok)
            {
                // 没有配置文件，创建默认配置
                StyleConfigs[1] = EntityStyleConfig.CreateMonsterDefault();
                return;
            }

            // 读取所有 [monster_*] sections
            bool hasAny = false;
            foreach (var sec in config.GetSections())
            {
                if (!sec.StartsWith("monster_")) continue;
                var idStr = sec.Substring("monster_".Length);
                if (!int.TryParse(idStr, out int id)) continue;

                var cfg = EntityStyleConfig.CreateMonsterDefault();
                LoadStyleConfigFromSection(config, sec, cfg);
                StyleConfigs[id] = cfg;
                hasAny = true;
            }

            // 迁移旧 [monster] section
            if (!hasAny && config.HasSection("monster"))
            {
                var cfg = EntityStyleConfig.CreateMonsterDefault();
                LoadStyleConfigFromSection(config, "monster", cfg);
                StyleConfigs[1] = cfg;
                hasAny = true;
            }

            if (!hasAny)
                StyleConfigs[1] = EntityStyleConfig.CreateMonsterDefault();

            GD.Print($"[MonsterManager] Loaded {StyleConfigs.Count} style configs");
        }

        public static void LoadStyleConfigFromSection(ConfigFile config, string section, EntityStyleConfig cfg)
        {
            cfg.VisualSizeScale = (float)(double)config.GetValue(section, "visual_size_scale", cfg.VisualSizeScale);
            cfg.BorderWidthScale = (float)(double)config.GetValue(section, "border_width_scale", cfg.BorderWidthScale);
            cfg.CornerRadius = (float)(double)config.GetValue(section, "corner_radius", cfg.CornerRadius);
            cfg.BgOpacity = (float)(double)config.GetValue(section, "bg_opacity", cfg.BgOpacity);
            cfg.FontSize = (int)(double)config.GetValue(section, "font_size", cfg.FontSize);

            float bcR = (float)(double)config.GetValue(section, "border_color_r", cfg.BorderColor.R);
            float bcG = (float)(double)config.GetValue(section, "border_color_g", cfg.BorderColor.G);
            float bcB = (float)(double)config.GetValue(section, "border_color_b", cfg.BorderColor.B);
            cfg.BorderColor = new Color(bcR, bcG, bcB);

            float bgcR = (float)(double)config.GetValue(section, "bg_color_r", cfg.BgColor.R);
            float bgcG = (float)(double)config.GetValue(section, "bg_color_g", cfg.BgColor.G);
            float bgcB = (float)(double)config.GetValue(section, "bg_color_b", cfg.BgColor.B);
            cfg.BgColor = new Color(bgcR, bgcG, bgcB);

            float tcR = (float)(double)config.GetValue(section, "text_color_r", cfg.TextColor.R);
            float tcG = (float)(double)config.GetValue(section, "text_color_g", cfg.TextColor.G);
            float tcB = (float)(double)config.GetValue(section, "text_color_b", cfg.TextColor.B);
            cfg.TextColor = new Color(tcR, tcG, tcB);

            for (int i = 0; i < 4; i++)
            {
                cfg.LabelFontSizes[i] = (int)(double)config.GetValue(section, $"label_font_size_{i}", 0);
                cfg.LabelXOffsets[i] = (float)(double)config.GetValue(section, $"label_x_offset_{i}", 0);
                cfg.LabelCenterX[i] = (bool)config.GetValue(section, $"label_center_x_{i}", true);
                cfg.LabelYOffsets[i] = (float)(double)config.GetValue(section, $"label_y_offset_{i}", 0);
            }
        }

        public void SetGridSize(int size)
        {
            _gridSize = size;
            foreach (var m in _monsters)
                m.SetGridSize(size);
        }

        public void SpawnMonsters(List<Game.MonsterInfo> monsterData, int gridSize)
        {
            foreach (var m in _monsters)
                m.QueueFree();
            _monsters.Clear();
            _monsterPositions.Clear();

            _gridSize = gridSize;
            if (monsterData == null) return;

            foreach (var m in monsterData)
            {
                var monster = new Monster();
                monster.Setup(
                    m.InstanceId,
                    m.MonsterId,
                    m.X,
                    m.Y,
                    m.Name,
                    m.Level,
                    gridSize,
                    m.Attrs
                );
                ApplyDefaultStyle(monster);
                AddChild(monster);
                _monsters.Add(monster);
                _monsterPositions.Add(new Vector2I(m.X, m.Y));

                GD.Print($"[MonsterManager] Spawned monster {monster.InstanceId}({monster.MonsterName}) at ({monster.GridX},{monster.GridY})");
            }
        }

        public void ApplyDefaultStyle(Monster monster)
        {
            if (monster == null) return;
            var cfg = GetStyleConfig((int)monster.MonsterId);
            monster.SetVisualSizeScale(cfg.VisualSizeScale);
            monster.SetBorderWidthScale(cfg.BorderWidthScale);
            monster.SetCornerRadius(cfg.CornerRadius);
            monster.SetBgOpacity(cfg.BgOpacity);
            monster.SetFontSize(cfg.FontSize);
            monster.SetBorderColor(cfg.BorderColor);
            monster.SetBgColor(cfg.BgColor);
            monster.SetTextColor(cfg.TextColor);
            for (int i = 0; i < 4; i++)
            {
                if (!string.IsNullOrEmpty(cfg.LabelTexts[i]))
                    monster.SetLabelText(i, cfg.LabelTexts[i]);
                monster.SetLabelFontSize(i, cfg.LabelFontSizes[i]);
                monster.SetLabelXOffset(i, cfg.LabelXOffsets[i]);
                monster.SetLabelCenterX(i, cfg.LabelCenterX[i]);
                monster.SetLabelYOffset(i, cfg.LabelYOffsets[i]);
            }

            // 血条 / MP 条 — 只设 scale，setter 内部会自动计算 length/height
            monster.SetHealthBarVisible(cfg.HpBarVisible);
            monster.SetHealthBarLengthScale(cfg.HpBarLengthScale);
            monster.SetHealthBarHeightScale(cfg.HpBarHeightScale);
            monster.SetHealthBarFillPercent(cfg.HpBarFillPercent);
            monster.SetHealthBarOffset(new Vector2(cfg.HpBarOffsetX, cfg.HpBarOffsetY));
            monster.SetHealthBarColor(cfg.HpBarColor);
            monster.SetMpBarVisible(cfg.MpBarVisible);
            monster.SetMpBarLengthScale(cfg.MpBarLengthScale);
            monster.SetMpBarHeightScale(cfg.MpBarHeightScale);
            monster.SetMpBarFillPercent(cfg.MpBarFillPercent);
            monster.SetMpBarOffset(new Vector2(cfg.MpBarOffsetX, cfg.MpBarOffsetY));
            monster.SetMpBarColor(cfg.MpBarColor);
        }

        public void ApplyStyleToAll()
        {
            foreach (var m in _monsters)
                ApplyDefaultStyle(m);
        }

        public void SetActionBarTextYOffsetAll(float offset)
        {
            foreach (var m in _monsters)
                m.SetActionBarTextYOffset(offset);
        }

        public void SetActionBarProgressHeightAll(float height)
        {
            foreach (var m in _monsters)
                m.SetActionBarProgressHeight(height);
        }

        public System.Collections.Generic.IReadOnlyList<Monster> GetMonsters() => _monsters;

        public void SetHealthBarVisibleAll(bool visible) { foreach (var m in _monsters) m.SetHealthBarVisible(visible); SyncStyleConfigHpBar(); }
        public void SetHealthBarLengthScaleAll(float scale) { foreach (var m in _monsters) m.SetHealthBarLengthScale(scale); SyncStyleConfigHpBar(); }
        public void SetHealthBarHeightScaleAll(float scale) { foreach (var m in _monsters) m.SetHealthBarHeightScale(scale); SyncStyleConfigHpBar(); }
        public void SetHealthBarFillPercentAll(float percent) { foreach (var m in _monsters) m.SetHealthBarFillPercent(percent); SyncStyleConfigHpBar(); }
        public void SetHealthBarColorAll(Color color) { foreach (var m in _monsters) m.SetHealthBarColor(color); SyncStyleConfigHpBar(); }
        public void SetHealthBarOffsetAll(Vector2 offset) { foreach (var m in _monsters) m.SetHealthBarOffset(offset); SyncStyleConfigHpBar(); }

        public void SetMpBarVisibleAll(bool visible) { foreach (var m in _monsters) m.SetMpBarVisible(visible); SyncStyleConfigHpBar(); }
        public void SetMpBarLengthScaleAll(float scale) { foreach (var m in _monsters) m.SetMpBarLengthScale(scale); SyncStyleConfigHpBar(); }
        public void SetMpBarHeightScaleAll(float scale) { foreach (var m in _monsters) m.SetMpBarHeightScale(scale); SyncStyleConfigHpBar(); }
        public void SetMpBarFillPercentAll(float percent) { foreach (var m in _monsters) m.SetMpBarFillPercent(percent); SyncStyleConfigHpBar(); }
        public void SetMpBarColorAll(Color color) { foreach (var m in _monsters) m.SetMpBarColor(color); SyncStyleConfigHpBar(); }
        public void SetMpBarOffsetAll(Vector2 offset) { foreach (var m in _monsters) m.SetMpBarOffset(offset); SyncStyleConfigHpBar(); }

        /// <summary>
        /// 将当前怪物实体的血条/MP条 Scale 参数同步回所有 StyleConfig
        /// </summary>
        private void SyncStyleConfigHpBar()
        {
            if (_monsters.Count == 0) return;
            var first = _monsters[0];
            foreach (var kv in StyleConfigs)
            {
                var c = kv.Value;
                c.HpBarVisible = first.HealthBarVisible;
                c.HpBarLengthScale = first.HealthBarLengthScale;
                c.HpBarHeightScale = first.HealthBarHeightScale;
                c.HpBarFillPercent = first.HealthBarFillPercent;
                c.HpBarOffsetX = first.HealthBarOffset.X;
                c.HpBarOffsetY = first.HealthBarOffset.Y;
                c.HpBarColor = first.HealthBarColor;
                c.MpBarVisible = first.MpBarVisible;
                c.MpBarLengthScale = first.MpBarLengthScale;
                c.MpBarHeightScale = first.MpBarHeightScale;
                c.MpBarFillPercent = first.MpBarFillPercent;
                c.MpBarOffsetX = first.MpBarOffset.X;
                c.MpBarOffsetY = first.MpBarOffset.Y;
                c.MpBarColor = first.MpBarColor;
            }
        }

        public Monster GetMonsterAt(Vector2I gridPos)
        {
            if (!_monsterPositions.Contains(gridPos))
                return null;
            foreach (var m in _monsters)
            {
                if (m.GridX == gridPos.X && m.GridY == gridPos.Y)
                    return m;
            }
            return null;
        }

        public void OnMonsterMove(uint instanceId, Vector2I from, Vector2I to, string state, int durationMs)
        {
            var m = _monsters.Find(x => x.InstanceId == instanceId);
            if (m == null) return;
            _monsterPositions.Remove(from);
            _monsterPositions.Add(to);
            m.CurrentState = state;
            float durationSec = durationMs > 0 ? durationMs / 1000.0f : 0.15f;
            m.MoveTo(to, durationSec);
        }

        public bool IsBlockedByMonster(Vector2I gridPos)
        {
            return _monsterPositions.Contains(gridPos);
        }

        private void OnCombatStateNotify(Game.CombatStateNotify notify)
        {
            if (notify.Units.Count == 0)
            {
                foreach (var m in _monsters)
                {
                    m.CastingSkill = "";
                    m.CastProgress = 0;
                    m.HealthBarFillPercent = 1.0f;
                    m.MpBarFillPercent = 1.0f;
                    m.QueueRedraw();
                }
                return;
            }

            var combatMonsters = _combatUnits;
            combatMonsters.Clear();
            foreach (var unit in notify.Units)
            {
                if (!unit.IsPlayer)
                    combatMonsters[unit.EntityId] = unit;
            }

            foreach (var m in _monsters)
            {
                if (combatMonsters.TryGetValue(m.InstanceId, out var unit))
                {
                    m.CastingSkill = unit.CastingSkill;
                    m.CastProgress = unit.CastProgress;
                    if (unit.MaxHp > 0)
                        m.HealthBarFillPercent = (float)unit.Hp / unit.MaxHp;
                    if (unit.MaxMp > 0)
                        m.MpBarFillPercent = (float)unit.Mp / unit.MaxMp;
                }
                else
                {
                    m.CastingSkill = "";
                    m.CastProgress = 0;
                    m.HealthBarFillPercent = 1.0f;
                    m.MpBarFillPercent = 1.0f;
                }
                m.QueueRedraw();
            }
        }

        public void OnMonsterMoveCancel(Game.MonsterMoveCancelNotify notify)
        {
            var m = _monsters.Find(x => x.InstanceId == notify.InstanceId);
            if (m == null) return;

            var rollbackPos = new Vector2I(notify.RollbackX, notify.RollbackY);
            _monsterPositions.Remove(new Vector2I(m.GridX, m.GridY));
            _monsterPositions.Add(rollbackPos);

            // 移动中用平滑弹回动画，避免瞬移
            if (m.IsMoving)
                m.PlayBounceBack(rollbackPos);
            else
                m.RollbackTo(rollbackPos);

            GD.Print($"[MonsterManager] Monster {m.InstanceId}({m.MonsterName}) move cancelled, rolled back to ({rollbackPos.X},{rollbackPos.Y})");
        }
    }
}
