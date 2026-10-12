using System;
using System.Linq;
using NUnit.Framework;

namespace BreadLingo.I2.Editor.Tests
{
    public sealed class LanguageSetupTests
    {
        private static UnityProjectLanguage Row(string code,string status="active"){return new UnityProjectLanguage{code=code,displayName=code,nativeName=code,status=status,role="target"};}
        [Test] public void MoreThan64LanguagesAreAppendedWithoutChangingExistingTextTouchFlagsOrOrder()
        {
            var source=new I2SnapshotTests.Source();var text=source.mTerms[0].Languages.ToArray();var touch=source.mTerms[0].Languages_Touch.ToArray();var flags=source.mTerms[0].Flags.ToArray();var rows=Enumerable.Range(0,100).Select(i=>Row("qaa-x-"+i)).ToArray();
            var preserved=I2LanguageSetup.ExistingContentHash(source,2);I2LanguageSetup.Append(source,rows);Assert.AreEqual(preserved,I2LanguageSetup.ExistingContentHash(source,2));Assert.AreEqual(102,source.mLanguages.Count);CollectionAssert.AreEqual(new[]{"ko","en"},source.mLanguages.Take(2).Select(l=>l.Code));CollectionAssert.AreEqual(text,source.mTerms[0].Languages.Take(2));CollectionAssert.AreEqual(touch,source.mTerms[0].Languages_Touch.Take(2));CollectionAssert.AreEqual(flags,source.mTerms[0].Flags.Take(2));Assert.IsTrue(source.mTerms[0].Languages.Skip(2).All(v=>v==null));Assert.IsTrue(source.mTerms[0].Flags.Skip(2).All(v=>v==0));
        }
        [Test] public void InvalidLateEntryFailsBeforeAnyMutation()
        {
            var source=new I2SnapshotTests.Source();Assert.Throws<InvalidOperationException>(()=>I2LanguageSetup.Append(source,new[]{Row("ar"),Row("en")}));Assert.AreEqual(2,source.mLanguages.Count);Assert.AreEqual(2,source.mTerms[0].Languages.Length);
        }
        [Test] public void DisabledOrExistingColumnsAreNeverRemovedOrRecreated()
        {
            var rows=new[]{Row("EN"),Row("cy","disabled"),Row("ar")};CollectionAssert.AreEqual(new[]{"ar"},I2LanguageSetup.Missing(new[]{"ko","en"},rows).Select(r=>r.code));
        }
        [Test] public void LegacyDuplicateTermKeysDoNotBlockNonDestructiveLanguageSetup()
        {
            var source=new I2SnapshotTests.Source();source.mTerms.Add(new I2SnapshotTests.TermFixture());I2LanguageSetup.Append(source,new[]{Row("ar")});Assert.AreEqual(3,source.mTerms[1].Languages.Length);Assert.AreEqual("Hello {0}",source.mTerms[1].Languages[1]);
        }
    }
}
