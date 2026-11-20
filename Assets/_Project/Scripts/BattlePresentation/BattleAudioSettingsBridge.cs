using System;
using System.Reflection;
using UnityEngine;

public static class BattleAudioSettingsBridge
{
    private static bool resolved;
    private static Action<AudioSource, bool, float> registerSource;
    private static Action<AudioSource> unregisterSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        resolved = false;
        registerSource = null;
        unregisterSource = null;
    }

    public static void RegisterAudioSource(AudioSource source, bool isMusic, float baseVolume)
    {
        if (source == null) return;
        Resolve();
        if (registerSource != null)
        {
            registerSource(source, isMusic, baseVolume);
            return;
        }
        string prefix = isMusic ? "Music" : "Sound";
        float volume = Mathf.Clamp(PlayerPrefs.GetInt(prefix + "Volume", 100), 0, 100) / 100f;
        source.volume = Mathf.Clamp01(baseVolume) * volume;
        source.mute = PlayerPrefs.GetInt(prefix + "Enabled", 1) != 1 || volume <= 0f;
    }

    public static void UnregisterAudioSource(AudioSource source)
    {
        if (ReferenceEquals(source, null)) return;
        Resolve();
        unregisterSource?.Invoke(source);
    }

    private static void Resolve()
    {
        if (resolved) return;
        resolved = true;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type settings = assembly.GetType("GameSettingsRuntime", false);
            if (settings == null) continue;
            MethodInfo register = settings.GetMethod("RegisterAudioSource", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(AudioSource), typeof(bool), typeof(float) }, null);
            MethodInfo unregister = settings.GetMethod("UnregisterAudioSource", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(AudioSource) }, null);
            if (register != null)
                registerSource = (Action<AudioSource, bool, float>)Delegate.CreateDelegate(typeof(Action<AudioSource, bool, float>), register);
            if (unregister != null)
                unregisterSource = (Action<AudioSource>)Delegate.CreateDelegate(typeof(Action<AudioSource>), unregister);
            return;
        }
    }
}
