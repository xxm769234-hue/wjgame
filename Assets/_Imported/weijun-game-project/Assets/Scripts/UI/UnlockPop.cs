using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UnlockPop : MonoBehaviour
{
    public TMP_Text tipText;
    public Button unlockBtn;
    public Button closeBtn;
    private MapAreaConfig targetArea;
    private ScrollRect tipScroll;

    private void Awake()
    {
        ConfigureTipLayout();
        closeBtn.onClick.AddListener(() => gameObject.SetActive(false));
        unlockBtn.onClick.AddListener(OnClickUnlock);
    }

    public void SetAreaData(MapAreaConfig areaCfg)
    {
        targetArea = areaCfg;
        tipText.text = MapAreaGenerator.Instance.GetUnlockTip(areaCfg);
        Canvas.ForceUpdateCanvases();
        if (tipScroll != null) tipScroll.verticalNormalizedPosition = 1f;
        bool canUnlock = MapAreaGenerator.Instance.CheckUnlockCondition(areaCfg);
        unlockBtn.interactable = canUnlock;
    }

    void OnClickUnlock()
    {
        MapAreaGenerator.Instance.UnlockWholeArea(targetArea);
        gameObject.SetActive(false);
    }

    private void ConfigureTipLayout()
    {
        if (tipText == null || tipScroll != null) return;
        RectTransform content = tipText.rectTransform;
        GameObject viewportObject = new GameObject("UnlockTipViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(content.parent, false);
        viewport.SetSiblingIndex(content.GetSiblingIndex());
        viewport.anchorMin = content.anchorMin;
        viewport.anchorMax = content.anchorMax;
        viewport.pivot = content.pivot;
        viewport.anchoredPosition = content.anchoredPosition;
        viewport.sizeDelta = content.sizeDelta;
        viewportObject.GetComponent<Image>().color = Color.clear;
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        tipText.font = RuntimeFontAssetProvider.Resolve();
        tipText.fontSize = 20f;
        tipText.fontStyle = FontStyles.Normal;
        tipText.enableAutoSizing = false;
        tipText.enableWordWrapping = true;
        tipText.overflowMode = TextOverflowModes.Overflow;
        tipText.alignment = TextAlignmentOptions.TopLeft;
        tipText.lineSpacing = 3f;
        tipText.raycastTarget = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        tipScroll = viewportObject.GetComponent<ScrollRect>();
        tipScroll.viewport = viewport;
        tipScroll.content = content;
        tipScroll.horizontal = false;
        tipScroll.vertical = true;
        tipScroll.movementType = ScrollRect.MovementType.Clamped;
        tipScroll.scrollSensitivity = 18f;
    }
}
