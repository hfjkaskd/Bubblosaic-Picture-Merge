using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Sound playback with per-kind volume/polyphony/cooldown/delay mirroring
    /// Godot sound_manager.gd exactly. Kinds map to the Godot enum names.
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager I { get; private set; }

        class KindDef
        {
            public string path;
            public float volumeDb;
            public int maxPoly = 1;
            public float cooldown;   // seconds between plays
            public float delay;      // fixed delay before playing
        }

        static Dictionary<string, KindDef> BuildDefs()
        {
            string merge = AppConfig.MainSoundNew ? "Audio/Sfx/bubble_merge_new" : "Audio/Sfx/bubble_merge";
            string reject = AppConfig.MainSoundNew ? "Audio/Sfx/merge_reject_new" : "Audio/Sfx/merge_reject";
            return new Dictionary<string, KindDef>
            {
                { "button",        new KindDef { path = "Audio/Sfx/btn_click_2", maxPoly = 4 } },
                { "dialog",        new KindDef { path = "Audio/Sfx/dlg_open_1", maxPoly = 1 } },
                { "combo_merge_1", new KindDef { path = "Audio/Sfx/combo_link_1", maxPoly = 2 } },
                { "combo_merge_2", new KindDef { path = "Audio/Sfx/combo_link_2", maxPoly = 2 } },
                { "combo_merge_3", new KindDef { path = "Audio/Sfx/combo_link_3", maxPoly = 2 } },
                { "nice",          new KindDef { path = "Audio/Sfx/nice_man", maxPoly = 1 } },
                { "perfect",       new KindDef { path = "Audio/Sfx/perfect_man", maxPoly = 1 } },
                { "excellent1",    new KindDef { path = "Audio/Sfx/excellent_man_1", maxPoly = 1 } },
                { "excellent2",    new KindDef { path = "Audio/Sfx/excellent_man_2", maxPoly = 1 } },
                { "excellent3",    new KindDef { path = "Audio/Sfx/excellent_man_3", maxPoly = 1 } },
                { "wonderful",     new KindDef { path = "Audio/Sfx/wonderful", maxPoly = 1 } },
                { "merge",         new KindDef { path = merge, volumeDb = 2.8f, maxPoly = 3, delay = 0.1f } },
                { "sub_merge",     new KindDef { path = "Audio/Sfx/sub_merge", maxPoly = 3 } },
                { "pop",           new KindDef { path = "Audio/Sfx/bubble_pop", maxPoly = 2 } },
                { "image_complete",new KindDef { path = "Audio/Sfx/image_complete", maxPoly = 2 } },
                { "collide",       new KindDef { path = "Audio/Sfx/bubble_collide_1", volumeDb = -1.94f, maxPoly = 4, cooldown = 0.06f } },
                { "reject",        new KindDef { path = reject, maxPoly = 2 } },
                { "coin",          new KindDef { path = "Audio/Sfx/coin_fly", maxPoly = 6 } },
                { "game_completed",new KindDef { path = "Audio/Sfx/game_completed", maxPoly = 1 } },
                { "death",         new KindDef { path = "Audio/Sfx/bubble_death", maxPoly = 1 } },
                { "big_merge",     new KindDef { path = "Audio/Sfx/big_merge", maxPoly = 1 } },
                { "unlock",        new KindDef { path = "Audio/Sfx/bubble_unlock", maxPoly = 2 } },
                { "bonus_chest_open", new KindDef { path = "Audio/Sfx/bonus_chest_open", maxPoly = 1 } },
                { "daily_incentive_win", new KindDef { path = "Audio/Sfx/daily_incentive_win", maxPoly = 1 } },
                { "lucky_break_unlock", new KindDef { path = "Audio/Sfx/lucky_break_unlock", maxPoly = 1 } },
                { "sea_hero", new KindDef { path = "Audio/Sfx/sea_hero", maxPoly = 1 } },
                { "starfish_bubble_pop", new KindDef { path = "Audio/Sfx/starfish_bubble_pop", maxPoly = 3 } },
                { "starfish_collect", new KindDef { path = "Audio/Sfx/starfish_collect", maxPoly = 3 } },
                { "starfish_unlock", new KindDef { path = "Audio/Sfx/starfish_unlock", maxPoly = 3 } },
                // PERFECT_FIT reuses big_merge in the recovered 1.0.9 data.
                { "perfect_fit", new KindDef { path = "Audio/Sfx/big_merge", maxPoly = 1 } },
                { "chapter_bg_claim", new KindDef { path = "Audio/Sfx/outerloop_chapter_bg_claim", maxPoly = 1 } },
                { "chapter_bg_unlock", new KindDef { path = "Audio/Sfx/outerloop_chapter_bg_unlock", maxPoly = 1 } },
                { "magnet_bubble_burst", new KindDef { path = "Audio/Sfx/magnet_bubble_burst", maxPoly = 1 } },
                { "outerloop_bubble_squeeze", new KindDef { path = "Audio/Sfx/outloop_bubble", maxPoly = 1 } },
                // Keep file-name aliases used by early Unity restoration code.
                { "outerloop_chapter_bg_claim", new KindDef { path = "Audio/Sfx/outerloop_chapter_bg_claim", maxPoly = 1 } },
                { "outerloop_chapter_bg_unlock", new KindDef { path = "Audio/Sfx/outerloop_chapter_bg_unlock", maxPoly = 1 } },
            };
        }

        Dictionary<string, KindDef> _defs;
        readonly Dictionary<string, float> _lastPlay = new Dictionary<string, float>();
        readonly Dictionary<string, List<AudioSource>> _sourcesByKind =
            new Dictionary<string, List<AudioSource>>();

        public static void Create()
        {
            if (I != null) return;
            var go = new GameObject("~SoundManager");
            DontDestroyOnLoad(go);
            I = go.AddComponent<SoundManager>();
            I._defs = BuildDefs();
        }

        AudioSource GetSource(string kind, int maxPolyphony)
        {
            if (!_sourcesByKind.TryGetValue(kind, out var sources))
            {
                sources = new List<AudioSource>(maxPolyphony);
                _sourcesByKind[kind] = sources;
            }

            foreach (var source in sources)
                if (source != null && !source.isPlaying)
                    return source;

            if (sources.Count >= maxPolyphony)
                return null;

            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            sources.Add(src);
            return src;
        }

        public void Play(string kind, float pitch = 1f)
        {
            if (!SaveState.SoundOn) return;
            if (_defs == null) _defs = BuildDefs();
            if (!_defs.TryGetValue(kind, out var def)) { Debug.LogWarning("No sound kind " + kind); return; }
            float now = Time.unscaledTime;
            if (def.cooldown > 0 && _lastPlay.TryGetValue(kind, out float last) && now - last < def.cooldown) return;

            var clip = AssetLib.Clip(def.path);
            if (clip == null) return;
            _lastPlay[kind] = now;

            if (def.delay > 0)
            {
                StartCoroutine(PlayDelayed(kind, def, clip, pitch));
                return;
            }

            PlayImmediate(kind, def, clip, pitch);
        }

        IEnumerator PlayDelayed(
            string kind,
            KindDef def,
            AudioClip clip,
            float pitch)
        {
            yield return new WaitForSecondsRealtime(def.delay);
            // Godot's delayed callback checks the live sound switch again.
            if (!SaveState.SoundOn)
                yield break;
            PlayImmediate(kind, def, clip, pitch);
        }

        void PlayImmediate(
            string kind,
            KindDef def,
            AudioClip clip,
            float pitch)
        {
            if (!SaveState.SoundOn)
                return;

            var src = GetSource(kind, def.maxPoly);
            if (src == null)
                return;

            src.clip = clip;
            src.volume = Mathf.Pow(10f, def.volumeDb / 20f);
            src.pitch = pitch;
            src.Play();
        }
    }
}
