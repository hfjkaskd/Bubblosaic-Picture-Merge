using System.Collections;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Recovered reward-level top-bar chest. It replaces the dolphin during
    /// bonus rounds and uses the original box Spine resource and idle/open
    /// animations.
    /// </summary>
    public sealed class BonusChestDecoration : MonoBehaviour
    {
        const float ChestScale = 0.275f;
        const float MoveToCenterSeconds = 0.7f;
        const float FadeOutSeconds = 0.25f;

        [SerializeField] SpineLite.SpineSprite _spine;

        Vector3 _basePosition;
        float _baseScale;
        bool _openPlaying;

        public void InitializePrefabRuntime(
            Vector3 centerWorldPosition,
            float hudScale)
        {
            _basePosition = centerWorldPosition;
            _baseScale = ChestScale * hudScale;
            if (_spine == null)
                _spine = GetComponentInChildren<SpineLite.SpineSprite>(true);
            if (_spine == null)
            {
                var spineObject = new GameObject("SpineSprite");
                spineObject.transform.SetParent(transform, false);
                _spine = spineObject.AddComponent<SpineLite.SpineSprite>();
            }
            if (_spine.Data == null) _spine.Load("box");
            _spine.SortingOrder = SortOrder.DolphinNormal;
            transform.position = _basePosition;
            transform.localScale = Vector3.one * _baseScale;
            gameObject.SetActive(false);
        }

        public void SetBonusVisible(bool visible)
        {
            _openPlaying = false;
            gameObject.SetActive(visible);
            if (!visible || _spine == null) return;
            transform.position = _basePosition;
            transform.localScale = Vector3.one * _baseScale;
            _spine.Tint = Color.white;
            _spine.SortingOrder = SortOrder.DolphinNormal;
            if (_spine.HasAnimation("idle"))
                _spine.SetAnimation("idle", true, 0f);
        }

        public SpineLite.TrackEntry PlayOpen()
        {
            if (_spine == null || !_spine.HasAnimation("Appear_open"))
                return null;
            gameObject.SetActive(true);
            _spine.SortingOrder = 4000;
            return _spine.SetAnimation("Appear_open", false, 0f);
        }

        public IEnumerator PlayOpenSequence()
        {
            if (_openPlaying || _spine == null) yield break;
            _openPlaying = true;
            gameObject.SetActive(true);
            _spine.SortingOrder = 4000;
            _spine.Tint = Color.white;

            if (_spine.HasAnimation("Appear"))
                _spine.SetAnimation("Appear", false, 0f);

            Vector3 fromPosition = transform.position;
            Vector3 fromScale = transform.localScale;
            Vector3 center = App.DesignToWorld(
                new Vector2(BubbleField.ViewW * 0.5f,
                    BubbleField.ViewH * 0.5f));
            yield return Tween.Run(
                MoveToCenterSeconds,
                value =>
                {
                    if (this == null) return;
                    transform.position = Vector3.LerpUnclamped(
                        fromPosition, center, value);
                    transform.localScale = Vector3.LerpUnclamped(
                        fromScale, Vector3.one, value);
                },
                Ease.OutBack);

            SoundManager.I?.Play("bonus_chest_open");
            SpineLite.TrackEntry open = PlayOpen();
            if (open != null && open.AnimationEnd > 0f)
                yield return new WaitForSeconds(open.AnimationEnd);

            yield return Tween.Run(
                FadeOutSeconds,
                value =>
                {
                    if (_spine != null)
                        _spine.Tint = new Color(1f, 1f, 1f, 1f - value);
                },
                Ease.InSine);
            if (this != null) gameObject.SetActive(false);
            _openPlaying = false;
        }
    }
}
