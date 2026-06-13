using Godot;
using System.Collections.Generic;
using System.Linq;
using Protocol;

namespace ClinetCSharp
{
    /// <summary>
    /// 鎬墿绠＄悊鍣?- 绠＄悊鍦板浘涓婃墍鏈夋€墿瀹炰綋
    /// 鎸傝浇鍒?Main 鍦烘櫙
    /// </summary>
    public partial class MonsterManager : Node
    {
        private List<Monster> _monsters = new();
        private Dictionary<uint, Vector2I> _monsterPositions = new();
        private HashSet<Vector2I> _monsterReservedPositions = new();
        private int _gridSize = 111;
        private NetworkManager _network;
        private readonly Dictionary<ulong, Game.CombatStateNotify.Types.CombatUnit> _combatUnits = new();

        // 怪物死亡效果配置（供调试面板调节）
        public int DeathEffectMode { get; set; } = 1; // 0=直接删除, 1=淡出, 2=变灰停留后淡出
        public float DeathFadeDuration { get; set; } = 0.5f;
        public float DeathGrayDelay { get; set; } = 3.0f;

        /// <summary>统一设置所有怪物实例的可见性（地图编辑器用）</summary>
        public void SetAllMonstersVisible(bool visible)
        {
            foreach (var monster in _monsters)
            {
                if (monster != null && IsInstanceValid(monster))
                    monster.Visible = visible;
            }
        }

        // 多配置样式系统：Key = 配置ID（MonsterId）
        // [Obsolete] 已被 EntityProfileManager 接管。保留仅用于旧代码兼容和迁移。
        public readonly Dictionary<int, EntityStyleConfig> StyleConfigs = new();
        private readonly HashSet<int> _missingConfigWarned = new();

        public EntityStyleConfig GetStyleConfig(int id)
        {
            if (StyleConfigs.TryGetValue(id, out var cfg))
                return cfg;
            // 没有配置时用默认值显示，只报一次错
            if (!_missingConfigWarned.Contains(id))
            {
                GD.PrintErr($"[MonsterManager] No StyleConfig for MonsterId={id}! Available: [{string.Join(", ", StyleConfigs.Keys)}]. Using default. Fix: create config for this ID in debug panel.");
                _missingConfigWarned.Add(id);
            }
            return EntityStyleConfig.CreateMonsterDefault();
        }

        public EntityStyleConfig GetOrCreateStyleConfig(int id)
        {
            if (StyleConfigs.TryGetValue(id, out var cfg))
                return cfg;
            GD.PrintErr($"[MonsterManager] No StyleConfig for MonsterId={id}! Available: [{string.Join(", ", StyleConfigs.Keys)}]. Create it in the debug panel first.");
            // 回退到第一个已有配置，避免 NPE
            if (StyleConfigs.Count > 0)
                return StyleConfigs.Values.First();
            StyleConfigs[1] = EntityStyleConfig.CreateMonsterDefault();
            return StyleConfigs[1];
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
                    _network.CombatEndNotify += OnCombatEndNotify;
                    _network.CastStartNotify += OnCastStartNotify;
                    _network.CombatEventNotify += OnCombatEventNotify;
                    _network.MonsterMoveCancelNotify += OnMonsterMoveCancel;
                    _network.MonsterDeathNotify += OnMonsterDeathNotify;
                    _network.MonsterRespawnNotify += OnMonsterRespawnNotify;
                }
            }

            // 鎻愬墠鍔犺浇閰嶇疆锛岀‘淇?DefaultVisualSizeScale 绛夐粯璁ゅ€煎湪 SpawnMonsters 涔嬪墠灏辩华
            LoadDefaultStyleConfig();

