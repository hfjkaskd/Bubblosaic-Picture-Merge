using System;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async void DialogSmoke()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/UnityAllUI-20260928/Checks"));Directory.CreateDirectory(folder);
            string output=Path.Combine(folder,"interaction.txt");var report=new StringBuilder();int failed=0;
            void Check(bool ok,string name){report.AppendLine((ok?"PASS ":"FAIL ")+name);if(!ok)failed++;File.WriteAllText(output,report.ToString());}
            try
            {
                if(!EditorApplication.isPlaying||BizzaGameplayBridge.Page==null)throw new InvalidOperationException("Run the formal InitWZ flow first.");
                CloseRuntime();await UniTask.Delay(200,ignoreTimeScale:true);
                var pause=(PausePanel)await UIModule.Instance.OpenPage(UIPageIds.PausePanel);await UniTask.Delay(250,ignoreTimeScale:true);
                var settings=SaveDataUtils.SettingData;bool[] original={settings.enableMusic,settings.enableSound,settings.enableVibrate};
                var buttons=new[]{pause.musicSwitchButton,pause.soundSwitchButton,pause.libSwitchButton};
                try
                {
                    for(int i=0;i<buttons.Length;i++)
                    {
                        var button=buttons[i].GetComponent<Button>();Check(HitStandard(button),"Settings visible Button hit "+i);PressStandard(button);await UniTask.DelayFrame(2);
                        bool state=i==0?settings.enableMusic:i==1?settings.enableSound:settings.enableVibrate;
                        Check(state!=original[i],"Setting changed "+i);PressStandard(button);await UniTask.DelayFrame(2);
                        state=i==0?settings.enableMusic:i==1?settings.enableSound:settings.enableVibrate;Check(state==original[i],"Setting restored "+i);
                    }
                }
                finally{settings.enableMusic=original[0];settings.enableSound=original[1];settings.enableVibrate=original[2];pause.Refresh();global::SoundManager.Instance.MuteBGM(!original[0]);global::SoundManager.Instance.MuteSFX(!original[1]);}
                var resume=pause.ContinueButton.GetComponent<Button>();Check(HitStandard(resume),"Settings continue Button hit");PressStandard(resume);await UniTask.Delay(200,ignoreTimeScale:true);Check(!UIModule.Instance.PageIsOpen(UIPageIds.PausePanel),"Continue closes settings");
                var mission=(DailyMissionPanel)await UIModule.Instance.OpenPage(UIPageIds.DailyMissionPanel);await UniTask.Delay(2000,ignoreTimeScale:true);
                var serialized=new SerializedObject(mission);var fill=(Image)serialized.FindProperty("taskProgressFill").objectReferenceValue;Check(fill!=null&&fill.type==Image.Type.Filled&&fill.fillMethod==Image.FillMethod.Horizontal,"Daily progress retains horizontal fill");
                float originalFill=fill.fillAmount;
                try
                {
                    foreach(float value in new[]{0f,.5f,1f}){fill.fillAmount=value;await UniTask.DelayFrame(2);string image=Path.Combine(folder,"Progress-"+Mathf.RoundToInt(value*100)+".png");DateTime request=DateTime.UtcNow;ScreenCapture.CaptureScreenshot(image);for(int i=0;i<30&&(!File.Exists(image)||File.GetLastWriteTimeUtc(image)<request);i++)await UniTask.Delay(100,ignoreTimeScale:true);Check(File.Exists(image)&&File.GetLastWriteTimeUtc(image)>=request,"Explicit rendering fixture progress "+value);}
                }
                finally{fill.fillAmount=originalFill;}
                var missionClose=mission.transform.Find("Title/CloseBtn").GetComponent<Button>();Check(HitStandard(missionClose),"Daily mission close Button hit");PressStandard(missionClose);await UniTask.Delay(200,ignoreTimeScale:true);Check(!UIModule.Instance.PageIsOpen(UIPageIds.DailyMissionPanel),"Daily mission closes");
                foreach(var type in new[]{E_ItemType.GameProp_1,E_ItemType.GameProp_2,E_ItemType.GameProp_3})
                {
                    var prop=(AddPropPanel)await UIModule.Instance.OpenPage(UIPageIds.AddPropPanel,type);await UniTask.Delay(250,ignoreTimeScale:true);Check(prop.propIcon.sprite!=null&&!string.IsNullOrEmpty(prop.propName.text),"Prop variant has dynamic icon and name "+(int)type);Check(HitStandard(prop.adBuyBtn.GetComponent<Button>()),"Prop ad Button visible and reachable "+(int)type);Check(HitStandard(prop.closeBtn.GetComponent<Button>()),"Prop close Button visible and reachable "+(int)type);PressStandard(prop.closeBtn.GetComponent<Button>());await UniTask.Delay(200,ignoreTimeScale:true);Check(!UIModule.Instance.PageIsOpen(UIPageIds.AddPropPanel),"Prop closes "+(int)type);
                }
                Check(BizzaGameplayBridge.Page.Input.InputEnabled&&!BizzaGameplayBridge.IsInputBlocked,"Gameplay input remains enabled");
            }
            catch(Exception e){Check(false,e.ToString());Debug.LogException(e);}
            report.AppendLine("Failures="+failed+" Completed="+DateTime.UtcNow.ToString("O"));report.AppendLine("No advertisements, rewards, payout submissions, or support messages triggered. Progress fixture screenshots are explicitly simulated visual states; live values were restored.");File.WriteAllText(output,report.ToString());
        }
    }
}
