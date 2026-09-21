using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BubblePics
{
    public enum Ease
    {
        Linear, InQuad, OutQuad, InOutQuad, InCubic, OutCubic, InOutCubic,
        OutBack, InBack, OutElastic, OutBounce, InSine, OutSine, InOutSine, OutQuart, InQuart
    }

    /// <summary>Small self-contained tween engine (no external packages).</summary>
    public static class Tween
    {
        public static float Evaluate(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case Ease.Linear: return t;
                case Ease.InQuad: return t * t;
                case Ease.OutQuad: return 1 - (1 - t) * (1 - t);
                case Ease.InOutQuad: return t < 0.5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;
                case Ease.InCubic: return t * t * t;
                case Ease.OutCubic: return 1 - Mathf.Pow(1 - t, 3);
                case Ease.InOutCubic: return t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
                case Ease.InQuart: return t * t * t * t;
                case Ease.OutQuart: return 1 - Mathf.Pow(1 - t, 4);
                case Ease.InSine: return 1 - Mathf.Cos(t * Mathf.PI / 2);
                case Ease.OutSine: return Mathf.Sin(t * Mathf.PI / 2);
                case Ease.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1) / 2;
                case Ease.OutBack:
                {
                    const float c1 = 1.70158f, c3 = c1 + 1;
                    return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
                }
                case Ease.InBack:
                {
                    const float c1 = 1.70158f, c3 = c1 + 1;
                    return c3 * t * t * t - c1 * t * t;
                }
                case Ease.OutElastic:
                {
                    const float c4 = 2 * Mathf.PI / 3;
                    if (t <= 0) return 0;
                    if (t >= 1) return 1;
                    return Mathf.Pow(2, -10 * t) * Mathf.Sin((t * 10 - 0.75f) * c4) + 1;
                }
                case Ease.OutBounce:
                {
                    const float n1 = 7.5625f, d1 = 2.75f;
                    if (t < 1 / d1) return n1 * t * t;
                    if (t < 2 / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
                    if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
                    t -= 2.625f / d1; return n1 * t * t + 0.984375f;
                }
                default: return t;
            }
        }

        public static IEnumerator Run(float duration, Action<float> step, Ease ease = Ease.Linear, Action done = null)
        {
            if (duration <= 0f)
            {
                step?.Invoke(1f);
                done?.Invoke();
                yield break;
            }
            float t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                step?.Invoke(Evaluate(ease, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            step?.Invoke(1f);
            done?.Invoke();
        }

        /// <summary>World-space move.</summary>
        public static IEnumerator Move(Transform tr, Vector3 to, float duration, Ease ease = Ease.OutQuad, Action done = null)
        {
            Vector3 from = tr.position;
            yield return Run(duration, k => { if (tr != null) tr.position = Vector3.LerpUnclamped(from, to, k); }, ease);
            done?.Invoke();
        }

        public static IEnumerator MoveLocal(Transform tr, Vector3 to, float duration, Ease ease = Ease.OutQuad, Action done = null)
        {
            Vector3 from = tr.localPosition;
            yield return Run(duration, k => { if (tr != null) tr.localPosition = Vector3.LerpUnclamped(from, to, k); }, ease);
            done?.Invoke();
        }

        public static IEnumerator Scale(Transform tr, Vector3 to, float duration, Ease ease = Ease.OutQuad, Action done = null)
        {
            Vector3 from = tr.localScale;
            yield return Run(duration, k => { if (tr != null) tr.localScale = Vector3.LerpUnclamped(from, to, k); }, ease);
            done?.Invoke();
        }

        public static IEnumerator Fade(CanvasGroup cg, float to, float duration, Ease ease = Ease.Linear, Action done = null)
        {
            float from = cg.alpha;
            yield return Run(duration, k => { if (cg != null) cg.alpha = Mathf.LerpUnclamped(from, to, k); }, ease);
            done?.Invoke();
        }

        public static IEnumerator FadeSprite(SpriteRenderer sr, float to, float duration, Ease ease = Ease.Linear)
        {
            float from = sr != null ? sr.color.a : 0f;
            yield return Run(duration, k =>
            {
                if (sr != null)
                {
                    var c = sr.color; c.a = Mathf.LerpUnclamped(from, to, k); sr.color = c;
                }
            }, ease);
        }

        /// <summary>Button-style press pulse: shrink then overshoot back (Godot common_button).</summary>
        public static IEnumerator PressPulse(Transform tr, float baseScale = 1f)
        {
            // 4/60s to 0.9, 8/60s to 1.03, 10/60s back to 1.0
            yield return Scale(tr, Vector3.one * (0.9f * baseScale), 4f / 60f, Ease.OutQuad);
            yield return Scale(tr, Vector3.one * (1.03f * baseScale), 8f / 60f, Ease.OutQuad);
            yield return Scale(tr, Vector3.one * (1.0f * baseScale), 10f / 60f, Ease.OutQuad);
        }
    }

    public static class EaseExt
    {
        public static float Evaluate(this Ease ease, float t) => Tween.Evaluate(ease, t);
    }

    /// <summary>Runs coroutines from non-MonoBehaviour contexts and provides timers.</summary>
    public class TweenRunner : MonoBehaviour
    {
        static TweenRunner _inst;
        public static TweenRunner I
        {
            get
            {
                if (_inst == null)
                {
                    var go = new GameObject("~TweenRunner");
                    DontDestroyOnLoad(go);
                    _inst = go.AddComponent<TweenRunner>();
                }
                return _inst;
            }
        }

        public static Coroutine Go(IEnumerator routine) => I.StartCoroutine(routine);

        public static Coroutine Delay(float seconds, Action act) => I.StartCoroutine(DelayCo(seconds, act));

        static IEnumerator DelayCo(float seconds, Action act)
        {
            yield return new WaitForSeconds(seconds);
            act?.Invoke();
        }
    }
}
