using System;
using System.Collections.Generic;
using System.Text;

namespace BreadLingo.I2.Editor
{
    public static class I2CsvExporter
    {
        // Translation exchange CSV, NOT an I2 runtime CSV or an apply artifact.
        // Normal translations only; raw touch arrays and flags remain in the JSON snapshot.
        public static string Build(I2Snapshot snapshot, string sourceLocale)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var sourceIndex = snapshot.languages.FindIndex(language => string.Equals(language.code, sourceLocale, StringComparison.Ordinal));
            if (sourceIndex < 0) throw new ArgumentException("Choose a source locale present in this source.", nameof(sourceLocale));
            var builder = new StringBuilder();
            var header = new List<string> { "key", "source_locale", "source_text", "description", "i2_source_id" };
            foreach (var language in snapshot.languages) header.Add(language.code);
            AppendRow(builder, header);
            foreach (var entry in snapshot.entries)
            {
                var row = new List<string> { entry.key, sourceLocale, entry.translations[sourceIndex], entry.description, snapshot.sourceId };
                row.AddRange(entry.translations);
                AppendRow(builder, row);
            }
            return builder.ToString();
        }

        private static void AppendRow(StringBuilder builder, IList<string> values)
        {
            for (var index = 0; index < values.Count; index++)
            {
                if (index > 0) builder.Append(',');
                var value = values[index] ?? "";
                if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
                    builder.Append('"').Append(value.Replace("\"", "\"\"")).Append('"');
                else builder.Append(value);
            }
            builder.Append('\n');
        }
    }
}
