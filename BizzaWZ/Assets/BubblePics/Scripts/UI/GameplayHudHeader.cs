using UnityEngine;

namespace BubblePics
{
    /// <summary>Mounts the authored currency/status header and bottom entry row.</summary>
    public sealed class GameplayHudHeader : MonoBehaviour
    {
        [SerializeField] RectTransform _statusMount;
        [SerializeField] RectTransform _currencyRow;
        [SerializeField] RectTransform _entryRow;
        [SerializeField, Min(1f)] float _referenceWidth = 1080f;
        [SerializeField, Min(0f)] float _topPadding = 24f;
        [SerializeField, Min(1f)] float _maximumScale = 1f;

        public RectTransform Root => (RectTransform)transform;
        public RectTransform StatusMount => _statusMount;
        public RectTransform CurrencyRow => _currencyRow;
        public RectTransform EntryRow => _entryRow;
        public float LayoutScale { get; private set; } = 1f;
        public float StatusTopDesign => _topDesign - _statusMount.anchoredPosition.y * LayoutScale;
        float _centerDesignX;
        float _topDesign;

        public void Mount(RectTransform hudRoot, TopGameBar status, ToolbarView toolbar)
        {
            if (Root.parent != hudRoot) Root.SetParent(hudRoot, false);
            ApplyDeviceLayout(DeviceLayout.Current);
            status.MountHeader(this);
            MountEntries(toolbar);
        }

        public void MountEntries(ToolbarView toolbar)
        {
            if (_entryRow.parent != toolbar.Root) _entryRow.SetParent(toolbar.Root, false);
        }

        // Called only when mounting or when viewport/safe-area metrics change.
        public void ApplyDeviceLayout(DeviceLayoutMetrics layout)
        {
            float availableWidth = Mathf.Max(1f, layout.ViewWidth - layout.SafeLeft - layout.SafeRight);
            LayoutScale = Mathf.Min(_maximumScale, availableWidth / _referenceWidth);
            float centerOffset = (layout.SafeLeft - layout.SafeRight) * 0.5f;
            _centerDesignX = layout.ViewWidth * 0.5f + centerOffset;
            _topDesign = layout.SafeTop + _topPadding * LayoutScale;
            Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _referenceWidth);
            Root.localScale = Vector3.one * LayoutScale;
            Root.anchoredPosition = new Vector2(centerOffset, -_topDesign);
        }

        public Vector2 StatusToDesign(Vector2 localTopDown)
        {
            return new Vector2(
                _centerDesignX + (_statusMount.anchoredPosition.x + localTopDown.x) * LayoutScale,
                StatusTopDesign + localTopDown.y * LayoutScale);
        }
    }
}
