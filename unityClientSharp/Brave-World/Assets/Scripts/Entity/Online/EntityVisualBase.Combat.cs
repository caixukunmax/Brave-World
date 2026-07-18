using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 实体战斗表现 — 读条/受击反馈/攻击前冲/战斗光环。
    /// 移植自 Godot EntityBase 的战斗视觉部分（PlayHitEffect/PlayAttackShake/CombatAuraComponent）。
    /// 受击与攻击协程独立于移动 tween（铁律②不适用；抖动仅非移动时触发，与 Godot 一致）。
    /// </summary>
    public partial class EntityVisualBase
    {
        // 受击/攻击常量（对齐 Godot EntityBase 全局静态参数）
        public const float HitEffectDuration = 0.12f;
        public const float HitFlashIntensity = 0.3f;
        public const float HitShakeAmplitude = 3.0f;
        public const float AttackShakeDistance = 12f;
        public const float AttackShakeDuration = 0.08f;

        private SpriteRenderer _castBg, _castFill;
        private float _castLen;
        private GameObject _castBarRoot;
        private GameObject _auraGo;

        private Coroutine _hitFlashRoutine, _hitShakeRoutine, _attackShakeRoutine;
        private static readonly Dictionary<int, Sprite> _auraCache = new();

        // ============ 读条（CastBar）============

        public void SetCastFill(float percent)
        {
            if (_castFill == null) return;
            float p = Mathf.Clamp01(percent);
            _castFill.size = new Vector2(_castLen * p, _castFill.size.y);
            _castFill.transform.localPosition = new Vector3(-_castLen / 2f + _castLen * p / 2f, 0, 0);
        }

        public void SetCastBarVisible(bool visible)
        {
            if (_castBarRoot != null && _castBarRoot.activeSelf != visible)
                _castBarRoot.SetActive(visible);
        }

        // ============ 战斗光环 ============

        public void SetCombatAuraVisible(bool visible)
        {
            if (visible && _auraGo == null)
                BuildAura();
            if (_auraGo != null && _auraGo.activeSelf != visible)
                _auraGo.SetActive(visible);
        }

        private void BuildAura()
        {
            // 半径 = VisualOuterSize × 0.55（对齐 Godot CombatAuraComponent）
            int outerRef = Mathf.Clamp(_gridSize * Mathf.Max(1, SizeX), 10, _gridSize * Mathf.Max(1, SizeX));
            int radius = Mathf.RoundToInt(outerRef * 0.55f);

            _auraGo = new GameObject("CombatAura");
            _auraGo.transform.SetParent(transform, false);
            var sr = _auraGo.AddComponent<SpriteRenderer>();
            sr.sprite = GetAuraSprite(radius);
            sr.sortingOrder = _sortingOrder - 1; // 身体下层（对齐 DrawOrder=-10）
            _auraGo.SetActive(false);
        }

        /// <summary>光环贴图：外圈 (1,0.15,0.15,0.35) + 内芯 ×0.6 (1,0.3,0.2,0.25)，按半径缓存。</summary>
        private static Sprite GetAuraSprite(int radius)
        {
            if (_auraCache.TryGetValue(radius, out var cached) && cached != null)
                return cached;

            int size = radius * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"CombatAura_{radius}",
            };
            float center = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                    Color c = Color.clear;
                    // 外圈环带（半径 r 附近 ~2px 环）
                    if (d <= radius && d > radius * 0.6f)
                    {
                        float edge = Mathf.Min(radius - d, d - radius * 0.6f);
                        float a = Mathf.Clamp01(edge) * 0.35f;
                        c = new Color(1f, 0.15f, 0.15f, a);
                    }
                    // 内芯
                    else if (d <= radius * 0.6f)
                    {
                        float a = Mathf.Clamp01(radius * 0.6f - d) * 0.25f;
                        c = new Color(1f, 0.3f, 0.2f, a);
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 1f);
            _auraCache[radius] = sprite;
            return sprite;
        }

        // ============ 受击反馈 ============

        /// <summary>掉血反馈：红闪 + （非移动时）抖动。由 SyncHp 下降触发（对齐 Godot PlayHitEffect）。</summary>
        public void PlayHitEffect()
        {
            PlayHitFlash();
            if (!IsMoving)
                PlayHitShake();
        }

        private void PlayHitFlash()
        {
            if (_hitFlashRoutine != null)
                StopCoroutine(_hitFlashRoutine);
            _hitFlashRoutine = StartCoroutine(HitFlashRoutine());
        }

        private IEnumerator HitFlashRoutine()
        {
            // 对齐 Godot：0.12s×0.3 红 (1,0.3,0.3)，再 ×0.7 回白
            var body = GetBodyRenderer();
            if (body != null)
                body.color = new Color(1f, HitFlashIntensity, HitFlashIntensity);
            yield return new WaitForSeconds(HitEffectDuration * 0.3f);
            if (body != null)
                body.color = Color.white;
            yield return new WaitForSeconds(HitEffectDuration * 0.7f);
            _hitFlashRoutine = null;
        }

        private void PlayHitShake()
        {
            if (_hitShakeRoutine != null)
                StopCoroutine(_hitShakeRoutine);
            _hitShakeRoutine = StartCoroutine(HitShakeRoutine());
        }

        private IEnumerator HitShakeRoutine()
        {
            // 对齐 Godot：X 向 ±3px、±1.5px、回位，共 5 段各 0.02s
            Vector3 basePos = transform.position;
            float[] offsets = { HitShakeAmplitude, -HitShakeAmplitude, HitShakeAmplitude * 0.5f, -HitShakeAmplitude * 0.5f, 0f };
            foreach (var ox in offsets)
            {
                transform.position = basePos + new Vector3(ox, 0, 0);
                yield return new WaitForSeconds(0.02f);
            }
            transform.position = basePos;
            _hitShakeRoutine = null;
        }

        // ============ 攻击前冲 ============

        /// <summary>攻击者前冲：朝目标格方向冲 12px（0.08s×0.4 Quad/Out），×0.6 弹回（对齐 Godot PlayAttackShake）。</summary>
        public void PlayAttackShake(Vector2Int? targetPos = null)
        {
            if (_attackShakeRoutine != null)
                StopCoroutine(_attackShakeRoutine);
            _attackShakeRoutine = StartCoroutine(AttackShakeRoutine(targetPos));
        }

        private IEnumerator AttackShakeRoutine(Vector2Int? targetPos)
        {
            Vector3 basePos = transform.position;
            Vector3 dir;
            if (targetPos.HasValue)
            {
                dir = PositionForGridPos(targetPos.Value) - basePos;
                dir.z = 0;
            }
            else
            {
                dir = Vector3.right; // 默认向右（对齐 Godot）
            }
            if (dir.magnitude < 0.001f) dir = Vector3.right;
            dir.Normalize();

            Vector3 front = basePos + dir * AttackShakeDistance;
            float t = 0f;
            float d1 = AttackShakeDuration * 0.4f, d2 = AttackShakeDuration * 0.6f;
            while (t < d1)
            {
                t += Time.deltaTime;
                transform.position = Vector3.LerpUnclamped(basePos, front, SimpleTween.Evaluate(SimpleTween.Ease.QuadOut, t / d1));
                yield return null;
            }
            t = 0f;
            while (t < d2)
            {
                t += Time.deltaTime;
                transform.position = Vector3.LerpUnclamped(front, basePos, SimpleTween.Evaluate(SimpleTween.Ease.QuadOut, t / d2));
                yield return null;
            }
            transform.position = basePos;
            _attackShakeRoutine = null;
        }

        private SpriteRenderer GetBodyRenderer()
        {
            var t = transform.Find("Body");
            return t != null ? t.GetComponent<SpriteRenderer>() : null;
        }
    }
}
