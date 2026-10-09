using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
namespace BreadLingo.I2.Editor
{
    public sealed class BreadLingoI2Window : EditorWindow
    {
        private UnityEngine.Object sourceAsset;
        private I2Snapshot snapshot;
        private int sourceLanguage;
        private string origin="https://breadlingo.com",workspace="",project="",message;
        private static string token=""; // Never serialized or persisted.
        private bool busy;
        private Vector2 scroll;
        private int previewPage;
        private string previewHash,previewScope;
        private List<I2MergeChange> changes;
        private List<I2SyncRequest> previewRequests;
        private List<string> previewDigests;
        private I2SyncBaseline previewBaseline;
        [MenuItem("Window/BreadLingo/I2 Localization")]
        public static void Open(){GetWindow<BreadLingoI2Window>("BreadLingo I2");}
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("0.2.0 — I2 ↔ BreadLingo normal/touch text sync and approved merge preview. Configure exact source/target locale codes in the project. Branch: main.",MessageType.Info);
            if(I2SnapshotReader.DetectSourceType()==null){EditorGUILayout.HelpBox("Install I2 Localization separately. This package contains no I2 vendor code.",MessageType.Warning);return;}
            using(new EditorGUI.DisabledScope(busy))
            {
                EditorGUI.BeginChangeCheck();sourceAsset=EditorGUILayout.ObjectField("I2 source prefab",sourceAsset,typeof(GameObject),false);
                origin=EditorGUILayout.TextField("Service origin",origin);workspace=EditorGUILayout.TextField("Workspace ID / slug",workspace);project=EditorGUILayout.TextField("Project ID / slug",project);token=EditorGUILayout.PasswordField("Unity connector token",token);
                if(EditorGUI.EndChangeCheck()){snapshot=null;ClearPreview();}
                EditorGUILayout.LabelField("Token stays in memory; CI may use BREADLINGO_CONNECTOR_TOKEN.",EditorStyles.wordWrappedMiniLabel);
                if(GUILayout.Button("Use selected prefab")){sourceAsset=Selection.activeObject;snapshot=null;ClearPreview();}
                using(new EditorGUI.DisabledScope(sourceAsset==null))if(GUILayout.Button("Scan source"))Run(()=>{snapshot=I2SnapshotReader.ReadAsset(sourceAsset);sourceLanguage=0;ClearPreview();message="Scan complete.";});
                if(snapshot!=null)
                {
                    EditorGUILayout.LabelField("Terms / text / excluded assets",snapshot.termCount+" / "+snapshot.entries.Count+" / "+snapshot.nonTextCount);
                    EditorGUI.BeginChangeCheck();sourceLanguage=EditorGUILayout.Popup("Source locale",sourceLanguage,snapshot.languages.ConvertAll(l=>l.name+" ("+l.code+")").ToArray());if(EditorGUI.EndChangeCheck())ClearPreview();
                    EditorGUILayout.HelpBox("Send registers existing translations as needs_review and preserves web edits. Pull applies current-source approved translations. Missing terms are retained; rename/deletion requires explicit handling. Use a dedicated project for this source.",MessageType.Info);
                    if(GUILayout.Button("Send I2 → BreadLingo"))Start(Send);
                    if(GUILayout.Button("Preview approved BreadLingo → I2"))Start(Preview);
                    if(changes!=null)
                    {
                        int safe=changes.Count(c=>c.issue==null);EditorGUILayout.LabelField("Preview",safe+" safe changes / "+(changes.Count-safe)+" blocked conflicts");
                        scroll=EditorGUILayout.BeginScrollView(scroll,GUILayout.Height(230));
                        previewPage=Math.Max(0,Math.Min((changes.Count-1)/50,EditorGUILayout.IntField("Preview page (0-based)",previewPage)));
                        foreach(var c in changes.Skip(previewPage*50).Take(50))
                        {
                            using(new EditorGUI.DisabledScope(c.issue!=null))c.selected=EditorGUILayout.ToggleLeft(c.key+" ["+c.variant+" / "+c.locale+"]"+(c.issue==null?"":" — "+c.issue),c.selected);
                            EditorGUILayout.LabelField("Local: "+c.local,EditorStyles.wordWrappedMiniLabel);EditorGUILayout.LabelField("Approved: "+c.remote,EditorStyles.wordWrappedMiniLabel);
                        }
                        EditorGUILayout.EndScrollView();
                        EditorGUILayout.LabelField("Pages",(previewPage+1)+" / "+Math.Max(1,(changes.Count+49)/50)+" — "+changes.Count(c=>c.selected)+" selected");
                        if(GUILayout.Button("Clear selection"))foreach(var c in changes)c.selected=false;
                        using(new EditorGUI.DisabledScope(!changes.Any(c=>c.selected)))if(GUILayout.Button("Revalidate and apply selected translations"))Start(Apply);
                    }
                    if(GUILayout.Button("Export local text snapshot (JSON)"))Export(false);if(GUILayout.Button("Export local exchange CSV"))Export(true);
                }
            }
            if(!string.IsNullOrEmpty(message))EditorGUILayout.HelpBox(message,MessageType.Info);
            if(GUILayout.Button("Documentation"))Application.OpenURL("https://github.com/breadpack/breadlingo.i2#readme");
        }
        private I2SyncClient Client(){return new I2SyncClient(origin,workspace,project,string.IsNullOrEmpty(token)?Environment.GetEnvironmentVariable("BREADLINGO_CONNECTOR_TOKEN"):token);}
        private string Locale {get{return snapshot.languages[sourceLanguage].code;}}
        private I2Snapshot Fresh()
        {
            var current=I2SnapshotReader.ReadAsset(sourceAsset);if(snapshot==null||current.sourceHash!=snapshot.sourceHash)throw new InvalidOperationException("Source changed since scanning. Scan again.");return current;
        }
        private List<I2SyncRequest> Chunks(List<I2SyncItem> items)
        {
            var chunks=new List<I2SyncRequest>();I2SyncRequest chunk=null;
            foreach(var item in items)
            {
                if(chunk==null)chunk=new I2SyncRequest{sourceId=snapshot.sourceId,sourceLocale=Locale,requestId=Guid.NewGuid().ToString("N")};chunk.items.Add(item);
                if(chunk.items.Count>25||Encoding.UTF8.GetByteCount(JsonUtility.ToJson(chunk))>250000)
                {
                    chunk.items.RemoveAt(chunk.items.Count-1);if(chunk.items.Count==0)throw new InvalidOperationException("One term exceeds the safe chunk size.");
                    chunks.Add(chunk);chunk=new I2SyncRequest{sourceId=snapshot.sourceId,sourceLocale=Locale,requestId=Guid.NewGuid().ToString("N"),items=new List<I2SyncItem>{item}};
                    if(Encoding.UTF8.GetByteCount(JsonUtility.ToJson(chunk))>250000)throw new InvalidOperationException("One term exceeds the safe chunk size.");
                }
            }
            if(chunk!=null)chunks.Add(chunk);return chunks;
        }
        private async Task Send()
        {
            Fresh();ClearPreview();using(var client=Client())
            {
                var store=new I2BaselineStore(client.Scope,snapshot.sourceId,Locale);var baseline=store.Load(client.Scope,snapshot.sourceId,Locale);
                var previous=baseline.items.ToDictionary(i=>I2SyncModel.Identity(i.key,i.variant));
                Func<I2SyncRequest,Task> send=async request=>
                {
                    store.Pending(request);var response=await client.Post<I2PushResponse>("push",request);
                    if(response.schemaVersion!=request.schemaVersion||response.sourceId!=request.sourceId||response.sourceLocale!=request.sourceLocale||response.branch!="main"||response.items==null||response.items.Count!=request.items.Count)throw new InvalidOperationException("Sync acknowledgement mismatch.");
                    var updates=new List<I2SyncItem>();
                    foreach(var item in request.items)
                    {
                        var version=response.items.Single(v=>v.key==item.key&&v.variant==item.variant).version;if(version<1)throw new InvalidOperationException("Invalid source version.");
                        var identity=I2SyncModel.Identity(item.key,item.variant);var saved=JsonUtility.FromJson<I2SyncItem>(JsonUtility.ToJson(item));saved.expectedVersion=version;saved.lastSentHash=I2SyncModel.SentHash(item);
                        if(previous.TryGetValue(identity,out var old))foreach(var t in saved.translations){var known=old.translations.Find(v=>v.locale==t.locale);if(known!=null)t.text=known.text;}
                        previous[identity]=saved;updates.Add(saved);
                    }
                    store.Append(baseline,updates);store.ClearPending();
                };
                if(baseline.pending!=null)await send(baseline.pending);
                baseline.items=previous.Values.ToList();var all=I2SyncModel.BuildItems(snapshot,Locale,baseline);
                var delta=all.Where(i=>!previous.TryGetValue(I2SyncModel.Identity(i.key,i.variant),out var old)||I2SyncModel.SentHash(i)!=old.lastSentHash).ToList();
                // Requests use the immutable captured snapshot. Re-reading a large
                // game prefab for every small chunk would make this quadratic.
                var chunks=Chunks(delta);for(int i=0;i<chunks.Count;i++){message="Sending "+(i+1)+" / "+chunks.Count+" chunks…";Repaint();await send(chunks[i]);}
                var latest=I2SnapshotReader.ReadAsset(sourceAsset);
                message=latest.sourceHash==snapshot.sourceHash
                    ? "Send complete: "+delta.Count+" term variants. Existing web translations preserved. Review in BreadLingo before pulling."
                    : "Captured snapshot sent. The local source changed during upload; scan and send again before pulling.";
            }
        }
        private async Task Preview()
        {
            Fresh();ClearPreview();using(var client=Client())
            {
                var baseline=new I2BaselineStore(client.Scope,snapshot.sourceId,Locale).Load(client.Scope,snapshot.sourceId,Locale);
                if(baseline.pending!=null)throw new InvalidOperationException("Complete pending send before pulling.");if(baseline.items.Count==0)throw new InvalidOperationException("Send this source first to establish a merge baseline.");
                var requests=Chunks(I2SyncModel.BuildItems(snapshot,Locale,baseline));var digests=new List<string>();var remote=new List<I2ApprovedItem>();
                foreach(var request in requests){message="Reading approved chunks: "+(digests.Count+1)+" / "+requests.Count;Repaint();var result=await client.Post<I2PullResponse>("pull",request);ValidatePull(request,result);digests.Add(result.digest);remote.AddRange(result.items);}
                Fresh();changes=I2SyncModel.Preview(snapshot,baseline,remote,Locale);previewRequests=requests;previewDigests=digests;previewHash=snapshot.sourceHash;previewScope=client.Scope;previewBaseline=baseline;message="Preview ready. Unapproved, missing and locally edited translations are preserved.";
            }
        }
        private static void ValidatePull(I2SyncRequest request,I2PullResponse result)
        {
            if(result.schemaVersion!=request.schemaVersion||result.sourceId!=request.sourceId||result.sourceLocale!=request.sourceLocale||result.branch!="main"||string.IsNullOrEmpty(result.digest)||result.items==null)throw new InvalidOperationException("Approved snapshot scope mismatch.");
            foreach(var item in result.items)if(item.sourceVersion<1||item.translationVersion<1||item.text==null||!request.items.Any(i=>i.key==item.key&&i.variant==item.variant))throw new InvalidOperationException("Unexpected approved entry.");
        }
        private async Task Apply()
        {
            Fresh();using(var client=Client())
            {
                if(client.Scope!=previewScope)throw new InvalidOperationException("Project scope changed. Preview again.");var selected=changes.Where(c=>c.selected).ToList();
                var revalidationStarted=DateTime.UtcNow;
                for(int i=0;i<previewRequests.Count;i++)
                {
                    if(!previewRequests[i].items.Any(item=>selected.Any(c=>c.key==item.key&&c.variant==item.variant)))continue;
                    var current=await client.Post<I2PullResponse>("pull",previewRequests[i]);ValidatePull(previewRequests[i],current);if(current.digest!=previewDigests[i])throw new InvalidOperationException("Server approval or revision changed. Preview again.");
                }
                if((DateTime.UtcNow-revalidationStarted).TotalSeconds>30)throw new InvalidOperationException("Revalidation took too long. Select fewer cells and preview again.");
                Fresh();var backup=I2SafeApply.ApplyAsset(sourceAsset,previewHash,selected);var updated=new List<I2SyncItem>();
                var baselineByIdentity=previewBaseline.items.ToDictionary(i=>I2SyncModel.Identity(i.key,i.variant));
                foreach(var group in selected.GroupBy(c=>I2SyncModel.Identity(c.key,c.variant)))
                {
                    var item=baselineByIdentity[group.Key];foreach(var change in group)item.translations.Single(t=>t.locale==change.locale).text=change.remote;updated.Add(item);
                }
                new I2BaselineStore(client.Scope,snapshot.sourceId,Locale).Append(previewBaseline,updated);snapshot=I2SnapshotReader.ReadAsset(sourceAsset);ClearPreview();message="Applied "+selected.Count+" text cells; saved prefab verified. Backup: "+backup+". Separate runtime CSV loaders need their own runtime publication.";
            }
        }
        private void Export(bool csv)
        {
            var path=EditorUtility.SaveFilePanel("Export local I2 text data","","i2-text",csv?"csv":"json");if(string.IsNullOrEmpty(path))return;
            Run(()=>{Fresh();if(string.Equals(Path.GetFullPath(path),Path.GetFullPath(AssetDatabase.GetAssetPath(sourceAsset)),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Cannot overwrite prefab.");File.WriteAllText(path,csv?I2CsvExporter.Build(snapshot,Locale):JsonUtility.ToJson(snapshot,true),new UTF8Encoding(false));message="Export complete. Exchange CSV is not a runtime I2 CSV.";});
        }
        private void ClearPreview(){changes=null;previewRequests=null;previewDigests=null;previewBaseline=null;previewHash=null;previewScope=null;}
        private void Run(Action action){try{action();}catch(Exception e){message="Stopped: "+e.Message;}}
        private async void Start(Func<Task> action){if(busy)return;busy=true;try{await action();}catch(Exception e){ClearPreview();message="Stopped: "+e.Message;}finally{busy=false;Repaint();}}
    }
}
