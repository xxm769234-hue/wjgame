using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleSceneAnimation : MonoBehaviour
{
    private static readonly HashSet<BattleSceneAnimation> Instances = new HashSet<BattleSceneAnimation>();
    [SerializeField] private string environmentKey = "BattleBackground";
    private BattleAnimationSequence sequence;
    private RawImage overlay;
    private float elapsed;
    private bool visible;
    private bool paused;
    private bool applicationPaused;

    public bool IsVisible => visible && overlay != null && overlay.enabled;
    public int CurrentFrame { get; private set; }
    public string EnvironmentKey => environmentKey;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstances() => Instances.Clear();

    public static BattleSceneAnimation Configure(Image target) => ConfigureTarget(target);
    public static BattleSceneAnimation Configure(RawImage target) => ConfigureTarget(target);

    private static BattleSceneAnimation ConfigureTarget(Graphic target)
    {
        if (target == null) return null;
        BattleSceneAnimation animation = target.GetComponent<BattleSceneAnimation>();
        if (animation == null) animation = target.gameObject.AddComponent<BattleSceneAnimation>();
        if (animation.isActiveAndEnabled) Instances.Add(animation);
        animation.EnsureOverlay();
        return animation;
    }

    public static void SetBattleVisible(bool show)
    {
        foreach (BattleSceneAnimation animation in Instances)
        {
            if (animation == null) continue;
            animation.visible = show;
            if (!show)
            {
                animation.elapsed = 0f;
                animation.CurrentFrame = 0;
                animation.sequence = null;
            }
            animation.ApplyFrame();
        }
        SceneBackgroundMusic.SetBattleVisible(show);
    }

    public static void SetPaused(bool value)
    {
        foreach (BattleSceneAnimation animation in Instances)
            if (animation != null) animation.paused = value;
    }

    public void SetEnvironment(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || environmentKey == key) return;
        environmentKey = key;
        sequence = null;
        elapsed = 0f;
        ApplyFrame();
    }

    private void EnsureOverlay()
    {
        if (overlay != null) return;
        GameObject visual = new GameObject("Animated Battle Scenery", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        visual.transform.SetParent(transform, false);
        overlay = visual.GetComponent<RawImage>();
        overlay.raycastTarget = false;
        overlay.rectTransform.anchorMin = Vector2.zero;
        overlay.rectTransform.anchorMax = Vector2.one;
        overlay.rectTransform.offsetMin = Vector2.zero;
        overlay.rectTransform.offsetMax = Vector2.zero;
        overlay.enabled = false;
    }

    private void Update()
    {
        if (!visible) return;
        if (!paused && !applicationPaused) elapsed += Time.deltaTime;
        ApplyFrame();
    }

    private void ApplyFrame()
    {
        EnsureOverlay();
        if (!visible)
        {
            overlay.enabled = false;
            overlay.texture = null;
            return;
        }
        if (sequence == null) sequence = Resources.Load<BattleAnimationSequence>("BattleAnimations/Environment/" + environmentKey);
        if (sequence == null || !sequence.IsValid)
        {
            overlay.enabled = false;
            return;
        }
        float time = elapsed % Mathf.Max(.01f, sequence.Duration);
        int frame = Mathf.Min(sequence.FrameCount - 1, Mathf.FloorToInt(time / sequence.Duration * sequence.FrameCount));
        if (sequence.frameTimes != null && sequence.frameTimes.Length == sequence.FrameCount)
        {
            frame = 0;
            while (frame + 1 < sequence.FrameCount && sequence.frameTimes[frame + 1] <= time) frame++;
        }
        Sprite sprite = sequence.frames[frame];
        CurrentFrame = frame;
        if (sprite == null)
        {
            overlay.enabled = false;
            return;
        }
        Rect textureRect = sprite.textureRect;
        Rect screen = overlay.rectTransform.rect;
        float sourceAspect = textureRect.width / Mathf.Max(1f, textureRect.height);
        float screenAspect = screen.width / Mathf.Max(1f, screen.height);
        float cropWidth = screenAspect < sourceAspect ? screenAspect / sourceAspect : 1f;
        float cropHeight = screenAspect > sourceAspect ? sourceAspect / screenAspect : 1f;
        overlay.texture = sprite.texture;
        overlay.uvRect = new Rect((textureRect.x + textureRect.width * (1f - cropWidth) * .5f) / sprite.texture.width,
            (textureRect.y + textureRect.height * (1f - cropHeight) * .5f) / sprite.texture.height,
            textureRect.width * cropWidth / sprite.texture.width, textureRect.height * cropHeight / sprite.texture.height);
        overlay.color = Color.white;
        overlay.enabled = true;
    }

    private void OnEnable() => Instances.Add(this);
    private void OnDisable()
    {
        Instances.Remove(this);
        visible = false;
        paused = false;
        elapsed = 0f;
        CurrentFrame = 0;
        sequence = null;
        if (overlay != null)
        {
            overlay.enabled = false;
            overlay.texture = null;
        }
    }
    private void OnApplicationPause(bool value) => applicationPaused = value;
    private void OnDestroy()
    {
        Instances.Remove(this);
        if (overlay != null) Destroy(overlay.gameObject);
    }
}
