#if BIZZA_REAL_WITHDRAW
using System.Collections.Generic;
using Bizza.Sdk;
using UnityEngine;

// One resource selection path for HUD, rewards, inline UI and collection effects.
public static class CountryCurrencyIcons
{
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    public static string CountryCode(AccountModule.E_CountryType country)
    {
        switch (country)
        {
            case AccountModule.E_CountryType.US: return "US";
            case AccountModule.E_CountryType.BR: return "BR";
            case AccountModule.E_CountryType.ID: return "ID";
            default: return null;
        }
    }

    public static E_WzIconType ResolveType(E_WzIconType type, bool singleCurrency)
    {
        if (!singleCurrency) return type;
        switch (type)
        {
            case E_WzIconType.GoldCoin:
            case E_WzIconType.MoneyEnhancement: return E_WzIconType.StackMoney;
            case E_WzIconType.PileGold:
            case E_WzIconType.PileWealth: return E_WzIconType.HundredMoney;
            default: return type;
        }
    }

    public static string IconKey(E_WzIconType type)
    {
        switch (type)
        {
            case E_WzIconType.MoneyIcon: return "StackMoney";
            case E_WzIconType.PieceMoney: return "PieceMoney";
            case E_WzIconType.StackMoney: return "StackMoney";
            case E_WzIconType.PileMoney: return "PileMoney";
            case E_WzIconType.PileWealth: return "PileWealth";
            case E_WzIconType.PileGold: return "PileGold";
            case E_WzIconType.HundredMoney: return "HundredMoney";
            case E_WzIconType.AbundanceWealth: return "AbundanceWealth";
            case E_WzIconType.MoneyEnhancement: return "MoneyEnhancement";
            case E_WzIconType.GoldCoin: return "GoldCoin";
            case E_WzIconType.BubbleCoin:
            case E_WzIconType.BubbleMoney: return "Bubble";
            default: return null;
        }
    }

    public static Sprite Load(E_WzIconType type, AccountModule.E_CountryType country, bool singleCurrency = false)
    {
        return Load(IconKey(ResolveType(type, singleCurrency)), country);
    }

    public static Sprite Load(string iconKey, AccountModule.E_CountryType country)
    {
        string suffix = CountryCode(country);
        if (string.IsNullOrEmpty(iconKey) || suffix == null) return null;
        string address = iconKey + "_" + suffix;
        if (Cache.TryGetValue(address, out Sprite sprite) && sprite != null) return sprite;
        // Original icons are small Addressable assets. Cache once per icon/country,
        // instead of repeatedly retaining Addressables handles on every popup.
        sprite = AssetUtils.LoadAssetSync<Sprite>(address);
        if (sprite != null) Cache[address] = sprite;
        return sprite;
    }

    public static Sprite LoadItem(E_ItemType type)
    {
        if (type != E_ItemType.Gold && type != E_ItemType.Dollar) return null;
        if (ChannelConfig.Instance == null) return null;
        bool singleCurrency = ChannelConfig.Instance.real_CustomConfig.singleCurrencyMode;
        return Load(type == E_ItemType.Gold ? E_WzIconType.GoldCoin : E_WzIconType.StackMoney,
            AccountModule.CountryType, singleCurrency);
    }
}
#endif
