using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;

namespace BubblePics.EditorTools
{
    public static class PauseReferenceAuthoring
    {
        public static void Apply()
        {
            var a=new ReferencePrefabTools("PausePanel",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/a26ed6416def-02-PausePanel-simple.png")),849,1852);
            a.Slice("Panel",10,615,829,960).Slice("Title",285,657,277,76).Slice("Close",698,665,108,110)
                .Slice("MusicRow",69,786,710,161).Slice("SoundRow",69,971,710,159).Slice("VibrationRow",69,1152,710,158)
                .Slice("MusicCaption",266,842,130,49).Slice("SoundCaption",266,1020,170,53).Slice("VibrationCaption",266,1203,208,55)
                .Slice("On",538,817,214,103).Slice("Off",537,1177,215,104).Slice("Restart",69,1337,353,143).Slice("Continue",428,1337,356,143);
            a.Import();a.Erase(a.Mat("Panel"),new Rect(275,651,300,88),new Rect(65,782,725,704),new Rect(690,655,119,120),new Rect(386,1491,74,45));a.Mat("Panel").SetVector("_SamplePoint",new Vector4(50,1080,1,0));a.Mat("Panel").SetFloat("_EraseFeather",6);a.Round(a.Mat("Panel"),a.R("Panel"),94,1);
            a.Mat("Title").SetFloat("_InkOnly",1);a.Round(a.Mat("Close"),a.R("Close"),53,1);
            a.Mat("Panel").SetVector("_TopCurve",new Vector4(424,617,414,95));
            foreach(string row in new[]{"MusicRow","SoundRow","VibrationRow"}){var r=a.R(row);a.Erase(a.Mat(row),new Rect(258,r.y+48,224,65),new Rect(529,r.y+18,228,126));a.Mat(row).SetFloat("_SampleX",506);a.Mat(row).SetFloat("_EraseFeather",6);a.Round(a.Mat(row),r,53,1);}
            foreach(string caption in new[]{"MusicCaption","SoundCaption","VibrationCaption"})a.Mat(caption).SetFloat("_InkOnly",1);
            a.Round(a.Mat("On"),a.R("On"),49,1);a.Round(a.Mat("Off"),a.R("Off"),49,1);
            foreach(string button in new[]{"Restart","Continue"}){var r=a.R(button);a.Round(a.Mat(button),r,68,1);var m=a.Material(button+"Blank");a.Erase(m,new Rect(r.x+67,r.y+40,r.width-128,66));m.SetFloat("_SampleX",r.x+44);a.Round(m,r,68,1);}
            const string path="Assets/BizzaWZ/Common/UI/SettingPanel/PausePanel.prefab";var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var page=go.GetComponent<PausePanel>();var tr=go.transform;var legacy=Child(tr,"LegacyLanguageConfiguration");page.languageDropdown.transform.SetParent(legacy,false);if(page.LanguageText!=null&&!page.LanguageText.transform.IsChildOf(legacy))page.LanguageText.transform.SetParent(legacy,false);legacy.gameObject.SetActive(false);
                for(int i=tr.childCount-1;i>=0;i--)if(tr.GetChild(i)!=legacy)UnityEngine.Object.DestroyImmediate(tr.GetChild(i).gameObject);Stretch(tr);
                var rootImage=tr.GetComponent<Image>();if(rootImage!=null)rootImage.enabled=false;var resource=tr.GetComponent<CoralResourceSprite>();if(resource!=null)resource.enabled=false;var animation=tr.GetComponent<Animation>();if(animation!=null)animation.enabled=false;
                a.Overlay(tr,new Color(0,.11f,.24f,.18f));a.Graphic(tr,"Panel");var title=a.Text(tr,"Title",new Rect(238,652,382,91),68);Localize(title,"settings");a.Caption(tr,title,"Title","Settings");page.CloseButton=a.Button(tr,"Close","Close");
                SetupSwitch(a,tr,"Music",a.R("MusicRow"),"MusicRow","Music",out var music,out var musicOn,out var musicOff);page.musicSwitchButton=music;page.musicOnIm=musicOn;page.musicOffIm=musicOff;
                SetupSwitch(a,tr,"Sound",a.R("SoundRow"),"SoundRow","sound",out var sound,out var soundOn,out var soundOff);page.soundSwitchButton=sound;page.soundOnIm=soundOn;page.soundOffIm=soundOff;
                SetupSwitch(a,tr,"Vibration",a.R("VibrationRow"),"VibrationRow","vibration",out var vibration,out var vibrationOn,out var vibrationOff);page.libSwitchButton=vibration;page.LibOnIm=vibrationOn;page.LibOffIm=vibrationOff;
                var game=Child(tr,"GamePauseGroup");Stretch(game);page.GamePauseGroup=game.gameObject;page.BackButton=a.Button(game,"Restart","Restart",key:"restart",caption:"Restart",size:51);page.ContinueButton=a.Button(game,"Continue","Continue",key:"continue",caption:"Continue",size:51);
                var main=Child(tr,"MainPauseGroup");Stretch(main);page.MainPauseGroup=main.gameObject;page.MainBackButton=a.Button(main,"Continue","Continue",new Rect(243,1337,356,143),"continue","Continue",51);Stretch(page.MainBackButton.transform.Find("ReferenceContinue"));Stretch(page.MainBackButton.transform.Find("ContinueLabel"));main.gameObject.SetActive(false);
                var version=a.Text(tr,"Version",new Rect(331,1493,187,44),29,color:new Color(.58f,.57f,.59f));Bind(page,"versionText",version);Validate(tr);PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
        }
        static void SetupSwitch(ReferencePrefabTools a,Transform parent,string name,Rect rect,string slice,string key,out BizzaButton button,out Image on,out Image off)
        {
            button=a.Button(parent,name,slice,rect);var label=a.Text(parent,name+"Label",new Rect(262,rect.y+43,254,76),45,TextAlignmentOptions.MidlineLeft);Localize(label,key);a.Caption(parent,label,name+"Caption",name);label.transform.SetParent(button.transform,true);parent.Find("Reference"+name+"Caption").SetParent(button.transform,true);
            var onRoot=Child(button.transform,"On");a.Local(onRoot,new Rect(469,31,214,103));on=a.Visual(onRoot,"On");var offRoot=Child(button.transform,"Off");a.Local(offRoot,new Rect(469,31,214,103));off=a.Visual(offRoot,"Off");
        }
    }
}
