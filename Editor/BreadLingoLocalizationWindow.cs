using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace BreadLingo.I2.Editor
{
    public sealed class BreadLingoLocalizationWindow : EditorWindow
    {
        // All secrets live only in this transient state and the OS encrypted vault.
        private UnityConnectionState connection;
        private UnitySourceInfo source;
        private GameObject asset;
        private int sourceIndex=-1;
        private bool busy,advanced;
        private string message,origin="https://breadlingo.com";
        private double nextPoll;
        private Vector2 scroll;
        private readonly HashSet<string> selected=new HashSet<string>();
        private string languageQuery="",nextCursor;private int catalogTotal;
        private readonly List<UnityCatalogLanguage> catalog=new List<UnityCatalogLanguage>();
        [MenuItem("Window/BreadLingo/Game Localization")]
        public static void Open(){var window=GetWindow<BreadLingoLocalizationWindow>("BreadLingo");window.minSize=new Vector2(480,560);}
        private void OnEnable()
        {
            EditorApplication.update+=UpdateConnection;
            try
            {
                connection=UnityAccountConnection.Load();if(connection!=null){origin=connection.origin;foreach(var code in connection.selectedLocales??new string[0])selected.Add(code);}
                asset=connection!=null?AssetDatabase.LoadAssetAtPath<GameObject>(connection.assetPath):UnityAccountConnection.AutoDetectSource();
                Discover();
                if(connection!=null&&connection.Connected)RunOperation(RefreshLanguages);
            }catch(Exception error){message=error.Message;}
        }
        private void OnDisable(){EditorApplication.update-=UpdateConnection;}
        private void Discover()
        {
            source=asset!=null?UnityAccountConnection.Discover(asset):null;
            sourceIndex=source!=null&&connection!=null?source.languages.FindIndex(l=>l.code==connection.sourceLocale):-1;
        }
        private void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(18);EditorGUILayout.LabelField("BreadLingo",new GUIStyle(EditorStyles.boldLabel){fontSize=25});
            GUILayout.Space(12);EditorGUILayout.LabelField("Connect your game. Choose your languages.",EditorStyles.wordWrappedLabel);GUILayout.Space(18);
            using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(connection!=null&&connection.Connected?"✓ Account connected · Choose languages":"1  Account connection",EditorStyles.boldLabel);
                using(new EditorGUI.DisabledScope(busy||connection!=null))
                {
                    EditorGUI.BeginChangeCheck();asset=(GameObject)EditorGUILayout.ObjectField("I2 source",asset,typeof(GameObject),false);
                    if(EditorGUI.EndChangeCheck()){try{Discover();}catch(Exception error){source=null;message=error.Message;}}
                    if(source!=null)
                    {
                        EditorGUILayout.LabelField("Game",source.projectName);EditorGUILayout.LabelField("Existing languages",string.Join(", ",source.languages.Select(l=>l.code)));
                        var choices=new[]{"Choose original authoring language…"}.Concat(source.languages.Select(l=>l.name+" ("+l.code+")")).ToArray();sourceIndex=EditorGUILayout.Popup("Original language",sourceIndex+1,choices)-1;
                    }
                }
                if(source==null)EditorGUILayout.HelpBox("Select a saved I2 LanguageSource prefab. The package does not include the I2 vendor tool.",MessageType.Info);
                if(connection==null)
                {
                    using(new EditorGUI.DisabledScope(busy||source==null||sourceIndex<0))if(GUILayout.Button("Connect account in browser",GUILayout.Height(40)))RunOperation(async()=>{connection=await UnityAccountConnection.Begin(source,source.languages[sourceIndex].code,origin);Application.OpenURL(connection.verificationUri);message="Approve the matching game and code in your browser.";nextPoll=0;});
                }
                else if(!connection.Connected)
                {
                    EditorGUILayout.LabelField("Connection code",connection.userCode??"Request interrupted");
                    EditorGUILayout.HelpBox("Confirm this project in your browser. This request expires in 10 minutes.",MessageType.Info);
                    using(new EditorGUI.DisabledScope(busy))
                    {
                        if(!string.IsNullOrEmpty(connection.verificationUri)&&GUILayout.Button("Open account confirmation"))Application.OpenURL(connection.verificationUri);
                        if(GUILayout.Button("Cancel / reconnect"))RunOperation(Disconnect);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Account",connection.accountName);EditorGUILayout.LabelField("Connected project",connection.projectName);
                    using(new EditorGUI.DisabledScope(busy))if(GUILayout.Button("Disconnect account"))RunOperation(Disconnect);
                }
            }
            GUILayout.Space(16);
            if(connection!=null&&connection.Connected)
            {
                using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("2  Target languages",EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Every searchable language is available for human translation. AI readiness is shown separately for this project.",EditorStyles.wordWrappedLabel);GUILayout.Space(10);
                    using(new EditorGUI.DisabledScope(busy))
                    {
                        if(GUILayout.Button("Refresh project languages from BreadLingo"))RunOperation(RefreshLanguages);
                        foreach(var row in connection.projectLanguages??new UnityProjectLanguage[0])if(row.role=="target")
                        {
                            EditorGUILayout.LabelField(row.displayName+" ("+row.code+") · "+row.status+" · "+AiLabel(row.aiStatus),EditorStyles.wordWrappedLabel);
                        }
                        GUILayout.Space(10);languageQuery=EditorGUILayout.TextField("Search all languages",languageQuery);
                        if(GUILayout.Button("Search by name, native name or code"))RunOperation(()=>Search(false));
                        if(catalogTotal>0)EditorGUILayout.LabelField(catalogTotal+" matching languages");
                        foreach(var row in catalog)if(row.code!=connection.sourceLocale)
                        {bool exists=(connection.selectedLocales??new string[0]).Contains(row.code);var readiness=(connection.aiLanguageStates??new UnityLanguageAiState[0]).FirstOrDefault(state=>state.code==row.code);using(new EditorGUI.DisabledScope(exists)){bool value=EditorGUILayout.ToggleLeft(row.name+" · "+row.native+" ("+row.code+")"+(exists?" · Active":" · "+AiLabel(readiness?.status??connection.aiDefaultStatus)),selected.Contains(row.code));if(value)selected.Add(row.code);else selected.Remove(row.code);}}
                        if(!string.IsNullOrEmpty(nextCursor)&&GUILayout.Button("More results"))RunOperation(()=>Search(true));
                        GUILayout.Space(12);
                        using(new EditorGUI.DisabledScope(!selected.Except(connection.selectedLocales??new string[0]).Any()))if(GUILayout.Button("Add and save selected languages",GUILayout.Height(40)))RunOperation(async()=>{await UnityAccountConnection.ConfigureLanguages(connection,selected.OrderBy(c=>c,StringComparer.Ordinal).ToArray());message="Project languages saved and refreshed. AI translation starts separately.";});
                        if(GUILayout.Button("Open project language settings"))Application.OpenURL(connection.origin+"/app#project-languages?workspace="+Uri.EscapeDataString(connection.workspaceId)+"&project="+Uri.EscapeDataString(connection.projectId));
                    }
                }
                EditorGUILayout.HelpBox("Saving or refreshing changes project settings only. Synchronize below to append blank I2 language columns with a backup. Existing text, flags, assets and language order are preserved. Deactivation is managed on the project settings page.",MessageType.Info);
                using(new EditorGUI.DisabledScope(busy||asset==null||connection.languageVersion<1))if(GUILayout.Button("Synchronize active project languages to I2…"))RunOperation(async()=>{await UnityAccountConnection.RefreshLanguages(connection);var additions=I2LanguageSetup.Missing(source.languages.Select(l=>l.code),connection.projectLanguages);if(additions.Count==0){message="I2 language columns are already synchronized.";return;}if(!EditorUtility.DisplayDialog("Add I2 language columns",string.Join(", ",additions.Select(l=>l.code))+"\n\nAppend blank columns and create a prefab backup? Existing language columns will remain.","Add blank columns","Cancel"))return;var backup=I2LanguageSetup.ApplyAsset(asset,additions);Discover();connection.existingLocales=source.languages.Select(l=>l.code).ToArray();UnityAccountConnection.Save(connection);message="I2 languages synchronized. Backup: "+backup;});
                using(new EditorGUI.DisabledScope(busy))if(GUILayout.Button("Open source sync / approved translation preview"))BreadLingoI2Window.Open();
            }
            GUILayout.Space(14);advanced=EditorGUILayout.Foldout(advanced,"Advanced connection",true);
            if(advanced)
            {
                using(new EditorGUI.DisabledScope(connection!=null||busy))origin=EditorGUILayout.TextField("HTTPS service origin",origin);
                if(GUILayout.Button("Memory-only manual connector"))BreadLingoI2Window.Open();
            }
            if(!string.IsNullOrEmpty(message))EditorGUILayout.HelpBox(message,MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
        private async Task Disconnect()
        {
            try{await UnityAccountConnection.Disconnect(connection);}catch(UnityConnectionException error){if(error.Status!=403&&error.Status!=410)throw;UnityConnectionVault.Delete(UnityAccountConnection.VaultKey);}
            connection=null;selected.Clear();sourceIndex=-1;message="Disconnected. Connect again to choose a different account or original language.";
        }
        private static string AiLabel(string status){switch(status){case "available":return "AI supported";case "unsupported":return "AI unsupported";case "not_connected":return "AI not connected";case "policy_disabled":return "AI disabled by policy";default:return "AI unverified";}}
        private async Task RefreshLanguages(){await UnityAccountConnection.RefreshLanguages(connection);selected.Clear();foreach(var code in connection.selectedLocales)selected.Add(code);message="Project languages refreshed from BreadLingo.";if(catalog.Count==0)await Search(false);}
        private async Task Search(bool more){var page=await UnityAccountConnection.SearchLanguages(origin,languageQuery,more?nextCursor:null);if(!more)catalog.Clear();catalog.AddRange(page.entries??new UnityCatalogLanguage[0]);nextCursor=page.nextCursor;catalogTotal=page.total;}
        private void UpdateConnection()
        {
            if(busy||connection==null||connection.Connected||string.IsNullOrEmpty(connection.id)||EditorApplication.timeSinceStartup<nextPoll)return;
            nextPoll=EditorApplication.timeSinceStartup+6;
            RunOperation(async()=>{try{if(await UnityAccountConnection.Poll(connection))message="Account connected. Choose your target languages below.";}catch(UnityConnectionException error){if(error.Status!=429){message=error.Message;nextPoll=EditorApplication.timeSinceStartup+30;}}});
        }
        private async void RunOperation(Func<Task> action){if(busy)return;busy=true;try{await action();}catch(Exception error){message=error.Message;}finally{busy=false;Repaint();}}
    }
}
