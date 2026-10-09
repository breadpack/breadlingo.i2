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
        private static I2SyncBaseline Baseline(I2Snapshot snapshot)
        {
            var items=I2SyncModel.BuildItems(snapshot,"ko",null);
            foreach(var item in items)item.expectedVersion=1;
            return new I2SyncBaseline{scope="test",sourceId=snapshot.sourceId,sourceLocale="ko",items=items};
        }
        private static I2ApprovedItem Approved(string text="Approved {0}",string variant="normal")
        {
            return new I2ApprovedItem{key="UI/Message",variant=variant,sourceText=variant=="normal"?"안녕 {0}":"터치",locale="en",text=text,sourceVersion=1,translationVersion=2};
        }
        [Test] public void SafeThreeWayMergeChangesOnlySelectedNormalCell()
        {
            var source=new Source();var snapshot=I2SnapshotReader.Read(source,"fixture:1");
            var changes=I2SyncModel.Preview(snapshot,Baseline(snapshot),new[]{Approved()},"ko");
            Assert.That(changes.Count,Is.EqualTo(1));Assert.That(changes[0].issue,Is.Null);
            I2SafeApply.ApplyValues(source,changes);
            Assert.That(source.mTerms[0].Languages[1],Is.EqualTo("Approved {0}"));
            Assert.That(source.mTerms[0].Languages[0],Is.EqualTo("안녕 {0}"));
            Assert.That(source.mTerms[0].Languages_Touch[1],Is.EqualTo("Touch"));
            Assert.That(source.mTerms[0].Flags[1],Is.EqualTo(1));
        }
        [Test] public void LocalAndRemoteEditsConflictWithoutMutation()
        {
            var source=new Source();var snapshot=I2SnapshotReader.Read(source,"fixture:1");var baseline=Baseline(snapshot);
            source.mTerms[0].Languages[1]="Local {0}";
            var changes=I2SyncModel.Preview(I2SnapshotReader.Read(source,"fixture:1"),baseline,new[]{Approved()},"ko");
            Assert.That(changes[0].issue,Is.Not.Null);Assert.That(changes[0].selected,Is.False);
            I2SafeApply.ApplyValues(source,changes);Assert.That(source.mTerms[0].Languages[1],Is.EqualTo("Local {0}"));
        }
        [Test] public void RemoteUnchangedFromBaseKeepsLocalEdit()
        {
            var source=new Source();var snapshot=I2SnapshotReader.Read(source,"fixture:1");var baseline=Baseline(snapshot);source.mTerms[0].Languages[1]="Local {0}";
            Assert.That(I2SyncModel.Preview(I2SnapshotReader.Read(source,"fixture:1"),baseline,new[]{Approved("Hello {0}")},"ko"),Is.Empty);
        }
        [Test] public void TouchApplyPreservesNormalTextAndFlags()
        {
            var source=new Source();var snapshot=I2SnapshotReader.Read(source,"fixture:1");
            var changes=I2SyncModel.Preview(snapshot,Baseline(snapshot),new[]{Approved("Approved touch","touch")},"ko");I2SafeApply.ApplyValues(source,changes);
            Assert.That(source.mTerms[0].Languages_Touch[1],Is.EqualTo("Approved touch"));Assert.That(source.mTerms[0].Languages[1],Is.EqualTo("Hello {0}"));Assert.That(source.mTerms[0].Flags[1],Is.EqualTo(1));
        }
        [Test] public void PreflightStopsEntireApplyWhenOneCellChanged()
        {
            var source=new Source();var snapshot=I2SnapshotReader.Read(source,"fixture:1");
            var changes=I2SyncModel.Preview(snapshot,Baseline(snapshot),new[]{Approved(),Approved("Touch remote","touch")},"ko");source.mTerms[0].Languages_Touch[1]="Late edit";
            Assert.Throws<InvalidOperationException>(()=>I2SafeApply.ApplyValues(source,changes));Assert.That(source.mTerms[0].Languages[1],Is.EqualTo("Hello {0}"));
        }
        [Test] public void MissingBaselineAndDuplicateRemoteCellsAreBlocked()
        {
            var snapshot=I2SnapshotReader.Read(new Source(),"fixture:1");
            Assert.Throws<InvalidOperationException>(()=>I2SyncModel.Preview(snapshot,null,new[]{Approved()},"ko"));
            Assert.Throws<InvalidOperationException>(()=>I2SyncModel.Preview(snapshot,Baseline(snapshot),new[]{Approved(),Approved()},"ko"));
        }
        [Test] public void StaleSourceRevisionAndUnknownLocaleAreBlocked()
        {
            var snapshot=I2SnapshotReader.Read(new Source(),"fixture:1");var remote=Approved();remote.sourceVersion=2;
            Assert.That(I2SyncModel.Preview(snapshot,Baseline(snapshot),new[]{remote},"ko")[0].issue,Is.Not.Null);
            remote=Approved();remote.locale="fr";Assert.That(I2SyncModel.Preview(snapshot,Baseline(snapshot),new[]{remote},"ko")[0].issue,Is.Not.Null);
        }
        [Test] public void SourceTextEditBlocksApprovedApply()
        {
            var source=new Source();var snapshot=I2SnapshotReader.Read(source,"fixture:1");var baseline=Baseline(snapshot);source.mTerms[0].Languages[0]="Changed";
            Assert.That(I2SyncModel.Preview(I2SnapshotReader.Read(source,"fixture:1"),baseline,new[]{Approved()},"ko")[0].issue,Is.Not.Null);
        }
        [Test] public void SendFingerprintDetectsLaterTranslationForPreviouslyEmptyTarget()
        {
            var source=new Source();source.mTerms[0].Languages[1]="";var snapshot=I2SnapshotReader.Read(source,"fixture:1");var original=I2SyncModel.BuildItems(snapshot,"ko",null)[0];
            var hash=I2SyncModel.SentHash(original);source.mTerms[0].Languages[1]="Later {0}";
            Assert.That(I2SyncModel.SentHash(I2SyncModel.BuildItems(I2SnapshotReader.Read(source,"fixture:1"),"ko",null)[0]),Is.Not.EqualTo(hash));
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
