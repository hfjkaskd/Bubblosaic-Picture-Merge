#if BIZZA_REAL_WITHDRAW
// Loading feedback follows the account language, with the same English fallback as LanguageUtils.
internal static class BusinessPanelLoadingText
{
    public static string Select(string english, string portuguese, string indonesian)
    {
        return LanguageUtils.SelectedLanguage switch
        {
            "pt-BR" => portuguese,
            "id-ID" => indonesian,
            _ => english
        };
    }
}
#endif
