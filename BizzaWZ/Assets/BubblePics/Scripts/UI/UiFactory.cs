using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>Helpers to build UGUI hierarchies in code. Design space: 1080x2400, origin center.</summary>
    public static class UiFactory
    {
        public const float DesignW = 1080f;
        public const float DesignH = 2400f;

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            return rt;
        }

        public static RectTransform FullStretch(Transform parent, string name)
        {
            var rt = Node(parent, name);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Position using design coords with y-down from screen center (dx right, dy down).</summary>
        public static void SetPos(RectTransform rt, float dx, float dy)
        {
            rt.anchoredPosition = new Vector2(dx, -dy);
        }

        public static Image Img(Transform parent, string name, string spritePath, float w = 0, float h = 0)
        {
            var rt = Node(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            if (!string.IsNullOrEmpty(spritePath))
            {
                img.sprite = AssetLib.Sprite(spritePath);
                if (img.sprite != null && w <= 0 && h <= 0)
                    img.SetNativeSize();
            }
            if (w > 0 || h > 0)
                rt.sizeDelta = new Vector2(w > 0 ? w : rt.sizeDelta.x, h > 0 ? h : rt.sizeDelta.y);
            img.raycastTarget = false;
            return img;
        }

        public static Image Rect(Transform parent, string name, Color color, float w, float h)
        {
            var rt = Node(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            rt.sizeDelta = new Vector2(w, h);
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Label(
            Transform parent,
            string name,
            string text,
            int size,
            Color color,
            TMP_FontAsset font = null,
            TextAnchor anchor = TextAnchor.MiddleCenter,
            float w = 600,
            float h = 100)
        {
            var rt = Node(parent, name);
            rt.sizeDelta = new Vector2(w, h);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font != null ? font : AssetLib.UiFont;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = ToTmpAlignment(anchor);
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.richText = true;
            t.enableKerning = true;
            return t;
        }

        public static void AddOutline(TMP_Text text, Color color, float pixels = 2f)
        {
            TmpTextStyle.ApplyOutline(text, color, pixels);
        }

        public static void AddShadow(
            TMP_Text text,
            Color color,
            Vector2 offsetPixels,
            float dilatePixels = 0f,
            float softnessPixels = 0f)
        {
            TmpTextStyle.ApplyShadow(
                text,
                color,
                offsetPixels,
                dilatePixels,
                softnessPixels);
        }

        static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                    return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter:
                    return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight:
                    return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft:
                    return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight:
                    return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft:
                    return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter:
                    return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight:
                    return TextAlignmentOptions.BottomRight;
                default:
                    return TextAlignmentOptions.Center;
            }
        }

        /// <summary>Clickable behaviour with the standard press animation and click sound.</summary>
        public static void MakeButton(
            GameObject go,
            Action onClick,
            bool pressAnim = true,
            bool sound = true,
            bool haptic = true)
        {
            var img = go.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
            var btn = go.AddComponent<PressButtonPrefab>();
            btn.OnClick = onClick;
            btn.PressAnim = pressAnim;
            btn.PlaySound = sound;
            btn.PlayHaptic = haptic;
        }
    }

    /// <summary>Button with Godot-style press pulse (0.9 -> 1.03 -> 1.0).</summary>
    public class PressButton : Button
    {
        public Action OnClick;
        public bool PressAnim = true;
        public bool PlaySound = true;
        public bool PlayHaptic = true;
        public bool Interactable { get => interactable; set => interactable = value; }
        Coroutine _anim;

        public override void OnPointerDown(PointerEventData e)
        {
            base.OnPointerDown(e);
            if (!Interactable) return;
            if (PlaySound && SoundManager.I != null)
                SoundManager.I.Play("button");
            if (PlayHaptic)
                Haptics.Play(HapticLevel.VeryWeak);
            if (PressAnim)
            {
                if (_anim != null) StopCoroutine(_anim);
                _anim = StartCoroutine(Tween.Scale(transform, Vector3.one * 0.9f, 4f / 60f, Ease.OutQuad));
            }
        }

        public override void OnPointerUp(PointerEventData e)
        {
            base.OnPointerUp(e);
            if (!Interactable) return;
            if (PressAnim)
            {
                if (_anim != null) StopCoroutine(_anim);
                _anim = StartCoroutine(ReleaseAnim());
            }
        }

        System.Collections.IEnumerator ReleaseAnim()
        {
            yield return Tween.Scale(transform, Vector3.one * 1.03f, 8f / 60f, Ease.OutQuad);
            yield return Tween.Scale(transform, Vector3.one * 1.0f, 10f / 60f, Ease.OutQuad);
        }

        public override void OnPointerClick(PointerEventData e)
        {
            if (!IsActive() || !IsInteractable() || e.button != PointerEventData.InputButton.Left) return;
            base.OnPointerClick(e);
            OnClick?.Invoke();
        }

        /// <summary>Programmatic click used by the autoplay driver (with animation + sound).</summary>
        public void SimulateClick()
        {
            if (!Interactable) return;
            StartCoroutine(SimulateCo());
        }

        System.Collections.IEnumerator SimulateCo()
        {
            if (PlaySound && SoundManager.I != null)
                SoundManager.I.Play("button");
            if (PlayHaptic)
                Haptics.Play(HapticLevel.VeryWeak);
            if (PressAnim) yield return Tween.Scale(transform, Vector3.one * 0.9f, 4f / 60f, Ease.OutQuad);
            OnClick?.Invoke();
            if (PressAnim && this != null && transform != null)
            {
                yield return Tween.Scale(transform, Vector3.one * 1.03f, 8f / 60f, Ease.OutQuad);
                yield return Tween.Scale(transform, Vector3.one * 1.0f, 10f / 60f, Ease.OutQuad);
            }
        }
    }
}
