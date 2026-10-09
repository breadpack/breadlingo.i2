using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace BreadLingo.I2.Editor
{
    public sealed class I2BaselineStore
    {
        private readonly string directory;
        public I2BaselineStore(string scope,string sourceId,string sourceLocale)
        {
            using(var hash=SHA256.Create()) directory="Library/BreadLingoI2/State/"+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(scope+"\n"+sourceId+"\n"+sourceLocale))).Replace("-","").ToLowerInvariant();
            Directory.CreateDirectory(directory);
        }
        public I2SyncBaseline Load(string scope,string sourceId,string sourceLocale)
        {
            var baseline=new I2SyncBaseline {scope=scope,sourceId=sourceId,sourceLocale=sourceLocale};var items=new Dictionary<string,I2SyncItem>();
            foreach(var file in Directory.GetFiles(directory,"journal-*.json").OrderBy(p=>p,StringComparer.Ordinal))
            {
                var record=JsonUtility.FromJson<I2SyncBaseline>(File.ReadAllText(file));
                if(record.scope!=scope||record.sourceId!=sourceId||record.sourceLocale!=sourceLocale)throw new InvalidOperationException("Baseline scope mismatch.");
                foreach(var item in record.items)items[I2SyncModel.Identity(item.key,item.variant)]=item;
            }
            baseline.items=items.Values.ToList();
            if(File.Exists(directory+"/pending.json"))baseline.pending=JsonUtility.FromJson<I2SyncRequest>(File.ReadAllText(directory+"/pending.json"));return baseline;
        }
        public void Pending(I2SyncRequest request){Atomic(directory+"/pending.json",JsonUtility.ToJson(request));}
        public void Append(I2SyncBaseline baseline,List<I2SyncItem> changed)
        {
            // Append small chunks instead of rewriting the entire game's baseline on every request.
            Atomic(directory+"/journal-"+DateTime.UtcNow.Ticks.ToString("D19")+"-"+Guid.NewGuid().ToString("N")+".json",JsonUtility.ToJson(new I2SyncBaseline {scope=baseline.scope,sourceId=baseline.sourceId,sourceLocale=baseline.sourceLocale,items=changed}));
        }
        public void ClearPending(){if(File.Exists(directory+"/pending.json"))File.Delete(directory+"/pending.json");}
        private static void Atomic(string path,string text)
        {
            string temporary=path+".tmp";File.WriteAllText(temporary,text,new UTF8Encoding(false));if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
        }
    }
}
