using System.Collections.Generic;
using UnityEngine;

namespace BubblePics
{
    /// <summary>Port of ambient_bubbles.gd — foreground rising bubbles with pops.</summary>
    public class AmbientBubbles : MonoBehaviour
    {
        class Bub
        {
            public SpriteRenderer Sr;
            public float Speed, PopAt, Age, WobblePhase;
            public float MaxA = 0.25f;
        }

        readonly List<Bub> _bubbles = new List<Bub>();
        float _nextSpawn;
        bool _running;
        float _viewW, _floorY;
        float _groupAlpha = 1f;
        [SerializeField] Sprite _sprite;

        public void Setup(float viewW, float floorY)
        {
            _viewW = viewW;
            _floorY = floorY;
            if (_sprite == null)
                _sprite = AssetLib.Sprite("Art/Sprites/Bubble/ambient_bubble");
        }

        public void StartLayer()
        {
            _running = true;
            _nextSpawn = Random.Range(0.3f, 1.2f);
        }

        public void StopLayer()
        {
            _running = false;
            foreach (var b in _bubbles) if (b.Sr != null) Destroy(b.Sr.gameObject);
            _bubbles.Clear();
        }

        public void SetGroupAlpha(float a) { _groupAlpha = a; }

        void Update()
        {
            if (!_running) return;
            _nextSpawn -= Time.deltaTime;
            if (_nextSpawn <= 0)
            {
                _nextSpawn = Random.Range(3.0f, 5.6f);
                Spawn();
            }
            for (int i = _bubbles.Count - 1; i >= 0; i--)
            {
                var b = _bubbles[i];
                if (b.Sr == null) { _bubbles.RemoveAt(i); continue; }
                b.Age += Time.deltaTime;
                var p = b.Sr.transform.position;
                p.y += b.Speed * Time.deltaTime; // rising = +y world
                p.x += Mathf.Sin(b.Age * Mathf.PI * 2f * 0.6f + b.WobblePhase) * 6f * Time.deltaTime;
                b.Sr.transform.position = p;
                float a = Mathf.Min(b.Age / 0.4f, 1f) * b.MaxA;
                var c = b.Sr.color; c.a = a * _groupAlpha; b.Sr.color = c;
                bool popNow = b.Age >= b.PopAt || p.y > App.DesignToWorld(new Vector2(0, -50)).y;
                if (popNow)
                {
                    Pop(b);
                    _bubbles.RemoveAt(i);
                }
            }
        }

        void Spawn()
        {
            var go = new GameObject("AmbBubble");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite;
            sr.sortingOrder = 30;
            sr.color = new Color(1, 1, 1, 0);
            float scale = Random.Range(0.4f, 0.6f);
            go.transform.localScale = Vector3.one * scale;
            go.transform.position = App.DesignToWorld(new Vector2(Random.Range(0f, _viewW), _floorY + 20f));
            _bubbles.Add(new Bub
            {
                Sr = sr,
                Speed = Random.Range(60f, 100f),
                PopAt = Random.Range(3f, 14f),
                WobblePhase = Random.value * Mathf.PI * 2f,
            });
        }

