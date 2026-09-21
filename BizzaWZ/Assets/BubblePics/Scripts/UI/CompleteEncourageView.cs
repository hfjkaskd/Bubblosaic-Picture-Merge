using System;
using System.Collections;
using System.Collections.Generic;
using Obfuz;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    [Serializable]
    public struct CompletePerformance
    {
        public int ImageCount;
        public string DifficultyType;
        public float TimeSeconds;
        public int UsedSteps;
        public int MaxCombo;
    }

    [ObfuzIgnore(ObfuzScope.Field)]
    public enum CompleteMetric
    {
        Normal,
        Time,
        Step,
        Combo,
    }

    public readonly struct CompleteEncourageResult
    {
        public readonly CompleteMetric Metric;
        public readonly int Percentile;
        public readonly string Text;

        public CompleteEncourageResult(
            CompleteMetric metric,
            int percentile,
            string text)
        {
            Metric = metric;
            Percentile = percentile;
            Text = text;
        }
    }

    /// <summary>1.0.9 result-percentile lookup and tie-breaking port.</summary>
    public static class CompleteEncouragePicker
    {
        const string BankPath = "Version109/Data/Result/result_percentile_bank";
        static readonly CompleteMetric[] TiePriority =
        {
            CompleteMetric.Combo,
            CompleteMetric.Step,
            CompleteMetric.Time,
        };

        static Dictionary<string, object> _bank;
        static List<object> _percentiles;

        public static CompleteEncourageResult Pick(
            CompletePerformance performance,
            int beatThreshold = 60)
        {
            CompleteMetric best = CompleteMetric.Normal;
            float bestPercentile = -1f;
            for (int i = 0; i < TiePriority.Length; i++)
            {
                CompleteMetric metric = TiePriority[i];
                float value = metric switch
                {
                    CompleteMetric.Time => performance.TimeSeconds,
                    CompleteMetric.Step => performance.UsedSteps,
                    _ => performance.MaxCombo,
                };
                float percentile = LookupPercentile(
                    metric,
                    performance.ImageCount,
                    performance.DifficultyType,
                    value);
                if (percentile < beatThreshold || percentile <= bestPercentile)
                    continue;
                best = metric;
                bestPercentile = percentile;
            }

            int rounded = best == CompleteMetric.Normal
                ? 0
                : Mathf.RoundToInt(bestPercentile);
            int tip = UnityEngine.Random.Range(1, 11);
            string metricName = best.ToString().ToLowerInvariant();
            string key = $"complete_encourage_{metricName}_tips{tip}";
            string fallback = Fallback(best, rounded);
            string localized = Localization.Tr(key);
            string text = string.IsNullOrEmpty(localized) || localized == key
                ? fallback
                : localized;
            if (best != CompleteMetric.Normal)
                text = text.Replace("%d", rounded.ToString());
            return new CompleteEncourageResult(best, rounded, text);
        }

        public static float LookupPercentile(
            CompleteMetric metric,
            int imageCount,
            string difficultyType,
            float value)
        {
            EnsureLoaded();
            if (_bank == null || _percentiles == null) return -1f;
            string metricName = metric.ToString().ToLowerInvariant();
            if (!_bank.TryGetValue(metricName, out object rawTable) ||
                rawTable is not Dictionary<string, object> table)
                return -1f;
            string key = $"{imageCount}_{difficultyType}";
            if (!table.TryGetValue(key, out object rawThresholds) ||
                rawThresholds is not List<object> thresholds)
                return -1f;
            bool lowerIsBetter = metric == CompleteMetric.Time || metric == CompleteMetric.Step;
            int count = Mathf.Min(thresholds.Count, _percentiles.Count);
            for (int i = 0; i < count; i++)
            {
                if (!TryFloat(thresholds[i], out float threshold) ||
                    !TryFloat(_percentiles[i], out float percentile))
                    continue;
                if ((lowerIsBetter && value <= threshold) ||
                    (!lowerIsBetter && value >= threshold))
                    return percentile * 100f;
            }
            return -1f;
        }

        static void EnsureLoaded()
        {
            if (_bank != null) return;
            TextAsset asset = ResourceAssetLoader.Load<TextAsset>(BankPath);
            if (asset == null)
            {
                Debug.LogWarning($"Missing percentile bank: Resources/{BankPath}.json");
                return;
            }
            _bank = SpineLite.MiniJson.Parse(asset.text) as
                Dictionary<string, object>;
            if (_bank != null && _bank.TryGetValue("percentiles", out object raw))
                _percentiles = raw as List<object>;
        }

        static bool TryFloat(object value, out float result)
        {
            if (value is double d)
            {
                result = (float)d;
                return true;
            }
            if (value is long l)
            {
                result = l;
                return true;
            }
            return float.TryParse(Convert.ToString(value), out result);
        }

        static string Fallback(CompleteMetric metric, int percentile)
        {
            return metric switch
            {
                CompleteMetric.Time => $"Faster than {percentile}% of players!",
                CompleteMetric.Step => $"You used fewer moves than {percentile}% of players!",
                CompleteMetric.Combo => $"Bigger combo than {percentile}% of players!",
                _ => "Great job!",
            };
        }
    }

    /// <summary>Port of complete_encourage_banner.gd.</summary>
    public sealed class CompleteEncourageView : MetaFlowView
    {
        [SerializeField] RectTransform _backgroundRect;
        [SerializeField] Image _background;
        [SerializeField] TMP_Text _label;
        [SerializeField] Sprite[] _textures = Array.Empty<Sprite>();
        [SerializeField] string[] _texturePaths =
        {
            "Art/Sprites/Bubble/complete_encourage_bg_brown",
            "Art/Sprites/Bubble/complete_encourage_bg_green",
            "Art/Sprites/Bubble/complete_encourage_bg_purple",
            "Art/Sprites/Bubble/complete_encourage_bg_red",
        };

        int _textureIndex;

        public Vector4 BackgroundBorder => _background != null && _background.sprite != null
            ? _background.sprite.border
            : Vector4.zero;
        public float BackgroundCenterX => _backgroundRect != null
            ? _backgroundRect.anchoredPosition.x + _backgroundRect.rect.width * 0.5f
            : float.NaN;

        public override void InitializePrefabRuntime()
        {
            base.InitializePrefabRuntime();
            _background ??= FindNamed<Image>(panelRoot, "Bg");
            if (_backgroundRect == null && _background != null)
                _backgroundRect = _background.rectTransform;
            _label ??= FindNamed<TMP_Text>(panelRoot, "Label");

            // Godot uses a NinePatchRect with 55/40/55/40 design-pixel
            // margins.  The imported Unity sprites do not serialize those
            // borders, so treating them as Sliced still stretches the whole
            // 176x112 texture.  Rebuild every rotating skin with the recovered
            // margins at runtime.
            if (_texturePaths != null && _texturePaths.Length > 0)
            {
                _textures = new Sprite[_texturePaths.Length];
                for (int i = 0; i < _texturePaths.Length; i++)
                {
                    _textures[i] = AssetLib.Sprite9Design(
                        _texturePaths[i],
                        55f,
                        40f,
                        55f,
                        40f);
                }
            }
            if (_background != null)
            {
                if (_textures != null && _textures.Length > 0)
                    _background.sprite = _textures[0];
                _background.type = Image.Type.Sliced;
                _background.preserveAspect = false;
            }
            if (_backgroundRect != null)
            {
                // Match Godot Control.position: top-left origin, positive Y down.
                _backgroundRect.anchorMin = new Vector2(0f, 1f);
                _backgroundRect.anchorMax = new Vector2(0f, 1f);
                _backgroundRect.pivot = new Vector2(0f, 1f);
            }
        }

        public void Play(CompleteEncourageResult result, float centerY = -1f)
        {
            Show();
            if (_textures != null && _textures.Length > 0 && _background != null)
            {
                _background.sprite = _textures[_textureIndex++ % _textures.Length];
                _background.type = Image.Type.Sliced;
            }
            if (_label != null) _label.text = result.Text;
            Run(PlayRoutine(centerY));
        }

        public void ConfigurePrefabAuthoring(
            RectTransform authoredPanelRoot,
            CanvasGroup authoredCanvasGroup,
            RectTransform backgroundRect,
            Image background,
            TMP_Text label,
            Sprite[] textures)
        {
            base.ConfigurePrefabAuthoring(authoredPanelRoot, null, authoredCanvasGroup);
            _backgroundRect = backgroundRect;
            _background = background;
            _label = label;
            _textures = textures ?? Array.Empty<Sprite>();
        }

        IEnumerator PlayRoutine(float centerY)
        {
            yield return null;
            float preferredWidth = _label != null
                ? _label.GetPreferredValues(_label.text, 2000f, 110f).x
                : 174f;
            float width = preferredWidth + 120f;
            if (_backgroundRect != null)
                _backgroundRect.sizeDelta = new Vector2(width, 110f);
            float topY = centerY >= 0f
                ? centerY - 55f
                : App.SafeTopDesign + 40f;
            Vector2 start = new Vector2(App.DesignW, -topY);
            Vector2 end = new Vector2(-width, -topY);
            if (_backgroundRect != null) _backgroundRect.anchoredPosition = start;
            yield return new WaitForSecondsRealtime(1f);
            float elapsed = 0f;
            while (elapsed < 8f)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_backgroundRect != null)
                {
                    _backgroundRect.anchoredPosition = Vector2.Lerp(
                        start,
                        end,
                        Mathf.Clamp01(elapsed / 8f));
                }
                yield return null;
            }
            Hide();
        }
    }
}
