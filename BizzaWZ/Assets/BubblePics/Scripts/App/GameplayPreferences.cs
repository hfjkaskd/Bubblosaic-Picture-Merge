using System;
using System.Collections.Generic;
using System.Globalization;

public partial class GameSaveData
{
    public List<BubblePics.GameplaySaveValue> bubbleGameplayValues = new List<BubblePics.GameplaySaveValue>();
}

namespace BubblePics
{
    [Serializable]
    public sealed class GameplaySaveValue
    {
        public string key;
        public string value;
    }

    /// <summary>Original gameplay keys stored inside the framework's game save.</summary>
    public static class GameplayPreferences
    {
        private static List<GameplaySaveValue> Values => SaveDataUtils.GameData != null
            ? SaveDataUtils.GameData.bubbleGameplayValues ??= new List<GameplaySaveValue>()
            : throw new InvalidOperationException("Gameplay must wait for the framework save loading task.");

        public static string GetString(string key, string fallback = "")
        {
            var values = Values;
            for (int i = 0; i < values.Count; i++) if (values[i].key == key) return values[i].value;
            return fallback;
        }

        public static int GetInt(string key, int fallback = 0) => int.TryParse(GetString(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
        public static float GetFloat(string key, float fallback = 0) => float.TryParse(GetString(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;
        public static void SetInt(string key, int value) => SetString(key, value.ToString(CultureInfo.InvariantCulture));
        public static void SetFloat(string key, float value) => SetString(key, value.ToString("R", CultureInfo.InvariantCulture));
        public static void SetString(string key, string value)
        {
            var values = Values;
            for (int i = 0; i < values.Count; i++)
                if (values[i].key == key) { values[i].value = value ?? ""; return; }
            values.Add(new GameplaySaveValue { key = key, value = value ?? "" });
        }
        public static bool HasKey(string key) => Values.Exists(entry => entry.key == key);
        public static void DeleteKey(string key) => Values.RemoveAll(entry => entry.key == key);
        public static void DeleteAll() { Values.Clear(); Save(); }
        public static void Save() => SaveDataUtils.gameStrategy.SaveData();
    }
}
