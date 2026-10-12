using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BreadLingo.I2.Editor.Tests
{
    public sealed class UnityConnectionTests
    {
        [Test] public void SourcePairAiStatesDeserializeForLanguagesNotYetAddedToTheProject()
        {
            var response=JsonUtility.FromJson<UnityProjectLanguages>("{\"aiStates\":[{\"code\":\"ar\",\"status\":\"available\",\"model\":\"evaluated-model\"},{\"code\":\"aa\",\"status\":\"unsupported\"}],\"ai\":{\"defaultState\":{\"status\":\"unknown\"}}}");Assert.AreEqual("available",response.aiStates[0].status);Assert.AreEqual("evaluated-model",response.aiStates[0].model);Assert.AreEqual("unsupported",response.aiStates[1].status);Assert.AreEqual("unknown",response.ai.defaultState.status);
        }
        [Test] public void OriginRejectsHttpRedirectsCredentialsAndNonOriginUrls()
        {
            UnityAccountConnection.ValidateOrigin("https://breadlingo.com");
            foreach(var url in new[]{"http://breadlingo.com","https://user:pass@breadlingo.com","https://breadlingo.com/path","https://breadlingo.com?token=x","https://breadlingo.com/","https://breadlingo.com#x"})Assert.Throws<InvalidOperationException>(()=>UnityAccountConnection.ValidateOrigin(url));
        }
        [Test] public void CryptographicHashMatchesServerContract()
        {
            Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",UnityAccountConnection.Hash("abc"));
        }
        [Test] public void AccountStateRequiresBothProjectAndCredential()
        {
            var state=new UnityConnectionState();Assert.IsFalse(state.Connected);state.projectId="project";Assert.IsFalse(state.Connected);state.connectorSecret="synthetic";Assert.IsTrue(state.Connected);
        }
        [Test] public void CurrentUserVaultRestoresPendingProofAndLanguageChoiceAfterReload()
        {
            if(Application.platform!=RuntimePlatform.WindowsEditor)Assert.Ignore("Windows DPAPI native test.");
            var key=UnityConnectionVault.Key(System.IO.Path.Combine(System.IO.Path.GetTempPath(),Guid.NewGuid().ToString("N")));
            var json=JsonUtility.ToJson(new UnityConnectionState{deviceSecret="synthetic_pending_proof",connectorSecret="synthetic_connector",userCode="AABBCCDDEEFF",projectId="p",selectedLocales=new[]{"es-ES","pt-BR"}});
            try
            {
                UnityConnectionVault.Save(key,json);var restored=JsonUtility.FromJson<UnityConnectionState>(UnityConnectionVault.Load(key));Assert.AreEqual("synthetic_pending_proof",restored.deviceSecret);CollectionAssert.AreEqual(new[]{"es-ES","pt-BR"},restored.selectedLocales);
                var path=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BreadLingo","Unity",key+".dpapi");Assert.IsFalse(System.Text.Encoding.UTF8.GetString(System.IO.File.ReadAllBytes(path)).Contains("synthetic_connector"));
            }
            finally{UnityConnectionVault.Delete(key);}
            Assert.IsNull(UnityConnectionVault.Load(key));
        }
    }
}
