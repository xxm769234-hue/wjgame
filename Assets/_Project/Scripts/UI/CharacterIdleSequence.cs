using UnityEngine;

public sealed class CharacterIdleSequence : ScriptableObject
{
    [SerializeField] private float frameRate = 24f;
    [SerializeField] private Sprite[] frames;

    public float FrameRate => Mathf.Max(1f, frameRate);
    public int FrameCount => frames != null ? frames.Length : 0;
    public float Duration => FrameCount / FrameRate;

    public Sprite FrameAt(int index)
    {
        return FrameCount > 0 ? frames[Mathf.Clamp(index, 0, FrameCount - 1)] : null;
    }
}
