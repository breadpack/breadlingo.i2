using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BreadLingo.I2.Editor
{
    public static class I2SafeApply
    {
        // Only specific text array cells change; flags, references and unrelated terms survive.
        public static void ApplyValues(object source, IEnumerable<I2MergeChange> selected)
        {
            var changes = selected.ToList();
            var languages = (IList)source.GetType().GetField("mLanguages").GetValue(source);
            var terms = (IList)source.GetType().GetField("mTerms").GetValue(source);
            var codes = new Dictionary<string,int>(StringComparer.Ordinal);
            for (int i=0;i<languages.Count;i++) codes.Add((string)languages[i].GetType().GetField("Code").GetValue(languages[i]),i);
            var byKey = new Dictionary<string,object>(StringComparer.Ordinal);
            foreach(var term in terms) byKey.Add((string)term.GetType().GetField("Term").GetValue(term),term);
            var writes = new List<Action>(); var identities = new HashSet<string>();
            foreach(var change in changes)
            {
                if(!change.selected)continue;
                if(change.issue!=null)throw new InvalidOperationException("Resolve blocked entries by editing or resending, then preview again.");
                if(change.variant!="normal"&&change.variant!="touch")throw new InvalidOperationException("Unknown variant.");
                if(!identities.Add(I2SyncModel.Identity(change.key,change.variant)+"\n"+change.locale))throw new InvalidOperationException("Duplicate target write.");
                var term=byKey[change.key];var type=term.GetType();
                if(type.GetField("TermType").GetValue(term).ToString()!="Text")throw new InvalidOperationException("Only text terms may be changed.");
                var values=(string[])type.GetField(change.variant=="normal"?"Languages":"Languages_Touch").GetValue(term);
                int index=codes[change.locale];
                if(values.Length!=languages.Count||(values[index]??"")!=change.local)throw new InvalidOperationException("Local translation changed after preview.");
                writes.Add(()=>values[index]=change.remote);
            }
            // All validation finishes before the first mutation, including direct callers/tests.
            foreach(var write in writes)write();
        }
        public static string ApplyAsset(UnityEngine.Object asset, string expectedHash, List<I2MergeChange> changes)
        {
            var current=I2SnapshotReader.ReadAsset(asset);
            if(current.sourceHash!=expectedHash)throw new InvalidOperationException("Prefab changed after preview. Preview again.");
            if(!changes.Any(c=>c.selected))throw new InvalidOperationException("No translations selected.");
            string path=AssetDatabase.GetAssetPath(asset);
            if(!path.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Saved prefab required.");
            Directory.CreateDirectory("Library/BreadLingoI2/Backups");
            string backup="Library/BreadLingoI2/Backups/"+Guid.NewGuid().ToString("N")+".prefab";
            File.Copy(path,backup,false);
            // The persistent component is the same GUID/local-ID inspected by the reader.
            var gameObject=asset as GameObject;
            var source=gameObject != null ? gameObject.GetComponent(I2SnapshotReader.DetectSourceType()) : asset as Component;
            Undo.RecordObject(source,"Apply BreadLingo approved translations");
            try
            {
                ApplyValues(source,changes); EditorUtility.SetDirty(source);
                var expected=I2SnapshotReader.ReadAsset(asset);
                PrefabUtility.SavePrefabAsset(source.gameObject.transform.root.gameObject);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                var saved=I2SnapshotReader.ReadAsset(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                if(saved.sourceHash!=expected.sourceHash)throw new InvalidOperationException("Saved source changed outside the selected text cells.");
                foreach(var change in changes.Where(c=>c.selected))
                {
                    var entry=saved.entries.Find(e=>e.key==change.key);int locale=saved.languages.FindIndex(l=>l.code==change.locale);
                    if((change.variant=="normal"?entry.translations:entry.touchTranslations)[locale]!=change.remote)throw new InvalidOperationException("Saved prefab verification failed.");
                }
                return backup;
            }
            catch
            {
                File.Copy(backup,path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);throw;
            }
        }
    }
}
