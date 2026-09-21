using System.Collections;
using UnityEngine;

namespace BubblePics
{
    /// <summary>World-space effects ported from bubble_view.gd / merge runner.</summary>
    public static class Fx
    {
        static Transform _fxRoot;

        public static Transform FxRoot
        {
            get
            {
                if (_fxRoot == null)
                {
                    var go = new GameObject("~Fx");
                    _fxRoot = go.transform;
                }
                return _fxRoot;
            }
        }

        static Sprite _sprite(string path) => AssetLib.Sprite(path);

        // ------------------------------------------------------------ particle helper
        class OneShot : MonoBehaviour { }

        static ParticleSystem MakeParticles(Vector3 pos, Transform parent, string texPath, int order)
        {
            var go = new GameObject("P");
            go.transform.SetParent(parent != null ? parent : FxRoot, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.material = ParticleMat(texPath, false);
            psr.sortingOrder = order;
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            return ps;
        }

        static Material _particleMatAdd, _particleMatAlpha;
        static Material ParticleMat(string texPath, bool additive)
        {
            var tex = AssetLib.Texture(texPath);
            var shader = Shader.Find("BubblePics/SpineLite"); // simple tinted alpha shader
            var m = new Material(shader) { mainTexture = tex };
            if (additive)
            {
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            }
            else
            {
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
            return m;
        }

        /// <summary>Dissolve burst (explode()): 20 ambient bubbles from sphere surface.</summary>
        public static void SpawnDissolveBurst(Vector3 pos, float radius, Transform parent)
        {
            var ps = MakeParticles(pos, parent, "Art/Sprites/Bubble/ambient_bubble", 200);
            var main = ps.main;
            main.startLifetime = 0.65f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(130, 600);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f * TexW("Art/Sprites/Bubble/ambient_bubble"), 0.26f * TexW("Art/Sprites/Bubble/ambient_bubble"));
            main.gravityModifier = 0;
            main.maxParticles = 20;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.95f;
            shape.radiusThickness = 0f;
            var lim = ps.limitVelocityOverLifetime;
            lim.enabled = true; lim.drag = 2.5f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.95f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            ps.Emit(20);
            Object.Destroy(ps.gameObject, 0.85f);
        }

        static float TexW(string path)
        {
            var t = AssetLib.Texture(path);
            return t != null ? t.width : 64f;
        }

        /// <summary>Ring effect (shockwave / light flash).</summary>
        static void SpawnRing(Vector3 pos, float radius, Transform parent, string texPath, Color color, float scaleMult, float duration, int order, float holdRatio)
        {
            var go = new GameObject("Ring");
            go.transform.SetParent(parent != null ? parent : FxRoot, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite(texPath);
            sr.color = color;
            sr.sortingOrder = order;
            float texW = sr.sprite.rect.width;
            float initialS = (2f * radius) / texW;
            go.transform.localScale = Vector3.one * initialS;
            TweenRunner.Go(RingCo(go, sr, initialS, scaleMult, duration, holdRatio));
        }

        static IEnumerator RingCo(GameObject go, SpriteRenderer sr, float s0, float mult, float dur, float holdRatio)
        {
            float t = 0;
            float holdDur = dur * holdRatio;
            float baseAlpha = sr.color.a;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                if (go == null) yield break;
                go.transform.localScale = Vector3.one * Mathf.Lerp(s0, s0 * mult, k);
                float aK = t <= holdDur ? 1f : 1f - Mathf.Clamp01((t - holdDur) / (dur - holdDur));
                var c = sr.color; c.a = baseAlpha * aK; sr.color = c;
                yield return null;
            }
            Object.Destroy(go);
        }

        public static void SpawnShockwaveRing(Vector3 pos, float radius, Transform parent)
        {
            SpawnRing(pos, radius, parent, "Art/Sprites/Bubble/disc", new Color(0.7f, 0.95f, 1f, 0.65f), 1.5f, 0.35f, 150, 0.4f);
        }

        public static void SpawnLightFlashRing(Vector3 pos, float radius, Transform parent)
        {
            SpawnRing(pos, radius, parent, "Art/Sprites/Bubble/bubble_glow", new Color(1, 1, 1, 0.85f), 1.5f, 0.2f, 170, 0.4f);
        }

        /// <summary>Closure ripple ring (bubble_ripple.png): expands 1->1.5, 40 frames total,
        /// alpha up for first 25%.</summary>
        public static void SpawnClosureRing(Vector3 pos, float radius, float delaySec, Color? tint = null)
        {
            TweenRunner.Go(ClosureRingCo(pos, radius, delaySec, tint ?? Color.white));
        }

        static IEnumerator ClosureRingCo(Vector3 pos, float radius, float delay, Color tint)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            var go = new GameObject("ClosureRing");
            go.transform.SetParent(FxRoot, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite("Art/Sprites/Bubble/bubble_ripple");
            sr.sortingOrder = 1000; // ClosureFxCanvasLayer layer=100
            sr.color = new Color(tint.r, tint.g, tint.b, 0f);
            float finalDiameter = 2f * radius * BubbleView.CLOSURE_RING_SIZE_MULT;
            float unitS = finalDiameter / sr.sprite.rect.width;
            go.transform.localScale = Vector3.one * unitS;

            float totalSec = BubbleView.CLOSURE_RING_TOTAL_FRAMES / 60f;
            const float alphaUpRatio = 0.25f;
            const float scaleEnd = 1.5f;
            float t = 0;
            while (t < totalSec)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / totalSec);
                if (go == null) yield break;
                float eased = 1f - (1f - k) * (1f - k); // quad out
                go.transform.localScale = Vector3.one * (unitS * Mathf.Lerp(0f, scaleEnd, eased));
                float a = k < alphaUpRatio ? k / alphaUpRatio : 1f - (k - alphaUpRatio) / (1f - alphaUpRatio);
                sr.color = new Color(tint.r, tint.g, tint.b, a * 0.35f);
                yield return null;
            }
            Object.Destroy(go);
        }

