#if BIZZA_REAL_WITHDRAW
using Bizza.Sdk;
using UnityEngine;
using UnityEngine.UI;

[Obfuz.ObfuzIgnore]
public class WzIconAmend : MonoBehaviour
{
    public E_WzIconType iconType;
    public Image image;
    public bool isNativeSize = false;

    // Legacy authoring fields retained for old asset tools; country art always wins.
    [SerializeField, HideInInspector] private string skinAtlasResource;
    [SerializeField, HideInInspector] private string skinSpriteName;
    [SerializeField, HideInInspector] private string singleCurrencySkinSpriteName;
    [SerializeField, HideInInspector] private Material skinMaterial;
    [SerializeField, HideInInspector] private Material singleCurrencySkinMaterial;
    private void OnEnable()
    {
        BizzaEventSystem.Set(EventDefine.Login.InitContentByCountry, Refresh, true);
        Refresh();
    }

    private void OnDisable()
    {
        BizzaEventSystem.Set(EventDefine.Login.InitContentByCountry, Refresh, false);
    }

    public void Refresh()
    {
        if (ChannelConfig.Instance == null) return;
        ApplyForCountry(AccountModule.CountryType, ChannelConfig.Instance.real_CustomConfig.singleCurrencyMode);
    }

    public void ApplyForCountry(AccountModule.E_CountryType country, bool singleCurrency)
    {
        if (image == null && !TryGetComponent(out image)) return;
        Sprite sprite = CountryCurrencyIcons.Load(iconType, country, singleCurrency);
        if (sprite == null) return; // Country is assigned by login; retry on its event.
        image.sprite = sprite;
        image.material = null;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        if (isNativeSize) image.SetNativeSize();
    }

}

public static partial class EventDefine
{
    public static class Login
    {
        public static GameEvent InitContentByCountry = new();
    }
}
#endif
