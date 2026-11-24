using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class IdleAdventureStartPreviewLayout
{
    private const string BookSkinResource = "IdleAdventureUI/FormationBook/";
    private BookSkinLayout bookSkinLayout;
    private bool bookSkinReady;

    [Serializable]
    private sealed class BookSkinLayout
    {
        public float width;
        public float height;
        public BookSkinRegion[] entries;
    }

    [Serializable]
    private sealed class BookSkinRegion
    {
        public string name;
        public float x;
        public float y;
        public float width;
        public float height;
        public Vector2[] corners;
    }

    private bool TryApplyBookSkin(RectTransform startPanel)
    {
        if (bookSkinLayout == null)
        {
            TextAsset layout = Resources.Load<TextAsset>(BookSkinResource + "Layout");
            if (layout == null) return false;
            bookSkinLayout = JsonUtility.FromJson<BookSkinLayout>(layout.text);
        }
        if (bookSkinLayout == null || bookSkinLayout.entries == null || LoadSprite(BookSkinResource + "Decor") == null) return false;
        bookSkinReady = true;
        DisableBookEffects(layoutRoot);
        foreach (string path in new[] { "Open Formation Button", "Start Battle Button", "Stage Selector" })
            DisableBookEffects(startPanel.Find(path));

        Image decor = FindImage(layoutRoot, "Book Decorations");
        if (decor == null) decor = CreateImage("Book Decorations", layoutRoot, Color.white);
        ApplyResourceSprite(decor, BookSkinResource + "Decor", false);
        Stretch(decor.rectTransform);
        decor.transform.SetAsFirstSibling();

        foreach (string path in new[] { "Start Left Page", "Start Right Page", "Start Status Strip" })
            SetBookObjectActive(startPanel.Find(path), false);
        Transform titleBlock = startPanel.Find("Start Title Block");
        if (titleBlock != null)
        {
            SetBookObjectActive(titleBlock.Find("Start Panel Title"), false);
            Anchor(titleBlock as RectTransform, new Vector2(0.22f, 0.022f), new Vector2(0.80f, 0.064f));
            TextMeshProUGUI body = FindText(titleBlock, "Start Panel Body Text");
            if (body != null)
            {
                Stretch(body.rectTransform);
                StyleBookText(body, 15f, new Color(0.30f, 0.20f, 0.13f), false);
            }
        }

        Image title = FindImage(layoutRoot, "Formation Title");
        ClearBookImage(title);
        PlaceBookRect(title.rectTransform, "FormationTitleText");
        TextMeshProUGUI titleLabel = FindText(title.transform, "Label");
        if (titleLabel == null) titleLabel = CreateText("Label", title.transform, "冒险小队", 42f, FontStyles.Bold);
        titleLabel.text = "冒险小队";
        Stretch(titleLabel.rectTransform);
        StyleBookText(titleLabel, 42f, Color.white, true);

        Image counter = FindImage(layoutRoot, "Formation Count Badge");
        ClearBookImage(counter);
        PlaceBookRect(counter.rectTransform, "FormationCountText");
        Stretch(formationCountText.rectTransform);
        StyleBookText(formationCountText, 27f, Color.white, true);

        Image stageTitle = FindImage(layoutRoot, "Stage Title Navigation");
        ClearBookImage(stageTitle);
        PlaceBookRect(stageTitle.rectTransform, "StageTitleText");
        Stretch(stageTitleText.rectTransform);
        StyleBookText(stageTitleText, 32f, Color.white, true);
        stageTitleText.enableWordWrapping = true;
        stageTitleText.lineSpacing = 5f;

        RectTransform formationContent = layoutRoot.Find("Formation Preview Content") as RectTransform;
        PlaceBookRect(formationContent, "FormationHall");
        SetBookObjectActive(formationContent.Find("Formation Preview Background"), false);
        EnsureFormationHall();
        SetBookObjectActive(layoutRoot.Find("Formation Preview Frame"), false);
        ConfigureBookFormation();

        RectTransform enemyContent = layoutRoot.Find("Enemy Preview Content") as RectTransform;
        PlaceBookRect(enemyContent, "EnemyContent");
        SetBookObjectActive(enemyContent.Find("Enemy Preview Background"), false);
        SetBookObjectActive(enemyContent.Find("Enemy Preview Shade"), false);
        SetBookObjectActive(layoutRoot.Find("Enemy Preview Frame"), false);
        SetBookObjectActive(layoutRoot.Find("Reward Preview Frame"), false);
        Anchor(enemySummaryText.rectTransform, new Vector2(0.22f, 0.89f), new Vector2(0.98f, 0.99f));
        StyleBookText(enemySummaryText, 19f, new Color(0.38f, 0.27f, 0.19f), false);
        foreach (TextMeshProUGUI label in enemyLabels)
        {
            StyleBookText(label, 19f, new Color(0.25f, 0.17f, 0.11f), false);
            label.enableWordWrapping = true;
        }
        foreach (Image image in enemyImages)
            Anchor(image.rectTransform, new Vector2(0.04f, 0.22f), new Vector2(0.96f, 0.98f));

        SetBookObjectActive(startPanel.Find("Open Formation Button"), false);
        ConfigureBookFormationIcon(quickAutoFillButton, "编队", "自动编队", 830f);
        ConfigureBookFormationIcon(quickClearButton, "一键清除", "清空编队", 930f);
        EnsureBookRoster();
        ConfigureBookButton(FindButton(startPanel, "Start Battle Button"), "StartBattle", "开始挑战", true);
        RectTransform selector = startPanel.Find("Stage Selector") as RectTransform;
        if (selector != null)
        {
            Stretch(selector);
            ClearBookImage(selector.GetComponent<Image>());
            SetBookObjectActive(selector.Find("Stage Selection Text"), false);
            ConfigureBookButton(FindButton(selector, "Previous Stage Button"), "PreviousStage", null, false);
            ConfigureBookButton(FindButton(selector, "Next Stage Button"), "NextStage", null, false);
        }

        TextMeshProUGUI rewardTitle = FindText(layoutRoot, "Book Reward Title");
        if (rewardTitle == null) rewardTitle = CreateText("Book Reward Title", layoutRoot, "通关奖励", 26f, FontStyles.Bold);
        PlaceBookRect(rewardTitle.rectTransform, "RewardTitleText");
        StyleBookText(rewardTitle, 26f, Color.white, true);
        return true;
    }

    private void ConfigureBookFormation()
    {
        for (int row = 0; row < 3; row++)
        {
            ConfigureBookFormationSlot(row + 3, row, false);
            ConfigureBookFormationSlot(row, row, true);
        }
    }

    private void ConfigureBookFormationIcon(Button button, string icon, string caption, float horizontal)
    {
        if (button == null) return;
        DisableBookEffects(button.transform);
        RectTransform rect = button.transform as RectTransform;
        Anchor(rect, new Vector2(horizontal / 2048f, 1f - 742f / 1152f),
            new Vector2((horizontal + 66f) / 2048f, 1f - 672f / 1152f));
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true)) label.gameObject.SetActive(false);
        SmallButtonIconUtility.ApplyIconOnlyToButton(button, icon);
        TextMeshProUGUI actionLabel = FindText(button.transform, "Action Caption");
        if (actionLabel == null) actionLabel = CreateText("Action Caption", button.transform, caption, 17f, FontStyles.Bold);
        actionLabel.gameObject.layer = button.gameObject.layer;
        actionLabel.text = caption;
        actionLabel.gameObject.SetActive(true);
        StyleBookText(actionLabel, 17f, new Color(0.28f, 0.18f, 0.12f), false);
        actionLabel.enableAutoSizing = false;
        RectTransform captionRect = actionLabel.rectTransform;
        captionRect.anchorMin = captionRect.anchorMax = new Vector2(0.5f, 0f);
        captionRect.pivot = new Vector2(0.5f, 1f);
        captionRect.sizeDelta = new Vector2(74f, 32f);
        captionRect.anchoredPosition = new Vector2(0f, -3f);
        actionLabel.transform.SetAsLastSibling();
        button.gameObject.SetActive(true);
        button.transform.SetAsLastSibling();
    }

    private void EnsureBookRoster()
    {
        RectTransform root = layoutRoot.Find("Formation Roster") as RectTransform;
        if (root == null) root = CreateRect("Formation Roster", layoutRoot);
        Anchor(root, new Vector2(314f / 2048f, 1f - 995f / 1152f),
            new Vector2(1011f / 2048f, 1f - 760f / 1152f));
        formationRoster = root.GetComponent<IdleAdventureFormationRoster>();
        if (formationRoster == null) formationRoster = root.gameObject.AddComponent<IdleAdventureFormationRoster>();
        formationRoster.EnsureLayout();
    }

    private void ConfigureBookFormationSlot(int index, int row, bool frontColumn)
    {
        Image portrait = formationPortraits[index];
        RectTransform slot = formationSlots[index].rectTransform;
        ClearPreviewCardBackground(formationSlots[index]);
        SetBookObjectActive(formationSlotLabels[index].transform, false);
        Vector2 foot = new Vector2(0.5f, 0.04f);
        float bodyFraction = 0.93f;
        Sprite sprite = portrait.sprite;
        CharacterIdleImage animation = portrait.GetComponent<CharacterIdleImage>();
        if (sprite != null && animation != null && animation.Sequence != null)
        {
            foot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            switch (animation.Sequence.name)
            {
                case "Npc_2_Idle": bodyFraction = 277f / 303f; break;
                case "Npc_3_Idle": bodyFraction = 271f / 291f; break;
                case "Npc_4_Idle": bodyFraction = 275.5f / 296f; break;
                case "Npc_5_Idle": bodyFraction = 280f / 300f; break;
                case "Npc_6_Idle": bodyFraction = 283f / 296f; break;
                case "Npc_7_Idle": bodyFraction = 277f / 292f; break;
            }
        }
        else if (sprite != null && sprite.name == "Npc_1")
        {
            foot = new Vector2(0.48f, 202f / 5499f);
            bodyFraction = 5096f / 5499f;
        }
        float columnSpacing = 0.17f;
        float horizontal = 0.265f + (frontColumn ? 2f : 0f) * columnSpacing;
        if (row == 1) horizontal += columnSpacing * (1f + 0.45f / 2.7f);
        float ground = 0.46f - row * 0.16f;
        float height = (0.275f + row * 0.02f) / bodyFraction;
        slot.pivot = foot;
        Anchor(slot, new Vector2(horizontal, ground - foot.y * height), new Vector2(horizontal, ground + (1f - foot.y) * height));
        slot.localRotation = Quaternion.identity;
        slot.localScale = Vector3.one;
        AspectRatioFitter fitter = slot.GetComponent<AspectRatioFitter>();
        if (fitter == null) fitter = slot.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectRatio = sprite != null ? sprite.rect.width / sprite.rect.height : 1f;
        fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
        fitter.SetLayoutVertical();
        slot.anchoredPosition = Vector2.zero;
        Stretch(portrait.rectTransform);
        portrait.rectTransform.localRotation = Quaternion.identity;
        portrait.rectTransform.localScale = Vector3.one;
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        Image shadow = FindImage(slot, "Ground Shadow");
        if (shadow == null) shadow = CreateImage("Ground Shadow", slot, Color.white);
        ApplyResourceSprite(shadow, "IdleAdventureUI/FormationFootShadow", false);
        shadow.color = new Color(0.19f, 0.12f, 0.07f, 0.30f);
        shadow.raycastTarget = false;
        float halfWidth = 0.23f / fitter.aspectRatio;
        Anchor(shadow.rectTransform, new Vector2(foot.x - halfWidth, foot.y - 0.035f), new Vector2(foot.x + halfWidth, foot.y + 0.035f));
        shadow.transform.SetAsFirstSibling();
        shadow.gameObject.SetActive(portrait.gameObject.activeSelf && sprite != null);
        slot.SetAsLastSibling();
        BindFormationSlotInteraction(slot, index, portrait);
        ConfigureBookEmptySlot(index, row, horizontal, ground);
    }

    private void ConfigureBookEmptySlot(int index, int row, float horizontal, float ground)
    {
        Transform parent = formationSlots[index].transform.parent;
        string markerName = $"Formation Empty Slot {index + 1}";
        RectTransform marker = parent.Find(markerName) as RectTransform;
        if (marker == null) marker = CreateRect(markerName, parent);
        float halfWidth = (90f + row * 7f) / (657.23883f * 1920f / 2048f) * 0.5f;
        marker.pivot = new Vector2(0.5f, 55f / 208f);
        Anchor(marker, new Vector2(horizontal - halfWidth, ground), new Vector2(horizontal + halfWidth, ground));
        marker.localScale = Vector3.one;
        marker.localRotation = Quaternion.identity;
        AspectRatioFitter fitter = marker.GetComponent<AspectRatioFitter>();
        if (fitter == null) fitter = marker.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectRatio = 320f / 208f;
        fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
        marker.anchoredPosition = Vector2.zero;
        Image ring = ConfigureEmptySlotImage(marker, "Ground Ring", "FormationEmptySlotRing");
        Image shadow = ConfigureEmptySlotImage(marker, "Plus Shadow", "FormationEmptySlotShadow");
        Image plus = ConfigureEmptySlotImage(marker, "Floating Plus", "FormationEmptySlotPlus");
        plus.color = new Color(1f, 1f, 1f, 0.70f);
        ring.transform.SetAsFirstSibling();
        plus.transform.SetAsLastSibling();
        IdleAdventureEmptySlotMarker animation = marker.GetComponent<IdleAdventureEmptySlotMarker>();
        if (animation == null) animation = marker.gameObject.AddComponent<IdleAdventureEmptySlotMarker>();
        animation.Initialize(plus.rectTransform, shadow.rectTransform);
        marker.SetAsLastSibling();
        BindFormationSlotInteraction(marker, index, null);
        marker.gameObject.SetActive(!formationOccupied[index]);
    }

    private Image ConfigureEmptySlotImage(RectTransform marker, string childName, string resource)
    {
        Image image = FindImage(marker, childName);
        if (image == null) image = CreateImage(childName, marker, Color.white);
        ApplyResourceSprite(image, "IdleAdventureUI/" + resource, false);
        Stretch(image.rectTransform);
        image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        image.rectTransform.localScale = Vector3.one;
        image.rectTransform.localRotation = Quaternion.identity;
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private void EnsureFormationHall()
    {
        BookSkinRegion region = Array.Find(bookSkinLayout.entries, item => item.name == "FormationHall");
        if (region == null || region.corners == null || region.corners.Length != 4) return;
        RectTransform viewport = layoutRoot.Find("Formation Hall Viewport") as RectTransform;
        if (viewport == null) viewport = CreateRect("Formation Hall Viewport", layoutRoot);
        PlaceBookRect(viewport, "FormationHall");
        viewport.SetAsFirstSibling();
        IdleAdventureFormationHallMask shape = viewport.GetComponent<IdleAdventureFormationHallMask>();
        if (shape == null) shape = viewport.gameObject.AddComponent<IdleAdventureFormationHallMask>();
        shape.Configure(region.corners);
        Mask mask = viewport.GetComponent<Mask>();
        if (mask == null) mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        Image hall = FindImage(viewport, "Formation Hall Image");
        if (hall == null) hall = CreateImage("Formation Hall Image", viewport, Color.white);
        ApplyResourceSprite(hall, FormationBackgroundResource, false);
        hall.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        hall.rectTransform.localScale = Vector3.one;
        hall.rectTransform.localRotation = Quaternion.identity;
        AspectRatioFitter fitter = hall.GetComponent<AspectRatioFitter>();
        if (fitter == null) fitter = hall.gameObject.AddComponent<AspectRatioFitter>();
        if (hall.sprite != null) fitter.aspectRatio = hall.sprite.rect.width / hall.sprite.rect.height;
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
    }

    private void ConfigureBookRewards()
    {
        RectTransform content = layoutRoot != null ? layoutRoot.Find("Reward Preview Content") as RectTransform : null;
        if (content == null || !HasRewardBindings()) return;
        PlaceBookRect(content, "RewardArea");
        RectTransform title = layoutRoot.Find("Book Reward Title") as RectTransform;
        if (title != null)
        {
            float center = (title.anchorMin.x + title.anchorMax.x) * 0.5f;
            float halfWidth = (content.anchorMax.x - content.anchorMin.x) * 0.5f;
            content.anchorMin = new Vector2(center - halfWidth, content.anchorMin.y);
            content.anchorMax = new Vector2(center + halfWidth, content.anchorMax.y);
        }
        ClearBookImage(content.GetComponent<Image>());
        SetBookObjectActive(content.Find("Reward Category"), false);
        int count = 0;
        foreach (Image slot in rewardSlots)
            if (slot.gameObject.activeSelf) count++;
        float width = 0.207f;
        float spacing = 0.033f;
        float total = count * width + Mathf.Max(0, count - 1) * spacing;
        int visibleIndex = 0;
        for (int index = 0; index < RewardSlotCount; index++)
        {
            Image slot = rewardSlots[index];
            if (!slot.gameObject.activeSelf) continue;
            float left = (1f - total) * 0.5f + visibleIndex * (width + spacing);
            Anchor(slot.rectTransform, new Vector2(left, 0.5f), new Vector2(left + width, 0.5f));
            slot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            slot.rectTransform.localScale = Vector3.one;
            slot.rectTransform.localRotation = Quaternion.identity;
            Image icon = rewardIcons[index];
            bool ink = icon.sprite != null && icon.sprite.name == "InkIcon";
            ApplyResourceSprite(slot, BookSkinResource + (ink ? "RewardInk" : "RewardGold"), false);
            AspectRatioFitter fitter = slot.GetComponent<AspectRatioFitter>();
            if (fitter == null) fitter = slot.gameObject.AddComponent<AspectRatioFitter>();
            if (slot.sprite != null) fitter.aspectRatio = slot.sprite.rect.width / slot.sprite.rect.height;
            fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            DisableBookEffects(slot.transform);
            Anchor(icon.rectTransform, new Vector2(0.22f, 0.40f), new Vector2(0.78f, 0.92f));
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            icon.rectTransform.localScale = Vector3.one;
            icon.rectTransform.localRotation = Quaternion.identity;
            icon.preserveAspect = true;
            StyleBookRewardLabel(rewardLabels[index], icon.sprite != null);
            rewardLabels[index].transform.SetAsLastSibling();
            visibleIndex++;
        }
    }

    private static void StyleBookRewardLabel(TextMeshProUGUI label, bool hasIcon)
    {
        Anchor(label.rectTransform, hasIcon ? new Vector2(0.12f, 0.12f) : new Vector2(0.10f, 0.10f), hasIcon ? new Vector2(0.88f, 0.36f) : new Vector2(0.90f, 0.90f));
        label.rectTransform.localScale = Vector3.one;
        label.rectTransform.localRotation = Quaternion.identity;
        StyleBookText(label, hasIcon ? 25f : 17f, new Color(0.25f, 0.17f, 0.11f), hasIcon);
        if (hasIcon)
        {
            label.outlineColor = new Color(1f, 0.96f, 0.83f);
            label.outlineWidth = 0.12f;
            label.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.18f);
        }
        else label.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = !hasIcon;
    }

    private Sprite ResolveBookCurrency(string key)
    {
        string normalized = (key ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized == "gold" || normalized == "金币") return LoadSprite(BookSkinResource + "GoldIcon");
        if (normalized == "diamond" || normalized == "crystal" || normalized == "bluecrystal" || normalized == "万能彩墨" || normalized == "彩墨" || normalized == "钻石")
            return LoadSprite(BookSkinResource + "InkIcon");
        return null;
    }

    private void ConfigureBookButton(Button button, string region, string text, bool primary)
    {
        if (button == null) return;
        DisableBookEffects(button.transform);
        PlaceBookRect(button.transform as RectTransform, region);
        Image image = button.GetComponent<Image>();
        ApplyResourceSprite(image, BookSkinResource + region, false);
        image.raycastTarget = true;
        image.raycastPadding = Vector4.zero;
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.98f, 0.94f);
        colors.pressedColor = new Color(0.78f, 0.82f, 0.90f);
        colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.8f);
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        SetBookObjectActive(button.transform.Find("SmallIcon"), false);
        TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
        for (int index = 0; index < labels.Length; index++)
        {
            TextMeshProUGUI label = labels[index] as TextMeshProUGUI;
            if (label == null) continue;
            label.gameObject.SetActive(text != null && index == 0);
            if (text == null || index != 0) continue;
            label.text = text;
            Anchor(label.rectTransform, primary ? new Vector2(0.31f, 0.15f) : new Vector2(0.07f, 0.14f), primary ? new Vector2(0.82f, 0.78f) : new Vector2(0.93f, 0.72f));
            StyleBookText(label, primary ? 31.2f : 27f, primary ? Color.white : new Color(0.30f, 0.20f, 0.14f), primary);
        }
    }

    private void PlaceBookRect(RectTransform rect, string name)
    {
        if (rect == null || bookSkinLayout == null) return;
        foreach (BookSkinRegion region in bookSkinLayout.entries)
        {
            if (region.name != name) continue;
            Anchor(rect,
                new Vector2(region.x / bookSkinLayout.width, 1f - (region.y + region.height) / bookSkinLayout.height),
                new Vector2((region.x + region.width) / bookSkinLayout.width, 1f - region.y / bookSkinLayout.height));
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return;
        }
    }

    private static void StyleBookText(TextMeshProUGUI label, float size, Color color, bool outlined)
    {
        label.font = RuntimeFontAssetProvider.Resolve();
        label.fontSize = size;
        label.fontSizeMin = Mathf.Min(14f, size);
        label.fontSizeMax = size;
        label.enableAutoSizing = true;
        label.fontStyle = FontStyles.Bold;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        label.richText = false;
        label.margin = Vector4.zero;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.outlineColor = new Color(0.10f, 0.17f, 0.30f, 1f);
        label.outlineWidth = outlined ? 0.16f : 0f;
    }

    private static void ClearBookImage(Image image)
    {
        if (image == null) return;
        image.sprite = null;
        image.color = Color.clear;
        image.raycastTarget = false;
    }

    private static void DisableBookEffects(Transform root)
    {
        if (root == null) return;
        foreach (IdleAdventureBookPagePerspective effect in root.GetComponentsInChildren<IdleAdventureBookPagePerspective>(true))
            effect.enabled = false;
        foreach (Shadow shadow in root.GetComponentsInChildren<Shadow>(true)) shadow.enabled = false;
        foreach (RectMask2D mask in root.GetComponentsInChildren<RectMask2D>(true)) mask.enabled = false;
    }

    private static void SetBookObjectActive(Transform target, bool active)
    {
        if (target != null) target.gameObject.SetActive(active);
    }
}
