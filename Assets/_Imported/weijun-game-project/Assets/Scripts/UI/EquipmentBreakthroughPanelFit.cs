using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public sealed class EquipmentBreakthroughPanelFit : MonoBehaviour
{
    private RectTransform rectangle;

    private void OnEnable()
    {
        Fit();
    }

    private void LateUpdate()
    {
        Fit();
    }

    public void Fit()
    {
        if (rectangle == null) rectangle = GetComponent<RectTransform>();
        RectTransform parent = rectangle.parent as RectTransform;
        if (parent == null || parent.rect.width <= 0f || parent.rect.height <= 0f) return;
        float scale = Mathf.Min(1f, (parent.rect.width - 24f) / 1280f, (parent.rect.height - 40f) / 850f);
        rectangle.localScale = Vector3.one * Mathf.Max(.01f, scale);
    }
}
