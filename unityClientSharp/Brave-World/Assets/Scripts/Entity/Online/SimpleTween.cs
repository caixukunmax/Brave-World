using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 缓动函数库 — 与 Godot Tween 的 TransitionType 一一对应（端点精确 0/1）。
    /// 移动/回弹/矫正动画统一只用这里的缓动（AGENTS.md 移动铁律②）。
    /// </summary>
    public static class SimpleTween
    {
        public enum Ease
        {
            Linear,
            QuadOut,
            QuadIn,
            SineOut,
            SineIn,
            CubicIn,
        }

        public static float Evaluate(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            return ease switch
            {
                Ease.Linear => t,
                Ease.QuadOut => 1f - (1f - t) * (1f - t),
                Ease.QuadIn => t * t,
                Ease.SineOut => Mathf.Sin(t * Mathf.PI / 2f),
                Ease.SineIn => 1f - Mathf.Cos(t * Mathf.PI / 2f),
                Ease.CubicIn => t * t * t,
                _ => t,
            };
        }
    }
}
