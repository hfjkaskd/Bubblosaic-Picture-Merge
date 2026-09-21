using Obfuz;

namespace BubblePics
{
    /// <summary>
    /// Explicit support state for restored native and proprietary integrations.
    /// A missing SDK is never treated as a successful ad, purchase, or consent.
    /// </summary>
    [ObfuzIgnore(ObfuzScope.Field)]
    public enum PlatformCapabilityState
    {
        Supported = 0,
        Unsupported = 1,
        ProviderRequired = 2,
    }

    public readonly struct PlatformCapability
    {
        public PlatformCapability(
            PlatformCapabilityState state,
            string reason)
        {
            State = state;
            Reason = reason ?? string.Empty;
        }

        public PlatformCapabilityState State { get; }
        public string Reason { get; }
        public bool IsSupported => State == PlatformCapabilityState.Supported;

        public override string ToString()
        {
            return string.IsNullOrEmpty(Reason)
                ? State.ToString()
                : State + ": " + Reason;
        }
    }

    public static class PlatformIntegrationCapabilities
    {
        public const string MissingUniKitReason =
            "Unsupported: the proprietary UniKit SDK, production keys, " +
            "CMP adapter, ad adapter, and purchase adapter are not present.";

#if BUBBLEPICS_UNIKIT
        public const bool UniKitSdkCompiled = true;
#else
        public const bool UniKitSdkCompiled = false;
#endif

        public static PlatformCapability UniKit => UniKitSdkCompiled
            ? new PlatformCapability(
                PlatformCapabilityState.ProviderRequired,
                "BUBBLEPICS_UNIKIT is enabled; production adapters must " +
                "register and report real native callbacks.")
            : new PlatformCapability(
                PlatformCapabilityState.Unsupported,
                MissingUniKitReason);

        public static PlatformCapability Advertising =>
            RewardedAds.Provider != null || InterstitialAds.Provider != null
                ? new PlatformCapability(
                    PlatformCapabilityState.Supported,
                    "A provider is registered; rewards still require its real callback.")
                : new PlatformCapability(
                    PlatformCapabilityState.Unsupported,
                    "Unsupported: no production ad provider is registered; " +
                    "rewarded ads return false and interstitial gates continue offline.");

        public static PlatformCapability InAppPurchase =>
            new PlatformCapability(
                PlatformCapabilityState.Unsupported,
                "Unsupported: no purchase SDK/provider is installed; no purchase " +
                "or no-ads entitlement is synthesized.");
    }
}
