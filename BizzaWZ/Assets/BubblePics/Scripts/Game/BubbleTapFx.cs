using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Prefab-authored port of bubble_tap_fx.gd.
    ///
    /// Empty-space input has two independent effects: a three-particle press
    /// burst and a six-particle continuous pointer trail.  They intentionally
    /// do not share the fourteen-particle drag-release ring on BubbleEntity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BubbleTapFx : MonoBehaviour
    {
        public const int BurstAmount = 3;
        public const int TrailAmount = 6;
        public const float ParticleLifetime = 0.55f;

        // Several fixed emitters let consecutive taps overlap without
        // allocating/destroying particle GameObjects during play.
        const int BurstPoolSize = 4;

        [SerializeField] ParticleSystem[] _burstPool = new ParticleSystem[BurstPoolSize];
        [SerializeField] ParticleSystem _trail;

        int _nextBurst;
        Material _ownedRuntimeMaterial;

        public bool HasRequiredReferences
        {
            get
            {
                if (_trail == null || _burstPool == null ||
                    _burstPool.Length != BurstPoolSize)
                    return false;
                for (var i = 0; i < _burstPool.Length; i++)
                    if (_burstPool[i] == null) return false;
                return true;
            }
        }

        /// <summary>
        /// Editor/prefab compatibility authoring hook. Normal gameplay uses the
        /// already-authored children saved in BubblePage.prefab.
        /// </summary>
        public void SetupPrefabAuthoring(Material sharedMaterial = null)
        {
            var material = sharedMaterial;
            if (material == null)
            {
                material = BubbleParticleAuthoring.CreateRuntimeMaterial();
                _ownedRuntimeMaterial = material;
            }

            if (_burstPool == null || _burstPool.Length != BurstPoolSize)
                _burstPool = new ParticleSystem[BurstPoolSize];

            for (var i = 0; i < BurstPoolSize; i++)
            {
                _burstPool[i] = GetOrCreateEmitter(
                    _burstPool[i], $"PressBurst_{i:D2}");
                BubbleParticleAuthoring.ConfigureTapBurst(
                    _burstPool[i], material, BurstAmount, ParticleLifetime);
            }

            _trail = GetOrCreateEmitter(_trail, "PointerTrail");
            BubbleParticleAuthoring.ConfigurePointerTrail(
                _trail, material, TrailAmount, ParticleLifetime);
        }

        public bool InitializeRuntime()
        {
            ResolveReferences();
            if (!HasRequiredReferences)
            {
                Debug.LogError(
                    "BubbleTapFx is missing its prefab-authored particle emitters. " +
                    "Rebuild BubblePage.prefab from BubblePics/Prefabs/Align Bubble Input FX.");
                return false;
            }

            for (var i = 0; i < _burstPool.Length; i++)
                StopAndClear(_burstPool[i]);
            StopAndClear(_trail);
            _nextBurst = 0;
            return true;
        }

        public void Burst(Vector3 worldPosition)
        {
            if (!HasRequiredReferences && !InitializeRuntime()) return;

            var selected = -1;
            for (var offset = 0; offset < _burstPool.Length; offset++)
            {
                var index = (_nextBurst + offset) % _burstPool.Length;
                if (_burstPool[index].particleCount != 0) continue;
                selected = index;
                break;
            }
            if (selected < 0) selected = _nextBurst;
            _nextBurst = (selected + 1) % _burstPool.Length;

            var emitter = _burstPool[selected];
            StopAndClear(emitter);
            emitter.transform.position = worldPosition;
            emitter.Play(true);
            emitter.Emit(BurstAmount);
        }

        public void BeginTrail(Vector3 worldPosition)
        {
            if (!HasRequiredReferences && !InitializeRuntime()) return;
            StopAndClear(_trail);
            _trail.transform.position = worldPosition;
            _trail.Play(true);
        }

        public void UpdateTrail(Vector3 worldPosition)
        {
            if (_trail != null) _trail.transform.position = worldPosition;
        }

        public void EndTrail()
        {
            if (_trail != null)
                _trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public void ResetFx()
        {
            if (_trail != null) StopAndClear(_trail);
            if (_burstPool == null) return;
            for (var i = 0; i < _burstPool.Length; i++)
                if (_burstPool[i] != null) StopAndClear(_burstPool[i]);
        }

        void ResolveReferences()
        {
            if (_burstPool == null || _burstPool.Length != BurstPoolSize)
                _burstPool = new ParticleSystem[BurstPoolSize];
            for (var i = 0; i < _burstPool.Length; i++)
            {
                if (_burstPool[i] != null) continue;
                var child = transform.Find($"PressBurst_{i:D2}");
                if (child != null) _burstPool[i] = child.GetComponent<ParticleSystem>();
            }
            if (_trail == null)
            {
                var child = transform.Find("PointerTrail");
                if (child != null) _trail = child.GetComponent<ParticleSystem>();
            }
        }

        ParticleSystem GetOrCreateEmitter(ParticleSystem current, string childName)
        {
            if (current != null) return current;
            var child = transform.Find(childName);
            if (child == null)
            {
                var go = new GameObject(childName);
                child = go.transform;
                child.SetParent(transform, false);
            }
            var emitter = child.GetComponent<ParticleSystem>();
            if (emitter == null) emitter = child.gameObject.AddComponent<ParticleSystem>();
            return emitter;
        }

        static void StopAndClear(ParticleSystem emitter)
        {
            emitter.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void OnDestroy()
        {
            if (_ownedRuntimeMaterial == null) return;
            if (Application.isPlaying) Destroy(_ownedRuntimeMaterial);
            else DestroyImmediate(_ownedRuntimeMaterial);
            _ownedRuntimeMaterial = null;
        }
    }

    /// <summary>
    /// Exact particle parameters shared by the authored tap and fall emitters.
    /// Godot's y-down negative gravity maps to positive world-y in Unity.
    /// </summary>
    internal static class BubbleParticleAuthoring
    {
        const string AmbientBubblePath = "Art/Sprites/Bubble/ambient_bubble";

        public static Material CreateRuntimeMaterial()
        {
            var shader = Shader.Find("BubblePics/ParticleTintUv");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = "BubbleParticle" };
            var texture = AssetLib.Texture(AmbientBubblePath);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", Color.white);
            return material;
        }

        public static void ConfigureTapBurst(
            ParticleSystem particles, Material material, int amount, float lifetime)
        {
            ConfigureCommon(
                particles,
                material,
                false,
                amount,
                lifetime,
                30f,
                120f,
                240f,
                60f,
                1.5f,
                3f,
                0.261f,
                0.78f,
                90);
            var emission = particles.emission;
            emission.enabled = false;
        }

        public static void ConfigurePointerTrail(
            ParticleSystem particles, Material material, int amount, float lifetime)
        {
            ConfigureCommon(
                particles,
                material,
                true,
                amount,
                lifetime,
                24f,
                40f,
                120f,
                60f,
                1.5f,
                3f,
                0.261f,
                0.78f,
                90);
            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = amount / lifetime;
        }

        public static void ConfigureFallTrail(
            ParticleSystem particles, Material material)
        {
            const int amount = 8; // CPUParticles2D default amount.
            const float lifetime = 0.6f;
            ConfigureCommon(
                particles,
                material,
                true,
                amount,
                lifetime,
                160f,
                10f,
                40f,
                20f,
                1f,
                2f,
                0.15f,
                0.5f,
                90);

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = amount / lifetime;

            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.4f),
                    new Keyframe(0.5f, 0.55f),
                    new Keyframe(1f, 0.3f)));
        }

        static void ConfigureCommon(
            ParticleSystem particles,
            Material material,
            bool loop,
            int amount,
            float lifetime,
            float radius,
            float speedMin,
            float speedMax,
            float gravityWorldY,
            float dampingMin,
            float dampingMax,
            float scaleMin,
            float scaleMax,
            int sortingOrder)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var texture = AssetLib.Texture(AmbientBubblePath);
            var textureWidth = texture != null ? texture.width : 1f;

            var main = particles.main;
            main.loop = loop;
            main.playOnAwake = false;
            main.duration = lifetime;
            main.startLifetime = lifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(
                scaleMin * textureWidth, scaleMax * textureWidth);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = amount;
            main.stopAction = ParticleSystemStopAction.None;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 0f;
            shape.arc = 360f;

            var force = particles.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;
            force.x = 0f;
            force.y = gravityWorldY;
            force.z = 0f;

            var damping = particles.limitVelocityOverLifetime;
            damping.enabled = true;
            damping.space = ParticleSystemSimulationSpace.World;
            damping.drag = new ParticleSystem.MinMaxCurve(dampingMin, dampingMax);
            damping.dampen = 0f;
            damping.multiplyDragByParticleSize = false;
            damping.multiplyDragByParticleVelocity = false;

            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(loop && radius >= 100f ? 0.85f : 0f, 0f),
                    new GradientAlphaKey(loop && radius >= 100f ? 0.5f : 0.85f,
                        loop && radius >= 100f ? 0.3f : 0.2f),
                    new GradientAlphaKey(0f, 1f),
                });
            color.color = gradient;

            var size = particles.sizeOverLifetime;
            size.enabled = false;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortingOrder = sortingOrder;
            renderer.sharedMaterial = material;
        }
    }
}
