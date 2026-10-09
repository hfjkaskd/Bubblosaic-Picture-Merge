using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;
namespace BubblePics.EditorTools
{
 public static class SequentialBroadcastAuthoring
 {
  public static void Apply()
  {
   var a=new ReferencePrefabTools("BroadCastBar",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/b3da013f0df5-23-BroadCastBar-simple.png")),849,1852);a.Slice("Panel",14,392,822,176).Slice("Coin",105,427,136,108).Slice("Cash",420,430,128,107).Slice("CoinAmount",239,446,154,73).Slice("CashAmount",548,448,203,74);a.Import();a.Round(a.Mat("Panel"),a.R("Panel"),53);a.Erase(a.Mat("Panel"),new Rect(103,420,658,123));a.Mat("Panel").SetFloat("_SampleX",779);a.Round(a.Mat("Panel"),a.R("Panel"),53);a.Mat("Panel").SetFloat("_EraseFeather",7);a.Mat("Coin").SetFloat("_WhiteMatte",1);a.Mat("Cash").SetFloat("_WhiteMatte",1);
   const string path="Assets/BizzaWZ/Final/MenuSystem/Common/UITips/Resources/BroadCastBar.prefab";var go=PrefabUtility.LoadPrefabContents(path);
   try{var p=go.GetComponent<BroadcastBarController>();var t=go.transform;Clear(t);Stretch(t);p.barTransform=(RectTransform)t;Ensure<CanvasGroup>(t);a.Graphic(t,"Panel");
    var items=Child(t,"Items");Stretch(items);p.entryObj=items.gameObject;var coins=Child(items,"Coins");Stretch(coins);p.entryAObj=coins.gameObject;var cash=Child(items,"Cash");Stretch(cash);p.entryBObj=cash.gameObject;p.entryAImage=a.Graphic(coins,"Coin").GetComponent<Image>();p.entryBImage=a.Graphic(cash,"Cash").GetComponent<Image>();
    p.entryAText=a.Text(coins,"CoinValue",new Rect(237,443,162,80),68,color:new Color(.92f,.44f,0));a.Caption(coins,p.entryAText,"CoinAmount","+250");p.entryBText=a.Text(cash,"CashValue",new Rect(546,444,217,80),64,color:new Color(0,.53f,.02f));a.Caption(cash,p.entryBText,"CashAmount","+$0.05");
    var text=a.Text(t,"Message",new Rect(66,420,716,120),45);text.enableWordWrapping=true;p.textObj=text.gameObject;p.textComponent=(TextMeshProUGUI)text;Validate(t);PrefabUtility.SaveAsPrefabAsset(go,path);
   }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
  }
 }
}
