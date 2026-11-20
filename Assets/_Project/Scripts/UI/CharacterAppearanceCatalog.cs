using System;
using UnityEngine;

public sealed class CharacterAppearanceCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public string[] keys;
        public Sprite portrait;
        public Sprite residentPortrait;
        public string idleResource;

        [NonSerialized] private CharacterIdleSequence idle;

        public CharacterIdleSequence LoadIdle()
        {
            if (idle == null && !string.IsNullOrEmpty(idleResource))
                idle = Resources.Load<CharacterIdleSequence>(idleResource);
            return idle;
        }
    }

    [SerializeField] private Entry[] entries;
    private static CharacterAppearanceCatalog cached;

    public static Entry Find(params string[] keys)
    {
        if (keys == null) return null;
        if (cached == null) cached = Resources.Load<CharacterAppearanceCatalog>("CharacterAppearanceCatalog");
        if (cached == null || cached.entries == null) return null;
        foreach (string key in keys)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            string normalized = key.Trim().Replace('\\', '/');
            int separator = normalized.LastIndexOf('/');
            if (separator >= 0) normalized = normalized.Substring(separator + 1);
            if (normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(0, normalized.Length - 4);
            foreach (Entry entry in cached.entries)
            {
                if (entry == null || entry.keys == null) continue;
                foreach (string candidate in entry.keys)
                    if (string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase)) return entry;
            }
        }
        return null;
    }
}
