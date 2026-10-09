using System;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace BubblePics.EditorTools
{
 public static partial class AllUiAuthoring
 {
  static async UniTask ReviewBroadcastCore(string folder,Action<bool,string> check)
  {
   var p=BroadcastBarController.Instance;double balance=ItemUtils.Get(E_ItemType.Dollar).Count;await UniTask.Delay(1000,ignoreTimeScale:true);p.ShowMessage(new ItemEntry{Type=E_ItemType.Gold,Count=250},new ItemEntry{Type=E_ItemType.Dollar,Count=.05f});await UniTask.Delay(TimeSpan.FromSeconds(p.animDuration+.05f),ignoreTimeScale:true);check(p.gameObject.activeSelf&&p.entryAObj.activeSelf&&p.entryBObj.activeSelf,"First reward call displays both currencies");await PayPalCapture(folder,"Live-State.png");
   await UniTask.Delay(1000,ignoreTimeScale:true);p.ShowMessage(new ItemEntry{Type=E_ItemType.Gold,Count=250},new ItemEntry{Type=E_ItemType.Dollar,Count=.05f});p.entryAText.text="+250";p.entryBText.text="+$0.05";await UniTask.Delay(TimeSpan.FromSeconds(p.animDuration+.04f),ignoreTimeScale:true);await PayPalCapture(folder,"Reference-State.png");await UniTask.Delay(1000,ignoreTimeScale:true);check(!p.gameObject.activeSelf,"Reward strip fades out on its original timer");
   p.ShowMessage("Rewards added to your balance");await UniTask.Delay(TimeSpan.FromSeconds(p.animDuration+.05f),ignoreTimeScale:true);check(p.textObj.activeSelf&&!p.entryObj.activeSelf,"Text broadcasts retain a separate message state");p.textComponent.ForceMeshUpdate();check(!p.textComponent.isTextOverflowing,"Message fits the panel");await PayPalCapture(folder,"Message-State.png");await UniTask.Delay(1000,ignoreTimeScale:true);p.ShowMessage(new ItemEntry{Type=E_ItemType.Gold,Count=50},default);await UniTask.Delay(TimeSpan.FromSeconds(p.animDuration+.04f),ignoreTimeScale:true);check(p.entryAObj.activeSelf&&!p.entryBObj.activeSelf,"Single currency hides the unused cash slot");check(balance==ItemUtils.Get(E_ItemType.Dollar).Count,"View-only broadcast does not award money");await UniTask.Delay(1000,ignoreTimeScale:true);
  }
  static async UniTask ReviewFloatChestCore(string folder,Action<bool,string> check)
  {
   var p=(UIBizzaAAA)await UIModule.Instance.OpenPage(new PageId("UIBizzaAAA"));inspectedPage=p;var data=new SerializedObject(p);var button=(Button)data.FindProperty("btnFlowTreature").objectReferenceValue;check(!button.gameObject.activeSelf,"Entry honors its original initial wait");await UniTask.Delay(32500,ignoreTimeScale:true);check(button.gameObject.activeSelf,"Entry appears after production delay and tutorial eligibility");check(HitStandard(button),"Floating chest owns reachable native Button");var start=button.transform.localPosition;await PayPalCapture(folder,"Reference-State.png");await UniTask.Delay(600,ignoreTimeScale:true);check(Vector3.Distance(start,button.transform.localPosition)>5,"Floating chest retains its motion");PressStandard(button);await UniTask.Delay(1200,ignoreTimeScale:true);check(!button.gameObject.activeSelf,"Entry hides on activation");check(UIModule.Instance.PageIsOpen(UIPageIds.GetRewardPanel),"Chest opens the existing reward preview without claiming");UIModule.Instance.ClosePage(UIPageIds.GetRewardPanel);CloseRuntime();
  }
 }
}
