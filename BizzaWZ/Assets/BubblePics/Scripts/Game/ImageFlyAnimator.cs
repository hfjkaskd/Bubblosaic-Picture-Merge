using System.Collections;
using System.Collections.Generic;
using BubblePics.GameModes;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>
    /// Port of bubble_image_fly_animator.gd — completed image grows in a card
    /// frame then flies to the target pill on the top bar.
    /// Runs on the HUD canvas in screen space.
    /// </summary>
    public class ImageFlyAnimator : MonoBehaviour
    {
        const float FLY_PHOTO_CORNER_RATIO = 0.04f;
        const float FRAME_INNER_RATIO = 640f / 756f;
        const float PHOTO_OVERFLOW = 1.03f;
        const float GROW_PEAK = 1.45f;
        const float FLY_DUR = 0.75f;
        const float ROT_FINISH_RATIO = 0.7f;

        public BubblePage Page;
        bool _flying;
        public bool IsFlying => _flying;
        readonly System.Collections.Generic.List<GameObject> _activeNodes = new System.Collections.Generic.List<GameObject>();

        static Canvas hudCanvas;
        static Camera HudCamera
        {
            get
            {
                if (hudCanvas == null) hudCanvas = App.I.HudRoot.GetComponentInParent<Canvas>().rootCanvas;
                return hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : hudCanvas.worldCamera;
            }
        }

        public static Vector3 WorldToHudPosition(Vector3 worldPosition)
        {
            Vector2 screen = App.I.Cam.WorldToScreenPoint(worldPosition);
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(App.I.HudRoot, screen, HudCamera, out Vector3 position))
                throw new System.InvalidOperationException("Could not project the collected image onto the gameplay HUD.");
            return position;
        }

        static Vector3 HudToWorldPosition(Vector3 hudPosition)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(HudCamera, hudPosition);
            float depth = App.I.Cam.WorldToScreenPoint(Vector3.zero).z;
            return App.I.Cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
        }

        public void Cancel()
        {
            StopAllCoroutines();
            foreach (var n in _activeNodes) if (n != null) Destroy(n);
            _activeNodes.Clear();
            _flying = false;
        }

        public void Play(Texture2D flyTex, Vector3 startWorld, float bubbleD, int slotIdx, float startRot,
            int imageId, bool useEnhancedGrow = false, bool rainbowShine = false)
        {
            if (slotIdx < 0) return;
            if (CategoryMatchContent.IsCategoryImage(imageId))
            {
                StartCoroutine(PlayCategoryCo(
                    startWorld,
                    bubbleD,
                    slotIdx,
                    startRot,
                    imageId,
                    rainbowShine));
                return;
            }
            if (WordMatchContent.IsWordImage(imageId))
            {
                StartCoroutine(PlayWordCo(
                    startWorld,
                    bubbleD,
                    slotIdx,
                    startRot,
                    imageId,
                    rainbowShine));
                return;
            }
            StartCoroutine(PlayCo(
                flyTex,
                startWorld,
                bubbleD,
                slotIdx,
                startRot,
                imageId,
                useEnhancedGrow,
                rainbowShine));
        }

        IEnumerator PlayCategoryCo(
            Vector3 startWorld,
            float bubbleD,
            int slotIdx,
            float startRot,
            int imageId,
            bool rainbowShine)
        {
            _flying = true;
            float flySize = bubbleD;

            var fly = new GameObject("CategoryFly");
            fly.transform.SetParent(App.I.HudRoot, false);
            var flyRect = fly.AddComponent<RectTransform>();
            flyRect.sizeDelta = new Vector2(flySize, flySize);
            flyRect.localRotation = Quaternion.Euler(0f, 0f, startRot);
            var canvasGroup = fly.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            var nestedCanvas = fly.AddComponent<Canvas>();
            nestedCanvas.overrideSorting = true;
            nestedCanvas.sortingOrder = 1000;
            _activeNodes.Add(fly);

            var backgroundObject = new GameObject("CategoryDisc");
            backgroundObject.transform.SetParent(fly.transform, false);
            var background = backgroundObject.AddComponent<RawImage>();
            background.texture =
                AssetLib.Texture("Art/Sprites/Bubble/gp_pic_bubble_number");
            background.raycastTarget = false;
            RectTransform backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            var quadrants = new List<int> { 0, 1, 2, 3 };
            float cell = CategoryMatchContent.FormationCell(
                quadrants,
                bubbleD * 0.5f,
                4);
            float iconSize = CategoryMatchContent.IconDisplaySize(cell, 4);
            var iconRects = new List<RectTransform>(4);
            for (int slot = 0; slot < 4; slot++)
            {
                Texture2D iconTexture = CategoryMatchContent.IconFor(
                    imageId,
                    slot);
                if (iconTexture == null) continue;
                var iconObject = new GameObject("CategoryIcon" + slot);
                iconObject.transform.SetParent(fly.transform, false);
                var icon = iconObject.AddComponent<RawImage>();
                icon.texture = iconTexture;
                icon.raycastTarget = false;
                RectTransform iconRect = icon.rectTransform;
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(iconSize, iconSize);
                var aspect = iconObject.AddComponent<AspectRatioFitter>();
                aspect.aspectRatio = iconTexture.width /
                                     (float)Mathf.Max(1, iconTexture.height);
                aspect.aspectMode = iconTexture.width >= iconTexture.height
                    ? AspectRatioFitter.AspectMode.WidthControlsHeight
                    : AspectRatioFitter.AspectMode.HeightControlsWidth;
                Vector2 offset = CategoryMatchContent.FormationOffset(
                    slot,
                    quadrants,
                    cell);
                iconRect.anchoredPosition = new Vector2(offset.x, -offset.y);
                iconRects.Add(iconRect);
            }

            var label = UiFactory.Label(
                fly.transform,
                "CategoryLabel",
                CategoryMatchContent.LabelFor(imageId),
                Mathf.RoundToInt(bubbleD * 0.3f),
                Color.white,
                AssetLib.NumFont,
                TextAnchor.MiddleCenter,
                bubbleD * 0.82f,
                bubbleD * 0.55f);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Max(12f, bubbleD * 0.12f);
            label.fontSizeMax = Mathf.Max(18f, bubbleD * 0.3f);
            TmpTextStyle.ApplyOutline(
                label,
                new Color(0f, 0.16863f, 0.33725f, 0.4f),
                Mathf.Max(1f, bubbleD * 0.036f));
            labelRect.localScale = Vector3.zero;

            flyRect.position = WorldToHudPosition(startWorld);

            float elapsed = 0f;
            const float fadeDuration = 0.2f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }

            const float iconStagger = 0.07f;
            const float iconShrinkDuration = 0.15f;
            float shrinkTotal = iconShrinkDuration +
                                iconStagger * Mathf.Max(0, iconRects.Count - 1);
            elapsed = 0f;
            while (elapsed < shrinkTotal)
            {
                elapsed += Time.deltaTime;
                for (int i = 0; i < iconRects.Count; i++)
                {
                    float local = Mathf.Clamp01(
                        (elapsed - i * iconStagger) / iconShrinkDuration);
                    float eased = Tween.Evaluate(Ease.InBack, local);
                    iconRects[i].localScale =
                        Vector3.one * Mathf.LerpUnclamped(1f, 0f, eased);
                }
                yield return null;
            }

            elapsed = 0f;
            const float labelPopDuration = 0.22f;
            while (elapsed < labelPopDuration)
            {
                elapsed += Time.deltaTime;
                float eased = Tween.Evaluate(
                    Ease.OutBack,
                    Mathf.Clamp01(elapsed / labelPopDuration));
                labelRect.localScale = Vector3.one * eased;
                yield return null;
            }

            elapsed = 0f;
            const float miniDuration = 0.18f;
            while (elapsed < miniDuration)
            {
                elapsed += Time.deltaTime;
                float eased = Tween.Evaluate(
                    Ease.InCubic,
                    Mathf.Clamp01(elapsed / miniDuration));
                flyRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.1f, eased);
                yield return null;
            }

            ParticleSystem trail = MakeTrail(startWorld, bubbleD);
            _activeNodes.Add(trail.gameObject);
            Vector3 fromPosition = flyRect.position;
            Vector3 toPosition = Page.TopBar.TargetCard.position;
            float targetWidth = Page.TopBar.TargetCard != null
                ? Page.TopBar.TargetCard.sizeDelta.x * 0.34f
                : flySize * 0.1f;
            float targetScale = targetWidth / Mathf.Max(1f, flySize);
            Vector3 fromScale = flyRect.localScale;
            Vector3 toScale = Vector3.one * targetScale;
            float fromRotation = flyRect.localEulerAngles.z;
            if (fromRotation > 180f) fromRotation -= 360f;

            elapsed = 0f;
            while (elapsed < FLY_DUR)
            {
                elapsed += Time.deltaTime;
                float raw = Mathf.Clamp01(elapsed / FLY_DUR);
                float positionProgress = 1f - Mathf.Pow(1f - raw, 5f);
                flyRect.position = Vector3.LerpUnclamped(
                    fromPosition,
                    toPosition,
                    positionProgress);
                flyRect.localScale = Vector3.LerpUnclamped(
                    fromScale,
                    toScale,
                    Tween.Evaluate(Ease.InQuart, raw));
                float rotationProgress = Mathf.Clamp01(
                    raw / ROT_FINISH_RATIO);
                flyRect.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.LerpAngle(
                        fromRotation,
                        0f,
                        Tween.Evaluate(Ease.OutQuad, rotationProgress)));
                if (raw > 0.6f)
                    canvasGroup.alpha = 1f - (raw - 0.6f) / 0.4f;
                if (trail != null) trail.transform.position = HudToWorldPosition(flyRect.position);
                yield return null;
            }

            yield return new WaitForSeconds(0.12f);
            Page.TopBar.FillCollected(slotIdx);
            if (rainbowShine) Page.TopBar.PlayLastLinkShine();
            Fx.Vibrate(1);
            _activeNodes.Remove(fly);
            Destroy(fly);
            if (trail != null)
            {
                _activeNodes.Remove(trail.gameObject);
                var emission = trail.emission;
                emission.enabled = false;
                Destroy(trail.gameObject, 0.65f);
            }
            _flying = false;
            Page.NotifyImageLanded();
        }

        IEnumerator PlayWordCo(
            Vector3 startWorld,
            float bubbleD,
            int slotIdx,
            float startRot,
            int imageId,
            bool rainbowShine)
        {
            _flying = true;
            float flySize = bubbleD;

            var fly = new GameObject("WordFly");
            fly.transform.SetParent(App.I.HudRoot, false);
            var flyRect = fly.AddComponent<RectTransform>();
            flyRect.sizeDelta = new Vector2(flySize, flySize);
            flyRect.localRotation = Quaternion.Euler(0f, 0f, startRot);
            var canvasGroup = fly.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            var nestedCanvas = fly.AddComponent<Canvas>();
            nestedCanvas.overrideSorting = true;
            nestedCanvas.sortingOrder = 1000;
            _activeNodes.Add(fly);

            var backgroundObject = new GameObject("WordDisc");
            backgroundObject.transform.SetParent(fly.transform, false);
            var background = backgroundObject.AddComponent<RawImage>();
            background.texture =
                AssetLib.Texture("Art/Sprites/Bubble/gp_pic_bubble_number");
            background.raycastTarget = false;
            RectTransform backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            var quadrants = new List<int> { 0, 1, 2, 3 };
            float cell = WordMatchContent.FormationCell(
                quadrants,
                bubbleD * 0.5f,
                4);
            var wordRects = new List<RectTransform>(4);
            for (int slot = 0; slot < 4; slot++)
            {
                var word = UiFactory.Label(
                    fly.transform,
                    "Word" + slot,
                    WordMatchContent.WordFor(imageId, slot),
                    Mathf.RoundToInt(cell * 0.37f),
                    Color.white,
                    AssetLib.NumFont,
                    TextAnchor.MiddleCenter,
                    cell * 0.94f,
                    cell * 0.7f);
                RectTransform wordRect = word.rectTransform;
                wordRect.anchorMin = wordRect.anchorMax =
                    new Vector2(0.5f, 0.5f);
                wordRect.pivot = new Vector2(0.5f, 0.5f);
                Vector2 offset = WordMatchContent.FormationOffset(
                    slot,
                    quadrants,
                    cell);
                wordRect.anchoredPosition = new Vector2(offset.x, -offset.y);
                word.enableAutoSizing = true;
                word.fontSizeMin = Mathf.Max(10f, cell * 0.14f);
                word.fontSizeMax = Mathf.Max(18f, cell * 0.37f);
                TmpTextStyle.ApplyOutline(
                    word,
                    new Color(0f, 0.16863f, 0.33725f, 0.4f),
                    Mathf.Max(1f, cell * 0.0444f));
                wordRects.Add(wordRect);
            }

            var label = UiFactory.Label(
                fly.transform,
                "WordCategoryLabel",
                WordMatchContent.LabelFor(imageId),
                Mathf.RoundToInt(bubbleD * 0.3f),
                Color.white,
                AssetLib.NumFont,
                TextAnchor.MiddleCenter,
                bubbleD * 0.82f,
                bubbleD * 0.55f);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax =
                new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Max(12f, bubbleD * 0.12f);
            label.fontSizeMax = Mathf.Max(18f, bubbleD * 0.3f);
            TmpTextStyle.ApplyOutline(
                label,
                new Color(0f, 0.16863f, 0.33725f, 0.4f),
                Mathf.Max(1f, bubbleD * 0.036f));
            labelRect.localScale = Vector3.zero;

            flyRect.position = WorldToHudPosition(startWorld);

            float elapsed = 0f;
            const float fadeDuration = 0.2f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }

            const float wordStagger = 0.07f;
            const float wordShrinkDuration = 0.15f;
            float shrinkTotal = wordShrinkDuration +
                                wordStagger * Mathf.Max(0, wordRects.Count - 1);
            elapsed = 0f;
            while (elapsed < shrinkTotal)
            {
                elapsed += Time.deltaTime;
                for (int i = 0; i < wordRects.Count; i++)
                {
                    float local = Mathf.Clamp01(
                        (elapsed - i * wordStagger) / wordShrinkDuration);
                    float eased = Tween.Evaluate(Ease.InBack, local);
                    wordRects[i].localScale = Vector3.one *
                        Mathf.LerpUnclamped(1f, 0f, eased);
                }
                yield return null;
            }

            elapsed = 0f;
            const float labelPopDuration = 0.22f;
            while (elapsed < labelPopDuration)
            {
                elapsed += Time.deltaTime;
                float eased = Tween.Evaluate(
                    Ease.OutBack,
                    Mathf.Clamp01(elapsed / labelPopDuration));
                labelRect.localScale = Vector3.one * eased;
                yield return null;
            }

            elapsed = 0f;
            const float miniDuration = 0.18f;
            while (elapsed < miniDuration)
            {
                elapsed += Time.deltaTime;
                float eased = Tween.Evaluate(
                    Ease.InCubic,
                    Mathf.Clamp01(elapsed / miniDuration));
                flyRect.localScale = Vector3.one *
                    Mathf.Lerp(1f, 0.1f, eased);
                yield return null;
            }

            ParticleSystem trail = MakeTrail(startWorld, bubbleD);
            _activeNodes.Add(trail.gameObject);
            Vector3 fromPosition = flyRect.position;
            Vector3 toPosition = Page.TopBar.TargetCard.position;
            float targetWidth = Page.TopBar.TargetCard != null
                ? Page.TopBar.TargetCard.sizeDelta.x * 0.34f
                : flySize * 0.1f;
            float targetScale = targetWidth / Mathf.Max(1f, flySize);
            Vector3 fromScale = flyRect.localScale;
            Vector3 toScale = Vector3.one * targetScale;
            float fromRotation = flyRect.localEulerAngles.z;
            if (fromRotation > 180f) fromRotation -= 360f;

            elapsed = 0f;
            while (elapsed < FLY_DUR)
            {
                elapsed += Time.deltaTime;
                float raw = Mathf.Clamp01(elapsed / FLY_DUR);
                float positionProgress = 1f - Mathf.Pow(1f - raw, 5f);
                flyRect.position = Vector3.LerpUnclamped(
                    fromPosition,
                    toPosition,
                    positionProgress);
                flyRect.localScale = Vector3.LerpUnclamped(
                    fromScale,
                    toScale,
                    Tween.Evaluate(Ease.InQuart, raw));
                float rotationProgress = Mathf.Clamp01(
                    raw / ROT_FINISH_RATIO);
                flyRect.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.LerpAngle(
                        fromRotation,
                        0f,
                        Tween.Evaluate(Ease.OutQuad, rotationProgress)));
                if (raw > 0.6f)
                    canvasGroup.alpha = 1f - (raw - 0.6f) / 0.4f;
                if (trail != null) trail.transform.position = HudToWorldPosition(flyRect.position);
                yield return null;
            }

            yield return new WaitForSeconds(0.12f);
            Page.TopBar.FillCollected(slotIdx);
            if (rainbowShine) Page.TopBar.PlayLastLinkShine();
            Fx.Vibrate(1);
            _activeNodes.Remove(fly);
            Destroy(fly);
            if (trail != null)
            {
                _activeNodes.Remove(trail.gameObject);
                var emission = trail.emission;
                emission.enabled = false;
                Destroy(trail.gameObject, 0.65f);
            }
            _flying = false;
            Page.NotifyImageLanded();
        }

        IEnumerator PlayCo(Texture2D flyTex, Vector3 startWorld, float bubbleD, int slotIdx, float startRot,
            int imageId, bool useEnhancedGrow, bool rainbowShine)
        {
            _flying = true;

            float inscribedD = bubbleD * BubbleView.INSCRIBE_RATIO_BY_COUNT[4] / Mathf.Sqrt(2f);
            float frameSize = inscribedD / FRAME_INNER_RATIO;

            var fly = new GameObject("FlyImage");
            fly.transform.SetParent(App.I.HudRoot, false);
            var flyRt = fly.AddComponent<RectTransform>();
            flyRt.sizeDelta = new Vector2(frameSize, frameSize);
            var flyCanvasGroup = fly.AddComponent<CanvasGroup>();
            flyCanvasGroup.alpha = 0f;
            flyCanvasGroup.blocksRaycasts = false;
            _activeNodes.Add(fly);
            // put above panels/bubbles but below dialogs (z=100 in Godot page)
            var nestedCanvas = fly.AddComponent<Canvas>();
            nestedCanvas.overrideSorting = true;
            nestedCanvas.sortingOrder = 1000;

            // frame (behind photo; opaque white card with border)
            var frameGo = new GameObject("Frame");
            frameGo.transform.SetParent(fly.transform, false);
            var frame = frameGo.AddComponent<Image>();
            frame.sprite = AssetLib.Sprite("Art/Sprites/Bubble/fly_card_frame");
            frame.raycastTarget = false;
            var frameRt = frame.rectTransform;
            frameRt.anchorMin = Vector2.zero; frameRt.anchorMax = Vector2.one;
            frameRt.offsetMin = Vector2.zero; frameRt.offsetMax = Vector2.zero;
            frame.color = new Color(1, 1, 1, 0);

            // photo (on top of frame)
            float photoSize = inscribedD * PHOTO_OVERFLOW;
            var photoGo = new GameObject("Photo");
            photoGo.transform.SetParent(fly.transform, false);
            var photo = photoGo.AddComponent<RawImage>();
            bool numberMode = NumberMatchContent.IsNumberImage(imageId);
            photo.texture = numberMode
                ? AssetLib.Texture("Art/Sprites/Bubble/gp_pic_bubble_number")
                : flyTex;
            photo.raycastTarget = false;
            var photoRt = photo.rectTransform;
            photoRt.sizeDelta = new Vector2(photoSize, photoSize);
            var photoMat = new Material(Shader.Find("BubblePics/FragmentImage"));
            photoMat.SetFloat("_CornerRadius", FLY_PHOTO_CORNER_RATIO);
            photoMat.SetFloat("_CornerMask", 15);
            photoMat.SetFloat("_FillCornerMask", 0);
            photoMat.SetFloat("_EdgeMask", 15);
            photoMat.SetVector("_UvRegion", new Vector4(0, 0, 1, 1));
            photo.material = photoMat;
            if (numberMode)
            {
                var result = UiFactory.Label(
                    photoGo.transform,
                    "NumberResult",
                    NumberMatchContent.ResultFor(imageId).ToString(),
                    Mathf.RoundToInt(photoSize * 0.43f),
                    new Color(1f, 0.88235f, 0.00784f, 1f),
                    AssetLib.NumFont,
                    TextAnchor.MiddleCenter,
                    photoSize,
                    photoSize);
                TmpTextStyle.ApplyOutline(
                    result,
                    new Color(0f, 0.16863f, 0.33725f, 0.4f),
                    Mathf.RoundToInt(photoSize * 0.052f));
            }

            // position: convert world start to hud position
            flyRt.position = WorldToHudPosition(startWorld);
            flyRt.localRotation = Quaternion.Euler(0, 0, startRot);
            flyRt.localScale = Vector3.one;

            // trail particles in world space
            var trail = MakeTrail(startWorld, bubbleD);
            _activeNodes.Add(trail.gameObject);

            // fade in 0.06
            float t = 0;
            while (t < 0.06f)
            {
                t += Time.deltaTime;
                flyCanvasGroup.alpha = Mathf.Clamp01(t / 0.06f);
                yield return null;
            }

            // grow
            bool useNewGrow = (AppConfig.CompleteAnimationNew || useEnhancedGrow);
            float growDur = useNewGrow ? 0.25f : 0.42f;
            var growEase = useNewGrow ? Ease.OutCubic : Ease.InCubic;
            float frameFadeDur = useNewGrow ? growDur : growDur * 0.6f;
            StartCoroutine(Tween.Run(frameFadeDur, k => frame.color = new Color(1, 1, 1, k), Ease.OutQuad));
            t = 0;
            Vector3 growFrom = Vector3.one;
            Vector3 growTo = Vector3.one * GROW_PEAK;
            while (t < growDur)
            {
                t += Time.deltaTime;
                float k = Tween.Evaluate(growEase, Mathf.Clamp01(t / growDur));
                flyRt.localScale = Vector3.LerpUnclamped(growFrom, growTo, k);
                if (trail != null) trail.transform.position = HudToWorldPosition(flyRt.position);
                yield return null;
            }

            // flight to target pill
            Vector3 fromPos = flyRt.position;
            Vector3 toPos = Page.TopBar.TargetCard.position;
            float dstScaleRatio = (Page.TopBar.TargetCard.sizeDelta.x * 0.34f * App.HudScale) / (frameSize * App.HudScale * GROW_PEAK);
            Vector3 fromScale = flyRt.localScale;
            Vector3 toScale = fromScale * dstScaleRatio;
            float fromRot = flyRt.localEulerAngles.z;
            if (fromRot > 180) fromRot -= 360;

            t = 0;
            while (t < FLY_DUR)
            {
                t += Time.deltaTime;
                float raw = Mathf.Clamp01(t / FLY_DUR);
                float p = 1f - Mathf.Pow(1f - raw, 5f);
                flyRt.position = Vector3.LerpUnclamped(fromPos, toPos, p);
                float sk = Tween.Evaluate(Ease.InQuart, raw);
                flyRt.localScale = Vector3.LerpUnclamped(fromScale, toScale, sk);
                float rotK = Mathf.Clamp01(raw / ROT_FINISH_RATIO);
                flyRt.localRotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(fromRot, 0, Tween.Evaluate(Ease.OutQuad, rotK)));
                if (raw > 0.6f) flyCanvasGroup.alpha = 1f - (raw - 0.6f) / 0.4f;
                if (trail != null) trail.transform.position = HudToWorldPosition(flyRt.position);
                yield return null;
            }

            yield return new WaitForSeconds(0.12f);
            Page.TopBar.FillCollected(slotIdx);
            if (rainbowShine)
                Page.TopBar.PlayLastLinkShine();
            Fx.Vibrate(1);
            _activeNodes.Remove(fly);
            Destroy(fly);
            if (trail != null)
            {
                var em = trail.emission; em.enabled = false;
                Destroy(trail.gameObject, 0.65f);
            }
            _flying = false;
            Page.NotifyImageLanded();
        }

        ParticleSystem MakeTrail(Vector3 pos, float bubbleD)
        {
            var go = new GameObject("FlyTrail");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.material = new Material(Shader.Find("BubblePics/SpineLite"))
            {
                mainTexture = AssetLib.Texture("Art/Sprites/Bubble/ambient_bubble")
            };
            psr.sortingOrder = 990;
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.55f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(8, 35);
            float texW = AssetLib.Texture("Art/Sprites/Bubble/ambient_bubble").width;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f * texW, 0.13f * texW);
            main.startRotation = new ParticleSystem.MinMaxCurve(-45 * Mathf.Deg2Rad, 45 * Mathf.Deg2Rad);
            main.maxParticles = 60;
            var em = ps.emission;
            em.rateOverTime = 22f / 0.55f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = bubbleD * 0.18f;
            var lim = ps.limitVelocityOverLifetime;
            lim.enabled = true; lim.drag = 2.2f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0.4f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            ps.Play();
            return ps;
        }
    }
}
