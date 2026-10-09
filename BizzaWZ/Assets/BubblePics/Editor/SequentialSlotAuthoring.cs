using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;
namespace BubblePics.EditorTools
{
    public static class SequentialSlotAuthoring
    {
        public const string PathName="Assets/BizzaWZ/Final/Real/UI/SlotsPanel/SlotPanel/SlotPanel.prefab";
        public static void Apply()
        {
            var a=new ReferencePrefabTools("SlotPanel",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/3a28c1d5d114-15-SlotPanel-simple.png")),849,1852);
            a.Slice("Background",0,0,849,1852).Slice("Back",18,69,106,108).Slice("FAQ",727,70,104,106).Slice("Header",188,66,474,128).Slice("Cabinet",29,505,791,655)
                .Slice("Shell",104,811,185,170).Slice("Star",332,800,182,184).Slice("Pearl",554,817,188,173).Slice("BlueShell",157,706,77,70).Slice("SmallStar",389,707,66,65)
                .Slice("Spin",112,1394,626,183).Slice("SpinCaption",399,1430,188,104).Slice("AdIcon",268,1424,118,115).Slice("SpinsLabel",316,1227,160,60).Slice("Hint",39,1188,771,191).Slice("CoinsAmount",231,277,214,72).Slice("CashAmount",611,292,169,69);
            a.Import();SequentialContourAuthoring.ImportSpriteOutlines(a,"Slot","Shell","Star","Pearl","BlueShell","SmallStar");
            var extra=new ReferencePrefabTools("SlotSymbols",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/f83716ad167a-16-SlotFAQPanel-simple.png")),849,1852);extra.Slice("Drink",88,931,78,88).Slice("Diamond",88,1067,78,86);extra.Import();SequentialContourAuthoring.ImportSpriteOutlines(extra,"Slot","Drink","Diamond");
            // Rebuild from the clean local top/bottom edges, never from the bright outer frame.
            // Both balances use a single live-text row next to their existing currency icons.
            a.Erase(a.Mat("Background"),new Rect(228,277,222,103),new Rect(609,290,172,73));a.Mat("Background").SetFloat("_EraseFeather",3);
            a.Erase(a.Mat("Hint"),new Rect(481,1217,62,75),new Rect(210,1300,426,46));a.Mat("Hint").SetVector("_SamplePoint",new Vector4(758,1288,1,0));a.Mat("Hint").SetFloat("_EraseFeather",5);a.Round(a.Mat("Hint"),a.R("Hint"),73);
            a.Erase(a.Mat("Cabinet"),new Rect(103,809,189,175),new Rect(330,798,188,189),new Rect(550,796,198,201));a.Mat("Cabinet").SetVector("_SamplePoint",new Vector4(295,800,1,0));a.Mat("Cabinet").SetFloat("_EraseFeather",13);
            var h=a.Material("HeaderBlank");a.Erase(h,new Rect(228,88,394,76));h.SetFloat("_EraseFeather",2);a.Round(h,a.R("Header"),59);a.Round(a.Mat("Header"),a.R("Header"),59);
            var spin=a.Material("SpinBlank");a.Erase(spin,new Rect(258,1420,344,126));spin.SetFloat("_SampleX",622);spin.SetFloat("_EraseFeather",5);a.Round(spin,a.R("Spin"),89);a.Round(a.Mat("Spin"),a.R("Spin"),89);
            a.Round(a.Mat("Back"),a.R("Back"),52);a.Round(a.Mat("FAQ"),a.R("FAQ"),52);
            var go=PrefabUtility.LoadPrefabContents(PathName);
            try
            {
                var p=go.GetComponent<SlotPanel>();var t=go.transform;foreach(var tr in t.GetComponentsInChildren<Transform>(true))if(tr!=t&&PrefabUtility.IsAnyPrefabInstanceRoot(tr.gameObject))PrefabUtility.UnpackPrefabInstance(tr.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                foreach(var tr in t.GetComponentsInChildren<Transform>(true))if(tr!=null&&((tr.parent==t&&(tr.name.StartsWith("Sequential")||tr.name.StartsWith("Reference")))||tr.name=="PageBackdrop"||tr.name=="OpaqueUnderlay"||tr.name=="CurrencyPlate"))Object.DestroyImmediate(tr.gameObject);
                Stretch(t);t.Find("bg").gameObject.SetActive(false);var top=t.Find("Top");var safe=top.GetComponent<TopSafeAreaAdapter>();if(safe!=null)Object.DestroyImmediate(safe);Stretch(top);
                foreach(var graphic in top.GetComponentsInChildren<Graphic>(true))graphic.enabled=false;
                Stretch(t.Find("Content"));var manager=p.slotMachineManager;var mt=manager.transform;Stretch(mt);
                foreach(var graphic in mt.GetComponentsInChildren<Graphic>(true))graphic.enabled=false;
                // Spine remains running for the original completion callbacks; only its old cabinet art is hidden.
                manager.anim.enabled=true;manager.anim.color=Color.clear;
                var cn=mt.Find("Content");Stretch(cn);var rewards=manager.rewardGourp.transform;var follower=rewards.GetComponent<Spine.Unity.BoneFollowerGraphic>();if(follower!=null)Object.DestroyImmediate(follower);Stretch(rewards);
                var mask=manager.slotEntries[0].transform.parent;a.Place(mask,new Rect(89,691,669,410));mask.GetComponent<Image>().enabled=true;mask.GetComponent<Image>().color=Color.white;mask.GetComponent<Mask>().showMaskGraphic=false;
                var background=a.Graphic(t,"SequentialBackground","Background");background.SetAsFirstSibling();var cabinet=a.Graphic(t,"SequentialCabinet","Cabinet");cabinet.SetSiblingIndex(1);
                for(int i=0;i<manager.slotEntries.Length;i++)
                {
                    var entry=manager.slotEntries[i];a.Local(entry.transform,new Rect(9+i*224,108,195,192));
                    // Preserve the resting layout while scaling the reward pulse about each reel's center.
                    var entryRect=(RectTransform)entry.transform;
                    entryRect.pivot=new Vector2(.5f,.5f);
                    entryRect.anchoredPosition+=new Vector2(entryRect.sizeDelta.x*.5f,-entryRect.sizeDelta.y*.5f);
                    foreach(var im in new[]{entry.img1,entry.img2}){Stretch(im.transform);im.transform.localScale=Vector3.one;im.enabled=true;im.raycastTarget=false;im.color=Color.white;im.material=null;im.type=Image.Type.Simple;im.useSpriteMesh=true;im.preserveAspect=true;var d=new SerializedObject(Ensure<CoralResourceSprite>(im));d.FindProperty("_resourcePath").stringValue=a.Resource;d.FindProperty("_spriteName").stringValue=new[]{"Shell","Star","Pearl"}[i];d.FindProperty("_image").objectReferenceValue=im;d.ApplyModifiedPropertiesWithoutUndo();im.sprite=null;}
                    var reward=new[]{manager.rewardIm1,manager.rewardIm2,manager.rewardIm3}[i];reward.useSpriteMesh=true;reward.preserveAspect=true;reward.material=null;
                }
                var data=new SerializedObject(manager);data.FindProperty("symbolAtlasPath").stringValue=a.Resource;var names=data.FindProperty("symbolNames");names.arraySize=5;var resources=data.FindProperty("symbolResources");resources.arraySize=5;var symbols=new[]{"Shell","Star","Pearl","Drink","Diamond"};for(int i=0;i<5;i++){names.GetArrayElementAtIndex(i).stringValue=symbols[i];resources.GetArrayElementAtIndex(i).stringValue=i<3?a.Resource:extra.Resource;data.FindProperty("slotEntryss").GetArrayElementAtIndex(i).FindPropertyRelative("sprite").objectReferenceValue=null;}data.ApplyModifiedPropertiesWithoutUndo();
                var pulse=new SerializedObject(manager);pulse.FindProperty("rewardPulseScale").floatValue=1.12f;pulse.ApplyModifiedPropertiesWithoutUndo();
                if(p.closeBtn!=null)p.closeBtn.gameObject.SetActive(false);if(p.faqBtn!=null)p.faqBtn.gameObject.SetActive(false);if(p.slotBtn!=null)p.slotBtn.gameObject.SetActive(false);if(p.slotHintTxt!=null)p.slotHintTxt.gameObject.SetActive(false);
                p.closeBtn=a.Button(t,"SequentialBack","Back");p.faqBtn=a.Button(t,"SequentialFAQ","FAQ");a.Graphic(t,"SequentialHeader","Header").GetComponent<Image>().material=h;var title=a.Text(t,"SequentialTitle",new Rect(233,91,384,72),50,TextAlignmentOptions.Midline);Localize(title,"seq_coral_rewards");a.Caption(t,title,"Header","Coral Rewards");
                var coin=t.Find("Top/IconInfo").GetComponent<CurrencyInfo>();var dollar=t.Find("Top/DollarInfo").GetComponent<CurrencyInfo>();coin.valueText=a.Text(t,"SequentialCoins",new Rect(236,293,211,70),42,TextAlignmentOptions.MidlineLeft);dollar.valueText=a.Text(t,"SequentialCash",new Rect(605,293,174,70),42,TextAlignmentOptions.MidlineLeft);p.slotRewardPanel.coinTargetPos=coin.valueText.transform;p.slotRewardPanel.dollarTargetPos=dollar.valueText.transform;
                a.Graphic(t,"SequentialHintPlate","Hint");var count=a.Text(t,"SequentialSpinCount",new Rect(480,1216,68,84),84,color:new Color(0,.63f,1));Bind(p,"spinCountText",count);p.slotHintTxt=a.Text(t,"SequentialHint",new Rect(174,1296,502,53),31);p.slotHintTxt.enableWordWrapping=true;
                p.slotBtn=a.Button(t,"SequentialSpin","Spin");p.slotBtn.GetComponent<Image>().material=spin;
                var free=Child(p.slotBtn.transform,"Free");Stretch(free);var paid=Child(p.slotBtn.transform,"Rewarded");Stretch(paid);p.canClickObj=free.gameObject;p.notCanClickObj=paid.gameObject;
                foreach(var group in new[]{free,paid}){var label=a.Text(t,"SequentialSpinLabel",new Rect(399,1430,188,104),88,color:Color.white);Localize(label,"seq_spin");a.Caption(t,label,"SpinCaption","Spin");label.transform.SetParent(group,true);t.Find("ReferenceSpinCaption").SetParent(group,true);if(group==free){label.rectTransform.anchoredPosition=Vector2.zero;var caption=group.Find("ReferenceSpinCaption").GetComponent<RectTransform>();caption.anchoredPosition=Vector2.zero;}}
                var video=a.Graphic(t,"SequentialAd","AdIcon");video.SetParent(paid,true);
                // The reward modal must remain above newly authored machine controls.
                p.slotRewardPanel.transform.SetAsLastSibling();
                Validate(t);PrefabUtility.SaveAsPrefabAsset(go,PathName);
            }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
            SlotRewardFlightAuthoring.Apply();
        }
    }
}
