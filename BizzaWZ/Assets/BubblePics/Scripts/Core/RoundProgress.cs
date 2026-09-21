using System;

namespace BubblePics
{
    [Serializable]
    public sealed class RoundProgress
    {
        public int version = 2;
        public int level;
        public string levelSignature;
        public string frozenLevelJson;
        public int stepsLeft;
        public int usedSteps;
        public int linkSteps;
        public bool unlimited;
        public bool dead;
        public int continueAdCount;
        public bool firstRevivePending;
        public int autoLinkCount;
        public int[] collectedImages = Array.Empty<int>();
        public RoundWaveProgress[] pendingWaves = Array.Empty<RoundWaveProgress>();
        public BubbleProgress[] bubbles = Array.Empty<BubbleProgress>();
    }

    [Serializable]
    public sealed class RoundWaveProgress
    {
        public string[] tokens = Array.Empty<string>();
    }

    [Serializable]
    public sealed class BubbleProgress
    {
        public string token;
        public float worldX;
        public float worldY;
        public float velocityX;
        public float velocityY;
        public bool landed;
        public bool locked;
        public int lockCount;
    }
}
