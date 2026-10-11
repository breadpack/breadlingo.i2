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
        private static readonly string[] Codes={"en","ja","zh-CN","zh-TW","es-ES","pt-BR","fr","de","it","ru","tr","th","vi","id","ar","ko"};
        private static readonly string[] Names={"English","日本語","简体中文","繁體中文","Español (España)","Português (Brasil)","Français","Deutsch","Italiano","Русский","Türkçe","ไทย","Tiếng Việt","Bahasa Indonesia","العربية","한국어"};
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
                    using(new EditorGUI.DisabledScope(busy||source==null||sourceIndex<0))if(GUILayout.Button("Connect account in browser",GUILayout.Height(40)))Start(async()=>{connection=await UnityAccountConnection.Begin(source,source.languages[sourceIndex].code,origin);Application.OpenURL(connection.verificationUri);message="Approve the matching game and code in your browser.";nextPoll=0;});
                }
                else if(!connection.Connected)
                {
                    EditorGUILayout.LabelField("Connection code",connection.userCode??"Request interrupted");
                    EditorGUILayout.HelpBox("Confirm this project in your browser. This request expires in 10 minutes.",MessageType.Info);
                    using(new EditorGUI.DisabledScope(busy))
                    {
                        if(!string.IsNullOrEmpty(connection.verificationUri)&&GUILayout.Button("Open account confirmation"))Application.OpenURL(connection.verificationUri);
                        if(GUILayout.Button("Cancel / reconnect"))Start(Disconnect);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Account",connection.accountName);EditorGUILayout.LabelField("Connected project",connection.projectName);
                    using(new EditorGUI.DisabledScope(busy))if(GUILayout.Button("Disconnect account"))Start(Disconnect);
                }
            }
            GUILayout.Space(16);
            if(connection!=null&&connection.Connected)
            {
                using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("2  Target languages",EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Choose the languages you want to localize into.",EditorStyles.wordWrappedLabel);GUILayout.Space(10);
                    using(new EditorGUI.DisabledScope(busy))
                    {
                        for(int i=0;i<Codes.Length;i++)if(Codes[i]!=connection.sourceLocale)
                        {
                            bool exists=connection.existingLocales.Contains(Codes[i]);
                            bool value=EditorGUILayout.ToggleLeft(Names[i]+" ("+Codes[i]+")"+(exists?" · Existing":""),selected.Contains(Codes[i]));
                            if(value)selected.Add(Codes[i]);else selected.Remove(Codes[i]);
                        }
                        // Preserve and expose less common exact I2 locale codes too.
                        foreach(var code in connection.existingLocales.Where(c=>c!=connection.sourceLocale&&!Codes.Contains(c)))
                        {bool value=EditorGUILayout.ToggleLeft(code+" · Existing",selected.Contains(code));if(value)selected.Add(code);else selected.Remove(code);}
                        GUILayout.Space(12);
                        using(new EditorGUI.DisabledScope(selected.Count==0))if(GUILayout.Button("Save selected languages",GUILayout.Height(40)))Start(async()=>{await UnityAccountConnection.ConfigureLanguages(connection,selected.OrderBy(c=>c,StringComparer.Ordinal).ToArray());message="Languages saved to BreadLingo. No game text was uploaded and no AI job was started.";});
                    }
                }
                EditorGUILayout.HelpBox("The source prefab is unchanged. New local I2 language columns are created only by a future explicit apply operation. AI translation is started separately in BreadLingo.",MessageType.Info);
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
        private void UpdateConnection()
        {
            if(busy||connection==null||connection.Connected||string.IsNullOrEmpty(connection.id)||EditorApplication.timeSinceStartup<nextPoll)return;
            nextPoll=EditorApplication.timeSinceStartup+6;
            Start(async()=>{try{if(await UnityAccountConnection.Poll(connection))message="Account connected. Choose your target languages below.";}catch(UnityConnectionException error){if(error.Status!=429){message=error.Message;nextPoll=EditorApplication.timeSinceStartup+30;}}});
        }
        private async void Start(Func<Task> action){if(busy)return;busy=true;try{await action();}catch(Exception error){message=error.Message;}finally{busy=false;Repaint();}}
    }
}
