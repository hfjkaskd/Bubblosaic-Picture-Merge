#if BIZZA_REAL_WITHDRAW
using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class FAQAccordionItem : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text indicator;
    [SerializeField] private TMP_Text detailedQuestion;
    [SerializeField] private GameObject[] answerObjects;
    [SerializeField] private GameObject[] expandedDecorations;
    [SerializeField] private LayoutElement height;
    [SerializeField] private float collapsedHeight;
    [SerializeField] private float expandedHeight;

    public bool IsExpanded { get; private set; }

    private void Awake()
    {
        button.onClick.AddListener(Toggle);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(Toggle);
    }

    public void Toggle()
    {
        SetExpanded(!IsExpanded);
    }

    public void SetExpanded(bool expanded)
    {
        IsExpanded = expanded;
        foreach (var answer in answerObjects)
            if (answer != null) answer.SetActive(expanded);
        foreach (var decoration in expandedDecorations)
            if (decoration != null) decoration.SetActive(expanded);

        if (background != null)
            background.color = expanded ? new Color32(184, 226, 255, 255) : Color.white;

        if (height != null)
        {
            height.minHeight = expanded ? expandedHeight : collapsedHeight;
            height.preferredHeight = height.minHeight;
        }

        if (indicator != null)
        {
            indicator.text = expanded ? "−" : "+";
            indicator.color = expanded ? new Color32(0, 77, 142, 255) : new Color32(0, 110, 214, 255);
        }
        if (transform.parent is RectTransform parent)
            LayoutRebuilder.MarkLayoutForRebuild(parent);
    }

    public void RefreshDetailedText(FAQDesc description, string localizedText)
    {
        if (detailedQuestion == null) return;

        var newline = localizedText.IndexOf('\n');
        var newlineLength = 1;
        if (newline < 0)
        {
            newline = localizedText.IndexOf("\\n", StringComparison.Ordinal);
            newlineLength = 2;
        }

        if (newline >= 0)
        {
            detailedQuestion.text = Regex.Replace(
                localizedText.Substring(0, newline), "<[^>]+>", string.Empty).Trim();
            description.tMP_Text.text = localizedText.Substring(newline + newlineLength).TrimStart('\r', '\n');
        }
        else
        {
            // Keep unexpected translations readable instead of hiding their content.
            detailedQuestion.text = Regex.Replace(localizedText.Split('\n')[0], "<[^>]+>", string.Empty);
            description.tMP_Text.text = localizedText;
        }
    }
}
#endif
