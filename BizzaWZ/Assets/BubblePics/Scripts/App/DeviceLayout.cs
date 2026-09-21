using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Immutable result of the Godot canvas_items/expand viewport calculation.
    /// Insets and viewport dimensions are expressed in design pixels, while
    /// SafeAreaPixels keeps Unity's physical-screen coordinate system.
    /// </summary>
    public readonly struct DeviceLayoutMetrics
    {
        public readonly int ScreenWidthPixels;
        public readonly int ScreenHeightPixels;
        public readonly Rect SafeAreaPixels;
        public readonly float PixelsPerDesignUnit;
        public readonly float ViewWidth;
        public readonly float ViewHeight;
        public readonly float SafeLeft;
        public readonly float SafeTop;
        public readonly float SafeRight;
        public readonly float SafeBottom;

        public float Aspect => ViewHeight > 0f ? ViewWidth / ViewHeight : 0f;
        public float UsableHeight => Mathf.Max(0f, ViewHeight - SafeTop - SafeBottom);

        internal DeviceLayoutMetrics(
            int screenWidthPixels,
            int screenHeightPixels,
            Rect safeAreaPixels,
            float pixelsPerDesignUnit,
            float viewWidth,
            float viewHeight,
            float safeLeft,
            float safeTop,
            float safeRight,
            float safeBottom)
        {
            ScreenWidthPixels = screenWidthPixels;
            ScreenHeightPixels = screenHeightPixels;
            SafeAreaPixels = safeAreaPixels;
            PixelsPerDesignUnit = pixelsPerDesignUnit;
            ViewWidth = viewWidth;
            ViewHeight = viewHeight;
            SafeLeft = safeLeft;
            SafeTop = safeTop;
            SafeRight = safeRight;
            SafeBottom = safeBottom;
        }

        public float FloorY(float navigationHeight)
            => ViewHeight - Mathf.Max(0f, navigationHeight) - SafeBottom;

        internal bool ApproximatelyEquals(DeviceLayoutMetrics other)
        {
            return ScreenWidthPixels == other.ScreenWidthPixels
                && ScreenHeightPixels == other.ScreenHeightPixels
                && Approximately(SafeAreaPixels.x, other.SafeAreaPixels.x)
                && Approximately(SafeAreaPixels.y, other.SafeAreaPixels.y)
                && Approximately(SafeAreaPixels.width, other.SafeAreaPixels.width)
                && Approximately(SafeAreaPixels.height, other.SafeAreaPixels.height)
                && Approximately(PixelsPerDesignUnit, other.PixelsPerDesignUnit)
                && Approximately(ViewWidth, other.ViewWidth)
                && Approximately(ViewHeight, other.ViewHeight)
                && Approximately(SafeLeft, other.SafeLeft)
                && Approximately(SafeTop, other.SafeTop)
                && Approximately(SafeRight, other.SafeRight)
                && Approximately(SafeBottom, other.SafeBottom);
        }

        static bool Approximately(float a, float b) => Mathf.Abs(a - b) <= 0.01f;
    }

    /// <summary>
    /// Single source of truth for viewport and safe-area conversion.
    ///
    /// The reference project uses a 1080x2400 viewport with
    /// canvas_items/stretch-aspect=expand. That means the uniform scale is the
    /// smaller of the width and height scales; the other design axis expands.
    /// </summary>
    public static class DeviceLayout
    {
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 2400f;
        public const float RecordingSafeTop = 62f;
        public const float RecordingAspect = 720f / 1600f;

        static DeviceLayoutMetrics _current = Compute(
            720,
            1600,
            new Rect(0f, 0f, 720f, 1600f));
        static bool _debugSafeAreaEnabled;
        static int _debugSafeTopPixels;
        static int _debugSafeBottomPixels;

        public static DeviceLayoutMetrics Current => _current;
        public static float ViewWidth => _current.ViewWidth;
        public static float ViewHeight => _current.ViewHeight;
        public static float SafeTopDesign => _current.SafeTop;
        public static float SafeBottomDesign => _current.SafeBottom;
        public static float PixelsPerDesignUnit => _current.PixelsPerDesignUnit;
        public static bool DebugSafeAreaEnabled => _debugSafeAreaEnabled;
        public static int DebugSafeTopPixels => _debugSafeTopPixels;
        public static int DebugSafeBottomPixels => _debugSafeBottomPixels;

        /// <summary>
        /// Recomputes from Unity's real viewport. A real device, Device
        /// Simulator, or explicit GM safe area always takes precedence. Desktop
        /// comparison captures intentionally use the complete Godot viewport.
        /// </summary>
        public static bool RefreshFromScreen()
        {
            int width = Mathf.Max(1, Screen.width);
            int height = Mathf.Max(1, Screen.height);
            Rect safe = Screen.safeArea;

            if (_debugSafeAreaEnabled)
            {
                int top = Mathf.Clamp(
                    _debugSafeTopPixels,
                    0,
                    height);
                int bottom = Mathf.Clamp(
                    _debugSafeBottomPixels,
                    0,
                    height - top);
                safe = Rect.MinMaxRect(
                    0f,
                    bottom,
                    width,
                    height - top);
            }

            var next = Compute(width, height, safe);
            if (_current.ApproximatelyEquals(next))
                return false;

            _current = next;
            return true;
        }

        /// <summary>
        /// Session-only physical-pixel safe-area simulation used by the GM
        /// panel. Unity's real Screen.safeArea is restored when cleared.
        /// </summary>
        public static void SetDebugSafeAreaPixels(
            int topPixels,
            int bottomPixels)
        {
            _debugSafeTopPixels = Mathf.Max(0, topPixels);
            _debugSafeBottomPixels = Mathf.Max(0, bottomPixels);
            _debugSafeAreaEnabled = true;
            RefreshFromScreen();
        }

        public static void ClearDebugSafeArea()
        {
            _debugSafeAreaEnabled = false;
            _debugSafeTopPixels = 0;
            _debugSafeBottomPixels = 0;
            RefreshFromScreen();
        }

        /// <summary>
        /// Pure calculation entry point used by editor/CI validation.
        /// SafeAreaPixels follows Unity's bottom-left-origin Screen.safeArea.
        /// Optional fallback insets are design pixels and apply only when the
        /// supplied safe area covers the whole screen.
        /// </summary>
        public static DeviceLayoutMetrics Compute(
            int screenWidthPixels,
            int screenHeightPixels,
            Rect safeAreaPixels,
            float fallbackSafeTopDesign = 0f,
            float fallbackSafeBottomDesign = 0f)
        {
            int width = Mathf.Max(1, screenWidthPixels);
            int height = Mathf.Max(1, screenHeightPixels);
            float scale = Mathf.Min(width / ReferenceWidth, height / ReferenceHeight);
            if (scale <= 0f || float.IsNaN(scale) || float.IsInfinity(scale))
                scale = 1f;

            Rect safe = ClampSafeArea(safeAreaPixels, width, height);
            float safeLeft = safe.xMin / scale;
            float safeBottom = safe.yMin / scale;
            float safeRight = (width - safe.xMax) / scale;
            float safeTop = (height - safe.yMax) / scale;

            if (IsFullSafeArea(safe, width, height))
            {
                safeTop = Mathf.Max(0f, fallbackSafeTopDesign);
                safeBottom = Mathf.Max(0f, fallbackSafeBottomDesign);
            }

            float viewWidth = width / scale;
            float viewHeight = height / scale;
            safeLeft = Mathf.Clamp(safeLeft, 0f, viewWidth);
            safeRight = Mathf.Clamp(safeRight, 0f, viewWidth - safeLeft);
            safeTop = Mathf.Clamp(safeTop, 0f, viewHeight);
            safeBottom = Mathf.Clamp(safeBottom, 0f, viewHeight - safeTop);

            return new DeviceLayoutMetrics(
                width,
                height,
                safe,
                scale,
                viewWidth,
                viewHeight,
                safeLeft,
                safeTop,
                safeRight,
                safeBottom);
        }

        static Rect ClampSafeArea(Rect safe, int width, int height)
        {
            float xMin = Mathf.Clamp(safe.xMin, 0f, width);
            float xMax = Mathf.Clamp(safe.xMax, xMin, width);
            float yMin = Mathf.Clamp(safe.yMin, 0f, height);
            float yMax = Mathf.Clamp(safe.yMax, yMin, height);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        static bool IsFullSafeArea(Rect safe, int width, int height)
        {
            const float epsilon = 0.5f;
            return safe.xMin <= epsilon
                && safe.yMin <= epsilon
                && safe.xMax >= width - epsilon
                && safe.yMax >= height - epsilon;
        }
    }

    /// <summary>Pure bubble-layout rules ported from bubble_field/page.gd.</summary>
    public static class BubbleLayoutMath
    {
        public static float ComputeBaseRadius(
            DeviceLayoutMetrics layout,
            float perRowScale = 1f)
        {
            float scale = Mathf.Max(perRowScale, 0.0001f);
            float aspect = layout.ViewHeight > 0f
                ? layout.ViewWidth / layout.ViewHeight
                : 0f;
            if (aspect <= BubbleField.ASPECT_CAP_W_OVER_H)
                return layout.ViewWidth * BubbleField.BASE_RADIUS_RATIO / scale;

            float perRowAtCap =
                BubbleField.ASPECT_CAP_W_OVER_H /
                BubbleField.BASE_RADIUS_RATIO;
            float t =
                (aspect - BubbleField.ASPECT_CAP_W_OVER_H) /
                (1f - BubbleField.ASPECT_CAP_W_OVER_H);
            float perRow = Mathf.LerpUnclamped(
                perRowAtCap,
                BubbleField.WIDE_PER_ROW_AT_SQUARE,
                t);
            perRow = Mathf.Max(
                perRowAtCap,
                Mathf.Round(perRow - 0.5f) + 0.5f);
            perRow *= scale;
            return layout.ViewWidth / (2f * perRow);
        }

        public static float ComputeDynamicPerRow(
            DeviceLayoutMetrics layout,
            int peakBubbleCount,
            bool newGameUi)
        {
            float basePerRow;
            if (newGameUi)
            {
                basePerRow = peakBubbleCount <= 25
                    ? 3.8f
                    : peakBubbleCount <= 30 ? 4.2f : 4.5f;
            }
            else
            {
                basePerRow = peakBubbleCount <= 22
                    ? 3.8f
                    : peakBubbleCount <= 26 ? 4.2f : 4.5f;
            }

            float usableHeight = layout.UsableHeight;
            float aspect = usableHeight > 0f
                ? layout.ViewWidth / usableHeight
                : 0f;
            float wideOffset = 0f;
            if (aspect > BubbleField.ASPECT_CAP_W_OVER_H)
            {
                float perRowAtCap =
                    BubbleField.ASPECT_CAP_W_OVER_H /
                    BubbleField.BASE_RADIUS_RATIO;
                float t =
                    (aspect - BubbleField.ASPECT_CAP_W_OVER_H) /
                    (1f - BubbleField.ASPECT_CAP_W_OVER_H);
                float widePerRow = Mathf.LerpUnclamped(
                    perRowAtCap,
                    BubbleField.WIDE_PER_ROW_AT_SQUARE,
                    t);
                wideOffset = widePerRow - perRowAtCap;
            }

            float raw = basePerRow + wideOffset;
            float baseInteger = Mathf.Floor(raw);
            float[] candidates =
            {
                baseInteger + 0.2f,
                baseInteger + 0.5f,
                baseInteger + 0.8f,
                baseInteger + 1.2f
            };
            float perRow = candidates[0];
            foreach (float candidate in candidates)
            {
                if (Mathf.Abs(candidate - raw) < Mathf.Abs(perRow - raw))
                    perRow = candidate;
            }
            return Mathf.Clamp(perRow, 3.8f, 6.5f);
        }
    }
}
