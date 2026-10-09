using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BreadLingo.I2.Editor
{
    [Serializable] public sealed class I2SyncTranslation { public string locale; public string text; }
    [Serializable] public sealed class I2SyncItem
    {
        public string key, variant, sourceText, description, lastSentHash;
        public int expectedVersion;
        public List<I2SyncTranslation> translations = new List<I2SyncTranslation>();
    }
    [Serializable] public sealed class I2SyncRequest
    {
        public string schemaVersion = "breadlingo.i2.sync.v1", branch = "main", sourceId, sourceLocale, requestId;
        public List<I2SyncItem> items = new List<I2SyncItem>();
    }
    [Serializable] public sealed class I2SourceVersion { public string key, variant; public int version; }
    [Serializable] public sealed class I2PushResponse { public string schemaVersion, sourceId, sourceLocale, branch; public List<I2SourceVersion> items; }
    [Serializable] public sealed class I2ApprovedItem
    {
        public string key, variant, sourceText, locale, text;
        public int sourceVersion, translationVersion;
    }
    [Serializable] public sealed class I2PullResponse
    {
        public string schemaVersion, sourceId, sourceLocale, branch, workspaceId, projectId, digest;
        public List<I2ApprovedItem> items = new List<I2ApprovedItem>();
    }
    [Serializable] public sealed class I2SyncBaseline
    {
        public string scope, sourceId, sourceLocale;
        public List<I2SyncItem> items = new List<I2SyncItem>();
        public I2SyncRequest pending;
    }
    public sealed class I2MergeChange
    {
        public string key, variant, locale, local, remote, issue;
        public bool selected;
    }
    public static class I2SyncModel
    {
        public static string Identity(string key, string variant) { return variant + "\n" + key; }
        public static string SentHash(I2SyncItem item)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))using(var sha=SHA256.Create())
            {
                foreach(var text in new[]{item.key,item.variant,item.sourceText,item.description})writer.Write(text??"");
                foreach(var t in item.translations.OrderBy(t=>t.locale,StringComparer.Ordinal)){writer.Write(t.locale);writer.Write(t.text??"");}
                writer.Flush();return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }
        public static List<I2SyncItem> BuildItems(I2Snapshot snapshot, string sourceLocale, I2SyncBaseline baseline)
        {
            int sourceIndex = snapshot.languages.FindIndex(l => l.code == sourceLocale);
            if (sourceIndex < 0) throw new InvalidOperationException("Source locale is missing.");
            var previous = (baseline?.items ?? new List<I2SyncItem>()).ToDictionary(i => Identity(i.key, i.variant));
            var result = new List<I2SyncItem>();
            foreach (var entry in snapshot.entries)
                foreach (var variant in new[] { "normal", "touch" })
                {
                    var values = variant == "normal" ? entry.translations : entry.touchTranslations;
                    if (variant == "touch" && values.All(string.IsNullOrEmpty)) continue;
                    previous.TryGetValue(Identity(entry.key, variant), out var old);
                    var item = new I2SyncItem { key = entry.key, variant = variant, sourceText = values[sourceIndex] ?? "", description = entry.description ?? "", expectedVersion = old?.expectedVersion ?? 0 };
                    for (int i = 0; i < snapshot.languages.Count; i++) if (i != sourceIndex)
                        item.translations.Add(new I2SyncTranslation { locale = snapshot.languages[i].code, text = values[i] ?? "" });
                    result.Add(item);
                }
            return result;
        }
        public static List<I2MergeChange> Preview(I2Snapshot local, I2SyncBaseline baseline, IEnumerable<I2ApprovedItem> remote, string sourceLocale)
        {
            if (baseline == null || baseline.sourceId != local.sourceId || baseline.sourceLocale != sourceLocale)
                throw new InvalidOperationException("Matching send baseline required before applying translations.");
            var entries = local.entries.ToDictionary(e => e.key);
            var bases = baseline.items.ToDictionary(i => Identity(i.key, i.variant));
            int sourceIndex = local.languages.FindIndex(l => l.code == sourceLocale);
            if(sourceIndex < 0) throw new InvalidOperationException("Source locale changed.");
            var seen = new HashSet<string>(); var changes = new List<I2MergeChange>();
            foreach (var item in remote)
            {
                if (item.variant != "normal" && item.variant != "touch") throw new InvalidOperationException("Unknown variant.");
                if (!seen.Add(Identity(item.key, item.variant) + "\n" + item.locale)) throw new InvalidOperationException("Duplicate approved target.");
                int target = local.languages.FindIndex(l => l.code == item.locale);
                string issue = null, current = null;
                bases.TryGetValue(Identity(item.key, item.variant), out var original);
                entries.TryGetValue(item.key, out var entry);
                if (entry == null || target < 0 || target == sourceIndex) issue = "Term or target locale missing";
                else
                {
                    var values = item.variant == "normal" ? entry.translations : entry.touchTranslations;
                    current = values[target] ?? "";
                    if (values[sourceIndex] != item.sourceText) issue = "Source text changed";
                    else if (original == null || original.sourceText != item.sourceText || original.expectedVersion != item.sourceVersion) issue = "Source baseline changed; send current source first";
                    else
                    {
                        var baseTranslation = original.translations.Find(t => t.locale == item.locale);
                        if (baseTranslation == null) issue = "Target baseline missing";
                        else if (current != baseTranslation.text && current != item.text && item.text != baseTranslation.text) issue = "Local and remote translations both changed";
                        // A remote value unchanged since the baseline must not replace a local edit.
                        else if (item.text == baseTranslation.text && current != item.text) continue;
                    }
                }
                if (issue == null && current == item.text) continue;
                changes.Add(new I2MergeChange { key = item.key, variant = item.variant, locale = item.locale, local = current, remote = item.text, issue = issue, selected = issue == null });
            }
            return changes;
        }
    }
}
