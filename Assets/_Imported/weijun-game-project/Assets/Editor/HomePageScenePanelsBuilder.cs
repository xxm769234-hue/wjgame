using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;
using Font = TMPro.TMP_FontAsset;
using InputField = TMPro.TMP_InputField;
using LegacyText = UnityEngine.UI.Text;

public static class HomePageScenePanelsBuilder
{
    private const string HomeScenePath = "Assets/_Project/Scenes/HomeScene.unity";
    private const string CanvasName = "HomePanelsCanvas";
    private const string InventoryPrefabPath = "Assets/Resources/UI/InventoryPanel.prefab";
    private const string HomeBackgroundPath = "Assets/_Imported/weijun-game-project/Assets/UI/背景.png";
    private const string TestNodeName = "测试节点";
    private const int MinimumComponentizedBookSchemaVersion = 8;
    private const int ScenePanelsSchemaVersion = 12;
    private const int EquipmentRows = 6;
    private const string RoleFontPath = "Assets/_Imported/weijun-game-project/Assets/Sprites/font/SourceHanSansSC-Normal SDF.asset";
    private const string ProfileFontPath = "Assets/_Imported/weijun-game-project/Assets/Sprites/font/SourceHanSansSC-Normal SDF.asset";
    private const int RoleListRows = 20;
    private const string FontPath = "Assets/_Imported/weijun-game-project/Assets/Sprites/font/SourceHanSansSC-Normal SDF.asset";

    private static Font defaultFont;
    private static Font fallbackFont;
    private static TMP_FontAsset roleFontAsset;

    [InitializeOnLoadMethod]
    private static void BuildMissingPanelsAfterReload()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            string sessionKey = "WeiJun.HomeScenePanels.SchemaCheck." + ScenePanelsSchemaVersion;
            if (SessionState.GetBool(sessionKey, false))
            {
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(HomeScenePath);
            bool openedForCheck = !scene.IsValid() || !scene.isLoaded;
            if (openedForCheck)
            {
                scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Additive);
            }

            try
            {
                bool addedTestNode = EnsureTestNode(scene);
                bool addedAccountProfileDetails = false;
                GameObject canvasObject = FindRoot(scene, CanvasName);
                HomePageScenePanels registry = canvasObject != null
                    ? canvasObject.GetComponent<HomePageScenePanels>()
                    : null;
                if (registry != null && registry.SchemaVersion < 2)
                {
                    RemoveLegacyHomeCanvas(scene);
                }

                if (registry != null &&
                    registry.SchemaVersion >= MinimumComponentizedBookSchemaVersion &&
                    registry.SchemaVersion < ScenePanelsSchemaVersion)
                {
                    if (registry.Get(HomePageScenePanelType.Role) == null)
                    {
                        roleFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProfileFontPath);
                        HomePageScenePanel rolePanel = BuildRolePanel(canvasObject.transform);
                        rolePanel.SetVisible(false);
                        registry.EditorAppendPanel(rolePanel, ScenePanelsSchemaVersion);
                    }
                    else
                    {
                        registry.EditorSetSchemaVersion(ScenePanelsSchemaVersion);
                    }
                    EditorUtility.SetDirty(registry);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                else if (registry == null || registry.SchemaVersion != ScenePanelsSchemaVersion)
                {
                    BuildScenePanels(scene, true);
                }
                else if (addedTestNode)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }

                if (registry != null && registry.SchemaVersion == ScenePanelsSchemaVersion)
                {
                    Transform profileRoot = canvasObject == null
                        ? null
                        : FindTransformByName(canvasObject.transform, "AccountProfileScenePanel");
                    addedAccountProfileDetails = EnsureAccountProfileDetails(profileRoot);
                }

