#if BIZZA_REAL_WITHDRAW
using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    // Disposable prefab previews, using the actual country resolver. No login or save changes.
    public static class CountryCurrencyInspection
    {
        const string Folder=CountryCurrencyAuthoring.Folder;
        static readonly AccountModule.E_CountryType[] Countries={AccountModule.E_CountryType.US,AccountModule.E_CountryType.BR,AccountModule.E_CountryType.ID};
        public static void ApplyAndVerify(){CountryCurrencyAuthoring.Apply();Run();}
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(Folder); var report=new StringBuilder();int count=0;
            try
            {
                foreach(var country in Countries)
                foreach(E_WzIconType type in Enum.GetValues(typeof(E_WzIconType)))
                foreach(bool single in new[]{false,true})
                {
                    Sprite sprite=CountryCurrencyIcons.Load(type,country,single);
                    string key=CountryCurrencyIcons.IconKey(CountryCurrencyIcons.ResolveType(type,single))+"_"+CountryCurrencyIcons.CountryCode(country);
                    string path="Assets/BizzaWZ/Final/Real/GameAssets/WzTexture_Money/"+key+".png";
                    Require(sprite!=null&&AssetDatabase.GetAssetPath(sprite)==path,"Original Addressables sprite mismatch: "+key);
                    Require(CountryCurrencyIcons.Load(type,country,single)==sprite,"Cache mismatch: "+key);count++;
                }
                report.AppendLine("PASS original country resource loading + cache: "+count+" country/type/mode combinations");
                int bindings=0;
                foreach(string path in CountryCurrencyAuthoring.Paths)
                {
                    var go=PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        foreach(var icon in go.GetComponentsInChildren<WzIconAmend>(true))
                        {
                            icon.ApplyForCountry(Countries[0],false);
                            Require(icon.image!=null,"Missing image: "+path+"/"+icon.name);
                            Require(icon.GetComponent<CoralResourceSprite>()==null,"Competing skin binder: "+icon.name);
                            var rect=icon.image.rectTransform;Vector2 position=rect.anchoredPosition,size=rect.sizeDelta;
                            E_WzIconType original=icon.iconType;
                            foreach(var country in new[]{Countries[0],Countries[1],Countries[2],Countries[0]})
                            foreach(bool single in new[]{true,false})
                            {
                                icon.ApplyForCountry(country,single);
                                Require(icon.image.sprite==CountryCurrencyIcons.Load(original,country,single),"Binding mismatch: "+icon.name);
                                Require(icon.iconType==original,"Single currency mode mutated configured type");
                                Require(rect.anchoredPosition==position&&rect.sizeDelta==size,"Currency update changed authored geometry");
                                Require(icon.image.preserveAspect,"Aspect ratio lost");
                            }
                            bindings++;
                        }
                    }
                    finally{PrefabUtility.UnloadPrefabContents(go);}
                }
                report.AppendLine("PASS "+bindings+" prefab icon bindings: US → BR → ID → US, single/dual currency, unchanged positions and sizes");
                foreach(var country in Countries)
                {
                    Render(CountryCurrencyAuthoring.Paths[7],country,"reward",report);
                    Render(CountryCurrencyAuthoring.Paths[3],country,"tier",report,1080,520);
                    Render(CountryCurrencyAuthoring.Paths[8],country,"slots",report);
                    Render(CountryCurrencyAuthoring.Paths[8],country,"slot-reward",report);
                    Render(CountryCurrencyAuthoring.Paths[9],country,"slot-guide",report);
                }
                for(int i=10;i<=13;i++) Render(CountryCurrencyAuthoring.Paths[i],Countries[1],Path.GetFileNameWithoutExtension(CountryCurrencyAuthoring.Paths[i]),report);
                report.AppendLine("PASS isolated Unity prefab renders. No account, rewards, ads or save data modified. No Android build/device interaction in this check.");
            }
            catch(Exception ex){report.AppendLine("FAIL "+ex);throw;}
            finally{File.WriteAllText(Path.Combine(Folder,"verification.txt"),report.ToString());}
        }
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static void Prepare(GameObject go,AccountModule.E_CountryType country)
        {
            foreach(var binding in go.GetComponentsInChildren<CoralResourceSprite>(true))
            {
                var so=new SerializedObject(binding);string resource=so.FindProperty("_resourcePath").stringValue,name=so.FindProperty("_spriteName").stringValue;
                binding.SetSource(resource,name);
                var image=binding.GetComponent<Image>();
                Require(image==null||image.sprite!=null,"Missing prefab art: "+resource+"/"+name);
            }
            foreach(var icon in go.GetComponentsInChildren<WzIconAmend>(true))icon.ApplyForCountry(country,false);
            foreach(var label in go.GetComponentsInChildren<TMP_Text>(true))
            {
                // In Edit Mode a previously baked caption may have left the live sample hidden.
                var group=label.GetComponent<CanvasGroup>();if(group!=null)group.alpha=1;
            }
            foreach(var caption in go.GetComponentsInChildren<ApprovedHudCaption>(true))caption.gameObject.SetActive(false);
        }
        static void Render(string path,AccountModule.E_CountryType country,string name,StringBuilder report,int width=1080,int height=2360)
        {
            Scene scene=EditorSceneManager.NewPreviewScene();RenderTexture target=new RenderTexture(width,height,24);Texture2D capture=null;var previous=RenderTexture.active;
            try
            {
                var camGo=new GameObject("Currency preview camera",typeof(Camera));SceneManager.MoveGameObjectToScene(camGo,scene);
                var camera=camGo.GetComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=height/2f;camera.aspect=(float)width/height;camera.transform.position=new Vector3(0,0,-100);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.04f,.21f,.38f);camera.targetTexture=target;
                var canvasGo=new GameObject("Currency preview",typeof(Canvas));SceneManager.MoveGameObjectToScene(canvasGo,scene);var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;((RectTransform)canvas.transform).sizeDelta=new Vector2(width,height);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                go.transform.SetParent(canvas.transform,false);go.SetActive(true);
                if(name=="tier")
                {
                    var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(490,230);rect.anchoredPosition=Vector2.zero;rect.localScale=Vector3.one*1.8f;
                }
                else ReferencePrefabTools.Stretch(go.transform);
                Prepare(go,country);
                Samples(go,name,country);
                if(name=="reward")
                {
                    var coin=go.transform.Find("Content/RewardRow/CoinReward");coin.gameObject.SetActive(true);
                    var cash=go.transform.Find("Content/RewardRow/CashReward");cash.gameObject.SetActive(true);
                }
                Canvas.ForceUpdateCanvases();
                foreach(var label in go.GetComponentsInChildren<TMP_Text>(false))label.ForceMeshUpdate(true,true);
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                capture=new Texture2D(width,height,TextureFormat.RGB24,false);capture.ReadPixels(new Rect(0,0,width,height),0,0);capture.Apply();
                string file=name+"-"+CountryCurrencyIcons.CountryCode(country)+".png";File.WriteAllBytes(Path.Combine(Folder,file),capture.EncodeToPNG());report.AppendLine("Rendered "+file);
            }
            finally{RenderTexture.active=previous;if(capture!=null)UnityEngine.Object.DestroyImmediate(capture);target.Release();UnityEngine.Object.DestroyImmediate(target);EditorSceneManager.ClosePreviewScene(scene);}
        }
        static void Samples(GameObject go,string name,AccountModule.E_CountryType country)
        {
            // Sample strings belong only to the disposable preview; no business methods run.
            string amount=country==AccountModule.E_CountryType.US?"$12.50":country==AccountModule.E_CountryType.BR?"R$12,50":"Rp12.500";
            var t=go.transform.Find("AspectContent")??go.transform;
            void Text(string path,string value){var label=t.Find(path)?.GetComponent<TMP_Text>();if(label!=null)label.text=value;}
            if(name=="reward")
            {
                var page=go.GetComponent<GetRewardPanel>();page.itemATxt.text="1000";page.itemBTxt.text=amount;page.levelTxt.text="Level 8";
                Text("Content/HeaderCaption","Your Reward");Text("Content/RewardRow/CoinReward/CoinUnit","Coins");
                foreach(var label in page.claimBtn.GetComponentsInChildren<TMP_Text>(true))if(label.name.Contains("Label")||label.name.Contains("Caption"))label.text="Claim";
                foreach(var label in page.closeBtn.GetComponentsInChildren<TMP_Text>(true))label.text="Collect";
            }
            if(name=="tier")
            {
                var item=go.GetComponent<WithdrawLevelItem>();item.levelTxt.text="Level 1";item.rateText.text="1.0X";item.balanceText.text=amount;
            }
            if(name=="slots"||name=="slot-reward")
            {
                Text("SequentialTitle","Coral Rewards");Text("SequentialCoins","1000");Text("SequentialCash",amount);Text("SequentialSpinCount","3");Text("SequentialHint","Watch videos to earn spins");
                var panel=go.GetComponent<SlotPanel>().slotRewardPanel;
                panel.gameObject.SetActive(name=="slot-reward");
                if(name=="slot-reward")
                {
                    panel.coinObj.SetActive(true);panel.dollarObj.SetActive(true);panel.coinText.text="1000";panel.dollarText.text=amount;
                    var title=panel.transform.Find("Title")?.GetComponent<TMP_Text>();if(title!=null)title.text="Your reward!";
                }
            }
            if(name=="slot-guide")
            {
                Text("Coins","1000");Text("Cash",amount);Text("GuideTitle","Reward guide");
                for(int i=0;i<6;i++)Text("Rule"+i,i<3?"Cash reward":"Bonus reward");
                Text("SpinRuleText","1 spin every 5 levels");Text("VideoRuleText","Watch a video for an extra spin");
            }
            if(name=="NewbieGiftPage"){Text("WelcomeTitle","Welcome gift!");Text("GiftAmount",amount);Text("Intro","A little gift to start\nyour journey.");Text("Claim/ClaimLabel","Claim");}
        }
    }
}
#endif
