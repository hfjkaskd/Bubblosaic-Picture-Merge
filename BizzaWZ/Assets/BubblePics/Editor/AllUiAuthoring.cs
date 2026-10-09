using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    // Offline authoring/inspection only. Does not replace runtime initialization or data.
    [InitializeOnLoad]
    public static partial class AllUiAuthoring
    {
        public static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath,"../../ArtChanges/AllUI-20260924"));
        [Serializable] public class Plan { public Page[] items; }
        [Serializable] public class Page { public string id,name,prefab,target; }
        [Serializable] public class Command { public string operation,page,output; }
        static double next;
        static AllUiAuthoring(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+1;
            string path=Path.Combine(Folder,"command.json");if(!File.Exists(path))return;
            var cmd=JsonUtility.FromJson<Command>(File.ReadAllText(path));File.Delete(path);
            try
            {
                if(cmd.operation=="inventory")Inventory();
                else if(cmd.operation=="import")ImportArt();
                else if(cmd.operation=="apply")ApplySkin();
                else if(cmd.operation=="audit")Audit();
                else if(cmd.operation=="previews")RenderPreviews();
                else if(cmd.operation=="runtime-open")OpenRuntime(cmd.page,cmd.output);
                else if(cmd.operation=="runtime-close")CloseRuntime();
                else if(cmd.operation=="interaction-smoke")InteractionSmoke();
                else if(cmd.operation=="register-victory")RegisterVictory();
                else if(cmd.operation=="refine-main-otter")MainOtterAuthoring.Apply();
                else if(cmd.operation=="review-main-otter")ReviewMainOtter();
                else if(cmd.operation=="refine-slot-entry-progress")SlotEntryProgressAuthoring.Apply();
                else if(cmd.operation=="review-slot-entry-progress")ReviewSlotEntryProgress();
                else if(cmd.operation=="refine-slot-reward-flight")SlotRewardFlightAuthoring.Apply();
                else if(cmd.operation=="refine-form-continue")FormPresentationAuthoring.ApplySingleMethodLayout();
                else if(cmd.operation=="refine-dialogs")UnityDialogFidelity.Apply();
                else if(cmd.operation=="dialog-smoke")DialogSmoke();
                else if(cmd.operation=="refine-paypal")PayPalReferenceAuthoring.Apply();
                else if(cmd.operation=="review-paypal")ReviewPayPal();
                else if(cmd.operation=="refine-confirm")ConfirmReferenceAuthoring.Apply();
                else if(cmd.operation=="review-confirm")ReviewConfirm();
                else if(cmd.operation=="refine-pending")PendingReferenceAuthoring.Apply();
                else if(cmd.operation=="review-pending")ReviewPending();
                else if(cmd.operation=="refine-history")HistoryReferenceAuthoring.Apply();
                else if(cmd.operation=="review-history")ReviewHistory();
                else if(cmd.operation=="refine-faq")FAQReferenceAuthoring.Apply();
                else if(cmd.operation=="refine-faq-accordion")FAQAccordionAuthoring.Apply();
                else if(cmd.operation=="review-faq")ReviewFAQ();
                else if(cmd.operation=="refine-tier")TierReferenceAuthoring.Apply();
                else if(cmd.operation=="review-tier")ReviewTier();
                else if(cmd.operation=="refine-newplayer")NewPlayerReferenceAuthoring.Apply();
                else if(cmd.operation=="review-newplayer")ReviewNewPlayer();
                else if(cmd.operation=="refine-sequential")SequentialReferenceAuthoring.Apply(cmd.page);
                else if(cmd.operation=="review-sequential")ReviewSequential(cmd.page);
                else throw new InvalidOperationException(cmd.operation);
                File.WriteAllText(Path.Combine(Folder,"result.txt"),"PASS "+cmd.operation+" "+DateTime.UtcNow.ToString("O"));
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Folder,"result.txt"),e.ToString());Debug.LogException(e);}
        }
        static void RegisterVictory()
        {
            RequireEdit();var settings=UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            if(settings==null)throw new InvalidOperationException("Addressables settings missing");
            string path="Assets/BizzaWZ/Common/BizzaGame/WhiteWinPanel/WhiteWinPanel.prefab";
            var group=settings.FindGroup("Default Local Group");Backup(AssetDatabase.GetAssetPath(group));
            var entry=settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path),group);entry.address="UIPanel/WhiteWinPanel";
            EditorUtility.SetDirty(settings);EditorUtility.SetDirty(group);AssetDatabase.SaveAssets();
        }
        public static void Inventory()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Inventory requires Edit Mode.");
            Directory.CreateDirectory(Path.Combine(Folder,"Inventory"));
            var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(Path.Combine(Folder,"plan.json")));
            var dependencies=new HashSet<string>();
            foreach(var page in plan.items)
            {
                Dump(page.id,page.prefab);
                foreach(string dependency in AssetDatabase.GetDependencies(page.prefab,true))
                    if(dependency.EndsWith(".prefab")&&(dependency.Contains("/UI/")||dependency.Contains("/Task/")))dependencies.Add(dependency);
            }
            foreach(string dependency in dependencies)Dump("Dependency-"+AssetDatabase.AssetPathToGUID(dependency)+"-"+Path.GetFileNameWithoutExtension(dependency),dependency);
            foreach(string p in new[]{
                "Assets/BizzaWZ/Final/Real/UI/GamePanel/Resources/Resources/GameUiWidget.prefab",
                "Assets/BubblePics/RuntimePrefabs/UI/TopGameBar.prefab",
                "Assets/BubblePics/RuntimePrefabs/UI/BubbleToolbar.prefab",
                "Assets/BizzaWZ/Final/Real/UI/RealWithdrawalPanl/WithdrawWay.prefab",
                "Assets/BizzaWZ/Final/Real/UI/RealWithdrawalPanl/WithdrawLevelItem.prefab"})
                if(File.Exists(p))Dump(Path.GetFileNameWithoutExtension(p),p);
            File.WriteAllText(Path.Combine(Folder,"inventory-complete.txt"),"PASS "+plan.items.Length+" pages "+DateTime.UtcNow.ToString("O"));
        }
        static void Dump(string id,string asset)
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(asset);if(root==null)throw new FileNotFoundException(asset);
            var b=new StringBuilder(asset+"\n");
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                string path=AnimationUtility.CalculateTransformPath(t,root.transform);
                b.Append("\n[").Append(path).Append("] active=").Append(t.gameObject.activeSelf).Append(" scale=").Append(t.localScale);
                var rt=t as RectTransform;if(rt!=null)b.Append(" pos=").Append(rt.anchoredPosition).Append(" size=").Append(rt.sizeDelta).Append(" anchors=").Append(rt.anchorMin).Append("..").Append(rt.anchorMax).Append(" pivot=").Append(rt.pivot);
                foreach(var c in t.GetComponents<Component>())
                {
                    if(c==null){b.Append("\n MISSING SCRIPT");continue;}
                    if(c is Transform)continue;
                    b.Append("\n ").Append(c.GetType().FullName);
                    if(c is Image im)b.Append(" sprite=").Append(im.sprite!=null?AssetDatabase.GetAssetPath(im.sprite)+"/"+im.sprite.name:"null").Append(" color=").Append(im.color).Append(" enabled=").Append(im.enabled).Append(" raycast=").Append(im.raycastTarget);
                    if(c is TMP_Text text)b.Append(" text=").Append(text.text.Replace("\n","\\n")).Append(" font=").Append(AssetDatabase.GetAssetPath(text.font)).Append(" size=").Append(text.fontSize).Append(" color=").Append(text.color);
                    if(c is Button btn)b.Append(" target=").Append(btn.targetGraphic!=null?AnimationUtility.CalculateTransformPath(btn.targetGraphic.transform,root.transform):"null").Append(" persistent=").Append(btn.onClick.GetPersistentEventCount());
                    if(c is MonoBehaviour && !(c is Graphic)&&!(c is LayoutGroup)&&!(c is Selectable))
                    {
                        var so=new SerializedObject(c);var p=so.GetIterator();bool enter=true;
                        while(p.NextVisible(enter))
                        {
                            enter=p.propertyType==SerializedPropertyType.Generic;
                            if(p.name=="m_Script")continue;
                            if(p.propertyType==SerializedPropertyType.ObjectReference)
                            {
                                var value=p.objectReferenceValue;
                                b.Append("\n  ").Append(p.propertyPath).Append(" -> ");
                                if(value is Component comp)b.Append(AnimationUtility.CalculateTransformPath(comp.transform,root.transform));
                                else if(value is GameObject go)b.Append(AnimationUtility.CalculateTransformPath(go.transform,root.transform));
                                else b.Append(value!=null?AssetDatabase.GetAssetPath(value):"null");
                            }
                            else if(p.propertyType==SerializedPropertyType.String)b.Append("\n  ").Append(p.propertyPath).Append(" = ").Append(p.stringValue.Replace("\n","\\n"));
                        }
                    }
                }
                b.Append('\n');
            }
            File.WriteAllText(Path.Combine(Folder,"Inventory",id+".txt"),b.ToString());
        }
    }
}
