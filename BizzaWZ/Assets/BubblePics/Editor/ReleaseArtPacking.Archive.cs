using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace BubblePics.EditorTools
{
    public static partial class ReleaseArtPacking
    {
        [Serializable] sealed class ArchivePlan { public string[] paths; }
        public static void ArchiveSources()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            var archive=JsonUtility.FromJson<ArchivePlan>(File.ReadAllText(Path.Combine(Folder,"archive-paths.json")));
            var paths=new HashSet<string>(archive.paths);
            foreach(string candidate in ReadPlan().candidates)
                foreach(string dependency in AssetDatabase.GetDependencies(candidate,true))
                    if(paths.Contains(dependency))throw new InvalidOperationException("Still referenced: "+candidate+" -> "+dependency);
            var settings=AddressableAssetSettingsDefaultObject.Settings;var log=new StringBuilder();
            foreach(string path in archive.paths)
            {
                if(!File.Exists(path))continue;
                BackupFile(path);settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(path));
                string to="Assets/BubblePics/ArtArchive/ReleaseCleanup20260929/"+path.Substring("Assets/BubblePics/".Length).Replace("Resources/","SourceData/");
                Directory.CreateDirectory(Path.GetDirectoryName(to));AssetDatabase.Refresh();
                string error=AssetDatabase.MoveAsset(path,to);if(!string.IsNullOrEmpty(error))throw new InvalidOperationException(error);
                log.AppendLine(path+" -> "+to);
            }
            EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
            log.AppendLine("PASS "+DateTime.UtcNow.ToString("O"));File.WriteAllText(Path.Combine(Folder,"archive-result.txt"),log.ToString());
        }
    }
}
