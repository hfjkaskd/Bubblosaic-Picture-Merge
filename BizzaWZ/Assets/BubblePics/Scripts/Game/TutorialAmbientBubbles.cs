using System;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>
    /// Prefab-authored port of PopupAmbientBubbles used behind the tutorial
    /// toast. Bubble and burst nodes are pooled in BubbleTutorial.prefab so
    /// showing the guide does not construct UI objects at runtime.
    /// </summary>
    public sealed class TutorialAmbientBubbles : MonoBehaviour
    {
        const float SPAWN_INTERVAL_MIN = 0.6f;
        const float SPAWN_INTERVAL_MAX = 1.4f;
        const float RISE_SPEED_MIN = 60f;
        const float RISE_SPEED_MAX = 110f;
        const float POP_DELAY_MIN = 1.5f;
        const float POP_DELAY_MAX = 4.5f;
        const float WOBBLE_AMPLITUDE = 8f;
        const float WOBBLE_FREQUENCY = 0.7f;
        const float FADE_IN_DURATION = 0.35f;
        const float SCALE_MIN = 0.45f;
        const float SCALE_MAX = 0.75f;
        const float ALPHA_MAX = 0.65f;
        const float START_Y = -190f; // design y=1360 in the 1080 x 2340 HUD
        const float FORCE_POP_Y = 140f; // design y=1030 in the 1080 x 2340 HUD

        const int BURST_AMOUNT = 10;
        const float BURST_LIFETIME = 0.8f;
        const float BURST_VELOCITY_MIN = 25f;
        const float BURST_VELOCITY_MAX = 70f;
        const float BURST_SCALE_MIN = 0.08f;
        const float BURST_SCALE_MAX = 0.28f;
        const float BURST_EMIT_RADIUS = 30f;
        const float BURST_GRAVITY_Y = 40f;
        const float BURST_ALPHA_PEAK = 0.7f;

        [Serializable]
        struct BubbleState
        {
            public Image Image;
            public bool Active;
            public float Speed;
            public float Elapsed;
            public float PopAt;
            public float StartX;
            public float WobblePhase;
        }

        [Serializable]
        struct FragmentState
        {
            public Image Image;
            public bool Active;
            public float Elapsed;
            public float Damping;
            public Vector2 Velocity;
        }

        [SerializeField] Image[] _bubblePool = Array.Empty<Image>();
        [SerializeField] Image[] _fragmentPool = Array.Empty<Image>();

        BubbleState[] _bubbles = Array.Empty<BubbleState>();
        FragmentState[] _fragments = Array.Empty<FragmentState>();
        System.Random _random;
        bool _running;
        float _spawnAccum;
        float _nextSpawnIn;

        public void InitializeRuntime()
        {
            _bubbles = new BubbleState[_bubblePool != null ? _bubblePool.Length : 0];
            for (var i = 0; i < _bubbles.Length; i++)
            {
                _bubbles[i].Image = _bubblePool[i];
                SetInactive(_bubblePool[i]);
            }

            _fragments = new FragmentState[_fragmentPool != null ? _fragmentPool.Length : 0];
            for (var i = 0; i < _fragments.Length; i++)
            {
                _fragments[i].Image = _fragmentPool[i];
                SetInactive(_fragmentPool[i]);
            }

            _random = new System.Random(unchecked(Environment.TickCount * 397 ^ GetInstanceID()));
            _running = false;
            _spawnAccum = 0f;
            _nextSpawnIn = 0f;
        }

        public void StartEffect()
        {
            if (_bubbles.Length == 0 && _fragments.Length == 0)
                InitializeRuntime();
            if (_running) return;
            _running = true;
            _spawnAccum = 0f;
            _nextSpawnIn = 0f;
        }

        public void StopEffect()
        {
            _running = false;
            for (var i = 0; i < _bubbles.Length; i++)
            {
                _bubbles[i].Active = false;
                SetInactive(_bubbles[i].Image);
            }
            for (var i = 0; i < _fragments.Length; i++)
            {
                _fragments[i].Active = false;
                SetInactive(_fragments[i].Image);
            }
        }

        void Update()
        {
            if (!_running) return;
            var delta = Time.deltaTime;
            _spawnAccum += delta;
            if (_spawnAccum >= _nextSpawnIn)
            {
                _spawnAccum = 0f;
                _nextSpawnIn = Range(SPAWN_INTERVAL_MIN, SPAWN_INTERVAL_MAX);
                SpawnBubble();
            }

            for (var i = 0; i < _bubbles.Length; i++)
            {
                var state = _bubbles[i];
                if (!state.Active || state.Image == null) continue;
                state.Elapsed += delta;
                var y = START_Y + state.Speed * state.Elapsed;
                var x = state.StartX + WOBBLE_AMPLITUDE *
                    Mathf.Sin(state.Elapsed * Mathf.PI * 2f * WOBBLE_FREQUENCY + state.WobblePhase);
                state.Image.rectTransform.anchoredPosition = new Vector2(x, y);
                var alpha = ALPHA_MAX * Mathf.Clamp01(state.Elapsed / FADE_IN_DURATION);
                var color = state.Image.color;
                color.a = alpha;
                state.Image.color = color;

                if (state.Elapsed >= state.PopAt || y > FORCE_POP_Y)
                {
                    var position = state.Image.rectTransform.anchoredPosition;
                    state.Active = false;
                    SetInactive(state.Image);
                    _bubbles[i] = state;
                    SpawnBurst(position);
                    continue;
                }
                _bubbles[i] = state;
            }

            for (var i = 0; i < _fragments.Length; i++)
            {
                var state = _fragments[i];
                if (!state.Active || state.Image == null) continue;
                state.Elapsed += delta;
                if (state.Elapsed >= BURST_LIFETIME)
                {
                    state.Active = false;
                    SetInactive(state.Image);
                    _fragments[i] = state;
                    continue;
                }

                state.Velocity.y += BURST_GRAVITY_Y * delta;
                state.Velocity *= Mathf.Exp(-state.Damping * delta);
                state.Image.rectTransform.anchoredPosition += state.Velocity * delta;
                var color = state.Image.color;
                color.a = BURST_ALPHA_PEAK * (1f - state.Elapsed / BURST_LIFETIME);
                state.Image.color = color;
                _fragments[i] = state;
            }
        }

        void SpawnBubble()
        {
            for (var i = 0; i < _bubbles.Length; i++)
            {
                if (_bubbles[i].Active || _bubbles[i].Image == null) continue;
                var image = _bubbles[i].Image;
                var scale = Range(SCALE_MIN, SCALE_MAX);
                image.gameObject.SetActive(true);
                image.rectTransform.localScale = new Vector3(scale, scale, 1f);
                image.rectTransform.anchoredPosition = new Vector2(Range(-540f, 540f), START_Y);
                image.color = new Color(1f, 1f, 1f, 0f);
                _bubbles[i] = new BubbleState
                {
                    Image = image,
                    Active = true,
                    Speed = Range(RISE_SPEED_MIN, RISE_SPEED_MAX),
                    PopAt = Range(POP_DELAY_MIN, POP_DELAY_MAX),
                    StartX = image.rectTransform.anchoredPosition.x,
                    WobblePhase = Range(0f, Mathf.PI * 2f),
                };
                return;
            }
        }

        void SpawnBurst(Vector2 position)
        {
            for (var emitted = 0; emitted < BURST_AMOUNT; emitted++)
            {
                var slot = FindFreeFragment();
                if (slot < 0) return;
                var image = _fragments[slot].Image;
                var radius = Mathf.Sqrt(Range(0f, 1f)) * BURST_EMIT_RADIUS;
                var positionAngle = Range(0f, Mathf.PI * 2f);
                var velocityAngle = Range(0f, Mathf.PI * 2f);
                var velocity = new Vector2(Mathf.Cos(velocityAngle), Mathf.Sin(velocityAngle)) *
                    Range(BURST_VELOCITY_MIN, BURST_VELOCITY_MAX);
                var scale = Range(BURST_SCALE_MIN, BURST_SCALE_MAX);

                image.gameObject.SetActive(true);
                image.rectTransform.anchoredPosition = position +
                    new Vector2(Mathf.Cos(positionAngle), Mathf.Sin(positionAngle)) * radius;
                image.rectTransform.localScale = new Vector3(scale, scale, 1f);
                image.color = new Color(1f, 1f, 1f, BURST_ALPHA_PEAK);
                _fragments[slot] = new FragmentState
                {
                    Image = image,
                    Active = true,
                    Velocity = velocity,
                    Damping = Range(1f, 3f),
                };
            }
        }

        int FindFreeFragment()
        {
            for (var i = 0; i < _fragments.Length; i++)
                if (!_fragments[i].Active && _fragments[i].Image != null)
                    return i;
            return -1;
        }

        float Range(float min, float max)
        {
            if (_random == null)
                _random = new System.Random(unchecked(Environment.TickCount * 397 ^ GetInstanceID()));
            return min + (float)_random.NextDouble() * (max - min);
        }

        static void SetInactive(Image image)
        {
            if (image != null)
                image.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        public void ConfigurePrefabAuthoring(Image[] bubbles, Image[] fragments)
        {
            _bubblePool = bubbles ?? Array.Empty<Image>();
            _fragmentPool = fragments ?? Array.Empty<Image>();
        }
#endif
    }
}
