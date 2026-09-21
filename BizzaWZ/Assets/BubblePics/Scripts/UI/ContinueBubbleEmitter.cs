using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>
    /// UI-space equivalent of continue_panel/BubbleEmitter. The source scene
    /// uses one continuously recycled particle with a 2.5 second lifetime.
    /// Keeping the image in the prefab makes the effect visible and editable
    /// without creating scene objects at runtime.
    /// </summary>
    public sealed class ContinueBubbleEmitter : MonoBehaviour
    {
        const float Lifetime = 2.5f;
        const float Radius = 130f;
        const float UpwardAcceleration = 25f;

        [SerializeField] Image _bubble;
        Coroutine _emitRoutine;

        public void ConfigurePrefabAuthoring(Image bubble)
        {
            _bubble = bubble;
            BindPrefabRuntime();
        }

        public void BindPrefabRuntime()
        {
            if (_bubble == null)
            {
                var images = GetComponentsInChildren<Image>(true);
                foreach (var image in images)
                {
                    if (image.gameObject.name != "Bubble") continue;
                    _bubble = image;
                    break;
                }
            }
            if (_bubble != null)
            {
                _bubble.raycastTarget = false;
                _bubble.gameObject.SetActive(false);
            }
        }

        public void StartEmitting()
        {
            BindPrefabRuntime();
            if (_bubble == null || !isActiveAndEnabled) return;
            StopEmitting();
            _emitRoutine = StartCoroutine(EmitCo());
        }

        public void StopEmitting()
        {
            if (_emitRoutine != null)
            {
                StopCoroutine(_emitRoutine);
                _emitRoutine = null;
            }
            if (_bubble != null)
                _bubble.gameObject.SetActive(false);
        }

        IEnumerator EmitCo()
        {
            var rect = _bubble.rectTransform;
            while (true)
            {
                Vector2 start = Random.insideUnitCircle * Radius;
                float angle = Random.Range(-90f, 90f) * Mathf.Deg2Rad;
                float speed = Random.Range(30f, 60f);
                Vector2 velocity = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * speed;
                float damping = Random.Range(1.5f, 2.5f);
                float scale = Random.Range(0.18f, 0.36f);

                rect.anchoredPosition = start;
                rect.localScale = Vector3.one * scale;
                _bubble.color = new Color(1f, 1f, 1f, 0f);
                _bubble.gameObject.SetActive(true);

                float elapsed = 0f;
                while (elapsed < Lifetime)
                {
                    float delta = Time.unscaledDeltaTime;
                    elapsed += delta;
                    velocity += Vector2.up * (UpwardAcceleration * delta);
                    velocity *= Mathf.Exp(-damping * delta);
                    rect.anchoredPosition += velocity * delta;

                    float p = Mathf.Clamp01(elapsed / Lifetime);
                    float alpha = p < 0.15f
                        ? Mathf.Lerp(0f, 0.85f, p / 0.15f)
                        : p > 0.85f
                            ? Mathf.Lerp(0.85f, 0f, (p - 0.85f) / 0.15f)
                            : 0.85f;
                    _bubble.color = new Color(1f, 1f, 1f, alpha);
                    yield return null;
                }

                _bubble.gameObject.SetActive(false);
                yield return null;
            }
        }

        void OnDisable()
        {
            _emitRoutine = null;
            if (_bubble != null)
                _bubble.gameObject.SetActive(false);
        }
    }
}