        void Pop(Bub b)
        {
            var pos = b.Sr.transform.position;
            Destroy(b.Sr.gameObject);
            var ps = new GameObject("Pop").AddComponent<ParticleSystem>();
            ps.transform.position = pos;
            var psr = ps.GetComponent<ParticleSystemRenderer>();
            psr.material = new Material(Shader.Find("BubblePics/SpineLite")) { mainTexture = _sprite.texture };
            psr.sortingOrder = 31;
            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.9f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(20, 60);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f * _sprite.rect.width, 0.225f * _sprite.rect.width);
            main.gravityModifier = -30f / 980f;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 80f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0.425f, 0f), new GradientAlphaKey(0.425f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            ps.Emit(12);
            Destroy(ps.gameObject, 1.1f);
        }
    }

    /// <summary>Port of mid_depth_bubbles.gd — tiny slow background bubbles.</summary>
    public class MidDepthBubbles : MonoBehaviour
    {
        class Bub
        {
            public SpriteRenderer Sr;
            public float Speed, Lifetime, Age;
        }

        readonly List<Bub> _bubbles = new List<Bub>();
        float _nextSpawn;
        bool _running;
        float _viewW, _viewH;
        float _groupAlpha = 1f;
        [SerializeField] Sprite _sprite;
        static readonly Color Tint = new Color(0.88f, 0.96f, 1f);

        public void Setup(float viewW, float viewH)
        {
            _viewW = viewW;
            _viewH = viewH;
            if (_sprite == null)
                _sprite = AssetLib.Sprite("Art/Sprites/Bubble/ambient_bubble");
        }

        public void StartLayer() { _running = true; _nextSpawn = 0.2f; }

        public void StopLayer()
        {
            _running = false;
            foreach (var b in _bubbles) if (b.Sr != null) Destroy(b.Sr.gameObject);
            _bubbles.Clear();
        }

        public void SetGroupAlpha(float a) { _groupAlpha = a; }

        void Update()
        {
            if (!_running) return;
            _nextSpawn -= Time.deltaTime;
            if (_nextSpawn <= 0)
            {
                _nextSpawn = Random.Range(0.8f, 1.8f);
                Spawn();
            }
            for (int i = _bubbles.Count - 1; i >= 0; i--)
            {
                var b = _bubbles[i];
                if (b.Sr == null) { _bubbles.RemoveAt(i); continue; }
                b.Age += Time.deltaTime;
                var p = b.Sr.transform.position;
                p.y += b.Speed * Time.deltaTime;
                b.Sr.transform.position = p;
                float a;
                if (b.Age < 0.7f) a = b.Age / 0.7f;
                else if (b.Age > b.Lifetime - 1.2f) a = Mathf.Clamp01((b.Lifetime - b.Age) / 1.2f);
                else a = 1f;
                var c = b.Sr.color; c.a = a * 0.22f * _groupAlpha; b.Sr.color = c;
                if (b.Age >= b.Lifetime)
                {
                    Destroy(b.Sr.gameObject);
                    _bubbles.RemoveAt(i);
                }
            }
        }

        void Spawn()
        {
            var go = new GameObject("MidBubble");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite;
            sr.sortingOrder = 25;
            sr.color = new Color(Tint.r, Tint.g, Tint.b, 0);
            go.transform.localScale = Vector3.one * Random.Range(0.06f, 0.16f);
            go.transform.position = App.DesignToWorld(new Vector2(Random.Range(0, _viewW), Random.Range(0.22f * _viewH, 0.62f * _viewH)));
            _bubbles.Add(new Bub
            {
                Sr = sr,
                Speed = Random.Range(16f, 32f),
                Lifetime = Random.Range(4.5f, 7.5f),
            });
        }
    }

    /// <summary>Port of light_rays.gd — 16-ray fan from top center.</summary>
    public class LightRays : MonoBehaviour
    {
        [System.Serializable]
        class Ray
        {
            [SerializeField] public SpriteRenderer Sr;
            [SerializeField] public float BaseAngle, BaseAlpha, WidthScale, LengthScale;
            [SerializeField] public float BreathePhase, FlickerPhase, SwayPhase, WidthPhase;
        }

        [Header("Prefab hierarchy")]
        [SerializeField] List<Ray> _rays = new List<Ray>();
        [SerializeField] Sprite _raySprite;
        float _groupAlpha = 1f;
        [SerializeField] int _order = 25;

        /// <summary>Legacy entry point for a code-built hierarchy.</summary>
        public void Build(int sortingOrder)
        {
            EnsureRays(sortingOrder);
        }

        [ContextMenu("Authoring/Setup Prefab Hierarchy")]
        public void SetupPrefabAuthoring()
        {
            SetupPrefabAuthoring(_order);
        }

        public void SetupPrefabAuthoring(int sortingOrder)
        {
            EnsureRays(sortingOrder);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// Reuses only serialized rays. Deleted rays and cleared sprites are
        /// intentional prefab edits and are never reconstructed at runtime.
        /// </summary>
        public void InitializePrefabRuntime(int sortingOrder = 25)
        {
            _order = sortingOrder;
            transform.position = App.DesignToWorld(
                new Vector2(BubbleField.ViewW / 2f, -520f));
            if (_rays == null)
                _rays = new List<Ray>();
            _rays.RemoveAll(ray => ray == null || ray.Sr == null);
            foreach (Ray ray in _rays)
                ray.Sr.sortingOrder = _order;
        }

        void EnsureRays(int sortingOrder)
        {
            _order = sortingOrder;
            EnsureRaySprite();

            if (_rays == null) _rays = new List<Ray>();
            var previous = new Dictionary<SpriteRenderer, Ray>();
            foreach (var ray in _rays)
                if (ray != null && ray.Sr != null && !previous.ContainsKey(ray.Sr))
                    previous.Add(ray.Sr, ray);

            _rays.Clear();
            var rng = new System.Random(20260609);
            transform.position = App.DesignToWorld(new Vector2(BubbleField.ViewW / 2f, -520f));
            for (int i = 0; i < 16; i++)
            {
                var child = FindDirectChild("Ray" + i);
                if (child == null)
                {
                    var go = new GameObject("Ray" + i);
                    child = go.transform;
                    child.SetParent(transform, false);
                }
                var sr = child.GetComponent<SpriteRenderer>();
                if (sr == null) sr = child.gameObject.AddComponent<SpriteRenderer>();
                if (_raySprite != null) sr.sprite = _raySprite;
                sr.sortingOrder = _order;
                float t = i / 15f;
                float ang = Mathf.Lerp(-33f, 33f, t) + (float)(rng.NextDouble() * 6 - 3);
                if (!previous.TryGetValue(sr, out var ray))
                {
                    ray = new Ray
                    {
                        Sr = sr,
                        BaseAngle = ang,
                        BaseAlpha = Mathf.Lerp(0.28f, 0.56f, (float)rng.NextDouble()),
                        WidthScale = Mathf.Lerp(0.22f, 2.3f, (float)rng.NextDouble()),
                        LengthScale = Mathf.Lerp(0.9f, 1.9f, (float)rng.NextDouble()),
                        BreathePhase = (float)rng.NextDouble() * Mathf.PI * 2f,
                        FlickerPhase = (float)rng.NextDouble() * Mathf.PI * 2f,
                        SwayPhase = (float)rng.NextDouble() * Mathf.PI * 2f,
                        WidthPhase = (float)rng.NextDouble() * Mathf.PI * 2f,
                    };
                }
                else
                {
                    // Advance the deterministic generator even when authored
                    // values are kept, so newly added later rays stay stable.
                    rng.NextDouble();
                    rng.NextDouble();
                    rng.NextDouble();
                    rng.NextDouble();
                    rng.NextDouble();
                    rng.NextDouble();
                    rng.NextDouble();
                }
                sr.color = new Color(0.85f, 0.95f, 1f, ray.BaseAlpha);
                child.localRotation = Quaternion.Euler(0, 0, -ray.BaseAngle); // design cw -> world ccw flip
                child.localScale = new Vector3(ray.WidthScale, ray.LengthScale, 1);
                _rays.Add(ray);
            }
        }

        void EnsureRaySprite()
        {
            if (_raySprite != null) return;
            for (int i = 0; i < transform.childCount; i++)
            {
                var renderer = transform.GetChild(i).GetComponent<SpriteRenderer>();
                if (renderer != null && renderer.sprite != null)
                {
                    _raySprite = renderer.sprite;
                    return;
                }
            }
            var tex = AssetLib.Texture("Art/Sprites/Home/home_light");
            if (tex == null) return;
            _raySprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 1f), 1f, 0, SpriteMeshType.FullRect);
        }

        Transform FindDirectChild(string nodeName)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name == nodeName) return child;
            }
            return null;
        }

        public void SetGroupAlpha(float a) { _groupAlpha = a; }

        void Update()
        {
            float t = Time.time;
            foreach (var r in _rays)
            {
                if (r.Sr == null) continue;
                float breathe = Mathf.Sin(t * Mathf.PI * 2f / 6f + r.BreathePhase) * 0.08f;
                float flicker = Mathf.Sin(t * Mathf.PI * 2f / 1.6f + r.FlickerPhase) * 0.03f;
                float a = Mathf.Clamp01(r.BaseAlpha + breathe + flicker) * _groupAlpha;
                r.Sr.color = new Color(0.85f, 0.95f, 1f, a);
                float sway = Mathf.Sin(t * Mathf.PI * 2f / 8f + r.SwayPhase) * 0.7f;
                r.Sr.transform.localRotation = Quaternion.Euler(0, 0, -(r.BaseAngle + sway));
                float widthPulse = 1f + Mathf.Sin(t * Mathf.PI * 2f / 7f + r.WidthPhase) * 0.06f;
                r.Sr.transform.localScale = new Vector3(r.WidthScale * widthPulse, r.LengthScale, 1);
            }
        }
    }

    /// <summary>Port of water_wave_band prefab — caustics band (completion + splash).</summary>
    public class WaterWaveBand : MonoBehaviour
    {
        [Header("Prefab hierarchy")]
        [SerializeField] SpriteRenderer _spriteRenderer;
        [SerializeField] Material _materialTemplate;
        Material _material;
        bool _ownsMaterial;

        public SpriteRenderer Sr => _spriteRenderer;

        /// <summary>Legacy entry point; now reuses the prefab's renderer.</summary>
        public void Build(Transform parent, int sortingOrder)
        {
            if (parent != null && parent != transform && transform.parent != parent)
                transform.SetParent(parent, false);
            InitializePrefabRuntime(sortingOrder);
        }

        [ContextMenu("Authoring/Setup Prefab Hierarchy")]
        public void SetupPrefabAuthoring()
        {
            SetupPrefabAuthoring(135);
        }

        public void SetupPrefabAuthoring(int sortingOrder)
        {
            EnsureRendererAndMaterial(sortingOrder);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// Configures an instantiated completion-band prefab without adding a
        /// second SpriteRenderer. Missing data is created only for legacy use.
        /// </summary>
        public void InitializePrefabRuntime(int sortingOrder = 135)
        {
            EnsureRendererAndMaterial(sortingOrder);
        }

        void EnsureRendererAndMaterial(int sortingOrder)
        {
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null)
                _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

            var white = Texture2D.whiteTexture;
            if (_spriteRenderer.sprite == null)
                _spriteRenderer.sprite = Sprite.Create(white, new Rect(0, 0, white.width, white.height),
                    new Vector2(0, 1), 1f, 0, SpriteMeshType.FullRect);
            float bandH = BubbleField.ViewH * 0.234f;
            transform.localScale = new Vector3(BubbleField.ViewW / white.width, bandH / white.height, 1);
            transform.position = App.DesignToWorld(new Vector2(0, 0));

            var rippleShader = Shader.Find("BubblePics/WaterRipple");
            if (_material == null || _material.shader != rippleShader)
            {
                if (_ownsMaterial && _material != null)
                    Destroy(_material);
                _material = null;
                _ownsMaterial = false;

                Material source = _materialTemplate != null &&
                                  _materialTemplate.shader == rippleShader
                    ? _materialTemplate
                    : _spriteRenderer.sharedMaterial != null &&
                      _spriteRenderer.sharedMaterial.shader == rippleShader
                        ? _spriteRenderer.sharedMaterial
                        : null;
                if (source != null)
                {
                    _material = new Material(source)
                    {
                        name = "CompletionWaterWaveBand (Runtime)"
                    };
                    _ownsMaterial = true;
                }
                else if (rippleShader != null)
                {
                    _material = new Material(rippleShader)
                    {
                        name = "CompletionWaterWaveBand (Runtime)"
                    };
                    _ownsMaterial = true;
                }
            }
            // Never expose the white carrier sprite if the effect shader was
            // stripped or failed to load. The wave band is decorative; hiding
            // it is the safe fallback.
            _spriteRenderer.enabled = _material != null;
            if (_material == null)
            {
                _spriteRenderer.sortingOrder = sortingOrder;
                return;
            }

            _material.SetTexture("_CausticsTex", AssetLib.Texture("Art/Sprites/Home/water_caustics"));
            _material.SetFloat("_ScaleU", 3.46f);
            _material.SetFloat("_Aspect", bandH / BubbleField.ViewW);
            _material.SetFloat("_HStretch", 1.6f);
            _material.SetVector("_ScrollA", new Vector4(0.05f, 0.03f, 0, 0));
            _material.SetVector("_ScrollB", new Vector4(-0.045f, 0.0375f, 0, 0));
            _material.SetFloat("_RippleFreq", 2.4f);
            _material.SetFloat("_RippleAmp", 0.05f);
            _material.SetFloat("_Intensity", 0.25f);
            _material.SetColor("_Tint", new Color(0.85f, 0.95f, 1f, 1f));
            _material.SetFloat("_BandHeight", 0.167f);
            _material.SetFloat("_BandFade", 0.833f);
            _material.SetFloat("_Persp", 5.0f);
            _material.SetFloat("_PerspCurve", 3.0f);
            _spriteRenderer.sharedMaterial = _material;
            _spriteRenderer.sortingOrder = sortingOrder;
        }

        public void SetAlpha(float a)
        {
            if (_spriteRenderer == null) return;
            var c = _spriteRenderer.color; c.a = a; _spriteRenderer.color = c;
        }

        void OnDestroy()
        {
            if (_ownsMaterial && _material != null)
                Destroy(_material);
        }
    }
}
