using UnityEngine;

namespace BubblePics
{
    [CreateAssetMenu(menuName = "BubblePics/Tool appearance")]
    public sealed class ToolAppearance : ScriptableObject
    {
        public string AtlasResource;
        public string BackgroundAtlasResource;
        public string NormalSprite;
        public string LockedSprite;
        public string HintSprite;
        public string DropSprite;
        public string MagnetResource;
        public Material NormalBackgroundMaterial;
        public Material LockedBackgroundMaterial;
        public Color DisabledTint = Color.white;

        public Sprite Background(bool locked)
        {
            string resource=string.IsNullOrEmpty(BackgroundAtlasResource)?AtlasResource:BackgroundAtlasResource;
            return CoralResourceSprite.Load(resource,locked?LockedSprite:NormalSprite);
        }

        public Material BackgroundMaterial(bool locked) => locked ? LockedBackgroundMaterial : NormalBackgroundMaterial;

        public Sprite Icon(string toolId)
        {
            if (toolId == "hint") return CoralResourceSprite.Load(AtlasResource, HintSprite);
            if (toolId == "drop") return CoralResourceSprite.Load(AtlasResource, DropSprite);
            return CoralResourceSprite.Load(MagnetResource);
        }
    }
}
