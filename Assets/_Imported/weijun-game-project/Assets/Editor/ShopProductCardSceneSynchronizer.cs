using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class ShopProductCardSceneSynchronizer
{
    private const string HomeScenePath = "Assets/_Project/Scenes/HomeScene.unity";

    [InitializeOnLoadMethod]
    private static void RegisterWarehouseButtonSync()
    {
        EditorApplication.delayCall += SyncLoadedShopWarehouseButton;
        EditorApplication.playModeStateChanged -= OnWarehouseButtonPlayModeChanged;
        EditorApplication.playModeStateChanged += OnWarehouseButtonPlayModeChanged;
        EditorSceneManager.sceneOpened -= OnWarehouseButtonSceneOpened;
        EditorSceneManager.sceneOpened += OnWarehouseButtonSceneOpened;
    }

    private static void OnWarehouseButtonPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += SyncLoadedShopWarehouseButton;
        }
    }

    private static void OnWarehouseButtonSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += SyncLoadedShopWarehouseButton;
    }

    [MenuItem("Tools/WeiJun UI/Sync Shop Warehouse Button")]
    public static void SyncLoadedShopWarehouseButton()
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

        Transform shopPanel = FindSceneTransform(scene, "ShopPanel");
        if (shopPanel == null || FindChildTransform(shopPanel, "ShopWarehouseButton") != null)
        {
            return;
        }

        Transform template = FindChildTransform(shopPanel, "ShopBuyCrystalButton");
        RectTransform templateRect = template as RectTransform;
        if (templateRect == null || template.GetComponent<Button>() == null)
        {
            Debug.LogWarning("ShopBuyCrystalButton is missing; the warehouse shortcut could not be added.", shopPanel);
            return;
        }

        GameObject shortcut = Object.Instantiate(template.gameObject, template.parent);
        shortcut.name = "ShopWarehouseButton";
        Undo.RegisterCreatedObjectUndo(shortcut, "Add Shop Warehouse Button");
        RectTransform shortcutRect = shortcut.GetComponent<RectTransform>();
        shortcutRect.anchoredPosition = templateRect.anchoredPosition + new Vector2(-88.78f, 0f);
        shortcutRect.sizeDelta = new Vector2(80f, templateRect.sizeDelta.y);
        shortcutRect.SetSiblingIndex(template.GetSiblingIndex() + 1);
        Button button = shortcut.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.interactable = true;
        Image background = shortcut.GetComponent<Image>();
        if (background != null)
        {
            background.preserveAspect = false;
        }

        TMP_Text label = shortcut.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = "\u4ed3\u5e93";
        }

        shortcut.SetActive(true);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("ShopWarehouseButton added to the loaded HomeScene. Save the scene to keep this node.", shortcut);
    }

    [MenuItem("Tools/WeiJun UI/Sync Shop Cards From Card 0")]
    public static void SyncHomeSceneShopCards()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != HomeScenePath)
        {
            scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
        }
        if (!scene.IsValid() || scene.path != HomeScenePath)
        {
            Debug.LogWarning("无法打开 Assets/_Project/Scenes/HomeScene.unity，无法同步商城商品卡片。", null);
            return;
        }

        Transform template = FindSceneTransform(scene, "ShopProductCard0");
        if (template == null)
        {
            Debug.LogWarning("未找到 ShopProductCard0，无法同步商城商品卡片。", scene.GetRootGameObjects()[0]);
            return;
        }

        string[] cardNames =
        {
            "ShopProductCard1",
            "ShopProductCard2",
            "ShopProductCard3",
            "ShopProductCard4",
            "ShopProductCard5"
        };

        for (int i = 0; i < cardNames.Length; i++)
        {
            Transform target = FindSceneTransform(scene, cardNames[i]);
            if (target == null)
            {
                Debug.LogWarning("未找到 " + cardNames[i] + "，已跳过该卡片。", template);
                continue;
            }

            CopyCardAppearanceAndChildren(template, target);
        }

        EnsureAdditionalShopCard(template, "ShopProductCard6", new Vector2(780f, -62f));
        EnsureAdditionalShopCard(template, "ShopProductCard7", new Vector2(780f, -312f));

        Transform detail = FindSceneTransform(scene, "ShopDetail");
        Transform closeTemplate = FindSceneTransform(scene, "ShopCloseButton");
        if (detail != null && closeTemplate != null)
        {
            EnsureDetailCloseButton(detail, closeTemplate);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("商城商品卡片已按 ShopProductCard0 同步，原有卡片位置已保留。", template);
    }

    private static void EnsureAdditionalShopCard(Transform template, string cardName, Vector2 position)
    {
        Transform target = FindChildTransform(template.parent, cardName);
        if (target == null)
        {
            GameObject clone = Object.Instantiate(template.gameObject, template.parent);
            clone.name = cardName;
            target = clone.transform;
            RectTransform cloneRect = target as RectTransform;
            if (cloneRect != null)
            {
                cloneRect.anchoredPosition = position;
            }
        }

        CopyCardAppearanceAndChildren(template, target);
    }

    [MenuItem("Tools/WeiJun UI/Remove Shop Legacy Nodes")]
    public static void RemoveShopLegacyNodes()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != HomeScenePath)
        {
            scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
        }
        if (!scene.IsValid() || scene.path != HomeScenePath)
        {
            Debug.LogWarning("无法打开 HomeScene，无法删除商城旧节点。", null);
            return;
        }

        string[] nodeNames =
        {
            "ShopGoldTextBadge",
            "ShopCrystalTextBadge",
            "ShopRefreshButton"
        };

        for (int i = 0; i < nodeNames.Length; i++)
        {
            Transform node = FindSceneTransform(scene, nodeNames[i]);
            if (node != null)
            {
                Object.DestroyImmediate(node.gameObject);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("商城旧节点已删除。", null);
    }

    [MenuItem("Tools/WeiJun UI/Remove Shop Panel Frame Nodes")]
    public static void RemoveShopPanelFrameNodes()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != HomeScenePath)
        {
            scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
        }
        if (!scene.IsValid() || scene.path != HomeScenePath)
        {
            Debug.LogWarning("无法打开 HomeScene，无法删除商城边框节点。", null);
            return;
        }

        Transform shopPanel = FindSceneTransform(scene, "ShopPanel");
        if (shopPanel == null)
        {
            Debug.LogWarning("未找到 ShopPanel，无法删除商城边框节点。", null);
            return;
        }

        string[] nodeNames =
        {
            "FrameTop",
            "FrameBottom",
            "FrameLeft",
            "FrameRight"
        };

        for (int i = 0; i < nodeNames.Length; i++)
        {
            Transform node = shopPanel.Find(nodeNames[i]);
            if (node != null)
            {
                Object.DestroyImmediate(node.gameObject);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("ShopPanel 下的边框节点已删除。", shopPanel);
    }

    private static void CopyCardAppearanceAndChildren(Transform template, Transform target)
    {
        RectTransform templateRect = template as RectTransform;
        RectTransform targetRect = target as RectTransform;
        if (templateRect == null || targetRect == null)
        {
            Debug.LogWarning("商城卡片缺少 RectTransform，无法同步。", target);
            return;
        }

        Vector2 anchoredPosition = targetRect.anchoredPosition;
        bool active = target.gameObject.activeSelf;

        Image templateImage = template.GetComponent<Image>();
        Image targetImage = target.GetComponent<Image>();
        if (templateImage != null && targetImage != null)
        {
            EditorUtility.CopySerialized(templateImage, targetImage);
        }

        Button templateButton = template.GetComponent<Button>();
        Button targetButton = target.GetComponent<Button>();
        if (templateButton != null && targetButton != null)
        {
            EditorUtility.CopySerialized(templateButton, targetButton);
            targetButton.targetGraphic = targetImage;
        }

        while (target.childCount > 0)
        {
            Object.DestroyImmediate(target.GetChild(0).gameObject);
        }

        for (int i = 0; i < template.childCount; i++)
        {
            GameObject child = Object.Instantiate(template.GetChild(i).gameObject, target);
            child.name = template.GetChild(i).name;
            child.transform.SetSiblingIndex(i);
        }

        targetRect.anchorMin = templateRect.anchorMin;
        targetRect.anchorMax = templateRect.anchorMax;
        targetRect.pivot = templateRect.pivot;
        targetRect.sizeDelta = templateRect.sizeDelta;
        targetRect.localRotation = templateRect.localRotation;
        targetRect.localScale = templateRect.localScale;
        targetRect.anchoredPosition = anchoredPosition;
        target.gameObject.SetActive(active);
        EditorUtility.SetDirty(target.gameObject);
    }

    private static void EnsureDetailCloseButton(Transform detail, Transform closeTemplate)
    {
        if (FindChildTransform(detail, "ShopDetailCloseButton") != null)
        {
            return;
        }

        GameObject closeObject = Object.Instantiate(closeTemplate.gameObject, detail);
        closeObject.name = "ShopDetailCloseButton";
        RectTransform closeRect = closeObject.GetComponent<RectTransform>();
        RectTransform templateRect = closeTemplate as RectTransform;
        if (closeRect != null)
        {
            closeRect.anchorMin = Vector2.one;
            closeRect.anchorMax = Vector2.one;
            closeRect.pivot = Vector2.one;
            closeRect.anchoredPosition = new Vector2(-12f, -12f);
            closeRect.sizeDelta = templateRect == null ? new Vector2(48f, 48f) : templateRect.sizeDelta;
        }
        closeObject.transform.SetAsLastSibling();
        EditorUtility.SetDirty(closeObject);
    }

    private static Transform FindSceneTransform(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform result = FindChildTransform(roots[i].transform, objectName);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }

    private static Transform FindChildTransform(Transform parent, string objectName)
    {
        if (parent.name == objectName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindChildTransform(parent.GetChild(i), objectName);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }
}
