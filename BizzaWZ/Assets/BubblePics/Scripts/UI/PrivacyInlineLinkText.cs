using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BubblePics
{
    /// <summary>
    /// Adds Godot RichTextLabel-style inline URL hit testing to TMP text. Native
    /// TMP link metadata keeps wrapped and fallback-font glyphs clickable.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class PrivacyInlineLinkText :
        MonoBehaviour,
        IPointerClickHandler,
        ICanvasRaycastFilter
    {
        const string TermsLinkId = "terms";
        const string PrivacyLinkId = "privacy";
        const string LinkColor = "#00BC32";
        const float HitPadding = 7f;

        [SerializeField] TextMeshProUGUI _text;

        Action _openTerms;
        Action _openPrivacy;
        bool _layoutDirty = true;

        public int LinkRegionCount
        {
            get
            {
                EnsureLayout();
                return _text != null ? _text.textInfo.linkCount : 0;
            }
        }

        void Awake()
        {
            ResolveText();
        }

        void OnEnable()
        {
            InvalidateLayout();
        }

        void OnRectTransformDimensionsChange()
        {
            InvalidateLayout();
        }

        void LateUpdate()
        {
            if (_layoutDirty)
                EnsureLayout();
        }

        public void Bind(Action openTerms, Action openPrivacy)
        {
            _openTerms = openTerms;
            _openPrivacy = openPrivacy;
        }

        public void SetLocalizedTemplate(
            string localizedTemplate,
            string termsFallback,
            string privacyFallback)
        {
            ResolveText();
            string source = (localizedTemplate ?? string.Empty).Replace("\\n", "\n");

            int mark1 = source.IndexOf("%1", StringComparison.Ordinal);
            int mark2 = mark1 >= 0
                ? source.IndexOf("%2", mark1 + 2, StringComparison.Ordinal)
                : -1;
            int mark3 = mark2 >= 0
                ? source.IndexOf("%3", mark2 + 2, StringComparison.Ordinal)
                : -1;
            int mark4 = mark3 >= 0
                ? source.IndexOf("%4", mark3 + 2, StringComparison.Ordinal)
                : -1;

            if (mark1 < 0 || mark2 < mark1 || mark3 < mark2 || mark4 < mark3)
            {
                string plainText = source
                    .Replace("%1", string.Empty).Replace("%2", string.Empty)
                    .Replace("%3", string.Empty).Replace("%4", string.Empty);
                if (_text != null) _text.text = plainText;
                InvalidateLayout();
                return;
            }

            string prefix = source.Substring(0, mark1);
            string terms = source.Substring(mark1 + 2, mark2 - mark1 - 2);
            string connector = source.Substring(mark2 + 2, mark3 - mark2 - 2);
            string privacy = source.Substring(mark3 + 2, mark4 - mark3 - 2);
            string suffix = source.Substring(mark4 + 2);

            if (string.IsNullOrEmpty(terms)) terms = termsFallback ?? string.Empty;
            if (string.IsNullOrEmpty(privacy)) privacy = privacyFallback ?? string.Empty;

            if (_text != null)
            {
                _text.richText = true;
                _text.text =
                    prefix +
                    LinkOpen(TermsLinkId) + terms + LinkClose() +
                    connector +
                    LinkOpen(PrivacyLinkId) + privacy + LinkClose() +
                    suffix;
                _text.SetVerticesDirty();
            }
            InvalidateLayout();
        }

        public void InvalidateLayout()
        {
            _layoutDirty = true;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            EnsureLayout();
            int linkIndex = FindLink(eventData.position, eventData.pressEventCamera);
            if (linkIndex < 0)
                return;

            string linkId = _text.textInfo.linkInfo[linkIndex].GetLinkID();
            if (string.Equals(linkId, TermsLinkId, StringComparison.Ordinal))
                _openTerms?.Invoke();
            else if (string.Equals(linkId, PrivacyLinkId, StringComparison.Ordinal))
                _openPrivacy?.Invoke();
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            EnsureLayout();
            return FindLink(screenPoint, eventCamera) >= 0;
        }

        void EnsureLayout()
        {
            if (!_layoutDirty) return;
            _layoutDirty = false;
            ResolveText();
            if (_text != null)
                _text.ForceMeshUpdate(true, true);
        }

        void ResolveText()
        {
            if (_text == null) _text = GetComponent<TextMeshProUGUI>();
        }

        int FindLink(Vector2 screenPoint, Camera eventCamera)
        {
            ResolveText();
            if (_text == null)
                return -1;

            int directHit =
                TMP_TextUtilities.FindIntersectingLink(_text, screenPoint, eventCamera);
            if (directHit >= 0)
                return directHit;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _text.rectTransform,
                    screenPoint,
                    eventCamera,
                    out Vector2 localPoint))
                return -1;

            // Match the legacy control's forgiving 7-design-pixel hit padding.
            // TMP provides stable character and line indices even when a link
            // wraps or switches to a fallback font.
            TMP_TextInfo info = _text.textInfo;
            for (int linkIndex = 0; linkIndex < info.linkCount; linkIndex++)
            {
                TMP_LinkInfo link = info.linkInfo[linkIndex];
                int end = link.linkTextfirstCharacterIndex + link.linkTextLength;
                int currentLine = -1;
                bool hasBounds = false;
                Rect lineBounds = default;

                for (int i = link.linkTextfirstCharacterIndex;
                     i < end && i < info.characterCount;
                     i++)
                {
                    TMP_CharacterInfo character = info.characterInfo[i];
                    if (!character.isVisible)
                        continue;

                    if (currentLine != character.lineNumber)
                    {
                        if (hasBounds && ContainsPadded(lineBounds, localPoint))
                            return linkIndex;
                        currentLine = character.lineNumber;
                        lineBounds = CharacterRect(character);
                        hasBounds = true;
                    }
                    else
                    {
                        lineBounds = Union(lineBounds, CharacterRect(character));
                    }
                }

                if (hasBounds && ContainsPadded(lineBounds, localPoint))
                    return linkIndex;
            }
            return -1;
        }

        static Rect CharacterRect(TMP_CharacterInfo character)
        {
            return Rect.MinMaxRect(
                Mathf.Min(character.bottomLeft.x, character.topRight.x),
                Mathf.Min(character.bottomLeft.y, character.topRight.y),
                Mathf.Max(character.bottomLeft.x, character.topRight.x),
                Mathf.Max(character.bottomLeft.y, character.topRight.y));
        }

        static Rect Union(Rect a, Rect b)
        {
            return Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin),
                Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax),
                Mathf.Max(a.yMax, b.yMax));
        }

        static bool ContainsPadded(Rect rect, Vector2 point)
        {
            rect.xMin -= HitPadding;
            rect.xMax += HitPadding;
            rect.yMin -= HitPadding;
            rect.yMax += HitPadding;
            return rect.Contains(point);
        }

        static string LinkOpen(string id) =>
            $"<link=\"{id}\"><color={LinkColor}>";

        static string LinkClose() => "</color></link>";

#if UNITY_EDITOR
        public void ConfigurePrefabAuthoring(TextMeshProUGUI text)
        {
            _text = text;
        }
#endif
    }
}
