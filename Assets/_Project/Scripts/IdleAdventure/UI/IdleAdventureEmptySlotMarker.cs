using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class IdleAdventureEmptySlotMarker : MonoBehaviour
{
    private const float CycleSeconds = 2.4f;
    [SerializeField] private RectTransform floatingPlus;
    [SerializeField] private RectTransform plusShadow;
    private float elapsed;

    public void Initialize(RectTransform plus, RectTransform shadow)
    {
        floatingPlus = plus;
        plusShadow = shadow;
        ApplyPose();
    }

    private void Update()
    {
        Advance(Time.unscaledDeltaTime);
    }

    private void Advance(float deltaTime)
    {
        elapsed = Mathf.Repeat(elapsed + Mathf.Max(0f, deltaTime), CycleSeconds);
        ApplyPose();
    }

    private void ApplyPose()
    {
        float wave = Mathf.Sin(elapsed * Mathf.PI * 2f / CycleSeconds);
        if (floatingPlus != null)
        {
            float offset = wave * 8f / 208f;
            floatingPlus.anchorMin = new Vector2(0f, offset);
            floatingPlus.anchorMax = new Vector2(1f, 1f + offset);
            floatingPlus.offsetMin = Vector2.zero;
            floatingPlus.offsetMax = Vector2.zero;
        }
        if (plusShadow != null) plusShadow.localScale = new Vector3(1f + 3f / 37f * wave, 1f, 1f);
    }
}
