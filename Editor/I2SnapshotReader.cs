using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BreadLingo.I2.Editor
{
    public static class I2SnapshotReader
    {
        public static Type DetectSourceType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("I2.Loc.LanguageSource", false);
                if (type != null && typeof(Component).IsAssignableFrom(type)) return type;
            }
            // Unity can know a runtime assembly without eagerly loading it into
            // the Editor AppDomain. TypeCache covers that lazy-load case.
            foreach (var type in TypeCache.GetTypesDerivedFrom<Component>())
                if (type.FullName == "I2.Loc.LanguageSource") return type;
            return null;
        }

        public static I2Snapshot ReadAsset(UnityEngine.Object selected)
        {
            var type = DetectSourceType();
            if (type == null) throw new InvalidOperationException("Supported I2 LanguageSource component was not detected. Install I2 separately.");
            Component source = selected as Component;
            if (selected is GameObject gameObject) source = gameObject.GetComponent(type);
            if (source == null || !type.IsInstanceOfType(source))
                throw new InvalidOperationException("Select a GameObject or prefab containing an I2 LanguageSource component.");
            if (!EditorUtility.IsPersistent(source))
                throw new InvalidOperationException("This preview supports saved prefab assets only. Select the asset in the Project window.");
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long localId))
                throw new InvalidOperationException("The source has no saved asset identity.");
            return Read(source, guid + ":" + localId);
        }

        // Reflection stays at this boundary; all processing uses copied DTOs.
        // The source object is never mutated and I2 lifecycle/import methods are never called.
        public static I2Snapshot Read(object source, string sourceId)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("Source identity is required.", nameof(sourceId));
            var sourceFields = new Fields(source.GetType());
            var languages = sourceFields.Get(source, "mLanguages") as IList;
            var terms = sourceFields.Get(source, "mTerms") as IList;
            if (languages == null || terms == null)
                throw new InvalidOperationException("Unsupported I2 source structure: language and term lists are required.");
            var snapshot = new I2Snapshot { sourceId = sourceId, termCount = terms.Count };
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var reserved = new HashSet<string>(new[] { "key", "source_locale", "source_text", "description", "i2_source_id" }, StringComparer.OrdinalIgnoreCase);
            foreach (var item in languages)
            {
                if (item == null) throw new InvalidOperationException("A language entry is null.");
                var fields = new Fields(item.GetType());
                var code = fields.Get(item, "Code") as string;
                if (string.IsNullOrWhiteSpace(code) || code != code.Trim() || reserved.Contains(code) || !codes.Add(code))
                    throw new InvalidOperationException("Language codes must be nonempty, unique and distinct from CSV metadata columns.");
                snapshot.languages.Add(new I2Language {
                    name = fields.Get(item, "Name") as string,
                    code = code,
                    flags = Convert.ToByte(fields.Get(item, "Flags"))
                });
            }
            if (languages.Count == 0) throw new InvalidOperationException("At least one language is required.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var fieldCache = new Dictionary<Type, Fields>();
            foreach (var term in terms)
            {
                if (term == null) throw new InvalidOperationException("A term entry is null.");
                var type = term.GetType();
                if (!fieldCache.TryGetValue(type, out var fields)) fieldCache[type] = fields = new Fields(type);
                var key = fields.Get(term, "Term") as string;
                if (string.IsNullOrEmpty(key) || !keys.Add(key))
                    throw new InvalidOperationException("Term keys must be nonempty and unique within the selected source.");
                // Compare the enum name, never its numeric position (vendor versions differ).
                if (!string.Equals(Convert.ToString(fields.Get(term, "TermType")), "Text", StringComparison.Ordinal))
                {
                    snapshot.nonTextCount++;
                    continue;
                }
                var normal = fields.Get(term, "Languages") as string[];
                var touch = fields.Get(term, "Languages_Touch") as string[];
                var flags = fields.Get(term, "Flags") as byte[];
                if (normal == null || touch == null || flags == null || normal.Length != languages.Count || touch.Length != languages.Count || flags.Length != languages.Count)
                    throw new InvalidOperationException("Unsupported term arrays: language, touch and flag lengths must match the source languages.");
                snapshot.entries.Add(new I2TextEntry {
                    key = key,
                    description = fields.Get(term, "Description") as string,
                    translations = (string[])normal.Clone(),
                    touchTranslations = (string[])touch.Clone(),
                    flags = (byte[])flags.Clone()
                });
            }
            snapshot.entries.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
            snapshot.sourceHash = Hash(snapshot);
            return snapshot;
        }

        internal static string Hash(I2Snapshot snapshot)
        {
            // Length-prefixed values preserve null/empty differences and prevent
            // delimiter collisions. The fingerprint does not depend on Unity JSON.
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            using (var sha = SHA256.Create())
            {
                Write(writer, snapshot.schemaVersion); Write(writer, snapshot.sourceId);
                writer.Write(snapshot.termCount); writer.Write(snapshot.nonTextCount);
                writer.Write(snapshot.languages.Count);
                foreach (var language in snapshot.languages)
                {
                    Write(writer, language.name); Write(writer, language.code); writer.Write(language.flags);
                }
                writer.Write(snapshot.entries.Count);
                foreach (var entry in snapshot.entries)
                {
                    Write(writer, entry.key); Write(writer, entry.description);
                    foreach (var text in entry.translations) Write(writer, text);
                    foreach (var text in entry.touchTranslations) Write(writer, text);
                    writer.Write(entry.flags.Length); writer.Write(entry.flags);
                }
                writer.Flush(); stream.Position = 0;
                var bytes = sha.ComputeHash(stream);
                return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }
        }

        private static void Write(BinaryWriter writer, string value)
        {
            writer.Write(value != null);
            if (value != null) writer.Write(value);
        }

        private sealed class Fields
        {
            private readonly Type type;
            private readonly Dictionary<string, FieldInfo> cache = new Dictionary<string, FieldInfo>();
            public Fields(Type type) { this.type = type; }
            public object Get(object instance, string name)
            {
                if (!cache.TryGetValue(name, out var field))
                {
                    field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
                    if (field == null) throw new InvalidOperationException("Unsupported I2 structure: required field " + name + " is absent.");
                    cache[name] = field;
                }
                return field.GetValue(instance);
            }
        }
    }
}
