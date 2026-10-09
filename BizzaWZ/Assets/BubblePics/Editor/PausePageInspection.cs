using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewPauseCore(string folder,Action<bool,string> check)
        {
            var settings=SaveDataUtils.SettingData;bool music=settings.enableMusic,sound=settings.enableSound,vibration=settings.enableVibrate;PausePanel page=null;
            try
            {
                page=(PausePanel)await UIModule.Instance.OpenPage(new PageId("PausePanel"));inspectedPage=page;World.Current.Pause(page);await UniTask.DelayFrame(4);
                if(!settings.enableMusic)page.SwitchMusic();if(!settings.enableSound)page.SwitchSound();if(settings.enableVibrate)page.SwitchLib();await UniTask.DelayFrame(5);await PayPalCapture(folder,"Reference-State.png");
                check(World.Current.IsPause,"Pause screen pauses game state");
                foreach(var owner in new[]{page.CloseButton,page.BackButton,page.ContinueButton,page.musicSwitchButton,page.soundSwitchButton,page.libSwitchButton})check(HitStandard(owner.GetComponent<Button>()),"Visible native Button owns artwork: "+owner.name);
                PressStandard(page.musicSwitchButton.GetComponent<Button>());check(!settings.enableMusic&&page.musicOffIm.gameObject.activeSelf&&!page.musicOnIm.gameObject.activeSelf,"Music setting and switch visual turn off together");PressStandard(page.musicSwitchButton.GetComponent<Button>());check(settings.enableMusic,"Music switch turns on again");
                PressStandard(page.soundSwitchButton.GetComponent<Button>());check(!settings.enableSound&&page.soundOffIm.gameObject.activeSelf,"Sound setting and switch visual turn off together");PressStandard(page.soundSwitchButton.GetComponent<Button>());check(settings.enableSound,"Sound switch turns on again");
                PressStandard(page.libSwitchButton.GetComponent<Button>());check(settings.enableVibrate&&page.LibOnIm.gameObject.activeSelf,"Vibration setting and switch visual turn on together");PressStandard(page.libSwitchButton.GetComponent<Button>());check(!settings.enableVibrate,"Vibration switch turns off again");
                foreach(string lang in new[]{"pt_BR","id","zh_CN"}){Localization.SetLocale(lang);await UniTask.DelayFrame(5);foreach(var label in page.GetComponentsInChildren<TMP_Text>()){label.ForceMeshUpdate();check(!label.isTextOverflowing,"Setting caption fits "+lang+"/"+label.name);}await PayPalCapture(folder,lang+".png");}
                check(page.transform.Find("Version").GetComponent<TMP_Text>().text=="v"+Application.version,"Version shows actual player version");
                if(settings.enableMusic!=music)page.SwitchMusic();if(settings.enableSound!=sound)page.SwitchSound();if(settings.enableVibrate!=vibration)page.SwitchLib();PressStandard(page.ContinueButton.GetComponent<Button>());await UniTask.DelayFrame(4);check(!UIModule.Instance.PageIsOpen(new PageId("PausePanel"))&&!World.Current.IsPause,"Continue closes settings and resumes game");inspectedPage=null;
                page=(PausePanel)await UIModule.Instance.OpenPage(new PageId("PausePanel"));inspectedPage=page;World.Current.Pause(page);await UniTask.DelayFrame(3);PressStandard(page.CloseButton.GetComponent<Button>());await UniTask.DelayFrame(3);check(!UIModule.Instance.PageIsOpen(new PageId("PausePanel"))&&!World.Current.IsPause,"Close also resumes game");inspectedPage=null;
            }
            finally
            {
                if(page!=null){if(settings.enableMusic!=music)page.SwitchMusic();if(settings.enableSound!=sound)page.SwitchSound();if(settings.enableVibrate!=vibration)page.SwitchLib();World.Current.Resume(page);}CloseRuntime();
            }
        }
    }
}
