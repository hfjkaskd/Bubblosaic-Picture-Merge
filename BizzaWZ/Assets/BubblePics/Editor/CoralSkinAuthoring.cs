using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace BubblePics.EditorTools
{
    /// <summary>Offline prefab authoring and inspection. It never runs in a player or substitutes gameplay initialization.</summary>
    [InitializeOnLoad]
    public static class CoralSkinAuthoring
    {
        static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../ArtChanges/CoralV3-20260924"));
        static readonly string Validation = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Validation/CoralV3-20260924"));
        const string Art = "Assets/BubblePics/Resources/CoralV3/";
        static double nextPoll;
        [Serializable] public class Command { public string operation; }
        [Serializable] public class Spec { public PrefabEdit[] prefabs; }
        [Serializable] public class PrefabEdit { public string asset; public Edit[] edits; }
        [Serializable] public class Edit
        {
            public string path;
            public bool setRect;
            public Vector2 position, size;
            public bool setAnchor;
            public Vector2 anchor, pivot = new Vector2(.5f,.5f);
            public bool setScale;
            public Vector3 scale = Vector3.one;
            public int active = -1;
            public string art, resource;
            public bool image, renderer;
            public int imageEnabled = -1;
            public float fontSize;
            public string textColor, fontAsset;
            public Field[] fields;
            public bool portrait;
            public bool firstSibling;
        }
        [Serializable] public class Field { public string component, property, text, asset; public float number; public bool boolean; public string type; }

        static CoralSkinAuthoring() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + .5;
            string path = Path.Combine(Folder,"command.json");
            if (!File.Exists(path)) return;
            Command command = JsonUtility.FromJson<Command>(File.ReadAllText(path));
            File.Delete(path);
            try
            {
                if (command.operation == "dump") Dump();
                else if (command.operation == "import") Import();
                else if (command.operation == "apply") Apply();
                else if (command.operation == "inspect") Inspect();
                else if (command.operation == "smoke") UIModule.Instance.StartCoroutine(Smoke());
                else throw new InvalidOperationException(command.operation);
                File.WriteAllText(Path.Combine(Folder,"result.txt"), "PASS " + command.operation + " " + DateTime.UtcNow.ToString("O"));
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(Folder,"result.txt"),e.ToString()); Debug.LogException(e); }
        }

        static void Import()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring.");
            ConfigureTexture("CoralBackground.png",false);
            ConfigureTexture("CoralPortrait.png",false);
            var ti=ConfigureTexture("CoralUIAtlas.png",true);
            // Rectangles are authored against the delivered 1254 px atlas, top-left origin.
            string[] names={"Status","Currency","Conversion","Withdraw","Level","Settings","Tray","Tool","Slots","Crown","Count","Locked","Coin","Cash","Hint","Drop"};
            Rect[] r={new Rect(12,90,350,170),new Rect(364,114,361,125),new Rect(738,104,242,146),new Rect(986,105,254,145),
                new Rect(32,356,253,249),new Rect(343,356,253,249),new Rect(618,403,372,145),new Rect(1003,360,239,232),
                new Rect(20,640,290,281),new Rect(340,640,282,281),new Rect(703,691,189,192),new Rect(972,644,270,272),
                new Rect(43,969,235,234),new Rect(368,971,230,236),new Rect(697,969,188,232),new Rect(972,969,270,232)};
            var list=new SpriteRect[r.Length];
            for(int i=0;i<r.Length;i++)list[i]=new SpriteRect { name=names[i], rect=new Rect(r[i].x,1254-r[i].y-r[i].height,r[i].width,r[i].height),alignment=SpriteAlignment.Custom,pivot=new Vector2(.5f,.5f),spriteID=GUID.Generate() };
            SetSlices(ti,list);
            var portrait=(TextureImporter)AssetImporter.GetAtPath(Art+"CoralPortrait.png");
            portrait.spriteImportMode=SpriteImportMode.Multiple;
            portrait.SaveAndReimport();
            SetSlices(portrait,new[]{new SpriteRect { name="Portrait",rect=new Rect(136,138,1010,972),alignment=SpriteAlignment.Custom,pivot=new Vector2(.5f,.5f),spriteID=GUID.Generate() }});
            const string config=Art+"ToolAppearance.asset";
            var appearance=AssetDatabase.LoadAssetAtPath<ToolAppearance>(config);
            if(appearance==null){appearance=ScriptableObject.CreateInstance<ToolAppearance>();AssetDatabase.CreateAsset(appearance,config);}
            appearance.AtlasResource="CoralV3/CoralUIAtlas"; appearance.NormalSprite="Tool";appearance.LockedSprite="Locked";
            appearance.BackgroundAtlasResource="CoralV3/CoralThinFrames";
            appearance.HintSprite="Hint";appearance.DropSprite="Drop";appearance.MagnetResource="CoralV3/CoralPortrait";
            appearance.DisabledTint=new Color(.68f,.77f,.84f,1);
            EditorUtility.SetDirty(appearance);AssetDatabase.SaveAssets();
        }
        static TextureImporter ConfigureTexture(string name,bool multiple)
        {
            string path=Art+name;AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var ti=(TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType=TextureImporterType.Sprite;ti.spriteImportMode=multiple?SpriteImportMode.Multiple:SpriteImportMode.Single;
            ti.spritePixelsPerUnit=1;ti.alphaIsTransparency=true;ti.mipmapEnabled=false;ti.isReadable=false;
            ti.maxTextureSize=2048;ti.textureCompression=TextureImporterCompression.Uncompressed;
            ti.filterMode=FilterMode.Bilinear;ti.wrapMode=TextureWrapMode.Clamp;
            var settings=new TextureImporterSettings();ti.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;ti.SetTextureSettings(settings);
            var android=ti.GetPlatformTextureSettings("Android");android.overridden=true;android.maxTextureSize=2048;android.format=TextureImporterFormat.ASTC_4x4;ti.SetPlatformTextureSettings(android);
            var ios=ti.GetPlatformTextureSettings("iPhone");ios.overridden=true;ios.maxTextureSize=2048;ios.format=TextureImporterFormat.ASTC_4x4;ti.SetPlatformTextureSettings(ios);
            ti.SaveAndReimport();return ti;
        }
        static void SetSlices(TextureImporter ti,SpriteRect[] rects)
        {
            var factories=new SpriteDataProviderFactories();factories.Init();
            var provider=factories.GetSpriteEditorDataProviderFromObject(ti);provider.InitSpriteEditorDataProvider();
            provider.SetSpriteRects(rects);
            var names=provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            var pairs=new List<SpriteNameFileIdPair>();foreach(var rect in rects)pairs.Add(new SpriteNameFileIdPair(rect.name,rect.spriteID));
            names.SetNameFileIdPairs(pairs);provider.Apply();ti.SaveAndReimport();
        }

        static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before authoring.");
            Spec spec=JsonUtility.FromJson<Spec>(File.ReadAllText(Path.Combine(Folder,"layout.json")));
            foreach(var prefab in spec.prefabs)
            {
                string backup=Path.Combine(Folder,"Backup",prefab.asset);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));if(!File.Exists(backup))File.Copy(prefab.asset,backup);
                GameObject root=PrefabUtility.LoadPrefabContents(prefab.asset);
                try
                {
                    foreach(var edit in prefab.edits)
                    {
                        Transform t=string.IsNullOrEmpty(edit.path)?root.transform:root.transform.Find(edit.path);
                        if(t==null && edit.path.Contains("/GoldGroup/"))t=root.transform.Find(edit.path.Replace("/GoldGroup/","/GoldGroup/RealBtn/"));
                        if(t==null && edit.path.Contains("/DollarGroup/"))t=root.transform.Find(edit.path.Replace("/DollarGroup/","/DollarGroup/FakeBtn/"));
                        if(t==null)throw new InvalidOperationException(prefab.asset+" missing "+edit.path);
                        if(edit.firstSibling)t.SetAsFirstSibling();
                        if(edit.active>=0)t.gameObject.SetActive(edit.active!=0);
                        if(edit.setScale)t.localScale=edit.scale;
                        if(edit.setRect)
                        {
                            var rt=(RectTransform)t;
                            if(edit.setAnchor){rt.anchorMin=rt.anchorMax=edit.anchor;rt.pivot=edit.pivot;}
                            rt.anchoredPosition=edit.position;rt.sizeDelta=edit.size;
                        }
                        if(edit.art!=null && edit.art.Length>0 || edit.resource!=null && edit.resource.Length>0)
                            BindArt(t,edit.resource,edit.art,edit.image,edit.renderer);
                        Image image=t.GetComponent<Image>();if(image!=null && edit.imageEnabled>=0)image.enabled=edit.imageEnabled!=0;
                        var text=t.GetComponent<TMP_Text>();
                        if(text!=null)
                        {
                            if(!string.IsNullOrEmpty(edit.fontAsset)){text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(edit.fontAsset);text.fontSharedMaterial=text.font.material;}
                            if(edit.fontSize>0){text.fontSize=edit.fontSize;text.fontSizeMax=edit.fontSize;text.fontSizeMin=edit.fontSize*.7f;text.enableAutoSizing=true;}
                            if(!string.IsNullOrEmpty(edit.textColor) && ColorUtility.TryParseHtmlString(edit.textColor,out Color color)){text.color=color;text.enableVertexGradient=false;}
                        }
                        if(edit.fields!=null)foreach(var field in edit.fields)SetField(t,field);
                        if(edit.portrait)ConfigurePortrait(t);
                    }
                    if(prefab.asset.EndsWith("/GameUiWidget.prefab"))FinalizeCurrency(root.transform);
                    PrefabUtility.SaveAsPrefabAsset(root,prefab.asset);
                }
                finally {PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();Dump();
        }
        static void FinalizeCurrency(Transform root)
        {
            Transform currency=root.Find("GameplayHudHeader/CurrencyBar");
            if(currency==null)return;
            if(PrefabUtility.IsAnyPrefabInstanceRoot(currency.gameObject))
                PrefabUtility.UnpackPrefabInstance(currency.gameObject,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);
            foreach(string group in new[]{"GoldGroup","DollarGroup"})
            {
                Transform holder=currency.Find("CurrentGroup/"+group);
                Transform button=holder.Find(group=="GoldGroup"?"RealBtn":"FakeBtn");
                var children=new List<Transform>();foreach(Transform child in holder)if(child!=button)children.Add(child);
                foreach(Transform child in children)child.SetParent(button,false);
                button.SetAsFirstSibling();
            }
        }
        static void BindArt(Transform t,string resource,string name,bool image,bool renderer)
        {
            var binder=t.GetComponent<CoralResourceSprite>();if(binder==null)binder=t.gameObject.AddComponent<CoralResourceSprite>();
            var so=new SerializedObject(binder);
            so.FindProperty("_resourcePath").stringValue=UiBorderCalibration.FrameResource(string.IsNullOrEmpty(resource)?"CoralV3/CoralUIAtlas":resource,name);
            so.FindProperty("_spriteName").stringValue=name??"";
            if(image)
            {
                Image graphic=t.GetComponent<Image>();if(graphic==null)graphic=t.gameObject.AddComponent<Image>();
                graphic.sprite=null;graphic.color=Color.white;graphic.preserveAspect=false;graphic.type=Image.Type.Simple;
                UiBorderCalibration.ConfigureImage(graphic,so.FindProperty("_resourcePath").stringValue,name);
                Button button=t.GetComponent<Button>();
                BizzaButton legacy=t.GetComponent<BizzaButton>();
                if(button==null && legacy!=null)
                {
                    var bridge=t.gameObject.AddComponent<WithdrawCloudButton>();
                    var buttonData=new SerializedObject(bridge);buttonData.FindProperty("legacyOwner").objectReferenceValue=legacy;buttonData.ApplyModifiedPropertiesWithoutUndo();
                    var legacyData=new SerializedObject(legacy);legacyData.FindProperty("standardButton").objectReferenceValue=bridge;legacyData.ApplyModifiedPropertiesWithoutUndo();
                    button=bridge;
                }
                if(button!=null){button.targetGraphic=graphic;graphic.raycastTarget=true;}
                so.FindProperty("_image").objectReferenceValue=graphic;
            }
            if(renderer){var sr=t.GetComponent<SpriteRenderer>();sr.sprite=null;so.FindProperty("_renderer").objectReferenceValue=sr;}
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static void SetField(Transform t,Field f)
        {
            Component selected=null;foreach(Component c in t.GetComponents<Component>())if(c!=null && c.GetType().Name==f.component){selected=c;break;}
            if(selected==null)throw new InvalidOperationException(t.name+" missing component "+f.component);
            var so=new SerializedObject(selected);var p=so.FindProperty(f.property);
            if(p==null)throw new InvalidOperationException(f.component+" missing field "+f.property);
            if(f.type=="float")p.floatValue=f.number;
            else if(f.type=="int")p.intValue=(int)f.number;
            else if(f.type=="bool")p.boolValue=f.boolean;
            else if(f.type=="string")p.stringValue=f.text;
            else if(f.type=="asset")p.objectReferenceValue=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(f.asset);
            else throw new InvalidOperationException("Field type "+f.type);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static void ConfigurePortrait(Transform t)
        {
            var decoration=t.GetComponent<DolphinDecoration>();if(decoration==null)throw new InvalidOperationException("Missing portrait controller");
            Transform child=t.Find("CoralPortrait");
            if(child==null){var go=new GameObject("CoralPortrait",typeof(SpriteRenderer));child=go.transform;child.SetParent(t,false);}
            child.localPosition=Vector3.zero;child.localScale=Vector3.one*(232f/1010f/.72f);
            var sr=child.GetComponent<SpriteRenderer>();sr.sortingOrder=SortOrder.DolphinNormal;
            BindArt(child,"CoralV3/CoralPortrait","Portrait",false,true);
            var so=new SerializedObject(decoration);so.FindProperty("_portrait").objectReferenceValue=sr;
            so.FindProperty("_legacyPortraitRenderer").objectReferenceValue=t.Find("SpineSprite").GetComponent<MeshRenderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static string Relative(Transform t,Transform root)
        {
            string path=t.name;while(t.parent!=null && t!=root){t=t.parent;if(t!=root)path=t.name+"/"+path;}
            return path==root.name?"":path;
        }
        static void Dump()
        {
            string[] paths={"Assets/BizzaWZ/Final/Real/UI/GamePanel/Resources/Resources/GameUiWidget.prefab","Assets/BubblePics/Resources/Prefabs/FrameworkBubbleProp.prefab","Assets/BubblePics/RuntimePrefabs/UI/BubbleToolbar.prefab"};
            var report=new StringBuilder();
            foreach(string path in paths)
            {
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    report.AppendLine("PREFAB "+path);
                    foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        report.Append(Relative(t,root.transform)).Append(" active=").Append(t.gameObject.activeSelf);
                        var rt=t as RectTransform;if(rt!=null)report.Append(" pos=").Append(rt.anchoredPosition).Append(" size=").Append(rt.sizeDelta).Append(" anchors=").Append(rt.anchorMin).Append(" ").Append(rt.anchorMax);
                        report.AppendLine();
                        foreach(Component c in t.GetComponents<Component>())
                        {
                            if(c is Transform || c is CanvasRenderer)continue;
                            if(c==null){report.AppendLine("MISSING SCRIPT");continue;}
                            report.Append("  ").Append(c.GetType().Name);
                            if(c is Image img)report.Append(" sprite=").Append(AssetDatabase.GetAssetPath(img.sprite)).Append(" raycast=").Append(img.raycastTarget);
                            else if(c is TMP_Text text)report.Append(" text=").Append(text.text).Append(" fontSize=").Append(text.fontSize);
                            else if(c is MonoBehaviour)report.Append(" ").Append(EditorJsonUtility.ToJson(c));
                            report.AppendLine();
                        }
                    }
                }
                finally {PrefabUtility.UnloadPrefabContents(root);}
            }
            File.WriteAllText(Path.Combine(Folder,"unity-prefabs.txt"),report.ToString());
        }
        static void Inspect()
        {
            if(!EditorApplication.isPlaying || App.I==null)throw new InvalidOperationException("Gameplay is not running.");
            var report=new StringBuilder();report.AppendLine("scene="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            report.AppendLine("resolution="+Screen.width+"x"+Screen.height);
            foreach(Button button in UnityEngine.Object.FindObjectsOfType<Button>())
                report.AppendLine("BUTTON "+button.name+" interactable="+button.IsInteractable()+" graphic="+(button.targetGraphic!=null?button.targetGraphic.name:"null"));
            foreach(TMP_Text t in UnityEngine.Object.FindObjectsOfType<TMP_Text>())report.AppendLine("TEXT "+t.name+"="+t.text);
            File.WriteAllText(Path.Combine(Validation,"runtime-ui.txt"),report.ToString());
            ScreenCapture.CaptureScreenshot(Path.Combine(Validation,"after.png"));
        }

        static IEnumerator Smoke()
        {
            var report=new StringBuilder();int failed=0;
            Action<bool,string> check=(ok,message)=>{report.AppendLine((ok?"PASS ":"FAIL ")+message);if(!ok)failed++;File.WriteAllText(Path.Combine(Validation,"interaction-checks.txt"),report.ToString());};
            var page=BizzaGameplayBridge.Page;
            check(EditorApplication.isPlaying && page!=null,"Formal gameplay is running");
            if(page==null)yield break;
            var header=UnityEngine.Object.FindObjectOfType<GameplayHudHeader>();
            Transform currency=header.CurrencyRow;
            string[] buttonPaths={"PauseButton","CurrentGroup/GoldGroup/RealBtn","CurrentGroup/DollarGroup/FakeBtn"};
            PageId[] pageIds={UIPageIds.PausePanel,UIPageIds.RealWithdrawPanel,UIPageIds.FakeWithdrawPanel};
            for(int i=0;i<buttonPaths.Length;i++)
            {
                Button button=currency.Find(buttonPaths[i]).GetComponent<Button>();
                check(button!=null && button.IsInteractable() && Hits(button),buttonPaths[i]+" standard Button and raycast");
                if(button==null)continue;
                Click(button);yield return new WaitForSecondsRealtime(2);
                check(UIModule.Instance.PageIsOpen(pageIds[i]),buttonPaths[i]+" opens "+pageIds[i]);
                ScreenCapture.CaptureScreenshot(Path.Combine(Validation,"open-"+i+".png"));yield return null;
                if(i==0)
                {
                    var pause=UIModule.Instance.GetPage<PausePanel>();
                    if(pause!=null)Click(pause.CloseButton.GetComponent<Button>());
                }
                else UIModule.Instance.ClosePage(pageIds[i]);
                yield return new WaitForSecondsRealtime(1);
                check(!UIModule.Instance.PageIsOpen(pageIds[i]),buttonPaths[i]+" closes normally");
            }
            Transform entries=header.EntryRow;
            var slot=entries.Find("SlotEnter").GetComponent<Button>();
            check(slot!=null && Hits(slot),"777 standard Button and raycast");
            Click(slot);yield return new WaitForSecondsRealtime(2);
            check(UIModule.Instance.PageIsOpen(UIPageIds.SlotPanel),"777 opens original slots page");
            ScreenCapture.CaptureScreenshot(Path.Combine(Validation,"open-slots.png"));yield return null;
            UIModule.Instance.ClosePage(UIPageIds.SlotPanel);yield return new WaitForSecondsRealtime(1);
            var daily=entries.Find("DailyMissionItem/DailyMission").GetComponent<Button>();
            var crown=entries.Find("DailyMissionItem/Badge").GetComponent<Button>();
            Button reward=daily.gameObject.activeInHierarchy?daily:crown;
            PageId rewardId=reward==daily?UIPageIds.DailyMissionPanel:UIPageIds.WithdrawDanPanel;
            check(reward!=null && Hits(reward),"Country-specific reward Button and raycast");
            Click(reward);yield return new WaitForSecondsRealtime(2);
            check(UIModule.Instance.PageIsOpen(rewardId),"Country-specific reward opens original page");
            ScreenCapture.CaptureScreenshot(Path.Combine(Validation,"open-reward-entry.png"));yield return null;
            UIModule.Instance.ClosePage(rewardId);yield return new WaitForSecondsRealtime(1);
            foreach(var tool in new[]{page.Toolbar.Hint,page.Toolbar.Drop,page.Toolbar.Magnet})
            {
                var button=tool.GetComponentInChildren<Button>();
                check(button!=null && Hits(button),tool.Def.Id+" standard Button and raycast");
            }
            int hintBefore=SaveState.GetToolCount("hint");
            if(hintBefore>0 && page.CurrentLevelNumber>=page.Toolbar.Hint.Def.UnlockLevel)
            {
                int stepsBefore=page.StepsLeft;
                Click(page.Toolbar.Hint.GetComponentInChildren<Button>());
                yield return new WaitForSecondsRealtime(3);
                check(SaveState.GetToolCount("hint")==hintBefore-1,"Hint Button uses one real inventory item");
                check(page.StepsLeft==stepsBefore,"Hint preserves the remaining move count");
                ScreenCapture.CaptureScreenshot(Path.Combine(Validation,"hint-used.png"));yield return null;
            }
            int level=page.CurrentLevelNumber,count=SaveState.GetToolCount("magnet");
            page.Toolbar.Magnet.Refresh(9,0,false,true,true);
            check(page.Toolbar.Magnet.State==ToolState.Locked,"Locked tool state below level 10");
            yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Validation,"tool-locked-component-check.png"));yield return null;
            page.Toolbar.Magnet.Refresh(level,count,false,true,true);
            check(page.Toolbar.Magnet.State!=ToolState.Locked || level<10,"Tool state restored from live level/count");
            check(page.Input.InputEnabled && !BizzaGameplayBridge.IsInputBlocked,"Gameplay input restored after every page closes");
            foreach(var binding in UnityEngine.Object.FindObjectsOfType<CoralResourceSprite>())
            {
                Image img=binding.GetComponent<Image>();SpriteRenderer sr=binding.GetComponent<SpriteRenderer>();
                check((img!=null && img.sprite!=null)||(sr!=null && sr.sprite!=null),"Artwork loaded: "+binding.name);
            }
            report.AppendLine("Failures="+failed+"; live level="+level+"; remaining="+page.CountRemainingBubbles()+"; steps="+page.StepsLeft);
            File.WriteAllText(Path.Combine(Validation,"interaction-checks.txt"),report.ToString());
            ScreenCapture.CaptureScreenshot(Path.Combine(Validation,"after-tests.png"));
        }
        static void Click(Button button)
        {
            // Dispatch the complete standard Button pointer-click entry point, including
            // the existing PressButton subclass's tutorial-aware forwarding callback.
            ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        }
        static bool Hits(Button button)
        {
            if(button==null || button.targetGraphic==null)return false;
            Canvas canvas=button.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            Vector2 center=RectTransformUtility.WorldToScreenPoint(camera,button.targetGraphic.rectTransform.TransformPoint(button.targetGraphic.rectTransform.rect.center));
            var results=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=center},results);
            foreach(var result in results)
            {
                Button hit=result.gameObject.GetComponentInParent<Button>();
                if(hit!=null)return hit==button;
            }
            return false;
        }
    }
}
