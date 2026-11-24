using System;
using UnityEngine;

public sealed class ProductionWorkTimeIconCatalog : ScriptableObject
{
    [Serializable]
    private sealed class Entry
    {
        public string resourceName;
        public Sprite sprite;
    }

    [SerializeField] private Entry[] icons;

    public Sprite Find(string resourceName)
    {
        if (icons == null || string.IsNullOrWhiteSpace(resourceName)) return null;
        foreach (Entry entry in icons)
        {
            if (entry != null && string.Equals(entry.resourceName, resourceName, StringComparison.OrdinalIgnoreCase))
                return entry.sprite;
        }
        return null;
    }
}
