using System.Collections;
using TMPro;
using UnityClientSharp.Map.Core;
using UnityClientSharp.Map.Rendering;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 在线实体视觉基类：身体（程序生成圆角色块）+ 标签行（行数随 Profile 配置）+ HP/MP 条 + 死亡淡出。
    /// 移植自 Godot EntityBase 的显示部分（AppearanceComponent/LabelComponent/HealthBar/MpBar）。
    /// 玩家/怪物/NPC 继承；宝箱/掉落不是 EntityBase（Godot 亦然），不继承本类。
    /// </summary>
    public partial class EntityVisualBase : MonoBehaviour
    {
        public int ProfileId { get; protected set; }
        /// <summary>footprint 中心格子（与 Godot EntityBase.GridPos 语义一致）</summary>
        public Vector2Int GridPos { get; protected set; }
        public int SizeX { get; protected set; } = 1;
        public int SizeY { get; protected set; } = 1;
        /// <summary>朝向: 0=右, 1=下, 2=左, 3=上（仅存字段，方向箭头不做）</summary>
        public int Direction { get; set; } = 1;

        /// <summary>从移动向量推导 4 方向索引（对齐 Godot EntityBase.DirectionFromVector）。</summary>
        public static int DirectionFromVector(Vector2Int delta)
        {
            if (delta.x > 0) return 0;
            if (delta.x < 0) return 2;
            if (delta.y > 0) return 1;
            if (delta.y < 0) return 3;
            return 1;
        }

        protected int _gridSize = 111;
        protected int _sortingOrder = 2;

        private static Sprite s_unitSprite;
        private SpriteRenderer _body;
        private TextMeshPro[] _labels = new TextMeshPro[LabelGroupData.DefaultRowCount];
        private TextMeshPro[] _labelShadows = new TextMeshPro[LabelGroupData.DefaultRowCount];
        private SpriteRenderer _hpBg, _hpFill, _mpBg, _mpFill;
        private float _hpLen, _mpLen;
        private bool _dying;

        /// <summary>1x1 白图（条底/填充/描边/光晕共用，按 scale 拉伸）</summary>
        public static Sprite UnitSprite
        {
            get
            {
                if (s_unitSprite == null)
                {
                    var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    s_unitSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                    s_unitSprite.name = "UnitWhite";
                }
                return s_unitSprite;
            }
        }

        /// <summary>footprint 中心格 → 世界位置（移植 Godot EntityBase.GetWorldPositionForGridPos 的中心锚点语义）。</summary>
        protected Vector3 PositionForGridPos(Vector2Int gridPos)
        {
            var anchor = new Vector2Int(
                gridPos.x - (Mathf.Max(1, SizeX) - 1) / 2,
                gridPos.y - (Mathf.Max(1, SizeY) - 1) / 2);
            return GridMath.FootprintCenterWorld(anchor.x, anchor.y, SizeX, SizeY, _gridSize);
        }

        /// <summary>从 Profile 应用外观并定位。sizeX/sizeY 来自服务器数据（&gt;0 优先于 Profile）。</summary>
        protected virtual void Setup(int profileId, Vector2Int gridPos, int gridSize, int sizeX = 1, int sizeY = 1, int sortingOrder = 2)
        {
            ProfileId = profileId;
            GridPos = gridPos;
            _gridSize = gridSize;
            _sortingOrder = sortingOrder;
            SizeX = Mathf.Max(1, sizeX);
            SizeY = Mathf.Max(1, sizeY);

            var profile = EntityProfileManager.GetProfile(profileId);
            var app = profile?.GetData<AppearanceData>("appearance");
            transform.localPosition = PositionForGridPos(gridPos);

            BuildBody(app);
            BuildLabels(profile, app);
            BuildBars(profile, app);
        }

        private void BuildBody(AppearanceData app)
        {
            // 尺寸公式统一走 EntityAppearanceLayout（与编辑器预览/地图装饰一致）
            EntityAppearanceLayout.ComputeBody(_gridSize, SizeX, SizeY,
                app?.VisualSizeScale ?? 1.0f, app?.BorderWidthScale ?? EntityAppearanceLayout.DefaultBorderWidthScale,
                out int outerW, out int outerH, out int border);

            var go = new GameObject("Body");
            go.transform.SetParent(transform, false);
            _body = go.AddComponent<SpriteRenderer>();
            _body.sprite = EntityBodySprite.Get(outerW, outerH, border, app?.CornerRadius ?? 12f,
                app?.BorderColor ?? Color.white, app?.BgColor ?? Color.white, app?.BgOpacity ?? 0.9f);
            _body.sortingOrder = _sortingOrder;
        }

        private void BuildLabels(EntityProfile profile, AppearanceData app)
        {
            // 「停用」语义与调试面板预览一致：labels 组件停用时不生成任何标签
            if (profile == null || profile.IsComponentDisabled("labels"))
            {
                _labels = new TextMeshPro[0];
                _labelShadows = new TextMeshPro[0];
                return;
            }
            var labels = profile.GetData<LabelGroupData>("labels");
            if (labels == null) return;
            _labels = new TextMeshPro[labels.Count];
            _labelShadows = new TextMeshPro[labels.Count];

            for (int i = 0; i < labels.Count; i++)
            {
                if (!labels.Visible[i]) continue;
                // 字号/颜色/样式统一走 EntityAppearanceLayout（与编辑器预览/地图装饰一致）
                int fs = EntityAppearanceLayout.RowFontSize(_gridSize, labels, i,
                    app?.VisualSizeScale ?? 1.0f, app?.BorderWidthScale ?? EntityAppearanceLayout.DefaultBorderWidthScale);
                var go = new GameObject($"Label{i}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(labels.CenterX[i] ? 0 : labels.XOffset[i],
                    EntityAppearanceLayout.LineWorldOffsetY(i, fs, labels.YOffset[i], labels.Count), 0);
                var tmp = go.AddComponent<TextMeshPro>();
                FontUtil.SetWorldFontSize(tmp, fs);
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = EntityAppearanceLayout.RowFontStyle(labels);
                tmp.color = EntityAppearanceLayout.RowTextColor(labels, i);
                FontUtil.ApplyCjkFont(tmp);
                int order = _sortingOrder + 4; // 条=+2，阴影=+3，主标签=+4
                tmp.GetComponent<MeshRenderer>().sortingOrder = order;
                _labels[i] = tmp;
                if (labels.Shadow) _labelShadows[i] = EntityLabelShadow.Create(tmp, $"Label{i}Shadow", order - 1);
            }
        }

        private void BuildBars(EntityProfile profile, AppearanceData app)
        {
            int outerRef = EntityAppearanceLayout.ComputeOuterRef(_gridSize, app?.VisualSizeScale ?? 1.0f);
            var hp = profile?.GetData<BarData>("healthbar");
            var mp = profile?.GetData<BarData>("mpbar");
            if (hp != null)
            {
                _hpLen = Mathf.Clamp(outerRef * hp.LengthScale, 10, _gridSize * 2);
                float h = Mathf.Clamp(_gridSize * hp.HeightScale, 2, _gridSize);
                (_hpBg, _hpFill) = CreateBar("HpBar", hp, _hpLen, h);
                _hpBarRoot = _hpBg.transform.parent.gameObject;
                _hpBarRoot.SetActive(hp.Visible); // NPC 等先建后藏，战斗中再显示
            }
            if (mp != null)
            {
                _mpLen = Mathf.Clamp(outerRef * mp.LengthScale, 10, _gridSize * 2);
                float h = Mathf.Clamp(_gridSize * mp.HeightScale, 2, _gridSize);
                (_mpBg, _mpFill) = CreateBar("MpBar", mp, _mpLen, h);
                _mpBarRoot = _mpBg.transform.parent.gameObject;
                _mpBarRoot.SetActive(mp.Visible);
            }
            var cast = profile?.GetData<BarData>("castbar");
            if (cast != null)
            {
                _castLen = Mathf.Clamp(outerRef * cast.LengthScale, 10, _gridSize * 2);
                float h = Mathf.Clamp(_gridSize * cast.HeightScale, 2, _gridSize);
                (_castBg, _castFill) = CreateBar("CastBar", cast, _castLen, h);
                _castBarRoot = _castBg.transform.parent.gameObject;
                _castBarRoot.SetActive(false); // 读条默认隐藏（施法时才显示）
            }
        }

        private GameObject _hpBarRoot, _mpBarRoot;

        public void SetHpBarVisible(bool visible)
        {
            if (_hpBarRoot != null) _hpBarRoot.SetActive(visible);
        }

        public void SetMpBarVisible(bool visible)
        {
            if (_mpBarRoot != null) _mpBarRoot.SetActive(visible);
        }

        private (SpriteRenderer bg, SpriteRenderer fill) CreateBar(string name, BarData bar, float length, float height)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            // Godot 条中心 offset（y 向下为负 = 上方）；世界 y 取负
            root.transform.localPosition = new Vector3(bar.OffsetX, -bar.OffsetY, 0);

            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(root.transform, false);
            var bg = bgGo.AddComponent<SpriteRenderer>();
            bg.sprite = UnitSprite;
            bg.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            bg.drawMode = SpriteDrawMode.Sliced;
            bg.size = new Vector2(length, height);
            bg.sortingOrder = _sortingOrder + 2;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(root.transform, false);
            var fill = fillGo.AddComponent<SpriteRenderer>();
            fill.sprite = UnitSprite;
            fill.color = bar.Color;
            fill.drawMode = SpriteDrawMode.Sliced;
            fill.size = new Vector2(length * Mathf.Clamp01(bar.FillPercent), height);
            fill.sortingOrder = _sortingOrder + 2;
            // 填充左对齐（pivot 居中，故置于左缘+半宽处）
            fillGo.transform.localPosition = new Vector3(-length / 2f + length * Mathf.Clamp01(bar.FillPercent) / 2f, 0, 0);

            return (bg, fill);
        }

        public void SetLabel(int index, string text)
        {
            if (index < 0 || index >= _labels.Length) return;
            if (_labels[index] != null) _labels[index].text = text ?? "";
            // 阴影是独立文本副本，必须同步（EntityLabelShadow 注释）
            if (index < _labelShadows.Length && _labelShadows[index] != null) _labelShadows[index].text = text ?? "";
        }

        public void SetLabelColor(int index, Color color)
        {
            if (index < 0 || index >= _labels.Length) return;
            if (_labels[index] != null) _labels[index].color = color;
        }

        /// <summary>血条填充（0~1）。返回是否掉血（供后续受击特效）。</summary>
        public bool SetHpFill(float percent)
        {
            if (_hpFill == null) return false;
            float old = _hpFill.size.x / Mathf.Max(1e-5f, _hpLen);
            float p = Mathf.Clamp01(percent);
            _hpFill.size = new Vector2(_hpLen * p, _hpFill.size.y);
            _hpFill.transform.localPosition = new Vector3(-_hpLen / 2f + _hpLen * p / 2f, 0, 0);
            return p < old - 1e-4f;
        }

        public void SetMpFill(float percent)
        {
            if (_mpFill == null) return;
            float p = Mathf.Clamp01(percent);
            _mpFill.size = new Vector2(_mpLen * p, _mpFill.size.y);
            _mpFill.transform.localPosition = new Vector3(-_mpLen / 2f + _mpLen * p / 2f, 0, 0);
        }

        /// <summary>
        /// 按当前 ProfileId 重新应用配置外观（ProfileRefreshUtil.RefreshAll / 调试面板手动同步用）。
        /// 重建 Body/标签/HP/MP/Cast 条，保留运行时状态（标签文本、血条填充与可见性）与运行时视觉件（如 CombatAura）。
        /// </summary>
        public virtual void RefreshFromProfile()
        {
            if (_dying) return;

            // 1) 记录运行时状态
            var labelTexts = new string[_labels.Length];
            var labelColors = new Color[_labels.Length];
            for (int i = 0; i < _labels.Length; i++)
            {
                labelTexts[i] = _labels[i] != null ? _labels[i].text : null;
                labelColors[i] = _labels[i] != null ? _labels[i].color : Color.white;
            }
            float hpFill = _hpFill != null ? _hpFill.size.x / Mathf.Max(1e-5f, _hpLen) : -1f;
            float mpFill = _mpFill != null ? _mpFill.size.x / Mathf.Max(1e-5f, _mpLen) : -1f;
            bool hpVis = _hpBarRoot != null && _hpBarRoot.activeSelf;
            bool mpVis = _mpBarRoot != null && _mpBarRoot.activeSelf;

            // 2) 销毁 Profile 驱动的视觉子物体（保留 CombatAura 等运行时件）
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                string n = transform.GetChild(i).name;
                if (n == "Body" || n.StartsWith("Label") || n == "HpBar" || n == "MpBar" || n == "CastBar")
                    Destroy(transform.GetChild(i).gameObject);
            }
            _body = null;
            for (int i = 0; i < _labels.Length; i++) _labels[i] = null;
            for (int i = 0; i < _labelShadows.Length; i++) _labelShadows[i] = null;
            _hpBg = _hpFill = _mpBg = _mpFill = null;
            _hpBarRoot = _mpBarRoot = null;
            _castBg = _castFill = null;
            _castBarRoot = null;

            // 3) 按当前 Profile 重建（Setup 会重读配置并重新定位）
            Setup(ProfileId, GridPos, _gridSize, SizeX, SizeY, _sortingOrder);

            // 4) 恢复运行时状态（不重建的项保持 null，SetHpFill/SetMpFill 内部有判空）
            // 行数可能因配置增删而变化，只恢复两边都存在的下标
            int restoreCount = System.Math.Min(labelTexts.Length, _labels.Length);
            for (int i = 0; i < restoreCount; i++)
            {
                if (labelTexts[i] != null && _labels[i] != null)
                {
                    _labels[i].text = labelTexts[i];
                    _labels[i].color = labelColors[i];
                    // 阴影文本同步恢复（颜色保持阴影色，不跟随标签色）
                    if (i < _labelShadows.Length && _labelShadows[i] != null)
                        _labelShadows[i].text = labelTexts[i];
                }
            }
            if (hpFill >= 0f) SetHpFill(hpFill);
            if (mpFill >= 0f) SetMpFill(mpFill);
            if (_hpBarRoot != null) _hpBarRoot.SetActive(hpVis);
            if (_mpBarRoot != null) _mpBarRoot.SetActive(mpVis);
        }

        /// <summary>死亡淡出：缩放+透明到 0 后销毁（对齐 Godot 怪物 DeathEffectMode 1）。</summary>
        public void PlayDeathFade(float duration = 0.5f)
        {
            if (_dying) return;
            _dying = true;
            StartCoroutine(DeathFadeRoutine(duration));
        }

        private IEnumerator DeathFadeRoutine(float duration)
        {
            float t = 0f;
            var renderers = GetComponentsInChildren<SpriteRenderer>();
            var tmps = GetComponentsInChildren<TextMeshPro>();
            var startColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) startColors[i] = renderers[i].color;
            var startTmpColors = new Color[tmps.Length];
            for (int i = 0; i < tmps.Length; i++) startTmpColors[i] = tmps[i].color;
            var startScale = transform.localScale;

            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float a = 1f - k;
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null) renderers[i].color = new Color(startColors[i].r, startColors[i].g, startColors[i].b, startColors[i].a * a);
                for (int i = 0; i < tmps.Length; i++)
                    if (tmps[i] != null) tmps[i].color = new Color(startTmpColors[i].r, startTmpColors[i].g, startTmpColors[i].b, startTmpColors[i].a * a);
                transform.localScale = startScale * (1f - k);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