            GD.Print("[MonsterManager] _Ready");
        }

        public override void _ExitTree()
        {
            if (_network != null)
            {
                _network.CombatStateNotify -= OnCombatStateNotify;
                _network.CombatEndNotify -= OnCombatEndNotify;
                _network.CastStartNotify -= OnCastStartNotify;
                _network.CombatEventNotify -= OnCombatEventNotify;
                _network.MonsterMoveCancelNotify -= OnMonsterMoveCancel;
                _network.MonsterDeathNotify -= OnMonsterDeathNotify;
                _network.MonsterRespawnNotify -= OnMonsterRespawnNotify;
            }
        }

        /// <summary>
        /// 浠庨厤缃枃浠跺姞杞芥牱寮忛厤缃紝纭繚 SpawnMonsters 鏃朵娇鐢ㄥ凡淇濆瓨鐨勫€?        /// </summary>
        private void LoadDefaultStyleConfig()
        {
            var config = new ConfigFile();
            if (config.Load("res://debug_panel_config.cfg") != Error.Ok)
            {
                // 没有配置文件，创建默认配置
                StyleConfigs[1] = EntityStyleConfig.CreateMonsterDefault();
                return;
            }

            // 璇诲彇鎵€鏈?[monster_*] sections
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

            // 杩佺Щ鏃?[monster] section
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
            cfg.VisualSizeScale = EntityProfileManager.FromFp(EntityProfileManager.ReadFp(config, section, "visual_size_scale", EntityProfileManager.ToFp(cfg.VisualSizeScale)));
            cfg.BorderWidthScale = EntityProfileManager.FromFp(EntityProfileManager.ReadFp(config, section, "border_width_scale", EntityProfileManager.ToFp(cfg.BorderWidthScale)));
            cfg.CornerRadius = (float)(double)config.GetValue(section, "corner_radius", cfg.CornerRadius);
            cfg.BgOpacity = EntityProfileManager.FromFp(EntityProfileManager.ReadFp(config, section, "bg_opacity", EntityProfileManager.ToFp(cfg.BgOpacity)));
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
                cfg.LabelTexts[i] = (string)config.GetValue(section, $"label_text_{i}", cfg.LabelTexts[i] ?? "");
                cfg.LabelFontSizes[i] = (int)(double)config.GetValue(section, $"label_font_size_{i}", 0);
                cfg.LabelXOffsets[i] = (float)(double)config.GetValue(section, $"label_x_offset_{i}", 0);
                cfg.LabelCenterX[i] = (bool)config.GetValue(section, $"label_center_x_{i}", true);
                cfg.LabelYOffsets[i] = (float)(double)config.GetValue(section, $"label_y_offset_{i}", 0);
            }

            cfg.HpBarLengthScale = EntityProfileManager.FromFp(EntityProfileManager.ReadFp(config, section, "hp_bar_length_scale", EntityProfileManager.ToFp(cfg.HpBarLengthScale)));
            cfg.HpBarHeightScale = EntityProfileManager.FromFp(EntityProfileManager.ReadFp(config, section, "hp_bar_height_scale", EntityProfileManager.ToFp(cfg.HpBarHeightScale)));
            cfg.HpBarFillPercent = EntityProfileManager.FromFp(EntityProfileManager.ReadFp(config, section, "hp_bar_fill_percent", EntityProfileManager.ToFp(cfg.HpBarFillPercent)));
            cfg.HpBarVisible = (bool)config.GetValue(section, "hp_bar_visible", cfg.HpBarVisible);
            cfg.HpBarCenterX = (bool)config.GetValue(section, "hp_bar_center_x", RoleControlCenterXResolver.ResolveInitialCenterX(null, cfg.HpBarOffsetX));
            cfg.HpBarOffsetX = (float)(double)config.GetValue(section, "hp_bar_offset_x", cfg.HpBarOffsetX);
            cfg.HpBarOffsetY = (float)(double)config.GetValue(section, "hp_bar_offset_y", cfg.HpBarOffsetY);
            float hpR = (float)(double)config.GetValue(section, "hp_bar_color_r", cfg.HpBarColor.R);
            float hpG = (float)(double)config.GetValue(section, "hp_bar_color_g", cfg.HpBarColor.G);
            float hpB = (float)(double)config.GetValue(section, "hp_bar_color_b", cfg.HpBarColor.B);
            cfg.HpBarColor = new Color(hpR, hpG, hpB);

            cfg.MpBarLengthScale = EntityProfileManager.FromFp(EntityProfileManager.ReadFp(config, section, "mp_bar_length_scale", EntityProfileManager.ToFp(cfg.MpBarLengthScale)));
            cfg.MpBarHeightScale = EntityProfileManager.FromFp(EntityProfileManager.ReadFp(config, section, "mp_bar_height_scale", EntityProfileManager.ToFp(cfg.MpBarHeightScale)));
            cfg.MpBarFillPercent = EntityProfileManager.FromFp(EntityProfileManager.ReadFp(config, section, "mp_bar_fill_percent", EntityProfileManager.ToFp(cfg.MpBarFillPercent)));
            cfg.MpBarVisible = (bool)config.GetValue(section, "mp_bar_visible", cfg.MpBarVisible);
            cfg.MpBarCenterX = (bool)config.GetValue(section, "mp_bar_center_x", RoleControlCenterXResolver.ResolveInitialCenterX(null, cfg.MpBarOffsetX));
            cfg.MpBarOffsetX = (float)(double)config.GetValue(section, "mp_bar_offset_x", cfg.MpBarOffsetX);
            cfg.MpBarOffsetY = (float)(double)config.GetValue(section, "mp_bar_offset_y", cfg.MpBarOffsetY);
            float mpR = (float)(double)config.GetValue(section, "mp_bar_color_r", cfg.MpBarColor.R);
            float mpG = (float)(double)config.GetValue(section, "mp_bar_color_g", cfg.MpBarColor.G);
            float mpB = (float)(double)config.GetValue(section, "mp_bar_color_b", cfg.MpBarColor.B);
            cfg.MpBarColor = new Color(mpR, mpG, mpB);

            // InteractMenuOffset (NPC 专用)
            cfg.InteractMenuOffsetAX = (float)(double)config.GetValue(section, "interact_menu_offset_ax", cfg.InteractMenuOffsetAX);
            cfg.InteractMenuOffsetAY = (float)(double)config.GetValue(section, "interact_menu_offset_ay", cfg.InteractMenuOffsetAY);
            cfg.InteractMenuOffsetBX = (float)(double)config.GetValue(section, "interact_menu_offset_bx", cfg.InteractMenuOffsetBX);
            cfg.InteractMenuOffsetBY = (float)(double)config.GetValue(section, "interact_menu_offset_by", cfg.InteractMenuOffsetBY);
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
            _monsterReservedPositions.Clear();

            _gridSize = gridSize;
            if (monsterData == null) return;

            foreach (var m in monsterData)
            {
                // 从本地配置查 ui_config_id，默认1
                int mid = (int)m.MonsterId;
                int uiConfigId = 1;
                string quality = "普通";
                var mcm = GetNodeOrNull<MonsterConfigManager>("/root/MonsterConfigManager");
                if (mcm != null)
                {
                    var def = mcm.Config.Monsters.Find(d => d.MonsterId == mid);
                    if (def != null)
                    {
                        uiConfigId = def.UiConfigId;
                        if (!string.IsNullOrWhiteSpace(def.Quality))
                            quality = def.Quality;
                    }
                }

                var monster = new Monster();
                monster.Setup(
                    m.InstanceId,
                    m.MonsterId,
                    m.X,
                    m.Y,
                    m.Name,
                    m.Level,
                    gridSize,
                    m.Attrs,
                    uiConfigId,
                    quality
                );
                monster.MoveVisualCompleted += OnMonsterMoveVisualCompleted;
                ApplyDefaultStyle(monster);
                monster.ProfileId = 2;
                AddChild(monster);
                // 创建后立即应用 Profile
                var pm = EntityProfileManager.Instance;
                if (pm != null) pm.ApplyProfile(monster, 2);
                else GD.PrintErr("[MonsterManager] EntityProfileManager.Instance is null, cannot apply profile");
                _monsters.Add(monster);
                _monsterPositions[m.InstanceId] = new Vector2I(m.X, m.Y);

                GD.Print($"[MonsterManager] Spawned monster {monster.InstanceId}({monster.MonsterName}) at ({monster.GridX},{monster.GridY})");
            }
        }

        public void ApplyDefaultStyle(Monster monster)
        {
            if (monster == null) return;
            var cfg = GetStyleConfig(monster.UiConfigId);
            monster.ApplyStyle(cfg);
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

        public void SetHealthBarVisibleAll(bool visible) { foreach (var m in _monsters) m.SetHealthBarVisible(visible); SyncStyleConfigFromEntity(); }
        public void SetHealthBarLengthScaleAll(float scale) { foreach (var m in _monsters) m.SetHealthBarLengthScale(scale); SyncStyleConfigFromEntity(); }
        public void SetHealthBarHeightScaleAll(float scale) { foreach (var m in _monsters) m.SetHealthBarHeightScale(scale); SyncStyleConfigFromEntity(); }
        public void SetHealthBarFillPercentAll(float percent) { foreach (var m in _monsters) m.SetHealthBarFillPercent(percent); SyncStyleConfigFromEntity(); }
        public void SetHealthBarColorAll(Color color) { foreach (var m in _monsters) m.SetHealthBarColor(color); SyncStyleConfigFromEntity(); }
        public void SetHealthBarOffsetAll(Vector2 offset) { foreach (var m in _monsters) m.SetHealthBarOffset(offset); SyncStyleConfigFromEntity(); }

        public void SetMpBarVisibleAll(bool visible) { foreach (var m in _monsters) m.SetMpBarVisible(visible); SyncStyleConfigFromEntity(); }
        public void SetMpBarLengthScaleAll(float scale) { foreach (var m in _monsters) m.SetMpBarLengthScale(scale); SyncStyleConfigFromEntity(); }
        public void SetMpBarHeightScaleAll(float scale) { foreach (var m in _monsters) m.SetMpBarHeightScale(scale); SyncStyleConfigFromEntity(); }
        public void SetMpBarFillPercentAll(float percent) { foreach (var m in _monsters) m.SetMpBarFillPercent(percent); SyncStyleConfigFromEntity(); }
        public void SetMpBarColorAll(Color color) { foreach (var m in _monsters) m.SetMpBarColor(color); SyncStyleConfigFromEntity(); }
        public void SetMpBarOffsetAll(Vector2 offset) { foreach (var m in _monsters) m.SetMpBarOffset(offset); SyncStyleConfigFromEntity(); }

        /// <summary>
        /// 灏嗗綋鍓嶆€墿瀹炰綋鐨勮瑙夊睘鎬у悓姝ュ洖鎵€鏈?StyleConfig
        /// </summary>
        private void SyncStyleConfigFromEntity()
        {
            if (_monsters.Count == 0) return;
            var first = _monsters[0];
            foreach (var kv in StyleConfigs)
            {
                kv.Value.SyncFromEntity(first);
            }
        }

        public Monster GetMonsterAt(Vector2I gridPos)
        {
            if (!_monsterPositions.Values.Any(p => p == gridPos) && !_monsterReservedPositions.Contains(gridPos))
                return null;
            foreach (var m in _monsters)
            {
                if (m.GridX == gridPos.X && m.GridY == gridPos.Y)
                    return m;
                if (m.PendingGridPos.HasValue && m.PendingGridPos.Value == gridPos)
                    return m;
            }
            return null;
        }

        public void OnMonsterMove(uint instanceId, Vector2I from, Vector2I to, string state, int durationMs)
        {
            var m = _monsters.Find(x => x.InstanceId == instanceId);
            if (m == null) return;

            // 清理旧的预约位置，防止怪物改变移动目标时残留过期数据
            if (m.PendingGridPos.HasValue)
                _monsterReservedPositions.Remove(m.PendingGridPos.Value);

            _monsterPositions[instanceId] = from;
            _monsterReservedPositions.Add(to);
            m.CurrentState = state;
            float durationSec = durationMs > 0 ? durationMs / 1000.0f : 0.15f;
            m.MoveTo(to, durationSec);
        }

        public bool IsBlockedByMonster(Vector2I gridPos)
        {
            return _monsterPositions.Values.Any(p => p == gridPos) || _monsterReservedPositions.Contains(gridPos);
        }

        private void OnCombatStateNotify(Game.CombatStateNotify notify)
        {
            if (notify.Units.Count == 0)
            {
                foreach (var m in _monsters)
                {
                    m.IsInCombat = false;
                    m.CastingSkill = "";
                    m.CastProgress = 0;
                    m.HealthBarFillPercent = 1.0f;
                    m.MpBarFillPercent = 1.0f;
                    m.AtbValue = 0f;
                    if (!m.IsMoving && IsCombatState(m.CurrentState))
                        m.CurrentState = "idle";
                    m.RefreshDataBoundLabels();
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
                    m.IsInCombat = true;
                    m.CastingSkill = unit.CastingSkill;
                    m.CastProgress = unit.CastProgress;
                    m.AtbValue = 0f;
                    if (unit.MaxHp > 0)
                    {
                        if (m.SyncHp((int)unit.Hp, (int)unit.MaxHp))
                            m.PlayHitEffect();
                    }
                    if (unit.MaxMp > 0)
                        m.MpBarFillPercent = (float)unit.Mp / unit.MaxMp;
                    if (!m.IsMoving)
                        m.CurrentState = ResolveCombatDisplayState(m, unit);
                }
                else
                {
                    m.IsInCombat = false;
                    m.CastingSkill = "";
                    m.CastProgress = 0;
                    m.AtbValue = 0f;
                    m.HealthBarFillPercent = 1.0f;
                    m.MpBarFillPercent = 1.0f;
                    if (!m.IsMoving && IsCombatState(m.CurrentState))
                        m.CurrentState = "idle";
                }
                m.RefreshDataBoundLabels();
                m.QueueRedraw();
            }
        }

        private void OnCombatEndNotify(Game.CombatEndNotify notify)
        {
            foreach (var id in notify.EntityIds)
            {
                var m = _monsters.Find(x => x.InstanceId == id);
                if (m == null) continue;
                m.IsInCombat = false;
                m.CastingSkill = "";
                m.CastProgress = 0;
                m.AtbValue = 0f;
                if (!m.IsMoving && IsCombatState(m.CurrentState))
                    m.CurrentState = "idle";
                m.RefreshDataBoundLabels();
                m.QueueRedraw();
            }
        }

        private void OnCastStartNotify(Game.CastStartNotify notify)
        {
            var m = _monsters.Find(x => x.InstanceId == notify.CasterId);
            if (m == null) return;
            m.CastingSkill = SkillDataUtil.GetName((uint)notify.SkillId) ?? $"Skill{notify.SkillId}";
            m.CastProgress = 0f;
            m.QueueRedraw();
        }

        private void OnCombatEventNotify(Game.CombatEventNotify notify)
        {
            var m = _monsters.Find(x => x.InstanceId == notify.TargetId);
            if (m == null) return;

            if (notify.HpDelta != 0 && m.CurrentMaxHp > 0)
            {
                int newHp = Mathf.Clamp(m.CurrentHp + notify.HpDelta, 0, m.CurrentMaxHp);
                if (m.SyncHp(newHp, m.CurrentMaxHp))
                    m.PlayHitEffect();
            }
            m.QueueRedraw();
        }

        private void OnMonsterDeathNotify(Game.MonsterDeathNotify notify)
        {
            var m = _monsters.Find(x => x.InstanceId == notify.InstanceId);
            if (m == null) return;

            _monsterPositions.Remove(notify.InstanceId);
            if (m.PendingGridPos.HasValue)
                _monsterReservedPositions.Remove(m.PendingGridPos.Value);
            _monsters.Remove(m);

            m.PlayDeathAnimation(DeathEffectMode, DeathFadeDuration, DeathGrayDelay, () =>
            {
                m.QueueFree();
            });
        }

        private void OnMonsterRespawnNotify(Game.MonsterRespawnNotify notify)
        {
            // 如果已存在同 instanceId 的怪物（异常情况），先移除
            var existing = _monsters.Find(x => x.InstanceId == notify.InstanceId);
            if (existing != null)
            {
                _monsterPositions.Remove(notify.InstanceId);
                _monsters.Remove(existing);
                existing.QueueFree();
            }

            // 从本地配置查 ui_config_id 和品质
            int mid = (int)notify.MonsterId;
            int uiConfigId = 1;
            string quality = "普通";
            var mcm = GetNodeOrNull<MonsterConfigManager>("/root/MonsterConfigManager");
            if (mcm != null)
            {
                var def = mcm.Config.Monsters.Find(d => d.MonsterId == mid);
                if (def != null)
                {
                    uiConfigId = def.UiConfigId;
                    if (!string.IsNullOrWhiteSpace(def.Quality))
                        quality = def.Quality;
                }
            }

            var monster = new Monster();
            monster.Setup(notify.InstanceId, notify.MonsterId, notify.X, notify.Y, notify.Name, notify.Level, _gridSize, uiConfigId);
            monster.MonsterQuality = quality;
            monster.RefreshDataBoundLabels();
            monster.MoveVisualCompleted += OnMonsterMoveVisualCompleted;
            ApplyDefaultStyle(monster);
            monster.ProfileId = 2;
            AddChild(monster);
            var pm = EntityProfileManager.Instance;
            if (pm != null) pm.ApplyProfile(monster, 2);
            else GD.PrintErr("[MonsterManager] EntityProfileManager.Instance is null, cannot apply profile");
            _monsters.Add(monster);
            _monsterPositions[notify.InstanceId] = new Vector2I(notify.X, notify.Y);

            GD.Print($"[MonsterManager] Respawned monster {monster.InstanceId}({monster.MonsterName}) at ({monster.GridX},{monster.GridY})");
        }

        public void OnMonsterMoveCancel(Game.MonsterMoveCancelNotify notify)
        {
            var m = _monsters.Find(x => x.InstanceId == notify.InstanceId);
            if (m == null) return;

            var rollbackPos = new Vector2I(notify.RollbackX, notify.RollbackY);
            if (m.PendingGridPos.HasValue)
            {
                _monsterReservedPositions.Remove(m.PendingGridPos.Value);
            }
            _monsterPositions[notify.InstanceId] = rollbackPos;

            // Keep the rollback smooth while the monster is already moving.
            if (m.IsMoving)
                m.PlayBounceBack(rollbackPos);
            else
                m.RollbackTo(rollbackPos);

            GD.Print($"[MonsterManager] Monster {m.InstanceId}({m.MonsterName}) move cancelled, rolled back to ({rollbackPos.X},{rollbackPos.Y})");
        }

        private void OnMonsterMoveVisualCompleted(Monster monster, Vector2I fromGridPos, Vector2I targetGridPos)
        {
            if (monster == null)
                return;

            _monsterPositions[monster.InstanceId] = targetGridPos;
            _monsterReservedPositions.Remove(targetGridPos);
        }

        private static bool IsCombatState(string state)
        {
            return !string.IsNullOrWhiteSpace(state) &&
                   state.StartsWith("combat", System.StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveCombatDisplayState(Monster monster, Game.CombatStateNotify.Types.CombatUnit unit)
        {
            bool isCasting = !string.IsNullOrWhiteSpace(unit.CastingSkill) || unit.CastProgress > 0f;
            string current = monster.CurrentState ?? "";

            if (isCasting)
                return "combat_cast_hold";

            if (current.StartsWith("combat_ranged", System.StringComparison.OrdinalIgnoreCase))
                return "combat_ranged_hold";

            if (current.StartsWith("combat_cast", System.StringComparison.OrdinalIgnoreCase))
                return "combat_cast_hold";

            return "combat_hold";
        }
    }
}
