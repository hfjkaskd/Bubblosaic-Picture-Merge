using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>Loads authored artwork when its prefab is instantiated, without a texture dependency on bootstrap prefabs.</summary>
    [DefaultExecutionOrder(-10000), DisallowMultipleComponent]
    public sealed class CoralResourceSprite : MonoBehaviour
    {
        [SerializeField] string _resourcePath;
        [SerializeField] string _spriteName;
        [SerializeField] Image _image;
        [SerializeField] SpriteRenderer _renderer;
        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        public static Sprite Load(string path, string spriteName = "")
        {
            string key = path + "/" + spriteName;
            if (Sprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            Sprite result = null;
            if (string.IsNullOrEmpty(spriteName)) result = Resources.Load<Sprite>(path);
            else
            {
                Sprite[] atlas = Resources.LoadAll<Sprite>(path);
                for (int i = 0; i < atlas.Length; i++)
                {
                    Sprites[path + "/" + atlas[i].name] = atlas[i];
                    if (atlas[i].name == spriteName) result = atlas[i];
                }
            }
            if (result == null) Debug.LogError("Missing authored game artwork: " + key);
            else Sprites[key] = result;
            return result;
        }

        void Awake()
        {
            Apply();
        }

        public void SetSource(string path,string spriteName)
        {
            _resourcePath=path;_spriteName=spriteName;Apply();
        }

        void Apply()
        {
            Sprite sprite = Load(_resourcePath, _spriteName);
            if (_image != null) _image.sprite = sprite;
            if (_renderer != null) _renderer.sprite = sprite;
        }
    }
}
