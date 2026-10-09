using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BreadLingo.I2.Editor
{
    public sealed class I2SyncClient : IDisposable
    {
        private readonly HttpClient client;
        private readonly string endpoint;
        public readonly string Scope;
        public I2SyncClient(string origin, string workspace, string project, string token)
        {
            var uri = new Uri(origin);
            if (uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/")
                throw new InvalidOperationException("Use an HTTPS service origin with no path, credentials or query.");
            foreach (var reference in new[] { workspace, project })
                if (!System.Text.RegularExpressions.Regex.IsMatch(reference ?? "", "^[A-Za-z0-9_-]{1,128}$")) throw new InvalidOperationException("Workspace/project ID or slug required.");
            if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Project connector token required. It is kept in memory only.");
            Scope = uri.GetLeftPart(UriPartial.Authority) + "/" + workspace + "/" + project;
            endpoint = uri.GetLeftPart(UriPartial.Authority) + "/api/workspaces/" + workspace + "/projects/" + project + "/connectors/unity/i2/";
            client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(60) };
            client.DefaultRequestHeaders.Add("X-Connector-Token", token);
        }
        public async Task<T> Post<T>(string action, I2SyncRequest request)
        {
            if (action != "push" && action != "pull") throw new ArgumentException("Invalid sync operation.");
            var json = JsonUtility.ToJson(request);
            if (Encoding.UTF8.GetByteCount(json) > 262144) throw new InvalidOperationException("Chunk exceeds 256 KiB. Reduce term count or translation size.");
            using (var body = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var response = await client.PostAsync(endpoint + action, body))
            {
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Service refused " + action + " (HTTP " + (int)response.StatusCode + "). Check token scopes, exact project locales, QA and source baseline. No local asset was changed.");
                var text = await response.Content.ReadAsStringAsync();
                if (Encoding.UTF8.GetByteCount(text) > 8388608) throw new InvalidOperationException("Server response exceeds safe size.");
                return JsonUtility.FromJson<T>(text);
            }
        }
        public void Dispose() { client.Dispose(); }
    }
}
