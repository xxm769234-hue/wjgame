using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameSettingsRuntime
{
    private const string MusicEnabledKey = "MusicEnabled";
    private const string MusicVolumeKey = "MusicVolume";
    private const string SoundEnabledKey = "SoundEnabled";
    private const string SoundVolumeKey = "SoundVolume";
    private const string QualityLevelKey = "SettingsQualityLevel";
    private const string FullScreenKey = "SettingsFullScreen";
    private const string TargetFrameRateKey = "SettingsTargetFrameRate";

    private const int DefaultVolume = 100;
    private const int DefaultTargetFrameRate = 60;

    private static readonly Dictionary<AudioSource, AudioSourceState> AudioSourceStates =
        new Dictionary<AudioSource, AudioSourceState>();

    public static bool MusicEnabled => PlayerPrefs.GetInt(MusicEnabledKey, 1) == 1;
    public static int MusicVolume => Mathf.Clamp(PlayerPrefs.GetInt(MusicVolumeKey, DefaultVolume), 0, 100);
    public static bool SoundEnabled => PlayerPrefs.GetInt(SoundEnabledKey, 1) == 1;
    public static int SoundVolume => Mathf.Clamp(PlayerPrefs.GetInt(SoundVolumeKey, DefaultVolume), 0, 100);
    public static int QualityLevel => Mathf.Clamp(
        PlayerPrefs.GetInt(QualityLevelKey, QualitySettings.GetQualityLevel()),
        0,
        Mathf.Max(0, QualitySettings.names.Length - 1));
    public static bool FullScreen => PlayerPrefs.GetInt(FullScreenKey, Screen.fullScreen ? 1 : 0) == 1;
    public static int TargetFrameRate => PlayerPrefs.GetInt(TargetFrameRateKey, DefaultTargetFrameRate) <= 30 ? 30 : 60;

    public static int QualityPreset
    {
        get
        {
            int current = QualityLevel;
            int bestPreset = 0;
            int bestDistance = int.MaxValue;
            for (int preset = 0; preset < 3; preset++)
            {
                int distance = Mathf.Abs(current - GetQualityLevelForPreset(preset));
                if (distance < bestDistance)
                {
                    bestPreset = preset;
                    bestDistance = distance;
                }
            }

            return bestPreset;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyAfterFirstSceneLoad()
    {
        ApplyAll();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyAll();
    }

    public static void SetMusicEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(MusicEnabledKey, enabled ? 1 : 0);
        ApplyAudioSettings();
    }

    public static void SetMusicVolume(int volume)
    {
        PlayerPrefs.SetInt(MusicVolumeKey, Mathf.Clamp(volume, 0, 100));
        ApplyAudioSettings();
    }

    public static void SetSoundEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(SoundEnabledKey, enabled ? 1 : 0);
        ApplyAudioSettings();
    }

    public static void SetSoundVolume(int volume)
    {
        PlayerPrefs.SetInt(SoundVolumeKey, Mathf.Clamp(volume, 0, 100));
        ApplyAudioSettings();
    }

    public static void SetQualityPreset(int preset)
    {
        PlayerPrefs.SetInt(QualityLevelKey, GetQualityLevelForPreset(Mathf.Clamp(preset, 0, 2)));
        ApplyDisplaySettings();
    }

    public static void SetFullScreen(bool fullScreen)
    {
        PlayerPrefs.SetInt(FullScreenKey, fullScreen ? 1 : 0);
        ApplyDisplaySettings();
    }

    public static void SetTargetFrameRate(int frameRate)
    {
        PlayerPrefs.SetInt(TargetFrameRateKey, frameRate <= 30 ? 30 : 60);
        ApplyDisplaySettings();
    }

    public static void ResetDefaults()
    {
        PlayerPrefs.SetInt(MusicEnabledKey, 1);
        PlayerPrefs.SetInt(MusicVolumeKey, DefaultVolume);
        PlayerPrefs.SetInt(SoundEnabledKey, 1);
        PlayerPrefs.SetInt(SoundVolumeKey, DefaultVolume);
        PlayerPrefs.SetInt(QualityLevelKey, GetQualityLevelForPreset(1));
        PlayerPrefs.SetInt(FullScreenKey, 1);
        PlayerPrefs.SetInt(TargetFrameRateKey, DefaultTargetFrameRate);
        ApplyAll();
    }

    public static void ApplyAll()
    {
        ApplyDisplaySettings();
        ApplyAudioSettings();
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }

    private static int GetQualityLevelForPreset(int preset)
    {
        int count = QualitySettings.names.Length;
        if (count <= 1)
        {
            return 0;
        }

        if (preset <= 0)
        {
            return count >= 4 ? 1 : 0;
        }

        if (preset == 1)
        {
            return Mathf.Clamp(count / 2, 0, count - 1);
        }

        return count - 1;
    }

    private static void ApplyDisplaySettings()
    {
        int qualityLevel = QualityLevel;
        if (QualitySettings.GetQualityLevel() != qualityLevel)
        {
            QualitySettings.SetQualityLevel(qualityLevel, true);
        }

        bool fullScreen = FullScreen;
        FullScreenMode targetMode = fullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        if (Screen.fullScreenMode != targetMode)
        {
            Screen.fullScreenMode = targetMode;
        }

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
    }

    private static void ApplyAudioSettings()
    {
        AudioSource[] sources = UnityEngine.Object.FindObjectsOfType<AudioSource>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null)
            {
                continue;
            }

            if (!AudioSourceStates.TryGetValue(source, out AudioSourceState state))
            {
                state = new AudioSourceState(source.volume, source.mute);
                AudioSourceStates[source] = state;
            }

            bool isMusic = IsMusicSource(source);
            bool enabled = isMusic ? MusicEnabled : SoundEnabled;
            float volume = (isMusic ? MusicVolume : SoundVolume) / 100f;
            source.volume = state.Volume * volume;
            source.mute = state.Muted || !enabled || volume <= 0f;
        }
    }

    private static bool IsMusicSource(AudioSource source)
    {
        string name = source.name ?? string.Empty;
        string clipName = source.clip != null ? source.clip.name : string.Empty;
        string mixerName = source.outputAudioMixerGroup != null ? source.outputAudioMixerGroup.name : string.Empty;
        string identity = (name + " " + clipName + " " + mixerName).ToLowerInvariant();
        return source.loop
            || identity.Contains("music")
            || identity.Contains("bgm")
            || identity.Contains("background");
    }

    private sealed class AudioSourceState
    {
        public AudioSourceState(float volume, bool muted)
        {
            Volume = volume;
            Muted = muted;
        }

        public float Volume { get; }
        public bool Muted { get; }
    }
}
