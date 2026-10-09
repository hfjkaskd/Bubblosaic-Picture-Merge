using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class ReleaseArtPacking
    {
        const string OutputRoot="Assets/BubblePics/Resources/ReleaseUI/";
        const string BackupRoot="F:/CodexArtifacts/pingguoshu-optimize-20260929/stage3-before/";
        sealed class Sheet
        {
            public Source source;public Texture2D texture;public string output,resource;
            public readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
            public readonly Dictionary<string,SpriteRect> rects=new Dictionary<string,SpriteRect>();
            public ISpriteOutlineDataProvider outlines;public readonly List<Tile> tiles=new List<Tile>();
        }
        sealed class Tile
        {
            public Sheet sheet;public string key,name,hash;public int width,height;public Color32[] pixels;
            public Sprite original,result;public SpriteRect originalRect;public List<Vector2[]> outline;
            public RectInt packed;public string assetPath;
        }
        sealed class Bin { public readonly List<RectInt> free=new List<RectInt>{new RectInt(0,0,2048,2048)};public readonly List<Tile> tiles=new List<Tile>();public int width,height; }
        static Dictionary<string,Sheet> sheets;static Dictionary<string,Tile> tiles;
        static Dictionary<string,Shader> bakeShaders;static bool collecting;static StringBuilder bakeLog;

        public static void PrepareBakeShaders()
        {
            string folder="Assets/BubblePics/Editor/ReleaseBakeShaders";Directory.CreateDirectory(folder);
            foreach(string path in Directory.GetFiles("Assets/BubblePics/Shaders","*.shader"))
            {
                string text=File.ReadAllText(path);
                text=Regex.Replace(text,"Shader\\s+\"[^\"]+\"","Shader \"Hidden/BubblePics/ReleaseBake/"+Path.GetFileNameWithoutExtension(path)+"\"",RegexOptions.None);
                text=Regex.Replace(text,@"Blend\s+SrcAlpha\s+OneMinusSrcAlpha","Blend One Zero");
                text=text.Replace("ZTest [unity_GUIZTestMode]","ZTest Always");
                File.WriteAllText(folder+"/"+Path.GetFileName(path),text);
            }
            File.WriteAllText(folder+"/Copy.shader",@"Shader ""Hidden/BubblePics/ReleaseBake/Copy"" {
Properties { _MainTex(""Source"",2D)=""white""{} _Color(""Tint"",Color)=(1,1,1,1) }
SubShader { Cull Off ZWrite Off ZTest Always Blend One Zero Pass {
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include ""UnityCG.cginc""
struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};struct v2f{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
sampler2D _MainTex;float4 _Color;v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
fixed4 frag(v2f i):SV_Target{return tex2D(_MainTex,i.uv)*_Color;}
ENDCG
} } }");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void Bake(string filter=null)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            var plan=filter=="hud-extra"?JsonUtility.FromJson<Plan>(File.ReadAllText(Path.Combine(Folder,"hud-extra-plan.json"))):ReadPlan();
            if(filter=="hud-extra")filter=null;
            sheets=new Dictionary<string,Sheet>();tiles=new Dictionary<string,Tile>();bakeShaders=new Dictionary<string,Shader>();bakeLog=new StringBuilder();
            try
            {
                foreach(var source in plan.sources)
                {
                    var sheet=new Sheet{source=source,output=OutputRoot+source.resource.Replace("/ApprovedSource",""),resource="ReleaseUI/"+source.resource.Replace("/ApprovedSource","")};
                    var importer=(TextureImporter)AssetImporter.GetAtPath(source.path);
                    var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
                    foreach(var r in provider.GetSpriteRects())sheet.rects.Add(r.name,r);
                    sheet.outlines=provider.GetDataProvider<ISpriteOutlineDataProvider>();
                    foreach(var a in AssetDatabase.LoadAllAssetsAtPath(source.path))if(a is Sprite s)sheet.sprites[s.name]=s;
                    sheet.texture=new Texture2D(2,2,TextureFormat.RGBA32,false,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                    if(!sheet.texture.LoadImage(File.ReadAllBytes(source.path)))throw new InvalidOperationException("Cannot decode "+source.path);
                    sheets.Add(source.resource,sheet);
                }
                collecting=true;foreach(string path in plan.candidates)if(string.IsNullOrEmpty(filter)||path.Contains(filter))ProcessAsset(path);
                foreach(var sheet in sheets.Values)Pack(sheet);
                AssetDatabase.SaveAssets();
                collecting=false;foreach(string path in plan.candidates)if(string.IsNullOrEmpty(filter)||path.Contains(filter))ProcessAsset(path);
                AssetDatabase.SaveAssets();
                bakeLog.AppendLine("PASS Tile variants="+tiles.Count+" Sources="+sheets.Count+" "+DateTime.UtcNow.ToString("O"));
            }
            finally
            {
                if(sheets!=null)foreach(var sheet in sheets.Values)if(sheet.texture!=null)UnityEngine.Object.DestroyImmediate(sheet.texture);
                File.WriteAllText(Path.Combine(Folder,"bake-result.txt"),bakeLog.ToString());
            }
        }

        static Tile GetTile(string resource,string name,Material material)
        {
            var sheet=sheets[resource];string materialPath=material==null?"":AssetDatabase.GetAssetPath(material);
            string key=resource+"|"+name+"|"+materialPath;
            if(tiles.TryGetValue(key,out var found))return found;
            if(!collecting)throw new InvalidOperationException("Uncollected tile "+key);
            if(!sheet.sprites.TryGetValue(name,out var sprite))throw new InvalidOperationException("Missing sprite "+key);
            var r=sheet.rects[name];string suffix=Hash(Encoding.UTF8.GetBytes(materialPath)).Substring(0,10);
            var tile=new Tile{sheet=sheet,key=key,name=name+"__"+suffix,original=sprite,originalRect=r,width=Mathf.RoundToInt(r.rect.width),height=Mathf.RoundToInt(r.rect.height),outline=sheet.outlines.GetOutlines(r.spriteID)};
            tile.pixels=Render(sheet,r.rect,material,tile.width,tile.height);
            var bytes=new byte[tile.pixels.Length*4];for(int i=0;i<tile.pixels.Length;i++){var c=tile.pixels[i];bytes[i*4]=c.r;bytes[i*4+1]=c.g;bytes[i*4+2]=c.b;bytes[i*4+3]=c.a;}
            tile.hash=tile.width+"x"+tile.height+"/"+Hash(bytes);sheet.tiles.Add(tile);tiles.Add(key,tile);return tile;
        }
        static string Hash(byte[] data){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
        static Color32[] Render(Sheet sheet,Rect rect,Material original,int width,int height)
        {
            string shaderPath=original==null?"":AssetDatabase.GetAssetPath(original.shader);string name=string.IsNullOrEmpty(shaderPath)||!shaderPath.EndsWith(".shader")?"Copy":Path.GetFileNameWithoutExtension(shaderPath);
            if(!bakeShaders.TryGetValue(name,out var shader))
            {
                shader=Shader.Find("Hidden/BubblePics/ReleaseBake/"+name);
                if(shader==null)throw new InvalidOperationException("Missing bake shader "+name);
                bakeShaders.Add(name,shader);
            }
            var mat=new Material(shader);if(original!=null)mat.CopyPropertiesFromMaterial(original);mat.shaderKeywords=Array.Empty<string>();mat.SetTexture("_MainTex",sheet.texture);
            var rt=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);var prior=RenderTexture.active;
            var output=new Texture2D(width,height,TextureFormat.RGBA32,false,true);
            try
            {
                RenderTexture.active=rt;GL.Clear(true,true,Color.clear);GL.PushMatrix();GL.LoadOrtho();
                if(!mat.SetPass(0))throw new InvalidOperationException("Bake pass failed "+name);
                float x0=rect.xMin/sheet.texture.width,x1=rect.xMax/sheet.texture.width,y0=rect.yMin/sheet.texture.height,y1=rect.yMax/sheet.texture.height;
                GL.Begin(GL.QUADS);GL.Color(Color.white);
                GL.TexCoord2(x0,y0);GL.Vertex3(0,0,0);GL.TexCoord2(x1,y0);GL.Vertex3(1,0,0);GL.TexCoord2(x1,y1);GL.Vertex3(1,1,0);GL.TexCoord2(x0,y1);GL.Vertex3(0,1,0);GL.End();GL.PopMatrix();
                output.ReadPixels(new Rect(0,0,width,height),0,0);output.Apply();return output.GetPixels32();
            }
            finally {RenderTexture.active=prior;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(output);UnityEngine.Object.DestroyImmediate(mat);}
        }

        static void Pack(Sheet sheet)
        {
            if(sheet.tiles.Count==0){bakeLog.AppendLine("UNUSED "+sheet.source.path);return;}
            Directory.CreateDirectory(sheet.output);var bins=new List<Bin>();var canonical=new Dictionary<string,Tile>();var unique=new List<Tile>();
            foreach(var tile in sheet.tiles)if(!canonical.ContainsKey(tile.hash)){canonical.Add(tile.hash,tile);unique.Add(tile);}
            unique.Sort((a,b)=>Math.Max(b.width,b.height).CompareTo(Math.Max(a.width,a.height)));
            foreach(var tile in unique)
            {
                int width=tile.width+4,height=tile.height+4;if(width>2048||height>2048)throw new InvalidOperationException("Oversized tile "+tile.key);
                Bin best=null;RectInt location=default;long bestScore=long.MaxValue;
                foreach(var bin in bins)foreach(var r in bin.free)if(r.width>=width&&r.height>=height)
                {long score=(long)r.width*r.height-(long)width*height;if(score<bestScore){bestScore=score;best=bin;location=new RectInt(r.x,r.y,width,height);}}
                if(best==null){best=new Bin();bins.Add(best);location=new RectInt(0,0,width,height);}
                Occupy(best,location);tile.packed=new RectInt(location.x+2,location.y+2,tile.width,tile.height);best.tiles.Add(tile);best.width=Math.Max(best.width,location.xMax);best.height=Math.Max(best.height,location.yMax);
            }
            for(int index=0;index<bins.Count;index++)
            {
                var bin=bins[index];int w=(bin.width+3)/4*4,h=(bin.height+3)/4*4;var texture=new Texture2D(w,h,TextureFormat.RGBA32,false,true);var pixels=new Color32[w*h];
                var members=new List<Tile>();foreach(var tile in sheet.tiles)if(bin.tiles.Contains(canonical[tile.hash])){tile.packed=canonical[tile.hash].packed;members.Add(tile);}
                foreach(var tile in bin.tiles)
                {
                    var r=tile.packed;
                    for(int y=-2;y<r.height+2;y++)for(int x=-2;x<r.width+2;x++)pixels[(r.y+y)*w+r.x+x]=tile.pixels[Mathf.Clamp(y,0,r.height-1)*r.width+Mathf.Clamp(x,0,r.width-1)];
                }
                texture.SetPixels32(pixels);texture.Apply();string path=sheet.output+"/Atlas"+index+".png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var imp=(TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Multiple;imp.spritePixelsPerUnit=members[0].original.pixelsPerUnit;imp.mipmapEnabled=false;imp.npotScale=TextureImporterNPOTScale.None;imp.alphaIsTransparency=true;imp.maxTextureSize=2048;imp.textureCompression=TextureImporterCompression.CompressedHQ;
                var settings=new TextureImporterSettings();imp.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.Tight;imp.SetTextureSettings(settings);
                foreach(string platform in new[]{"Android","iPhone"}){var p=imp.GetPlatformTextureSettings(platform);p.overridden=true;p.maxTextureSize=2048;p.format=TextureImporterFormat.ASTC_4x4;p.compressionQuality=100;imp.SetPlatformTextureSettings(p);}imp.SaveAndReimport();
                var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(imp);provider.InitSpriteEditorDataProvider();var rects=new List<SpriteRect>();var pairs=new List<SpriteNameFileIdPair>();var outlines=provider.GetDataProvider<ISpriteOutlineDataProvider>();
                foreach(var tile in members)
                {
                    var guid=new GUID(Hash(Encoding.UTF8.GetBytes(tile.key)).Substring(0,32));
                    rects.Add(new SpriteRect{name=tile.name,spriteID=guid,rect=new Rect(tile.packed.x,tile.packed.y,tile.width,tile.height),alignment=tile.originalRect.alignment,pivot=tile.originalRect.pivot,border=tile.originalRect.border});pairs.Add(new SpriteNameFileIdPair(tile.name,guid));tile.assetPath=path;
                }
                provider.SetSpriteRects(rects.ToArray());provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(pairs);
                for(int i=0;i<members.Count;i++)if(members[i].outline!=null&&members[i].outline.Count>0)outlines.SetOutlines(rects[i].spriteID,members[i].outline);
                provider.Apply();imp.SaveAndReimport();var loaded=new Dictionary<string,Sprite>();foreach(var a in AssetDatabase.LoadAllAssetsAtPath(path))if(a is Sprite s)loaded.Add(s.name,s);
                foreach(var tile in members)tile.result=loaded[tile.name];bakeLog.AppendLine("ATLAS "+path+" "+w+"x"+h+" variants="+members.Count);
            }
        }
        static void Occupy(Bin bin,RectInt used)
        {
            var next=new List<RectInt>();foreach(var r in bin.free)
            {
                if(!r.Overlaps(used)){next.Add(r);continue;}
                if(used.xMin>r.xMin)next.Add(new RectInt(r.xMin,r.yMin,used.xMin-r.xMin,r.height));
                if(used.xMax<r.xMax)next.Add(new RectInt(used.xMax,r.yMin,r.xMax-used.xMax,r.height));
                if(used.yMin>r.yMin)next.Add(new RectInt(r.xMin,r.yMin,r.width,used.yMin-r.yMin));
                if(used.yMax<r.yMax)next.Add(new RectInt(r.xMin,used.yMax,r.width,r.yMax-used.yMax));
            }
            for(int i=next.Count-1;i>=0;i--)for(int j=0;j<next.Count;j++)if(i!=j&&Contains(next[j],next[i])){next.RemoveAt(i);break;}
            bin.free.Clear();bin.free.AddRange(next);
        }
        static bool Contains(RectInt a,RectInt b)=>a.xMin<=b.xMin&&a.yMin<=b.yMin&&a.xMax>=b.xMax&&a.yMax>=b.yMax;
        static void BackupFile(string path)
        {
            string target=BackupRoot+path;if(File.Exists(target))return;Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(path,target);
            if(File.Exists(path+".meta")&&!File.Exists(target+".meta"))File.Copy(path+".meta",target+".meta");
        }

        static void ProcessAsset(string path)
        {
            if(!collecting)BackupFile(path);
            if(!path.EndsWith(".prefab")){var a=AssetDatabase.LoadMainAssetAtPath(path);ProcessConfiguration(a);ProcessReferences(a);if(!collecting)EditorUtility.SetDirty(a);return;}
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var c in root.GetComponentsInChildren<MonoBehaviour>(true))if(c!=null&&!(c is CoralResourceSprite)&&!(c is Image))ProcessConfiguration(c);
                foreach(var bind in root.GetComponentsInChildren<CoralResourceSprite>(true))ProcessVisual(bind);
                if(!collecting)foreach(var stars in root.GetComponentsInChildren<StarRatingPopup>(true))foreach(var image in stars.starImages)image.material=null;
                if(!collecting)ClearBakedIconMaterials(root);
                foreach(var c in root.GetComponentsInChildren<Component>(true))if(c!=null)ProcessReferences(c);
                if(!collecting){ReferencePrefabTools.Validate(root.transform);PrefabUtility.SaveAsPrefabAsset(root,path);bakeLog.AppendLine("PREFAB "+path);}
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        static void ProcessVisual(CoralResourceSprite bind)
        {
            var so=new SerializedObject(bind);string resource=so.FindProperty("_resourcePath").stringValue;if(!sheets.ContainsKey(resource))return;
            string name=so.FindProperty("_spriteName").stringValue;var image=(Image)so.FindProperty("_image").objectReferenceValue;
            var tile=GetTile(resource,name,image.material);var caption=bind.GetComponent<ApprovedHudCaption>();
            if(caption!=null)
            {
                var cs=new SerializedObject(caption);var a=GetTile(resource,name,cs.FindProperty("_captionMaterial").objectReferenceValue as Material);var b=GetTile(resource,name,cs.FindProperty("_translatedMaterial").objectReferenceValue as Material);
                if(!collecting)
                {
                    cs.FindProperty("_bakedResourcePath").stringValue=a.sheet.resource;cs.FindProperty("_bakedCaptionSprite").stringValue=a.name;cs.FindProperty("_bakedTranslatedSprite").stringValue=b.name;
                    cs.FindProperty("_captionMaterial").objectReferenceValue=null;cs.FindProperty("_translatedMaterial").objectReferenceValue=null;cs.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            if(!collecting){so.FindProperty("_resourcePath").stringValue=tile.sheet.resource;so.FindProperty("_spriteName").stringValue=tile.name;so.ApplyModifiedPropertiesWithoutUndo();image.sprite=null;image.material=null;EditorUtility.SetDirty(image);}
        }

        static void ProcessConfiguration(UnityEngine.Object component)
        {
            var so=new SerializedObject(component);
            if(component is ToolAppearance appearance)
            {
                string path=appearance.BackgroundAtlasResource;if(!sheets.ContainsKey(path))return;
                var normal=GetTile(path,appearance.NormalSprite,appearance.NormalBackgroundMaterial);
                var locked=GetTile(path,appearance.LockedSprite,appearance.LockedBackgroundMaterial);
                if(!collecting){appearance.BackgroundAtlasResource=normal.sheet.resource;appearance.NormalSprite=normal.name;appearance.LockedSprite=locked.name;appearance.NormalBackgroundMaterial=null;appearance.LockedBackgroundMaterial=null;EditorUtility.SetDirty(appearance);}
                return;
            }
            else if(component is WzIconAmend)
            {
                var path=so.FindProperty("skinAtlasResource");if(!sheets.ContainsKey(path.stringValue))return;
                string original=path.stringValue;var material=so.FindProperty("skinMaterial");var alternateMaterial=so.FindProperty("singleCurrencySkinMaterial");
                var normal=GetTile(original,so.FindProperty("skinSpriteName").stringValue,material.objectReferenceValue as Material);
                var alternateName=so.FindProperty("singleCurrencySkinSpriteName");Tile alternate=null;
                if(!string.IsNullOrEmpty(alternateName.stringValue))alternate=GetTile(original,alternateName.stringValue,(alternateMaterial.objectReferenceValue??material.objectReferenceValue) as Material);
                if(!collecting){path.stringValue=normal.sheet.resource;so.FindProperty("skinSpriteName").stringValue=normal.name;if(alternate!=null)alternateName.stringValue=alternate.name;material.objectReferenceValue=null;alternateMaterial.objectReferenceValue=null;}
            }
            else if(component is WithdrawalFormPresentation)
            {
                var presets=so.FindProperty("presets");for(int i=0;i<presets.arraySize;i++)
                {
                    var images=presets.GetArrayElementAtIndex(i).FindPropertyRelative("images");for(int j=0;j<images.arraySize;j++)
                    {
                        var row=images.GetArrayElementAtIndex(j);var path=row.FindPropertyRelative("path");if(!sheets.ContainsKey(path.stringValue))continue;
                        var name=row.FindPropertyRelative("spriteName");var material=row.FindPropertyRelative("material");var tile=GetTile(path.stringValue,name.stringValue,material.objectReferenceValue as Material);
                        if(!collecting){path.stringValue=tile.sheet.resource;name.stringValue=tile.name;material.objectReferenceValue=null;row.FindPropertyRelative("sprite").objectReferenceValue=null;}
                    }
                }
            }
            else if(component is WithdrawWay)
            {
                var styles=so.FindProperty("resourceIconStyles");for(int i=0;i<styles.arraySize;i++)
                {
                    var row=styles.GetArrayElementAtIndex(i);ConvertRow(row,"resourcePath","spriteName","material");
                }
                var icon=so.FindProperty("styledIcon");if(icon.objectReferenceValue is Sprite sprite&&FindSheet(sprite,out var sheet))
                {
                    var tile=GetTile(sheet.source.resource,sprite.name,NamedMaterial(sheet,sprite.name));if(!collecting){icon.objectReferenceValue=tile.result;so.FindProperty("styledIconMaterial").objectReferenceValue=null;}
                }
            }
            else if(component is StarRatingPopup stars)
            {
                var path=so.FindProperty("starAtlasPath");if(sheets.ContainsKey(path.stringValue))
                {
                    var material=stars.starImages[0].material;var on=GetTile(path.stringValue,so.FindProperty("starOnName").stringValue,material);var off=GetTile(path.stringValue,so.FindProperty("starOffName").stringValue,material);
                    if(!collecting){path.stringValue=on.sheet.resource;so.FindProperty("starOnName").stringValue=on.name;so.FindProperty("starOffName").stringValue=off.name;}
                }
            }
            else if(component is WithdrawDanItem)
            {
                var path=so.FindProperty("artResource");if(sheets.ContainsKey(path.stringValue))
                {
                    string old=path.stringValue;foreach(string name in new[]{"completeFill","incompleteFill"})
                    {
                        var n=so.FindProperty(name);var m=so.FindProperty(name+"Material");var tile=GetTile(old,n.stringValue,m.objectReferenceValue as Material);if(!collecting){n.stringValue=tile.name;m.objectReferenceValue=null;}
                    }
                    if(!collecting)path.stringValue=sheets[old].resource;
                }
            }
            else if(so.FindProperty("symbolResources")!=null&&so.FindProperty("symbolNames")!=null)
            {
                var paths=so.FindProperty("symbolResources");var names=so.FindProperty("symbolNames");var fallback=so.FindProperty("symbolAtlasPath");
                for(int i=0;i<names.arraySize;i++)
                {
                    string path=i<paths.arraySize?paths.GetArrayElementAtIndex(i).stringValue:fallback.stringValue;if(string.IsNullOrEmpty(path))path=fallback.stringValue;
                    if(!sheets.ContainsKey(path))continue;var tile=GetTile(path,names.GetArrayElementAtIndex(i).stringValue,null);
                    if(!collecting){while(paths.arraySize<=i)paths.InsertArrayElementAtIndex(paths.arraySize);paths.GetArrayElementAtIndex(i).stringValue=tile.sheet.resource;names.GetArrayElementAtIndex(i).stringValue=tile.name;}
                }
                if(!collecting&&sheets.ContainsKey(fallback.stringValue))fallback.stringValue=sheets[fallback.stringValue].resource;
            }
            if(!collecting)so.ApplyModifiedPropertiesWithoutUndo();
        }
        static void ConvertRow(SerializedProperty row,string pathName,string spriteName,string materialName)
        {
            var path=row.FindPropertyRelative(pathName);if(!sheets.ContainsKey(path.stringValue))return;var name=row.FindPropertyRelative(spriteName);var mat=row.FindPropertyRelative(materialName);
            var tile=GetTile(path.stringValue,name.stringValue,mat.objectReferenceValue as Material);if(!collecting){path.stringValue=tile.sheet.resource;name.stringValue=tile.name;mat.objectReferenceValue=null;}
        }
        static bool FindSheet(Sprite sprite,out Sheet sheet)
        {
            string path=AssetDatabase.GetAssetPath(sprite);foreach(var s in sheets.Values)if(s.source.path==path){sheet=s;return true;}sheet=null;return false;
        }
        static void ClearBakedIconMaterials(GameObject root)
        {
            foreach(var wz in root.GetComponentsInChildren<WzIconAmend>(true))
            {
                if(!new SerializedObject(wz).FindProperty("skinAtlasResource").stringValue.StartsWith("ReleaseUI/",StringComparison.Ordinal))continue;
                var image=wz.image!=null?wz.image:wz.GetComponent<Image>();
                if(image!=null)image.material=null;
            }
        }
        public static void FinalizeHudMaterials()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(Path.Combine(Folder,"hud-extra-plan.json")));
            foreach(string path in plan.candidates)
            {
                if(!path.EndsWith(".prefab"))continue;
                var root=PrefabUtility.LoadPrefabContents(path);
                try{ClearBakedIconMaterials(root);PrefabUtility.SaveAsPrefabAsset(root,path);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();
        }
        static Material NamedMaterial(Sheet sheet,string name)=>AssetDatabase.LoadAssetAtPath<Material>(Path.GetDirectoryName(sheet.source.path).Replace('\\','/')+"/"+name+".mat");
        static void ProcessReferences(UnityEngine.Object target)
        {
            var so=new SerializedObject(target);var p=so.GetIterator();bool changed=false;
            while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue is Sprite sprite&&FindSheet(sprite,out var sheet))
            {
                var tile=GetTile(sheet.source.resource,sprite.name,NamedMaterial(sheet,sprite.name));if(!collecting){p.objectReferenceValue=tile.result;changed=true;}
            }
            if(changed)so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
