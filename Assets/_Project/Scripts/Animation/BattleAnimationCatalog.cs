using System;
using UnityEngine;

public sealed class BattleAnimationCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Animation
    {
        public string id;
        public BattleAnimationAction action;
        public string resource;
        public string[] skillKeys;
        public bool archived;
        [NonSerialized] private BattleAnimationSequence cached;

        public BattleAnimationSequence Load()
        {
            if (cached == null && !string.IsNullOrEmpty(resource)) cached = Resources.Load<BattleAnimationSequence>(resource);
            return cached;
        }

        public void Release() { cached = null; }
    }

    [Serializable]
    public sealed class Entry
    {
        public string key;
        public string[] keys;
        public bool sourceFacingRight = true;
        public float referenceBodyHeight = 3f;
        public float visualScale = 1f;
        public Vector2 offset;
        public Animation[] animations;

        public BattleAnimationSequence Resolve(BattleAnimationAction action, string skillKey = null, int variantSeed = 0)
        {
            if (animations == null) return null;
            string normalizedSkill = Normalize(skillKey);
            if (!string.IsNullOrEmpty(normalizedSkill))
            {
                foreach (Animation animation in animations)
                {
                    if (animation == null || animation.archived || animation.action != action) continue;
                    if (Normalize(animation.id) == normalizedSkill || Matches(animation.skillKeys, normalizedSkill)) return animation.Load();
                }
            }
            int count = 0;
            foreach (Animation animation in animations)
                if (animation != null && !animation.archived && animation.action == action) count++;
            if (count == 0) return null;
            int selected = (int)((uint)variantSeed % count);
            foreach (Animation animation in animations)
                if (animation != null && !animation.archived && animation.action == action && selected-- == 0) return animation.Load();
            return null;
        }

        public bool HasAction(BattleAnimationAction action)
        {
            return animations != null && Array.Exists(animations, animation => animation != null && !animation.archived && animation.action == action);
        }
    }

    public Entry[] entries;
    private static BattleAnimationCatalog cached;
    public static BattleAnimationCatalog Instance => cached != null ? cached : cached = Resources.Load<BattleAnimationCatalog>("BattleAnimationCatalog");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() { cached = null; }

    public static void ReleaseCachedSequences()
    {
        if (cached == null || cached.entries == null) return;
        foreach (Entry entry in cached.entries)
        {
            if (entry == null || entry.animations == null) continue;
            foreach (Animation animation in entry.animations) animation?.Release();
        }
    }

    public static Entry Find(params string[] keys)
    {
        BattleAnimationCatalog catalog = Instance;
        if (catalog == null || catalog.entries == null || keys == null) return null;
        foreach (string candidate in keys)
        {
            string normalized = Normalize(candidate);
            if (string.IsNullOrEmpty(normalized)) continue;
            foreach (Entry entry in catalog.entries)
                if (entry != null && (Normalize(entry.key) == normalized || Matches(entry.keys, normalized))) return entry;
        }
        return null;
    }

    private static bool Matches(string[] keys, string normalized)
    {
        return keys != null && Array.Exists(keys, key => Normalize(key) == normalized);
    }

    private static string Normalize(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        string normalized = key.Trim().Replace('\\', '/');
        int separator = normalized.LastIndexOf('/');
        if (separator >= 0) normalized = normalized.Substring(separator + 1);
        foreach (string extension in new[] { ".png", ".asset", ".jpg" })
            if (normalized.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) normalized = normalized.Substring(0, normalized.Length - extension.Length);
        return normalized.ToLowerInvariant();
    }
}
