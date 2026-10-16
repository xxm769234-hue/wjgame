using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class ReturnToHomeButton : MonoBehaviour
{
    [SerializeField] private string homeSceneName = "HomeScene";
    [SerializeField] private string buttonLabel = "Return Home";
    [SerializeField] private Color buttonColor = new Color(0.18f, 0.26f, 0.34f, 0.96f);

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.RemoveListener(ReturnToHome);
        button.onClick.AddListener(ReturnToHome);
        ConfigureAppearance();
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(ReturnToHome);
    }

    public void ReturnToHome()
    {
        if (BuildManager.Instance != null) BuildManager.Instance.ExitBuild();

        Time.timeScale = 1f;
        SceneManager.LoadScene(homeSceneName);
    }

    private void ConfigureAppearance()
    {
        Image background = GetComponent<Image>();
        if (background != null) background.color = buttonColor;

        TMP_Text label = GetComponentInChildren<TMP_Text>(true);
        if (label == null)
        {
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 4f);
            labelRect.offsetMax = new Vector2(-8f, -4f);
            label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = RuntimeFontAssetProvider.Resolve();
            label.fontSize = 20f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
        }

        if (label.font == null) label.font = RuntimeFontAssetProvider.Resolve();
        label.text = buttonLabel;
        label.raycastTarget = false;
    }
}
