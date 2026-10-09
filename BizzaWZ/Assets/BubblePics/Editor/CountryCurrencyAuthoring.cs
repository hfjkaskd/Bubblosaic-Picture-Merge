#if BIZZA_REAL_WITHDRAW
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;

namespace BubblePics.EditorTools
{
    // Offline migration only. All new layers are saved in prefabs; runtime only selects art.
    public static class CountryCurrencyAuthoring
    {
        public const string Folder = "../Validation/CountryCurrency-20261008";
        const string Real = "Assets/BizzaWZ/Final/Real/UI/";
        const string Common = "Assets/BizzaWZ/Final/MenuSystem/Common/";
        const string Controls = "AllUI20260924/SharedControls";
        const string Bubble = "BubblePicsDynamic/b1fc4bdc65e36d443a2cc1ed98504763/bubble_frame";
        public static readonly string[] Paths = {
            Real+"GamePanel/Resources/Resources/GameUiWidget.prefab",
            Common+"CurrencyBar/CurrencyBar.prefab",
            Real+"RealWithdrawalPanl/RealWithdrawPanel.prefab",
            Real+"RealWithdrawalPanl/WithdrawLevelItem.prefab",
            Real+"WithdrawOceanSkin/Prefabs/WithdrawLevelItem.prefab",
            Real+"WithdrawReferenceSkin/Prefabs/WithdrawLevelItem.prefab",
            Common+"UITips/Resources/BroadCastBar.prefab",
            Real+"GetRewardPanel/GetRewardPanel.prefab",
            Real+"SlotsPanel/SlotPanel/SlotPanel.prefab",
            Real+"SlotsPanel/SlotFQAPanel/SlotFQAPanel.prefab",
            Real+"DailyWithdrawPanel/DailyWithdrawPanel.prefab",
            Real+"ExchangeRatePanel/ExchangeRatePanel.prefab",
            Real+"DailyMissionPanel/DailyMissionPanel.prefab",
            Real+"NewbieGiftPage/NewbieGiftPage.prefab",
            Real+"RealWithdrawalPanl/BonusRate.prefab",
            Real+"GetRewardPanel/Real_WithdrawProgress/Real_WithdrawProgress.prefab"
        };

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before authoring.");
            const string groupPath="Assets/AddressableAssetsData/AssetGroups/RWzIcon.asset";
            string groupBackup=Path.Combine(Folder,"before",groupPath);
            Directory.CreateDirectory(Path.GetDirectoryName(groupBackup));
            if(!File.Exists(groupBackup))File.Copy(groupPath,groupBackup);
            var settings=AddressableAssetSettingsDefaultObject.Settings;
            var group=settings.FindGroup("RWzIcon");
            foreach(string country in new[]{"US","BR","ID"})
            {
                string address="Bubble_"+country;
                string guid=AssetDatabase.AssetPathToGUID("Assets/BizzaWZ/Final/Real/GameAssets/WzTexture_Money/"+address+".png");
                if(string.IsNullOrEmpty(guid))throw new InvalidOperationException("Missing original icon: "+address);
                settings.CreateOrMoveEntry(guid,group).address=address;
            }
            foreach (string path in Paths)
            {
                // Legacy shared bar contains an unrelated retired component. Its existing
                // WzIconAmend binds its own Image at runtime; no prefab edit is necessary.
                if(path==Common+"CurrencyBar/CurrencyBar.prefab") continue;
                string backup = Path.Combine(Folder, "before", path);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(path, backup);
                var go = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Transform t = go.transform;
                    if (path.EndsWith("WithdrawLevelItem.prefab")) Currency(t.Find("CashIllustration"), E_WzIconType.StackMoney);
                    else if (path.EndsWith("BroadCastBar.prefab"))
                    {
                        Currency(t.Find("Items/Coins/Coin"), E_WzIconType.GoldCoin);
                        Currency(t.Find("Items/Cash/Cash"), E_WzIconType.StackMoney);
                    }
                    else if (path.EndsWith("GetRewardPanel.prefab")) Reward(t);
                    else if (path.EndsWith("SlotPanel.prefab")) Slots(t);
                    else if (path.EndsWith("SlotFQAPanel.prefab")) Guide(t);
                    else if (path.EndsWith("DailyWithdrawPanel.prefab")) Daily(t);
                    else if (path.EndsWith("ExchangeRatePanel.prefab")) Rate(t);
                    else if (path.EndsWith("DailyMissionPanel.prefab")) Mission(t);
                    else if (path.EndsWith("NewbieGiftPage.prefab")) Newbie(t);
                    foreach (var icon in go.GetComponentsInChildren<WzIconAmend>(true)) Configure(icon, icon.iconType);
                    PrefabUtility.SaveAsPrefabAsset(go, path, out bool saved);
                    if(!saved)throw new InvalidOperationException("Could not save currency prefab: "+path);
                }
                finally { PrefabUtility.UnloadPrefabContents(go); }
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(Folder,"authoring.txt"), "Updated " + (Paths.Length-1) + " prefabs with original country currency layers; legacy shared bar uses the common runtime resolver without prefab changes.");
        }

        static Transform Content(Transform root) => root.Find("AspectContent") ?? root;
        static Transform Node(Transform parent, string name) => parent.Find(name) ?? Child(parent, name);
        static void Configure(WzIconAmend icon, E_WzIconType type)
        {
            icon.iconType = type;
            icon.image = Ensure<Image>(icon);
            icon.isNativeSize = false;
            foreach (var binder in icon.GetComponents<CoralResourceSprite>()) UnityEngine.Object.DestroyImmediate(binder);
            var so = new SerializedObject(icon);
            so.FindProperty("iconType").intValue=(int)type;
            so.FindProperty("image").objectReferenceValue=icon.image;
            so.FindProperty("isNativeSize").boolValue=false;
            foreach (string field in new[]{"skinAtlasResource","skinSpriteName","singleCurrencySkinSpriteName"}) so.FindProperty(field).stringValue = "";
            foreach (string field in new[]{"skinMaterial","singleCurrencySkinMaterial"}) so.FindProperty(field).objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            icon.image.sprite = null; icon.image.material = null; icon.image.type = Image.Type.Simple;
            icon.image.preserveAspect = true; icon.image.color = Color.white;
            EditorUtility.SetDirty(icon);
            EditorUtility.SetDirty(icon.image);
        }
        static void Currency(Transform t, E_WzIconType type)
        {
            if (t == null) throw new InvalidOperationException("Missing currency layer.");
            Configure(Ensure<WzIconAmend>(t), type);
        }
        static Image Art(Transform t, string path, string sprite = "", bool sliced = false)
        {
            var im = Ensure<Image>(t); im.sprite = null; im.material = null; im.color = Color.white;
            im.raycastTarget = false; im.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            im.preserveAspect = !sliced; im.pixelsPerUnitMultiplier = sliced ? .7f : 1;
            var so = new SerializedObject(Ensure<CoralResourceSprite>(t));
            so.FindProperty("_resourcePath").stringValue = path; so.FindProperty("_spriteName").stringValue = sprite;
            so.FindProperty("_image").objectReferenceValue = im; so.ApplyModifiedPropertiesWithoutUndo();
            return im;
        }
        static RectTransform Icon(Transform parent, string name, E_WzIconType type, Vector2 pos, Vector2 size)
        {
            var t = (RectTransform)Node(parent,name); t.anchorMin=t.anchorMax=t.pivot=new Vector2(.5f,.5f);
            t.anchoredPosition=pos; t.sizeDelta=size; t.localScale=Vector3.one;
            Currency(t,type); t.GetComponent<Image>().raycastTarget=false; return t;
        }
        static ReferencePrefabTools Layout(float w=849,float h=1852) => new ReferencePrefabTools("CountryCurrency", "", w,h);
        static Transform Plate(Transform t, ReferencePrefabTools a, string name, Rect rect, string sprite="Dialog")
        {
            var p=Node(t,name); a.Place(p,rect); Art(p,Controls,sprite,true); return p;
        }
        static void PlacedIcon(Transform t, ReferencePrefabTools a, string name, E_WzIconType type, Rect box)
        {
            var p=Icon(t,name,type,Vector2.zero,Vector2.one); a.Place(p,box);
        }
        static void Reward(Transform root)
        {
            foreach (var pair in new[]{("CoinReward","Coins",E_WzIconType.PileGold),("CashReward","Cash",E_WzIconType.HundredMoney)})
            {
                var group=root.Find("Content/RewardRow/"+pair.Item1);
                // A pale native circle keeps localized amounts legible over the dimmed game.
                // The separate glossy rim retains the game's bubble artwork.
                var fill=(RectTransform)Node(group,"CurrencyBubbleFill");
                fill.anchorMin=fill.anchorMax=fill.pivot=new Vector2(.5f,.5f);fill.anchoredPosition=Vector2.zero;fill.sizeDelta=new Vector2(410,410);fill.localScale=Vector3.one;
                var previousImage=fill.GetComponent<Image>();if(previousImage!=null)UnityEngine.Object.DestroyImmediate(previousImage);
                // Reuse the existing 256px alpha disc (small shared texture), not the
                // low-resolution built-in knob with its grey bevel.
                var circle=Ensure<RawImage>(fill);circle.texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/BizzaWZ/Final/BizzaGame/ArtPlugins/Effect/Textures/circle.png");
                if(circle.texture==null)throw new InvalidOperationException("Shared circle texture missing.");
                circle.color=new Color(.79f,.95f,1,1);circle.material=null;circle.raycastTarget=false;fill.SetAsFirstSibling();
                Art(group.Find(pair.Item2),Bubble);
                var icon=Icon(group,"CountryCurrencyIcon",pair.Item3,new Vector2(0,66),new Vector2(280,205));
                icon.SetSiblingIndex(group.Find(pair.Item2).GetSiblingIndex()+1);
            }
        }
        static void Slots(Transform root)
        {
            var t=Content(root); var a=Layout();
            // Replace the whole header plate rather than leave USD pixels below the new icons.
            var plate=Plate(t,a,"CountryCurrencyPlate",new Rect(13,226,823,195));
            var bg=t.Find("SequentialBackground");
            plate.SetSiblingIndex(bg!=null?bg.GetSiblingIndex()+1:0);
            Currency(t.Find("SequentialCoinIcon"),E_WzIconType.GoldCoin);
            Currency(t.Find("SequentialCashIcon"),E_WzIconType.StackMoney);
            foreach(var pair in new[]{("CoinCard",E_WzIconType.PileGold),("CashCard",E_WzIconType.PileMoney)})
            {
                var card=t.Find("GetRewadPanel/RewardCards/"+pair.Item1);
                Art(card,Controls,"Selected",true);
                var size=((RectTransform)card).sizeDelta;
                Icon(card,"CountryCurrencyIcon",pair.Item2,new Vector2(0,size.y*.17f),new Vector2(size.x*.67f,size.y*.50f)).SetAsFirstSibling();
            }
        }
        static void Guide(Transform root)
        {
            var t=Content(root);var a=Layout();
            var plate=Plate(t,a,"CountryCurrencyPlate",new Rect(13,226,823,195));
            var bg=t.Find("Background");plate.SetSiblingIndex(bg!=null?bg.GetSiblingIndex()+1:0);
            PlacedIcon(t,a,"CountryCoin",E_WzIconType.GoldCoin,new Rect(79,260,145,130));
            PlacedIcon(t,a,"CountryCash",E_WzIconType.StackMoney,new Rect(465,261,125,128));
            var basePlate=Plate(t,a,"CountryRulesPlate",new Rect(34,528,780,786));basePlate.SetSiblingIndex(plate.GetSiblingIndex()+1);
            int[] rows={539,667,792,918,1051,1182};
            string[] symbols={"Shell","Star","Pearl","Drink","Diamond","Shell"};
            for(int i=0;i<6;i++)
            {
                var row=t.Find("Row"+i);Art(row,Controls,"Tile",true);
                for(int j=0;j<3;j++)
                {
                    string symbol=i==5?new[]{"Shell","Star","Pearl"}[j]:symbols[i];
                    var symbolLayer=Node(row,"Match"+j);
                    var rect=(RectTransform)symbolLayer;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
                    rect.sizeDelta=new Vector2(79*a.SX,82*a.SY);rect.anchoredPosition=new Vector2((-302+88*j)*a.SX,0);
                    string resource="ReleaseUI/SequentialUI20260928/"+(symbol=="Drink"||symbol=="Diamond"?"SlotSymbols":"SlotPanel");
                    Art(symbolLayer,resource,symbol+"__e3b0c44298").useSpriteMesh=true;
                }
                // Keep the existing localized reward explanation and native symbols.
                var eq=Node(row,"Equals");var text=Ensure<TextMeshProUGUI>(eq);text.font=a.Font;text.fontSize=45*a.SY;text.text="=";text.alignment=TextAlignmentOptions.Center;text.color=new Color(.02f,.03f,.3f);text.raycastTarget=false;
                var er=(RectTransform)eq;er.anchorMin=er.anchorMax=er.pivot=new Vector2(.5f,.5f);er.sizeDelta=new Vector2(50*a.SX,70*a.SY);er.anchoredPosition=new Vector2(-25*a.SX,0);
                if(i<3) Icon(row,"Cash",new[]{E_WzIconType.StackMoney,E_WzIconType.PileMoney,E_WzIconType.HundredMoney}[i],new Vector2(85*a.SX,0),new Vector2(124*a.SX,86*a.SY));
                else
                {
                    Icon(row,"Coin",E_WzIconType.PileGold,new Vector2(39*a.SX,0),new Vector2(68*a.SX,83*a.SY));
                    Icon(row,"Cash",E_WzIconType.PileMoney,new Vector2(118*a.SX,0),new Vector2(70*a.SX,83*a.SY));
                }
            }
        }
        static void Daily(Transform root)
        {
            var t=Content(root);var a=Layout(852,1846);Art(t.Find("Panel"),Controls,"Dialog",true);
            PlacedIcon(t,a,"CountryCoin",E_WzIconType.GoldCoin,new Rect(333,660,185,177));
        }
        static void Rate(Transform root)
        {
            var t=Content(root);var a=Layout(852,1846);
            Art(t.Find("Before"),Controls,"Tile",true);Art(t.Find("Now"),Controls,"Selected",true);
            PlacedIcon(t,a,"BeforeCountryCoin",E_WzIconType.GoldCoin,new Rect(183,804,112,117));
            PlacedIcon(t,a,"NowCountryCoin",E_WzIconType.GoldCoin,new Rect(183,1177,112,117));
        }
        static void Mission(Transform root)
        {
            var t=Content(root);var a=Layout();Art(t.Find("Panel"),Controls,"Dialog",true);
            PlacedIcon(t,a,"CountryRewardArt",E_WzIconType.AbundanceWealth,new Rect(268,592,313,272));
        }
        static void Newbie(Transform root)
        {
            var t=Content(root);var a=Layout();var bg=t.Find("Backdrop")??root.Find("Backdrop");Art(bg,"CoralV3/CoralBackgroundReference");
            Art(t.Find("Title"),"RewardArt20260930/RewardTitle","RewardTitle");
            var caption=t.Find("ReferenceTitle");if(caption!=null)caption.gameObject.SetActive(false);
            var title=t.Find("WelcomeTitle").GetComponent<TMP_Text>();title.alpha=1;
            var titleGroup=title.GetComponent<CanvasGroup>();if(titleGroup!=null)titleGroup.alpha=1;
            var plate=Plate(t,a,"CountryGiftPanel",new Rect(23,437,803,1176));plate.SetSiblingIndex(bg.parent==t?bg.GetSiblingIndex()+1:0);
            var dragon=Node(t,"GiftDragon");a.Place(dragon,new Rect(224,493,400,412));Art(dragon,"RewardForeground20260930/Dragon");
            PlacedIcon(t,a,"CountryRewardArt",E_WzIconType.AbundanceWealth,new Rect(250,810,350,255));
        }
    }
}
#endif
