using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BreadLingo.I2.Editor
{
    public static class I2LanguageSetup
    {
        public static string ExistingContentHash(object source,int count)
        {
            using(var bytes=new MemoryStream())using(var writer=new BinaryWriter(bytes,Encoding.UTF8,true))using(var sha=SHA256.Create())
            {
                var languages=(IList)source.GetType().GetField("mLanguages").GetValue(source);var terms=(IList)source.GetType().GetField("mTerms").GetValue(source);if(count>languages.Count)throw new InvalidOperationException("Missing original language columns.");
                for(int i=0;i<count;i++)foreach(var field in new[]{"Code","Name","Flags"})Write(writer,Convert.ToString(languages[i].GetType().GetField(field).GetValue(languages[i])));
                writer.Write(terms.Count);foreach(var term in terms){foreach(var name in new[]{"Term","TermType","Description"})Write(writer,Convert.ToString(term.GetType().GetField(name).GetValue(term)));foreach(var name in new[]{"Languages","Languages_Touch","Flags"}){var values=(Array)term.GetType().GetField(name).GetValue(term);for(int i=0;i<count;i++)Write(writer,i<values.Length?(values.GetValue(i)==null?null:Convert.ToString(values.GetValue(i))):name=="Flags"?"0":null);}}
                writer.Flush();bytes.Position=0;return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            }
        }
        private static void Write(BinaryWriter writer,string value){writer.Write(value!=null);if(value!=null)writer.Write(value);}
        public static List<UnityProjectLanguage> Missing(IEnumerable<string> existing,IEnumerable<UnityProjectLanguage> project)
        {
            var codes=new HashSet<string>(existing,StringComparer.OrdinalIgnoreCase);
            var result=new List<UnityProjectLanguage>();
            foreach(var row in project??new UnityProjectLanguage[0])if(row.role=="target"&&row.status=="active")
            {if(!UnityAccountConnection.ValidLocaleSyntax(row.code))throw new InvalidOperationException("Invalid project language.");if(codes.Add(row.code))result.Add(row);}
            return result;
        }
        // Build and validate all writes before mutation; no term-key validation is
        // needed for appending language columns to an otherwise unchanged source.
        public static void Append(object source,IReadOnlyList<UnityProjectLanguage> additions)
        {
            var languages=(IList)source.GetType().GetField("mLanguages").GetValue(source);
            var terms=(IList)source.GetType().GetField("mTerms").GetValue(source);
            if(languages.Count==0)throw new InvalidOperationException("Original I2 language required.");
            var codes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var item in languages)if(!codes.Add((string)item.GetType().GetField("Code").GetValue(item)))throw new InvalidOperationException("Duplicate I2 language code.");
            var newLanguages=new List<object>();var writes=new List<Action>();int oldCount=languages.Count,newCount=oldCount+additions.Count;
            foreach(var row in additions){if(row.status!="active"||row.role!="target"||!UnityAccountConnection.ValidLocaleSyntax(row.code)||!codes.Add(row.code))throw new InvalidOperationException("Only new active target languages can be appended.");var item=Activator.CreateInstance(languages[0].GetType());item.GetType().GetField("Code").SetValue(item,row.code);item.GetType().GetField("Name").SetValue(item,string.IsNullOrEmpty(row.nativeName)?row.displayName:row.nativeName);newLanguages.Add(item);}
            foreach(var term in terms)
            {
                if(term==null)throw new InvalidOperationException("Invalid I2 term.");var type=term.GetType();
                foreach(var name in new[]{"Languages","Languages_Touch","Flags"})
                {
                    var field=type.GetField(name);var old=field?.GetValue(term) as Array;
                    if(field==null||old==null||old.Length>oldCount)throw new InvalidOperationException("Unsupported I2 term arrays.");
                    var next=Array.CreateInstance(old.GetType().GetElementType(),newCount);Array.Copy(old,next,old.Length);
                    // Null is the untranslated value; never insert explanatory text.
                    writes.Add(()=>field.SetValue(term,next));
                }
            }
            foreach(var item in newLanguages)languages.Add(item);foreach(var write in writes)write();
        }
        public static string ApplyAsset(UnityEngine.Object asset,IReadOnlyList<UnityProjectLanguage> additions)
        {
            if(additions.Count==0)throw new InvalidOperationException("No new language columns selected.");
            var path=AssetDatabase.GetAssetPath(asset);var go=asset as GameObject;var source=go?.GetComponent(I2SnapshotReader.DetectSourceType());
            if(source==null||!EditorUtility.IsPersistent(source)||!path.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Saved I2 source prefab required.");
            var original=UnityAccountConnection.Discover(asset);if(Missing(original.languages.Select(l=>l.code),additions).Count!=additions.Count)throw new InvalidOperationException("Language columns changed. Refresh first.");var preserved=ExistingContentHash(source,original.languages.Count);
            Directory.CreateDirectory("Library/BreadLingoI2/Backups");string backup="Library/BreadLingoI2/Backups/"+Guid.NewGuid().ToString("N")+".prefab";File.Copy(path,backup,false);
            Undo.RecordObject(source,"Synchronize BreadLingo languages");
            try{Append(source,additions);EditorUtility.SetDirty(source);PrefabUtility.SavePrefabAsset(go.transform.root.gameObject);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);var savedAsset=AssetDatabase.LoadAssetAtPath<GameObject>(path);var saved=UnityAccountConnection.Discover(savedAsset);var expected=original.languages.Select(l=>l.code).Concat(additions.Select(l=>l.code));if(!saved.languages.Select(l=>l.code).SequenceEqual(expected)||ExistingContentHash(savedAsset.GetComponent(I2SnapshotReader.DetectSourceType()),original.languages.Count)!=preserved)throw new InvalidOperationException("Saved language column verification failed; original content restored from backup.");return backup;}
            catch{File.Copy(backup,path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);throw;}
        }
    }
}
