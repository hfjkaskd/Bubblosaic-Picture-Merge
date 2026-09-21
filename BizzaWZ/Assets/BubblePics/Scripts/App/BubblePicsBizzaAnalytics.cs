using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Project-owned bootstrap and compatibility bridge for Bizza Analytics.
    /// The vendor runtime stays project-agnostic; the Bubblepics AppID and the
    /// existing FunSmith custom-event route are bound here.
    /// </summary>
    public static class BubblePicsBizzaAnalytics
    {
        public const string AppId = "Bubblepics_And";

        public static void TrackJson(string eventName, string json)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                return;

            try
            {
                object parsed = BizzaJson.Deserialize(
                    string.IsNullOrWhiteSpace(json) ? "{}" : json);
                Dictionary<string, object> payload =
                    BizzaValueUtil.AsStringObjectDictionary(parsed);
                BizzaAnalyticsAgent.Track(eventName, payload);
            }
            catch (Exception exception)
            {
                // Analytics must never interrupt gameplay or the native SDK.
                Debug.LogWarning(
                    "[BizzaAnalytics] Custom event bridge ignored invalid " +
                    "payload for " + eventName + ": " + exception.Message);
            }
        }
    }
}
