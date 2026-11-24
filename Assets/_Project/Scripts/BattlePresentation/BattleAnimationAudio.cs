using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleAnimationAudio : MonoBehaviour
{
    private sealed class Voice
    {
        public AudioSource source;
        public AudioClip clip;
        public float firstFrame;
        public float lastFrame;
        public float firstTime;
        public float lastTime;
        public float pitch = 1f;
        public bool started;
        public bool paused;
        public bool synchronized;
        public bool looping;
        public bool emphasize;
    }

    private readonly List<Voice> voices = new List<Voice>();
    private BattleAnimationSequence sequence;
    private int voiceCount;
    private float duration;
    private float previousFrame = -1f;
    private bool samplePaused;
    private bool applicationPaused;
    private static BattleHitFeedbackProfile defaultImpact;

    public int StartedCount { get; private set; }
    public int ActiveVoiceCount => voices.FindAll(voice => voice.clip != null && voice.started
        && (voice.paused || voice.source.isPlaying)).Count;

    public void Begin(BattleAnimationSequence animation, float seconds)
    {
        Clear();
        sequence = animation;
        if (sequence == null || !sequence.IsValid) return;
        duration = Mathf.Max(.01f, seconds > 0f ? seconds : sequence.Duration);
        bool attack = sequence.action == BattleAnimationAction.NormalAttack || sequence.action == BattleAnimationAction.SkillAttack;
        bool authored = sequence.HasAudio;
        if (authored)
        {
            foreach (BattleAudioCue cue in sequence.audioCues)
            {
                if (cue == null || !cue.IsValid) continue;
                AddVoice(cue.clip, cue.startFrame, Mathf.Min(sequence.FrameCount, cue.endFrame), cue.volume, true, false, attack);
            }
            if (!attack || sequence.audioCuesIncludeImpacts) return;
        }
        if (!attack) return;
        int impacts = sequence.impactFrames?.Length ?? 0;
        HashSet<AudioClip> sustained = new HashSet<AudioClip>();
        for (int index = 0; index < Mathf.Max(attack ? 1 : 0, impacts); index++)
        {
            BattleHitFeedbackProfile profile = sequence.impactFeedback != null && index < sequence.impactFeedback.Length
                ? sequence.impactFeedback[index] : null;
            if (attack && profile == null)
            {
                if (defaultImpact == null) defaultImpact = Resources.Load<BattleHitFeedbackProfile>("BattleAnimations/Feedback/Medium");
                profile = defaultImpact;
            }
            if (profile == null) continue;
            float impactFrame = index < impacts ? sequence.impactFrames[index] : sequence.FrameCount / 2;
            if (authored)
            {
                AddFeedbackVoice(profile.contactSounds, index, impactFrame, profile.contactGain * .45f, true);
                continue;
            }
            float windupFrame = FrameAtPlaybackTime(PlaybackTime(impactFrame) - profile.windupLead);
            AddFeedbackVoice(profile.windupSounds, index, windupFrame, attack ? .29f : .36f * .55f);
            AddFeedbackVoice(profile.contactSounds, index, impactFrame, profile.contactGain * (attack ? .78f : .55f), attack);
            AddFeedbackVoice(profile.tailSounds, index, impactFrame, .25f * .55f);
            if (profile.sustainSound != null && sustained.Add(profile.sustainSound))
            {
                float endFrame = sequence.releaseFrame > impactFrame ? sequence.releaseFrame : sequence.FrameCount;
                AddVoice(profile.sustainSound, impactFrame, endFrame, .18f * .55f, false, true);
            }
        }
    }

    public void Sample(float sourceFrame, bool paused)
    {
        samplePaused = paused;
        if (sequence == null || !sequence.IsValid) return;
        if (samplePaused || applicationPaused || Time.timeScale <= 0f)
        {
            ApplyPause(true);
            return;
        }
        float frame = Mathf.Clamp(sourceFrame, 0f, sequence.FrameCount);
        if (frame + .001f < previousFrame)
        {
            for (int index = 0; index < voiceCount; index++)
            {
                voices[index].source.Stop();
                voices[index].started = false;
                voices[index].paused = false;
            }
        }
        previousFrame = frame;
        for (int index = 0; index < voiceCount; index++)
        {
            Voice voice = voices[index];
            if (frame < voice.firstFrame || frame >= voice.lastFrame)
            {
                voice.source.Stop();
                voice.paused = false;
                continue;
            }
            voice.source.pitch = Mathf.Clamp(voice.pitch * Time.timeScale, .01f, 3f);
            float relativeTime = Mathf.Max(0f, PlaybackTime(frame) - voice.firstTime);
            float progress = relativeTime / Mathf.Max(.001f, voice.lastTime - voice.firstTime);
            float audioTime = voice.synchronized ? progress * voice.clip.length
                : relativeTime;
            if (voice.looping) audioTime %= Mathf.Max(.001f, voice.clip.length);
            audioTime = Mathf.Clamp(audioTime, 0f, Mathf.Max(0f, voice.clip.length - 1f / voice.clip.frequency));
            if (!voice.started)
            {
                voice.source.time = audioTime;
                voice.source.Play();
                if (voice.emphasize && !voice.source.mute && voice.source.volume > 0f)
                    SceneBackgroundMusic.EmphasizeBattleSound();
                voice.started = true;
                StartedCount++;
            }
            else if (voice.paused)
            {
                voice.source.UnPause();
            }
            voice.paused = false;
            if (voice.synchronized && voice.source.isPlaying && Mathf.Abs(voice.source.time - audioTime) > .09f)
                voice.source.time = audioTime;
        }
    }

    public void SetPaused(bool paused)
    {
        samplePaused = paused;
        ApplyPause(paused || applicationPaused || Time.timeScale <= 0f);
    }

    public void Clear()
    {
        foreach (Voice voice in voices)
        {
            if (voice.source == null) continue;
            voice.source.Stop();
            voice.source.clip = null;
            voice.clip = null;
            voice.started = false;
            voice.paused = false;
        }
        sequence = null;
        voiceCount = 0;
        previousFrame = -1f;
        samplePaused = false;
    }

    private void AddFeedbackVoice(AudioClip[] clips, int index, float frame, float gain, bool emphasize = false)
    {
        if (clips == null || clips.Length == 0) return;
        AudioClip clip = clips[index % clips.Length];
        if (clip == null) return;
        float lastFrame = FrameAtPlaybackTime(PlaybackTime(frame) + clip.length);
        AddVoice(clip, frame, lastFrame, gain, false, false, emphasize);
    }

    private void AddVoice(AudioClip clip, float firstFrame, float lastFrame, float gain, bool synchronized, bool looping, bool emphasize = false)
    {
        firstFrame = Mathf.Max(0f, firstFrame);
        if (clip == null || lastFrame <= firstFrame) return;
        if (voiceCount == voices.Count)
        {
            GameObject sound = new GameObject("Battle Animation Sound " + voiceCount);
            sound.transform.SetParent(transform, false);
            AudioSource source = sound.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            voices.Add(new Voice { source = source });
        }
        Voice voice = voices[voiceCount++];
        voice.clip = clip;
        voice.source.clip = clip;
        voice.source.loop = looping;
        voice.firstFrame = firstFrame;
        voice.lastFrame = lastFrame;
        voice.firstTime = PlaybackTime(firstFrame);
        voice.lastTime = PlaybackTime(lastFrame);
        voice.synchronized = synchronized;
        voice.looping = looping;
        voice.emphasize = emphasize;
        voice.pitch = synchronized ? clip.length / Mathf.Max(.001f, voice.lastTime - voice.firstTime) : 1f;
        BattleAudioSettingsBridge.RegisterAudioSource(voice.source, false, gain);
    }

    private float PlaybackTime(float frame) => BattleActorAnimation.SourceTime(sequence, frame)
        * duration / Mathf.Max(.01f, sequence.Duration);

    private float FrameAtPlaybackTime(float seconds) => BattleActorAnimation.SourceFrame(sequence,
        Mathf.Clamp(seconds, 0f, duration) * sequence.Duration / duration);

    private void ApplyPause(bool paused)
    {
        for (int index = 0; index < voiceCount; index++)
        {
            Voice voice = voices[index];
            if (!voice.started) continue;
            if (paused && !voice.paused && voice.source.isPlaying)
            {
                voice.source.Pause();
                voice.paused = true;
            }
            else if (!paused && voice.paused)
            {
                voice.source.UnPause();
                voice.paused = false;
            }
        }
    }

    private void Update() => ApplyPause(samplePaused || applicationPaused || Time.timeScale <= 0f);
    private void OnApplicationPause(bool paused)
    {
        applicationPaused = paused;
        ApplyPause(paused || samplePaused || Time.timeScale <= 0f);
    }
    private void OnDisable() => Clear();
    private void OnDestroy()
    {
        Clear();
        foreach (Voice voice in voices)
        {
            BattleAudioSettingsBridge.UnregisterAudioSource(voice.source);
            if (voice.source != null) Destroy(voice.source.gameObject);
        }
        voices.Clear();
    }
}
