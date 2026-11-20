using System;
using UnityEngine;

public sealed class IdleAdventurePreviewIconCatalog : ScriptableObject
{
    [Serializable]
    private sealed class Entry
    {
        public string[] keys;
        public Sprite sprite;
    }

    [SerializeField] private Entry[] icons;

    public Sprite Find(string key)
    {
        if (icons == null || string.IsNullOrWhiteSpace(key)) return null;
        string normalized = key.Trim();
        foreach (Entry entry in icons)
        {
            if (entry == null || entry.keys == null || entry.sprite == null) continue;
            foreach (string candidate in entry.keys)
            {
                if (string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase)) return entry.sprite;
            }
        }
        return null;
    }
}
