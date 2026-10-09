using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class ReleaseArtPacking
    {
        [Serializable] public class Source { public string path, resource, guid; }
        [Serializable] public class Plan { public Source[] sources; public string[] candidates; }
        static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Validation/ApkOptimize-20260929/stage3"));
        static Plan ReadPlan() => JsonUtility.FromJson<Plan>(File.ReadAllText(Path.Combine(Folder,"plan.json")));
        public static void Audit()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var plan=ReadPlan();var sources=new HashSet<string>();foreach(var s in plan.sources)sources.Add(s.resource);
            var log=new StringBuilder();int visuals=0;
            foreach(string path in plan.candidates)
            {
                if(!path.EndsWith(".prefab"))
                {
                    var asset=AssetDatabase.LoadMainAssetAtPath(path);InspectStrings(asset,path,sources,log);continue;
                }
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach(var bind in root.GetComponentsInChildren<CoralResourceSprite>(true))
                    {
                        var so=new SerializedObject(bind);string resource=so.FindProperty("_resourcePath").stringValue;
                        if(!sources.Contains(resource))continue;
                        var image=so.FindProperty("_image").objectReferenceValue as Image;
                        string name=so.FindProperty("_spriteName").stringValue;
                        log.AppendLine("VISUAL | "+path+" | "+Hierarchy(bind.transform)+" | "+resource+" | "+name+" | "+(image==null?"NO_IMAGE":AssetDatabase.GetAssetPath(image.material)));visuals++;
                    }
                    foreach(var c in root.GetComponentsInChildren<MonoBehaviour>(true))if(c!=null&&!(c is CoralResourceSprite))InspectStrings(c,path,sources,log);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            log.AppendLine("VISUALS="+visuals);File.WriteAllText(Path.Combine(Folder,"usage-audit.txt"),log.ToString());
        }
        static void InspectStrings(UnityEngine.Object asset,string path,HashSet<string> sources,StringBuilder log)
        {
            if(asset==null)return;var so=new SerializedObject(asset);var p=so.GetIterator();
            while(p.Next(true))if(p.propertyType==SerializedPropertyType.String&&sources.Contains(p.stringValue))
                log.AppendLine("CONFIG | "+path+" | "+asset.name+" | "+asset.GetType().Name+" | "+p.propertyPath+" | "+p.stringValue);
        }
        static string Hierarchy(Transform t){var result=t.name;while(t.parent!=null){t=t.parent;result=t.name+"/"+result;}return result;}
    }
}
