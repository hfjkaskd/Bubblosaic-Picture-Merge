using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace BubblePics.GameModes
{
    internal static class ModeJson
    {
        public static Dictionary<string, object> ParseObject(string json)
        {
            return SpineLite.MiniJson.Parse(json ?? string.Empty) as
                Dictionary<string, object>;
        }

        public static Dictionary<string, object> Object(
            object value)
        {
            return value as Dictionary<string, object>;
        }

        public static IList Array(object value)
        {
            return value as IList;
        }

        public static string String(
            Dictionary<string, object> source,
            string key,
            string fallback = "")
        {
            if (source == null || !source.TryGetValue(key, out object value) ||
                value == null)
                return fallback;
            return Convert.ToString(value, CultureInfo.InvariantCulture) ??
                   fallback;
        }

        public static int Int(
            Dictionary<string, object> source,
            string key,
            int fallback = 0)
        {
            if (source == null || !source.TryGetValue(key, out object value) ||
                value == null)
                return fallback;
            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        public static float Float(object value, float fallback = 0f)
        {
            if (value == null) return fallback;
            try
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        public static bool Bool(
            Dictionary<string, object> source,
            string key,
            bool fallback = false)
        {
            if (source == null || !source.TryGetValue(key, out object value) ||
                value == null)
                return fallback;
            try
            {
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }
    }
}
