from pathlib import Path

def body(text, signature, replacement):
    start = text.index(signature)
    left = text.index('{', start)
    depth, right = 1, left + 1
    while depth:
        depth += (text[right] == '{') - (text[right] == '}')
        right += 1
    return text[:left] + '{\n' + replacement + '\n        }' + text[right:]

p=Path('BizzaWZ/Assets/BubblePics/Scripts/UI/ToolbarView.cs')
s=p.read_text(encoding='utf-8-sig')
i=s.index('public class ToolbarView')
s=s[:i]+s[i:].replace('[SerializeField] RectTransform _root;', '[SerializeField] RectTransform _root;\n        [SerializeField] RectTransform _propRoot;\n        public RectTransform PropRoot => _propRoot;',1)
s=body(s,'public void Refresh()', '''            var panel = UIModule.Instance.GetPage<RealGamePanel>();
            if (panel == null || panel.propEntries == null) return;
            foreach (var entry in panel.propEntries) entry.Refresh();''')
s=body(s,'void RefreshAdDependentButtons(bool adReady)','            Refresh();')
s=body(s,'void OnToolPressed(ToolButton tb)','            tb.GetComponent<UIPropEntry>()?.RequestUse();')
a=s.index('        IEnumerator ApplyEffect(')
b=s.index('        public void OpenSettings()',a)
s=s[:a]+s[b:]
s=body(s,'void OnSettingsPressed()', '            if (Page != null && !Page.IsRoundFinalized()) BizzaGameplayBridge.OpenSettings();')
s=body(s,'public void CheckUnlockPopupsDeferred()', '            if (Hint != null && Drop != null && Magnet != null) StartCoroutine(CheckUnlockCo());')
s=s.replace('return false || RewardedAds.CanRequest;', 'return true;')
p.write_text(s,encoding='utf-8')
p=Path('BizzaWZ/Assets/BizzaWZ/Common/UI/GamePanel/UIPropEntry.cs')
s=p.read_text(encoding='utf-8-sig').replace('    public void Refresh()','    public void RequestUse() => OnClickProp();\n\n    public void Refresh()')
p.write_text(s,encoding='utf-8')
p=Path('BizzaWZ/Assets/BizzaWZ/Common/UI/GamePanel/RealGamePanel.cs')
s=p.read_text(encoding='utf-8-sig').replace('        CreateRecoveredTopHud();\n','')
s=s.replace('        propEntries.Clear();','''        propEntries.Clear();
        var toolbar = BubblePics.App.I != null ? BubblePics.App.I.Page?.Toolbar : null;
        if (toolbar != null) propsRoot = toolbar.PropRoot;''')
s=s.replace('            propEntries.Add(_propEntry);','''            propEntries.Add(_propEntry);
            propEntryGo.transform.SetSiblingIndex(propEntries.Count - 1);''')
s=s.replace('    private void CreateRecoveredTopHud()', '''    private void BindGameplayToolbar()
    {
        var toolbar = BubblePics.App.I.Page.Toolbar;
        toolbar.Hint = propEntries[0].GetComponent<BubblePics.ToolButton>();
        toolbar.Drop = propEntries[1].GetComponent<BubblePics.ToolButton>();
        toolbar.Magnet = propEntries[2].GetComponent<BubblePics.ToolButton>();
        toolbar.BindPrefabRuntime();
    }

    private void CreateRecoveredTopHud()''')
s=s.replace('        InitPropEntries();','        InitPropEntries();\n        BindGameplayToolbar();')
p.write_text(s,encoding='utf-8')
p=Path('BizzaWZ/Assets/BubblePics/Scripts/App/BizzaGameplayBridge.cs')
s=p.read_text(encoding='utf-8-sig').replace('            FlowModule.CanShowGuide();','            FlowModule.CanShowGuide();\n            Page.Toolbar.CheckUnlockPopupsDeferred();')
p.write_text(s,encoding='utf-8')
