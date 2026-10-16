using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
public sealed class LoginCanvasView : MonoBehaviour
{
    [SerializeField] private Canvas loginCanvas;
    [SerializeField] private CanvasScaler canvasScaler;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private LoginCanvasStyle style;
    [SerializeField] private LoginContentView contentView;

    public LoginCanvasStyle Style => contentView != null && contentView.Style != null ? contentView.Style : style;
    public LoginContentView ContentView => contentView;

    public RectTransform PrepareContentRoot()
    {
        EnsureReferences();
        LoginCanvasStyle activeStyle = Style;
        if (activeStyle != null)
        {
            activeStyle.ConfigureCanvas(loginCanvas, canvasScaler);
        }

        if (contentRoot == null)
        {
            GameObject content = new GameObject("LoginContent", typeof(RectTransform));
            content.transform.SetParent(transform, false);
            contentRoot = content.GetComponent<RectTransform>();
            Stretch(contentRoot);
        }

        return contentRoot;
    }

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnValidate()
    {
        EnsureReferences();
        LoginCanvasStyle activeStyle = Style;
        if (activeStyle != null)
        {
            activeStyle.ConfigureCanvas(loginCanvas, canvasScaler);
        }
    }

    private void EnsureReferences()
    {
        if (loginCanvas == null)
        {
            loginCanvas = GetComponent<Canvas>();
        }

        if (canvasScaler == null)
        {
            canvasScaler = GetComponent<CanvasScaler>();
        }

        if (style == null)
        {
            style = GetComponent<LoginCanvasStyle>();
        }

        if (contentView == null && contentRoot != null)
        {
            contentView = contentRoot.GetComponent<LoginContentView>();
        }

        if (contentRoot == null)
        {
            Transform content = transform.Find("LoginContent");
            contentRoot = content != null ? content.GetComponent<RectTransform>() : null;
            if (contentView == null && contentRoot != null)
            {
                contentView = contentRoot.GetComponent<LoginContentView>();
            }
        }
    }

    public void ClearContentForRuntimeFallback()
    {
        if (contentRoot == null)
        {
            return;
        }

        for (int childIndex = contentRoot.childCount - 1; childIndex >= 0; childIndex--)
        {
            Destroy(contentRoot.GetChild(childIndex).gameObject);
        }
    }

    private static void Stretch(RectTransform target)
    {
        target.anchorMin = Vector2.zero;
        target.anchorMax = Vector2.one;
        target.offsetMin = Vector2.zero;
        target.offsetMax = Vector2.zero;
    }

#if UNITY_EDITOR
    [ContextMenu("Capture Login Canvas References")]
    private void EditorCaptureReferences()
    {
        EnsureReferences();
        Transform content = transform.Find("LoginContent");
        contentRoot = content != null ? content.GetComponent<RectTransform>() : null;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
