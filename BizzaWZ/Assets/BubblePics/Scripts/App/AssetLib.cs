using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BubblePics
{
    /// <summary>Central cached loading for the small set of path-addressed runtime assets.</summary>
    public static class AssetLib
    {
        static readonly Dictionary<string, Sprite> _sprites = new();
        static readonly Dictionary<string, Texture2D> _textures = new();
        static readonly Dictionary<string, AudioClip> _clips = new();
        static readonly Dictionary<string, Font> _fonts = new();
        static readonly Dictionary<string, TMP_FontAsset> _tmpFonts = new();
        static readonly Dictionary<string, Shader> _shaders = new();

        const string MainTmpFontPath = "Fonts/TMP/BaggageGo-Bold SDF";

        static AssetLib()
        {
            Localization.LocaleChanged += ApplyLocalizedFontsToLoadedScenes;
        }

        public static Sprite Sprite(string path)
        {
            if (_sprites.TryGetValue(path, out var s) && s != null) return s;
            s = ResourceAssetLoader.Load<Sprite>(path);
            if (s == null)
            {
                var tex = Texture(path);
                if (tex != null)
                {
                    s = UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
                }
            }
            if (s == null) Debug.LogWarning("Missing sprite: " + path);
            _sprites[path] = s;
            return s;
        }

        /// <summary>Nine-slice sprite with Godot patch margins (left, top, right, bottom).
        /// Unity border vector is (left, bottom, right, top).</summary>
        public static Sprite Sprite9(string path, float l, float t, float r, float b)
        {
            string key = $"{path}#9{l},{t},{r},{b}";
            if (_sprites.TryGetValue(key, out var s) && s != null) return s;
            var tex = Texture(path);
            if (tex == null) { Debug.LogWarning("Missing 9-slice: " + path); return null; }
            s = UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect, new Vector4(l, b, r, t));
            _sprites[key] = s;
            return s;
        }

        /// <summary>
        /// Nine-slice for Godot panels whose patch margins are authored in UI
        /// design pixels. UGUI divides sprite PPU by the Canvas reference PPU
        /// (100), so these panels need PPU=100 to keep their margins 1:1.
        /// This is also required for common-button faces: using PPU=1 makes
        /// UGUI expand their borders by the Canvas reference PPU, then clamp
        /// the corners independently on each axis and squash the button into
        /// an oval.
        /// </summary>
        public static Sprite Sprite9Design(
            string path,
            float l,
            float t,
            float r,
            float b)
        {
            string key = $"{path}#9design{l},{t},{r},{b}";
            if (_sprites.TryGetValue(key, out var s) && s != null) return s;
            Texture2D tex = Texture(path);
            if (tex == null)
            {
                Debug.LogWarning("Missing design 9-slice: " + path);
                return null;
            }
            s = UnityEngine.Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(l, b, r, t));
            _sprites[key] = s;
            return s;
        }

        public static Texture2D Texture(string path)
        {
            if (_textures.TryGetValue(path, out var t) && t != null) return t;
            t = ResourceAssetLoader.Load<Texture2D>(path);
            if (t == null) Debug.LogWarning("Missing texture: " + path);
            _textures[path] = t;
            return t;
        }

        public static AudioClip Clip(string path)
        {
            if (_clips.TryGetValue(path, out var c) && c != null) return c;
            c = ResourceAssetLoader.Load<AudioClip>(path);
            _clips[path] = c;
            return c;
        }

        /// <summary>
        /// Main UI font restored from Godot's default font resource. The TMP
        /// asset owns the locale fallback chain, so every language and numeric
        /// label starts with the same BaggageGo face used by the source game.
        /// </summary>
        public static TMP_FontAsset UiFont =>
            PrefabCatalog.Current != null && PrefabCatalog.Current.UiFont != null
                ? PrefabCatalog.Current.UiFont
                : TmpFontByName(MainTmpFontPath);

        public static TMP_FontAsset NumFont => UiFont;

        /// <summary>
        /// Applies the shared BaggageGo TMP asset below <paramref name="root"/>.
        /// Locale-specific glyphs are resolved by that asset's ordered fallback
        /// table instead of swapping the primary font at runtime.
        /// </summary>
        public static void ApplyLocalizedFonts(Transform root)
        {
            if (root == null) return;

            TMP_FontAsset font = UiFont;
            if (font == null) return;

            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text != null)
                    ApplyMainFont(text, font);
            }
        }

        /// <summary>
        /// Reapplies the shared TMP font to instantiated product UI, including
        /// DontDestroyOnLoad objects. Calling this after a locale change also
        /// refreshes text whose glyphs now resolve through another fallback.
        /// </summary>
        public static void ApplyLocalizedFontsToLoadedScenes()
        {
            TMP_FontAsset font = UiFont;
            if (font == null) return;

            var texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
            for (int i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text == null ||
                    !text.gameObject.scene.IsValid() ||
                    !text.gameObject.scene.isLoaded)
                    continue;
                ApplyMainFont(text, font);
            }
        }

        static void ApplyMainFont(TMP_Text text, TMP_FontAsset font)
        {
            bool fontChanged = text.font != font;
            if (fontChanged)
                text.font = font;

            // Imported prefabs can retain TMP's default LiberationSans
            // material even after their font reference is rebound. Preserve
            // authored outline/shadow presets when they already use the main
            // font atlas, and only repair incompatible stale materials.
            Material material = text.fontSharedMaterial;
            Texture atlas = font.atlasTexture;
            bool materialChanged = material == null ||
                material.mainTexture == null ||
                material.mainTexture != atlas;
            if (materialChanged)
                text.fontSharedMaterial = font.material;

            if (fontChanged || materialChanged)
            {
                text.havePropertiesChanged = true;
                text.SetAllDirty();
            }
        }

        public static TMP_FontAsset TmpFontByName(string path)
        {
            if (_tmpFonts.TryGetValue(path, out var font) && font != null)
                return font;

            font = ResourceAssetLoader.Load<TMP_FontAsset>(path);
            if (font == null)
                Debug.LogWarning("Missing TMP font: " + path);
            _tmpFonts[path] = font;
            return font;
        }

        /// <summary>Loads a legacy Font for non-product or migration callers.</summary>
        public static Font FontByName(string path)
        {
            if (_fonts.TryGetValue(path, out var f) && f != null) return f;
            f = ResourceAssetLoader.Load<Font>(path);
            if (f == null) Debug.LogWarning("Missing font: " + path);
            _fonts[path] = f;
            return f;
        }

        public static Shader Shader(string path)
        {
            if (_shaders.TryGetValue(path, out var s) && s != null) return s;
            s = ResourceAssetLoader.Load<Shader>(path);
            if (s == null) Debug.LogWarning("Missing shader: " + path);
            _shaders[path] = s;
            return s;
        }

        /// <summary>Level photo texture by local image base name (md5).</summary>
        public static Texture2D LevelPhoto(string baseName)
        {
            return Texture("Art/Builtin/" + baseName);
        }
    }
}
