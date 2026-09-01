using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Creates the editable onboarding UI hierarchy in HomeScene.
/// </summary>
public static class PlayerOnboardingSceneBuilder
{
    private const string HomeScenePath = "Assets/_Project/Scenes/HomeScene.unity";
    private const string FontPath = "Assets/_Imported/weijun-game-project/Assets/Sprites/font/SourceHanSansSC-Normal SDF.asset";

    /// <summary>
    /// Builds missing onboarding nodes without replacing existing scene styling.
    /// </summary>
    [MenuItem("Tools/WeiJun UI/Build Player Onboarding UI")]
    public static void BuildHomeSceneOnboardingUi()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != HomeScenePath)
        {
            scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
        }

        GameObject managerObject = FindRoot(scene, "PlayerOnboardingManager");
        if (managerObject == null)
        {
            managerObject = new GameObject("PlayerOnboardingManager");
            managerObject.AddComponent<PlayerOnboardingManager>();
            SceneManager.MoveGameObjectToScene(managerObject, scene);
        }

        GameObject canvasObject = GetOrCreateChild(managerObject.transform, "PlayerOnboardingCanvas", out _);
        Canvas canvas = EnsureComponent<Canvas>(canvasObject);
        CanvasScaler scaler = EnsureComponent<CanvasScaler>(canvasObject);
        EnsureComponent<GraphicRaycaster>(canvasObject);
        if (canvasObject.transform.childCount == 0)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32760;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        BuildOverlay(canvasObject.transform, fontAsset);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = canvasObject;
        Debug.Log("PlayerOnboardingCanvas now contains editable onboarding UI nodes.");
    }

    private static void BuildOverlay(Transform canvasTransform, TMP_FontAsset fontAsset)
    {
        bool overlayCreated;
        GameObject overlay = GetOrCreateChild(canvasTransform, "Overlay", out overlayCreated);
        Image overlayImage = EnsureComponent<Image>(overlay);
        if (overlayCreated)
        {
            SetStretch(overlay.GetComponent<RectTransform>());
            overlayImage.color = new Color(0.03f, 0.04f, 0.05f, 0.64f);
            overlayImage.raycastTarget = true;
            overlay.SetActive(false);
        }

        bool panelCreated;
        GameObject panel = GetOrCreateChild(overlay.transform, "Panel", out panelCreated);
        Image panelImage = EnsureComponent<Image>(panel);
        if (panelCreated)
        {
            SetCentered(panel.GetComponent<RectTransform>(), Vector2.zero, new Vector2(760f, 410f));
            panelImage.color = new Color(0.97f, 0.92f, 0.81f, 1f);
            panelImage.raycastTarget = false;
        }

        bool unusedCreated;
        BuildText(panel.transform, "Title", fontAsset, 30f, TextAlignmentOptions.Center,
            new Vector2(24f, 330f), new Vector2(736f, 386f), out unusedCreated);
        BuildText(panel.transform, "Step", fontAsset, 16f, TextAlignmentOptions.Right,
            new Vector2(24f, 294f), new Vector2(736f, 328f), out unusedCreated);
        TMP_Text content = BuildText(panel.transform, "Content", fontAsset, 21f, TextAlignmentOptions.TopLeft,
            new Vector2(38f, 166f), new Vector2(722f, 286f), out unusedCreated);
        content.enableWordWrapping = true;
        TMP_Text target = BuildText(panel.transform, "Target", fontAsset, 16f, TextAlignmentOptions.TopLeft,
            new Vector2(38f, 100f), new Vector2(722f, 158f), out unusedCreated);
        target.enableWordWrapping = true;
        TMP_Text state = BuildText(panel.transform, "State", fontAsset, 15f, TextAlignmentOptions.Center,
            new Vector2(38f, 62f), new Vector2(722f, 98f), out unusedCreated);
        state.enableWordWrapping = true;

        BuildButton(panel.transform, "AcknowledgeButton", "完成当前步骤", new Vector2(-112f, 24f), fontAsset);
        BuildButton(panel.transform, "SkipButton", "跳过引导", new Vector2(112f, 24f), fontAsset);

        bool highlightCreated;
        GameObject highlight = GetOrCreateChild(canvasTransform, "TargetHighlight", out highlightCreated);
        Image highlightImage = EnsureComponent<Image>(highlight);
        Outline highlightOutline = EnsureComponent<Outline>(highlight);
        if (highlightCreated)
        {
            RectTransform highlightRect = highlight.GetComponent<RectTransform>();
            SetCentered(highlightRect, Vector2.zero, new Vector2(120f, 80f));
            highlightImage.color = new Color(1f, 0.84f, 0.24f, 0.12f);
            highlightImage.raycastTarget = false;
            highlightOutline.effectColor = new Color(1f, 0.84f, 0.24f, 0.95f);
            highlightOutline.effectDistance = new Vector2(4f, 4f);
            highlight.SetActive(false);
        }

        bool arrowCreated;
        TMP_Text arrow = BuildText(canvasTransform, "TargetArrow", fontAsset, 32f, TextAlignmentOptions.Center,
            Vector2.zero, new Vector2(60f, 60f), out arrowCreated);
        if (arrowCreated)
        {
            arrow.color = new Color(1f, 0.84f, 0.24f, 1f);
            arrow.text = "^";
            arrow.gameObject.SetActive(false);
        }
    }

    private static TMP_Text BuildText(
        Transform parent,
        string objectName,
        TMP_FontAsset fontAsset,
        float fontSize,
        TextAlignmentOptions alignment,
        Vector2 position,
        Vector2 size,
        out bool created)
    {
        GameObject textObject = GetOrCreateChild(parent, objectName, out created);
        TMP_Text text = EnsureComponent<TextMeshProUGUI>(textObject);
        if (text.font == null && fontAsset != null)
        {
            text.font = fontAsset;
        }

        if (created)
        {
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.16f, 0.12f, 0.09f, 1f);
            text.raycastTarget = false;
            SetRect(text.rectTransform, position, size);
        }

        return text;
    }

    private static void BuildButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 position,
        TMP_FontAsset fontAsset)
    {
        bool buttonCreated;
        GameObject buttonObject = GetOrCreateChild(parent, objectName, out buttonCreated);
        Image image = EnsureComponent<Image>(buttonObject);
        Button button = EnsureComponent<Button>(buttonObject);
        if (button.targetGraphic == null)
        {
            button.targetGraphic = image;
        }

        if (buttonCreated)
        {
            image.color = new Color(0.25f, 0.48f, 0.62f, 1f);
            SetCentered(buttonObject.GetComponent<RectTransform>(), position, new Vector2(190f, 52f));
        }

        bool labelCreated;
        TMP_Text labelText = BuildText(buttonObject.transform, "Label", fontAsset, 17f,
            TextAlignmentOptions.Center, Vector2.zero, new Vector2(190f, 52f), out labelCreated);
        if (buttonCreated || labelCreated)
        {
            labelText.text = label;
            labelText.color = Color.white;
            SetStretch(labelText.rectTransform);
        }
    }

    private static GameObject GetOrCreateChild(Transform parent, string objectName, out bool created)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            created = false;
            return existing.gameObject;
        }

        GameObject createdObject = new GameObject(objectName, typeof(RectTransform));
        createdObject.transform.SetParent(parent, false);
        created = true;
        return createdObject;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static GameObject FindRoot(Scene scene, string objectName)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].name == objectName)
            {
                return roots[i];
            }
        }

        return null;
    }

    private static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetCentered(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = size - position;
    }

}
