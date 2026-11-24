using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class SceneBackgroundMusic : MonoBehaviour
{
    private const string MusicPath = "Audio/BackgroundMusic/";
    private static SceneBackgroundMusic instance;
    private static bool quitting;

    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private AudioSource musicSource;
    private string currentTrack;
    private bool preparationVisible;
    private bool battleVisible;
    private AudioClip battleMusic;
    private float trackGain = 1f;
    private float duckGain = 1f;
    private float duckUntil;

    public static void EmphasizeBattleSound()
    {
        if (instance != null && instance.currentTrack == "Battle")
            instance.duckUntil = Time.unscaledTime + .18f;
    }

    private void Update()
    {
        if (currentTrack != "Battle") return;
        bool attacking = Time.unscaledTime < duckUntil;
        float next = Mathf.MoveTowards(duckGain, attacking ? .60f : 1f,
            Time.unscaledDeltaTime * (attacking ? 16f : 1.6f));
        if (next == duckGain) return;
        duckGain = next;
        BattleAudioSettingsBridge.RegisterAudioSource(musicSource, true, trackGain * duckGain);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        instance = null;
        quitting = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        EnsureInstance();
    }

    public static void SetPreparationVisible(bool visible)
    {
        if (!Application.isPlaying || quitting)
        {
            return;
        }

        if (visible)
        {
            EnsureInstance();
        }

        if (instance == null)
        {
            return;
        }

        instance.preparationVisible = visible;
        instance.RefreshForScene(SceneManager.GetActiveScene());
    }

    public static void SetBattleVisible(bool visible, AudioClip music = null)
    {
        if (!Application.isPlaying || quitting) return;
        if (visible) EnsureInstance();
        if (instance == null) return;
        instance.battleVisible = visible;
        instance.battleMusic = visible ? music : null;
        instance.RefreshForScene(SceneManager.GetActiveScene());
    }

    private static void EnsureInstance()
    {
        if (instance != null || quitting)
        {
            return;
        }

        instance = FindObjectOfType<SceneBackgroundMusic>();
        if (instance == null)
        {
            new GameObject("SceneBackgroundMusic").AddComponent<SceneBackgroundMusic>();
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.dopplerLevel = 0f;
        musicSource.priority = 0;
        musicSource.volume = 1f;
        BattleAudioSettingsBridge.RegisterAudioSource(musicSource, true, 1f);
    }

    private void OnEnable()
    {
        if (instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void OnDestroy()
    {
        BattleAudioSettingsBridge.UnregisterAudioSource(musicSource);
        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnApplicationQuit()
    {
        quitting = true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene == SceneManager.GetActiveScene())
        {
            RefreshForScene(scene);
        }
    }

    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        RefreshForScene(next);
    }

    private void RefreshForScene(Scene scene)
    {
        if (scene.name != "Idle Adventure mode")
        {
            battleVisible = false;
            battleMusic = null;
            preparationVisible = false;
        }
        string track = null;
        switch (scene.name)
        {
            case "Launch":
                track = "Login";
                break;
            case "HomeScene":
                track = "Home";
                break;
            case "Idle Adventure mode":
                track = battleVisible ? "Battle" : preparationVisible ? "Preparation" : null;
                break;
            case "Simulation management mode":
                track = "Simulation";
                break;
        }

        PlayTrack(track);
    }

    private void PlayTrack(string track)
    {
        AudioClip clip = track == "Battle" ? battleMusic : null;
        if (clip == null && !string.IsNullOrEmpty(track) && !clips.TryGetValue(track, out clip))
        {
            clip = Resources.Load<AudioClip>(MusicPath + track);
            if (clip != null) clips[track] = clip;
        }
        if (currentTrack == track && musicSource.clip == clip)
        {
            return;
        }

        musicSource.Stop();
        musicSource.clip = null;
        currentTrack = track;
        duckGain = 1f;
        duckUntil = 0f;
        if (string.IsNullOrEmpty(track))
        {
            return;
        }

        if (clip == null)
        {
            Debug.LogWarning("Background music is missing: " + MusicPath + track, this);
            return;
        }

        musicSource.clip = clip;
        musicSource.loop = true;
        trackGain = track == "Battle" ? (battleMusic != null ? .18f : .24f) : 1f;
        BattleAudioSettingsBridge.RegisterAudioSource(musicSource, true, trackGain);
        musicSource.Play();
    }
}