                if (addedAccountProfileDetails)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }

                string lotteryLayoutKey = "WeiJun.HomeScenePanels.LotteryLayoutCheck.1";
                if (registry != null
                    && registry.SchemaVersion >= MinimumComponentizedBookSchemaVersion
                    && registry.SchemaVersion == ScenePanelsSchemaVersion
                    && !SessionState.GetBool(lotteryLayoutKey, false)
                    && !HasLotteryLayout(registry))
                {
                    ReplaceLotteryPanelInScene(scene, canvasObject, registry);
                }
                SessionState.SetBool(lotteryLayoutKey, true);

                SessionState.SetBool(sessionKey, true);
            }
            finally
            {
                if (openedForCheck && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        };
    }

    [MenuItem("Tools/WeiJun UI/Rebuild Home Scene Panels")]
    public static void RebuildActiveHomeScenePanels()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != HomeScenePath)
        {
            scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
        }

        BuildScenePanels(scene, true);
    }

    [MenuItem("Tools/WeiJun UI/Rebuild Lottery Panel")]
    public static void RebuildLotteryPanel()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != HomeScenePath)
        {
            scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
        }

        GameObject canvasObject = FindRoot(scene, CanvasName);
        if (canvasObject == null)
        {
            Debug.LogError("HomePanelsCanvas was not found. Rebuild the home scene panels first.");
            return;
        }

        HomePageScenePanels registry = canvasObject.GetComponent<HomePageScenePanels>();
        if (registry == null)
        {
            Debug.LogError("HomePanelsCanvas is missing HomePageScenePanels.");
            return;
        }

        HomePageScenePanel existing = registry.Get(HomePageScenePanelType.Lottery);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        roleFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProfileFontPath);
        HomePageScenePanel panel = BuildLotteryPanel(canvasObject.transform);
        panel.SetVisible(false);
        registry.EditorReplacePanel(panel, registry.SchemaVersion);
        EditorUtility.SetDirty(panel);
        EditorUtility.SetDirty(registry);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = panel.gameObject;
        Debug.Log("Lottery panel rebuilt as an editable horizontal Unity UI panel.");
    }

    private static bool HasLotteryLayout(HomePageScenePanels registry)
    {
        HomePageScenePanel lottery = registry != null ? registry.Get(HomePageScenePanelType.Lottery) : null;
        return lottery != null && lottery.transform.Find("LotteryPoolPanel") != null;
    }

    private static HomePageScenePanel ReplaceLotteryPanelInScene(Scene scene, GameObject canvasObject, HomePageScenePanels registry)
    {
        if (canvasObject == null || registry == null)
        {
            return null;
        }

        HomePageScenePanel existing = registry.Get(HomePageScenePanelType.Lottery);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        roleFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProfileFontPath);
        HomePageScenePanel panel = BuildLotteryPanel(canvasObject.transform);
        panel.SetVisible(false);
        registry.EditorReplacePanel(panel, registry.SchemaVersion);
        EditorUtility.SetDirty(panel);
        EditorUtility.SetDirty(registry);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return panel;
    }

    [MenuItem("Tools/WeiJun UI/Add Role Panel")]
    public static void AddRolePanelToHomeScene()
    {
        Scene scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
        GameObject canvasObject = FindRoot(scene, CanvasName);
        if (canvasObject == null)
        {
            BuildScenePanels(scene, true);
            return;
        }

        HomePageScenePanels registry = canvasObject.GetComponent<HomePageScenePanels>();
        if (registry == null)
        {
            Debug.LogError("HomePanelsCanvas is missing HomePageScenePanels.");
            return;
        }

        if (registry.Get(HomePageScenePanelType.Role) == null)
        {
            roleFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProfileFontPath);
            HomePageScenePanel rolePanel = BuildRolePanel(canvasObject.transform);
            rolePanel.SetVisible(false);
            registry.EditorAppendPanel(rolePanel, ScenePanelsSchemaVersion);
            EditorUtility.SetDirty(rolePanel);
        }
        else
        {
            registry.EditorSetSchemaVersion(ScenePanelsSchemaVersion);
        }

        EditorUtility.SetDirty(registry);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = canvasObject;
        Debug.Log("Added editable RoleScenePanel to HomeScene.");
    }

    public static void BuildHomeScene()
    {
        Scene scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
        BuildScenePanels(scene, true);
    }

    [MenuItem("Tools/WeiJun UI/Update Account Profile Panel")]
    public static void UpdateAccountProfilePanel()
    {
        Scene scene = SceneManager.GetSceneByPath(HomeScenePath);
        bool openedForUpdate = !scene.IsValid() || !scene.isLoaded;
        if (openedForUpdate)
        {
            scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Additive);
        }

        try
        {
            GameObject canvasObject = FindRoot(scene, CanvasName);
            Transform profileRoot = canvasObject == null
                ? null
                : FindTransformByName(canvasObject.transform, "AccountProfileScenePanel");
            if (profileRoot == null)
            {
                Debug.LogError("AccountProfileScenePanel was not found in HomeScene.");
                return;
            }

            string[] removedObjects =
            {
                "ProfileDisplayName",
                "ProfileResourceTitle",
                "ProfileGoldCard",
                "ProfileWoodCard",
                "ProfileStoneCard",
                "ProfileIronCard",
                "ProfileExploreStatCard"
            };
            for (int i = 0; i < removedObjects.Length; i++)
            {
                Transform target = FindTransformByName(profileRoot, removedObjects[i]);
                if (target != null)
                {
                    Object.DestroyImmediate(target.gameObject);
                }
            }

            Transform identifier = FindTransformByName(profileRoot, "ProfileIdentifier");
            if (identifier != null)
            {
                TMP_Text identifierText = identifier.GetComponent<TMP_Text>();
                if (identifierText != null)
                {
                    identifierText.text = "玩家ID  WJ-00000000";
                }
                else
                {
                    LegacyText legacyIdentifier = identifier.GetComponent<LegacyText>();
                    if (legacyIdentifier != null)
                    {
                        legacyIdentifier.text = "玩家ID  WJ-00000000";
                    }
                }

                RectTransform identifierRect = identifier.GetComponent<RectTransform>();
                if (identifierRect != null)
                {
                    identifierRect.sizeDelta = new Vector2(170f, identifierRect.sizeDelta.y);
                }
            }

            Transform copyButton = FindTransformByName(profileRoot, "ProfileCopyIdButton");
            if (copyButton != null)
            {
                SetProfileButtonText(copyButton, "复制玩家ID");
                RectTransform copyRect = copyButton.GetComponent<RectTransform>();
                if (copyRect != null)
                {
                    copyRect.anchoredPosition = new Vector2(204f, -386f);
                    copyRect.sizeDelta = new Vector2(156f, 38f);
                }
            }

            Transform nicknameInput = FindTransformByName(profileRoot, "ProfileNicknameInput");
            TMP_FontAsset profileFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/_Imported/weijun-game-project/Assets/Sprites/font/SourceHanSansSC-Normal SDF.asset");
            ConvertProfileTextComponents(profileRoot, nicknameInput, profileFont);
            EnsureAccountProfileDetails(profileRoot);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Updated the account profile panel in HomeScene.");
        }
        finally
        {
            if (openedForUpdate && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    [MenuItem("Tools/WeiJun UI/Add Account Profile Details")]
    public static void AddAccountProfileDetails()
    {
        Scene scene = SceneManager.GetSceneByPath(HomeScenePath);
        bool openedForUpdate = !scene.IsValid() || !scene.isLoaded;
        if (openedForUpdate)
        {
            scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Additive);
        }

        try
        {
            GameObject canvasObject = FindRoot(scene, CanvasName);
            Transform profileRoot = canvasObject == null
                ? null
                : FindTransformByName(canvasObject.transform, "AccountProfileScenePanel");
            if (profileRoot == null)
            {
                Debug.LogWarning("AccountProfileScenePanel was not found in HomeScene.");
                return;
            }

            if (EnsureAccountProfileDetails(profileRoot))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally
        {
            if (openedForUpdate && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static bool EnsureAccountProfileDetails(Transform profileRoot)
    {
        if (profileRoot == null || FindTransformByName(profileRoot, "ProfileDetailsCard") != null)
        {
            return false;
        }

        Transform panel = FindTransformByName(profileRoot, "ProfilePanel");
        if (panel == null)
        {
            Debug.LogWarning("ProfilePanel was not found while adding account profile details.");
            return false;
        }

        BuildProfileDetailsCard(panel);
        return true;
    }

    private static void ConvertProfileTextComponents(Transform profileRoot, Transform excludedRoot, TMP_FontAsset fontAsset)
    {
        LegacyText[] legacyTexts = profileRoot.GetComponentsInChildren<LegacyText>(true);
        for (int i = 0; i < legacyTexts.Length; i++)
        {
            LegacyText legacy = legacyTexts[i];
            if (legacy == null || (excludedRoot != null && (legacy.transform == excludedRoot || legacy.transform.IsChildOf(excludedRoot))))
            {
                continue;
            }

            GameObject target = legacy.gameObject;
            string value = legacy.text;
            int fontSize = legacy.fontSize;
            FontStyle fontStyle = legacy.fontStyle;
            Color color = legacy.color;
            TextAnchor alignment = legacy.alignment;
            bool enabled = legacy.enabled;
            bool raycastTarget = legacy.raycastTarget;
            bool bestFit = legacy.resizeTextForBestFit;
            int minSize = legacy.resizeTextMinSize;
            int maxSize = legacy.resizeTextMaxSize;
            bool richText = legacy.supportRichText;
            HorizontalWrapMode horizontalOverflow = legacy.horizontalOverflow;
            VerticalWrapMode verticalOverflow = legacy.verticalOverflow;

            Object.DestroyImmediate(legacy);
            TextMeshProUGUI replacement = target.AddComponent<TextMeshProUGUI>();
            replacement.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            replacement.text = value ?? string.Empty;
            replacement.fontSize = Mathf.Max(1, fontSize);
            replacement.fontStyle = ConvertFontStyle(fontStyle);
            replacement.color = color;
            replacement.alignment = ConvertTextAlignment(alignment);
            replacement.enableAutoSizing = bestFit;
            replacement.fontSizeMin = Mathf.Max(1, minSize);
            replacement.fontSizeMax = Mathf.Max(replacement.fontSizeMin, maxSize);
            replacement.enableWordWrapping = horizontalOverflow == HorizontalWrapMode.Wrap;
            replacement.overflowMode = verticalOverflow == VerticalWrapMode.Overflow
                ? TextOverflowModes.Overflow
                : TextOverflowModes.Truncate;
            replacement.richText = richText;
            replacement.raycastTarget = raycastTarget;
            replacement.enabled = enabled;
            EditorUtility.SetDirty(replacement);
        }
    }

    private static void SetProfileButtonText(Transform buttonRoot, string value)
    {
        TMP_Text tmp = buttonRoot.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = value;
            return;
        }

        LegacyText legacy = buttonRoot.GetComponentInChildren<LegacyText>(true);
        if (legacy != null)
        {
            legacy.text = value;
        }
    }

    private static FontStyles ConvertFontStyle(FontStyle style)
    {
        switch (style)
        {
            case FontStyle.Bold:
                return FontStyles.Bold;
            case FontStyle.Italic:
                return FontStyles.Italic;
            case FontStyle.BoldAndItalic:
                return FontStyles.Bold | FontStyles.Italic;
            default:
                return FontStyles.Normal;
        }
    }

    private static TextAlignmentOptions ConvertTextAlignment(TextAnchor alignment)
    {
        switch (alignment)
        {
            case TextAnchor.UpperLeft:
                return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter:
                return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight:
                return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft:
                return TextAlignmentOptions.MidlineLeft;
            case TextAnchor.MiddleCenter:
                return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight:
                return TextAlignmentOptions.MidlineRight;
            case TextAnchor.LowerLeft:
                return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter:
                return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight:
                return TextAlignmentOptions.BottomRight;
            default:
                return TextAlignmentOptions.TopLeft;
        }
    }

    private static void BuildScenePanels(Scene scene, bool saveScene)
    {
        fallbackFont = AssetDatabase.LoadAssetAtPath<Font>(ProfileFontPath) ?? RuntimeFontAssetProvider.Resolve();
        defaultFont = AssetDatabase.LoadAssetAtPath<Font>(ProfileFontPath) ?? fallbackFont;
        roleFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProfileFontPath);
        EnsureTestNode(scene);

        GameObject existing = FindRoot(scene, CanvasName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject canvasObject = new GameObject(
            CanvasName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(HomePageScenePanels));
        canvasObject.layer = LayerMask.NameToLayer("UI");
        SceneManager.MoveGameObjectToScene(canvasObject, scene);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 230;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.localScale = Vector3.one;
        Stretch(canvasRect);

        BuildBookHomeCanvas(canvasRect);

        List<HomePageScenePanel> panels = new List<HomePageScenePanel>
        {
            BuildTaskPanel(canvasRect),
            BuildInventoryPanel(canvasRect),
            BuildRechargePanel(canvasRect),
            BuildLotteryPanel(canvasRect),
            BuildDailySignPanel(canvasRect),
            BuildNewbieSignPanel(canvasRect),
            BuildMailPanel(canvasRect),
            BuildSettingsPanel(canvasRect),
            BuildAccountProfilePanel(canvasRect),
            BuildFriendsPanel(canvasRect),
            BuildEquipmentPanel(canvasRect),
            BuildShopPanel(canvasRect),
            BuildRolePanel(canvasRect)
        };

        HomePageScenePanels registry = canvasObject.GetComponent<HomePageScenePanels>();
        registry.EditorSetPanels(panels.ToArray(), ScenePanelsSchemaVersion);
        for (int i = 0; i < panels.Count; i++)
        {
            panels[i].SetVisible(false);
            EditorUtility.SetDirty(panels[i]);
        }

        EditorUtility.SetDirty(registry);
        EditorSceneManager.MarkSceneDirty(scene);
        if (saveScene)
        {
            EditorSceneManager.SaveScene(scene);
        }

        Selection.activeGameObject = canvasObject;
        Debug.Log("Home scene panels rebuilt as editable Unity UI components.");
    }

    private static RectTransform BuildBookHomeCanvas(Transform parent)
    {
        Transform existing = parent.Find("BookHomeCanvas");
        GameObject bookObject = existing != null
            ? existing.gameObject
            : CreateRectObject("BookHomeCanvas", parent);
        RectTransform rect = bookObject.GetComponent<RectTransform>();
        Stretch(rect);
        rect.SetAsFirstSibling();

        HomePageBookStyle style = bookObject.GetComponent<HomePageBookStyle>();
        if (style == null)
        {
            style = bookObject.AddComponent<HomePageBookStyle>();
        }

        if (bookObject.GetComponent<HomePageBookView>() == null)
        {
            bookObject.AddComponent<HomePageBookView>();
        }

        HomePageUI homePage = bookObject.GetComponent<HomePageUI>();
        if (homePage == null)
        {
            homePage = bookObject.AddComponent<HomePageUI>();
        }

        RemoveLegacyHomePageController(parent.gameObject.scene, homePage);

        Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(HomeBackgroundPath);
        style.EditorSetDefaultSprites(background);
        homePage.EditorSetFont(defaultFont);
        homePage.EditorRebuildBookPreview();
        EditorUtility.SetDirty(style);
        EditorUtility.SetDirty(homePage);
        return rect;
    }

    private static void RemoveLegacyHomePageController(Scene scene, HomePageUI retainedController)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            HomePageUI[] controllers = roots[i].GetComponentsInChildren<HomePageUI>(true);
            for (int j = 0; j < controllers.Length; j++)
            {
                HomePageUI controller = controllers[j];
                if (controller == retainedController || controller.gameObject.name != "Home Page Controller")
                {
                    continue;
                }

                Component[] components = controller.gameObject.GetComponents<Component>();
                if (components.Length == 2)
                {
                    Object.DestroyImmediate(controller.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(controller);
                }
            }
        }
    }

    private static HomePageScenePanel BuildTaskPanel(Transform parent)
    {
        HomePageScenePanel scenePanel = CreateScenePanel(parent, "TaskScenePanel", HomePageScenePanelType.Task);
        Transform root = scenePanel.transform;
        CreateDimmer(root);

        Image shadow = CreateImage("TaskPanelShadow", root, new Color(0.10f, 0.05f, 0.02f, 0.48f));
        SetTopLeft(shadow.rectTransform, new Vector2(256f, -110f), new Vector2(1418f, 808f));
        shadow.raycastTarget = false;

        Image panel = CreateImage("TaskPanel", root, new Color(0.42f, 0.22f, 0.10f, 1f));
        SetTopLeft(panel.rectTransform, new Vector2(240f, -94f), new Vector2(1410f, 800f));

        Image paper = CreateImage("TaskPaper", panel.transform, new Color(1f, 0.88f, 0.66f, 1f));
        SetTopLeft(paper.rectTransform, new Vector2(14f, -14f), new Vector2(1382f, 772f));
        paper.raycastTarget = false;

        Image header = CreateImage("TaskHeader", panel.transform, new Color(0.48f, 0.17f, 0.10f, 1f));
        SetTopLeft(header.rectTransform, new Vector2(22f, -22f), new Vector2(1366f, 68f));
        header.raycastTarget = false;

        Text title = CreateText("TaskTitle", panel.transform, "任务列表", 36, FontStyle.Bold, new Color(1f, 0.86f, 0.46f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(title.rectTransform, new Vector2(58f, -30f), new Vector2(230f, 48f));
        Text subtitle = CreateText("TaskSubtitle", panel.transform, "完成委托，领取领地成长物资", 18, FontStyle.Bold, new Color(1f, 0.90f, 0.70f, 0.88f), TextAnchor.MiddleLeft);
        SetTopLeft(subtitle.rectTransform, new Vector2(286f, -38f), new Vector2(390f, 34f));
        Text total = CreateText("TaskTotalProgress", panel.transform, "本页进度  0/4", 20, FontStyle.Bold, new Color(1f, 0.88f, 0.58f, 1f), TextAnchor.MiddleRight);
        SetTopLeft(total.rectTransform, new Vector2(970f, -38f), new Vector2(300f, 34f));

        Button close = CreateButton("TaskCloseButton", panel.transform, "X", new Color(0.78f, 0.27f, 0.16f, 1f), Color.white, 24);
        SetTopLeft(close.GetComponent<RectTransform>(), new Vector2(1322f, -30f), new Vector2(50f, 50f));

        Image sidebar = CreateImage("TaskSidebar", panel.transform, new Color(0.72f, 0.43f, 0.20f, 0.92f));
        SetTopLeft(sidebar.rectTransform, new Vector2(28f, -106f), new Vector2(210f, 604f));
        string[] ids = { "daily", "main", "growth", "activity" };
        string[] labels = { "每日任务", "主线任务", "成长任务", "活动任务" };
        for (int i = 0; i < ids.Length; i++)
        {
            Button category = CreateButton(
                "TaskCategory_" + ids[i],
                sidebar.transform,
                labels[i],
                i == 0 ? new Color(0.88f, 0.35f, 0.18f, 1f) : new Color(0.98f, 0.77f, 0.43f, 1f),
                i == 0 ? Color.white : new Color(0.38f, 0.18f, 0.08f, 1f),
                22);
            SetTopLeft(category.GetComponent<RectTransform>(), new Vector2(16f, -24f - i * 104f), new Vector2(178f, 84f));
            Image marker = CreateImage("SelectedMarker", category.transform, new Color(1f, 0.89f, 0.42f, 1f));
            SetTopLeft(marker.rectTransform, new Vector2(164f, -23f), new Vector2(14f, 38f));
            marker.gameObject.SetActive(i == 0);
        }

        Image badge = CreateImage("TaskSidebarBadge", sidebar.transform, new Color(0.39f, 0.18f, 0.08f, 0.90f));
        SetTopLeft(badge.rectTransform, new Vector2(16f, -468f), new Vector2(178f, 104f));
        Text badgeText = CreateText("TaskSidebarBadgeTitle", badge.transform, "委托印章", 18, FontStyle.Bold, new Color(1f, 0.83f, 0.42f, 1f), TextAnchor.MiddleCenter);
        Stretch(badgeText.rectTransform);

        Image listHeader = CreateImage("TaskListHeader", panel.transform, new Color(0.89f, 0.59f, 0.28f, 1f));
        SetTopLeft(listHeader.rectTransform, new Vector2(258f, -106f), new Vector2(1092f, 64f));
        Text categoryTitle = CreateText("TaskCategoryTitle", listHeader.transform, "每日任务", 27, FontStyle.Bold, new Color(0.32f, 0.14f, 0.06f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(categoryTitle.rectTransform, new Vector2(22f, -8f), new Vector2(300f, 46f));
        Text categoryNote = CreateText("TaskCategoryNote", listHeader.transform, "每日 00:00 刷新完成状态与奖励", 17, FontStyle.Bold, new Color(0.43f, 0.24f, 0.12f, 0.90f), TextAnchor.MiddleRight);
        SetTopLeft(categoryNote.rectTransform, new Vector2(520f, -10f), new Vector2(544f, 42f));

        for (int i = 0; i < 4; i++)
        {
            BuildTaskRow(panel.transform, i, new Vector2(258f, -184f - i * 131f));
        }

        Image footer = CreateImage("TaskFooter", panel.transform, new Color(0.54f, 0.28f, 0.12f, 0.90f));
        SetTopLeft(footer.rectTransform, new Vector2(258f, -718f), new Vector2(1092f, 42f));
        Text footerText = CreateText("TaskFooterText", footer.transform, "每日任务会在次日刷新", 17, FontStyle.Bold, new Color(1f, 0.88f, 0.64f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(footerText.rectTransform, new Vector2(18f, -4f), new Vector2(1056f, 32f));
        return scenePanel;
    }

    private static void BuildTaskRow(Transform parent, int index, Vector2 position)
    {
        Image rowShadow = CreateImage("TaskRowShadow" + index, parent, new Color(0.26f, 0.12f, 0.05f, 0.24f));
        SetTopLeft(rowShadow.rectTransform, position + new Vector2(5f, -6f), new Vector2(1092f, 118f));
        rowShadow.raycastTarget = false;

        Image row = CreateImage("TaskRow" + index, parent, new Color(1f, 0.93f, 0.76f, 1f));
        SetTopLeft(row.rectTransform, position, new Vector2(1092f, 118f));
        Image accent = CreateImage("TaskRowAccent", row.transform, new Color(0.82f, 0.35f, 0.16f, 1f));
        SetTopLeft(accent.rectTransform, Vector2.zero, new Vector2(9f, 118f));

        Image numberBadge = CreateImage("TaskNumberBadge", row.transform, new Color(0.58f, 0.30f, 0.13f, 1f));
        SetTopLeft(numberBadge.rectTransform, new Vector2(22f, -30f), new Vector2(54f, 54f));
        Text number = CreateText("TaskNumber", numberBadge.transform, (index + 1).ToString(), 25, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(number.rectTransform);

        Text title = CreateText("TaskItemTitle", row.transform, "任务名称", 22, FontStyle.Bold, new Color(0.30f, 0.13f, 0.06f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(title.rectTransform, new Vector2(92f, -13f), new Vector2(390f, 32f));
        Text description = CreateText("TaskItemDescription", row.transform, "任务描述", 16, FontStyle.Normal, new Color(0.43f, 0.31f, 0.21f, 1f), TextAnchor.UpperLeft);
        SetTopLeft(description.rectTransform, new Vector2(92f, -49f), new Vector2(390f, 52f));

        Text progressTitle = CreateText("TaskProgressTitle", row.transform, "任务进度", 16, FontStyle.Bold, new Color(0.40f, 0.22f, 0.10f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(progressTitle.rectTransform, new Vector2(494f, -15f), new Vector2(190f, 24f));
        Image progressBackground = CreateImage("TaskProgressBackground", row.transform, new Color(0.34f, 0.20f, 0.11f, 0.65f));
        SetTopLeft(progressBackground.rectTransform, new Vector2(494f, -49f), new Vector2(190f, 18f));
        Image progressFill = CreateImage("TaskProgressFill", row.transform, new Color(0.95f, 0.58f, 0.18f, 1f));
        SetTopLeft(progressFill.rectTransform, new Vector2(494f, -49f), new Vector2(40f, 18f));
        Text progressValue = CreateText("TaskProgressValue", row.transform, "0/1", 16, FontStyle.Bold, new Color(0.39f, 0.22f, 0.11f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(progressValue.rectTransform, new Vector2(494f, -72f), new Vector2(190f, 24f));

        Image reward = CreateImage("TaskReward", row.transform, new Color(0.99f, 0.79f, 0.37f, 0.88f));
        SetTopLeft(reward.rectTransform, new Vector2(704f, -18f), new Vector2(188f, 82f));
        Image rewardIcon = CreateImage("TaskRewardIcon", reward.transform, new Color(1f, 0.71f, 0.12f, 1f));
        SetTopLeft(rewardIcon.rectTransform, new Vector2(12f, -20f), new Vector2(40f, 40f));
        Text rewardIconText = CreateText("TaskRewardIconText", rewardIcon.transform, "奖", 18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(rewardIconText.rectTransform);
        Text rewardText = CreateText("TaskRewardText", reward.transform, "金币\nx100", 16, FontStyle.Bold, new Color(0.38f, 0.17f, 0.06f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(rewardText.rectTransform, new Vector2(62f, -10f), new Vector2(116f, 62f));

        Button action = CreateButton("TaskActionButton", row.transform, "前往", new Color(0.83f, 0.34f, 0.16f, 1f), Color.white, 20);
        SetTopLeft(action.GetComponent<RectTransform>(), new Vector2(922f, -34f), new Vector2(142f, 50f));
    }

    private static HomePageScenePanel BuildInventoryPanel(Transform parent)
    {
        HomePageScenePanel scenePanel = CreateScenePanel(parent, "InventoryScenePanel", HomePageScenePanelType.Inventory);
        CreateDimmer(scenePanel.transform);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InventoryPrefabPath);
        if (prefab != null)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scenePanel.transform) as GameObject;
            if (instance != null)
            {
                instance.name = "InventoryPanelView";
                RectTransform rect = instance.GetComponent<RectTransform>();
                if (rect != null)
                {
                    Stretch(rect);
                }
            }
        }
        else
        {
            Text missing = CreateText("InventoryMissing", scenePanel.transform, "InventoryPanel.prefab 未找到", 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(missing.rectTransform);
        }

        return scenePanel;
    }

    private static HomePageScenePanel BuildRechargePanel(Transform parent)
    {
        HomePageScenePanel scenePanel = CreateScenePanel(parent, "RechargeScenePanel", HomePageScenePanelType.Recharge);
        Transform root = scenePanel.transform;
        CreateDimmer(root);

        Image panel = CreateImage("RechargePanel", root, new Color(0.47f, 0.26f, 0.12f, 1f));
        SetTopLeft(panel.rectTransform, new Vector2(240f, -94f), new Vector2(1410f, 800f));
        Image paper = CreateImage("RechargePaper", panel.transform, new Color(0.96f, 0.76f, 0.43f, 0.96f));
        SetTopLeft(paper.rectTransform, new Vector2(14f, -14f), new Vector2(1382f, 772f));

        Button close = CreateButton("RechargeCloseButton", panel.transform, "<", new Color(0.30f, 0.17f, 0.09f, 1f), new Color(1f, 0.86f, 0.58f, 1f), 28);
        SetTopLeft(close.GetComponent<RectTransform>(), new Vector2(24f, -24f), new Vector2(48f, 48f));
        Text title = CreateText("RechargeTitle", panel.transform, "钻石充值", 34, FontStyle.Bold, new Color(0.39f, 0.17f, 0.06f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(title.rectTransform, new Vector2(92f, -24f), new Vector2(220f, 48f));

        BuildRechargeResource(panel.transform, "RechargeGoldText", "金币  1000", new Vector2(330f, -22f), new Color(1f, 0.72f, 0.18f, 1f));
        BuildRechargeResource(panel.transform, "RechargeCrystalText", "钻石  0", new Vector2(566f, -22f), new Color(0.25f, 0.67f, 1f, 1f));

        Button guide = CreateButton("RechargeGuideButton", panel.transform, "贵族特权", new Color(0.54f, 0.40f, 0.22f, 1f), new Color(1f, 0.90f, 0.66f, 1f), 20);
        SetTopLeft(guide.GetComponent<RectTransform>(), new Vector2(1190f, -22f), new Vector2(166f, 48f));

        int[] rewards = { 60, 300, 680, 980, 1280, 1980, 3280, 6480 };
        int[] prices = { 6, 30, 68, 98, 128, 198, 328, 648 };
        for (int i = 0; i < 8; i++)
        {
            int row = i / 4;
            int column = i % 4;
            BuildRechargeCard(panel.transform, i, rewards[i], prices[i], new Vector2(118f + column * 316f, -112f - row * 294f));
        }

        Text status = CreateText("RechargeStatus", panel.transform, "选择充值档位", 19, FontStyle.Bold, new Color(0.40f, 0.20f, 0.08f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(status.rectTransform, new Vector2(96f, -718f), new Vector2(720f, 36f));
        Text warning = CreateText("RechargeWarning", panel.transform, "适度娱乐，理性消费", 22, FontStyle.Bold, new Color(0.58f, 0.20f, 0.12f, 1f), TextAnchor.MiddleRight);
        SetTopLeft(warning.rectTransform, new Vector2(932f, -718f), new Vector2(390f, 36f));
        return scenePanel;
    }

    private static HomePageScenePanel BuildShopPanel(Transform parent)
    {
        HomePageScenePanel scenePanel = CreateScenePanel(parent, "ShopScenePanel", HomePageScenePanelType.Shop);
        Transform root = scenePanel.transform;
        CreateDimmer(root);

        Image shadow = CreateImage("ShopPanelShadow", root, new Color(0.08f, 0.05f, 0.03f, 0.50f));
        SetTopLeft(shadow.rectTransform, new Vector2(236f, -116f), new Vector2(1484f, 844f));
        shadow.raycastTarget = false;

        Image panel = CreateImage("ShopPanel", root, new Color(0.36f, 0.20f, 0.10f, 1f));
        SetTopLeft(panel.rectTransform, new Vector2(220f, -100f), new Vector2(1480f, 830f));
        AddSimpleFrame(panel.transform, new Vector2(1480f, 830f), new Color(0.20f, 0.11f, 0.06f, 1f));

        Image header = CreateImage("ShopHeader", panel.transform, new Color(0.16f, 0.39f, 0.34f, 1f));
        SetTopLeft(header.rectTransform, new Vector2(22f, -22f), new Vector2(1436f, 76f));
        Image headerStripe = CreateImage("ShopHeaderStripe", header.transform, new Color(1f, 0.73f, 0.28f, 1f));
        SetTopLeft(headerStripe.rectTransform, Vector2.zero, new Vector2(1436f, 7f));

        Text title = CreateText("ShopTitle", header.transform, "领地商城", 36, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        SetTopLeft(title.rectTransform, new Vector2(30f, -12f), new Vector2(230f, 48f));
        Text subtitle = CreateText("ShopSubtitle", header.transform, "选择金币或钻石购买服务器商品", 17, FontStyle.Bold, new Color(0.86f, 0.96f, 0.90f, 0.90f), TextAnchor.MiddleLeft);
        SetTopLeft(subtitle.rectTransform, new Vector2(242f, -20f), new Vector2(320f, 34f));

        BuildShopResourceBadge(header.transform, "ShopGoldText", new Vector2(650f, -16f), "金币  1000", new Color(1f, 0.72f, 0.18f, 1f));
        BuildShopResourceBadge(header.transform, "ShopCrystalText", new Vector2(850f, -16f), "钻石  0", new Color(0.30f, 0.78f, 0.95f, 1f));

        Button refresh = CreateButton("ShopRefreshButton", header.transform, "刷新", new Color(0.94f, 0.72f, 0.30f, 1f), new Color(0.25f, 0.15f, 0.07f, 1f), 17);
        SetTopLeft(refresh.GetComponent<RectTransform>(), new Vector2(1090f, -14f), new Vector2(112f, 44f));
        Button close = CreateButton("ShopCloseButton", header.transform, "X", new Color(0.88f, 0.28f, 0.22f, 1f), Color.white, 22);
        SetTopLeft(close.GetComponent<RectTransform>(), new Vector2(1356f, -13f), new Vector2(48f, 48f));

        Image paper = CreateImage("ShopPaper", panel.transform, new Color(1f, 0.91f, 0.70f, 1f));
        SetTopLeft(paper.rectTransform, new Vector2(22f, -108f), new Vector2(1436f, 650f));

        Image sidebar = CreateImage("ShopSidebar", panel.transform, new Color(0.80f, 0.55f, 0.26f, 1f));
        SetTopLeft(sidebar.rectTransform, new Vector2(34f, -120f), new Vector2(182f, 626f));
        Text categoryTitle = CreateText("ShopCategoryTitle", sidebar.transform, "商品分类", 22, FontStyle.Bold, new Color(0.31f, 0.16f, 0.07f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(categoryTitle.rectTransform, new Vector2(12f, -18f), new Vector2(158f, 38f));
        BuildShopCategoryButton(sidebar.transform, "gold", "金币", new Vector2(12f, -76f));
        BuildShopCategoryButton(sidebar.transform, "diamond", "钻石", new Vector2(12f, -148f));

        Image sidebarNote = CreateImage("ShopSidebarNote", sidebar.transform, new Color(0.23f, 0.47f, 0.42f, 0.96f));
        SetTopLeft(sidebarNote.rectTransform, new Vector2(12f, -250f), new Vector2(158f, 176f));
        Text noteTitle = CreateText("ShopSidebarNoteTitle", sidebarNote.transform, "商城规则", 18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        SetTopLeft(noteTitle.rectTransform, new Vector2(12f, -14f), new Vector2(134f, 28f));
        Text note = CreateText("ShopSidebarNoteText", sidebarNote.transform, "商品、价格和限额\n均由服务器结算\n\n金币与钻石独立结算", 15, FontStyle.Normal, new Color(0.92f, 1f, 0.96f, 1f), TextAnchor.UpperCenter);
        SetTopLeft(note.rectTransform, new Vector2(12f, -48f), new Vector2(134f, 112f));

        Image catalog = CreateImage("ShopCatalog", panel.transform, new Color(0.98f, 0.86f, 0.62f, 1f));
        SetTopLeft(catalog.rectTransform, new Vector2(228f, -120f), new Vector2(790f, 626f));
        Text resultCount = CreateText("ShopResultCount", catalog.transform, "金币  0 件商品", 20, FontStyle.Bold, new Color(0.33f, 0.18f, 0.08f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(resultCount.rectTransform, new Vector2(18f, -12f), new Vector2(300f, 34f));
        Button previous = CreateButton("ShopPreviousButton", catalog.transform, "<", new Color(0.36f, 0.58f, 0.54f, 1f), Color.white, 20);
        SetTopLeft(previous.GetComponent<RectTransform>(), new Vector2(624f, -10f), new Vector2(42f, 36f));
        Text pageText = CreateText("ShopPageText", catalog.transform, "1/1", 17, FontStyle.Bold, new Color(0.34f, 0.20f, 0.10f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(pageText.rectTransform, new Vector2(668f, -10f), new Vector2(62f, 36f));
        Button next = CreateButton("ShopNextButton", catalog.transform, ">", new Color(0.36f, 0.58f, 0.54f, 1f), Color.white, 20);
        SetTopLeft(next.GetComponent<RectTransform>(), new Vector2(730f, -10f), new Vector2(42f, 36f));

        for (int i = 0; i < 6; i++)
        {
            int row = i / 3;
            int column = i % 3;
            BuildShopProductCard(catalog.transform, i, new Vector2(18f + column * 254f, -62f - row * 250f));
        }

        Text emptyState = CreateText("ShopEmptyState", catalog.transform, "当前分类暂无商品", 24, FontStyle.Bold, new Color(0.50f, 0.34f, 0.18f, 0.88f), TextAnchor.MiddleCenter);
        SetTopLeft(emptyState.rectTransform, new Vector2(170f, -246f), new Vector2(450f, 70f));

        Image detail = CreateImage("ShopDetail", panel.transform, new Color(0.93f, 0.79f, 0.55f, 1f));
        SetTopLeft(detail.rectTransform, new Vector2(1030f, -120f), new Vector2(416f, 626f));
        Text detailHeader = CreateText("ShopDetailHeader", detail.transform, "商品详情", 23, FontStyle.Bold, new Color(0.33f, 0.17f, 0.07f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(detailHeader.rectTransform, new Vector2(22f, -14f), new Vector2(200f, 36f));

        Image detailIcon = CreateImage("ShopDetailIcon", detail.transform, new Color(0.30f, 0.67f, 0.92f, 1f));
        SetTopLeft(detailIcon.rectTransform, new Vector2(22f, -62f), new Vector2(82f, 82f));
        Text detailIconText = CreateText("ShopDetailIconText", detailIcon.transform, "商", 34, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(detailIconText.rectTransform);
        Text detailName = CreateText("ShopDetailName", detail.transform, "暂无商品", 24, FontStyle.Bold, new Color(0.26f, 0.15f, 0.08f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(detailName.rectTransform, new Vector2(120f, -68f), new Vector2(270f, 42f));
        Text detailDescription = CreateText("ShopDetailDescription", detail.transform, "请选择左侧商品", 16, FontStyle.Normal, new Color(0.42f, 0.30f, 0.18f, 1f), TextAnchor.UpperLeft);
        SetTopLeft(detailDescription.rectTransform, new Vector2(22f, -160f), new Vector2(372f, 74f));

        Image divider = CreateImage("ShopDetailDivider", detail.transform, new Color(0.56f, 0.34f, 0.16f, 0.45f));
        SetTopLeft(divider.rectTransform, new Vector2(22f, -246f), new Vector2(372f, 3f));
        Text reward = CreateText("ShopDetailReward", detail.transform, "购买内容  --", 18, FontStyle.Bold, new Color(0.22f, 0.45f, 0.34f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(reward.rectTransform, new Vector2(22f, -262f), new Vector2(372f, 34f));
        Text price = CreateText("ShopDetailPrice", detail.transform, "价格  --", 20, FontStyle.Bold, new Color(0.78f, 0.24f, 0.17f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(price.rectTransform, new Vector2(22f, -304f), new Vector2(372f, 38f));
        Button buyGold = CreateButton("ShopBuyGoldButton", detail.transform, "购买", new Color(0.88f, 0.57f, 0.16f, 1f), new Color(0.31f, 0.17f, 0.06f, 1f), 19);
        SetTopLeft(buyGold.GetComponent<RectTransform>(), new Vector2(22f, -354f), new Vector2(180f, 50f));
        Button buyCrystal = CreateButton("ShopBuyCrystalButton", detail.transform, "购买记录", new Color(0.30f, 0.57f, 0.52f, 1f), Color.white, 19);
        SetTopLeft(buyCrystal.GetComponent<RectTransform>(), new Vector2(214f, -354f), new Vector2(180f, 50f));

        Image purchase = CreateImage("ShopPurchaseBox", detail.transform, new Color(1f, 0.91f, 0.70f, 1f));
        SetTopLeft(purchase.rectTransform, new Vector2(22f, -418f), new Vector2(372f, 186f));
        Text purchaseTitle = CreateText("ShopPurchaseTitle", purchase.transform, "购买信息", 18, FontStyle.Bold, new Color(0.35f, 0.19f, 0.09f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(purchaseTitle.rectTransform, new Vector2(14f, -10f), new Vector2(150f, 28f));
        Text purchaseSummary = CreateText("ShopPurchaseSummary", purchase.transform, "请选择服务器商品", 14, FontStyle.Normal, new Color(0.42f, 0.30f, 0.18f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(purchaseSummary.rectTransform, new Vector2(14f, -42f), new Vector2(344f, 30f));
        Text ownedCount = CreateText("ShopOwnedCount", purchase.transform, "背包已拥有  --", 15, FontStyle.Bold, new Color(0.19f, 0.43f, 0.37f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(ownedCount.rectTransform, new Vector2(14f, -76f), new Vector2(344f, 30f));
        Text storageMode = CreateText("ShopStorageModeText", purchase.transform, "商品以服务器目录为准", 13, FontStyle.Normal, new Color(0.48f, 0.31f, 0.17f, 0.88f), TextAnchor.MiddleLeft);
        SetTopLeft(storageMode.rectTransform, new Vector2(14f, -104f), new Vector2(132f, 30f));
        Button lotDecrease = CreateButton("ShopLotDecreaseButton", purchase.transform, "-", new Color(0.67f, 0.46f, 0.22f, 1f), Color.white, 16);
        SetTopLeft(lotDecrease.GetComponent<RectTransform>(), new Vector2(152f, -101f), new Vector2(38f, 34f));
        Text lotCount = CreateText("ShopLotCountText", purchase.transform, "批次 --", 14, FontStyle.Bold, new Color(0.27f, 0.16f, 0.08f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(lotCount.rectTransform, new Vector2(192f, -101f), new Vector2(74f, 34f));
        Button lotIncrease = CreateButton("ShopLotIncreaseButton", purchase.transform, "+", new Color(0.31f, 0.62f, 0.45f, 1f), Color.white, 16);
        SetTopLeft(lotIncrease.GetComponent<RectTransform>(), new Vector2(268f, -101f), new Vector2(38f, 34f));
        Button openBag = CreateButton("ShopOpenBagButton", purchase.transform, "打开背包", new Color(0.30f, 0.57f, 0.52f, 1f), Color.white, 17);
        SetTopLeft(openBag.GetComponent<RectTransform>(), new Vector2(214f, -142f), new Vector2(144f, 34f));

        Image footer = CreateImage("ShopFooter", panel.transform, new Color(0.17f, 0.37f, 0.33f, 1f));
        SetTopLeft(footer.rectTransform, new Vector2(34f, -772f), new Vector2(1412f, 40f));
        Text status = CreateText("ShopStatusText", footer.transform, "商城准备完成", 16, FontStyle.Bold, new Color(0.94f, 1f, 0.96f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(status.rectTransform, new Vector2(18f, -4f), new Vector2(1376f, 32f));

        return scenePanel;
    }

    private static void BuildShopResourceBadge(Transform parent, string name, Vector2 position, string value, Color accent)
    {
        Image badge = CreateImage(name + "Badge", parent, new Color(0.08f, 0.22f, 0.19f, 0.92f));
        SetTopLeft(badge.rectTransform, position, new Vector2(188f, 44f));
        Image mark = CreateImage(name + "Mark", badge.transform, accent);
        SetTopLeft(mark.rectTransform, new Vector2(10f, -12f), new Vector2(20f, 20f));
        Text text = CreateText(name, badge.transform, value, 17, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        SetTopLeft(text.rectTransform, new Vector2(40f, -5f), new Vector2(138f, 34f));
    }

    private static void BuildShopCategoryButton(Transform parent, string category, string label, Vector2 position)
    {
        Button button = CreateButton("ShopCategory_" + category, parent, label, new Color(0.95f, 0.78f, 0.48f, 1f), new Color(0.34f, 0.18f, 0.08f, 1f), 19);
        SetTopLeft(button.GetComponent<RectTransform>(), position, new Vector2(158f, 58f));
        Image selected = CreateImage("SelectedMarker", button.transform, new Color(1f, 0.87f, 0.48f, 1f));
        SetTopLeft(selected.rectTransform, Vector2.zero, new Vector2(7f, 58f));
    }

    private static void BuildShopProductCard(Transform parent, int index, Vector2 position)
    {
        Button card = CreateButton("ShopProductCard" + index, parent, string.Empty, new Color(1f, 0.96f, 0.82f, 1f), new Color(0.33f, 0.18f, 0.08f, 1f), 16);
        SetTopLeft(card.GetComponent<RectTransform>(), position, new Vector2(236f, 226f));
        Image selected = CreateImage("ShopSelectionMark", card.transform, new Color(0.20f, 0.58f, 0.48f, 1f));
        SetTopLeft(selected.rectTransform, Vector2.zero, new Vector2(8f, 226f));
        Image tag = CreateImage("ShopProductTagBg", card.transform, new Color(0.88f, 0.32f, 0.19f, 1f));
        SetTopLeft(tag.rectTransform, new Vector2(162f, -10f), new Vector2(62f, 28f));
        Text tagText = CreateText("ShopProductTag", tag.transform, "资源", 13, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(tagText.rectTransform);
        Image icon = CreateImage("ShopProductIcon", card.transform, new Color(0.30f, 0.67f, 0.92f, 1f));
        SetTopLeft(icon.rectTransform, new Vector2(18f, -24f), new Vector2(78f, 78f));
        Text iconText = CreateText("ShopProductIconText", icon.transform, "商", 30, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(iconText.rectTransform);
        Text name = CreateText("ShopProductName", card.transform, "商城商品", 18, FontStyle.Bold, new Color(0.29f, 0.16f, 0.08f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(name.rectTransform, new Vector2(108f, -48f), new Vector2(116f, 44f));
        Text reward = CreateText("ShopProductReward", card.transform, "奖励 x0", 16, FontStyle.Bold, new Color(0.24f, 0.47f, 0.36f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(reward.rectTransform, new Vector2(18f, -122f), new Vector2(204f, 30f));
        Image priceBar = CreateImage("ShopPriceBar", card.transform, new Color(0.90f, 0.68f, 0.32f, 1f));
        SetTopLeft(priceBar.rectTransform, new Vector2(18f, -170f), new Vector2(204f, 42f));
        Text price = CreateText("ShopProductPrice", priceBar.transform, "金币0/批", 15, FontStyle.Bold, new Color(0.36f, 0.17f, 0.07f, 1f), TextAnchor.MiddleCenter);
        Stretch(price.rectTransform);
    }

    private static void BuildRechargeResource(Transform parent, string name, string value, Vector2 position, Color iconColor)
    {
        Image badge = CreateImage(name + "Badge", parent, new Color(0.34f, 0.19f, 0.09f, 0.86f));
        SetTopLeft(badge.rectTransform, position, new Vector2(214f, 46f));
        Image icon = CreateImage(name + "Icon", badge.transform, iconColor);
        SetTopLeft(icon.rectTransform, new Vector2(10f, -8f), new Vector2(30f, 30f));
        Text label = CreateText(name, badge.transform, value, 18, FontStyle.Bold, new Color(1f, 0.91f, 0.68f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(label.rectTransform, new Vector2(50f, -5f), new Vector2(154f, 36f));
    }

    private static void BuildRechargeCard(Transform parent, int index, int reward, int price, Vector2 position)
    {
        Image shadow = CreateImage("RechargeProductShadow" + index, parent, new Color(0.12f, 0.05f, 0.02f, 0.24f));
        SetTopLeft(shadow.rectTransform, position + new Vector2(6f, -6f), new Vector2(250f, 248f));
        Image card = CreateImage("RechargeProductCard" + index, parent, new Color(0.48f, 0.31f, 0.17f, 0.96f));
        SetTopLeft(card.rectTransform, position, new Vector2(250f, 248f));

        Image header = CreateImage("ProductHeader", card.transform, new Color(0.82f, 0.62f, 0.30f, 1f));
        SetTopLeft(header.rectTransform, Vector2.zero, new Vector2(250f, 42f));
        Text headerText = CreateText("HeaderText", header.transform, $"钻石x{reward}", 20, FontStyle.Bold, new Color(0.30f, 0.17f, 0.08f, 1f), TextAnchor.MiddleCenter);
        Stretch(headerText.rectTransform);
        Text name = CreateText("ProductName", card.transform, $"钻石礼包 {index + 1}", 17, FontStyle.Bold, new Color(1f, 0.89f, 0.65f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(name.rectTransform, new Vector2(18f, -52f), new Vector2(214f, 28f));

        Image crystal = CreateImage("ProductCrystal", card.transform, new Color(0.35f, 0.69f, 1f, 1f));
        SetTopLeft(crystal.rectTransform, new Vector2(83f, -86f), new Vector2(84f, 68f));
        Text crystalText = CreateText("ProductCrystalText", crystal.transform, "晶", 32, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(crystalText.rectTransform);
        Text amount = CreateText("RewardAmount", card.transform, reward.ToString(), 23, FontStyle.Bold, new Color(0.83f, 0.75f, 1f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(amount.rectTransform, new Vector2(70f, -156f), new Vector2(110f, 30f));
        Text bonus = CreateText("BonusText", card.transform, "后端定价商品", 15, FontStyle.Bold, new Color(1f, 0.82f, 0.38f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(bonus.rectTransform, new Vector2(20f, -182f), new Vector2(210f, 24f));

        Button priceButton = CreateButton("RechargePriceButton", card.transform, $"￥{price}", new Color(0.58f, 0.20f, 0.16f, 1f), new Color(1f, 0.86f, 0.48f, 1f), 25);
        SetTopLeft(priceButton.GetComponent<RectTransform>(), new Vector2(0f, -206f), new Vector2(250f, 42f));
    }

    private static HomePageScenePanel BuildLotteryPanel(Transform parent)
    {
        HomePageScenePanel scenePanel = CreateScenePanel(parent, "LotteryScenePanel", HomePageScenePanelType.Lottery);
        Transform root = scenePanel.transform;
        CreateDimmer(root);

        Image panel = CreateImage("LotteryPanel", root, new Color(0.18f, 0.32f, 0.29f, 1f));
        SetTopLeft(panel.rectTransform, new Vector2(210f, -130f), new Vector2(1500f, 820f));
        AddSimpleFrame(panel.transform, new Vector2(1500f, 820f), new Color(0.82f, 0.63f, 0.27f, 1f));

        Image header = CreateImage("LotteryHeader", panel.transform, new Color(0.10f, 0.24f, 0.22f, 1f));
        SetTopLeft(header.rectTransform, new Vector2(24f, -24f), new Vector2(1452f, 82f));
        TMP_Text title = CreateTMPText("LotteryTitle", header.transform, "幸运抽奖", 38, Color.white, TextAlignmentOptions.MidlineLeft);
        SetTopLeft(title.rectTransform, new Vector2(28f, -15f), new Vector2(280f, 50f));
        TMP_Text subtitle = CreateTMPText("LotterySubtitle", header.transform, "服务器奖池 · 奖励按抽奖响应到账", 17, new Color(0.78f, 0.92f, 0.87f, 0.90f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(subtitle.rectTransform, new Vector2(316f, -22f), new Vector2(420f, 36f));

        Image diamondBadge = CreateImage("LotteryDiamondBadge", header.transform, new Color(0.20f, 0.46f, 0.52f, 1f));
        SetTopLeft(diamondBadge.rectTransform, new Vector2(918f, -14f), new Vector2(226f, 54f));
        Image diamondIcon = CreateImage("LotteryDiamondIcon", diamondBadge.transform, new Color(0.38f, 0.82f, 1f, 1f));
        SetTopLeft(diamondIcon.rectTransform, new Vector2(14f, -12f), new Vector2(30f, 30f));
        TMP_Text diamondText = CreateTMPText("LotteryDiamondText", diamondBadge.transform, "钻石 0", 19, Color.white, TextAlignmentOptions.MidlineLeft);
        SetTopLeft(diamondText.rectTransform, new Vector2(56f, -8f), new Vector2(156f, 38f));

        Button pool = CreateTMPButton("LotteryPoolButton", header.transform, "奖池", new Color(0.83f, 0.58f, 0.24f, 1f), new Color(0.20f, 0.12f, 0.06f, 1f), 18);
        SetTopLeft(pool.GetComponent<RectTransform>(), new Vector2(1160f, -12f), new Vector2(142f, 56f));
        Button close = CreateTMPButton("LotteryCloseButton", header.transform, "X", new Color(0.78f, 0.28f, 0.24f, 1f), Color.white, 23);
        SetTopLeft(close.GetComponent<RectTransform>(), new Vector2(1368f, -12f), new Vector2(58f, 58f));

        Image prizeArea = CreateImage("LotteryPrizeArea", panel.transform, new Color(0.93f, 0.85f, 0.65f, 1f));
        SetTopLeft(prizeArea.rectTransform, new Vector2(36f, -132f), new Vector2(1428f, 490f));
        TMP_Text prizeTitle = CreateTMPText("LotteryPrizeTitle", prizeArea.transform, "本期奖品", 26, new Color(0.23f, 0.18f, 0.10f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(prizeTitle.rectTransform, new Vector2(24f, -18f), new Vector2(240f, 40f));
        TMP_Text prizeHint = CreateTMPText("LotteryPrizeHint", prizeArea.transform, "奖项与概率以服务器返回为准", 16, new Color(0.42f, 0.36f, 0.26f, 0.88f), TextAlignmentOptions.MidlineRight);
        SetTopLeft(prizeHint.rectTransform, new Vector2(900f, -22f), new Vector2(494f, 32f));

        Image prizeViewport = CreateImage("LotteryPrizeViewport", prizeArea.transform, new Color(0.98f, 0.93f, 0.79f, 1f));
        SetTopLeft(prizeViewport.rectTransform, new Vector2(22f, -72f), new Vector2(1384f, 330f));
        prizeViewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect prizeScroll = prizeViewport.gameObject.AddComponent<ScrollRect>();
        prizeScroll.horizontal = true;
        prizeScroll.vertical = false;
        prizeScroll.movementType = ScrollRect.MovementType.Elastic;
        GameObject prizeContent = CreateRectObject("LotteryPrizeContent", prizeViewport.transform);
        SetTopLeft(prizeContent.GetComponent<RectTransform>(), new Vector2(18f, -18f), new Vector2(1348f, 292f));
        HorizontalLayoutGroup prizeLayout = prizeContent.AddComponent<HorizontalLayoutGroup>();
        prizeLayout.spacing = 16f;
        prizeLayout.padding = new RectOffset(12, 12, 10, 10);
        prizeLayout.childAlignment = TextAnchor.UpperLeft;
        prizeLayout.childControlWidth = false;
        prizeLayout.childControlHeight = false;
        prizeLayout.childForceExpandWidth = false;
        prizeLayout.childForceExpandHeight = false;
        ContentSizeFitter prizeFitter = prizeContent.AddComponent<ContentSizeFitter>();
        prizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        prizeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        prizeScroll.viewport = prizeViewport.rectTransform;
        prizeScroll.content = prizeContent.GetComponent<RectTransform>();
        BuildLotteryPrizeCardTemplate(prizeContent.transform);
        TMP_Text prizeEmpty = CreateTMPText("LotteryPrizeEmptyText", prizeArea.transform, "正在读取服务器奖池", 22, new Color(0.48f, 0.39f, 0.26f, 0.88f), TextAlignmentOptions.Center);
        SetTopLeft(prizeEmpty.rectTransform, new Vector2(430f, -198f), new Vector2(550f, 54f));

        TMP_Text status = CreateTMPText("LotteryStatus", panel.transform, "正在读取服务器奖池", 17, new Color(0.82f, 0.94f, 0.89f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(status.rectTransform, new Vector2(50f, -650f), new Vector2(1400f, 42f));
        Image actionBar = CreateImage("LotteryActionBar", panel.transform, new Color(0.10f, 0.24f, 0.22f, 1f));
        SetTopLeft(actionBar.rectTransform, new Vector2(36f, -704f), new Vector2(1428f, 88f));
        Button single = CreateTMPButton("LotterySingleButton", actionBar.transform, "单抽", new Color(0.83f, 0.58f, 0.24f, 1f), new Color(0.20f, 0.12f, 0.06f, 1f), 23);
        SetTopLeft(single.GetComponent<RectTransform>(), new Vector2(370f, -12f), new Vector2(300f, 64f));
        Button ten = CreateTMPButton("LotteryTenButton", actionBar.transform, "连抽", new Color(0.35f, 0.70f, 0.53f, 1f), Color.white, 23);
        SetTopLeft(ten.GetComponent<RectTransform>(), new Vector2(758f, -12f), new Vector2(300f, 64f));

        BuildLotteryPoolOverlay(root);
        BuildLotteryRewardConfirmOverlay(root);
        return scenePanel;
    }

    private static void BuildLotteryPrizeCardTemplate(Transform parent)
    {
        Image card = CreateImage("LotteryPrizeCardTemplate", parent, new Color(1f, 0.97f, 0.84f, 1f));
        SetTopLeft(card.rectTransform, Vector2.zero, new Vector2(210f, 286f));
        card.raycastTarget = false;
        Image icon = CreateImage("LotteryPrizeIcon", card.transform, new Color(0.25f, 0.63f, 0.78f, 1f));
        SetTopLeft(icon.rectTransform, new Vector2(67f, -20f), new Vector2(76f, 76f));
        TMP_Text iconText = CreateTMPText("LotteryPrizeIconText", icon.transform, "奖", 26, Color.white, TextAlignmentOptions.Center);
        Stretch(iconText.rectTransform);
        TMP_Text name = CreateTMPText("LotteryPrizeCardName", card.transform, "奖品名称", 19, new Color(0.26f, 0.16f, 0.08f, 1f), TextAlignmentOptions.Center);
        SetTopLeft(name.rectTransform, new Vector2(12f, -112f), new Vector2(186f, 34f));
        TMP_Text amount = CreateTMPText("LotteryPrizeCardAmount", card.transform, "x0", 21, new Color(0.72f, 0.20f, 0.16f, 1f), TextAlignmentOptions.Center);
        SetTopLeft(amount.rectTransform, new Vector2(12f, -152f), new Vector2(186f, 32f));
        TMP_Text probability = CreateTMPText("LotteryPrizeCardProbability", card.transform, "概率 0%", 15, new Color(0.43f, 0.35f, 0.24f, 1f), TextAlignmentOptions.Center);
        SetTopLeft(probability.rectTransform, new Vector2(12f, -204f), new Vector2(186f, 28f));
        card.gameObject.SetActive(false);
    }

    private static void BuildLotteryPoolOverlay(Transform root)
    {
        Image overlay = CreateImage("LotteryPoolPanel", root, new Color(0.04f, 0.08f, 0.07f, 0.78f));
        Stretch(overlay.rectTransform);
        Image panel = CreateImage("LotteryPoolCard", overlay.transform, new Color(0.93f, 0.85f, 0.65f, 1f));
        SetTopLeft(panel.rectTransform, new Vector2(260f, -80f), new Vector2(980f, 740f));
        AddSimpleFrame(panel.transform, new Vector2(980f, 740f), new Color(0.50f, 0.35f, 0.16f, 1f));
        TMP_Text title = CreateTMPText("LotteryPoolTitle", panel.transform, "当前奖池", 30, new Color(0.24f, 0.16f, 0.08f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(title.rectTransform, new Vector2(30f, -24f), new Vector2(300f, 42f));
        TMP_Text note = CreateTMPText("LotteryPoolNote", panel.transform, "奖品、数量和概率均以服务器返回为准", 16, new Color(0.45f, 0.36f, 0.24f, 0.90f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(note.rectTransform, new Vector2(30f, -68f), new Vector2(560f, 30f));
        Button close = CreateTMPButton("LotteryPoolCloseButton", panel.transform, "关闭", new Color(0.77f, 0.30f, 0.24f, 1f), Color.white, 17);
        SetTopLeft(close.GetComponent<RectTransform>(), new Vector2(810f, -20f), new Vector2(128f, 48f));
        Image viewport = CreateImage("LotteryPoolViewport", panel.transform, new Color(0.98f, 0.93f, 0.79f, 1f));
        SetTopLeft(viewport.rectTransform, new Vector2(28f, -116f), new Vector2(924f, 570f));
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        GameObject content = CreateRectObject("LotteryPoolContent", viewport.transform);
        SetTopLeft(content.GetComponent<RectTransform>(), new Vector2(16f, -16f), new Vector2(892f, 530f));
        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        scroll.viewport = viewport.rectTransform;
        scroll.content = content.GetComponent<RectTransform>();
        BuildLotteryPoolRowTemplate(content.transform);
        overlay.gameObject.SetActive(false);
    }

    private static void BuildLotteryPoolRowTemplate(Transform parent)
    {
        Image row = CreateImage("LotteryPoolRowTemplate", parent, new Color(1f, 0.97f, 0.84f, 1f));
        SetTopLeft(row.rectTransform, Vector2.zero, new Vector2(860f, 68f));
        row.raycastTarget = false;
        TMP_Text slot = CreateTMPText("LotteryPoolRowSlot", row.transform, "#1", 16, new Color(0.46f, 0.31f, 0.16f, 1f), TextAlignmentOptions.Center);
        SetTopLeft(slot.rectTransform, new Vector2(12f, -12f), new Vector2(60f, 42f));
        TMP_Text name = CreateTMPText("LotteryPoolRowName", row.transform, "奖品名称", 18, new Color(0.25f, 0.16f, 0.08f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(name.rectTransform, new Vector2(92f, -12f), new Vector2(360f, 42f));
        TMP_Text amount = CreateTMPText("LotteryPoolRowAmount", row.transform, "数量 x0", 16, new Color(0.70f, 0.25f, 0.16f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(amount.rectTransform, new Vector2(482f, -12f), new Vector2(150f, 42f));
        TMP_Text probability = CreateTMPText("LotteryPoolRowProbability", row.transform, "概率 0%", 16, new Color(0.28f, 0.48f, 0.35f, 1f), TextAlignmentOptions.MidlineRight);
        SetTopLeft(probability.rectTransform, new Vector2(650f, -12f), new Vector2(190f, 42f));
        row.gameObject.SetActive(false);
    }

    private static void BuildLotteryRewardConfirmOverlay(Transform root)
    {
        Image overlay = CreateImage("LotteryRewardConfirmPanel", root, new Color(0.04f, 0.08f, 0.07f, 0.82f));
        Stretch(overlay.rectTransform);
        Image panel = CreateImage("LotteryRewardConfirmCard", overlay.transform, new Color(0.93f, 0.85f, 0.65f, 1f));
        SetTopLeft(panel.rectTransform, new Vector2(220f, -100f), new Vector2(1060f, 700f));
        AddSimpleFrame(panel.transform, new Vector2(1060f, 700f), new Color(0.50f, 0.35f, 0.16f, 1f));
        TMP_Text title = CreateTMPText("LotteryRewardConfirmTitle", panel.transform, "抽奖结果", 32, new Color(0.24f, 0.16f, 0.08f, 1f), TextAlignmentOptions.Center);
        SetTopLeft(title.rectTransform, new Vector2(250f, -22f), new Vector2(560f, 46f));
        TMP_Text summary = CreateTMPText("LotteryRewardConfirmSummary", panel.transform, "奖励已由服务器发放", 17, new Color(0.35f, 0.46f, 0.31f, 1f), TextAlignmentOptions.Center);
        SetTopLeft(summary.rectTransform, new Vector2(120f, -70f), new Vector2(820f, 30f));
        Image viewport = CreateImage("LotteryRewardViewport", panel.transform, new Color(0.98f, 0.93f, 0.79f, 1f));
        SetTopLeft(viewport.rectTransform, new Vector2(50f, -112f), new Vector2(960f, 452f));
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        GameObject content = CreateRectObject("LotteryRewardContent", viewport.transform);
        SetTopLeft(content.GetComponent<RectTransform>(), new Vector2(16f, -16f), new Vector2(928f, 410f));
        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        scroll.viewport = viewport.rectTransform;
        scroll.content = content.GetComponent<RectTransform>();
        BuildLotteryRewardRowTemplate(content.transform);
        Button confirm = CreateTMPButton("LotteryRewardConfirmButton", panel.transform, "确认领取", new Color(0.30f, 0.64f, 0.40f, 1f), Color.white, 21);
        SetTopLeft(confirm.GetComponent<RectTransform>(), new Vector2(390f, -614f), new Vector2(280f, 58f));
        overlay.gameObject.SetActive(false);
    }

    private static void BuildLotteryRewardRowTemplate(Transform parent)
    {
        Image row = CreateImage("LotteryRewardRowTemplate", parent, new Color(1f, 0.97f, 0.84f, 1f));
        SetTopLeft(row.rectTransform, Vector2.zero, new Vector2(900f, 76f));
        row.raycastTarget = false;
        TMP_Text sequence = CreateTMPText("LotteryRewardRowSequence", row.transform, "1", 16, new Color(0.46f, 0.31f, 0.16f, 1f), TextAlignmentOptions.Center);
        SetTopLeft(sequence.rectTransform, new Vector2(14f, -14f), new Vector2(54f, 46f));
        TMP_Text name = CreateTMPText("LotteryRewardRowName", row.transform, "奖励名称", 19, new Color(0.25f, 0.16f, 0.08f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(name.rectTransform, new Vector2(88f, -14f), new Vector2(420f, 46f));
        TMP_Text amount = CreateTMPText("LotteryRewardRowAmount", row.transform, "x0", 19, new Color(0.70f, 0.25f, 0.16f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(amount.rectTransform, new Vector2(540f, -14f), new Vector2(120f, 46f));
        TMP_Text note = CreateTMPText("LotteryRewardRowNote", row.transform, string.Empty, 15, new Color(0.28f, 0.48f, 0.35f, 1f), TextAlignmentOptions.MidlineRight);
        SetTopLeft(note.rectTransform, new Vector2(672f, -14f), new Vector2(210f, 46f));
        row.gameObject.SetActive(false);
    }

    private static HomePageScenePanel BuildDailySignPanel(Transform parent)
    {
        HomePageScenePanel scenePanel = CreateScenePanel(parent, "DailySignScenePanel", HomePageScenePanelType.DailySign);
        Transform root = scenePanel.transform;
        CreateDimmer(root);
        Image panel = CreateImage("DailySignPanel", root, new Color(0.86f, 0.70f, 0.42f, 1f));
        SetTopLeft(panel.rectTransform, new Vector2(340f, -100f), new Vector2(1210f, 815f));
        AddSimpleFrame(panel.transform, new Vector2(1210f, 815f), new Color(0.24f, 0.43f, 0.74f, 1f));
        Text title = CreateText("DailySignTitle", panel.transform, "每日签到", 52, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        SetTopLeft(title.rectTransform, new Vector2(410f, -22f), new Vector2(390f, 74f));
        Text subtitle = CreateText("DailySignSubtitle", panel.transform, "两周签到奖励", 22, FontStyle.Bold, new Color(0.38f, 0.24f, 0.11f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(subtitle.rectTransform, new Vector2(486f, -92f), new Vector2(240f, 34f));
        Button close = CreateButton("DailySignCloseButton", panel.transform, "X", new Color(0.91f, 0.39f, 0.16f, 1f), Color.white, 27);
        SetTopLeft(close.GetComponent<RectTransform>(), new Vector2(1128f, -28f), new Vector2(58f, 58f));

        string[] names =
        {
            "金币", "木材", "石材", "英雄箱", "铁矿", "金币", "抽奖券",
            "木材", "石材", "铁矿", "金币", "加速道具", "抽奖券", "高级英雄箱"
        };
        string[] amounts = { "x100", "x120", "x120", "x1", "x80", "x200", "x1", "x180", "x180", "x120", "x260", "x1", "x2", "x1" };
        for (int i = 0; i < 14; i++)
        {
            int row = i / 7;
            int column = i % 7;
            BuildSignCard(panel.transform, "DailySignCard" + (i + 1), i + 1, names[i], amounts[i], new Vector2(50f + column * 162f, -142f - row * 244f), new Vector2(142f, 216f), false);
        }

        Image footer = CreateImage("DailySignFooter", panel.transform, new Color(1f, 0.94f, 0.74f, 0.94f));
        SetTopLeft(footer.rectTransform, new Vector2(62f, -662f), new Vector2(1086f, 92f));
        Text status = CreateText("DailySignStatus", footer.transform, "进度 0/14", 24, FontStyle.Bold, new Color(0.36f, 0.18f, 0.08f, 1f), TextAnchor.MiddleCenter);
        Stretch(status.rectTransform);
        return scenePanel;
    }

    private static HomePageScenePanel BuildNewbieSignPanel(Transform parent)
    {
        HomePageScenePanel scenePanel = CreateScenePanel(parent, "NewbieSignScenePanel", HomePageScenePanelType.NewbieSign);
        Transform root = scenePanel.transform;
        CreateDimmer(root);
        Image panel = CreateImage("NewbieSignPanel", root, new Color(0.91f, 0.78f, 0.48f, 1f));
        SetTopLeft(panel.rectTransform, new Vector2(448f, -156f), new Vector2(1010f, 655f));
        AddSimpleFrame(panel.transform, new Vector2(1010f, 655f), new Color(0.28f, 0.55f, 0.50f, 1f));
        Text title = CreateText("NewbieSignTitle", panel.transform, "新手签到", 50, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        SetTopLeft(title.rectTransform, new Vector2(330f, -24f), new Vector2(350f, 70f));
        Button close = CreateButton("NewbieSignCloseButton", panel.transform, "X", new Color(0.91f, 0.39f, 0.16f, 1f), Color.white, 27);
        SetTopLeft(close.GetComponent<RectTransform>(), new Vector2(930f, -28f), new Vector2(56f, 56f));

        string[] names = { "金币", "木材 石材", "装备宝箱" };
        string[] amounts = { "x300", "x300", "x1" };
        for (int i = 0; i < 3; i++)
        {
            BuildSignCard(panel.transform, "NewbieSignCard" + (i + 1), i + 1, names[i], amounts[i], new Vector2(76f + i * 300f, -130f), new Vector2(250f, 300f), true);
        }

        Image banner = CreateImage("NewbieSignBanner", panel.transform, new Color(1f, 0.96f, 0.78f, 0.96f));
        SetTopLeft(banner.rectTransform, new Vector2(76f, -472f), new Vector2(850f, 110f));
        Text bannerTitle = CreateText("NewbieBannerTitle", banner.transform, "连续签到 3 天", 30, FontStyle.Bold, new Color(0.48f, 0.20f, 0.07f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(bannerTitle.rectTransform, new Vector2(34f, -18f), new Vector2(280f, 38f));
        Text status = CreateText("NewbieSignStatus", banner.transform, "进度 0/3", 22, FontStyle.Bold, new Color(0.36f, 0.18f, 0.08f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(status.rectTransform, new Vector2(34f, -62f), new Vector2(720f, 34f));
        return scenePanel;
    }

    private static void BuildSignCard(Transform parent, string rootName, int day, string rewardName, string rewardAmount, Vector2 position, Vector2 size, bool large)
    {
        Image card = CreateImage(rootName, parent, new Color(1f, 0.92f, 0.74f, 1f));
        SetTopLeft(card.rectTransform, position, size);
        Image header = CreateImage("SignCardHeader", card.transform, new Color(0.93f, 0.52f, 0.20f, 1f));
        SetTopLeft(header.rectTransform, Vector2.zero, new Vector2(size.x, large ? 54f : 42f));
        Text dayText = CreateText("DayText", header.transform, $"第{day}天", large ? 26 : 19, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(dayText.rectTransform);

        Vector2 iconSize = large ? new Vector2(88f, 74f) : new Vector2(62f, 54f);
        Image rewardIcon = CreateImage("RewardIcon", card.transform, rewardName.Contains("箱") ? new Color(0.78f, 0.42f, 0.16f, 1f) : new Color(0.20f, 0.58f, 0.95f, 1f));
        SetTopLeft(rewardIcon.rectTransform, new Vector2((size.x - iconSize.x) * 0.5f, large ? -92f : -66f), iconSize);
        Text iconText = CreateText("RewardIconText", rewardIcon.transform, rewardName.Substring(0, 1), large ? 34 : 24, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(iconText.rectTransform);

        Text reward = CreateText("RewardText", card.transform, $"{rewardName}\n{rewardAmount}", large ? 24 : 17, FontStyle.Bold, new Color(0.40f, 0.22f, 0.10f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(reward.rectTransform, new Vector2(12f, large ? -200f : -144f), new Vector2(size.x - 24f, large ? 62f : 50f));

        Text check = CreateText("ClaimedCheck", card.transform, "✓", large ? 72 : 52, FontStyle.Bold, new Color(0.42f, 0.88f, 0.18f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(check.rectTransform, new Vector2(size.x * 0.5f - 50f, large ? -86f : -60f), new Vector2(100f, 80f));
        Image chip = CreateImage("ClaimedChip", card.transform, new Color(0.38f, 0.31f, 0.22f, 0.78f));
        SetTopLeft(chip.rectTransform, new Vector2(size.x * 0.5f - 46f, large ? -258f : -186f), new Vector2(92f, 30f));
        Text claimed = CreateText("ClaimedText", card.transform, "已领取", large ? 22 : 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        SetTopLeft(claimed.rectTransform, new Vector2(size.x * 0.5f - 46f, large ? -258f : -186f), new Vector2(92f, 30f));
        check.gameObject.SetActive(false);
        chip.gameObject.SetActive(false);
        claimed.gameObject.SetActive(false);
    }

    private static HomePageScenePanel BuildSettingsPanel(Transform parent)
    {
        HomePageScenePanel scenePanel = CreateScenePanel(parent, "SettingsScenePanel", HomePageScenePanelType.Settings);
        Transform root = scenePanel.transform;
        CreateDimmer(root);

        Image shadow = CreateImage("SettingsPanelShadow", root, new Color(0.09f, 0.05f, 0.03f, 0.48f));
        SetTopLeft(shadow.rectTransform, new Vector2(414f, -142f), new Vector2(1120f, 820f));
        shadow.raycastTarget = false;

        Image panel = CreateImage("SettingsPanel", root, new Color(0.43f, 0.24f, 0.12f, 1f));
        SetTopLeft(panel.rectTransform, new Vector2(400f, -128f), new Vector2(1120f, 820f));

        Image paper = CreateImage("SettingsPaper", panel.transform, new Color(1f, 0.94f, 0.77f, 1f));
        SetTopLeft(paper.rectTransform, new Vector2(14f, -14f), new Vector2(1092f, 792f));
        paper.raycastTarget = false;

        Image header = CreateImage("SettingsHeader", panel.transform, new Color(0.18f, 0.50f, 0.50f, 1f));
        SetTopLeft(header.rectTransform, new Vector2(24f, -24f), new Vector2(1072f, 76f));
        Text title = CreateText("SettingsTitle", panel.transform, "游戏设置", 38, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        SetTopLeft(title.rectTransform, new Vector2(60f, -34f), new Vector2(220f, 54f));
        Text subtitle = CreateText("SettingsSubtitle", panel.transform, "声音与显示", 18, FontStyle.Bold, new Color(0.89f, 1f, 0.94f, 0.86f), TextAnchor.MiddleLeft);
        SetTopLeft(subtitle.rectTransform, new Vector2(276f, -44f), new Vector2(220f, 34f));
        Button close = CreateButton("SettingsCloseButton", panel.transform, "X", new Color(0.88f, 0.28f, 0.24f, 1f), Color.white, 24);
        SetTopLeft(close.GetComponent<RectTransform>(), new Vector2(1026f, -36f), new Vector2(52f, 52f));

        BuildSettingsSectionHeader(
            panel.transform,
            "SettingsAudioHeader",
            "声音设置",
            new Vector2(52f, -130f),
            new Vector2(470f, 52f),
            new Color(0.38f, 0.64f, 0.30f, 1f));
        BuildSettingsSectionHeader(
            panel.transform,
            "SettingsDisplayHeader",
            "显示设置",
            new Vector2(600f, -130f),
            new Vector2(470f, 52f),
            new Color(0.30f, 0.54f, 0.76f, 1f));

        Image centerDivider = CreateImage("SettingsCenterDivider", panel.transform, new Color(0.53f, 0.34f, 0.18f, 0.42f));
        SetTopLeft(centerDivider.rectTransform, new Vector2(560f, -132f), new Vector2(4f, 464f));
        centerDivider.raycastTarget = false;

        Text musicLabel = CreateText("SettingsMusicLabel", panel.transform, "背景音乐", 24, FontStyle.Bold, new Color(0.31f, 0.18f, 0.09f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(musicLabel.rectTransform, new Vector2(70f, -210f), new Vector2(170f, 42f));
        CreateSettingToggle("SettingsMusicToggle", panel.transform, new Vector2(392f, -208f), true);
        CreateSettingSlider("SettingsMusicSlider", panel.transform, new Vector2(70f, -274f), new Vector2(360f, 26f), new Color(0.38f, 0.68f, 0.30f, 1f));
        Text musicValue = CreateText("SettingsMusicValue", panel.transform, "100%", 20, FontStyle.Bold, new Color(0.28f, 0.42f, 0.18f, 1f), TextAnchor.MiddleRight);
        SetTopLeft(musicValue.rectTransform, new Vector2(438f, -267f), new Vector2(64f, 34f));

        Image audioDivider = CreateImage("SettingsAudioDivider", panel.transform, new Color(0.53f, 0.34f, 0.18f, 0.28f));
        SetTopLeft(audioDivider.rectTransform, new Vector2(70f, -336f), new Vector2(432f, 3f));
        audioDivider.raycastTarget = false;

        Text soundLabel = CreateText("SettingsSoundLabel", panel.transform, "游戏音效", 24, FontStyle.Bold, new Color(0.31f, 0.18f, 0.09f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(soundLabel.rectTransform, new Vector2(70f, -370f), new Vector2(170f, 42f));
        CreateSettingToggle("SettingsSoundToggle", panel.transform, new Vector2(392f, -368f), true);
        CreateSettingSlider("SettingsSoundSlider", panel.transform, new Vector2(70f, -434f), new Vector2(360f, 26f), new Color(0.23f, 0.62f, 0.72f, 1f));
        Text soundValue = CreateText("SettingsSoundValue", panel.transform, "100%", 20, FontStyle.Bold, new Color(0.18f, 0.42f, 0.50f, 1f), TextAnchor.MiddleRight);
        SetTopLeft(soundValue.rectTransform, new Vector2(438f, -427f), new Vector2(64f, 34f));

        Text qualityLabel = CreateText("SettingsQualityLabel", panel.transform, "画质", 24, FontStyle.Bold, new Color(0.31f, 0.18f, 0.09f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(qualityLabel.rectTransform, new Vector2(620f, -210f), new Vector2(120f, 42f));
        Text qualityValue = CreateText("SettingsQualityValue", panel.transform, "标准", 20, FontStyle.Bold, new Color(0.17f, 0.43f, 0.58f, 1f), TextAnchor.MiddleRight);
        SetTopLeft(qualityValue.rectTransform, new Vector2(946f, -214f), new Vector2(104f, 36f));
        string[] qualityNames = { "流畅", "标准", "精细" };
        for (int i = 0; i < qualityNames.Length; i++)
        {
            Button quality = CreateButton(
                "SettingsQuality" + i,
                panel.transform,
                qualityNames[i],
                i == 1 ? new Color(0.20f, 0.56f, 0.66f, 1f) : new Color(0.90f, 0.80f, 0.62f, 1f),
                i == 1 ? Color.white : new Color(0.34f, 0.20f, 0.10f, 1f),
                19);
            SetTopLeft(quality.GetComponent<RectTransform>(), new Vector2(620f + i * 146f, -266f), new Vector2(128f, 48f));
        }

        Image displayDividerA = CreateImage("SettingsDisplayDividerA", panel.transform, new Color(0.53f, 0.34f, 0.18f, 0.28f));
        SetTopLeft(displayDividerA.rectTransform, new Vector2(620f, -336f), new Vector2(430f, 3f));
        displayDividerA.raycastTarget = false;

        Text fullScreenLabel = CreateText("SettingsFullScreenLabel", panel.transform, "全屏显示", 24, FontStyle.Bold, new Color(0.31f, 0.18f, 0.09f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(fullScreenLabel.rectTransform, new Vector2(620f, -370f), new Vector2(180f, 42f));
        CreateSettingToggle("SettingsFullScreenToggle", panel.transform, new Vector2(934f, -368f), true);

        Image displayDividerB = CreateImage("SettingsDisplayDividerB", panel.transform, new Color(0.53f, 0.34f, 0.18f, 0.28f));
        SetTopLeft(displayDividerB.rectTransform, new Vector2(620f, -432f), new Vector2(430f, 3f));
        displayDividerB.raycastTarget = false;

        Text frameRateLabel = CreateText("SettingsFrameRateLabel", panel.transform, "帧率上限", 24, FontStyle.Bold, new Color(0.31f, 0.18f, 0.09f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(frameRateLabel.rectTransform, new Vector2(620f, -468f), new Vector2(180f, 42f));
        Button frame30 = CreateButton("SettingsFrameRate30", panel.transform, "30 FPS", new Color(0.90f, 0.80f, 0.62f, 1f), new Color(0.34f, 0.20f, 0.10f, 1f), 19);
        SetTopLeft(frame30.GetComponent<RectTransform>(), new Vector2(800f, -464f), new Vector2(116f, 48f));
        Button frame60 = CreateButton("SettingsFrameRate60", panel.transform, "60 FPS", new Color(0.20f, 0.56f, 0.66f, 1f), Color.white, 19);
        SetTopLeft(frame60.GetComponent<RectTransform>(), new Vector2(934f, -464f), new Vector2(116f, 48f));

        Image footerDivider = CreateImage("SettingsFooterDivider", panel.transform, new Color(0.43f, 0.24f, 0.12f, 0.48f));
        SetTopLeft(footerDivider.rectTransform, new Vector2(52f, -620f), new Vector2(1018f, 4f));
        footerDivider.raycastTarget = false;

        Text status = CreateText("SettingsStatus", panel.transform, "设置修改后会即时生效", 18, FontStyle.Bold, new Color(0.32f, 0.42f, 0.28f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(status.rectTransform, new Vector2(58f, -642f), new Vector2(650f, 34f));
        Text account = CreateText("SettingsAccount", panel.transform, "当前账号：", 17, FontStyle.Normal, new Color(0.42f, 0.36f, 0.29f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(account.rectTransform, new Vector2(58f, -682f), new Vector2(480f, 32f));

        Button reset = CreateButton("SettingsResetButton", panel.transform, "恢复默认", new Color(0.88f, 0.75f, 0.50f, 1f), new Color(0.34f, 0.20f, 0.10f, 1f), 20);
        SetTopLeft(reset.GetComponent<RectTransform>(), new Vector2(548f, -724f), new Vector2(150f, 52f));
        Button logout = CreateButton("SettingsLogoutButton", panel.transform, "返回登录", new Color(0.84f, 0.43f, 0.34f, 1f), Color.white, 20);
        SetTopLeft(logout.GetComponent<RectTransform>(), new Vector2(718f, -724f), new Vector2(164f, 52f));
        Button save = CreateButton("SettingsSaveButton", panel.transform, "保存并关闭", new Color(0.26f, 0.60f, 0.40f, 1f), Color.white, 20);
        SetTopLeft(save.GetComponent<RectTransform>(), new Vector2(902f, -724f), new Vector2(168f, 52f));

        return scenePanel;
    }

    private static void BuildSettingsSectionHeader(
        Transform parent,
        string name,
        string label,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        Image header = CreateImage(name, parent, color);
        SetTopLeft(header.rectTransform, position, size);
        Text text = CreateText("Text", header.transform, label, 25, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
    }

    private static Toggle CreateSettingToggle(string name, Transform parent, Vector2 position, bool isOn)
    {
        Image background = CreateImage(name, parent, isOn
            ? new Color(0.28f, 0.66f, 0.38f, 1f)
            : new Color(0.57f, 0.55f, 0.51f, 1f));
        SetTopLeft(background.rectTransform, position, new Vector2(110f, 46f));
        Toggle toggle = background.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = background;

        Text check = CreateText("ToggleCheck", background.transform, "✓", 24, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        SetTopLeft(check.rectTransform, new Vector2(8f, -7f), new Vector2(32f, 32f));
        toggle.graphic = check;

        Text label = CreateText("ToggleLabel", background.transform, isOn ? "开启" : "关闭", 18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        SetTopLeft(label.rectTransform, new Vector2(38f, -5f), new Vector2(66f, 36f));
        toggle.isOn = isOn;
        return toggle;
    }

    private static Slider CreateSettingSlider(string name, Transform parent, Vector2 position, Vector2 size, Color fillColor)
    {
        Image track = CreateImage(name, parent, new Color(0.52f, 0.42f, 0.31f, 0.52f));
        SetTopLeft(track.rectTransform, position, size);
        Slider slider = track.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.wholeNumbers = true;
        slider.direction = Slider.Direction.LeftToRight;

        Image fill = CreateImage("Fill", track.transform, fillColor);
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.pivot = new Vector2(0.5f, 0.5f);
        fillRect.offsetMin = new Vector2(4f, 4f);
        fillRect.offsetMax = new Vector2(-4f, -4f);

        Image handle = CreateImage("Handle", track.transform, new Color(1f, 0.93f, 0.66f, 1f));
        RectTransform handleRect = handle.rectTransform;
        handleRect.anchorMin = new Vector2(1f, 0.5f);
        handleRect.anchorMax = new Vector2(1f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = new Vector2(24f, 34f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        slider.value = 100f;
        return slider;
    }

    private static HomePageScenePanel BuildAccountProfilePanel(Transform parent)
    {
        HomePageScenePanel scenePanel = CreateScenePanel(parent, "AccountProfileScenePanel", HomePageScenePanelType.AccountProfile);
        Transform root = scenePanel.transform;
        CreateDimmer(root);

        Image shadow = CreateImage("ProfilePanelShadow", root, new Color(0.09f, 0.05f, 0.03f, 0.50f));
        SetTopLeft(shadow.rectTransform, new Vector2(334f, -134f), new Vector2(1280f, 820f));
        shadow.raycastTarget = false;

        Image panel = CreateImage("ProfilePanel", root, new Color(0.43f, 0.23f, 0.11f, 1f));
        SetTopLeft(panel.rectTransform, new Vector2(320f, -120f), new Vector2(1280f, 820f));
        Image paper = CreateImage("ProfilePaper", panel.transform, new Color(1f, 0.94f, 0.76f, 1f));
        SetTopLeft(paper.rectTransform, new Vector2(14f, -14f), new Vector2(1252f, 792f));
        paper.raycastTarget = false;

        Image header = CreateImage("ProfileHeader", panel.transform, new Color(0.52f, 0.22f, 0.16f, 1f));
        SetTopLeft(header.rectTransform, new Vector2(24f, -24f), new Vector2(1232f, 76f));
        TMP_Text title = CreateTMPText("ProfileTitle", panel.transform, "个人信息", 38, Color.white, TextAlignmentOptions.MidlineLeft);
        SetTopLeft(title.rectTransform, new Vector2(58f, -34f), new Vector2(240f, 54f));
        Image modeBadge = CreateImage("ProfileModeBadgeBackground", panel.transform, new Color(0.28f, 0.67f, 0.40f, 1f));
        SetTopLeft(modeBadge.rectTransform, new Vector2(302f, -41f), new Vector2(156f, 42f));
        TMP_Text modeText = CreateTMPText("ProfileModeBadge", modeBadge.transform, "账号登录", 19, Color.white, TextAlignmentOptions.Center);
        Stretch(modeText.rectTransform);
        Button close = CreateTMPButton("ProfileCloseButton", panel.transform, "X", new Color(0.90f, 0.30f, 0.25f, 1f), Color.white, 24);
        SetTopLeft(close.GetComponent<RectTransform>(), new Vector2(1188f, -36f), new Vector2(50f, 50f));

        Image identityCard = CreateImage("ProfileIdentityCard", panel.transform, new Color(1f, 0.84f, 0.58f, 1f));
        SetTopLeft(identityCard.rectTransform, new Vector2(42f, -126f), new Vector2(384f, 548f));
        Image avatar = CreateImage("ProfileAvatarBackground", identityCard.transform, new Color(0.28f, 0.62f, 0.76f, 1f));
        SetTopLeft(avatar.rectTransform, new Vector2(132f, -24f), new Vector2(120f, 120f));
        TMP_Text avatarInitial = CreateTMPText("ProfileAvatarInitial", avatar.transform, "游", 58, Color.white, TextAlignmentOptions.Center);
        Stretch(avatarInitial.rectTransform);

        TMP_Text nicknameLabel = CreateTMPText("ProfileNicknameLabel", identityCard.transform, "账号昵称", 20, new Color(0.40f, 0.23f, 0.11f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(nicknameLabel.rectTransform, new Vector2(24f, -202f), new Vector2(180f, 30f));
        InputField nicknameInput = CreateSceneInputField("ProfileNicknameInput", identityCard.transform, "账号昵称由服务器管理", 18);
        SetTopLeft(nicknameInput.GetComponent<RectTransform>(), new Vector2(24f, -236f), new Vector2(336f, 48f));
        TMP_Text nicknameHint = CreateTMPText("ProfileNicknameHint", identityCard.transform, "昵称长度为 2-12 个字符", 15, new Color(0.46f, 0.39f, 0.30f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(nicknameHint.rectTransform, new Vector2(28f, -287f), new Vector2(328f, 26f));

        Button saveNickname = CreateTMPButton("ProfileSaveNicknameButton", identityCard.transform, "保存昵称", new Color(0.28f, 0.64f, 0.38f, 1f), Color.white, 19);
        SetTopLeft(saveNickname.GetComponent<RectTransform>(), new Vector2(24f, -326f), new Vector2(156f, 46f));
        Button copyId = CreateTMPButton("ProfileCopyIdButton", identityCard.transform, "复制玩家ID", new Color(0.28f, 0.55f, 0.76f, 1f), Color.white, 19);
        SetTopLeft(copyId.GetComponent<RectTransform>(), new Vector2(204f, -386f), new Vector2(156f, 38f));

        TMP_Text identifier = CreateTMPText("ProfileIdentifier", identityCard.transform, "玩家ID  WJ-00000000", 17, new Color(0.32f, 0.20f, 0.11f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(identifier.rectTransform, new Vector2(28f, -392f), new Vector2(170f, 30f));
        TMP_Text account = CreateTMPText("ProfileAccount", identityCard.transform, "登录账号", 17, new Color(0.42f, 0.32f, 0.22f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(account.rectTransform, new Vector2(28f, -430f), new Vector2(328f, 30f));
        TMP_Text level = CreateTMPText("ProfileLevel", identityCard.transform, "领主等级  Lv.1", 18, new Color(0.50f, 0.20f, 0.12f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(level.rectTransform, new Vector2(28f, -468f), new Vector2(328f, 32f));
        TMP_Text cancellationStatus = CreateTMPText("ProfileCancellationStatus", identityCard.transform, "账号注销状态：暂未同步", 15, new Color(0.46f, 0.30f, 0.18f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(cancellationStatus.rectTransform, new Vector2(28f, -510f), new Vector2(328f, 30f));

        Image vitalBar = CreateImage("ProfileVitalBar", panel.transform, new Color(0.98f, 0.78f, 0.52f, 1f));
        SetTopLeft(vitalBar.rectTransform, new Vector2(454f, -306f), new Vector2(746f, 50f));
        TMP_Text vital = CreateTMPText("ProfileVital", vitalBar.transform, "生命  1000    体力  100", 20, new Color(0.42f, 0.19f, 0.09f, 1f), TextAlignmentOptions.Center);
        Stretch(vital.rectTransform);

        TMP_Text growthTitle = CreateTMPText("ProfileGrowthTitle", panel.transform, "成长记录", 27, new Color(0.39f, 0.18f, 0.08f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(growthTitle.rectTransform, new Vector2(454f, -370f), new Vector2(240f, 38f));
        BuildProfileStatCard(panel.transform, "ProfileSignStat", new Vector2(454f, -416f), new Color(0.92f, 0.45f, 0.30f, 1f));
        BuildProfileStatCard(panel.transform, "ProfileFriendStat", new Vector2(711f, -416f), new Color(0.25f, 0.59f, 0.73f, 1f));

        Image syncCard = CreateImage("ProfileSyncCard", panel.transform, new Color(1f, 0.88f, 0.66f, 1f));
        SetTopLeft(syncCard.rectTransform, new Vector2(454f, -560f), new Vector2(746f, 92f));
        TMP_Text syncTitle = CreateTMPText("ProfileSyncTitle", syncCard.transform, "档案状态", 19, new Color(0.42f, 0.20f, 0.10f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(syncTitle.rectTransform, new Vector2(22f, -12f), new Vector2(150f, 30f));
        TMP_Text syncText = CreateTMPText("ProfileSyncText", syncCard.transform, "资料来源  当前登录账号", 17, new Color(0.44f, 0.36f, 0.27f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(syncText.rectTransform, new Vector2(22f, -46f), new Vector2(690f, 28f));

        BuildProfileDetailsCard(panel.transform);

        Image footerDivider = CreateImage("ProfileFooterDivider", panel.transform, new Color(0.48f, 0.27f, 0.13f, 0.48f));
        SetTopLeft(footerDivider.rectTransform, new Vector2(42f, -694f), new Vector2(1196f, 4f));
        footerDivider.raycastTarget = false;
        TMP_Text profileStatus = CreateTMPText("ProfileStatus", panel.transform, "账号资料等待同步", 17, new Color(0.36f, 0.42f, 0.24f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(profileStatus.rectTransform, new Vector2(50f, -716f), new Vector2(650f, 48f));
        Button refresh = CreateTMPButton("ProfileRefreshButton", panel.transform, "本地存档", new Color(0.28f, 0.57f, 0.73f, 1f), Color.white, 19);
        SetTopLeft(refresh.GetComponent<RectTransform>(), new Vector2(730f, -724f), new Vector2(150f, 52f));
        Button cancelAccount = CreateTMPButton("ProfileCancelAccountButton", panel.transform, "注销账号", new Color(0.70f, 0.25f, 0.20f, 1f), Color.white, 18);
        SetTopLeft(cancelAccount.GetComponent<RectTransform>(), new Vector2(900f, -724f), new Vector2(150f, 52f));
        Button switchLogin = CreateTMPButton("ProfileSwitchLoginButton", panel.transform, "切换账号", new Color(0.85f, 0.43f, 0.31f, 1f), Color.white, 19);
        SetTopLeft(switchLogin.GetComponent<RectTransform>(), new Vector2(1070f, -724f), new Vector2(168f, 52f));
        return scenePanel;
    }

    private static void BuildProfileDetailsCard(Transform parent)
    {
        Image card = CreateImage("ProfileDetailsCard", parent, new Color(1f, 0.90f, 0.69f, 1f));
        SetTopLeft(card.rectTransform, new Vector2(454f, -126f), new Vector2(746f, 160f));

        TMP_Text title = CreateProfileTMPText("ProfileDetailsTitle", card.transform, "账号与成长详情", 18, new Color(0.42f, 0.20f, 0.10f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(title.rectTransform, new Vector2(20f, -10f), new Vector2(700f, 26f));

        TMP_Text experience = CreateProfileTMPText("ProfileExperienceText", card.transform, "经验详情\n总经验：0\n本级经验：0/0", 14, new Color(0.40f, 0.31f, 0.22f, 1f), TextAlignmentOptions.TopLeft);
        SetTopLeft(experience.rectTransform, new Vector2(20f, -40f), new Vector2(342f, 48f));

        TMP_Text accountStatus = CreateProfileTMPText("ProfileAccountStatusText", card.transform, "账号状态\n状态：ACTIVE\n可正常进入游戏", 14, new Color(0.40f, 0.31f, 0.22f, 1f), TextAlignmentOptions.TopLeft);
        SetTopLeft(accountStatus.rectTransform, new Vector2(382f, -40f), new Vector2(342f, 48f));

        TMP_Text cancellation = CreateProfileTMPText("ProfileCancellationDetailsText", card.transform, "注销详情\n状态：未申请\n冷静期截止：未设置", 14, new Color(0.40f, 0.31f, 0.22f, 1f), TextAlignmentOptions.TopLeft);
        SetTopLeft(cancellation.rectTransform, new Vector2(20f, -92f), new Vector2(342f, 58f));

        TMP_Text compliance = CreateProfileTMPText("ProfileComplianceText", card.transform, "实名/防沉迷\n实名：UNKNOWN\n防沉迷：等待服务器状态", 14, new Color(0.40f, 0.31f, 0.22f, 1f), TextAlignmentOptions.TopLeft);
        SetTopLeft(compliance.rectTransform, new Vector2(382f, -92f), new Vector2(342f, 58f));
    }

    private static TMP_Text CreateProfileTMPText(string name, Transform parent, string value, int fontSize, Color color, TextAlignmentOptions alignment)
    {
        TMP_Text text = CreateTMPText(name, parent, value, fontSize, color, alignment);
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProfileFontPath)
            ?? roleFontAsset
            ?? TMP_Settings.defaultFontAsset;
        text.fontStyle = FontStyles.Normal;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static void BuildProfileStatCard(Transform parent, string valueName, Vector2 position, Color color)
    {
        Image card = CreateImage(valueName + "Card", parent, new Color(1f, 0.90f, 0.69f, 1f));
        SetTopLeft(card.rectTransform, position, new Vector2(235f, 126f));
        Image stripe = CreateImage(valueName + "Stripe", card.transform, color);
        SetTopLeft(stripe.rectTransform, Vector2.zero, new Vector2(12f, 126f));
        TMP_Text value = CreateTMPText(valueName, card.transform, "成长记录\n0", 21, new Color(0.37f, 0.19f, 0.09f, 1f), TextAlignmentOptions.Center);
        SetTopLeft(value.rectTransform, new Vector2(22f, -10f), new Vector2(200f, 106f));
    }

    private static HomePageScenePanel BuildFriendsPanel(Transform parent)
    {
        HomePageScenePanel scenePanel = CreateScenePanel(parent, "FriendsScenePanel", HomePageScenePanelType.Friends);
        Transform root = scenePanel.transform;
        CreateDimmer(root);

        Image shadow = CreateImage("FriendsPanelShadow", root, new Color(0.09f, 0.05f, 0.03f, 0.50f));
        SetTopLeft(shadow.rectTransform, new Vector2(224f, -134f), new Vector2(1500f, 830f));
        shadow.raycastTarget = false;
        Image panel = CreateImage("FriendsPanel", root, new Color(0.43f, 0.23f, 0.11f, 1f));
        SetTopLeft(panel.rectTransform, new Vector2(210f, -120f), new Vector2(1500f, 830f));
        Image paper = CreateImage("FriendsPaper", panel.transform, new Color(1f, 0.94f, 0.76f, 1f));
        SetTopLeft(paper.rectTransform, new Vector2(14f, -14f), new Vector2(1472f, 802f));
        paper.raycastTarget = false;

        Image header = CreateImage("FriendsHeader", panel.transform, new Color(0.20f, 0.53f, 0.58f, 1f));
        SetTopLeft(header.rectTransform, new Vector2(24f, -24f), new Vector2(1452f, 76f));
        Text title = CreateText("FriendsTitle", panel.transform, "好友名册", 38, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        SetTopLeft(title.rectTransform, new Vector2(58f, -34f), new Vector2(250f, 54f));
        Text subtitle = CreateText("FriendsSubtitle", panel.transform, "正式好友", 18, FontStyle.Bold, new Color(0.90f, 1f, 0.94f, 0.88f), TextAnchor.MiddleLeft);
        SetTopLeft(subtitle.rectTransform, new Vector2(310f, -45f), new Vector2(180f, 32f));
        Text count = CreateText("FriendsCount", panel.transform, "好友 0/0    在线 0", 19, FontStyle.Bold, Color.white, TextAnchor.MiddleRight);
        SetTopLeft(count.rectTransform, new Vector2(1030f, -43f), new Vector2(330f, 40f));
        Button close = CreateButton("FriendsCloseButton", panel.transform, "X", new Color(0.90f, 0.30f, 0.25f, 1f), Color.white, 24);
        SetTopLeft(close.GetComponent<RectTransform>(), new Vector2(1408f, -36f), new Vector2(50f, 50f));

        InputField search = CreateSceneInputField("FriendsSearchInput", panel.transform, "筛选当前列表", 18);
        SetTopLeft(search.GetComponent<RectTransform>(), new Vector2(42f, -122f), new Vector2(270f, 50f));
        Button clearSearch = CreateButton("FriendsClearSearchButton", panel.transform, "X", new Color(0.79f, 0.66f, 0.47f, 1f), Color.white, 18);
        SetTopLeft(clearSearch.GetComponent<RectTransform>(), new Vector2(322f, -122f), new Vector2(46f, 50f));
        Button filterAll = CreateButton("FriendsFilterAll", panel.transform, "好友", new Color(0.20f, 0.56f, 0.66f, 1f), Color.white, 18);
        SetTopLeft(filterAll.GetComponent<RectTransform>(), new Vector2(382f, -122f), new Vector2(90f, 50f));
        Button filterOnline = CreateButton("FriendsFilterOnline", panel.transform, "收到", new Color(0.90f, 0.79f, 0.59f, 1f), new Color(0.36f, 0.20f, 0.10f, 1f), 18);
        SetTopLeft(filterOnline.GetComponent<RectTransform>(), new Vector2(482f, -122f), new Vector2(100f, 50f));
        Button filterOutgoing = CreateButton("FriendsFilterOutgoing", panel.transform, "发出", new Color(0.90f, 0.79f, 0.59f, 1f), new Color(0.36f, 0.20f, 0.10f, 1f), 18);
        SetTopLeft(filterOutgoing.GetComponent<RectTransform>(), new Vector2(592f, -122f), new Vector2(90f, 50f));
        InputField addInput = CreateSceneInputField("FriendsAddInput", panel.transform, "输入玩家 ID", 18);
        SetTopLeft(addInput.GetComponent<RectTransform>(), new Vector2(880f, -122f), new Vector2(246f, 50f));
        Button addButton = CreateButton("FriendsAddButton", panel.transform, "搜索玩家", new Color(0.33f, 0.66f, 0.36f, 1f), Color.white, 19);
        SetTopLeft(addButton.GetComponent<RectTransform>(), new Vector2(1140f, -122f), new Vector2(318f, 50f));

        Image listArea = CreateImage("FriendsListArea", panel.transform, new Color(1f, 0.84f, 0.58f, 1f));
        SetTopLeft(listArea.rectTransform, new Vector2(42f, -196f), new Vector2(870f, 498f));
        for (int i = 0; i < 5; i++)
        {
            BuildFriendRow(listArea.transform, i, new Vector2(14f, -14f - i * 94f));
        }

        Image detailArea = CreateImage("FriendDetailArea", panel.transform, new Color(1f, 0.89f, 0.67f, 1f));
        SetTopLeft(detailArea.rectTransform, new Vector2(936f, -196f), new Vector2(522f, 498f));
        BuildFriendDetail(detailArea.transform);

        Image footerDivider = CreateImage("FriendsFooterDivider", panel.transform, new Color(0.47f, 0.27f, 0.13f, 0.48f));
        SetTopLeft(footerDivider.rectTransform, new Vector2(42f, -714f), new Vector2(1416f, 4f));
        footerDivider.raycastTarget = false;
        Text status = CreateText("FriendsStatus", panel.transform, "好友名册已打开", 17, FontStyle.Bold, new Color(0.36f, 0.42f, 0.24f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(status.rectTransform, new Vector2(50f, -734f), new Vector2(850f, 52f));
        Button previous = CreateButton("FriendsPreviousButton", panel.transform, "<", new Color(0.83f, 0.66f, 0.39f, 1f), Color.white, 24);
        SetTopLeft(previous.GetComponent<RectTransform>(), new Vector2(1092f, -740f), new Vector2(58f, 50f));
        Text pageText = CreateText("FriendsPageText", panel.transform, "1 / 1", 20, FontStyle.Bold, new Color(0.39f, 0.21f, 0.11f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(pageText.rectTransform, new Vector2(1164f, -740f), new Vector2(128f, 50f));
        Button next = CreateButton("FriendsNextButton", panel.transform, ">", new Color(0.83f, 0.66f, 0.39f, 1f), Color.white, 24);
        SetTopLeft(next.GetComponent<RectTransform>(), new Vector2(1306f, -740f), new Vector2(58f, 50f));
        return scenePanel;
    }

    private static void BuildFriendRow(Transform parent, int index, Vector2 position)
    {
        Button row = CreateButton("FriendRow" + index, parent, string.Empty, new Color(1f, 0.95f, 0.78f, 1f), Color.white, 18);
        SetTopLeft(row.GetComponent<RectTransform>(), position, new Vector2(842f, 82f));
        Image avatar = CreateImage("FriendRowAvatarBackground" + index, row.transform, new Color(0.28f, 0.62f, 0.76f, 1f));
        SetTopLeft(avatar.rectTransform, new Vector2(16f, -12f), new Vector2(58f, 58f));
        Text avatarText = CreateText("FriendRowAvatar" + index, avatar.transform, "友", 27, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(avatarText.rectTransform);
        Image onlineMark = CreateImage("FriendRowOnlineMark" + index, row.transform, new Color(0.22f, 0.70f, 0.34f, 1f));
        SetTopLeft(onlineMark.rectTransform, new Vector2(62f, -55f), new Vector2(14f, 14f));
        Text name = CreateText("FriendRowName" + index, row.transform, "好友昵称", 21, FontStyle.Bold, new Color(0.34f, 0.17f, 0.08f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(name.rectTransform, new Vector2(94f, -10f), new Vector2(320f, 32f));
        Text meta = CreateText("FriendRowMeta" + index, row.transform, "Lv.1    亲密度 0", 16, FontStyle.Normal, new Color(0.48f, 0.38f, 0.27f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(meta.rectTransform, new Vector2(94f, -44f), new Vector2(360f, 26f));
        Text state = CreateText("FriendRowState" + index, row.transform, "在线", 17, FontStyle.Bold, new Color(0.25f, 0.52f, 0.30f, 1f), TextAnchor.MiddleRight);
        SetTopLeft(state.rectTransform, new Vector2(618f, -24f), new Vector2(190f, 34f));
    }

    private static HomePageScenePanel BuildRolePanel(Transform parent)
    {
        roleFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProfileFontPath);
        HomePageScenePanel scenePanel = CreateScenePanel(parent, "RoleScenePanel", HomePageScenePanelType.Role);
        Transform root = scenePanel.transform;
        CreateDimmer(root);

        Image shadow = CreateImage("RolePanelShadow", root, new Color(0.08f, 0.04f, 0.02f, 0.50f));
        SetTopLeft(shadow.rectTransform, new Vector2(224f, -134f), new Vector2(1500f, 830f));
        shadow.raycastTarget = false;
        Image panel = CreateImage("RolePanel", root, new Color(0.43f, 0.23f, 0.11f, 1f));
        SetTopLeft(panel.rectTransform, new Vector2(210f, -120f), new Vector2(1500f, 830f));
        Image paper = CreateImage("RolePaper", panel.transform, new Color(1f, 0.94f, 0.76f, 1f));
        SetTopLeft(paper.rectTransform, new Vector2(14f, -14f), new Vector2(1472f, 802f));
        paper.raycastTarget = false;

        Image header = CreateImage("RoleHeader", panel.transform, new Color(0.22f, 0.48f, 0.55f, 1f));
        SetTopLeft(header.rectTransform, new Vector2(24f, -24f), new Vector2(1452f, 78f));
        TMP_Text title = CreateTMPText("RoleTitle", panel.transform, "\u89d2\u8272\u4e2d\u5fc3", 38, Color.white, TextAlignmentOptions.MidlineLeft);
        SetTopLeft(title.rectTransform, new Vector2(58f, -34f), new Vector2(260f, 54f));
        TMP_Text subtitle = CreateTMPText("RoleSubtitle", panel.transform, "\u670d\u52a1\u5668\u89d2\u8272\u6570\u636e", 18, new Color(0.90f, 1f, 0.94f, 0.90f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(subtitle.rectTransform, new Vector2(330f, -45f), new Vector2(250f, 32f));
        Button close = CreateTMPButton("RoleCloseButton", panel.transform, "X", new Color(0.90f, 0.30f, 0.25f, 1f), Color.white, 24);
        SetTopLeft(close.GetComponent<RectTransform>(), new Vector2(1408f, -36f), new Vector2(50f, 50f));

        Button ownedTab = CreateTMPButton("RoleOwnedTab", panel.transform, "\u6211\u7684\u89d2\u8272", new Color(0.20f, 0.56f, 0.66f, 1f), Color.white, 20);
        SetTopLeft(ownedTab.GetComponent<RectTransform>(), new Vector2(42f, -122f), new Vector2(190f, 52f));
        Button catalogTab = CreateTMPButton("RoleCatalogTab", panel.transform, "\u89d2\u8272\u56fe\u9274", new Color(0.90f, 0.79f, 0.59f, 1f), new Color(0.36f, 0.20f, 0.10f, 1f), 20);
        SetTopLeft(catalogTab.GetComponent<RectTransform>(), new Vector2(244f, -122f), new Vector2(190f, 52f));

        Image listCard = CreateImage("RoleListCard", panel.transform, new Color(1f, 0.84f, 0.58f, 1f));
        SetTopLeft(listCard.rectTransform, new Vector2(42f, -188f), new Vector2(470f, 570f));
        TMP_Text listTitle = CreateTMPText("RoleListTitle", panel.transform, "\u89d2\u8272\u5217\u8868", 26, new Color(0.38f, 0.18f, 0.08f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(listTitle.rectTransform, new Vector2(64f, -202f), new Vector2(180f, 36f));

        GameObject viewportObject = CreateRectObject("RoleListViewport", panel.transform);
        SetTopLeft(viewportObject.GetComponent<RectTransform>(), new Vector2(56f, -244f), new Vector2(424f, 490f));
        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 0.91f, 0.70f, 0.72f);
        viewportImage.raycastTarget = true;
        Mask viewportMask = viewportObject.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        GameObject contentObject = CreateRectObject("RoleListContent", viewportObject.transform);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, RoleListRows * 108f);

        ScrollRect scrollRect = viewportObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportObject.GetComponent<RectTransform>();
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.scrollSensitivity = 30f;

        Image scrollbarBackground = CreateImage("RoleListScrollbarBackground", panel.transform, new Color(0.45f, 0.27f, 0.13f, 0.62f));
        SetTopLeft(scrollbarBackground.rectTransform, new Vector2(486f, -244f), new Vector2(16f, 490f));
        Image scrollbarHandle = CreateImage("RoleListScrollbarHandle", scrollbarBackground.transform, new Color(0.22f, 0.48f, 0.55f, 1f));
        SetTopLeft(scrollbarHandle.rectTransform, new Vector2(2f, -2f), new Vector2(12f, 120f));
        Scrollbar scrollbar = scrollbarBackground.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = scrollbarHandle.rectTransform;
        scrollbar.targetGraphic = scrollbarHandle;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scrollRect.verticalScrollbarSpacing = 4f;

        for (int i = 0; i < RoleListRows; i++)
        {
            BuildRoleListRow(contentObject.transform, i);
        }

        Image detailCard = CreateImage("RoleDetailCard", panel.transform, new Color(1f, 0.84f, 0.58f, 1f));
        SetTopLeft(detailCard.rectTransform, new Vector2(532f, -188f), new Vector2(924f, 570f));
        TMP_Text detailTitle = CreateTMPText("RoleDetailTitle", detailCard.transform, "\u89d2\u8272\u8be6\u60c5", 28, new Color(0.38f, 0.18f, 0.08f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(detailTitle.rectTransform, new Vector2(28f, -18f), new Vector2(180f, 38f));

        Image detailIcon = CreateImage("RoleDetailIcon", detailCard.transform, new Color(0.28f, 0.62f, 0.76f, 1f));
        SetTopLeft(detailIcon.rectTransform, new Vector2(34f, -76f), new Vector2(156f, 156f));
        TMP_Text detailIconLabel = CreateTMPText("RoleDetailIconText", detailIcon.transform, "NPC", 24, Color.white, TextAlignmentOptions.Center);
        Stretch(detailIconLabel.rectTransform);
        TMP_Text detailName = CreateTMPText("RoleDetailName", detailCard.transform, "\u89d2\u8272\u540d\u79f0", 30, new Color(0.34f, 0.16f, 0.08f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(detailName.rectTransform, new Vector2(222f, -78f), new Vector2(360f, 42f));
        TMP_Text detailQuality = CreateTMPText("RoleDetailQuality", detailCard.transform, "\u54c1\u8d28  -", 19, new Color(0.72f, 0.31f, 0.18f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(detailQuality.rectTransform, new Vector2(222f, -126f), new Vector2(240f, 30f));
        TMP_Text detailStatus = CreateTMPText("RoleDetailStatus", detailCard.transform, "\u72b6\u6001  \u5f85\u83b7\u5f97", 19, new Color(0.28f, 0.52f, 0.30f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(detailStatus.rectTransform, new Vector2(222f, -162f), new Vector2(360f, 30f));
        TMP_Text detailLevel = CreateTMPText("RoleDetailLevel", detailCard.transform, "\u7b49\u7ea7  Lv.1", 20, new Color(0.46f, 0.23f, 0.10f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(detailLevel.rectTransform, new Vector2(222f, -198f), new Vector2(240f, 32f));

        Image divider = CreateImage("RoleDetailDivider", detailCard.transform, new Color(0.55f, 0.34f, 0.17f, 0.42f));
        SetTopLeft(divider.rectTransform, new Vector2(28f, -246f), new Vector2(868f, 3f));
        divider.raycastTarget = false;
        TMP_Text attribute = CreateTMPText("RoleDetailAttribute", detailCard.transform, "\u6218\u6597\u5c5e\u6027  -", 18, new Color(0.42f, 0.33f, 0.24f, 1f), TextAlignmentOptions.TopLeft);
        SetTopLeft(attribute.rectTransform, new Vector2(34f, -270f), new Vector2(420f, 44f));
        TMP_Text production = CreateTMPText("RoleDetailProduction", detailCard.transform, "\u4ea7\u51fa\u52a0\u6210  0%", 18, new Color(0.42f, 0.33f, 0.24f, 1f), TextAlignmentOptions.TopLeft);
        SetTopLeft(production.rectTransform, new Vector2(470f, -270f), new Vector2(410f, 44f));
        TMP_Text description = CreateTMPText("RoleDetailDescription", detailCard.transform, "\u89d2\u8272\u63cf\u8ff0", 17, new Color(0.42f, 0.33f, 0.24f, 1f), TextAlignmentOptions.TopLeft);
        SetTopLeft(description.rectTransform, new Vector2(34f, -330f), new Vector2(846f, 76f));
        TMP_Text acquisition = CreateTMPText("RoleDetailAcquisition", detailCard.transform, "\u83b7\u53d6\u65b9\u5f0f  -", 17, new Color(0.48f, 0.39f, 0.29f, 1f), TextAlignmentOptions.TopLeft);
        SetTopLeft(acquisition.rectTransform, new Vector2(34f, -418f), new Vector2(846f, 50f));

        Button upgrade = CreateTMPButton("RoleDetailUpgradeButton", detailCard.transform, "\u5347\u7ea7", new Color(0.28f, 0.64f, 0.38f, 1f), Color.white, 20);
        SetTopLeft(upgrade.GetComponent<RectTransform>(), new Vector2(34f, -492f), new Vector2(230f, 52f));
        Button acquire = CreateTMPButton("RoleDetailAcquireButton", detailCard.transform, "\u524d\u5f80\u83b7\u53d6", new Color(0.32f, 0.56f, 0.72f, 1f), Color.white, 20);
        SetTopLeft(acquire.GetComponent<RectTransform>(), new Vector2(282f, -492f), new Vector2(230f, 52f));

        TMP_Text empty = CreateTMPText("RoleEmptyText", detailCard.transform, "\u8bf7\u5148\u9009\u62e9\u4e00\u540d\u89d2\u8272", 24, new Color(0.46f, 0.36f, 0.25f, 1f), TextAlignmentOptions.Center);
        SetTopLeft(empty.rectTransform, new Vector2(240f, -220f), new Vector2(420f, 48f));
        empty.gameObject.SetActive(false);

        Image footerDivider = CreateImage("RoleFooterDivider", panel.transform, new Color(0.47f, 0.27f, 0.13f, 0.48f));
        SetTopLeft(footerDivider.rectTransform, new Vector2(42f, -776f), new Vector2(1416f, 4f));
        footerDivider.raycastTarget = false;
        TMP_Text status = CreateTMPText("RoleStatus", panel.transform, "\u6b63\u5728\u7b49\u5f85\u89d2\u8272\u6570\u636e", 17, new Color(0.36f, 0.42f, 0.24f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(status.rectTransform, new Vector2(50f, -792f), new Vector2(1300f, 30f));
        return scenePanel;
    }

    private static void BuildRoleListRow(Transform parent, int index)
    {
        Button row = CreateTMPButton("RoleListRow" + index, parent, string.Empty, new Color(1f, 0.95f, 0.78f, 1f), Color.white, 16);
        SetTopLeft(row.GetComponent<RectTransform>(), new Vector2(6f, -6f - index * 108f), new Vector2(412f, 98f));
        Image icon = CreateImage("RoleListRowIcon" + index, row.transform, new Color(0.28f, 0.62f, 0.76f, 1f));
        SetTopLeft(icon.rectTransform, new Vector2(12f, -10f), new Vector2(78f, 78f));
        TMP_Text iconLabel = CreateTMPText("RoleListRowIconText" + index, icon.transform, "NPC", 14, Color.white, TextAlignmentOptions.Center);
        Stretch(iconLabel.rectTransform);
        TMP_Text name = CreateTMPText("RoleListRowName" + index, row.transform, "\u89d2\u8272\u540d\u79f0", 18, new Color(0.34f, 0.17f, 0.08f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(name.rectTransform, new Vector2(104f, -12f), new Vector2(180f, 28f));
        TMP_Text quality = CreateTMPText("RoleListRowQuality" + index, row.transform, "\u54c1\u8d28  -", 14, new Color(0.72f, 0.31f, 0.18f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(quality.rectTransform, new Vector2(104f, -43f), new Vector2(180f, 24f));
        TMP_Text level = CreateTMPText("RoleListRowLevel" + index, row.transform, "Lv.1", 15, new Color(0.46f, 0.23f, 0.10f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(level.rectTransform, new Vector2(104f, -70f), new Vector2(100f, 22f));
        TMP_Text state = CreateTMPText("RoleListRowStatus" + index, row.transform, "\u5f85\u83b7\u5f97", 14, new Color(0.28f, 0.52f, 0.30f, 1f), TextAlignmentOptions.MidlineRight);
        SetTopLeft(state.rectTransform, new Vector2(286f, -36f), new Vector2(112f, 28f));
    }

    private static Button CreateTMPButton(string name, Transform parent, string value, Color backgroundColor, Color textColor, int fontSize)
    {
        Image background = CreateImage(name, parent, backgroundColor);
        background.raycastTarget = true;
        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.94f, 0.82f, 1f);
        colors.pressedColor = new Color(0.82f, 0.74f, 0.62f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.68f, 0.68f, 0.68f, 0.62f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        if (!string.IsNullOrEmpty(value))
        {
            TMP_Text label = CreateTMPText("Text", background.transform, value, fontSize, textColor, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
        }

        return button;
    }

    private static TMP_Text CreateTMPText(string name, Transform parent, string value, int fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        target.layer = LayerMask.NameToLayer("UI");
        target.transform.SetParent(parent, false);
        TextMeshProUGUI label = target.GetComponent<TextMeshProUGUI>();
        label.font = roleFontAsset != null ? roleFontAsset : TMP_Settings.defaultFontAsset;
        label.text = value;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Normal;
        label.color = color;
        label.alignment = alignment;
        label.enableAutoSizing = false;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    private static HomePageScenePanel BuildEquipmentPanel(Transform parent)
    {
        HomePageScenePanel scenePanel = CreateScenePanel(parent, "EquipmentScenePanel", HomePageScenePanelType.Equipment);
        Transform root = scenePanel.transform;
        CreateDimmer(root);

        Image shadow = CreateImage("EquipmentPanelShadow", root, new Color(0.09f, 0.05f, 0.03f, 0.50f));
        SetTopLeft(shadow.rectTransform, new Vector2(224f, -134f), new Vector2(1500f, 830f));
        shadow.raycastTarget = false;
        Image panel = CreateImage("EquipmentPanel", root, new Color(0.43f, 0.23f, 0.11f, 1f));
        SetTopLeft(panel.rectTransform, new Vector2(210f, -120f), new Vector2(1500f, 830f));
        Image paper = CreateImage("EquipmentPaper", panel.transform, new Color(1f, 0.94f, 0.76f, 1f));
        SetTopLeft(paper.rectTransform, new Vector2(14f, -14f), new Vector2(1472f, 802f));
        paper.raycastTarget = false;

        Image header = CreateImage("EquipmentHeader", panel.transform, new Color(0.42f, 0.24f, 0.50f, 1f));
        SetTopLeft(header.rectTransform, new Vector2(24f, -24f), new Vector2(1452f, 78f));
        Text title = CreateText("EquipmentTitle", panel.transform, "装备大厅", 38, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        SetTopLeft(title.rectTransform, new Vector2(58f, -34f), new Vector2(250f, 54f));
        Text mode = CreateText("EquipmentModeText", panel.transform, "账号库存 · 可穿戴", 18, FontStyle.Bold, new Color(0.94f, 0.90f, 1f, 0.90f), TextAnchor.MiddleLeft);
        SetTopLeft(mode.rectTransform, new Vector2(330f, -45f), new Vector2(280f, 32f));
        Text power = CreateText("EquipmentPowerText", panel.transform, "总战力  10", 22, FontStyle.Bold, new Color(1f, 0.87f, 0.45f, 1f), TextAnchor.MiddleRight);
        SetTopLeft(power.rectTransform, new Vector2(970f, -39f), new Vector2(240f, 40f));
        Text count = CreateText("EquipmentCount", panel.transform, "装备 0 件", 17, FontStyle.Bold, Color.white, TextAnchor.MiddleRight);
        SetTopLeft(count.rectTransform, new Vector2(770f, -45f), new Vector2(180f, 32f));
        Button refresh = CreateButton("EquipmentRefreshButton", panel.transform, "刷新背包", new Color(0.27f, 0.64f, 0.43f, 1f), Color.white, 17);
        SetTopLeft(refresh.GetComponent<RectTransform>(), new Vector2(1222f, -38f), new Vector2(160f, 48f));
        Button close = CreateButton("EquipmentCloseButton", panel.transform, "X", new Color(0.90f, 0.30f, 0.25f, 1f), Color.white, 24);
        SetTopLeft(close.GetComponent<RectTransform>(), new Vector2(1408f, -36f), new Vector2(50f, 50f));

        Image slotsCard = CreateImage("EquipmentSlotsCard", panel.transform, new Color(1f, 0.84f, 0.58f, 1f));
        SetTopLeft(slotsCard.rectTransform, new Vector2(42f, -160f), new Vector2(470f, 540f));
        Text slotsTitle = CreateText("EquipmentSlotsTitle", panel.transform, "装备栏", 27, FontStyle.Bold, new Color(0.38f, 0.18f, 0.08f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(slotsTitle.rectTransform, new Vector2(64f, -178f), new Vector2(180f, 36f));
        BuildEquipmentSlot(panel.transform, "weapon", "武器", new Vector2(62f, -222f), new Color(0.83f, 0.38f, 0.24f, 1f));
        BuildEquipmentSlot(panel.transform, "armor", "防具", new Vector2(287f, -222f), new Color(0.28f, 0.55f, 0.76f, 1f));
        BuildEquipmentSlot(panel.transform, "accessory", "饰品", new Vector2(62f, -430f), new Color(0.38f, 0.65f, 0.40f, 1f));
        BuildEquipmentSlot(panel.transform, "pet", "灵宠", new Vector2(287f, -430f), new Color(0.68f, 0.38f, 0.72f, 1f));

        Image listCard = CreateImage("EquipmentListCard", panel.transform, new Color(1f, 0.89f, 0.67f, 1f));
        SetTopLeft(listCard.rectTransform, new Vector2(532f, -160f), new Vector2(430f, 540f));
        Text listTitle = CreateText("EquipmentListTitle", panel.transform, "装备物品", 27, FontStyle.Bold, new Color(0.38f, 0.18f, 0.08f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(listTitle.rectTransform, new Vector2(554f, -178f), new Vector2(180f, 36f));
        string[] categoryIds = { "all", "weapon", "armor", "accessory", "pet" };
        string[] categoryLabels = { "全部", "武器", "防具", "饰品", "灵宠" };
        for (int i = 0; i < categoryIds.Length; i++)
        {
            BuildEquipmentCategoryButton(
                panel.transform,
                categoryIds[i],
                categoryLabels[i],
                new Vector2(548f + i * 82f, -216f));
        }
        for (int i = 0; i < EquipmentRows; i++)
        {
            BuildEquipmentRow(panel.transform, i, new Vector2(546f, -266f - i * 70f));
        }

        Image detailCard = CreateImage("EquipmentDetailCard", panel.transform, new Color(1f, 0.84f, 0.58f, 1f));
        SetTopLeft(detailCard.rectTransform, new Vector2(984f, -160f), new Vector2(474f, 540f));
        GameObject detailEmpty = CreateRectObject("EquipmentDetailEmpty", detailCard.transform);
        Stretch(detailEmpty.GetComponent<RectTransform>());
        Text emptyTitle = CreateText("EquipmentDetailEmptyTitle", detailEmpty.transform, "选择一件装备", 28, FontStyle.Bold, new Color(0.46f, 0.36f, 0.25f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(emptyTitle.rectTransform, new Vector2(48f, -190f), new Vector2(378f, 48f));
        Text emptyHint = CreateText("EquipmentDetailEmptyHint", detailEmpty.transform, "查看属性并穿戴到对应栏位", 17, FontStyle.Normal, new Color(0.52f, 0.45f, 0.36f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(emptyHint.rectTransform, new Vector2(48f, -248f), new Vector2(378f, 34f));
        detailEmpty.SetActive(false);

        GameObject detailContent = CreateRectObject("EquipmentDetailContent", detailCard.transform);
        Stretch(detailContent.GetComponent<RectTransform>());
        Image detailIconBackground = CreateImage("EquipmentDetailIconBackground", detailContent.transform, new Color(0.38f, 0.65f, 0.40f, 1f));
        SetTopLeft(detailIconBackground.rectTransform, new Vector2(182f, -26f), new Vector2(110f, 110f));
        Text detailIcon = CreateText("EquipmentDetailIcon", detailIconBackground.transform, "装", 48, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(detailIcon.rectTransform);
        Text detailName = CreateText("EquipmentDetailName", detailContent.transform, "装备名称", 28, FontStyle.Bold, new Color(0.34f, 0.16f, 0.08f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(detailName.rectTransform, new Vector2(40f, -150f), new Vector2(394f, 40f));
        Text detailType = CreateText("EquipmentDetailType", detailContent.transform, "武器 · 数量 x1", 17, FontStyle.Normal, new Color(0.48f, 0.39f, 0.29f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(detailType.rectTransform, new Vector2(40f, -194f), new Vector2(394f, 28f));
        Image descriptionCard = CreateImage("EquipmentDescriptionCard", detailContent.transform, new Color(1f, 0.93f, 0.76f, 1f));
        SetTopLeft(descriptionCard.rectTransform, new Vector2(28f, -232f), new Vector2(418f, 64f));
        Text description = CreateText("EquipmentDetailDescription", descriptionCard.transform, "装备描述", 16, FontStyle.Normal, new Color(0.42f, 0.33f, 0.24f, 1f), TextAnchor.MiddleCenter);
        Stretch(description.rectTransform);

        BuildEquipmentStatChip(detailContent.transform, "EquipmentDetailAttack", "攻击  +0", new Vector2(28f, -312f), new Color(0.85f, 0.39f, 0.27f, 1f));
        BuildEquipmentStatChip(detailContent.transform, "EquipmentDetailDefense", "防御  +0", new Vector2(244f, -312f), new Color(0.28f, 0.55f, 0.76f, 1f));
        BuildEquipmentStatChip(detailContent.transform, "EquipmentDetailHealth", "生命  +0", new Vector2(28f, -376f), new Color(0.35f, 0.66f, 0.38f, 1f));
        BuildEquipmentStatChip(detailContent.transform, "EquipmentDetailPower", "战力  +0", new Vector2(244f, -376f), new Color(0.65f, 0.42f, 0.72f, 1f));
        Button action = CreateButton("EquipmentActionButton", detailContent.transform, "穿戴装备", new Color(0.28f, 0.64f, 0.38f, 1f), Color.white, 20);
        SetTopLeft(action.GetComponent<RectTransform>(), new Vector2(136f, -454f), new Vector2(202f, 50f));
        Text detailStatus = CreateText("EquipmentDetailStatus", detailContent.transform, "选择穿戴后会替换同一栏位的装备", 15, FontStyle.Normal, new Color(0.45f, 0.36f, 0.27f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(detailStatus.rectTransform, new Vector2(28f, -510f), new Vector2(418f, 28f));

        Image footerDivider = CreateImage("EquipmentFooterDivider", panel.transform, new Color(0.47f, 0.27f, 0.13f, 0.48f));
        SetTopLeft(footerDivider.rectTransform, new Vector2(42f, -714f), new Vector2(1416f, 4f));
        footerDivider.raycastTarget = false;
        Text status = CreateText("EquipmentStatus", panel.transform, "账号装备等待同步", 17, FontStyle.Bold, new Color(0.36f, 0.42f, 0.24f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(status.rectTransform, new Vector2(50f, -728f), new Vector2(850f, 30f));
        Text hint = CreateText("EquipmentHint", panel.transform, "点击装备栏可卸下，选择物品后可穿戴", 15, FontStyle.Normal, new Color(0.48f, 0.39f, 0.29f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(hint.rectTransform, new Vector2(50f, -758f), new Vector2(1060f, 28f));
        Text attack = CreateText("EquipmentAttackText", panel.transform, "攻击  10", 16, FontStyle.Bold, new Color(0.85f, 0.39f, 0.27f, 1f), TextAnchor.MiddleRight);
        SetTopLeft(attack.rectTransform, new Vector2(1120f, -728f), new Vector2(110f, 28f));
        Text defense = CreateText("EquipmentDefenseText", panel.transform, "防御  8", 16, FontStyle.Bold, new Color(0.28f, 0.55f, 0.76f, 1f), TextAnchor.MiddleRight);
        SetTopLeft(defense.rectTransform, new Vector2(1235f, -728f), new Vector2(100f, 28f));
        Text health = CreateText("EquipmentHealthText", panel.transform, "生命  100", 16, FontStyle.Bold, new Color(0.35f, 0.66f, 0.38f, 1f), TextAnchor.MiddleRight);
        SetTopLeft(health.rectTransform, new Vector2(1340f, -728f), new Vector2(120f, 28f));
        Text powerDetail = CreateText("EquipmentPowerDetailText", panel.transform, "战力  28", 16, FontStyle.Bold, new Color(0.65f, 0.42f, 0.72f, 1f), TextAnchor.MiddleRight);
        SetTopLeft(powerDetail.rectTransform, new Vector2(1260f, -758f), new Vector2(200f, 28f));
        return scenePanel;
    }

    private static void BuildEquipmentSlot(Transform parent, string slotId, string label, Vector2 position, Color color)
    {
        string suffix = EquipmentPascalCase(slotId);
        Button slot = CreateButton("EquipmentSlot" + suffix, parent, string.Empty, new Color(0.86f, 0.72f, 0.48f, 1f), Color.white, 16);
        SetTopLeft(slot.GetComponent<RectTransform>(), position, new Vector2(205f, 190f));
        Image iconBackground = CreateImage("EquipmentSlotIconBackground" + suffix, slot.transform, color);
        SetTopLeft(iconBackground.rectTransform, new Vector2(64f, -16f), new Vector2(76f, 76f));
        Text icon = CreateText("EquipmentSlotIcon" + suffix, iconBackground.transform, slotId == "weapon" ? "剑" : slotId == "armor" ? "甲" : slotId == "pet" ? "宠" : "饰", 34, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(icon.rectTransform);
        Text name = CreateText("EquipmentSlotName" + suffix, slot.transform, "空装备位", 18, FontStyle.Bold, new Color(0.34f, 0.17f, 0.08f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(name.rectTransform, new Vector2(12f, -104f), new Vector2(181f, 30f));
        Text state = CreateText("EquipmentSlotState" + suffix, slot.transform, label, 15, FontStyle.Normal, new Color(0.48f, 0.38f, 0.27f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(state.rectTransform, new Vector2(12f, -140f), new Vector2(181f, 26f));
        GameObject empty = CreateRectObject("EquipmentSlotEmpty" + suffix, slot.transform);
        SetTopLeft(empty.GetComponent<RectTransform>(), new Vector2(12f, -164f), new Vector2(181f, 22f));
        Text emptyText = CreateText("Text", empty.transform, "点击栏位可卸下", 12, FontStyle.Normal, new Color(0.56f, 0.44f, 0.30f, 0.82f), TextAnchor.MiddleCenter);
        Stretch(emptyText.rectTransform);
        empty.SetActive(false);
    }

    private static void BuildEquipmentCategoryButton(Transform parent, string id, string label, Vector2 position)
    {
        Button button = CreateButton(
            "EquipmentCategory" + EquipmentPascalCase(id),
            parent,
            label,
            new Color(0.90f, 0.79f, 0.59f, 1f),
            new Color(0.36f, 0.20f, 0.10f, 1f),
            15);
        SetTopLeft(button.GetComponent<RectTransform>(), position, new Vector2(76f, 38f));
    }

    private static void BuildEquipmentRow(Transform parent, int index, Vector2 position)
    {
        Button row = CreateButton("EquipmentItemRow" + index, parent, string.Empty, new Color(1f, 0.94f, 0.74f, 1f), Color.white, 16);
        SetTopLeft(row.GetComponent<RectTransform>(), position, new Vector2(402f, 66f));
        Image iconBackground = CreateImage("EquipmentRowIconBackground" + index, row.transform, new Color(0.38f, 0.65f, 0.40f, 1f));
        SetTopLeft(iconBackground.rectTransform, new Vector2(10f, -8f), new Vector2(50f, 50f));
        Text icon = CreateText("EquipmentRowIcon" + index, iconBackground.transform, "装", 23, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(icon.rectTransform);
        Text name = CreateText("EquipmentRowName" + index, row.transform, "装备名称", 17, FontStyle.Bold, new Color(0.34f, 0.17f, 0.08f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(name.rectTransform, new Vector2(72f, -8f), new Vector2(180f, 26f));
        Text meta = CreateText("EquipmentRowMeta" + index, row.transform, "武器  x1", 13, FontStyle.Normal, new Color(0.48f, 0.38f, 0.27f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(meta.rectTransform, new Vector2(72f, -36f), new Vector2(180f, 22f));
        Text equipped = CreateText("EquipmentRowEquipped" + index, row.transform, "可穿戴", 13, FontStyle.Bold, new Color(0.25f, 0.55f, 0.30f, 1f), TextAnchor.MiddleRight);
        SetTopLeft(equipped.rectTransform, new Vector2(274f, -22f), new Vector2(112f, 26f));
    }

    private static void BuildEquipmentStatChip(Transform parent, string name, string value, Vector2 position, Color color)
    {
        Image chip = CreateImage(name + "Chip", parent, new Color(1f, 0.93f, 0.74f, 1f));
        SetTopLeft(chip.rectTransform, position, new Vector2(202f, 52f));
        Image mark = CreateImage(name + "Mark", chip.transform, color);
        SetTopLeft(mark.rectTransform, new Vector2(12f, -12f), new Vector2(28f, 28f));
        Text text = CreateText(name, chip.transform, value, 16, FontStyle.Bold, new Color(0.38f, 0.19f, 0.09f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(text.rectTransform, new Vector2(52f, -8f), new Vector2(138f, 32f));
    }

    private static string EquipmentPascalCase(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : char.ToUpper(value[0]) + value.Substring(1);
    }

    private static void BuildFriendDetail(Transform parent)
    {
        GameObject empty = CreateRectObject("FriendDetailEmpty", parent);
        Stretch(empty.GetComponent<RectTransform>());
        Text emptyTitle = CreateText("FriendDetailEmptyTitle", empty.transform, "暂无好友", 28, FontStyle.Bold, new Color(0.46f, 0.36f, 0.25f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(emptyTitle.rectTransform, new Vector2(72f, -176f), new Vector2(378f, 48f));
        Text emptyHint = CreateText("FriendDetailEmptyHint", empty.transform, "添加好友后可查看档案", 17, FontStyle.Normal, new Color(0.52f, 0.45f, 0.36f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(emptyHint.rectTransform, new Vector2(72f, -232f), new Vector2(378f, 34f));
        empty.SetActive(false);

        GameObject content = CreateRectObject("FriendDetailContent", parent);
        Stretch(content.GetComponent<RectTransform>());
        Image avatar = CreateImage("FriendDetailAvatarBackground", content.transform, new Color(0.28f, 0.62f, 0.76f, 1f));
        SetTopLeft(avatar.rectTransform, new Vector2(201f, -24f), new Vector2(120f, 120f));
        Text avatarText = CreateText("FriendDetailAvatar", avatar.transform, "友", 56, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(avatarText.rectTransform);
        Text name = CreateText("FriendDetailName", content.transform, "好友昵称", 29, FontStyle.Bold, new Color(0.34f, 0.16f, 0.08f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(name.rectTransform, new Vector2(52f, -156f), new Vector2(418f, 40f));
        Text id = CreateText("FriendDetailId", content.transform, "好友编号  WJ-1001", 16, FontStyle.Normal, new Color(0.48f, 0.39f, 0.29f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(id.rectTransform, new Vector2(52f, -200f), new Vector2(418f, 28f));
        Text level = CreateText("FriendDetailLevel", content.transform, "领主等级  Lv.1", 18, FontStyle.Bold, new Color(0.46f, 0.23f, 0.10f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(level.rectTransform, new Vector2(52f, -234f), new Vector2(418f, 30f));
        Text online = CreateText("FriendDetailOnline", content.transform, "当前在线", 17, FontStyle.Bold, new Color(0.23f, 0.55f, 0.30f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(online.rectTransform, new Vector2(52f, -270f), new Vector2(418f, 30f));
        Text intimacy = CreateText("FriendDetailIntimacy", content.transform, "亲密度  0", 18, FontStyle.Bold, new Color(0.70f, 0.31f, 0.24f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(intimacy.rectTransform, new Vector2(52f, -306f), new Vector2(418f, 30f));
        Image divider = CreateImage("FriendDetailDivider", content.transform, new Color(0.55f, 0.34f, 0.17f, 0.42f));
        SetTopLeft(divider.rectTransform, new Vector2(34f, -350f), new Vector2(454f, 3f));
        divider.raycastTarget = false;
        Text giftHint = CreateText("FriendGiftHint", content.transform, "今日可赠送 5 点体力", 17, FontStyle.Normal, new Color(0.46f, 0.37f, 0.27f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(giftHint.rectTransform, new Vector2(52f, -370f), new Vector2(418f, 30f));
        Button gift = CreateButton("FriendsGiftButton", content.transform, "赠送体力", new Color(0.32f, 0.66f, 0.38f, 1f), Color.white, 19);
        SetTopLeft(gift.GetComponent<RectTransform>(), new Vector2(72f, -416f), new Vector2(174f, 50f));
        Button delete = CreateButton("FriendsDeleteButton", content.transform, "删除好友", new Color(0.84f, 0.38f, 0.30f, 1f), Color.white, 19);
        SetTopLeft(delete.GetComponent<RectTransform>(), new Vector2(276f, -416f), new Vector2(174f, 50f));
        Button requestPrimary = CreateButton("FriendsRequestPrimaryButton", content.transform, "同意", new Color(0.32f, 0.66f, 0.38f, 1f), Color.white, 19);
        SetTopLeft(requestPrimary.GetComponent<RectTransform>(), new Vector2(72f, -416f), new Vector2(174f, 50f));
        Button requestSecondary = CreateButton("FriendsRequestSecondaryButton", content.transform, "拒绝", new Color(0.84f, 0.38f, 0.30f, 1f), Color.white, 19);
        SetTopLeft(requestSecondary.GetComponent<RectTransform>(), new Vector2(276f, -416f), new Vector2(174f, 50f));
    }

    private static InputField CreateSceneInputField(string name, Transform parent, string placeholder, int fontSize)
    {
        Image frame = CreateImage(name, parent, new Color(1f, 0.98f, 0.88f, 1f));
        frame.raycastTarget = true;
        InputField input = frame.gameObject.AddComponent<InputField>();
        input.targetGraphic = frame;
        input.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = input.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.94f, 0.78f, 1f);
        colors.pressedColor = new Color(0.92f, 0.85f, 0.70f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.78f, 0.76f, 0.70f, 0.72f);
        input.colors = colors;
        input.lineType = InputField.LineType.SingleLine;

        Text value = CreateText("Text", frame.transform, string.Empty, fontSize, FontStyle.Normal, new Color(0.24f, 0.16f, 0.09f, 1f), TextAnchor.MiddleLeft);
        value.enableAutoSizing = false;
        value.enableWordWrapping = true;
        value.overflowMode = TextOverflowModes.Truncate;
        value.rectTransform.anchorMin = Vector2.zero;
        value.rectTransform.anchorMax = Vector2.one;
        value.rectTransform.offsetMin = new Vector2(14f, 4f);
        value.rectTransform.offsetMax = new Vector2(-14f, -4f);
        input.textComponent = value;

        Text hint = CreateText("Placeholder", frame.transform, placeholder, Mathf.Max(14, fontSize - 2), FontStyle.Normal, new Color(0.52f, 0.47f, 0.40f, 0.82f), TextAnchor.MiddleLeft);
        hint.enableAutoSizing = false;
        hint.rectTransform.anchorMin = Vector2.zero;
        hint.rectTransform.anchorMax = Vector2.one;
        hint.rectTransform.offsetMin = new Vector2(14f, 4f);
        hint.rectTransform.offsetMax = new Vector2(-14f, -4f);
        input.placeholder = hint;
        return input;
    }

    private static HomePageScenePanel BuildMailPanel(Transform parent)
    {
        HomePageScenePanel scenePanel = CreateScenePanel(parent, "MailScenePanel", HomePageScenePanelType.Mail);
        Transform root = scenePanel.transform;
        CreateDimmer(root);

        Image panel = CreateImage("SystemMailPanel", root, new Color(1f, 0.95f, 0.77f, 1f));
        SetTopLeft(panel.rectTransform, new Vector2(310f, -128f), new Vector2(1290f, 765f));
        AddAirmailEdge(panel.transform, new Vector2(1290f, 765f));
        Text icon = CreateText("MailIcon", panel.transform, "✉", 46, FontStyle.Bold, new Color(0.48f, 0.18f, 0.12f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(icon.rectTransform, new Vector2(48f, -38f), new Vector2(58f, 58f));
        Text title = CreateText("MailTitle", panel.transform, "系统邮件", 42, FontStyle.Bold, new Color(0.34f, 0.17f, 0.10f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(title.rectTransform, new Vector2(112f, -42f), new Vector2(280f, 56f));
        Button close = CreateButton("SystemMailCloseButton", panel.transform, "X", new Color(0.92f, 0.26f, 0.36f, 1f), Color.white, 24);
        SetTopLeft(close.GetComponent<RectTransform>(), new Vector2(1218f, -34f), new Vector2(52f, 52f));

        Image list = CreateImage("MailListArea", panel.transform, new Color(1f, 0.84f, 0.58f, 0.95f));
        SetTopLeft(list.rectTransform, new Vector2(56f, -128f), new Vector2(384f, 555f));
        Image listHeader = CreateImage("MailListHeader", list.transform, new Color(0.82f, 0.32f, 0.28f, 1f));
        SetTopLeft(listHeader.rectTransform, new Vector2(16f, -16f), new Vector2(352f, 54f));
        Text listTitle = CreateText("MailListTitle", listHeader.transform, "邮件列表", 24, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        SetTopLeft(listTitle.rectTransform, new Vector2(18f, -7f), new Vector2(150f, 40f));
        Button claimAll = CreateButton("ClaimAllButton", listHeader.transform, "一键领取", new Color(1f, 0.73f, 0.25f, 1f), new Color(0.44f, 0.16f, 0.06f, 1f), 18);
        SetTopLeft(claimAll.GetComponent<RectTransform>(), new Vector2(212f, -7f), new Vector2(124f, 40f));

        Image empty = CreateImage("MailEmptyCard", list.transform, new Color(1f, 0.95f, 0.76f, 1f));
        SetTopLeft(empty.rectTransform, new Vector2(26f, -96f), new Vector2(332f, 132f));
        Text emptyText = CreateText("MailEmptyText", empty.transform, "暂无邮件", 24, FontStyle.Bold, new Color(0.42f, 0.41f, 0.38f, 1f), TextAnchor.MiddleCenter);
        Stretch(emptyText.rectTransform);
        for (int i = 0; i < 5; i++)
        {
            BuildMailItem(list.transform, i, new Vector2(26f, -96f - i * 84f));
        }

        Image detail = CreateImage("MailDetailArea", panel.transform, new Color(1f, 0.98f, 0.88f, 0.98f));
        SetTopLeft(detail.rectTransform, new Vector2(470f, -128f), new Vector2(620f, 555f));
        Text detailTitle = CreateText("MailDetailTitle", detail.transform, "系统提示", 30, FontStyle.Bold, new Color(0.33f, 0.15f, 0.08f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(detailTitle.rectTransform, new Vector2(30f, -28f), new Vector2(548f, 48f));
        Image divider = CreateImage("MailDetailDivider", detail.transform, new Color(0.78f, 0.42f, 0.26f, 0.55f));
        SetTopLeft(divider.rectTransform, new Vector2(28f, -92f), new Vector2(564f, 4f));
        Text detailBody = CreateText("MailDetailBody", detail.transform, "当前没有可展示的系统邮件。", 20, FontStyle.Normal, new Color(0.11f, 0.12f, 0.14f, 1f), TextAnchor.UpperLeft);
        SetTopLeft(detailBody.rectTransform, new Vector2(32f, -116f), new Vector2(552f, 215f));
        Text rewardTitle = CreateText("MailRewardTitle", detail.transform, "奖励", 24, FontStyle.Bold, new Color(0.76f, 0.22f, 0.15f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(rewardTitle.rectTransform, new Vector2(32f, -350f), new Vector2(140f, 38f));
        BuildMailReward(detail.transform, new Vector2(32f, -400f), "金币", "x100");
        BuildMailReward(detail.transform, new Vector2(154f, -400f), "木材", "x80");
        BuildMailReward(detail.transform, new Vector2(276f, -400f), "石材", "x80");
        Button claim = CreateButton("ClaimMailButton", detail.transform, "领取", new Color(0.83f, 0.30f, 0.23f, 1f), Color.white, 22);
        SetTopLeft(claim.GetComponent<RectTransform>(), new Vector2(446f, -475f), new Vector2(126f, 48f));

        BuildMailTab(panel.transform, "MailTabSystem", "系统", new Vector2(1118f, -146f), true);
        BuildMailTab(panel.transform, "MailTabReward", "奖励", new Vector2(1118f, -226f), false);
        BuildMailTab(panel.transform, "MailTabNotice", "公告", new Vector2(1118f, -306f), false);
        Text status = CreateText("MailStatus", panel.transform, "已读取系统邮件", 17, FontStyle.Bold, new Color(0.48f, 0.25f, 0.12f, 0.88f), TextAnchor.MiddleLeft);
        SetTopLeft(status.rectTransform, new Vector2(72f, -706f), new Vector2(930f, 34f));
        return scenePanel;
    }

    private static void BuildMailItem(Transform parent, int index, Vector2 position)
    {
        Button card = CreateButton("MailItem" + index, parent, string.Empty, index == 0 ? new Color(1f, 0.92f, 0.45f, 1f) : new Color(1f, 0.96f, 0.78f, 1f), Color.white, 16);
        SetTopLeft(card.GetComponent<RectTransform>(), position, new Vector2(332f, 72f));
        Image mark = CreateImage("UnreadMark", card.transform, new Color(0.90f, 0.22f, 0.16f, 1f));
        SetTopLeft(mark.rectTransform, new Vector2(14f, -20f), new Vector2(14f, 14f));
        Text title = CreateText("MailItemTitle", card.transform, "系统邮件", 19, FontStyle.Bold, new Color(0.11f, 0.12f, 0.14f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(title.rectTransform, new Vector2(38f, -10f), new Vector2(258f, 28f));
        Text meta = CreateText("MailItemMeta", card.transform, "系统  无附件", 15, FontStyle.Normal, new Color(0.42f, 0.41f, 0.38f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(meta.rectTransform, new Vector2(38f, -42f), new Vector2(258f, 24f));
    }

    private static void BuildMailReward(Transform parent, Vector2 position, string label, string amount)
    {
        Image slot = CreateImage("MailRewardSlot", parent, new Color(1f, 0.88f, 0.55f, 1f));
        SetTopLeft(slot.rectTransform, position, new Vector2(98f, 82f));
        Text text = CreateText("MailRewardText", slot.transform, $"{label}\n{amount}", 15, FontStyle.Bold, new Color(0.36f, 0.16f, 0.07f, 1f), TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
    }

    private static void BuildMailTab(Transform parent, string name, string label, Vector2 position, bool selected)
    {
        Button tab = CreateButton(
            name,
            parent,
            "✉  " + label,
            selected ? new Color(0.95f, 0.42f, 0.32f, 1f) : new Color(0.96f, 0.77f, 0.57f, 1f),
            selected ? Color.white : new Color(0.45f, 0.18f, 0.10f, 1f),
            18);
        SetTopLeft(tab.GetComponent<RectTransform>(), position, new Vector2(134f, 56f));
    }

    private static HomePageScenePanel CreateScenePanel(Transform parent, string name, HomePageScenePanelType type)
    {
        GameObject target = CreateRectObject(name, parent);
        RectTransform rect = target.GetComponent<RectTransform>();
        Stretch(rect);
        HomePageScenePanel panel = target.AddComponent<HomePageScenePanel>();
        panel.EditorSetReferences(type, rect);
        return panel;
    }

    private static void CreateDimmer(Transform parent)
    {
        Image dimmer = CreateImage("Dimmer", parent, new Color(0.05f, 0.035f, 0.02f, 0.56f));
        Stretch(dimmer.rectTransform);
        dimmer.raycastTarget = true;
    }

    private static void AddSimpleFrame(Transform parent, Vector2 size, Color color)
    {
        Image top = CreateImage("FrameTop", parent, color);
        SetTopLeft(top.rectTransform, Vector2.zero, new Vector2(size.x, 22f));
        Image bottom = CreateImage("FrameBottom", parent, color);
        SetTopLeft(bottom.rectTransform, new Vector2(0f, -size.y + 22f), new Vector2(size.x, 22f));
        Image left = CreateImage("FrameLeft", parent, color);
        SetTopLeft(left.rectTransform, Vector2.zero, new Vector2(22f, size.y));
        Image right = CreateImage("FrameRight", parent, color);
        SetTopLeft(right.rectTransform, new Vector2(size.x - 22f, 0f), new Vector2(22f, size.y));
    }

    private static void AddAirmailEdge(Transform parent, Vector2 size)
    {
        Color red = new Color(0.84f, 0.20f, 0.20f, 1f);
        Color blue = new Color(0.18f, 0.40f, 0.70f, 1f);
        for (int i = 0; i < 25; i++)
        {
            Image top = CreateImage("AirmailTop" + i, parent, i % 2 == 0 ? red : blue);
            SetTopLeft(top.rectTransform, new Vector2(18f + i * 50f, -14f), new Vector2(32f, 12f));
            Image bottom = CreateImage("AirmailBottom" + i, parent, i % 2 == 0 ? red : blue);
            SetTopLeft(bottom.rectTransform, new Vector2(18f + i * 50f, -size.y + 26f), new Vector2(32f, 12f));
        }
    }

    private static Transform FindTransformByName(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == name)
            {
                return transforms[i];
            }
        }

        return null;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == name)
            {
                return roots[i];
            }
        }

        return null;
    }

    private static bool EnsureTestNode(Scene scene)
    {
        if (FindRoot(scene, TestNodeName) != null)
        {
            return false;
        }

        GameObject testNode = new GameObject(TestNodeName);
        SceneManager.MoveGameObjectToScene(testNode, scene);
        EditorUtility.SetDirty(testNode);
        return true;
    }

    private static bool RemoveLegacyHomeCanvas(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root.name != "Canvas" || root.activeSelf || root.GetComponent<Canvas>() == null)
            {
                continue;
            }

            Object.DestroyImmediate(root);
            Debug.Log("Removed legacy inactive HomeScene Canvas hierarchy.");
            return true;
        }

        return false;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        target.layer = LayerMask.NameToLayer("UI");
        target.transform.SetParent(parent, false);
        Image image = target.GetComponent<Image>();
        image.color = color;
        image.type = Image.Type.Simple;
        return image;
    }

    private static Button CreateButton(string name, Transform parent, string value, Color backgroundColor, Color textColor, int fontSize)
    {
        Image background = CreateImage(name, parent, backgroundColor);
        background.raycastTarget = true;
        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.94f, 0.82f, 1f);
        colors.pressedColor = new Color(0.82f, 0.74f, 0.62f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.68f, 0.68f, 0.68f, 0.62f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        if (!string.IsNullOrEmpty(value))
        {
            Text label = CreateText("Text", background.transform, value, fontSize, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
        }

        return button;
    }

    private static Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle style, Color color, TextAnchor alignment)
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        target.layer = LayerMask.NameToLayer("UI");
        target.transform.SetParent(parent, false);
        Text label = target.GetComponent<Text>();
        Font resolvedFont = ResolveFont(value);
        label.font = resolvedFont;
        label.text = value;
        label.fontSize = fontSize;
        label.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : style == FontStyle.Italic ? FontStyles.Italic : style == FontStyle.BoldAndItalic ? FontStyles.Bold | FontStyles.Italic : FontStyles.Normal;
        label.color = color;
        label.alignment = ConvertTextAlignment(alignment);
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Min(12, fontSize);
        label.fontSizeMax = fontSize;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Truncate;
        label.raycastTarget = false;
        return label;
    }

    private static Font ResolveFont(string value)
    {
        return defaultFont ?? fallbackFont ?? RuntimeFontAssetProvider.Resolve();
    }

    private static GameObject CreateRectObject(string name, Transform parent)
    {
        GameObject target = new GameObject(name, typeof(RectTransform));
        target.layer = LayerMask.NameToLayer("UI");
        target.transform.SetParent(parent, false);
        return target;
    }

    private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
