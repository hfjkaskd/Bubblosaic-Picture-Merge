using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>Uses authored caption pixels for the matching language; translated captions retain the live TMP label.</summary>
    [DisallowMultipleComponent]
    public sealed class ApprovedHudCaption : MonoBehaviour
    {
        [SerializeField] TMP_Text _label;
        [SerializeField] Image _surface;
        [SerializeField] string _authoredCaption;
        [SerializeField] Material _captionMaterial;
        [SerializeField] Material _translatedMaterial;
        [SerializeField] CanvasGroup _captionGroup;
        [SerializeField] string _bakedResourcePath;
        [SerializeField] string _bakedCaptionSprite;
        [SerializeField] string _bakedTranslatedSprite;
        bool refreshPending;

        void OnEnable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            refreshPending = true;
        }

        void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            refreshPending = false;
        }

        void OnTextChanged(UnityEngine.Object changed)
        {
            if (changed == _label) refreshPending = true;
        }

        void LateUpdate()
        {
            if (!refreshPending) return;
            refreshPending = false;
            // TMP emits TEXT_CHANGED during its canvas rebuild. Queue the material
            // swap to the next update instead of registering another rebuild inside it.
            RefreshCaption();
        }

        public void RefreshCaption()
        {
            if (_label == null || _surface == null) return;
            bool authored = string.Equals(_label.text, _authoredCaption, StringComparison.Ordinal);
            Material desired = authored ? _captionMaterial : _translatedMaterial;
            float alpha = authored ? 0f : 1f;
            if (!string.IsNullOrEmpty(_bakedResourcePath))
            {
                var sprite = CoralResourceSprite.Load(_bakedResourcePath, authored ? _bakedCaptionSprite : _bakedTranslatedSprite);
                if (_surface.sprite != sprite) _surface.sprite = sprite;
            }
            else if (_surface.material != desired) _surface.material = desired;
            if (_captionGroup != null)
            {
                // CanvasGroup also hides SDF outline/underlay passes, without preventing
                // the live text from rebuilding and emitting localization changes.
                if (!Mathf.Approximately(_label.alpha, 1f)) _label.alpha = 1f;
                if (!Mathf.Approximately(_captionGroup.alpha, alpha)) _captionGroup.alpha = alpha;
            }
            else if (!Mathf.Approximately(_label.alpha, alpha)) _label.alpha = alpha;
        }
    }
}
