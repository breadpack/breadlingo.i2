using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace BreadLingo.I2.Editor
{
    [Serializable] public sealed class UnitySourceInfo { public string sourceId,projectName,assetPath;public List<I2Language> languages=new List<I2Language>(); }
    [Serializable] public sealed class UnityConnectionState
    {
        public string origin="https://breadlingo.com",id,userCode,deviceSecret,connectorSecret,verificationUri,expiresAt,sourceId,sourceLocale,assetPath,workspaceId,projectId,projectName,accountName;
        public string[] existingLocales=new string[0],selectedLocales=new string[0];
        public int languageVersion;public string aiDefaultStatus="unknown"; public UnityProjectLanguage[] projectLanguages=new UnityProjectLanguage[0];public UnityLanguageAiState[] aiLanguageStates=new UnityLanguageAiState[0];
        public bool Connected { get {return !string.IsNullOrEmpty(projectId)&&!string.IsNullOrEmpty(connectorSecret);} }
    }
    [Serializable] public sealed class UnityPairingStart {public string projectName,sourceId,sourceLocale,deviceSecret,tokenHash;public string[] existingLocales;}
    [Serializable] public sealed class UnityPairingProof {public string id,deviceSecret;}
    [Serializable] public sealed class UnityPairingResponse {public string id,userCode,expiresAt,verificationUri,status,workspaceId,projectId,projectName,sourceLocale,accountName;public string[] existingLocales;}
    [Serializable] public sealed class UnityLocaleRequest {public string[] targetLocales;}
    [Serializable] public sealed class UnityLocaleResponse {public string[] targetLocales,selectedLocales;}
    [Serializable] public sealed class UnityProjectLanguage {public string id,code,displayName,nativeName,direction,status,role,aiStatus;public int version;}
    [Serializable] public sealed class UnityProjectLanguages {public string workspaceId,projectId,sourceLocale;public int version;public string[] activeTargetLocales,tokenAllowedLocales;public UnityProjectLanguage[] locales;public UnityLanguageAi ai;public UnityLanguageAiState[] aiStates;}
    [Serializable] public sealed class UnityLanguageAi {public UnityLanguageAiState defaultState;}
    [Serializable] public sealed class UnityLanguageAiState {public string code,status,reason,providerId,model,qualityMode;}
    [Serializable] public sealed class UnityCatalogLanguage {public string code,name,english,native,direction,kind;}
    [Serializable] public sealed class UnityCatalogPage {public string version,nextCursor;public int total,catalogTotal;public UnityCatalogLanguage[] entries;}
    [Serializable] public sealed class UnityVersionedLocaleRequest {public string[] targetLocales;public int baseVersion;}
    [Serializable] internal sealed class UnityApiError {public UnityApiErrorDetail error;}
    [Serializable] internal sealed class UnityApiErrorDetail {public string code;}
    public sealed class UnityConnectionException : Exception {public readonly string Code;public readonly int Status;public UnityConnectionException(string code,int status):base("BreadLingo connection: "+code+" (HTTP "+status+")."){Code=code;Status=status;}}
    public static class UnityAccountConnection
    {
        public static string VaultKey {get{return UnityConnectionVault.Key(Directory.GetParent(Application.dataPath).FullName);}}
        public static UnityConnectionState Load(){var json=UnityConnectionVault.Load(VaultKey);return string.IsNullOrEmpty(json)?null:JsonUtility.FromJson<UnityConnectionState>(json);}
        public static void Save(UnityConnectionState state){UnityConnectionVault.Save(VaultKey,JsonUtility.ToJson(state));}
        public static UnitySourceInfo Discover(UnityEngine.Object asset)
        {
            var type=I2SnapshotReader.DetectSourceType();if(type==null)throw new InvalidOperationException("Install I2 Localization separately.");
            var source=asset is GameObject go?go.GetComponent(type):asset as Component;
            if(source==null||!EditorUtility.IsPersistent(source)||!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long localId))throw new InvalidOperationException("Select a saved I2 LanguageSource prefab.");
            var list=type.GetField("mLanguages",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(source) as IList;
            if(list==null||list.Count==0)throw new InvalidOperationException("This I2 source has no languages.");
            var info=new UnitySourceInfo{sourceId=guid+":"+localId,projectName=string.IsNullOrWhiteSpace(PlayerSettings.productName)?Directory.GetParent(Application.dataPath).Name:PlayerSettings.productName,assetPath=AssetDatabase.GetAssetPath(asset)};
            foreach(var item in list)
            {
                if(item==null)throw new InvalidOperationException("Invalid I2 language entry.");var t=item.GetType();
                var code=t.GetField("Code")?.GetValue(item) as string;var name=t.GetField("Name")?.GetValue(item) as string;
                if(!ValidLocaleSyntax(code)||info.languages.Any(l=>string.Equals(l.code,code,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("I2 locale codes must be valid and unique.");
                info.languages.Add(new I2Language{code=code,name=name});
            }
            return info; // No term scan; data-quality errors must not prevent account onboarding.
        }
        public static GameObject AutoDetectSource()
        {
            var candidates=AssetDatabase.FindAssets("I2Languages t:Prefab");if(candidates.Length!=1)return null;
            var go=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(candidates[0]));
            var type=I2SnapshotReader.DetectSourceType();return type!=null&&go!=null&&go.GetComponent(type)!=null?go:null;
        }
        public static async Task<UnityConnectionState> Begin(UnitySourceInfo source,string sourceLocale,string origin="https://breadlingo.com")
        {
            ValidateOrigin(origin);if(!source.languages.Any(l=>l.code==sourceLocale))throw new InvalidOperationException("Choose the original authoring language first.");
            var state=new UnityConnectionState{origin=origin,sourceId=source.sourceId,sourceLocale=sourceLocale,projectName=source.projectName,assetPath=source.assetPath,existingLocales=source.languages.Select(l=>l.code).ToArray(),deviceSecret=RandomHex(),connectorSecret="bt_unity_"+RandomHex()};
            // Persist proof before network. No usable connector exists until the browser approves.
            Save(state);
            var response=await Post<UnityPairingResponse>(origin+"/api/connectors/unity/pairings",new UnityPairingStart{projectName=state.projectName,sourceId=state.sourceId,sourceLocale=sourceLocale,existingLocales=state.existingLocales,deviceSecret=state.deviceSecret,tokenHash=Hash(state.connectorSecret)});
            if(!System.Text.RegularExpressions.Regex.IsMatch(response.userCode??"","^[A-F0-9]{12}$")||response.verificationUri!=origin+"/unity-connect.html?code="+response.userCode)throw new InvalidOperationException("Unexpected verification address.");
            state.id=response.id;state.userCode=response.userCode;state.verificationUri=response.verificationUri;state.expiresAt=response.expiresAt;Save(state);return state;
        }
        public static async Task<bool> Poll(UnityConnectionState state)
        {
            var response=await Post<UnityPairingResponse>(state.origin+"/api/connectors/unity/pairings/poll",new UnityPairingProof{id=state.id,deviceSecret=state.deviceSecret});
            if(response.status!="approved")return false;
            if(string.IsNullOrEmpty(response.workspaceId)||string.IsNullOrEmpty(response.projectId)||response.sourceLocale!=state.sourceLocale)throw new InvalidOperationException("Unexpected project binding.");
            state.workspaceId=response.workspaceId;state.projectId=response.projectId;state.accountName=response.accountName;Save(state);return true;
        }
        public static async Task ConfigureLanguages(UnityConnectionState state,string[] locales)
        {
            if(!state.Connected)throw new InvalidOperationException("Connect the account first.");
            if(state.languageVersion<1)await RefreshLanguages(state);
            var response=await Post<UnityLocaleResponse>(ProjectUrl(state)+"/configure",new UnityVersionedLocaleRequest{targetLocales=locales,baseVersion=state.languageVersion},state.connectorSecret);
            if(response.selectedLocales==null||!response.selectedLocales.SequenceEqual(locales))throw new InvalidOperationException("Service did not confirm the selected languages.");
            state.selectedLocales=response.selectedLocales;Save(state);await RefreshLanguages(state);
        }
        public static bool ValidLocaleSyntax(string code){return !string.IsNullOrEmpty(code)&&code.Length<=128&&System.Text.RegularExpressions.Regex.IsMatch(code,"^(?:[a-zA-Z]{2,3}(-[a-zA-Z0-9]{1,8})*|x(-[a-zA-Z0-9]{1,8})+)$");}
        private static string ProjectUrl(UnityConnectionState state){ValidateOrigin(state.origin);if(!state.Connected||string.IsNullOrEmpty(state.workspaceId))throw new InvalidOperationException("Connect the account first.");return state.origin+"/api/workspaces/"+Uri.EscapeDataString(state.workspaceId)+"/projects/"+Uri.EscapeDataString(state.projectId)+"/connectors/unity";}
        public static async Task<UnityProjectLanguages> RefreshLanguages(UnityConnectionState state)
        {
            var data=await Get<UnityProjectLanguages>(ProjectUrl(state)+"/languages",state.connectorSecret);
            if(data.projectId!=state.projectId||data.workspaceId!=state.workspaceId||data.sourceLocale!=state.sourceLocale||data.version<1||data.locales==null||data.activeTargetLocales==null)throw new InvalidOperationException("Unexpected project language binding.");
            state.languageVersion=data.version;state.projectLanguages=data.locales;state.aiDefaultStatus=data.ai?.defaultState?.status??"unknown";state.aiLanguageStates=data.aiStates??new UnityLanguageAiState[0];state.selectedLocales=data.activeTargetLocales;Save(state);return data;
        }
        public static Task<UnityCatalogPage> SearchLanguages(string origin,string query="",string cursor=null,string category="all")
        {
            ValidateOrigin(origin);return Get<UnityCatalogPage>(origin+"/api/languages?uiLocale=en&limit=80&category="+Uri.EscapeDataString(category)+"&query="+Uri.EscapeDataString(query??"")+(string.IsNullOrEmpty(cursor)?"":"&cursor="+Uri.EscapeDataString(cursor)));
        }
        private static async Task<T> Get<T>(string url,string token=null)
        {
            using(var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(60)})
            {if(token!=null)client.DefaultRequestHeaders.Add("X-Connector-Token",token);using(var response=await client.GetAsync(url))return await ReadResponse<T>(response);}
        }
        private static async Task<T> ReadResponse<T>(HttpResponseMessage response)
        {
            var text=await response.Content.ReadAsStringAsync();if(text.Length>2097152)throw new InvalidOperationException("Connection response exceeds safe size.");
            if(!response.IsSuccessStatusCode){UnityApiError error=null;try{error=JsonUtility.FromJson<UnityApiError>(text);}catch{}throw new UnityConnectionException(error?.error?.code??"connection_failed",(int)response.StatusCode);}return JsonUtility.FromJson<T>(text);
        }
        public static async Task Disconnect(UnityConnectionState state)
        {
            // Device proof expires after pairing; persistent disconnect uses its project token.
            if(state.Connected)await Post<UnityLocaleResponse>(state.origin+"/api/workspaces/"+state.workspaceId+"/projects/"+state.projectId+"/connectors/unity/disconnect",new UnityLocaleRequest(),state.connectorSecret);
            else if(!string.IsNullOrEmpty(state.id))await Post<UnityLocaleResponse>(state.origin+"/api/connectors/unity/pairings/cancel",new UnityPairingProof{id=state.id,deviceSecret=state.deviceSecret});
            UnityConnectionVault.Delete(VaultKey);
        }
        public static void ValidateOrigin(string origin)
        {
            if(!Uri.TryCreate(origin,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment)||uri.AbsolutePath!="/"||origin.EndsWith("/"))throw new InvalidOperationException("Use an HTTPS service origin without a path, credentials or query.");
        }
        private static string RandomHex(){var bytes=new byte[32];using(var random=RandomNumberGenerator.Create())random.GetBytes(bytes);return BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();}
        public static string Hash(string text){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
        private static async Task<T> Post<T>(string url,object data,string token=null)
        {
            using(var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(60)})
            using(var content=new StringContent(JsonUtility.ToJson(data),Encoding.UTF8,"application/json"))
            {
                if(token!=null)client.DefaultRequestHeaders.Add("X-Connector-Token",token);
                using(var response=await client.PostAsync(url,content))
                {
                    return await ReadResponse<T>(response);
                }
            }
        }
    }
}
