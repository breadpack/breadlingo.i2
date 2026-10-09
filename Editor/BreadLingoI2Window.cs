using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BreadLingo.I2.Editor
{
    public sealed class BreadLingoI2Window : EditorWindow
    {
        private UnityEngine.Object sourceAsset;
        private UnityEngine.Object scannedAsset;
        private I2Snapshot snapshot;
        private int sourceLanguage;
        private string message;

        [MenuItem("Window/BreadLingo/I2 Localization")]
        public static void Open() { GetWindow<BreadLingoI2Window>("BreadLingo I2"); }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("0.1.0 Preview — local inspection and export only. Online sync, approved pull, applying translations and runtime CSV generation are not available yet.", MessageType.Info);
            if (I2SnapshotReader.DetectSourceType() == null)
            {
                EditorGUILayout.HelpBox("Supported I2 LanguageSource was not detected. Install I2 Localization separately. This package does not include I2.", MessageType.Warning);
                return;
            }
            EditorGUI.BeginChangeCheck();
            sourceAsset = EditorGUILayout.ObjectField("I2 source prefab", sourceAsset, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck()) { snapshot = null; scannedAsset = null; message = null; }
            if (GUILayout.Button("Use selected prefab")) { sourceAsset = Selection.activeObject; snapshot = null; scannedAsset = null; message = null; }
            using (new EditorGUI.DisabledScope(sourceAsset == null))
                if (GUILayout.Button("Scan source")) Run(() => { snapshot = I2SnapshotReader.ReadAsset(sourceAsset); scannedAsset = sourceAsset; sourceLanguage = 0; message = "Scan complete. Source was not modified."; });
            if (snapshot != null)
            {
                EditorGUILayout.LabelField("Terms / text / excluded assets", snapshot.termCount + " / " + snapshot.entries.Count + " / " + snapshot.nonTextCount);
                var options = snapshot.languages.ConvertAll(language => language.name + " (" + language.code + ")").ToArray();
                sourceLanguage = EditorGUILayout.Popup("Source language", sourceLanguage, options);
                EditorGUILayout.SelectableLabel(snapshot.sourceHash, GUILayout.Height(20));
                EditorGUILayout.HelpBox("JSON contains text, touch variants and flags; it is not a complete I2 asset backup. CSV contains normal text only. Neither output is an I2 runtime import file. Exported content may be confidential. CSV preserves raw values: review untrusted formula-like text before opening in spreadsheet software.", MessageType.Warning);
                if (GUILayout.Button("Export text snapshot (JSON)")) Export(false);
                if (GUILayout.Button("Export translation exchange (CSV)")) Export(true);
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
            if (GUILayout.Button("Documentation")) Application.OpenURL("https://github.com/breadpack/breadlingo.i2#readme");
        }

        private void Export(bool csv)
        {
            var destination = EditorUtility.SaveFilePanel("Export local I2 text data", "", "i2-text", csv ? "csv" : "json");
            if (string.IsNullOrEmpty(destination)) return;
            Run(() => {
                // Read again: a user may have edited the prefab after scanning.
                var latest = I2SnapshotReader.ReadAsset(scannedAsset);
                if (!string.Equals(latest.sourceHash, snapshot.sourceHash, StringComparison.Ordinal))
                    throw new InvalidOperationException("Source changed after scanning. Scan again before exporting.");
                if (string.Equals(Path.GetFullPath(destination), Path.GetFullPath(AssetDatabase.GetAssetPath(scannedAsset)), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Export cannot overwrite the source asset.");
                File.WriteAllText(destination, csv ? I2CsvExporter.Build(snapshot, snapshot.languages[sourceLanguage].code) : JsonUtility.ToJson(snapshot, true), new UTF8Encoding(false));
                message = "Local export complete. No data was uploaded or applied.";
            });
        }

        private void Run(Action operation)
        {
            try { operation(); }
            catch (Exception error) { snapshot = null; scannedAsset = null; message = "Operation stopped: " + error.Message; }
        }
    }
}
