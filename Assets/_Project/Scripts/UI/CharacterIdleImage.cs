using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class CharacterIdleImage : MonoBehaviour
{
    private Image target;
    private CharacterIdleSequence sequence;
    private float elapsed;
    private int frameIndex;
    public BattleActorAnimation BattlePlayback { private get; set; }

    public int FrameIndex => frameIndex;
    public CharacterIdleSequence Sequence => sequence;

    public static bool TryApply(Image image, params string[] keys)
    {
        if (image == null) return false;
        CharacterAppearanceCatalog.Entry appearance = CharacterAppearanceCatalog.Find(keys);
        if (appearance == null)
        {
            Stop(image);
            return false;
        }
        CharacterIdleSequence animation = appearance.LoadIdle();
        if (animation != null && animation.FrameCount > 0)
        {
            CharacterIdleImage player = image.GetComponent<CharacterIdleImage>();
            if (player == null) player = image.gameObject.AddComponent<CharacterIdleImage>();
            player.target = image;
            if (player.sequence != animation)
            {
                player.sequence = animation;
                player.elapsed = 0f;
                player.frameIndex = 0;
            }
            player.enabled = true;
            image.sprite = animation.FrameAt(player.frameIndex);
        }
        else
        {
            Stop(image);
            image.sprite = appearance.portrait;
        }
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        return image.sprite != null;
    }

    public static void Stop(Image image)
    {
        if (image == null) return;
        CharacterIdleImage player = image.GetComponent<CharacterIdleImage>();
        if (player == null) return;
        player.enabled = false;
        player.BattlePlayback = null;
        player.sequence = null;
        player.elapsed = 0f;
        player.frameIndex = 0;
    }

    private void Update()
    {
        if (target == null || !target.enabled) return;
        if (BattlePlayback != null && (BattlePlayback.Paused || BattlePlayback.HitStopped || BattlePlayback.IsPlayingDeath || BattlePlayback.IsDead)) return;
        Advance(Time.unscaledDeltaTime);
    }

    private void Advance(float deltaTime)
    {
        if (sequence == null || sequence.FrameCount == 0 || deltaTime <= 0f) return;
        elapsed = Mathf.Repeat(elapsed + deltaTime, sequence.Duration);
        int nextFrame = Mathf.Min(sequence.FrameCount - 1, Mathf.FloorToInt(elapsed * sequence.FrameRate));
        if (nextFrame == frameIndex) return;
        frameIndex = nextFrame;
        target.sprite = sequence.FrameAt(frameIndex);
    }
}
