using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CozySudoku
{
    public enum Ease { Linear, OutQuad, OutCubic, InCubic, InOutCubic, OutBack, OutElastic }

    /// <summary>
    /// Küçük, bağımlılıksız tween sistemi. Aynı hedef+kanal için yeni tween eskisini iptal eder.
    /// Zaman ölçeğinden bağımsızdır (unscaled).
    /// </summary>
    public static class Tween
    {
        sealed class Entry { public Coroutine Co; }

        static readonly Dictionary<long, Entry> running = new Dictionary<long, Entry>();

        public static int ActiveCount => running.Count;

        public static float Evaluate(Ease ease, float t)
        {
            switch (ease)
            {
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.OutCubic: { float u = 1f - t; return 1f - u * u * u; }
                case Ease.InCubic: return t * t * t;
                case Ease.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
                case Ease.OutBack:
                {
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    float u = t - 1f;
                    return 1f + c3 * u * u * u + c1 * u * u;
                }
                case Ease.OutElastic:
                {
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    const float c4 = (2f * Mathf.PI) / 3f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
                }
                default: return t;
            }
        }

        static long Key(UnityEngine.Object target, int channel) => ((long)target.GetInstanceID() << 4) | (uint)channel;

        /// <summary>Genel amaçlı tween: 0→1 ilerlemeyi <paramref name="apply"/>'a verir.</summary>
        public static void Run(UnityEngine.Object target, int channel, float duration, Ease ease, Action<float> apply, Action done = null, float delay = 0f)
        {
            if (target == null || Runner.Instance == null) { apply?.Invoke(1f); done?.Invoke(); return; }
            long key = Key(target, channel);
            Stop(key);
            var entry = new Entry();
            running[key] = entry;
            entry.Co = Runner.Instance.StartCoroutine(Routine(key, entry, target, duration, ease, apply, done, delay));
        }

        public static void Stop(UnityEngine.Object target, int channel)
        {
            if (target != null) Stop(Key(target, channel));
        }

        static void Stop(long key)
        {
            if (running.TryGetValue(key, out var entry))
            {
                running.Remove(key);
                if (entry.Co != null && Runner.Instance != null) Runner.Instance.StopCoroutine(entry.Co);
            }
        }

        static void Release(long key, Entry entry)
        {
            if (running.TryGetValue(key, out var current) && current == entry) running.Remove(key);
        }

        static IEnumerator Routine(long key, Entry entry, UnityEngine.Object target, float duration, Ease ease, Action<float> apply, Action done, float delay)
        {
            if (delay > 0f)
            {
                float d = 0f;
                while (d < delay)
                {
                    d += Time.unscaledDeltaTime;
                    yield return null;
                    if (target == null) { Release(key, entry); yield break; }
                }
            }
            float t = 0f;
            while (t < duration)
            {
                apply(Evaluate(ease, t / duration));
                t += Time.unscaledDeltaTime;
                yield return null;
                if (target == null) { Release(key, entry); yield break; }
            }
            apply(Evaluate(ease, 1f));
            Release(key, entry);
            done?.Invoke();
        }

        // ---------- Kısayollar ----------

        public const int ChScale = 0, ChAlpha = 1, ChMove = 2, ChColor = 3, ChRotate = 4, ChExtra = 5;

        public static void Scale(Transform t, Vector3 to, float duration, Ease ease = Ease.OutBack, Action done = null, float delay = 0f)
        {
            if (t == null) return;
            Vector3 from = t.localScale;
            Run(t, ChScale, duration, ease, k => t.localScale = Vector3.LerpUnclamped(from, to, k), done, delay);
        }

        public static void ScaleFrom(Transform t, Vector3 from, Vector3 to, float duration, Ease ease = Ease.OutBack, Action done = null, float delay = 0f)
        {
            if (t == null) return;
            t.localScale = from;
            Run(t, ChScale, duration, ease, k => t.localScale = Vector3.LerpUnclamped(from, to, k), done, delay);
        }

        /// <summary>Yumuşak "zıplama" (basınca/doğru sayıda).</summary>
        public static void Punch(Transform t, float amount = 0.15f, float duration = 0.3f)
        {
            if (t == null) return;
            Run(t, ChScale, duration, Ease.Linear, k =>
            {
                float s = 1f + Mathf.Sin(k * Mathf.PI) * amount * (1f - k * 0.5f);
                t.localScale = new Vector3(s, s, 1f);
            });
        }

        /// <summary>Yanlış cevapta yatay titreme.</summary>
        public static void Shake(RectTransform t, float strength = 14f, float duration = 0.35f)
        {
            if (t == null) return;
            Vector2 origin = t.anchoredPosition;
            Run(t, ChMove, duration, Ease.Linear, k =>
            {
                float x = Mathf.Sin(k * Mathf.PI * 7f) * strength * (1f - k);
                t.anchoredPosition = origin + new Vector2(x, 0f);
            }, () => { if (t != null) t.anchoredPosition = origin; });
        }

        public static void Fade(CanvasGroup g, float to, float duration, Ease ease = Ease.OutQuad, Action done = null, float delay = 0f)
        {
            if (g == null) return;
            float from = g.alpha;
            Run(g, ChAlpha, duration, ease, k => g.alpha = Mathf.LerpUnclamped(from, to, k), done, delay);
        }

        public static void Move(RectTransform t, Vector2 to, float duration, Ease ease = Ease.OutCubic, Action done = null, float delay = 0f)
        {
            if (t == null) return;
            Vector2 from = t.anchoredPosition;
            Run(t, ChMove, duration, ease, k => t.anchoredPosition = Vector2.LerpUnclamped(from, to, k), done, delay);
        }

        public static void Color(UnityEngine.UI.Graphic g, Color to, float duration, Ease ease = Ease.OutQuad)
        {
            if (g == null) return;
            Color from = g.color;
            Run(g, ChColor, duration, ease, k => g.color = UnityEngine.Color.LerpUnclamped(from, to, k));
        }
    }
}
