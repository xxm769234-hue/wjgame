using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LegacyText = UnityEngine.UI.Text;
using LegacyInputField = UnityEngine.UI.InputField;

public static class TmpFontProjectMigrator
{
    private const string FontAssetPath = "Assets/_Imported/weijun-game-project/Assets/Sprites/font/SourceHanSansSC-Normal SDF.asset";
    private const string ExcludedPath = "Assets/TextMesh Pro/Examples & Extras/";

    [MenuItem("Tools/WeiJun UI/Migrate All Text To Source Han TMP")]
    public static void MigrateAll()
    {
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fontAsset == null)
        {
            Debug.LogError("SourceHanSansSC-Normal SDF was not found at " + FontAssetPath);
            return;
        }

        int sceneCount = MigrateScenes(fontAsset);
        int prefabCount = MigratePrefabs(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Migrated " + sceneCount + " scenes and " + prefabCount + " prefabs to SourceHanSansSC-Normal SDF.");
    }

    private static int MigrateScenes(TMP_FontAsset fontAsset)
    {
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        int migratedCount = 0;
        for (int i = 0; i < sceneGuids.Length; i++)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
            if (scenePath.StartsWith(ExcludedPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            bool changed = false;
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    changed |= MigrateHierarchy(roots[rootIndex], fontAsset);
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    migratedCount++;
                }
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        return migratedCount;
    }

    private static int MigratePrefabs(TMP_FontAsset fontAsset)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int migratedCount = 0;
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            if (prefabPath.StartsWith(ExcludedPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            bool changed = false;
            try
            {
                changed = MigrateHierarchy(root, fontAsset);
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    migratedCount++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return migratedCount;
    }

    private static bool MigrateHierarchy(GameObject root, TMP_FontAsset fontAsset)
    {
        if (root == null)
        {
            return false;
        }

        LegacyText[] legacyTexts = root.GetComponentsInChildren<LegacyText>(true);
        LegacyInputField[] legacyInputs = root.GetComponentsInChildren<LegacyInputField>(true);
        Dictionary<LegacyText, TMP_Text> temporaryTextMap = new Dictionary<LegacyText, TMP_Text>();
        Dictionary<LegacyInputField, TMP_InputField> temporaryInputMap = new Dictionary<LegacyInputField, TMP_InputField>();
        Dictionary<LegacyText, TMP_Text> finalTextMap = new Dictionary<LegacyText, TMP_Text>();
        Dictionary<LegacyInputField, TMP_InputField> finalInputMap = new Dictionary<LegacyInputField, TMP_InputField>();
        Dictionary<LegacyText, LegacyTextData> textDataMap = new Dictionary<LegacyText, LegacyTextData>();
        Dictionary<LegacyText, GameObject> textTargetMap = new Dictionary<LegacyText, GameObject>();
        Dictionary<LegacyInputField, GameObject> inputTargetMap = new Dictionary<LegacyInputField, GameObject>();
        Dictionary<LegacyInputField, LegacyText> inputTextComponentMap = new Dictionary<LegacyInputField, LegacyText>();
        Dictionary<LegacyInputField, LegacyText> inputPlaceholderMap = new Dictionary<LegacyInputField, LegacyText>();
        List<GameObject> temporaryObjects = new List<GameObject>();
        bool changed = false;

        for (int i = 0; i < legacyTexts.Length; i++)
        {
            LegacyText legacy = legacyTexts[i];
            if (legacy == null)
            {
                continue;
            }

            LegacyTextData data = CaptureText(legacy);
            GameObject temporaryObject = new GameObject("TmpTextTemplate");
            TMP_Text temporaryText = temporaryObject.AddComponent<TextMeshProUGUI>();
            CopyText(data, temporaryText, fontAsset);
            temporaryObjects.Add(temporaryObject);
            temporaryTextMap[legacy] = temporaryText;
            textDataMap[legacy] = data;
            textTargetMap[legacy] = legacy.gameObject;
            changed = true;
        }

        for (int i = 0; i < legacyInputs.Length; i++)
        {
            LegacyInputField legacy = legacyInputs[i];
            if (legacy == null)
            {
                continue;
            }

            GameObject target = legacy.gameObject;
            GameObject temporaryObject = new GameObject("TmpInputFieldTemplate");
            TMP_InputField temporaryInput = temporaryObject.AddComponent<TMP_InputField>();
            TMP_Text textComponent = GetMappedText(temporaryTextMap, legacy.textComponent);
            TMP_Text placeholderComponent = GetMappedText(temporaryTextMap, legacy.placeholder as LegacyText);
            CopyInputField(legacy, temporaryInput, textComponent, placeholderComponent);
            temporaryObjects.Add(temporaryObject);
            temporaryInputMap[legacy] = temporaryInput;
            inputTargetMap[legacy] = target;
            inputTextComponentMap[legacy] = legacy.textComponent;
            inputPlaceholderMap[legacy] = legacy.placeholder as LegacyText;
            changed = true;
        }

        if (legacyTexts.Length > 0 || legacyInputs.Length > 0)
        {
            ReplaceReferences(root, temporaryTextMap, temporaryInputMap);
        }

        for (int i = 0; i < legacyTexts.Length; i++)
        {
            LegacyText legacy = legacyTexts[i];
            if (legacy == null || !textTargetMap.TryGetValue(legacy, out GameObject target))
            {
                continue;
            }

            UnityEngine.Object.DestroyImmediate(legacy);
            TMP_Text replacement = target.AddComponent<TextMeshProUGUI>();
            CopyText(textDataMap[legacy], replacement, fontAsset);
            finalTextMap[legacy] = replacement;
        }

        for (int i = 0; i < legacyInputs.Length; i++)
        {
            LegacyInputField legacy = legacyInputs[i];
            if (legacy == null || !inputTargetMap.TryGetValue(legacy, out GameObject target))
            {
                continue;
            }

            TMP_InputField temporaryInput = temporaryInputMap[legacy];
            UnityEngine.Object.DestroyImmediate(legacy);
            TMP_InputField replacement = target.AddComponent<TMP_InputField>();
            CopyInputFieldSettings(temporaryInput, replacement);
            replacement.textComponent = GetMappedText(finalTextMap, inputTextComponentMap[legacy]);
            replacement.placeholder = GetMappedText(finalTextMap, inputPlaceholderMap[legacy]);
            finalInputMap[legacy] = replacement;
        }

        Dictionary<TMP_Text, TMP_Text> temporaryToFinalTextMap = new Dictionary<TMP_Text, TMP_Text>();
        foreach (KeyValuePair<LegacyText, TMP_Text> pair in temporaryTextMap)
        {
            if (finalTextMap.TryGetValue(pair.Key, out TMP_Text replacement))
            {
                temporaryToFinalTextMap[pair.Value] = replacement;
            }
        }

        Dictionary<TMP_InputField, TMP_InputField> temporaryToFinalInputMap = new Dictionary<TMP_InputField, TMP_InputField>();
        foreach (KeyValuePair<LegacyInputField, TMP_InputField> pair in temporaryInputMap)
        {
            if (finalInputMap.TryGetValue(pair.Key, out TMP_InputField replacement))
            {
                temporaryToFinalInputMap[pair.Value] = replacement;
            }
        }

        if (temporaryToFinalTextMap.Count > 0 || temporaryToFinalInputMap.Count > 0)
        {
            ReplaceTemporaryReferences(root, temporaryToFinalTextMap, temporaryToFinalInputMap);
        }

        for (int i = 0; i < temporaryObjects.Count; i++)
        {
            if (temporaryObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(temporaryObjects[i]);
            }
        }

        TMP_Text[] tmpTexts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmpTexts.Length; i++)
        {
            if (tmpTexts[i] != null
                && (tmpTexts[i].font != fontAsset
                    || (fontAsset != null && fontAsset.material != null && tmpTexts[i].fontSharedMaterial != fontAsset.material)))
            {
                tmpTexts[i].font = fontAsset;
                if (fontAsset != null && fontAsset.material != null)
                {
                    tmpTexts[i].fontSharedMaterial = fontAsset.material;
                }
                changed = true;
            }
        }

        return changed;
    }

    private static LegacyTextData CaptureText(LegacyText source)
    {
        return new LegacyTextData
        {
            Text = source.text,
            FontSize = source.fontSize,
            FontStyle = source.fontStyle,
            Color = source.color,
            Alignment = source.alignment,
            BestFit = source.resizeTextForBestFit,
            MinSize = source.resizeTextMinSize,
            MaxSize = source.resizeTextMaxSize,
            Wrap = source.horizontalOverflow == HorizontalWrapMode.Wrap,
            Overflow = source.verticalOverflow == VerticalWrapMode.Overflow,
            RichText = source.supportRichText,
            RaycastTarget = source.raycastTarget,
            Enabled = source.enabled
        };
    }

    private static void CopyText(LegacyTextData source, TMP_Text destination, TMP_FontAsset fontAsset)
    {
        if (destination == null)
        {
            return;
        }

        destination.font = fontAsset;
        if (fontAsset != null && fontAsset.material != null)
        {
            destination.fontSharedMaterial = fontAsset.material;
        }

        destination.text = source.Text ?? string.Empty;
        destination.fontSize = Mathf.Max(1, source.FontSize);
        destination.fontStyle = ConvertFontStyle(source.FontStyle);
        destination.color = source.Color;
        destination.alignment = ConvertAlignment(source.Alignment);
        destination.enableAutoSizing = source.BestFit;
        destination.fontSizeMin = Mathf.Max(1, source.MinSize);
        destination.fontSizeMax = Mathf.Max(destination.fontSizeMin, source.MaxSize);
        destination.enableWordWrapping = source.Wrap;
        destination.overflowMode = source.Overflow
            ? TextOverflowModes.Overflow
            : TextOverflowModes.Truncate;
        destination.richText = source.RichText;
        destination.raycastTarget = source.RaycastTarget;
        destination.enabled = source.Enabled;
    }

    private struct LegacyTextData
    {
        public string Text;
        public int FontSize;
        public FontStyle FontStyle;
        public Color Color;
        public TextAnchor Alignment;
        public bool BestFit;
        public int MinSize;
        public int MaxSize;
        public bool Wrap;
        public bool Overflow;
        public bool RichText;
        public bool RaycastTarget;
        public bool Enabled;
    }

    private static void CopyInputField(
        LegacyInputField source,
        TMP_InputField destination,
        TMP_Text textComponent,
        TMP_Text placeholderComponent)
    {
        destination.text = source.text ?? string.Empty;
        destination.characterLimit = source.characterLimit;
        destination.contentType = ConvertContentType(source.contentType);
        destination.lineType = ConvertLineType(source.lineType);
        destination.interactable = source.interactable;
        destination.transition = source.transition;
        destination.colors = source.colors;
        destination.targetGraphic = source.targetGraphic;
        destination.readOnly = source.readOnly;
        destination.caretBlinkRate = source.caretBlinkRate;
        destination.caretWidth = source.caretWidth;
        destination.selectionColor = source.selectionColor;
        destination.shouldHideMobileInput = source.shouldHideMobileInput;
        destination.asteriskChar = source.asteriskChar;
        destination.textComponent = textComponent;
        destination.placeholder = placeholderComponent;
        CopySerializedProperty(source, destination, "m_OnValueChanged");
        CopySerializedProperty(source, destination, "m_OnEndEdit");
        CopySerializedProperty(source, destination, "m_OnSubmit");
    }

    private static void CopyInputFieldSettings(TMP_InputField source, TMP_InputField destination)
    {
        destination.text = source.text ?? string.Empty;
        destination.characterLimit = source.characterLimit;
        destination.contentType = source.contentType;
        destination.lineType = source.lineType;
        destination.interactable = source.interactable;
        destination.transition = source.transition;
        destination.colors = source.colors;
        destination.targetGraphic = source.targetGraphic;
        destination.readOnly = source.readOnly;
        destination.caretBlinkRate = source.caretBlinkRate;
        destination.caretWidth = source.caretWidth;
        destination.selectionColor = source.selectionColor;
        destination.shouldHideMobileInput = source.shouldHideMobileInput;
        destination.asteriskChar = source.asteriskChar;
        destination.textComponent = source.textComponent;
        destination.placeholder = source.placeholder;
        CopySerializedProperty(source, destination, "m_OnValueChanged");
        CopySerializedProperty(source, destination, "m_OnEndEdit");
        CopySerializedProperty(source, destination, "m_OnSubmit");
    }

    private static void CopySerializedProperty(Component source, Component destination, string propertyName)
    {
        SerializedObject sourceObject = new SerializedObject(source);
        SerializedObject destinationObject = new SerializedObject(destination);
        SerializedProperty sourceProperty = sourceObject.FindProperty(propertyName);
        SerializedProperty destinationProperty = destinationObject.FindProperty(propertyName);
        if (sourceProperty != null && destinationProperty != null)
        {
            destinationObject.CopyFromSerializedProperty(sourceProperty);
            destinationObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static TMP_Text GetMappedText(Dictionary<LegacyText, TMP_Text> textMap, LegacyText source)
    {
        if (ReferenceEquals(source, null) || textMap == null)
        {
            return null;
        }

        textMap.TryGetValue(source, out TMP_Text replacement);
        return replacement;
    }

    private static void ReplaceReferences(
        GameObject root,
        Dictionary<LegacyText, TMP_Text> textMap,
        Dictionary<LegacyInputField, TMP_InputField> inputMap)
    {
        Component[] components = root.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
            {
                continue;
            }

            if (component is LegacyText || component is LegacyInputField)
            {
                continue;
            }

            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            bool changed = false;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                if (property.objectReferenceValue is LegacyText legacyText
                    && textMap.TryGetValue(legacyText, out TMP_Text replacementText))
                {
                    property.objectReferenceValue = replacementText;
                    changed = true;
                }
                else if (property.objectReferenceValue is LegacyInputField legacyInput
                    && inputMap.TryGetValue(legacyInput, out TMP_InputField replacementInput))
                {
                    property.objectReferenceValue = replacementInput;
                    changed = true;
                }
            }

            if (changed)
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static void ReplaceTemporaryReferences(
        GameObject root,
        Dictionary<TMP_Text, TMP_Text> textMap,
        Dictionary<TMP_InputField, TMP_InputField> inputMap)
    {
        Component[] components = root.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
            {
                continue;
            }

            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            bool changed = false;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                if (property.objectReferenceValue is TMP_Text temporaryText
                    && textMap.TryGetValue(temporaryText, out TMP_Text replacementText))
                {
                    property.objectReferenceValue = replacementText;
                    changed = true;
                }
                else if (property.objectReferenceValue is TMP_InputField temporaryInput
                    && inputMap.TryGetValue(temporaryInput, out TMP_InputField replacementInput))
                {
                    property.objectReferenceValue = replacementInput;
                    changed = true;
                }
            }

            if (changed)
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static FontStyles ConvertFontStyle(FontStyle style)
    {
        switch (style)
        {
            case FontStyle.Bold: return FontStyles.Bold;
            case FontStyle.Italic: return FontStyles.Italic;
            case FontStyle.BoldAndItalic: return FontStyles.Bold | FontStyles.Italic;
            default: return FontStyles.Normal;
        }
    }

    private static TextAlignmentOptions ConvertAlignment(TextAnchor alignment)
    {
        switch (alignment)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.MidlineLeft;
            case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.MidlineRight;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.TopLeft;
        }
    }

    private static TMP_InputField.ContentType ConvertContentType(LegacyInputField.ContentType contentType)
    {
        switch (contentType)
        {
            case LegacyInputField.ContentType.Autocorrected: return TMP_InputField.ContentType.Autocorrected;
            case LegacyInputField.ContentType.IntegerNumber: return TMP_InputField.ContentType.IntegerNumber;
            case LegacyInputField.ContentType.DecimalNumber: return TMP_InputField.ContentType.DecimalNumber;
            case LegacyInputField.ContentType.Alphanumeric: return TMP_InputField.ContentType.Alphanumeric;
            case LegacyInputField.ContentType.Name: return TMP_InputField.ContentType.Name;
            case LegacyInputField.ContentType.EmailAddress: return TMP_InputField.ContentType.EmailAddress;
            case LegacyInputField.ContentType.Password: return TMP_InputField.ContentType.Password;
            case LegacyInputField.ContentType.Pin: return TMP_InputField.ContentType.Pin;
            case LegacyInputField.ContentType.Custom: return TMP_InputField.ContentType.Custom;
            default: return TMP_InputField.ContentType.Standard;
        }
    }

    private static TMP_InputField.LineType ConvertLineType(LegacyInputField.LineType lineType)
    {
        switch (lineType)
        {
            case LegacyInputField.LineType.MultiLineNewline: return TMP_InputField.LineType.MultiLineNewline;
            case LegacyInputField.LineType.MultiLineSubmit: return TMP_InputField.LineType.MultiLineSubmit;
            default: return TMP_InputField.LineType.SingleLine;
        }
    }
}
