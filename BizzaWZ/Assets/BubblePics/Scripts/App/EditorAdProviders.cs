#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Matches the restored original's editor_ad_plugin.gd: editor play mode
    /// can exercise reward/interstitial flows without granting rewards in a
    /// production build that has no real SDK.
    /// </summary>
    public sealed class EditorAdProviders :
        IRewardedAdProvider, IInterstitialAdProvider
    {
        readonly MonoBehaviour _host;
        bool _showing;

        public EditorAdProviders(MonoBehaviour host)
        {
            _host = host;
        }

        public bool IsReady => _host != null && !_showing;

        void IRewardedAdProvider.Show(string placement, Action<bool> completed)
        {
            if (!IsReady)
            {
                completed?.Invoke(false);
                return;
            }
            _host.StartCoroutine(RewardCo(placement, completed));
        }

        void IInterstitialAdProvider.Show(
            int levelNum,
            string placement,
            Action<bool> completed)
        {
            if (!IsReady)
            {
                completed?.Invoke(false);
                return;
            }
            _host.StartCoroutine(InterstitialCo(placement, completed));
        }

        IEnumerator RewardCo(string placement, Action<bool> completed)
        {
            _showing = true;
            Debug.Log($"[EditorAds] rewarded placement={placement}");
            yield return new WaitForSecondsRealtime(0.35f);
            _showing = false;
            completed?.Invoke(true);
        }

        IEnumerator InterstitialCo(
            string placement,
            Action<bool> completed)
        {
            _showing = true;
            Debug.Log($"[EditorAds] interstitial placement={placement}");
            yield return new WaitForSecondsRealtime(0.35f);
            _showing = false;
            completed?.Invoke(true);
        }
    }
}
#endif
