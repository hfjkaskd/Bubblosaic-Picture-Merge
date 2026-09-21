namespace BubblePics
{
    /// <summary>Central sorting-order bands (Godot z_index emulation).
    /// World background 0..99; bubbles 100..619 ((1+z)*20+intra, z capped);
    /// HUD canvas 700 (world bubbles always below, like Godot's control layer);
    /// closure fly card / rings 990..1000 (Godot ClosureFxCanvasLayer=100);
    /// panels 1500; combo words 1600; waves 2500; dialogs 2600.</summary>
    public static class SortOrder
    {
        public const int Hud = 700;
        // The Godot dolphin has z_index=0 while the two HUD cards use z_index=2,
        // so its normal pose must stay just below the HUD canvas. Completion
        // raises it independently to DolphinDecoration.COMPLETE_TOP_Z_INDEX.
        public const int DolphinNormal = Hud - 1;

        /// <summary>Bubble child order: container z=1 with per-bubble z offset.
        /// Clamped so even boosted bubbles never cover the HUD canvas.</summary>
        public static int BubbleBand(int bubbleZ, int intra)
        {
            if (bubbleZ < 0) bubbleZ = 0;
            if (bubbleZ > 25) bubbleZ = 25;
            return 100 + bubbleZ * 20 + intra;
        }
    }
}
