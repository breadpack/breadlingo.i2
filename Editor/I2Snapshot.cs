using System;
using System.Collections.Generic;

namespace BreadLingo.I2.Editor
{
    [Serializable]
    public sealed class I2Snapshot
    {
        public string schemaVersion = "breadlingo.i2.snapshot.preview.v1";
        public string sourceId;
        public string sourceHash;
        public int termCount;
        public int nonTextCount;
        public List<I2Language> languages = new List<I2Language>();
        public List<I2TextEntry> entries = new List<I2TextEntry>();
    }

    [Serializable]
    public sealed class I2Language
    {
        public string name;
        public string code;
        public byte flags;
    }

    [Serializable]
    public sealed class I2TextEntry
    {
        public string key;
        public string description;
        public string[] translations;
        public string[] touchTranslations;
        public byte[] flags;
    }
}
