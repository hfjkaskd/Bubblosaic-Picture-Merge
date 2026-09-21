#if BIZZA_REAL_WITHDRAW
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RealUiWidget : MonoBehaviour
{
    public BizzaButton taskBtn;
    public TMP_Text levelTxt;


    public ItemForCountry itemForCountry;


    void Awake()
    {
        taskBtn.onClick.AddListener(() =>
        {
            _ = UIModule.Instance.OpenPage(UIPageIds.UI_DailyTaskPage);
        });
    }

    void OnEnable()
    {
        // The HUD stays active while the next level loads.
        BizzaEventSystem.Set(EventDefine.Item.GameStart, RefreshLevelText, true);
        RefreshLevelText();
        itemForCountry.OnRefresh();
    }

    void OnDisable()
    {
        BizzaEventSystem.Set(EventDefine.Item.GameStart, RefreshLevelText, false);
    }

    public void RefreshLevelText()
    {
        levelTxt.text = $"{SaveDataUtils.GameData.playerSelectedLv}"; //LanguageUtils.GetFormatText("Menu_LevelBtn", SaveDataUtils.GameData.playerUnlockedLv);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
#endif
