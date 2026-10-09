using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BreadLingo.I2.Editor.Tests
{
    public sealed class I2SnapshotTests
    {
        public enum Kind { Text, Sprite }
        public sealed class Language { public string Name; public string Code; public byte Flags; }
        public sealed class TermFixture
        {
            public string Term = "UI/Message";
            public Kind TermType = Kind.Text;
            public string Description = "Context";
            public string[] Languages = { "안녕 {0}", "Hello {0}" };
            public string[] Languages_Touch = { "터치", "Touch" };
            public byte[] Flags = { 0, 1 };
        }
        public sealed class Source
        {
            public List<Language> mLanguages = new List<Language> {
                new Language { Name = "Korean", Code = "ko" }, new Language { Name = "English", Code = "en" }
            };
            public List<TermFixture> mTerms = new List<TermFixture> { new TermFixture() };
        }
        [Test] public void CopiesTextTouchFlagsWithoutChangingSource()
        {
            var source = new Source();
            var snapshot = I2SnapshotReader.Read(source, "fixture:1");
            Assert.That(snapshot.entries[0].touchTranslations[0], Is.EqualTo("터치"));
            Assert.That(snapshot.entries[0].flags[1], Is.EqualTo(1));
            snapshot.entries[0].translations[0] = "changed";
            snapshot.entries[0].flags[1] = 0;
            Assert.That(source.mTerms[0].Languages[0], Is.EqualTo("안녕 {0}"));
            Assert.That(source.mTerms[0].Flags[1], Is.EqualTo(1));
        }
        [Test] public void RejectsUnsupportedStructure() { Assert.Throws<InvalidOperationException>(() => I2SnapshotReader.Read(new object(), "fixture:1")); }
        [Test] public void RejectsDuplicateLocaleCodes()
        {
            var source = new Source(); source.mLanguages[1].Code = "KO";
            Assert.Throws<InvalidOperationException>(() => I2SnapshotReader.Read(source, "fixture:1"));
        }
        [Test] public void RejectsDuplicateKeys()
        {
            var source = new Source(); source.mTerms.Add(new TermFixture());
            Assert.Throws<InvalidOperationException>(() => I2SnapshotReader.Read(source, "fixture:1"));
        }
        [Test] public void RejectsArrayLocaleMismatch()
        {
            var source = new Source(); source.mTerms[0].Languages_Touch = new string[0];
            Assert.Throws<InvalidOperationException>(() => I2SnapshotReader.Read(source, "fixture:1"));
        }
        [Test] public void ExcludesNonTextWithoutReadingAssetTranslations()
        {
            var source = new Source(); source.mTerms.Add(new TermFixture { Term = "UI/Icon", TermType = Kind.Sprite, Languages = null });
            var snapshot = I2SnapshotReader.Read(source, "fixture:1");
            Assert.That(snapshot.termCount, Is.EqualTo(2)); Assert.That(snapshot.nonTextCount, Is.EqualTo(1)); Assert.That(snapshot.entries.Count, Is.EqualTo(1));
        }
        [Test] public void FingerprintDetectsTextTouchFlagsAndLocaleChanges()
        {
            var source = new Source(); var hash = I2SnapshotReader.Read(source, "fixture:1").sourceHash;
            source.mTerms[0].Languages_Touch[1] = "Updated touch";
            var touchHash = I2SnapshotReader.Read(source, "fixture:1").sourceHash;
            Assert.That(touchHash, Is.Not.EqualTo(hash)); source.mTerms[0].Flags[1] = 0;
            Assert.That(I2SnapshotReader.Read(source, "fixture:1").sourceHash, Is.Not.EqualTo(touchHash));
        }
        [Test] public void CsvPreservesQuotesNewlinesAndFormulaLikeText()
        {
            var source = new Source(); source.mTerms[0].Languages[0] = "=text,\"quoted\"\nnext\\n";
            var csv = I2CsvExporter.Build(I2SnapshotReader.Read(source, "fixture:1"), "ko");
            Assert.That(csv, Does.StartWith("key,source_locale,source_text,description,i2_source_id,ko,en\n"));
            Assert.That(csv, Does.Contain("\"=text,\"\"quoted\"\"\nnext\\n\"")); Assert.That(csv, Does.Not.Contain("'=text"));
        }
        [Test] public void CsvRejectsUnknownSourceLocale() { Assert.Throws<ArgumentException>(() => I2CsvExporter.Build(I2SnapshotReader.Read(new Source(), "fixture:1"), "ja")); }
        [Test] public void RejectsLocaleCollidingWithCsvMetadata()
        {
            var source = new Source(); source.mLanguages[0].Code = "source_text";
            Assert.Throws<InvalidOperationException>(() => I2SnapshotReader.Read(source, "fixture:1"));
        }
        [Test] public void FingerprintDistinguishesNullAndEmptyTranslation()
        {
            var source = new Source(); source.mTerms[0].Languages[0] = null;
            var hash = I2SnapshotReader.Read(source, "fixture:1").sourceHash;
            source.mTerms[0].Languages[0] = "";
            Assert.That(I2SnapshotReader.Read(source, "fixture:1").sourceHash, Is.Not.EqualTo(hash));
        }
        [Test] public void FingerprintIsStableAcrossTermOrdering()
        {
            var source = new Source(); source.mTerms.Add(new TermFixture { Term = "UI/Another" });
            var hash = I2SnapshotReader.Read(source, "fixture:1").sourceHash;
            source.mTerms.Reverse(); Assert.That(I2SnapshotReader.Read(source, "fixture:1").sourceHash, Is.EqualTo(hash));
        }
        [Test] public void JsonRoundTripKeepsMetadataVariantsAndFlags()
        {
            var before = I2SnapshotReader.Read(new Source(), "fixture:1"); var after = JsonUtility.FromJson<I2Snapshot>(JsonUtility.ToJson(before));
            Assert.That(after.sourceHash, Is.EqualTo(before.sourceHash)); Assert.That(after.entries[0].touchTranslations, Is.EqualTo(before.entries[0].touchTranslations));
            Assert.That(after.entries[0].flags, Is.EqualTo(before.entries[0].flags)); Assert.That(after.languages[0].code, Is.EqualTo("ko"));
        }
    }
}