        /// <summary>Closure water droplet sparkles (eff_star15, additive, twinkling).</summary>
        public static void SpawnWaterDroplets(Vector3 pos, float radius, Transform parent, float amountMult, bool rainbow)
        {
            var go = new GameObject("Droplets");
            go.transform.SetParent(parent != null ? parent : FxRoot, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.material = ParticleMat("Art/Sprites/Bubble/eff_star15", true);
            psr.sortingOrder = 200;
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(BubbleView.CLOSURE_SPARKLE_LIFETIME_SEC * 0.4f, BubbleView.CLOSURE_SPARKLE_LIFETIME_SEC);
            main.startSpeed = new ParticleSystem.MinMaxCurve(400, 800);
            float texW = TexW("Art/Sprites/Bubble/eff_star15");
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f * texW, 1.0f * texW);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.gravityModifier = 0;
            main.maxParticles = 100;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.4f;
            shape.radiusThickness = 0f;
            // godot damping 3-5 px/s^2 is negligible for 400-800 px/s starts:
            // the sparkles must scatter far like the recording
            var lim = ps.limitVelocityOverLifetime;
            lim.enabled = false;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

            // twinkle alpha ramp + scale curve approximation
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.02f),
                    new GradientAlphaKey(0.5f, 0.3f), new GradientAlphaKey(1f, 0.35f),
                    new GradientAlphaKey(1f, 0.48f), new GradientAlphaKey(0f, 0.53f),
                    new GradientAlphaKey(1f, 0.58f), new GradientAlphaKey(0f, 0.76f),
                });
            col.color = grad;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.02f, 0.14f), new Keyframe(0.3f, 0.2f),
                new Keyframe(0.35f, 1f), new Keyframe(0.48f, 1f), new Keyframe(0.53f, 0f),
                new Keyframe(0.58f, 1f), new Keyframe(0.71f, 1f), new Keyframe(0.76f, 0f),
                new Keyframe(0.81f, 1f), new Keyframe(0.94f, 1f), new Keyframe(1f, 0f));
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

            if (rainbow)
            {
                var sc = ps.main;
                var startColor = new ParticleSystem.MinMaxGradient(RainbowGradient());
                startColor.mode = ParticleSystemGradientMode.RandomColor;
                sc.startColor = startColor;
            }
            else
            {
                var sc = ps.main;
                sc.startColor = new Color(0.62f, 0.9f, 1f, 1f); // BubbleShineOverlay.TRAIL_COLOR approx
            }

            ps.Emit((int)(72 * Mathf.Max(amountMult, 0.1f)));
            Object.Destroy(go, BubbleView.CLOSURE_SPARKLE_LIFETIME_SEC + 0.2f);
        }

        static Gradient RainbowGradient()
        {
            var g = new Gradient();
            g.SetKeys(new[]
            {
                new GradientColorKey(new Color(1f, 0.35f, 0.35f), 0f),
                new GradientColorKey(new Color(1f, 0.65f, 0.25f), 0.2f),
                new GradientColorKey(new Color(1f, 0.95f, 0.35f), 0.4f),
                new GradientColorKey(new Color(0.45f, 1f, 0.5f), 0.6f),
                new GradientColorKey(new Color(0.45f, 0.75f, 1f), 0.8f),
                new GradientColorKey(new Color(0.85f, 0.5f, 1f), 1f),
            }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            return g;
        }

        /// <summary>Merge burst (ambient bubbles) from merge runner F25.</summary>
        public static void SpawnMergeBurst(Vector3 pos, float radius, Transform parent)
        {
            var ps = MakeParticles(pos, parent, "Art/Sprites/Bubble/ambient_bubble", 120);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f * 0.7f, 0.6f);
            float dampingAvg = (2.0f + 3.5f) * 0.5f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.75f * dampingAvg, radius * 1.4f * dampingAvg);
            float texW = TexW("Art/Sprites/Bubble/ambient_bubble");
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f * texW, 0.6f * texW);
            main.gravityModifier = 0;
            main.maxParticles = 14;
            var em = ps.emission; em.enabled = false;
            var lim = ps.limitVelocityOverLifetime;
            lim.enabled = true; lim.drag = dampingAvg;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.1f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0, 1f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.3f)));
            ps.Emit(14);
            Object.Destroy(ps.gameObject, 0.85f);
        }

        /// <summary>Drag release burst on bubble (drag trail restart): one-shot ring of bubbles.</summary>
        public static void SpawnDragReleaseBurst(Vector3 pos, Transform parent)
        {
            var ps = MakeParticles(pos, parent, "Art/Sprites/Bubble/ambient_bubble", 90);
            var main = ps.main;
            main.startLifetime = 0.6f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(10, 40);
            float texW = TexW("Art/Sprites/Bubble/ambient_bubble");
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f * texW, 0.6f * texW);
            main.gravityModifier = -0.02f;
            main.maxParticles = 14;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 180f;
            shape.radiusThickness = 1f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.5f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            ps.Emit(14);
            Object.Destroy(ps.gameObject, 0.8f);
        }

        public static void Vibrate(int level)
        {
            Haptics.Play(Haptics.FromLegacyIndex(level));
        }
    }
}
