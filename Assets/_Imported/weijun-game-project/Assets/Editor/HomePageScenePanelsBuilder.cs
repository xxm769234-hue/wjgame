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
    private const string RoleFontPath = "Assets/_Imported/weijun-game-project/Assets/Sprites/font/SourceHanSansSC-Normal SDF.asset";
    private const string ProfileFontPath = "Assets/_Imported/weijun-game-project/Assets/Sprites/font/SourceHanSansSC-Normal SDF.asset";
    private const int RoleListRows = 1;
    private const string FontPath = "Assets/_Imported/weijun-game-project/Assets/Sprites/font/SourceHanSansSC-Normal SDF.asset";
    private const string TaskPanelBackgroundPath = "Assets/_Project/Art/UI/二级界面/二级界面背景.png";
    private const string TaskArtDirectory = "Assets/_Project/Art/UI/二级界面/任务二级界面/";
    private const string TaskCloseSpritePath = "Assets/_Project/Art/UI/三级界面/关闭按钮.png";
    private const string TaskActionSpritePath = "Assets/_Project/Art/UI/二级界面/仓库二级界面/按钮.png";
    private const string TaskActionDisabledSpritePath = "Assets/_Project/Art/UI/二级界面/仓库二级界面/灰阶按钮.png";
    private const string SecondaryPanelBackgroundPath = "Assets/_Project/Art/UI/二级界面/二级界面背景.png";
    private const string SecondaryHeaderPath = "Assets/_Project/Art/UI/二级界面/任务二级界面/顶部信息栏.png";
    private const string SecondaryCloseSpritePath = "Assets/_Project/Art/UI/三级界面/关闭按钮.png";
    private const string SecondaryActionSpritePath = "Assets/_Project/Art/UI/二级界面/任务二级界面/左侧信息栏.png";
    private const string SecondaryActionDisabledSpritePath = "Assets/_Project/Art/UI/二级界面/任务二级界面/左侧信息栏（无点击）.png";
    private static readonly string[] EquipmentIconBindingKeys =
    {
        "COMMON:weapon", "COMMON:helmet", "COMMON:armor", "COMMON:boots", "COMMON:gloves",
        "FINE:weapon", "FINE:helmet", "FINE:armor", "FINE:boots", "FINE:gloves",
        "EXCELLENT:weapon", "EXCELLENT:helmet", "EXCELLENT:armor", "EXCELLENT:boots", "EXCELLENT:gloves",
        "EPIC:weapon", "EPIC:helmet", "EPIC:armor", "EPIC:boots", "EPIC:gloves",
        "LEGENDARY:weapon", "LEGENDARY:helmet", "LEGENDARY:armor", "LEGENDARY:boots", "LEGENDARY:gloves",
        "short_sword", "starter_sword", "\u77ed\u5251", "\u65b0\u624b\u77ed\u5251",
        "helmet", "starter_helmet", "\u5934\u76d4", "\u65b0\u624b\u5934\u76d4",
        "armor", "starter_armor", "\u94e0\u7532", "\u65b0\u624b\u94e0\u7532"
    };
    private static readonly string[] EquipmentIconBindingPaths =
    {
        "Assets/_Project/Art/UI/装备图标/普通级/武器.png",
        "Assets/_Project/Art/UI/装备图标/普通级/头盔.png",
        "Assets/_Project/Art/UI/装备图标/普通级/胸甲.png",
        "Assets/_Project/Art/UI/装备图标/普通级/鞋子.png",
        "Assets/_Project/Art/UI/装备图标/普通级/手套.png",
        "Assets/_Project/Art/UI/装备图标/优秀级/武器1.png",
        "Assets/_Project/Art/UI/装备图标/优秀级/头盔.png",
        "Assets/_Project/Art/UI/装备图标/优秀级/护甲.png",
        "Assets/_Project/Art/UI/装备图标/优秀级/鞋子.png",
        "Assets/_Project/Art/UI/装备图标/优秀级/手套.png",
        "Assets/_Project/Art/UI/装备图标/稀有级/武器.png",
        "Assets/_Project/Art/UI/装备图标/稀有级/头盔.png",
        "Assets/_Project/Art/UI/装备图标/稀有级/护甲.png",
        "Assets/_Project/Art/UI/装备图标/稀有级/鞋子.png",
        "Assets/_Project/Art/UI/装备图标/稀有级/手套.png",
        "Assets/_Project/Art/UI/装备图标/史诗级/武器.png",
        "Assets/_Project/Art/UI/装备图标/史诗级/头盔.png",
        "Assets/_Project/Art/UI/装备图标/史诗级/护甲.png",
        "Assets/_Project/Art/UI/装备图标/史诗级/鞋子.png",
        "Assets/_Project/Art/UI/装备图标/史诗级/手套.png",
        "Assets/_Project/Art/UI/装备图标/传说级/橙金/武器1.png",
        "Assets/_Project/Art/UI/装备图标/传说级/橙金/头盔.png",
        "Assets/_Project/Art/UI/装备图标/传说级/橙金/胸甲.png",
        "Assets/_Project/Art/UI/装备图标/传说级/橙金/鞋子.png",
        "Assets/_Project/Art/UI/装备图标/传说级/橙金/手套.png",
        "Assets/_Project/Art/UI/装备图标/普通级/武器.png",
        "Assets/_Project/Art/UI/装备图标/普通级/武器.png",
        "Assets/_Project/Art/UI/装备图标/普通级/武器.png",
        "Assets/_Project/Art/UI/装备图标/普通级/武器.png",
        "Assets/_Project/Art/UI/装备图标/普通级/头盔.png",
        "Assets/_Project/Art/UI/装备图标/普通级/头盔.png",
        "Assets/_Project/Art/UI/装备图标/普通级/头盔.png",
        "Assets/_Project/Art/UI/装备图标/普通级/头盔.png",
        "Assets/_Project/Art/UI/装备图标/普通级/胸甲.png",
        "Assets/_Project/Art/UI/装备图标/普通级/胸甲.png",
        "Assets/_Project/Art/UI/装备图标/普通级/胸甲.png",
        "Assets/_Project/Art/UI/装备图标/普通级/胸甲.png"
    };

    private static Font defaultFont;
    private static Font fallbackFont;
    private static TMP_FontAsset roleFontAsset;
    private const string ResourceIconDirectory = "Assets/_Project/Art/UI/\u8D44\u6E90\u56FE\u6807/";

    [InitializeOnLoadMethod]
    private static void RegisterEquipmentIconBindingSync()
    {
        EditorApplication.delayCall += SyncLoadedEquipmentIconBindings;
        EditorSceneManager.sceneOpened -= SyncEquipmentIconBindingsOnSceneOpened;
        EditorSceneManager.sceneOpened += SyncEquipmentIconBindingsOnSceneOpened;
        EditorApplication.playModeStateChanged -= SyncEquipmentIconBindingsOnPlayModeChanged;
        EditorApplication.playModeStateChanged += SyncEquipmentIconBindingsOnPlayModeChanged;
    }

    private static void SyncEquipmentIconBindingsOnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        SyncEquipmentIconBindings(scene);
    }

    private static void SyncEquipmentIconBindingsOnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += SyncLoadedEquipmentIconBindings;
        }
    }

    [MenuItem("Tools/WeiJun UI/Sync Equipment Icon Bindings")]
    private static void SyncLoadedEquipmentIconBindings()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetSceneByPath(HomeScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        SyncEquipmentIconBindings(scene);
    }

    private static void SyncEquipmentIconBindings(Scene scene)
    {
        bool changed = false;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            HomePageUI[] controllers = roots[rootIndex].GetComponentsInChildren<HomePageUI>(true);
            for (int controllerIndex = 0; controllerIndex < controllers.Length; controllerIndex++)
            {
                changed |= SyncEquipmentIconBindings(controllers[controllerIndex]);
            }
        }

        if (changed) EditorSceneManager.MarkSceneDirty(scene);
    }

    private static bool SyncEquipmentIconBindings(HomePageUI controller)
    {
        if (controller == null) return false;
        SerializedObject serializedController = new SerializedObject(controller);
        SerializedProperty bindings = serializedController.FindProperty("equipmentIconBindings");
        if (bindings == null || !bindings.isArray) return false;

        bool changed = false;
        for (int mappingIndex = 0; mappingIndex < EquipmentIconBindingKeys.Length; mappingIndex++)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(EquipmentIconBindingPaths[mappingIndex]);
            if (sprite == null) continue;

            SerializedProperty binding = null;
            for (int bindingIndex = 0; bindingIndex < bindings.arraySize; bindingIndex++)
            {
                SerializedProperty candidate = bindings.GetArrayElementAtIndex(bindingIndex);
                SerializedProperty candidateKey = candidate.FindPropertyRelative("iconKey");
                if (candidateKey != null && candidateKey.stringValue == EquipmentIconBindingKeys[mappingIndex])
                {
                    binding = candidate;
                    break;
                }
            }

            bool addedBinding = binding == null;
            if (addedBinding)
            {
                bindings.arraySize++;
                binding = bindings.GetArrayElementAtIndex(bindings.arraySize - 1);
                binding.FindPropertyRelative("iconKey").stringValue = EquipmentIconBindingKeys[mappingIndex];
                changed = true;
            }

            SerializedProperty spriteProperty = binding.FindPropertyRelative("sprite");
            bool preserveAssignedAlias = EquipmentIconBindingKeys[mappingIndex].IndexOf(':') < 0
                && !addedBinding && spriteProperty != null && spriteProperty.objectReferenceValue != null;
            if (preserveAssignedAlias) continue;
            if (spriteProperty != null && spriteProperty.objectReferenceValue != sprite)
            {
                spriteProperty.objectReferenceValue = sprite;
                changed = true;
            }
        }

        if (!changed) return false;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
        return true;
    }

    [InitializeOnLoadMethod]
    private static void RegisterSignMakeupDialogSync()
    {
        EditorApplication.delayCall += SyncLoadedSignMakeupDialogs;
        EditorSceneManager.sceneOpened -= SyncSignMakeupDialogsOnSceneOpened;
        EditorSceneManager.sceneOpened += SyncSignMakeupDialogsOnSceneOpened;
    }

    private static void SyncSignMakeupDialogsOnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        SyncLoadedSignMakeupDialogs();
    }

    [MenuItem("Tools/WeiJun UI/Sync Sign Makeup Dialogs")]
    private static void SyncLoadedSignMakeupDialogs()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetSceneByPath(HomeScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        GameObject canvasObject = FindRoot(scene, CanvasName);
        if (canvasObject != null && EnsureSignMakeupDialogs(canvasObject.transform))
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    private static bool EnsureSignMakeupDialogs(Transform parent)
    {
        bool changed = false;
        HomePageScenePanel daily = FindExistingScenePanel(parent, HomePageScenePanelType.DailySign);
        HomePageScenePanel newbie = FindExistingScenePanel(parent, HomePageScenePanelType.NewbieSign);
        if (daily != null) changed |= BuildSignMakeupDialog(daily.transform);
        if (newbie != null) changed |= BuildSignMakeupDialog(newbie.transform);
        return changed;
    }

    [InitializeOnLoadMethod]
    private static void RegisterNewbieSignIconMigration()
    {
        EditorApplication.delayCall += SyncLoadedNewbieSignRewardIcons;
        EditorSceneManager.sceneOpened -= SyncNewbieSignRewardIconsOnSceneOpened;
        EditorSceneManager.sceneOpened += SyncNewbieSignRewardIconsOnSceneOpened;
    }

    private static void SyncNewbieSignRewardIconsOnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        SyncLoadedNewbieSignRewardIcons();
    }

    private static void SyncLoadedNewbieSignRewardIcons()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene scene = SceneManager.GetSceneByPath(HomeScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        GameObject canvasObject = FindRoot(scene, CanvasName);
        if (canvasObject != null && SyncNewbieSignRewardIcons(canvasObject.transform))
        {
            // Do not save other unsaved scene edits during automatic migration.
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    [InitializeOnLoadMethod]
    private static void RegisterFriendCopyButtonSync()
    {
        EditorApplication.delayCall += SyncLoadedFriendCopyButtons;
        EditorSceneManager.sceneOpened -= SyncFriendCopyButtonOnSceneOpened;
        EditorSceneManager.sceneOpened += SyncFriendCopyButtonOnSceneOpened;
        EditorApplication.playModeStateChanged -= SyncFriendCopyButtonOnPlayModeChanged;
        EditorApplication.playModeStateChanged += SyncFriendCopyButtonOnPlayModeChanged;
    }

    private static void SyncFriendCopyButtonOnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += SyncLoadedFriendCopyButtons;
        }
    }

    private static void SyncFriendCopyButtonOnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        SyncLoadedFriendCopyButtons();
    }

    [MenuItem("Tools/WeiJun UI/Sync Friend Copy ID Button")]
    [MenuItem("Tools/WeiJun UI/Sync Friend Dialogs")]
    private static void SyncLoadedFriendCopyButtons()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetSceneByPath(HomeScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;

        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
        {
            foreach (HomePageScenePanel panel in sceneRoot.GetComponentsInChildren<HomePageScenePanel>(true))
            {
                if (panel.PanelType != HomePageScenePanelType.Friends) continue;
                if (HomePageUI.EnsureFriendRequestsEntry(panel.PanelRoot)) EditorSceneManager.MarkSceneDirty(scene);
                if (HomePageUI.EnsureFriendSearchDialog(panel.PanelRoot)) EditorSceneManager.MarkSceneDirty(scene);
                if (HomePageUI.EnsureFriendRequestsDialog(panel.PanelRoot)) EditorSceneManager.MarkSceneDirty(scene);
                foreach (string oldButtonName in new[] { "FriendsRequestPrimaryButton", "FriendsRequestSecondaryButton", "FriendsFilterOutgoing" })
                {
                    Transform oldButton = FindTransformByName(panel.transform, oldButtonName);
                    if (oldButton == null) continue;
                    Undo.DestroyObjectImmediate(oldButton.gameObject);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
                Transform content = FindTransformByName(panel.transform, "FriendDetailContent");
                if (content == null || FindTransformByName(content, "FriendCopyIdButton") != null) continue;
                Transform idNode = FindTransformByName(content, "FriendDetailId");
                RectTransform idRect = idNode as RectTransform;
                if (idRect == null) continue;

                Button copyButton = CreateTMPButton("FriendCopyIdButton", content, "复制", Color.white, Color.white, 16);
                Undo.RegisterCreatedObjectUndo(copyButton.gameObject, "Add friend ID copy button");
                Image image = copyButton.GetComponent<Image>();
                Transform giftNode = FindTransformByName(content, "FriendsGiftButton");
                Image giftImage = giftNode != null ? giftNode.GetComponent<Image>() : null;
                if (image != null && giftImage != null)
                {
                    image.sprite = giftImage.sprite;
                    image.type = giftImage.type;
                    image.color = giftImage.color;
                    image.preserveAspect = false;
                }

                RectTransform copyRect = copyButton.GetComponent<RectTransform>();
                Text idLabel = idNode.GetComponent<Text>();
                float labelWidth = idLabel != null ? Mathf.Min(idRect.rect.width, idLabel.preferredWidth) : idRect.rect.width;
                copyRect.anchorMin = idRect.anchorMin;
                copyRect.anchorMax = idRect.anchorMin;
                copyRect.pivot = new Vector2(0f, 0.5f);
                copyRect.sizeDelta = new Vector2(76f, 36f);
                copyRect.anchoredPosition = idRect.anchoredPosition + new Vector2(
                    -idRect.pivot.x * idRect.rect.width + labelWidth + 12f,
                    (0.5f - idRect.pivot.y) * idRect.rect.height);
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("Added FriendDetailContent/FriendCopyIdButton to the open HomeScene. Save the scene to keep it.");
            }
        }
    }

    [InitializeOnLoadMethod]
    private static void UpdateSecondaryPanelStyleAfterReload()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            const string secondaryPanelStyleKey = "WeiJun.HomeScenePanels.SecondaryPanelStyleCheck.1";
            if (SessionState.GetBool(secondaryPanelStyleKey, false))
            {
                return;
            }

            UpdateSecondaryPanelStyles();
            SessionState.SetBool(secondaryPanelStyleKey, true);
        };
    }

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
                else if (registry == null)
                {
                    BuildScenePanels(scene, true);
                }
                else if (registry.SchemaVersion != ScenePanelsSchemaVersion)
                {
                    registry.EditorSetSchemaVersion(ScenePanelsSchemaVersion);
                    EditorUtility.SetDirty(registry);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                else if (addedTestNode)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }

                string secondaryPanelStyleKey = "WeiJun.HomeScenePanels.SecondaryPanelStyleCheck.1";
                if (registry != null
                    && registry.SchemaVersion == ScenePanelsSchemaVersion
                    && !SessionState.GetBool(secondaryPanelStyleKey, false))
                {
                    if (ApplySecondaryPanelStyles(registry))
                    {
                        EditorUtility.SetDirty(registry);
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                    SessionState.SetBool(secondaryPanelStyleKey, true);
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

    [MenuItem("Tools/WeiJun UI/Sync Newbie Sign Reward Icons")]
    public static void SyncNewbieSignRewardIconsInHomeScene()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene scene = SceneManager.GetSceneByPath(HomeScenePath);
        bool openedForUpdate = !scene.IsValid() || !scene.isLoaded;
        if (openedForUpdate)
        {
            scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Additive);
        }

        try
        {
            GameObject canvasObject = FindRoot(scene, CanvasName);
            if (canvasObject == null)
            {
                Debug.LogWarning("HomePanelsCanvas was not found in HomeScene.");
                return;
            }

            if (!SyncNewbieSignRewardIcons(canvasObject.transform))
            {
                Debug.Log("Newbie sign reward icons are already synchronized.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Newbie sign reward icons synchronized without changing layout.");
        }
        finally
        {
            if (openedForUpdate && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    [MenuItem("Tools/WeiJun UI/Update Task Panel Style")]
    public static void UpdateTaskPanelStyle()
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
            HomePageScenePanels registry = canvasObject != null
                ? canvasObject.GetComponent<HomePageScenePanels>()
                : null;
            HomePageScenePanel taskPanel = registry != null
                ? registry.Get(HomePageScenePanelType.Task)
                : null;
            if (taskPanel == null)
            {
                Debug.LogWarning("TaskScenePanel was not found in HomeScene.");
                return;
            }

            ApplyTaskPanelStyle(taskPanel.transform);
            EditorUtility.SetDirty(taskPanel);
            EditorUtility.SetDirty(registry);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Updated TaskScenePanel style without rebuilding other home panels.");
        }
        finally
        {
            if (openedForUpdate && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    [MenuItem("Tools/WeiJun UI/Update Role Panel Style")]
    public static void UpdateRolePanelStyle()
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
            HomePageScenePanels registry = canvasObject != null
                ? canvasObject.GetComponent<HomePageScenePanels>()
                : null;
            HomePageScenePanel rolePanel = registry != null
                ? registry.Get(HomePageScenePanelType.Role)
                : null;
            if (rolePanel == null)
            {
                Debug.LogWarning("RoleScenePanel was not found in HomeScene.");
                return;
            }

            Selection.activeGameObject = rolePanel.gameObject;
            Debug.Log("RoleScenePanel style is controlled by the Unity scene and was left unchanged.");
        }
        finally
        {
            if (openedForUpdate && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    [MenuItem("Tools/WeiJun UI/Update Equipment Lottery Friends Shop Styles")]
    public static void UpdateSecondaryPanelStyles()
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
            HomePageScenePanels registry = canvasObject != null
                ? canvasObject.GetComponent<HomePageScenePanels>()
                : null;
            if (registry == null)
            {
                Debug.LogWarning("HomePanelsCanvas or HomePageScenePanels was not found in HomeScene.");
                return;
            }

            bool changed = ApplySecondaryPanelStyles(registry);

            if (changed)
            {
                EditorUtility.SetDirty(registry);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log("Updated Friends panel styles. LotteryScenePanel and ShopScenePanel keep their Unity scene appearance.");
        }
        finally
        {
            if (openedForUpdate && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static bool ApplySecondaryPanelStyles(HomePageScenePanels registry)
    {
        if (registry == null)
        {
            return false;
        }

        HomePageScenePanelType[] panelTypes =
        {
            HomePageScenePanelType.Friends
        };

        bool changed = false;
        for (int i = 0; i < panelTypes.Length; i++)
        {
            HomePageScenePanel panel = registry.Get(panelTypes[i]);
            if (panel == null)
            {
                Debug.LogWarning(panelTypes[i] + " panel was not found in HomeScene.");
                continue;
            }

            ApplySecondaryPanelStyle(panel.transform, panelTypes[i].ToString());
            EditorUtility.SetDirty(panel);
            changed = true;
        }

        return changed;
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

    [MenuItem("Tools/WeiJun UI/Apply RoleListRow1 Template")]
    public static void ApplyRoleListRow1Template()
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
            HomePageScenePanels registry = canvasObject != null
                ? canvasObject.GetComponent<HomePageScenePanels>()
                : null;
            HomePageScenePanel rolePanel = registry != null
                ? registry.Get(HomePageScenePanelType.Role)
                : null;
            Transform content = rolePanel == null
                ? null
                : FindTransformByName(rolePanel.transform, "RoleListContent");
            Transform template = content == null ? null : FindTransformByName(content, "RoleListRow1");
            if (content == null || template == null)
            {
                Debug.LogWarning("RoleListContent or RoleListRow1 was not found in HomeScene.");
                return;
            }

            int rowIndex = 1;
            bool changed = false;
            while (true)
            {
                Transform row = content.Find("RoleListRow" + rowIndex);
                if (row == null)
                {
                    break;
                }

                rowIndex++;
            }

            if (changed)
            {
                EditorUtility.SetDirty(rolePanel);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Selection.activeGameObject = rolePanel.gameObject;
            Debug.Log(changed
                ? "Applied RoleListRow1 components and HeadImage to the role list rows."
                : "RoleListRow1 remains the runtime template; existing scene rows were left unchanged.");
        }
        finally
        {
            if (openedForUpdate && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void RenameRoleRowTemplateChildren(Transform row, int rowIndex)
    {
        Transform[] children = row.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] == row)
            {
                continue;
            }

            string childName = children[i].name;
            if (childName.EndsWith("0", System.StringComparison.Ordinal))
            {
                children[i].name = childName.Substring(0, childName.Length - 1) + rowIndex;
            }
        }
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
                "ProfileGoldCard",
                "ProfileWoodCard",
                "ProfileStoneCard",
                "ProfileIronCard"
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

            }

            Transform copyButton = FindTransformByName(profileRoot, "ProfileCopyIdButton");
            if (copyButton != null)
            {
                SetProfileButtonText(copyButton, "复制玩家ID");
            }

            Transform nicknameInput = FindTransformByName(profileRoot, "ProfileNicknameInput");
            TMP_FontAsset profileFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/_Imported/weijun-game-project/Assets/Sprites/font/SourceHanSansSC-Normal SDF.asset");
            ConvertProfileTextComponents(profileRoot, nicknameInput, profileFont);

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
            HomePageScenePanels existingRegistry = existing.GetComponent<HomePageScenePanels>();
            if (existingRegistry == null)
            {
                existingRegistry = existing.AddComponent<HomePageScenePanels>();
            }

            SyncNewbieSignRewardIcons(existing.transform);
            EnsureSignMakeupDialogs(existing.transform);
            EnsureDailySignScrollView(existing.transform);
            EnsureActivityRewardPagingButtons(existing.transform);

            HomePageScenePanelType[] panelTypes =
            {
                HomePageScenePanelType.Task,
                HomePageScenePanelType.Inventory,
                HomePageScenePanelType.Recharge,
                HomePageScenePanelType.Lottery,
                HomePageScenePanelType.DailySign,
                HomePageScenePanelType.NewbieSign,
                HomePageScenePanelType.Mail,
                HomePageScenePanelType.Settings,
                HomePageScenePanelType.AccountProfile,
                HomePageScenePanelType.Friends,
                HomePageScenePanelType.Equipment,
                HomePageScenePanelType.Shop,
                HomePageScenePanelType.Role
            };

            for (int i = 0; i < panelTypes.Length; i++)
            {
                if (existingRegistry.Get(panelTypes[i]) != null)
                {
                    continue;
                }

                HomePageScenePanel existingPanel = FindExistingScenePanel(existing.transform, panelTypes[i]);
                if (existingPanel != null)
                {
                    existingRegistry.EditorAppendPanel(existingPanel, ScenePanelsSchemaVersion);
                    EditorUtility.SetDirty(existingRegistry);
                    continue;
                }

                HomePageScenePanel missingPanel = BuildMissingScenePanel(existing.transform, panelTypes[i]);
                if (missingPanel == null)
                {
                    continue;
                }

                missingPanel.SetVisible(false);
                existingRegistry.EditorAppendPanel(missingPanel, ScenePanelsSchemaVersion);
                EditorUtility.SetDirty(missingPanel);
            }

            existingRegistry.EditorSetSchemaVersion(ScenePanelsSchemaVersion);
            EditorUtility.SetDirty(existingRegistry);
            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene)
            {
                EditorSceneManager.SaveScene(scene);
            }

            Selection.activeGameObject = existing;
            Debug.Log("Home scene panels updated without replacing existing Unity scene nodes.");
            return;
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

    private static HomePageScenePanel FindExistingScenePanel(Transform parent, HomePageScenePanelType panelType)
    {
        if (parent == null)
        {
            return null;
        }

        HomePageScenePanel[] scenePanels = parent.GetComponentsInChildren<HomePageScenePanel>(true);
        for (int i = 0; i < scenePanels.Length; i++)
        {
            HomePageScenePanel scenePanel = scenePanels[i];
            if (scenePanel != null && scenePanel.PanelType == panelType)
            {
                return scenePanel;
            }
        }

        return null;
    }

    private static bool SyncNewbieSignRewardIcons(Transform parent)
    {
        HomePageScenePanel panel = FindExistingScenePanel(parent, HomePageScenePanelType.NewbieSign);
        if (panel == null)
        {
            return false;
        }

        bool changed = false;
        string[] iconNames = { "金币", "木材", "宝箱（未打开）" };
        for (int i = 0; i < iconNames.Length; i++)
        {
            Transform card = FindTransformByName(panel.transform, "NewbieSignCard" + (i + 1));
            Transform placeholder = card == null ? null : FindTransformByName(card, "RewardIconText");
            if (placeholder == null || placeholder.GetComponent<Image>() != null)
            {
                continue;
            }

            TMP_Text tmp = placeholder.GetComponent<TMP_Text>();
            LegacyText legacy = placeholder.GetComponent<LegacyText>();
            if (tmp == null && legacy == null)
            {
                continue;
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ResourceIconDirectory + iconNames[i] + ".png");
            if (sprite == null)
            {
                Debug.LogWarning("Newbie sign reward sprite not found: " + iconNames[i]);
                continue;
            }

            // Convert only legacy placeholders. Never recreate deleted nodes or overwrite an edited Image.
            if (tmp != null) Undo.DestroyObjectImmediate(tmp);
            if (legacy != null) Undo.DestroyObjectImmediate(legacy);
            Undo.RecordObject(placeholder.gameObject, "Convert newbie sign reward icon");
            placeholder.name = "RewardResourceImage";
            Image image = Undo.AddComponent<Image>(placeholder.gameObject);
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            EditorUtility.SetDirty(image);
            changed = true;
        }

        return changed;
    }

    private static HomePageScenePanel BuildMissingScenePanel(Transform parent, HomePageScenePanelType panelType)
    {
        switch (panelType)
        {
            case HomePageScenePanelType.Task:
                return BuildTaskPanel(parent);
            case HomePageScenePanelType.Inventory:
                return BuildInventoryPanel(parent);
            case HomePageScenePanelType.Recharge:
                return BuildRechargePanel(parent);
            case HomePageScenePanelType.Lottery:
                return BuildLotteryPanel(parent);
            case HomePageScenePanelType.DailySign:
                return BuildDailySignPanel(parent);
            case HomePageScenePanelType.NewbieSign:
                return BuildNewbieSignPanel(parent);
            case HomePageScenePanelType.Mail:
                return BuildMailPanel(parent);
            case HomePageScenePanelType.Settings:
                return BuildSettingsPanel(parent);
            case HomePageScenePanelType.AccountProfile:
                return BuildAccountProfilePanel(parent);
            case HomePageScenePanelType.Friends:
                return BuildFriendsPanel(parent);
            case HomePageScenePanelType.Equipment:
                return BuildEquipmentPanel(parent);
            case HomePageScenePanelType.Shop:
                return BuildShopPanel(parent);
            case HomePageScenePanelType.Role:
                return BuildRolePanel(parent);
            default:
                return null;
        }
    }

    private static RectTransform BuildBookHomeCanvas(Transform parent)
    {
        Transform existing = parent.Find("BookHomeCanvas");
        bool hasExistingBookLayout = existing != null
            && existing.Find("MainBookPage") != null
            && existing.GetComponent<HomePageBookView>() != null;
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
        if (!hasExistingBookLayout)
        {
            homePage.EditorRebuildBookPreview();
        }
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
        SetTopLeft(panel.rectTransform, new Vector2(32f, -26f), new Vector2(1856f, 1050f));
        panel.sprite = LoadTaskSprite(TaskPanelBackgroundPath);
        panel.color = Color.white;
        panel.type = Image.Type.Simple;

        Image paper = CreateImage("TaskPaper", panel.transform, new Color(1f, 0.88f, 0.66f, 1f));
        SetTopLeft(paper.rectTransform, new Vector2(14f, -14f), new Vector2(1382f, 772f));
        paper.raycastTarget = false;
        paper.gameObject.SetActive(false);

        Image header = CreateImage("TaskHeader", panel.transform, new Color(0.48f, 0.17f, 0.10f, 1f));
        SetTopLeft(header.rectTransform, new Vector2(525f, -44f), new Vector2(810f, 116f));
        header.sprite = LoadTaskSprite(TaskArtDirectory + "顶部信息栏.png");
        header.color = Color.white;
        header.type = Image.Type.Simple;
        header.raycastTarget = false;

        Text title = CreateText("TaskTitle", panel.transform, "任务", 44, FontStyle.Bold, new Color(0.10f, 0.05f, 0.02f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(title.rectTransform, new Vector2(545f, -48f), new Vector2(770f, 78f));
        Text subtitle = CreateText("TaskSubtitle", panel.transform, "完成委托，领取领地成长物资", 18, FontStyle.Normal, new Color(0.30f, 0.15f, 0.07f, 0.88f), TextAnchor.MiddleLeft);
        SetTopLeft(subtitle.rectTransform, new Vector2(465f, -174f), new Vector2(430f, 34f));
        Text total = CreateText("TaskTotalProgress", panel.transform, "本页进度  0/4", 20, FontStyle.Bold, new Color(0.30f, 0.15f, 0.07f, 1f), TextAnchor.MiddleRight);
        SetTopLeft(total.rectTransform, new Vector2(1340f, -174f), new Vector2(300f, 34f));

        Button close = CreateButton("TaskCloseButton", panel.transform, string.Empty, Color.white, Color.white, 24);
        Image closeImage = close.GetComponent<Image>();
        closeImage.sprite = LoadTaskSprite(TaskCloseSpritePath);
        closeImage.color = Color.white;
        closeImage.type = Image.Type.Simple;
        closeImage.preserveAspect = true;
        SetTopLeft(close.GetComponent<RectTransform>(), new Vector2(76f, -58f), new Vector2(108f, 108f));

        Image sidebar = CreateImage("TaskSidebar", panel.transform, Color.clear);
        SetTopLeft(sidebar.rectTransform, new Vector2(104f, -202f), new Vector2(278f, 780f));
        sidebar.raycastTarget = false;
        string[] ids = { "daily", "main", "growth", "activity" };
        string[] labels = { "每日任务", "主线任务", "成长任务", "活动任务" };
        for (int i = 0; i < ids.Length; i++)
        {
            Button category = CreateButton(
                "TaskCategory_" + ids[i],
                sidebar.transform,
                labels[i],
                Color.white,
                new Color(0.16f, 0.08f, 0.03f, 1f),
                24);
            Image categoryImage = category.GetComponent<Image>();
            categoryImage.sprite = LoadTaskSprite(TaskArtDirectory + (i == 0 ? "左侧信息栏.png" : "左侧信息栏（无点击）.png"));
            categoryImage.color = Color.white;
            categoryImage.type = Image.Type.Simple;
            SetTopLeft(category.GetComponent<RectTransform>(), new Vector2(0f, -i * 190f), new Vector2(278f, 158f));
        }

        Image scrollBar = CreateImage("TaskScrollBar", panel.transform, Color.white);
        SetTopLeft(scrollBar.rectTransform, new Vector2(414f, -216f), new Vector2(32f, 720f));
        scrollBar.sprite = LoadTaskSprite(TaskArtDirectory + "滑动条.png");
        scrollBar.type = Image.Type.Simple;
        scrollBar.preserveAspect = true;
        scrollBar.raycastTarget = false;
        Image scrollHandle = CreateImage("TaskScrollHandle", scrollBar.transform, Color.white);
        SetTopLeft(scrollHandle.rectTransform, new Vector2(5f, -5f), new Vector2(22f, 112f));
        scrollHandle.sprite = LoadTaskSprite(TaskArtDirectory + "滑动条（内）.png");
        scrollHandle.type = Image.Type.Simple;
        scrollHandle.preserveAspect = true;
        scrollHandle.raycastTarget = false;

        BuildTaskRow(panel.transform, 0, new Vector2(465f, -435f));

        Image footer = CreateImage("TaskFooter", panel.transform, Color.clear);
        SetTopLeft(footer.rectTransform, new Vector2(465f, -1014f), new Vector2(1260f, 38f));
        Text footerText = CreateText("TaskFooterText", footer.transform, "每日任务会在次日刷新", 17, FontStyle.Normal, new Color(0.34f, 0.22f, 0.12f, 0.82f), TextAnchor.MiddleCenter);
        Stretch(footerText.rectTransform);
        ApplyTaskPanelStyle(scenePanel.transform);
        return scenePanel;
    }

    private static void ApplyTaskPanelStyle(Transform scenePanelRoot)
    {
        Transform panelTransform = FindTransformByName(scenePanelRoot, "TaskPanel");
        if (panelTransform == null)
        {
            Debug.LogWarning("TaskPanel was not found under TaskScenePanel.");
            return;
        }

        Image panel = panelTransform.GetComponent<Image>();
        if (panel != null)
        {
            SetTopLeft(panel.rectTransform, new Vector2(32f, -26f), new Vector2(1856f, 1050f));
            panel.sprite = LoadTaskSprite(TaskPanelBackgroundPath);
            panel.color = Color.white;
            panel.type = Image.Type.Simple;
        }

        Transform shadowTransform = FindTransformByName(scenePanelRoot, "TaskPanelShadow");
        if (shadowTransform != null)
        {
            Image shadow = shadowTransform.GetComponent<Image>();
            if (shadow != null)
            {
                SetTopLeft(shadow.rectTransform, new Vector2(42f, -36f), new Vector2(1836f, 1048f));
                shadow.color = new Color(0.10f, 0.05f, 0.02f, 0.20f);
            }
        }

        Transform paperTransform = FindTransformByName(panelTransform, "TaskPaper");
        if (paperTransform != null)
        {
            paperTransform.gameObject.SetActive(false);
        }

        Image header = FindImage(panelTransform, "TaskHeader");
        if (header != null)
        {
            SetTopLeft(header.rectTransform, new Vector2(525f, -44f), new Vector2(810f, 116f));
            header.sprite = LoadTaskSprite(TaskArtDirectory + "顶部信息栏.png");
            header.color = Color.white;
            header.type = Image.Type.Simple;
            header.raycastTarget = false;
        }

        SetTaskTextStyle(panelTransform, "TaskTitle", new Vector2(545f, -48f), new Vector2(770f, 78f), 44, TextAnchor.MiddleCenter, new Color(0.10f, 0.05f, 0.02f, 1f));
        SetTaskTextStyle(panelTransform, "TaskSubtitle", new Vector2(465f, -174f), new Vector2(430f, 34f), 18, TextAnchor.MiddleLeft, new Color(0.30f, 0.15f, 0.07f, 0.88f));
        SetTaskTextStyle(panelTransform, "TaskTotalProgress", new Vector2(1340f, -174f), new Vector2(300f, 34f), 20, TextAnchor.MiddleRight, new Color(0.30f, 0.15f, 0.07f, 1f));

        Image closeImage = FindImage(panelTransform, "TaskCloseButton");
        if (closeImage != null)
        {
            SetTopLeft(closeImage.rectTransform, new Vector2(76f, -58f), new Vector2(108f, 108f));
            closeImage.sprite = LoadTaskSprite(TaskCloseSpritePath);
            closeImage.color = Color.white;
            closeImage.type = Image.Type.Simple;
            closeImage.preserveAspect = true;
        }

        Transform sidebarTransform = FindTransformByName(panelTransform, "TaskSidebar");
        if (sidebarTransform != null)
        {
            Image sidebar = sidebarTransform.GetComponent<Image>();
            if (sidebar != null)
            {
                SetTopLeft(sidebar.rectTransform, new Vector2(104f, -202f), new Vector2(278f, 780f));
                sidebar.color = Color.clear;
                sidebar.raycastTarget = false;
            }

            string[] categoryIds = { "daily", "main", "growth", "activity" };
            for (int i = 0; i < categoryIds.Length; i++)
            {
                Transform categoryTransform = FindTransformByName(sidebarTransform, "TaskCategory_" + categoryIds[i]);
                if (categoryTransform == null)
                {
                    continue;
                }

                Image categoryImage = categoryTransform.GetComponent<Image>();
                if (categoryImage != null)
                {
                    SetTopLeft(categoryImage.rectTransform, new Vector2(0f, -i * 190f), new Vector2(278f, 158f));
                    categoryImage.sprite = LoadTaskSprite(TaskArtDirectory + "左侧信息栏（无点击）.png");
                    categoryImage.color = Color.white;
                    categoryImage.type = Image.Type.Simple;
                }

                Image selectedImage = FindImage(categoryTransform, "TaskCategorySelectedImage");
                if (selectedImage == null)
                {
                    selectedImage = CreateImage("TaskCategorySelectedImage", categoryTransform, Color.white);
                    selectedImage.transform.SetAsFirstSibling();
                }

                Stretch(selectedImage.rectTransform);
                selectedImage.sprite = LoadTaskSprite(TaskArtDirectory + "左侧信息栏.png");
                selectedImage.color = Color.white;
                selectedImage.type = Image.Type.Simple;
                selectedImage.raycastTarget = false;
                selectedImage.gameObject.SetActive(i == 0);

            }
        }

        Image scrollBar = FindImage(panelTransform, "TaskScrollBar");
        if (scrollBar == null)
        {
            scrollBar = CreateImage("TaskScrollBar", panelTransform, Color.white);
        }
        SetTopLeft(scrollBar.rectTransform, new Vector2(414f, -216f), new Vector2(32f, 720f));
        scrollBar.sprite = LoadTaskSprite(TaskArtDirectory + "滑动条.png");
        scrollBar.color = Color.white;
        scrollBar.type = Image.Type.Simple;
        scrollBar.preserveAspect = true;
        scrollBar.raycastTarget = false;

        Image scrollHandle = FindImage(scrollBar.transform, "TaskScrollHandle");
        if (scrollHandle == null)
        {
            scrollHandle = CreateImage("TaskScrollHandle", scrollBar.transform, Color.white);
        }
        SetTopLeft(scrollHandle.rectTransform, new Vector2(5f, -5f), new Vector2(22f, 112f));
        scrollHandle.sprite = LoadTaskSprite(TaskArtDirectory + "滑动条（内）.png");
        scrollHandle.color = Color.white;
        scrollHandle.type = Image.Type.Simple;
        scrollHandle.preserveAspect = true;
        scrollHandle.raycastTarget = false;

        EnsureTaskScrollLayout(panelTransform, scrollBar, scrollHandle);

        for (int i = 0; i < 1; i++)
        {
            Transform rowTransform = FindTransformByName(panelTransform, "TaskRow" + i);
            if (rowTransform != null)
            {
                ApplyTaskRowStyle(panelTransform, rowTransform, i);
            }
        }

        Transform footerTransform = FindTransformByName(panelTransform, "TaskFooter");
        if (footerTransform != null)
        {
            Image footer = footerTransform.GetComponent<Image>();
            if (footer != null)
            {
                SetTopLeft(footer.rectTransform, new Vector2(465f, -1014f), new Vector2(1260f, 38f));
                footer.color = Color.clear;
                footer.raycastTarget = false;
            }
            TMP_Text footerText = FindText(panelTransform, "TaskFooterText");
            if (footerText != null)
            {
                Stretch(footerText.rectTransform);
                footerText.color = new Color(0.34f, 0.22f, 0.12f, 0.82f);
                footerText.fontStyle = FontStyles.Normal;
            }
        }
    }

    private static void ApplyRolePanelStyle(Transform scenePanelRoot)
    {
        Transform panelTransform = FindTransformByName(scenePanelRoot, "RolePanel");
        if (panelTransform == null)
        {
            Debug.LogWarning("RolePanel was not found under RoleScenePanel.");
            return;
        }

        Image panel = panelTransform.GetComponent<Image>();
        if (panel != null)
        {
            SetTopLeft(panel.rectTransform, new Vector2(32f, -26f), new Vector2(1856f, 1050f));
            panel.sprite = LoadTaskSprite(TaskPanelBackgroundPath);
            panel.color = Color.white;
            panel.type = Image.Type.Simple;
        }

        Transform shadowTransform = FindTransformByName(scenePanelRoot, "RolePanelShadow");
        if (shadowTransform != null)
        {
            Image shadow = shadowTransform.GetComponent<Image>();
            if (shadow != null)
            {
                SetTopLeft(shadow.rectTransform, new Vector2(42f, -36f), new Vector2(1836f, 1048f));
                shadow.color = new Color(0.10f, 0.05f, 0.02f, 0.20f);
            }
        }

        Transform paperTransform = FindTransformByName(panelTransform, "RolePaper");
        if (paperTransform != null)
        {
            paperTransform.gameObject.SetActive(false);
        }

        Image header = FindImage(panelTransform, "RoleHeader");
        if (header != null)
        {
            SetTopLeft(header.rectTransform, new Vector2(525f, -44f), new Vector2(810f, 116f));
            header.sprite = LoadTaskSprite(TaskArtDirectory + "顶部信息栏.png");
            header.color = Color.white;
            header.type = Image.Type.Simple;
            header.raycastTarget = false;
        }

        SetTaskTextStyle(panelTransform, "RoleTitle", new Vector2(545f, -48f), new Vector2(770f, 78f), 44, TextAnchor.MiddleCenter, new Color(0.10f, 0.05f, 0.02f, 1f));
        SetTaskTextStyle(panelTransform, "RoleSubtitle", new Vector2(465f, -174f), new Vector2(430f, 34f), 18, TextAnchor.MiddleLeft, new Color(0.30f, 0.15f, 0.07f, 0.88f));
        SetTaskTextStyle(panelTransform, "RoleListTitle", new Vector2(64f, -202f), new Vector2(360f, 36f), 26, TextAnchor.MiddleLeft, new Color(0.30f, 0.15f, 0.07f, 1f));

        Image closeImage = FindImage(panelTransform, "RoleCloseButton");
        if (closeImage != null)
        {
            SetTopLeft(closeImage.rectTransform, new Vector2(76f, -58f), new Vector2(108f, 108f));
            closeImage.sprite = LoadTaskSprite(TaskCloseSpritePath);
            closeImage.color = Color.white;
            closeImage.type = Image.Type.Simple;
            closeImage.preserveAspect = true;
        }

        ApplyRoleTabStyle(panelTransform, "RoleOwnedTab");
        ApplyRoleTabStyle(panelTransform, "RoleCatalogTab");

        ApplyRoleCardStyle(panelTransform, "RoleListCard");
        ApplyRoleCardStyle(panelTransform, "RoleDetailCard");

        Transform listCardTransform = FindTransformByName(panelTransform, "RoleListCard");
        if (listCardTransform != null)
        {
            SetTopLeft(listCardTransform.GetComponent<RectTransform>(), new Vector2(42f, -188f), new Vector2(470f, 780f));
        }

        Transform detailCardTransform = FindTransformByName(panelTransform, "RoleDetailCard");
        if (detailCardTransform != null)
        {
            SetTopLeft(detailCardTransform.GetComponent<RectTransform>(), new Vector2(532f, -188f), new Vector2(1290f, 780f));
        }

        Image viewport = FindImage(panelTransform, "RoleListViewport");
        if (viewport != null)
        {
            SetTopLeft(viewport.rectTransform, new Vector2(56f, -244f), new Vector2(424f, 700f));
            viewport.color = Color.clear;
            viewport.raycastTarget = true;
        }

        Transform contentTransform = FindTransformByName(panelTransform, "RoleListContent");
        if (contentTransform != null)
        {
            RectTransform contentRect = contentTransform.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                contentRect.sizeDelta = new Vector2(0f, RoleListRows * 108f);
            }
        }

        for (int i = 1; i <= RoleListRows; i++)
        {
            Transform rowTransform = FindTransformByName(panelTransform, "RoleListRow" + i);
            if (rowTransform == null)
            {
                continue;
            }

            Image row = rowTransform.GetComponent<Image>();
            if (row != null)
            {
                SetTopLeft(row.rectTransform, new Vector2(6f, -6f - i * 108f), new Vector2(412f, 98f));
                row.sprite = LoadTaskSprite(TaskArtDirectory + "内容栏.png");
                row.color = Color.white;
                row.type = Image.Type.Simple;
            }
        }

        Transform detailTransform = FindTransformByName(panelTransform, "RoleDetailCard");
        if (detailTransform != null)
        {
            SetTaskTextStyle(detailTransform, "RoleDetailTitle", new Vector2(28f, -18f), new Vector2(300f, 38f), 28, TextAnchor.MiddleLeft, new Color(0.30f, 0.15f, 0.07f, 1f));

            Image detailIcon = FindImage(detailTransform, "RoleDetailIcon");
            if (detailIcon != null)
            {
                SetTopLeft(detailIcon.rectTransform, new Vector2(34f, -76f), new Vector2(156f, 156f));
                detailIcon.type = Image.Type.Simple;
            }

            SetTaskTextStyle(detailTransform, "RoleDetailName", new Vector2(190f, -78f), new Vector2(420f, 42f), 30, TextAnchor.MiddleLeft, new Color(0.26f, 0.12f, 0.05f, 1f));
            SetTaskTextStyle(detailTransform, "RoleDetailQuality", new Vector2(190f, -124f), new Vector2(300f, 30f), 19, TextAnchor.MiddleLeft, new Color(0.60f, 0.25f, 0.12f, 1f));
            SetTaskTextStyle(detailTransform, "RoleDetailStatus", new Vector2(190f, -158f), new Vector2(420f, 30f), 19, TextAnchor.MiddleLeft, new Color(0.22f, 0.46f, 0.25f, 1f));
            SetTaskTextStyle(detailTransform, "RoleDetailLevel", new Vector2(190f, -192f), new Vector2(300f, 32f), 20, TextAnchor.MiddleLeft, new Color(0.38f, 0.18f, 0.08f, 1f));

            Image divider = FindImage(detailTransform, "RoleDetailDivider");
            if (divider != null)
            {
                SetTopLeft(divider.rectTransform, new Vector2(28f, -220f), new Vector2(868f, 3f));
                divider.color = new Color(0.47f, 0.27f, 0.13f, 0.48f);
            }

            SetTaskTextStyle(detailTransform, "RoleDetailAttribute", new Vector2(34f, -240f), new Vector2(420f, 28f), 18, TextAnchor.MiddleLeft, new Color(0.34f, 0.25f, 0.16f, 1f));
            SetTaskTextStyle(detailTransform, "RoleDetailProduction", new Vector2(670f, -240f), new Vector2(590f, 28f), 18, TextAnchor.MiddleLeft, new Color(0.34f, 0.25f, 0.16f, 1f));

            TMP_Text battleStats = FindText(detailTransform, "RoleDetailBattleStats");
            if (battleStats == null)
            {
                battleStats = CreateTMPText("RoleDetailBattleStats", detailTransform, "战斗属性  暂无服务器数据", 17, new Color(0.34f, 0.25f, 0.16f, 1f), TextAlignmentOptions.MidlineLeft);
            }
            SetTopLeft(battleStats.rectTransform, new Vector2(34f, -275f), new Vector2(1230f, 30f));
            battleStats.fontSize = 17f;
            battleStats.fontStyle = FontStyles.Normal;

            TMP_Text equipment = FindText(detailTransform, "RoleDetailEquipment");
            if (equipment == null)
            {
                equipment = CreateTMPText("RoleDetailEquipment", detailTransform, "\u5df2\u7a7f\u6234\u88c5\u5907  \u6682\u65e0\u670d\u52a1\u5668\u6570\u636e", 17, new Color(0.34f, 0.25f, 0.16f, 1f), TextAlignmentOptions.MidlineLeft);
            }
            SetTopLeft(equipment.rectTransform, new Vector2(34f, -310f), new Vector2(1230f, 30f));
            equipment.fontSize = 17f;
            equipment.fontStyle = FontStyles.Normal;

            SetTaskTextStyle(detailTransform, "RoleDetailDescription", new Vector2(34f, -350f), new Vector2(1230f, 60f), 17, TextAnchor.UpperLeft, new Color(0.34f, 0.25f, 0.16f, 1f));
            SetTaskTextStyle(detailTransform, "RoleDetailAcquisition", new Vector2(34f, -420f), new Vector2(1230f, 48f), 17, TextAnchor.UpperLeft, new Color(0.38f, 0.30f, 0.22f, 1f));

            TMP_Text upgradePreview = FindText(detailTransform, "RoleDetailUpgradePreview");
            if (upgradePreview == null)
            {
                upgradePreview = CreateTMPText("RoleDetailUpgradePreview", detailTransform, "所需资源", 16, new Color(0.38f, 0.30f, 0.22f, 1f), TextAlignmentOptions.TopLeft);
            }
            upgradePreview.text = "所需资源";
            SetTopLeft(upgradePreview.rectTransform, new Vector2(34f, -475f), new Vector2(1230f, 48f));
            upgradePreview.fontSize = 16f;
            upgradePreview.fontStyle = FontStyles.Normal;

            ApplyRoleActionStyle(detailTransform, "RoleDetailUpgradeButton", new Vector2(60f, -540f));
            ApplyRoleActionStyle(detailTransform, "RoleDetailAcquireButton", new Vector2(240f, -540f));
        }

        Image footerDivider = FindImage(panelTransform, "RoleFooterDivider");
        if (footerDivider != null)
        {
            SetTopLeft(footerDivider.rectTransform, new Vector2(42f, -1014f), new Vector2(1416f, 4f));
            footerDivider.color = new Color(0.47f, 0.27f, 0.13f, 0.48f);
            footerDivider.raycastTarget = false;
        }

        SetTaskTextStyle(panelTransform, "RoleStatus", new Vector2(50f, -1030f), new Vector2(1300f, 30f), 17, TextAnchor.MiddleLeft, new Color(0.34f, 0.22f, 0.12f, 0.82f));
        ApplyRoleTextFont(panelTransform);
    }

    private static void ApplySecondaryPanelStyle(Transform scenePanelRoot, string panelPrefix)
    {
        Transform panelTransform = FindTransformByName(scenePanelRoot, panelPrefix + "Panel");
        if (panelTransform == null)
        {
            Debug.LogWarning(panelPrefix + "Panel was not found under " + panelPrefix + "ScenePanel.");
            return;
        }

        Sprite panelBackground = LoadTaskSprite(SecondaryPanelBackgroundPath);
        Image panel = panelTransform.GetComponent<Image>();
        if (panel != null)
        {
            if (panelBackground != null)
            {
                panel.sprite = panelBackground;
            }
            else
            {
                Debug.LogWarning("Secondary panel background sprite was not found: " + SecondaryPanelBackgroundPath);
            }

            panel.color = Color.white;
            panel.type = Image.Type.Simple;
        }

        Transform paper = FindTransformByName(panelTransform, panelPrefix + "Paper");
        if (paper != null)
        {
            paper.gameObject.SetActive(false);
        }

        Image header = FindImage(panelTransform, panelPrefix + "Header");
        if (header != null)
        {
            Sprite headerSprite = LoadTaskSprite(SecondaryHeaderPath);
            if (headerSprite != null)
            {
                header.sprite = headerSprite;
            }
            else
            {
                Debug.LogWarning("Secondary panel header sprite was not found: " + SecondaryHeaderPath);
            }

            header.color = Color.white;
            header.type = Image.Type.Simple;
            header.raycastTarget = false;
        }

        // Friend buttons (including rows and close) keep their authored Image and Button states.
        if (panelPrefix == nameof(HomePageScenePanelType.Friends))
        {
            return;
        }

        Button[] buttons = panelTransform.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            ApplySecondaryButtonStyle(buttons[i]);
        }

        Image closeImage = FindImage(panelTransform, panelPrefix + "CloseButton");
        if (closeImage != null)
        {
            Sprite closeSprite = LoadTaskSprite(SecondaryCloseSpritePath);
            if (closeSprite != null)
            {
                closeImage.sprite = closeSprite;
            }
            else
            {
                Debug.LogWarning("Secondary panel close sprite was not found: " + SecondaryCloseSpritePath);
            }

            closeImage.color = Color.white;
            closeImage.type = Image.Type.Simple;
            closeImage.preserveAspect = true;
        }
    }

    private static void ApplySecondaryButtonStyle(Button button)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image == null)
        {
            return;
        }

        string buttonName = button.name;
        bool isCloseButton = buttonName.IndexOf("CloseButton", System.StringComparison.Ordinal) >= 0;
        string spritePath = isCloseButton ? SecondaryCloseSpritePath : SecondaryActionSpritePath;
        Sprite sprite = LoadTaskSprite(spritePath);
        if (sprite != null)
        {
            image.sprite = sprite;
        }
        else
        {
            Debug.LogWarning("Secondary panel button sprite was not found: " + spritePath);
        }

        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;

        button.transition = Selectable.Transition.SpriteSwap;
        SpriteState spriteState = button.spriteState;
        spriteState.disabledSprite = LoadTaskSprite(SecondaryActionDisabledSpritePath);
        button.spriteState = spriteState;
    }

    private static void ApplyRoleTabStyle(Transform panelTransform, string name)
    {
        Image image = FindImage(panelTransform, name);
        if (image == null)
        {
            return;
        }

        bool selectedByDefault = name == "RoleOwnedTab";
        image.sprite = LoadTaskSprite(selectedByDefault
            ? SecondaryActionSpritePath
            : SecondaryActionDisabledSpritePath);
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        Button button = image.GetComponent<Button>();
        if (button != null)
        {
            button.transition = Selectable.Transition.None;
            SpriteState spriteState = button.spriteState;
            spriteState.selectedSprite = LoadTaskSprite(SecondaryActionSpritePath);
            spriteState.disabledSprite = LoadTaskSprite(SecondaryActionDisabledSpritePath);
            button.spriteState = spriteState;
        }
        TMP_Text label = FindText(image.transform, "Text");
        if (label != null)
        {
            label.font = roleFontAsset;
            label.fontStyle = FontStyles.Normal;
            label.fontSize = 20f;
        }
    }

    private static void ApplyRoleCardStyle(Transform panelTransform, string name)
    {
        Image image = FindImage(panelTransform, name);
        if (image == null)
        {
            return;
        }

        image.sprite = LoadTaskSprite(TaskArtDirectory + "内容栏.png");
        image.color = Color.white;
        image.type = Image.Type.Simple;
    }

    private static void ApplyRoleActionStyle(Transform parent, string name, Vector2 position)
    {
        Image image = FindImage(parent, name);
        if (image == null)
        {
            return;
        }

        SetTopLeft(image.rectTransform, position, new Vector2(150f, 78f));
        image.sprite = LoadTaskSprite(TaskActionSpritePath);
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;

        Button button = image.GetComponent<Button>();
        if (button != null)
        {
            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState spriteState = button.spriteState;
            spriteState.disabledSprite = LoadTaskSprite(TaskActionDisabledSpritePath);
            button.spriteState = spriteState;
        }

        TMP_Text label = FindText(image.transform, "Text");
        if (label != null)
        {
            label.font = roleFontAsset;
            label.fontStyle = FontStyles.Normal;
            label.fontSize = 25f;
            label.fontSizeMin = 25f;
            label.fontSizeMax = 25f;
            label.enableAutoSizing = false;
        }
    }

    private static void ApplyRoleTextFont(Transform root)
    {
        if (root == null || roleFontAsset == null)
        {
            return;
        }

        TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null)
            {
                labels[i].font = roleFontAsset;
            }
        }
    }

    private static void EnsureTaskScrollLayout(Transform panelTransform, Image scrollBarImage, Image scrollHandleImage)
    {
        Transform viewportTransform = FindTransformByName(panelTransform, "TaskRowsViewport");
        if (viewportTransform == null)
        {
            viewportTransform = CreateRectObject("TaskRowsViewport", panelTransform).transform;
        }

        RectTransform viewportRect = viewportTransform.GetComponent<RectTransform>();
        SetTopLeft(viewportRect, new Vector2(465f, -216f), new Vector2(1260f, 769f));
        RectMask2D mask = viewportTransform.GetComponent<RectMask2D>();
        if (mask == null)
        {
            mask = viewportTransform.gameObject.AddComponent<RectMask2D>();
        }

        Transform contentTransform = FindTransformByName(viewportTransform, "TaskRowsContent");
        if (contentTransform == null)
        {
            contentTransform = CreateRectObject("TaskRowsContent", viewportTransform).transform;
        }

        RectTransform contentRect = contentTransform.GetComponent<RectTransform>();
        SetTopLeft(contentRect, Vector2.zero, new Vector2(1260f, 620f));

        for (int i = 0; i < 1; i++)
        {
            Transform rowTransform = FindTransformByName(panelTransform, "TaskRow" + i);
            if (rowTransform == null)
            {
                continue;
            }

            rowTransform.SetParent(contentTransform, false);
            Image rowImage = rowTransform.GetComponent<Image>();
            RectTransform rowRect = rowTransform.GetComponent<RectTransform>();
            SetTopLeft(rowRect, new Vector2(0f, -i * 155f), new Vector2(1260f, 140f));
            if (rowImage != null)
            {
                rowImage.raycastTarget = false;
            }
        }

        ScrollRect scroll = viewportTransform.GetComponent<ScrollRect>();
        if (scroll == null)
        {
            scroll = viewportTransform.gameObject.AddComponent<ScrollRect>();
        }
        scroll.content = contentRect;
        scroll.viewport = viewportRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.scrollSensitivity = 28f;

        Scrollbar scrollbar = scrollBarImage.GetComponent<Scrollbar>();
        if (scrollbar == null)
        {
            scrollbar = scrollBarImage.gameObject.AddComponent<Scrollbar>();
        }
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = scrollHandleImage;
        scrollbar.handleRect = scrollHandleImage.rectTransform;
        scrollBarImage.raycastTarget = true;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        RectTransform handleRect = scrollHandleImage.rectTransform;
        handleRect.anchorMin = new Vector2(0f, 0f);
        handleRect.anchorMax = new Vector2(1f, 0.24f);
        handleRect.pivot = new Vector2(0.5f, 0f);
        handleRect.offsetMin = new Vector2(5f, 0f);
        handleRect.offsetMax = new Vector2(-5f, 0f);
    }

    private static void ApplyTaskRowStyle(Transform panelTransform, Transform rowTransform, int index)
    {
        Image row = rowTransform.GetComponent<Image>();
        if (row != null)
        {
            SetTopLeft(row.rectTransform, new Vector2(0f, -index * 155f), new Vector2(1260f, 140f));
            row.sprite = LoadTaskSprite(TaskArtDirectory + "内容栏.png");
            row.color = Color.white;
            row.type = Image.Type.Simple;
        }

        Transform shadowTransform = FindTransformByName(panelTransform, "TaskRowShadow" + index);
        if (shadowTransform != null)
        {
            shadowTransform.gameObject.SetActive(false);
        }

        Transform accentTransform = FindTransformByName(rowTransform, "TaskRowAccent");
        if (accentTransform != null)
        {
            accentTransform.gameObject.SetActive(false);
        }

        SetTaskTextStyle(rowTransform, "TaskItemTitle", new Vector2(32f, -20f), new Vector2(585f, 34f), 23, TextAnchor.MiddleLeft, new Color(0.22f, 0.12f, 0.07f, 1f));
        SetTaskTextStyle(rowTransform, "TaskItemDescription", new Vector2(32f, -58f), new Vector2(585f, 34f), 16, TextAnchor.UpperLeft, new Color(0.38f, 0.28f, 0.19f, 0.95f));
        SetTaskTextStyle(rowTransform, "TaskProgressTitle", new Vector2(650f, -25f), new Vector2(260f, 28f), 16, TextAnchor.MiddleCenter, new Color(0.32f, 0.20f, 0.12f, 1f));
        SetTaskTextStyle(rowTransform, "TaskProgressValue", new Vector2(650f, -106f), new Vector2(260f, 24f), 15, TextAnchor.MiddleCenter, new Color(0.32f, 0.20f, 0.12f, 1f));

        Image progressBackground = FindImage(rowTransform, "TaskProgressBackground");
        if (progressBackground != null)
        {
            SetTopLeft(progressBackground.rectTransform, new Vector2(650f, -58f), new Vector2(260f, 22f));
            progressBackground.sprite = LoadTaskSprite(TaskArtDirectory + "滑动条（内）.png");
            progressBackground.color = new Color(0.49f, 0.35f, 0.23f, 0.35f);
            progressBackground.type = Image.Type.Simple;
            progressBackground.preserveAspect = false;
        }

        Image progressFill = FindImage(rowTransform, "TaskProgressFill");
        if (progressFill != null)
        {
            SetTopLeft(progressFill.rectTransform, new Vector2(650f, -58f), new Vector2(260f, 22f));
            progressFill.sprite = LoadTaskSprite(TaskArtDirectory + "滑动条（内）.png");
            progressFill.color = new Color(0.78f, 0.29f, 0.12f, 1f);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = 0;
            progressFill.fillAmount = 0f;
            progressFill.preserveAspect = false;
        }

        Transform rewardTransform = FindTransformByName(rowTransform, "TaskReward");
        if (rewardTransform != null)
        {
            Image reward = rewardTransform.GetComponent<Image>();
            if (reward != null)
            {
                SetTopLeft(reward.rectTransform, new Vector2(32f, -101f), new Vector2(590f, 30f));
                reward.color = Color.clear;
            }
            Transform rewardIconTransform = FindTransformByName(rewardTransform, "TaskRewardIcon");
            if (rewardIconTransform != null)
            {
                SetTopLeft(rewardIconTransform.GetComponent<RectTransform>(), new Vector2(0f, -1f), new Vector2(30f, 30f));
            }
            SetTaskTextStyle(rewardTransform, "TaskRewardText", new Vector2(40f, -1f), new Vector2(540f, 30f), 15, TextAnchor.MiddleLeft, new Color(0.34f, 0.20f, 0.11f, 1f));
        }

        Image action = FindImage(rowTransform, "TaskActionButton");
        if (action != null)
        {
            SetTopLeft(action.rectTransform, new Vector2(1080f, -31f), new Vector2(150f, 78f));
            action.sprite = LoadTaskSprite(TaskActionSpritePath);
            action.color = Color.white;
            action.type = Image.Type.Simple;
            action.preserveAspect = true;

            Button actionButton = action.GetComponent<Button>();
            if (actionButton != null)
            {
                actionButton.transition = Selectable.Transition.SpriteSwap;
                SpriteState spriteState = actionButton.spriteState;
                spriteState.disabledSprite = LoadTaskSprite(TaskActionDisabledSpritePath);
                actionButton.spriteState = spriteState;
            }

            TMP_Text actionText = FindText(action.transform, "Text");
            if (actionText != null)
            {
                actionText.fontSize = 25f;
                actionText.fontSizeMin = 25f;
                actionText.fontSizeMax = 25f;
                actionText.enableAutoSizing = false;
            }
        }
    }

    private static void SetTaskTextStyle(Transform parent, string name, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, Color color)
    {
        TMP_Text text = FindText(parent, name);
        if (text == null)
        {
            return;
        }

        SetTopLeft(text.rectTransform, position, size);
        text.fontSize = fontSize;
        text.alignment = ConvertTextAlignment(alignment);
        text.color = color;
    }

    private static TMP_Text FindText(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].name == name)
            {
                return texts[i];
            }
        }

        return null;
    }

    private static Image FindImage(Transform root, string name)
    {
        Transform target = FindTransformByName(root, name);
        return target != null ? target.GetComponent<Image>() : null;
    }

    private static Sprite LoadTaskSprite(string assetPath)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
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

        Text title = CreateText("TaskItemTitle", row.transform, "任务名称", 22, FontStyle.Bold, new Color(0.30f, 0.13f, 0.06f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(title.rectTransform, new Vector2(32f, -20f), new Vector2(585f, 34f));
        Text description = CreateText("TaskItemDescription", row.transform, "任务描述", 16, FontStyle.Normal, new Color(0.43f, 0.31f, 0.21f, 1f), TextAnchor.UpperLeft);
        SetTopLeft(description.rectTransform, new Vector2(32f, -58f), new Vector2(585f, 34f));

        Text progressTitle = CreateText("TaskProgressTitle", row.transform, "任务进度", 16, FontStyle.Bold, new Color(0.40f, 0.22f, 0.10f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(progressTitle.rectTransform, new Vector2(494f, -15f), new Vector2(190f, 24f));
        Image progressBackground = CreateImage("TaskProgressBackground", row.transform, new Color(0.34f, 0.20f, 0.11f, 0.65f));
        SetTopLeft(progressBackground.rectTransform, new Vector2(494f, -49f), new Vector2(190f, 18f));
        Image progressFill = CreateImage("TaskProgressFill", row.transform, new Color(0.95f, 0.58f, 0.18f, 1f));
        SetTopLeft(progressFill.rectTransform, new Vector2(494f, -49f), new Vector2(40f, 18f));
        Text progressValue = CreateText("TaskProgressValue", row.transform, "0/1", 16, FontStyle.Bold, new Color(0.39f, 0.22f, 0.11f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(progressValue.rectTransform, new Vector2(494f, -72f), new Vector2(190f, 24f));

        Image reward = CreateImage("TaskReward", row.transform, new Color(0.99f, 0.79f, 0.37f, 0.88f));
        SetTopLeft(reward.rectTransform, new Vector2(32f, -101f), new Vector2(590f, 30f));
        Image rewardIcon = CreateImage("TaskRewardIcon", reward.transform, new Color(1f, 0.71f, 0.12f, 1f));
        SetTopLeft(rewardIcon.rectTransform, new Vector2(0f, -1f), new Vector2(30f, 30f));
        Text rewardIconText = CreateText("TaskRewardIconText", rewardIcon.transform, "奖", 18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(rewardIconText.rectTransform);
        Text rewardText = CreateText("TaskRewardText", reward.transform, "金币 x100", 16, FontStyle.Bold, new Color(0.38f, 0.17f, 0.06f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(rewardText.rectTransform, new Vector2(40f, -1f), new Vector2(540f, 28f));

        Button action = CreateButton("TaskActionButton", row.transform, "前往", new Color(0.83f, 0.34f, 0.16f, 1f), Color.white, 25);
        SetTopLeft(action.GetComponent<RectTransform>(), new Vector2(922f, -20f), new Vector2(150f, 78f));
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
        HomePageScenePanel existing = FindExistingScenePanel(parent, HomePageScenePanelType.Recharge);
        if (existing != null)
        {
            return existing;
        }

        HomePageScenePanel scenePanel = CreateScenePanel(parent, "RechargeScenePanel", HomePageScenePanelType.Recharge);
        Transform root = scenePanel.transform;
        CreateDimmer(root);

        Image panel = CreateImage("RechargePanel", root, new Color(0.47f, 0.26f, 0.12f, 1f));
        SetTopLeft(panel.rectTransform, new Vector2(240f, -94f), new Vector2(1410f, 800f));
        Image paper = CreateImage("RechargePaper", panel.transform, new Color(0.96f, 0.76f, 0.43f, 0.96f));
        SetTopLeft(paper.rectTransform, new Vector2(14f, -14f), new Vector2(1382f, 772f));

        Button close = CreateButton("RechargeCloseButton", panel.transform, "<", new Color(0.30f, 0.17f, 0.09f, 1f), new Color(1f, 0.86f, 0.58f, 1f), 28);
        SetTopLeft(close.GetComponent<RectTransform>(), new Vector2(24f, -24f), new Vector2(48f, 48f));
        Text title = CreateText("RechargeTitle", panel.transform, "充值", 34, FontStyle.Bold, new Color(0.39f, 0.17f, 0.06f, 1f), TextAnchor.MiddleLeft);
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
        HomePageScenePanel existing = FindExistingScenePanel(parent, HomePageScenePanelType.Shop);
        if (existing != null)
        {
            return existing;
        }

        HomePageScenePanel scenePanel = CreateScenePanel(parent, "ShopScenePanel", HomePageScenePanelType.Shop);
        Transform root = scenePanel.transform;
        CreateDimmer(root);

        Image panel = CreateImage("ShopPanel", root, new Color(0.36f, 0.20f, 0.10f, 1f));
        SetTopLeft(panel.rectTransform, new Vector2(220f, -100f), new Vector2(1480f, 830f));

        Image header = CreateImage("ShopHeader", panel.transform, new Color(0.16f, 0.39f, 0.34f, 1f));
        SetTopLeft(header.rectTransform, new Vector2(22f, -22f), new Vector2(1436f, 76f));
        Image headerStripe = CreateImage("ShopHeaderStripe", header.transform, new Color(1f, 0.73f, 0.28f, 1f));
        SetTopLeft(headerStripe.rectTransform, Vector2.zero, new Vector2(1436f, 7f));

        Text title = CreateText("ShopTitle", header.transform, "领地商城", 36, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        SetTopLeft(title.rectTransform, new Vector2(30f, -12f), new Vector2(230f, 48f));
        Text subtitle = CreateText("ShopSubtitle", header.transform, "选择金币或钻石购买服务器商品", 17, FontStyle.Bold, new Color(0.86f, 0.96f, 0.90f, 0.90f), TextAnchor.MiddleLeft);
        SetTopLeft(subtitle.rectTransform, new Vector2(242f, -20f), new Vector2(320f, 34f));

        Button close = CreateButton("ShopCloseButton", header.transform, "X", new Color(0.88f, 0.28f, 0.22f, 1f), Color.white, 22);
        SetTopLeft(close.GetComponent<RectTransform>(), new Vector2(1356f, -13f), new Vector2(48f, 48f));

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
        SetTopLeft(catalog.rectTransform, new Vector2(228f, -120f), new Vector2(1083f, 626f));
        Text resultCount = CreateText("ShopResultCount", catalog.transform, "金币  0 件商品", 20, FontStyle.Bold, new Color(0.33f, 0.18f, 0.08f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(resultCount.rectTransform, new Vector2(18f, -12f), new Vector2(300f, 34f));
        Button previous = CreateButton("ShopPreviousButton", catalog.transform, "<", new Color(0.36f, 0.58f, 0.54f, 1f), Color.white, 20);
        SetTopLeft(previous.GetComponent<RectTransform>(), new Vector2(624f, -10f), new Vector2(42f, 36f));
        Text pageText = CreateText("ShopPageText", catalog.transform, "1/1", 17, FontStyle.Bold, new Color(0.34f, 0.20f, 0.10f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(pageText.rectTransform, new Vector2(668f, -10f), new Vector2(62f, 36f));
        Button next = CreateButton("ShopNextButton", catalog.transform, ">", new Color(0.36f, 0.58f, 0.54f, 1f), Color.white, 20);
        SetTopLeft(next.GetComponent<RectTransform>(), new Vector2(730f, -10f), new Vector2(42f, 36f));

        for (int i = 0; i < 8; i++)
        {
            int row = i / 4;
            int column = i % 4;
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
        HomePageScenePanel existing = FindExistingScenePanel(parent, HomePageScenePanelType.DailySign);
        if (existing != null)
        {
            EnsureDailySignScrollView(existing.transform);
            EnsureActivityRewardPagingButtons(existing.transform);
            BuildSignMakeupDialog(existing.transform);
            return existing;
        }

        HomePageScenePanel scenePanel = CreateScenePanel(parent, "DailySignScenePanel", HomePageScenePanelType.DailySign);
        Transform root = scenePanel.transform;
        CreateDimmer(root);
        Image panel = CreateImage("DailySignPanel", root, new Color(0.86f, 0.70f, 0.42f, 1f));
        SetTopLeft(panel.rectTransform, new Vector2(340f, -100f), new Vector2(1210f, 815f));
        Text title = CreateText("DailySignTitle", panel.transform, "每日签到", 52, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        SetTopLeft(title.rectTransform, new Vector2(410f, -22f), new Vector2(390f, 74f));
        Button close = CreateButton("DailySignCloseButton", panel.transform, "X", new Color(0.91f, 0.39f, 0.16f, 1f), Color.white, 27);
        SetTopLeft(close.GetComponent<RectTransform>(), new Vector2(1128f, -28f), new Vector2(58f, 58f));

        GameObject cardParent = CreateRectObject("DailySignCardP", panel.transform);
        SetTopLeft(cardParent.GetComponent<RectTransform>(), new Vector2(96f, -142f), new Vector2(1018f, 490f));
        BuildSignCard(cardParent.transform, "DailySignCard1", 1, "金币", "x100", Vector2.zero, new Vector2(176.054f, 194.659f), false);
        EnsureDailySignScrollView(root);

        Image footer = CreateImage("DailySignFooter", panel.transform, new Color(1f, 0.94f, 0.74f, 0.94f));
        SetTopLeft(footer.rectTransform, new Vector2(62f, -662f), new Vector2(1086f, 92f));
        Text status = CreateText("DailySignStatus", footer.transform, string.Empty, 24, FontStyle.Bold, new Color(0.36f, 0.18f, 0.08f, 1f), TextAnchor.MiddleCenter);
        Stretch(status.rectTransform);
        footer.gameObject.SetActive(false);
        BuildSignMakeupDialog(root);
        return scenePanel;
    }

    private static void EnsureDailySignScrollView(Transform root)
    {
        RectTransform viewport = FindTransformByName(root, "DailySignCardP") as RectTransform;
        if (viewport == null) return;

        ScrollRect existingScroll = viewport.GetComponent<ScrollRect>();
        if (existingScroll != null)
        {
            existingScroll.horizontal = false;
            existingScroll.movementType = ScrollRect.MovementType.Clamped;
            return;
        }

        RectTransform content = viewport.Find("DailySignCardsContent") as RectTransform;
        if (content == null)
        {
            content = CreateRectObject("DailySignCardsContent", viewport).GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, viewport.rect.height);
            List<Transform> cards = new List<Transform>();
            for (int index = 0; index < viewport.childCount; index++)
            {
                Transform child = viewport.GetChild(index);
                const string prefix = "DailySignCard";
                if (child.name.StartsWith(prefix, System.StringComparison.Ordinal)
                    && int.TryParse(child.name.Substring(prefix.Length), out int cardNumber)
                    && cardNumber > 0)
                {
                    cards.Add(child);
                }
            }
            for (int index = 0; index < cards.Count; index++) cards[index].SetParent(content, false);
        }

        if (viewport.GetComponent<Image>() == null)
        {
            Image surface = viewport.gameObject.AddComponent<Image>();
            surface.color = Color.clear;
            surface.raycastTarget = true;
        }
        if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35f;

        Image track = CreateImage("DailySignScrollbar", viewport.parent, Color.white);
        RectTransform trackRect = track.rectTransform;
        trackRect.anchorMin = viewport.anchorMin;
        trackRect.anchorMax = viewport.anchorMax;
        trackRect.pivot = new Vector2(0f, 0.5f);
        trackRect.anchoredPosition = viewport.anchoredPosition + new Vector2(
            (1f - viewport.pivot.x) * viewport.rect.width + 8f,
            (0.5f - viewport.pivot.y) * viewport.rect.height);
        trackRect.sizeDelta = new Vector2(18f, viewport.rect.height);
        track.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TaskArtDirectory + "滑动条 1.png");
        Image handle = CreateImage("Handle", track.transform, Color.white);
        Stretch(handle.rectTransform);
        handle.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/三级界面/滑动节点.png");
        Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;
        scrollbar.value = 1f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    private static void EnsureActivityRewardPagingButtons(Transform root)
    {
        Transform parent = FindTransformByName(root, "SignCardP");
        if (parent == null)
        {
            return;
        }

        EnsureActivityRewardPagingButton(parent, "ActivityRewardPreviousButton", "<", new Vector2(-45f, -145f));
        EnsureActivityRewardPagingButton(parent, "ActivityRewardNextButton", ">", new Vector2(1053f, -145f));
    }

    private static void EnsureActivityRewardPagingButton(Transform parent, string name, string label, Vector2 position)
    {
        if (FindTransformByName(parent, name) != null)
        {
            return;
        }

        Button button = CreateTMPButton(name, parent, label, Color.white, Color.white, 34);
        SetTopLeft(button.GetComponent<RectTransform>(), position, new Vector2(64f, 64f));
    }

    private static HomePageScenePanel BuildNewbieSignPanel(Transform parent)
    {
        HomePageScenePanel existing = FindExistingScenePanel(parent, HomePageScenePanelType.NewbieSign);
        if (existing != null)
        {
            BuildSignMakeupDialog(existing.transform);
            return existing;
        }

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
        BuildSignMakeupDialog(root);
        return scenePanel;
    }

    private static bool BuildSignMakeupDialog(Transform parent)
    {
        if (parent == null || FindTransformByName(parent, "SignMakeupConfirmation") != null) return false;

        Image blocker = CreateImage("SignMakeupConfirmation", parent, new Color(0f, 0f, 0f, 0.65f));
        Stretch(blocker.rectTransform);
        blocker.raycastTarget = true;
        Image content = CreateImage("SignMakeupContent", blocker.transform, Color.white);
        content.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Imported/weijun-game-project/Assets/UI/三级界面/背景.png");
        RectTransform contentRect = content.rectTransform;
        contentRect.anchorMin = contentRect.anchorMax = contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.anchoredPosition = new Vector2(0f, 22f);
        contentRect.sizeDelta = new Vector2(596.6365f, 376.9764f);

        Color inkColor = new Color(0.11f, 0.12f, 0.14f, 1f);
        Text title = CreateText("SignMakeupTitle", content.transform, "补签确认", 30, FontStyle.Normal, inkColor, TextAnchor.MiddleCenter);
        SetTopLeft(title.rectTransform, new Vector2(4.636505f, -44f), new Vector2(592f, 46f));

        Image viewport = CreateImage("SignMakeupDetailsViewport", content.transform, Color.clear);
        SetTopLeft(viewport.rectTransform, new Vector2(33.969727f, -90.000015f), new Vector2(530.7576f, 174.9848f));
        viewport.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        Image iconFrame = CreateImage("Icon", viewport.transform, Color.white);
        iconFrame.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/二级界面/仓库二级界面/格子（无点击）.png");
        RectTransform iconFrameRect = iconFrame.rectTransform;
        iconFrameRect.anchorMin = iconFrameRect.anchorMax = iconFrameRect.pivot = new Vector2(0.5f, 0.5f);
        iconFrameRect.anchoredPosition = Vector2.zero;
        iconFrameRect.sizeDelta = new Vector2(100f, 100f);
        Image icon = CreateImage("Image", iconFrame.transform, Color.white);
        icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/资源图标/金币.png");
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(74.6209f, 74.6209f);

        Text details = CreateText("SignMakeupDetails", viewport.transform, "金币*100", 19, FontStyle.Normal, inkColor, TextAnchor.UpperCenter);
        RectTransform detailsRect = details.rectTransform;
        detailsRect.anchorMin = new Vector2(0f, 1f);
        detailsRect.anchorMax = Vector2.one;
        detailsRect.pivot = new Vector2(0.5f, 1f);
        detailsRect.anchoredPosition = new Vector2(0f, -143.12f);
        detailsRect.sizeDelta = Vector2.zero;
        details.enableAutoSizing = false;
        details.fontSize = 18.52f;
        details.enableWordWrapping = true;
        details.overflowMode = TextOverflowModes.Overflow;
        details.richText = false;
        ContentSizeFitter fitter = details.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Button cancel = CreateButton("SignMakeupCancelButton", content.transform, "取消", Color.white, Color.white, 18);
        cancel.GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/二级界面/仓库二级界面/按钮(蓝).png");
        SetTopLeft(cancel.GetComponent<RectTransform>(), new Vector2(154f, -284f), new Vector2(119.7493f, 62.9571f));
        RectTransform cancelLabel = cancel.GetComponentInChildren<TMP_Text>().rectTransform;
        cancelLabel.anchoredPosition = new Vector2(0f, 4.6143f);
        cancelLabel.sizeDelta = new Vector2(0f, -9.2286f);
        Button confirm = CreateButton("SignMakeupConfirmButton", content.transform, "确认", Color.white, Color.white, 18);
        confirm.GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TaskActionSpritePath);
        SetTopLeft(confirm.GetComponent<RectTransform>(), new Vector2(338f, -284f), new Vector2(119.7493f, 62.2098f));
        RectTransform confirmLabel = confirm.GetComponentInChildren<TMP_Text>().rectTransform;
        confirmLabel.anchoredPosition = new Vector2(0f, 4.5596f);
        confirmLabel.sizeDelta = new Vector2(0f, -9.1191f);
        blocker.gameObject.SetActive(false);
        Undo.RegisterCreatedObjectUndo(blocker.gameObject, "Add sign makeup dialog");
        return true;
    }

    private static Image BuildSignCard(Transform parent, string rootName, int day, string rewardName, string rewardAmount, Vector2 position, Vector2 size, bool large)
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
        if (rootName.StartsWith("NewbieSignCard", System.StringComparison.Ordinal))
        {
            Image resource = CreateImage("RewardResourceImage", rewardIcon.transform, Color.white);
            string iconName = day == 1 ? "金币" : day == 2 ? "木材" : "宝箱（未打开）";
            resource.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ResourceIconDirectory + iconName + ".png");
            resource.preserveAspect = true;
            resource.raycastTarget = false;
            Stretch(resource.rectTransform);
        }
        else
        {
            Text iconText = CreateText("RewardIconText", rewardIcon.transform, rewardName.Substring(0, 1), large ? 34 : 24, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(iconText.rectTransform);
        }

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
        return card;
    }

    private static HomePageScenePanel BuildSettingsPanel(Transform parent)
    {
        HomePageScenePanel existing = FindExistingScenePanel(parent, HomePageScenePanelType.Settings);
        if (existing != null)
        {
            return existing;
        }

        HomePageScenePanel scenePanel = CreateScenePanel(parent, "SettingsScenePanel", HomePageScenePanelType.Settings);
        Transform root = scenePanel.transform;
        CreateDimmer(root);

        Image panel = CreateImage("SettingsPanel", root, new Color(0.43f, 0.24f, 0.12f, 1f));
        SetTopLeft(panel.rectTransform, new Vector2(400f, -128f), new Vector2(1120f, 820f));

        Image header = CreateImage("SettingsHeader", panel.transform, new Color(0.18f, 0.50f, 0.50f, 1f));
        SetTopLeft(header.rectTransform, new Vector2(24f, -24f), new Vector2(1072f, 76f));
        Text title = CreateText("SettingsTitle", panel.transform, "游戏设置", 38, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        SetTopLeft(title.rectTransform, new Vector2(60f, -34f), new Vector2(220f, 54f));
        Text subtitle = CreateText("SettingsSubtitle", panel.transform, "声音与显示", 18, FontStyle.Bold, new Color(0.89f, 1f, 0.94f, 0.86f), TextAnchor.MiddleLeft);
        SetTopLeft(subtitle.rectTransform, new Vector2(276f, -44f), new Vector2(220f, 34f));
        Button close = CreateButton("SettingsCloseButton", panel.transform, "X", new Color(0.88f, 0.28f, 0.24f, 1f), Color.white, 24);
        SetTopLeft(close.GetComponent<RectTransform>(), new Vector2(1026f, -36f), new Vector2(52f, 52f));

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

        return scenePanel;
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
        HomePageScenePanel existing = FindExistingScenePanel(parent, HomePageScenePanelType.AccountProfile);
        if (existing != null)
        {
            return existing;
        }

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
        Button requestsButton = CreateButton("FriendsRequestsButton", panel.transform, "申请", new Color(0.90f, 0.79f, 0.59f, 1f), new Color(0.36f, 0.20f, 0.10f, 1f), 18);
        SetTopLeft(requestsButton.GetComponent<RectTransform>(), new Vector2(482f, -122f), new Vector2(100f, 50f));
        InputField addInput = CreateSceneInputField("FriendsAddInput", panel.transform, "输入玩家 ID", 18);
        SetTopLeft(addInput.GetComponent<RectTransform>(), new Vector2(880f, -122f), new Vector2(246f, 50f));
        Button addButton = CreateButton("FriendsAddButton", panel.transform, "搜索玩家", new Color(0.33f, 0.66f, 0.36f, 1f), Color.white, 19);
        SetTopLeft(addButton.GetComponent<RectTransform>(), new Vector2(1140f, -122f), new Vector2(318f, 50f));

        Image listArea = CreateImage("FriendsListArea", panel.transform, new Color(1f, 0.84f, 0.58f, 1f));
        SetTopLeft(listArea.rectTransform, new Vector2(42f, -196f), new Vector2(870f, 498f));
        Image listViewport = CreateImage("FriendsListViewport", listArea.transform, Color.clear);
        SetTopLeft(listViewport.rectTransform, new Vector2(14f, -14f), new Vector2(842f, 470f));
        listViewport.gameObject.AddComponent<RectMask2D>();
        GameObject rowParent = new GameObject("RowParent", typeof(RectTransform));
        rowParent.layer = listArea.gameObject.layer;
        rowParent.transform.SetParent(listViewport.transform, false);
        RectTransform rowContent = rowParent.GetComponent<RectTransform>();
        SetTopLeft(rowContent, Vector2.zero, new Vector2(842f, 470f));
        VerticalLayoutGroup rowLayout = rowParent.AddComponent<VerticalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.UpperLeft;
        rowLayout.spacing = 12f;
        rowLayout.childControlWidth = rowLayout.childControlHeight = false;
        rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
        ContentSizeFitter rowFitter = rowParent.AddComponent<ContentSizeFitter>();
        rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect listScroll = listViewport.gameObject.AddComponent<ScrollRect>();
        listScroll.viewport = listViewport.rectTransform;
        listScroll.content = rowContent;
        listScroll.horizontal = false;
        listScroll.vertical = true;
        listScroll.movementType = ScrollRect.MovementType.Clamped;
        listScroll.scrollSensitivity = 30f;
        BuildFriendRow(rowParent.transform, 0, Vector2.zero);

        Image detailArea = CreateImage("FriendDetailArea", panel.transform, new Color(1f, 0.89f, 0.67f, 1f));
        SetTopLeft(detailArea.rectTransform, new Vector2(936f, -196f), new Vector2(522f, 498f));
        BuildFriendDetail(detailArea.transform);

        Image footerDivider = CreateImage("FriendsFooterDivider", panel.transform, new Color(0.47f, 0.27f, 0.13f, 0.48f));
        SetTopLeft(footerDivider.rectTransform, new Vector2(42f, -714f), new Vector2(1416f, 4f));
        footerDivider.raycastTarget = false;
        Text status = CreateText("FriendsStatus", panel.transform, "好友名册已打开", 17, FontStyle.Bold, new Color(0.36f, 0.42f, 0.24f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(status.rectTransform, new Vector2(50f, -734f), new Vector2(850f, 52f));
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
        Text meta = CreateText("FriendRowMeta" + index, row.transform, "Lv.1    ID --", 16, FontStyle.Normal, new Color(0.48f, 0.38f, 0.27f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(meta.rectTransform, new Vector2(94f, -44f), new Vector2(360f, 26f));
        Text state = CreateText("FriendRowState" + index, row.transform, "在线", 17, FontStyle.Bold, new Color(0.25f, 0.52f, 0.30f, 1f), TextAnchor.MiddleRight);
        SetTopLeft(state.rectTransform, new Vector2(618f, -24f), new Vector2(190f, 34f));
    }

    private static HomePageScenePanel BuildRolePanel(Transform parent)
    {
        HomePageScenePanel existing = FindExistingScenePanel(parent, HomePageScenePanelType.Role);
        if (existing != null)
        {
            return existing;
        }

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
        viewportObject.AddComponent<RectMask2D>();

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

        for (int i = 1; i <= RoleListRows; i++)
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
        TMP_Text equipment = CreateTMPText("RoleDetailEquipment", detailCard.transform, "\u5df2\u7a7f\u6234\u88c5\u5907  \u6682\u65e0\u670d\u52a1\u5668\u6570\u636e", 17, new Color(0.42f, 0.33f, 0.24f, 1f), TextAlignmentOptions.TopLeft);
        SetTopLeft(equipment.rectTransform, new Vector2(34f, -310f), new Vector2(846f, 30f));

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
        ApplyRolePanelStyle(scenePanel.transform);
        return scenePanel;
    }

    private static void BuildRoleListRow(Transform parent, int index)
    {
        Button row = CreateTMPButton("RoleListRow" + index, parent, string.Empty, new Color(1f, 0.95f, 0.78f, 1f), Color.white, 16);
        SetTopLeft(row.GetComponent<RectTransform>(), new Vector2(6f, -6f - index * 108f), new Vector2(412f, 98f));
        Image icon = CreateImage("RoleListRowIcon" + index, row.transform, new Color(0.28f, 0.62f, 0.76f, 1f));
        SetTopLeft(icon.rectTransform, new Vector2(12f, -10f), new Vector2(78f, 78f));
        Image headImage = CreateImage("HeadImage", icon.transform, Color.white);
        SetTopLeft(headImage.rectTransform, new Vector2(4f, -4f), new Vector2(70f, 70f));
        headImage.raycastTarget = false;
        TMP_Text name = CreateTMPText("RoleListRowName" + index, row.transform, "\u89d2\u8272\u540d\u79f0", 18, new Color(0.34f, 0.17f, 0.08f, 1f), TextAlignmentOptions.MidlineLeft);
        SetTopLeft(name.rectTransform, new Vector2(104f, -12f), new Vector2(180f, 28f));
        Image quality = CreateImage("RoleListRowQuality" + index, icon.transform, Color.white);
        SetTopLeft(quality.rectTransform, new Vector2(42f, -8f), new Vector2(20f, 20f));
        quality.raycastTarget = false;
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
        HomePageScenePanel existing = FindExistingScenePanel(parent, HomePageScenePanelType.Equipment);
        if (existing != null) return existing;
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
        Button close = CreateButton("EquipmentCloseButton", panel.transform, "X", new Color(0.90f, 0.30f, 0.25f, 1f), Color.white, 24);
        SetTopLeft(close.GetComponent<RectTransform>(), new Vector2(1408f, -36f), new Vector2(50f, 50f));

        Image slotsCard = CreateImage("EquipmentSlotsCard", panel.transform, new Color(1f, 0.84f, 0.58f, 1f));
        SetTopLeft(slotsCard.rectTransform, new Vector2(532f, -160f), new Vector2(430f, 540f));
        Image npcAvatar = CreateImage("EquipmentNpcAvatar", slotsCard.transform, new Color(0.38f, 0.65f, 0.40f, 1f));
        SetTopLeft(npcAvatar.rectTransform, new Vector2(174f, -48f), new Vector2(82f, 82f));
        Text npcName = CreateText("EquipmentNpcName", slotsCard.transform, "未选择角色", 22, FontStyle.Bold, new Color(0.38f, 0.18f, 0.08f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(npcName.rectTransform, new Vector2(16f, -48f), new Vector2(146f, 32f));
        Text npcDescription = CreateText("EquipmentNpcDescription", slotsCard.transform, "请选择角色查看穿戴装备", 13, FontStyle.Normal, new Color(0.38f, 0.18f, 0.08f, 1f), TextAnchor.MiddleLeft);
        SetTopLeft(npcDescription.rectTransform, new Vector2(16f, -140f), new Vector2(398f, 24f));
        Button previousNpc = CreateButton("EquipmentNpcPreviousButton", slotsCard.transform, "<", Color.white, Color.black, 18);
        SetTopLeft(previousNpc.GetComponent<RectTransform>(), new Vector2(16f, -100f), new Vector2(40f, 42f));
        Button nextNpc = CreateButton("EquipmentNpcNextButton", slotsCard.transform, ">", Color.white, Color.black, 18);
        SetTopLeft(nextNpc.GetComponent<RectTransform>(), new Vector2(374f, -100f), new Vector2(40f, 42f));
        BuildEquipmentSlot(slotsCard.transform, "weapon", "武器", new Vector2(13f, -174f), new Color(0.83f, 0.38f, 0.24f, 1f));
        BuildEquipmentSlot(slotsCard.transform, "helmet", "头盔", new Vector2(153f, -174f), new Color(0.68f, 0.38f, 0.72f, 1f));
        BuildEquipmentSlot(slotsCard.transform, "armor", "护甲", new Vector2(293f, -174f), new Color(0.28f, 0.55f, 0.76f, 1f));
        BuildEquipmentSlot(slotsCard.transform, "boots", "靴子", new Vector2(83f, -296f), new Color(0.38f, 0.65f, 0.40f, 1f));
        BuildEquipmentSlot(slotsCard.transform, "gloves", "手套", new Vector2(223f, -296f), new Color(0.60f, 0.45f, 0.30f, 1f));

        Image listCard = CreateImage("EquipmentListCard", panel.transform, new Color(1f, 0.89f, 0.67f, 1f));
        SetTopLeft(listCard.rectTransform, new Vector2(42f, -160f), new Vector2(470f, 540f));
        GameObject categoryRoot = CreateRectObject("EquipmentCategory", listCard.transform);
        Stretch(categoryRoot.GetComponent<RectTransform>());
        BuildEquipmentCategoryButton(categoryRoot.transform, "all", "全部", new Vector2(6f, -16f));
        for (int index = 0; index < 5; index++)
        {
            BuildEquipmentCategoryButton(categoryRoot.transform, "placeholder" + index,
                "分类", new Vector2(6f, -74f - index * 58f));
        }
        RectTransform itemParent = BuildEquipmentItemScroll(listCard.transform);
        BuildEquipmentRow(itemParent, 0, new Vector2(4f, -8f));

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
        SetTopLeft(detailIconBackground.rectTransform, new Vector2(197f, -16f), new Vector2(80f, 80f));
        Text detailIcon = CreateText("EquipmentDetailIcon", detailIconBackground.transform, "装", 48, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(detailIcon.rectTransform);
        Text detailName = CreateText("EquipmentDetailName", detailContent.transform, "装备名称", 28, FontStyle.Bold, new Color(0.34f, 0.16f, 0.08f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(detailName.rectTransform, new Vector2(40f, -104f), new Vector2(394f, 34f));
        Text detailType = CreateText("EquipmentDetailType", detailContent.transform, "武器 · 数量 x1", 17, FontStyle.Normal, new Color(0.48f, 0.39f, 0.29f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(detailType.rectTransform, new Vector2(40f, -140f), new Vector2(394f, 28f));
        Image descriptionCard = CreateImage("EquipmentDescriptionCard", detailContent.transform, new Color(1f, 0.93f, 0.76f, 1f));
        SetTopLeft(descriptionCard.rectTransform, new Vector2(28f, -174f), new Vector2(418f, 72f));
        Text description = CreateText("EquipmentDetailDescription", descriptionCard.transform, "装备描述", 16, FontStyle.Normal, new Color(0.42f, 0.33f, 0.24f, 1f), TextAnchor.MiddleCenter);
        Stretch(description.rectTransform);

        BuildEquipmentStatChip(detailContent.transform, "EquipmentDetailAttack", "攻击  +0", new Vector2(28f, -258f), new Color(0.85f, 0.39f, 0.27f, 1f));
        BuildEquipmentStatChip(detailContent.transform, "EquipmentDetailDefense", "防御  +0", new Vector2(244f, -258f), new Color(0.28f, 0.55f, 0.76f, 1f));
        BuildEquipmentStatChip(detailContent.transform, "EquipmentDetailHealth", "生命  +0", new Vector2(28f, -308f), new Color(0.35f, 0.66f, 0.38f, 1f));
        Image previewViewport = CreateImage("EquipmentUpgradePreviewViewport", detailContent.transform, Color.clear);
        SetTopLeft(previewViewport.rectTransform, new Vector2(28f, -408f), new Vector2(418f, 58f));
        previewViewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect previewScroll = previewViewport.gameObject.AddComponent<ScrollRect>();
        previewScroll.horizontal = false;
        previewScroll.movementType = ScrollRect.MovementType.Clamped;
        previewScroll.scrollSensitivity = 20f;
        previewScroll.viewport = previewViewport.rectTransform;
        Text upgradePreview = CreateText("EquipmentDetailUpgradePreview", previewViewport.transform, "一键升级预览：等待服务器返回...", 12, FontStyle.Normal, new Color(0.38f, 0.25f, 0.14f, 1f), TextAnchor.UpperLeft);
        SetTopLeft(upgradePreview.rectTransform, Vector2.zero, new Vector2(418f, 58f));
        upgradePreview.enableWordWrapping = true;
        upgradePreview.overflowMode = TextOverflowModes.Overflow;
        upgradePreview.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        previewScroll.content = upgradePreview.rectTransform;
        Button action = CreateButton("EquipmentActionButton", detailContent.transform, "穿戴装备", new Color(0.28f, 0.64f, 0.38f, 1f), Color.white, 20);
        SetTopLeft(action.GetComponent<RectTransform>(), new Vector2(28f, -478f), new Vector2(128f, 40f));
        Button upgrade = CreateButton("EquipmentUpgradeButton", detailContent.transform, "升级", Color.white, Color.black, 18);
        SetTopLeft(upgrade.GetComponent<RectTransform>(), new Vector2(173f, -478f), new Vector2(128f, 40f));
        Button autoUpgrade = CreateButton("EquipmentAutoUpgradeButton", detailContent.transform, "一键升级", Color.white, Color.black, 18);
        SetTopLeft(autoUpgrade.GetComponent<RectTransform>(), new Vector2(173f, -532f), new Vector2(128f, 40f));
        Button lockButton = CreateButton("EquipmentLockButton", detailContent.transform, "锁定", Color.white, Color.black, 18);
        SetTopLeft(lockButton.GetComponent<RectTransform>(), new Vector2(318f, -478f), new Vector2(128f, 40f));
        Text detailStatus = CreateText("EquipmentDetailStatus", detailContent.transform, "同部位已穿戴时请先卸下", 15, FontStyle.Normal, new Color(0.45f, 0.36f, 0.27f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(detailStatus.rectTransform, new Vector2(28f, -520f), new Vector2(418f, 20f));

        Text attack = CreateText("EquipmentAttackText", panel.transform, "攻击  10", 16, FontStyle.Bold, new Color(0.85f, 0.39f, 0.27f, 1f), TextAnchor.MiddleRight);
        SetTopLeft(attack.rectTransform, new Vector2(1120f, -728f), new Vector2(110f, 28f));
        Text defense = CreateText("EquipmentDefenseText", panel.transform, "防御  8", 16, FontStyle.Bold, new Color(0.28f, 0.55f, 0.76f, 1f), TextAnchor.MiddleRight);
        SetTopLeft(defense.rectTransform, new Vector2(1235f, -728f), new Vector2(100f, 28f));
        Text health = CreateText("EquipmentHealthText", panel.transform, "生命  100", 16, FontStyle.Bold, new Color(0.35f, 0.66f, 0.38f, 1f), TextAnchor.MiddleRight);
        SetTopLeft(health.rectTransform, new Vector2(1340f, -728f), new Vector2(120f, 28f));
        return scenePanel;
    }

    private static void BuildEquipmentSlot(Transform parent, string slotId, string label, Vector2 position, Color color)
    {
        string suffix = EquipmentPascalCase(slotId);
        Button slot = CreateButton("EquipmentSlot" + suffix, parent, string.Empty, new Color(0.86f, 0.72f, 0.48f, 1f), Color.white, 16);
        SetTopLeft(slot.GetComponent<RectTransform>(), position, new Vector2(124f, 114f));
        Image iconBackground = CreateImage("EquipmentSlotIconBackground" + suffix, slot.transform, color);
        SetTopLeft(iconBackground.rectTransform, new Vector2(38f, -8f), new Vector2(48f, 48f));
        Text icon = CreateText("EquipmentSlotIcon" + suffix, iconBackground.transform, slotId == "weapon" ? "剑" : slotId == "armor" ? "甲" : slotId == "pet" ? "宠" : "饰", 34, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(icon.rectTransform);
        Text name = CreateText("EquipmentSlotName" + suffix, slot.transform, "空装备位", 18, FontStyle.Bold, new Color(0.34f, 0.17f, 0.08f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(name.rectTransform, new Vector2(6f, -58f), new Vector2(112f, 26f));
        Text state = CreateText("EquipmentSlotState" + suffix, slot.transform, label, 15, FontStyle.Normal, new Color(0.48f, 0.38f, 0.27f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(state.rectTransform, new Vector2(6f, -86f), new Vector2(112f, 22f));
        GameObject empty = CreateRectObject("EquipmentSlotEmpty" + suffix, slot.transform);
        SetTopLeft(empty.GetComponent<RectTransform>(), new Vector2(12f, -164f), new Vector2(181f, 22f));
        Text emptyText = CreateText("Text", empty.transform, "未穿戴", 12, FontStyle.Normal, new Color(0.56f, 0.44f, 0.30f, 0.82f), TextAnchor.MiddleCenter);
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
        SetTopLeft(button.GetComponent<RectTransform>(), position, new Vector2(64f, 38f));
    }

    private static RectTransform BuildEquipmentItemScroll(Transform parent)
    {
        Image viewport = CreateImage("EquipmentItemsViewport", parent, Color.clear);
        SetTopLeft(viewport.rectTransform, new Vector2(90f, -8f), new Vector2(332f, 520f));
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = CreateRectObject("EquipmentItemP", viewport.transform).GetComponent<RectTransform>();
        SetTopLeft(content, Vector2.zero, new Vector2(332f, 520f));
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport.rectTransform;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35f;
        Image track = CreateImage("EquipmentScrollbar", parent, Color.white);
        SetTopLeft(track.rectTransform, new Vector2(434f, -8f), new Vector2(14f, 520f));
        track.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TaskArtDirectory + "滑动条 1.png");
        Image handle = CreateImage("Handle", track.transform, Color.white);
        Stretch(handle.rectTransform);
        handle.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/三级界面/滑动节点.png");
        Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return content;
    }

    private static void BuildEquipmentRow(Transform parent, int index, Vector2 position)
    {
        Button row = CreateButton("EquipmentItemRow" + index, parent, string.Empty, Color.white, Color.white, 16);
        SetTopLeft(row.GetComponent<RectTransform>(), position, new Vector2(89.268f, 91.791f));
        row.GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Art/UI/二级界面/仓库二级界面/格子（白）.png");
        Image icon = CreateImage("EquipmentRowIconBackground" + index, row.transform, Color.white);
        SetTopLeft(icon.rectTransform, new Vector2(13.978f, -15.545f), new Vector2(60.701f, 60.701f));
        icon.preserveAspect = true;
        Image selection = CreateImage("Select", row.transform, Color.white);
        selection.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        selection.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        selection.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        selection.rectTransform.anchoredPosition = Vector2.zero;
        selection.rectTransform.sizeDelta = new Vector2(100f, 100f);
        selection.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Art/UI/二级界面/仓库二级界面/格子选择（点击）.png");
        selection.raycastTarget = false;
        selection.gameObject.SetActive(false);
        Text name = CreateText("EquipmentRowName" + index, row.transform, "装备名称", 13,
            FontStyle.Normal, new Color(0.34f, 0.17f, 0.08f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(name.rectTransform, new Vector2(0.657f, -93f), new Vector2(88.611f, 20.054f));
        Text level = CreateText("EquipmentRowMeta" + index, row.transform, "Lv.1", 11,
            FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);
        SetTopLeft(level.rectTransform, new Vector2(1.314f, -69.791f), new Vector2(85.105f, 14.514f));
        Image badge = CreateImage("EquipmentRowNPC", row.transform, Color.white);
        SetTopLeft(badge.rectTransform, new Vector2(74.68f, 5f), new Vector2(24.182f, 24.182f));
        Text wearer = CreateText("Text", badge.transform, "", 14, FontStyle.Normal, Color.black, TextAnchor.MiddleCenter);
        Stretch(wearer.rectTransform);
        badge.gameObject.SetActive(false);
    }

    private static void BuildEquipmentStatChip(Transform parent, string name, string value, Vector2 position, Color color)
    {
        Image chip = CreateImage(name + "Chip", parent, new Color(1f, 0.93f, 0.74f, 1f));
        SetTopLeft(chip.rectTransform, position, new Vector2(202f, 40f));
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
        Button copyId = CreateTMPButton("FriendCopyIdButton", content.transform, "复制", new Color(0.28f, 0.55f, 0.76f, 1f), Color.white, 16);
        SetTopLeft(copyId.GetComponent<RectTransform>(), new Vector2(374f, -200f), new Vector2(76f, 32f));
        Text level = CreateText("FriendDetailLevel", content.transform, "领主等级  Lv.1", 18, FontStyle.Bold, new Color(0.46f, 0.23f, 0.10f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(level.rectTransform, new Vector2(52f, -234f), new Vector2(418f, 30f));
        Text online = CreateText("FriendDetailOnline", content.transform, "当前在线", 17, FontStyle.Bold, new Color(0.23f, 0.55f, 0.30f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(online.rectTransform, new Vector2(52f, -270f), new Vector2(418f, 30f));
        Image divider = CreateImage("FriendDetailDivider", content.transform, new Color(0.55f, 0.34f, 0.17f, 0.42f));
        SetTopLeft(divider.rectTransform, new Vector2(34f, -350f), new Vector2(454f, 3f));
        divider.raycastTarget = false;
        Text giftHint = CreateText("FriendGiftHint", content.transform, "今日可赠送 5 点体力", 17, FontStyle.Normal, new Color(0.46f, 0.37f, 0.27f, 1f), TextAnchor.MiddleCenter);
        SetTopLeft(giftHint.rectTransform, new Vector2(52f, -370f), new Vector2(418f, 30f));
        Button gift = CreateButton("FriendsGiftButton", content.transform, "赠送体力", new Color(0.32f, 0.66f, 0.38f, 1f), Color.white, 19);
        SetTopLeft(gift.GetComponent<RectTransform>(), new Vector2(72f, -416f), new Vector2(174f, 50f));
        Button delete = CreateButton("FriendsDeleteButton", content.transform, "删除好友", new Color(0.84f, 0.38f, 0.30f, 1f), Color.white, 19);
        SetTopLeft(delete.GetComponent<RectTransform>(), new Vector2(276f, -416f), new Vector2(174f, 50f));
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
        Text text = CreateText("MailRewardText", slot.transform, amount, 15, FontStyle.Bold, new Color(0.36f, 0.16f, 0.07f, 1f), TextAnchor.MiddleCenter);
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
