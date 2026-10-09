using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static class ReleaseArtInspection
    {
        public static bool NamedSprite(Sprite sprite,string originalName) => sprite!=null &&
            (sprite.name==originalName || sprite.name.StartsWith(originalName+"__",StringComparison.Ordinal));
        public static bool BakedSprite(Image image,string originalName) => NamedSprite(image.sprite,originalName) &&
            AssetDatabase.GetAssetPath(image.sprite).StartsWith("Assets/BubblePics/Resources/ReleaseUI/",StringComparison.Ordinal);
        public static bool Caption(Image image,bool authored)
        {
            var caption=image.GetComponent<ApprovedHudCaption>();if(caption==null)return false;
            var so=new SerializedObject(caption);
            string path=so.FindProperty("_bakedResourcePath").stringValue;
            string name=so.FindProperty(authored?"_bakedCaptionSprite":"_bakedTranslatedSprite").stringValue;
            return !string.IsNullOrEmpty(path)&&image.sprite==CoralResourceSprite.Load(path,name);
        }
    }
}
