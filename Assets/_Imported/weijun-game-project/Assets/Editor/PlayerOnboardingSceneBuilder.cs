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
    private const string PanelSpriteGuid = "1330cb692bd507042a822abb84771fe2";
    private const string ButtonSpriteGuid = "0c1bfa941944bbd4f8a78d2366953989";
    private const string GuideIconGuid = "5dd2be4235203da4dae0ee48025a9fff";
    private const string TargetFrameGuid = "e02a81ed0bb168a44afeb8fc89281b5e";

    /// <summary>
    /// Builds missing onboarding nodes without replacing existing scene styling.
    /// </summary>
    [MenuItem("Tools/WeiJun UI/Build Player Onboarding UI")]
    public static void BuildHomeSceneOnboardingUi()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != HomeScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
        }

        GameObject managerObject = FindRoot(scene, "PlayerOnboardingManager");
        if (managerObject == null)
        {
            managerObject = new GameObject("PlayerOnboardingManager");
            SceneManager.MoveGameObjectToScene(managerObject, scene);
        }
        PlayerOnboardingManager manager = EnsureComponent<PlayerOnboardingManager>(managerObject);
        SerializedObject serializedManager = new SerializedObject(manager);
        EnsureSpriteReference(serializedManager, "panelBackground", PanelSpriteGuid);
        EnsureSpriteReference(serializedManager, "actionButtonBackground", ButtonSpriteGuid);
        EnsureSpriteReference(serializedManager, "guideIcon", GuideIconGuid);
        EnsureSpriteReference(serializedManager, "targetFrame", TargetFrameGuid);
        serializedManager.ApplyModifiedPropertiesWithoutUndo();

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
            SetCentered(panel.GetComponent<RectTransform>(), Vector2.zero, new Vector2(800f, 510f));
            panelImage.sprite = LoadSprite(PanelSpriteGuid);
            panelImage.color = Color.white;
            panelImage.raycastTarget = true;
            GameObject icon = GetOrCreateChild(panel.transform, "GuideIcon", out _);
            Image iconImage = EnsureComponent<Image>(icon);
            iconImage.sprite = LoadSprite(GuideIconGuid);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            SetRect(icon.GetComponent<RectTransform>(), new Vector2(96f, 400f), new Vector2(160f, 464f));
        }

        bool unusedCreated;
        TMP_Text title = BuildText(panel.transform, "Title", fontAsset, 30f, TextAlignmentOptions.Center,
            new Vector2(176f, 404f), new Vector2(656f, 456f), out unusedCreated);
        if (unusedCreated)
        {
            title.text = "新手引导";
            title.enableAutoSizing = true;
            title.fontSizeMin = 20f;
            title.fontSizeMax = 30f;
        }
        TMP_Text step = BuildText(panel.transform, "Step", fontAsset, 20f, TextAlignmentOptions.Center,
            new Vector2(110f, 344f), new Vector2(690f, 374f), out unusedCreated);
        if (unusedCreated)
        {
            step.text = "第 1 步";
            step.color = new Color(0.20f, 0.39f, 0.43f, 1f);
        }
        TMP_Text content = BuildText(panel.transform, "Content", fontAsset, 25f, TextAlignmentOptions.TopLeft,
            new Vector2(110f, 206f), new Vector2(690f, 332f), out unusedCreated);
        if (unusedCreated)
        {
            content.text = "欢迎来到你的领地！\n从第一块土地开始，建造属于你的家园。";
            content.enableWordWrapping = true;
            content.enableAutoSizing = true;
            content.fontSizeMin = 20f;
            content.fontSizeMax = 25f;
            content.overflowMode = TextOverflowModes.Ellipsis;
        }
        BuildText(panel.transform, "Target", fontAsset, 18f, TextAlignmentOptions.TopLeft,
            new Vector2(110f, 180f), new Vector2(690f, 202f), out unusedCreated);
        TMP_Text state = BuildText(panel.transform, "State", fontAsset, 20f, TextAlignmentOptions.Center,
            new Vector2(110f, 132f), new Vector2(690f, 176f), out unusedCreated);
        if (unusedCreated)
        {
            state.text = "准备好后，继续下一步";
            state.color = new Color(0.42f, 0.33f, 0.24f, 1f);
            state.enableWordWrapping = true;
        }

        BuildButton(panel.transform, "AcknowledgeButton", "开始旅程", new Vector2(140f, 82f), fontAsset);
        BuildButton(panel.transform, "SkipButton", "跳过引导", new Vector2(-140f, 82f), fontAsset);

        bool highlightCreated;
        GameObject highlight = GetOrCreateChild(canvasTransform, "TargetHighlight", out highlightCreated);
        Image highlightImage = EnsureComponent<Image>(highlight);
        Outline highlightOutline = EnsureComponent<Outline>(highlight);
        if (highlightCreated)
        {
            RectTransform highlightRect = highlight.GetComponent<RectTransform>();
            SetCentered(highlightRect, Vector2.zero, new Vector2(120f, 80f));
            highlightImage.sprite = LoadSprite(TargetFrameGuid);
            highlightImage.color = Color.white;
            highlightImage.raycastTarget = false;
            highlightOutline.enabled = false;
            highlightOutline.effectColor = new Color(1f, 0.84f, 0.24f, 0.95f);
            highlightOutline.effectDistance = new Vector2(4f, 4f);
            highlight.SetActive(false);
        }

        bool arrowCreated;
        TMP_Text arrow = BuildText(canvasTransform, "TargetArrow", fontAsset, 32f, TextAlignmentOptions.Center,
            Vector2.zero, new Vector2(60f, 60f), out arrowCreated);
        if (arrowCreated)
        {
            arrow.color = new Color(0.20f, 0.39f, 0.43f, 1f);
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
            text.color = new Color(0.25f, 0.20f, 0.16f, 1f);
            text.richText = false;
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
            image.sprite = LoadSprite(ButtonSpriteGuid);
            image.color = objectName == "SkipButton" ? new Color(0.86f, 0.90f, 0.91f, 1f) : Color.white;
            SetCentered(buttonObject.GetComponent<RectTransform>(), position, new Vector2(252f, 76f));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
        }

        bool labelCreated;
        TMP_Text labelText = BuildText(buttonObject.transform, "Label", fontAsset, 23f,
            TextAlignmentOptions.Center, Vector2.zero, new Vector2(252f, 76f), out labelCreated);
        if (buttonCreated || labelCreated)
        {
            labelText.text = label;
            labelText.color = new Color(1f, 0.96f, 0.85f, 1f);
            SetStretch(labelText.rectTransform);
            labelText.margin = new Vector4(24f, 8f, 24f, 8f);
            labelText.enableWordWrapping = false;
        }
    }

    private static Sprite LoadSprite(string guid)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
    }

    private static void EnsureSpriteReference(SerializedObject target, string propertyName, string guid)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null && property.objectReferenceValue == null)
            property.objectReferenceValue = LoadSprite(guid);
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
