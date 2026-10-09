using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    // Offline prefab authoring. The player uses the serialized Image settings.
    [InitializeOnLoad]
    public static class UiBorderCalibration
    {
        static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../ArtChanges/BorderCalibration-20260927"));
        const string Atlas = "Assets/BubblePics/Resources/CoralV3/CoralUIAtlas.png";
        const string ThinAtlas = "Assets/BubblePics/Resources/CoralV3/CoralThinFrames.png";
        [Serializable] public class Entry { public string name; }
        [Serializable] public class Settings { public Entry[] sprites; public float sharedMultiplier = 1; }
        [Serializable] class Command { public string operation; }
        static double next;
        static UiBorderCalibration() { EditorApplication.update += Poll; }
        static Settings ReadSettings() => JsonUtility.FromJson<Settings>(File.ReadAllText(Path.Combine(Folder,"settings.json")));
        public static string FrameResource(string resource,string name)
        {
            if(resource!="CoralV3/CoralUIAtlas" && resource!="CoralV3/CoralThinFrames") return resource;
            foreach(var entry in ReadSettings().sprites)if(entry.name==name)return "CoralV3/CoralThinFrames";
            return resource;
        }
        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + .5;
            string path=Path.Combine(Folder,"command.json");
            if(!File.Exists(path)) return;
            var command=JsonUtility.FromJson<Command>(File.ReadAllText(path));File.Delete(path);
            try
            {
                if(command.operation=="apply") Apply();
                else if(command.operation=="preview") UiBorderCalibrationPreview.Render();
                else if(command.operation=="validate") ValidateToolStates();
                else throw new InvalidOperationException(command.operation);
                File.WriteAllText(Path.Combine(Folder,"result.txt"),"PASS "+command.operation+" "+DateTime.UtcNow.ToString("O"));
            }
            catch(Exception e) { File.WriteAllText(Path.Combine(Folder,"result.txt"),e.ToString());Debug.LogException(e); }
        }
        static void Backup(string path)
        {
            string dest=Path.Combine(Folder,"Backup",path);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            if(!File.Exists(dest)) File.Copy(path,dest);
        }
        public static bool ConfigureImage(Image image,string resource,string name)
        {
            if((resource!="CoralV3/CoralUIAtlas" && resource!="CoralV3/CoralThinFrames") || image==null) return false;
            foreach(var entry in ReadSettings().sprites)
                if(entry.name==name)
                {
                    image.type=Image.Type.Simple;
                    image.pixelsPerUnitMultiplier=1;
                    image.preserveAspect=false;
                    image.fillCenter=true;
                    return true;
                }
            return false;
        }
        static void ValidateToolStates()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Validation requires Edit Mode.");
            var appearance=Resources.Load<ToolAppearance>("CoralV3/ToolAppearance");
            if(appearance==null || appearance.Background(false)==null || appearance.Background(true)==null)throw new InvalidOperationException("Missing tool background resource.");
            var root=PrefabUtility.LoadPrefabContents("Assets/BubblePics/Resources/Prefabs/FrameworkBubbleProp.prefab");
            var report=new StringBuilder();
            try
            {
                var tool=root.GetComponent<ToolButton>();var data=new SerializedObject(tool);
                var background=(Image)data.FindProperty("_bg").objectReferenceValue;
                var originalRect=background.rectTransform.rect;var button=background.GetComponent<Button>();
                if(button==null || button.targetGraphic!=background)throw new InvalidOperationException("Tool visual is not the standard Button graphic.");
                foreach(var definition in ToolDef.All)
                {
                    tool.Def=definition;
                    if(appearance.Icon(definition.Id)==null)throw new InvalidOperationException("Missing tool icon: "+definition.Id);
                    for(int i=0;i<4;i++)
                    {
                        tool.Refresh(i==0?definition.UnlockLevel-1:definition.UnlockLevel,i==2?2:0,i==1,true,true);
                        ToolState expected=i==0?ToolState.Locked:i==1?ToolState.Free:i==2?ToolState.Count:ToolState.Ad;
                        if(tool.State!=expected || background.sprite==null || background.type!=Image.Type.Simple || background.rectTransform.rect!=originalRect)throw new InvalidOperationException("Tool visual/state mismatch: "+definition.Id+" state "+i);
                        if(background.sprite!=appearance.Background(i==0))throw new InvalidOperationException("Incorrect frame for tool state.");
                        report.AppendLine("PASS "+definition.Id+" state="+i+" sprite="+background.sprite.name+" button graphic and rect preserved");
                    }
                }
                report.AppendLine("PASS 12 tool states. Isolated prefab checks; not gameplay or SDK end-to-end tests. "+DateTime.UtcNow.ToString("O"));
                File.WriteAllText(Path.Combine(Folder,"tool-state-checks.txt"),report.ToString());
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before authoring.");
            var settings=ReadSettings();
            Backup(Atlas+".meta");
            var importer=(TextureImporter)AssetImporter.GetAtPath(Atlas);
            var factories=new SpriteDataProviderFactories();factories.Init();
            var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var rects=provider.GetSpriteRects();
            foreach(var rect in rects)rect.border=Vector4.zero;
            // Preserve sprite IDs, rectangles, texture pixels, and world-sprite PPU.
            provider.SetSpriteRects(rects);provider.Apply();importer.SaveAndReimport();
            AssetDatabase.ImportAsset(ThinAtlas,ImportAssetOptions.ForceSynchronousImport);
            var thinImporter=(TextureImporter)AssetImporter.GetAtPath(ThinAtlas);
            thinImporter.textureType=TextureImporterType.Sprite;thinImporter.spriteImportMode=SpriteImportMode.Multiple;
            thinImporter.spritePixelsPerUnit=100;thinImporter.alphaIsTransparency=true;thinImporter.mipmapEnabled=false;thinImporter.isReadable=false;
            thinImporter.maxTextureSize=2048;thinImporter.textureCompression=TextureImporterCompression.Uncompressed;thinImporter.filterMode=FilterMode.Bilinear;thinImporter.wrapMode=TextureWrapMode.Clamp;
            foreach(string platform in new[]{"Android","iPhone"}){var ps=thinImporter.GetPlatformTextureSettings(platform);ps.overridden=true;ps.maxTextureSize=2048;ps.format=TextureImporterFormat.ASTC_4x4;thinImporter.SetPlatformTextureSettings(ps);}
            thinImporter.SaveAndReimport();
            var thinProvider=factories.GetSpriteEditorDataProviderFromObject(thinImporter);thinProvider.InitSpriteEditorDataProvider();
            var oldThinRects=thinProvider.GetSpriteRects();var thinRects=new List<SpriteRect>();var pairs=new List<SpriteNameFileIdPair>();
            foreach(var original in rects)
                foreach(var entry in settings.sprites)
                    if(entry.name==original.name)
                    {
                        GUID id=GUID.Generate();foreach(var old in oldThinRects)if(old.name==entry.name){id=old.spriteID;break;}
                        var rect=original.rect;
                        if(entry.name=="Tray")rect=new Rect(610,1254-400-150,382,150);
                        thinRects.Add(new SpriteRect{name=entry.name,rect=rect,alignment=SpriteAlignment.Center,pivot=new Vector2(.5f,.5f),spriteID=id});
                        pairs.Add(new SpriteNameFileIdPair(entry.name,id));
                    }
            thinProvider.SetSpriteRects(thinRects.ToArray());thinProvider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(pairs);thinProvider.Apply();thinImporter.SaveAndReimport();
            const string appearancePath="Assets/BubblePics/Resources/CoralV3/ToolAppearance.asset";
            Backup(appearancePath);var appearance=AssetDatabase.LoadAssetAtPath<ToolAppearance>(appearancePath);
            appearance.BackgroundAtlasResource="CoralV3/CoralThinFrames";EditorUtility.SetDirty(appearance);
            var spec=JsonUtility.FromJson<AllUiAuthoring.SkinSpec>(File.ReadAllText(Path.Combine(AllUiAuthoring.Folder,"layout.json")));
            var paths=new HashSet<string>();foreach(var prefab in spec.prefabs)paths.Add(prefab.asset);
            paths.Add("Assets/BubblePics/Resources/Prefabs/FrameworkBubbleProp.prefab");
            var report=new StringBuilder();int total=0,prefabsChanged=0;
            foreach(string path in paths)
            {
                var root=PrefabUtility.LoadPrefabContents(path);int count=0;
                try
                {
                    foreach(var binder in root.GetComponentsInChildren<CoralResourceSprite>(true))
                    {
                        var data=new SerializedObject(binder);
                        string resource=data.FindProperty("_resourcePath").stringValue;
                        string name=data.FindProperty("_spriteName").stringValue;
                        var image=data.FindProperty("_image").objectReferenceValue as Image;
                        if(image==null)continue;
                        float before=image.pixelsPerUnitMultiplier;int beforeType=(int)image.type;bool beforeFill=image.fillCenter;
                        bool edited=ConfigureImage(image,resource,name);
                        string frameResource=FrameResource(resource,name);bool resourceChanged=resource!=frameResource;
                        if(resourceChanged){data.FindProperty("_resourcePath").stringValue=frameResource;data.ApplyModifiedPropertiesWithoutUndo();}
                        if(resource=="AllUI20260924/SharedControls" && image.type==Image.Type.Sliced &&
                            (name=="Panel" || name=="Dialog" || name=="Orange" || name=="Green" || name=="Blue" || name=="Disabled"))
                        { image.pixelsPerUnitMultiplier=settings.sharedMultiplier;edited=true; }
                        if(edited && (resourceChanged || before!=image.pixelsPerUnitMultiplier || beforeType!=(int)image.type || beforeFill!=image.fillCenter))
                        { count++;report.AppendLine(path+" | "+AnimationUtility.CalculateTransformPath(image.transform,root.transform)+" | "+name+" | type "+beforeType+" -> "+(int)image.type+" | PPU multiplier "+before+" -> "+image.pixelsPerUnitMultiplier); }
                    }
                    // ToolButton selects its normal/locked sprite at runtime. Both use authored borders.
                    if(path.EndsWith("/FrameworkBubbleProp.prefab",StringComparison.Ordinal))
                    {
                        var toolData=new SerializedObject(root.GetComponent<ToolButton>());
                        var image=(Image)toolData.FindProperty("_bg").objectReferenceValue;
                        float before=image.pixelsPerUnitMultiplier;int beforeType=(int)image.type;
                        ConfigureImage(image,"CoralV3/CoralUIAtlas","Tool");
                        if(before!=image.pixelsPerUnitMultiplier || beforeType!=(int)image.type){count++;report.AppendLine(path+" | Bg | Tool/Locked | type "+beforeType+" -> "+(int)image.type+" | PPU multiplier "+before+" -> "+image.pixelsPerUnitMultiplier);}
                    }
                    if(count>0){Backup(path);PrefabUtility.SaveAsPrefabAsset(root,path);prefabsChanged++;total+=count;}
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            report.AppendLine("Images="+total+" Prefabs="+prefabsChanged+" "+DateTime.UtcNow.ToString("O"));
            File.WriteAllText(Path.Combine(Folder,"applied.txt"),report.ToString());
        }
    }
}
