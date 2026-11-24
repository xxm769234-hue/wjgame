using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class HomePageUI
{
    private void ConfigureHomePanelReadability()
    {
        if (scenePanels == null) return;
        foreach (HomePageScenePanel panel in scenePanels.GetComponentsInChildren<HomePageScenePanel>(true))
        {
            Transform root = panel.PanelRoot;
            if (panel.PanelType == HomePageScenePanelType.Shop)
            {
                PlaceReadableRect(root, "ShopDetailIcon", 70f, 132f, 84f, 84f);
                PlaceReadableRect(root, "ShopDetailName", 170f, 130f, 220f, 40f);
                PlaceReadableRect(root, "ShopDetailReward", 170f, 178f, 220f, 32f);
                PlaceReadableRect(root, "ShopDetailDescription", 70f, 220f, 320f, 110f);
                PlaceReadableRect(root, "ShopDetailPrice", 70f, 337f, 320f, 30f);
                PlaceReadableRect(root, "ShopPurchaseBox", 425f, 132f, 300f, 215f);
                PlaceReadableRect(root, "ShopPurchaseTitle", 16f, 12f, 268f, 30f);
                PlaceReadableRect(root, "ShopPurchaseSummary", 16f, 48f, 268f, 32f);
                PlaceReadableRect(root, "ShopOwnedCount", 16f, 84f, 268f, 30f);
                PlaceReadableRect(root, "ShopStorageModeText", 16f, 118f, 268f, 42f);
                PlaceReadableRect(root, "Count", 16f, 173f, 82f, 30f);
                PlaceReadableRect(root, "ShopLotDecreaseButton", 102f, 172f, 32f, 30f);
                PlaceReadableRect(root, "ShopLotCountText", 138f, 172f, 102f, 30f);
                PlaceReadableRect(root, "ShopLotIncreaseButton", 244f, 172f, 32f, 30f);
                SetSceneObjectActive(root, "ShopDetailDescription", true);
            }
            else if (panel.PanelType == HomePageScenePanelType.AccountProfile)
            {
                string[] columns = { "ProfileExperienceText", "ProfileAccountStatusText", "ProfileCancellationDetailsText", "ProfileComplianceText" };
                for (int index = 0; index < columns.Length; index++)
                    PlaceReadableRect(root, columns[index], index * 275f, 0f, 255f, 100f);
                ResizeReadableRect(root, "ProfileIdentifier", 222f, 38f);
                ResizeReadableRect(root, "ProfileSignStat", 200f, 70f);
                ResizeReadableRect(root, "ProfileFriendStat", 200f, 70f);
                ConfigureReadableButtonLabel(root, "ProfileCopyIdButton", 17f);
                ConfigureReadableButtonLabel(root, "ProfileSaveNicknameButton", 17f);
            }
            else if (panel.PanelType == HomePageScenePanelType.Recharge)
            {
                for (int index = 0; index < 8; index++)
                {
                    RectTransform card = FindSceneComponent<RectTransform>(root, "RechargeProductCard" + index);
                    if (card == null) continue;
                    bool featured = card.rect.width > 320f;
                    RectTransform icon = FindSceneComponent<RectTransform>(card, "ProductCrystal");
                    if (icon != null)
                    {
                        icon.anchorMin = icon.anchorMax = new Vector2(.5f, 1f);
                        icon.pivot = new Vector2(.5f, 1f);
                        icon.anchoredPosition = new Vector2(0f, featured ? -42f : -24f);
                        icon.sizeDelta = featured ? new Vector2(360f, 356f) : new Vector2(140f, 112f);
                        Image image = icon.GetComponent<Image>();
                        if (image != null) image.preserveAspect = true;
                    }
                    PlaceReadableRect(card, "RewardAmount", 12f, featured ? 0f : 135f, card.rect.width - 24f, 36f);
                }
            }
            else if (panel.PanelType == HomePageScenePanelType.Equipment)
            {
                Transform slots = FindSceneTransform(root, "EquipmentSlotsCard");
                if (slots != null)
                {
                    string[] slotNames = { "EquipmentSlotWeapon", "EquipmentSlotArmor", "EquipmentSlotAccessory", "EquipmentSlotPet", "EquipmentSlotGloves" };
                    foreach (string slotName in slotNames)
                    {
                        Transform duplicate = slots.Find(slotName + " (1)");
                        if (duplicate != null) duplicate.gameObject.SetActive(false);
                        Transform slot = slots.Find(slotName);
                        if (slot == null) continue;
                        foreach (TMP_Text label in slot.GetComponentsInChildren<TMP_Text>(true))
                        {
                            if (label.name.StartsWith("EquipmentSlotName")) label.gameObject.SetActive(false);
                            if (!label.name.StartsWith("EquipmentSlotState")) continue;
                            RectTransform rectangle = label.rectTransform;
                            rectangle.anchorMin = new Vector2(0f, 0f);
                            rectangle.anchorMax = new Vector2(1f, 0f);
                            rectangle.pivot = new Vector2(.5f, 0f);
                            rectangle.anchoredPosition = new Vector2(0f, 3f);
                            rectangle.sizeDelta = new Vector2(-8f, 24f);
                            label.fontSize = 14f;
                            label.enableAutoSizing = false;
                            label.enableWordWrapping = false;
                            label.alignment = TextAlignmentOptions.Center;
                            label.color = new Color(.34f, .21f, .12f, 1f);
                        }
                    }
                }
                PlaceReadableRect(root, "EquipmentModeText", 190f, 170f, 330f, 30f);
                PlaceReadableRect(root, "EquipmentDetailDescription", 10f, 0f, 424f, 84f);
            }
            else if (panel.PanelType == HomePageScenePanelType.Task)
            {
                RectTransform footer = FindSceneComponent<RectTransform>(root, "TaskFooter");
                if (footer != null)
                {
                    footer.anchorMin = footer.anchorMax = new Vector2(.5f, 0f);
                    footer.pivot = new Vector2(.5f, 0f);
                    footer.anchoredPosition = new Vector2(140f, 46f);
                    footer.sizeDelta = new Vector2(980f, 38f);
                    RectTransform text = FindSceneComponent<RectTransform>(footer, "TaskFooterText");
                    if (text != null) { text.anchorMin = Vector2.zero; text.anchorMax = Vector2.one; text.offsetMin = Vector2.zero; text.offsetMax = Vector2.zero; }
                }
            }
            else if (panel.PanelType == HomePageScenePanelType.NewbieSign)
            {
                Transform activities = FindSceneTransform(root, "ActivityPanel");
                Transform details = activities == null ? null : FindSceneTransform(activities, "NewbieSignPanel");
                if (details != null)
                {
                    PlaceReadableRect(details, "ActivityName", 30f, 0f, 960f, 54f);
                    PlaceReadableRect(details, "Data", 30f, 60f, 960f, 36f);
                    PlaceReadableRect(details, "Explain", 30f, 100f, 960f, 90f);
                }
                PositionActivityArrow(root, "ActivityRewardPreviousButton", -58f);
                PositionActivityArrow(root, "ActivityRewardNextButton", 58f);
            }
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true)) ConfigureReadableText(label, label.name);
            ConfigurePanelPresentation(panel);
        }
    }

    private static void PositionActivityArrow(Transform root, string name, float horizontal)
    {
        RectTransform arrow = FindSceneComponent<RectTransform>(root, name);
        if (arrow == null) return;
        arrow.anchorMin = arrow.anchorMax = new Vector2(.5f, 0f);
        arrow.pivot = new Vector2(.5f, 1f);
        arrow.anchoredPosition = new Vector2(horizontal, -8f);
        arrow.sizeDelta = new Vector2(100f, 44f);
        Button button = arrow.GetComponent<Button>();
        SetSceneButtonText(button, horizontal < 0f ? "上一页" : "下一页");
        ApplyPresentationButton(button, false, 21f);
        foreach (TMP_Text label in arrow.GetComponentsInChildren<TMP_Text>(true))
        {
            RectTransform text = label.rectTransform;
            text.anchorMin = Vector2.zero;
            text.anchorMax = Vector2.one;
            text.offsetMin = new Vector2(3f, 3f);
            text.offsetMax = new Vector2(-3f, -3f);
            label.alignment = TextAlignmentOptions.Center;
        }
    }

    private static void PlaceReadableRect(Transform root, string name, float left, float top, float width, float height)
    {
        RectTransform rectangle = FindSceneComponent<RectTransform>(root, name + "ReadabilityViewport")
            ?? FindSceneComponent<RectTransform>(root, name);
        if (rectangle == null) return;
        rectangle.anchorMin = rectangle.anchorMax = new Vector2(0f, 1f);
        rectangle.pivot = new Vector2(0f, 1f);
        rectangle.anchoredPosition = new Vector2(left, -top);
        rectangle.sizeDelta = new Vector2(width, height);
    }

    private static void ResizeReadableRect(Transform root, string name, float width, float height)
    {
        RectTransform rectangle = FindSceneComponent<RectTransform>(root, name);
        if (rectangle != null) rectangle.sizeDelta = new Vector2(width, height);
    }

    private static void ConfigureReadableButtonLabel(Transform root, string name, float fontSize)
    {
        Button button = FindSceneComponent<Button>(root, name);
        TMP_Text label = button == null ? null : button.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;
        label.enableAutoSizing = false;
        label.fontSize = fontSize;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static void ConfigureReadableText(TMP_Text label, string objectName)
    {
        if (label == null)
        {
            return;
        }

        if (objectName.StartsWith("EquipmentRowName"))
        {
            label.enableWordWrapping = true;
            label.enableAutoSizing = false;
            label.fontSize = 16f;
            label.color = new Color(.30f, .21f, .16f, 1f);
            label.alignment = TextAlignmentOptions.Top;
            label.maxVisibleLines = 3;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return;
        }
        if (objectName.StartsWith("FriendRowName") || objectName.StartsWith("FriendRowId")
            || objectName.StartsWith("FriendRowMeta") || objectName.StartsWith("FriendRowState"))
        {
            label.richText = false;
            label.enableWordWrapping = false;
            label.enableAutoSizing = false;
            label.fontSize = objectName.StartsWith("FriendRowId") ? 15f : 18f;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                objectName.StartsWith("FriendRowName") ? 28f : 24f);
            return;
        }

        switch (objectName)
        {
            case "MailItemTitle":
                RectTransform mailRow = label.transform.parent as RectTransform;
                PlaceReadableRect(label.transform.parent, objectName, 26f, 8f,
                    mailRow == null ? 500f : Mathf.Max(100f, mailRow.rect.width - 70f), 36f);
                label.enableWordWrapping = false;
                label.enableAutoSizing = false;
                label.fontSize = 24f;
                label.color = new Color(.30f, .21f, .16f, 1f);
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
            case "MailItemMeta":
                RectTransform mailMetaRow = label.transform.parent as RectTransform;
                PlaceReadableRect(label.transform.parent, objectName, 28f, 44f,
                    mailMetaRow == null ? 500f : Mathf.Max(100f, mailMetaRow.rect.width - 70f), 28f);
                label.enableWordWrapping = false;
                label.enableAutoSizing = false;
                label.fontSize = 18f;
                label.color = new Color(.44f, .35f, .25f, 1f);
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
            case "MailRewardText":
                label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 30f);
                label.enableWordWrapping = false;
                label.enableAutoSizing = false;
                label.fontSize = 18f;
                label.color = new Color(.30f, .21f, .16f, 1f);
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
            case "MailDetailTitle":
                label.enableWordWrapping = false;
                label.enableAutoSizing = true;
                label.fontSizeMin = 16f;
                label.fontSizeMax = 30f;
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
            case "BookAvatarName":
                PlaceReadableRect(label.transform.parent, objectName, 116f, 26f, 138f, 28f);
                label.richText = false;
                label.enableWordWrapping = false;
                label.enableAutoSizing = false;
                label.fontSize = 14f;
                label.margin = Vector4.zero;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
            case "BookAvatarLevel":
                label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 20f);
                label.enableWordWrapping = false;
                label.enableAutoSizing = false;
                label.fontSize = 13f;
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
            case "BookAvatarExperienceText":
                label.gameObject.SetActive(false);
                break;
            case "MailDetailBody":
                EnsureReadableBodyScroll(label, 20f);
                break;
            case "TaskItemTitle":
                label.enableWordWrapping = false;
                label.enableAutoSizing = true;
                label.fontSizeMin = 22f;
                label.fontSizeMax = 24f;
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
            case "TaskItemDescription":
                label.enableWordWrapping = false;
                label.enableAutoSizing = true;
                label.fontSizeMin = 16f;
                label.fontSizeMax = 18f;
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
            case "TaskRewardText":
                label.enableWordWrapping = false;
                label.enableAutoSizing = true;
                label.fontSizeMin = 18f;
                label.fontSizeMax = 20f;
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
            case "ShopProductName":
                label.enableWordWrapping = false;
                label.enableAutoSizing = true;
                label.fontSizeMin = 12f;
                label.fontSizeMax = 18f;
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
            case "ShopProductReward":
            case "ShopProductPrice":
                label.enableWordWrapping = false;
                label.enableAutoSizing = true;
                label.fontSizeMin = 11f;
                label.fontSizeMax = 16f;
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
            case "ShopDetailName":
            case "EquipmentDetailName":
                label.enableWordWrapping = false;
                label.enableAutoSizing = true;
                label.fontSizeMin = 16f;
                label.fontSizeMax = 24f;
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
            case "ShopDetailDescription":
            case "EquipmentDetailDescription":
                EnsureReadableBodyScroll(label, 16f);
                break;
            case "ProfileNicknameLabel":
                label.enableWordWrapping = false;
                label.richText = false;
                label.enableAutoSizing = true;
                label.fontSizeMin = 16f;
                label.fontSizeMax = 25f;
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
            case "ProfileIdentifier":
            case "ProfileAccount":
            case "ProfileLevel":
            case "ProfileVital":
                label.enableWordWrapping = false;
                label.enableAutoSizing = true;
                label.fontSizeMin = 16f;
                label.fontSizeMax = 25f;
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
            case "ActivityName":
                bool activityTab = label.transform.parent != null
                    && label.transform.parent.GetComponent<Button>() != null;
                label.enableWordWrapping = false;
                label.enableAutoSizing = false;
                label.fontSize = activityTab ? 20f : 36f;
                label.color = new Color(.30f, .21f, .16f, 1f);
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
            case "ProfileSignStat":
            case "ProfileFriendStat":
                label.enableWordWrapping = true;
                label.enableAutoSizing = true;
                label.fontSizeMin = 16f;
                label.fontSizeMax = 22f;
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
            case "RequestName":
            case "RequestMeta":
                if (objectName == "RequestMeta") label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 24f);
                label.enableWordWrapping = false;
                label.enableAutoSizing = true;
                label.fontSizeMin = 13f;
                label.fontSizeMax = objectName == "RequestName" ? 21f : 17f;
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
            case "FriendDetailName":
                label.richText = false;
                label.enableWordWrapping = false;
                label.enableAutoSizing = true;
                label.fontSizeMin = 13f;
                label.fontSizeMax = 22f;
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
            case "RequestStatus":
                label.enableWordWrapping = true;
                label.enableAutoSizing = true;
                label.fontSizeMin = 13f;
                label.fontSizeMax = 17f;
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
            case "DayText":
            case "RewardText":
            case "RewardAmount":
                label.color = new Color(.34f, .21f, .12f, 1f);
                label.enableWordWrapping = objectName == "RewardText";
                label.enableAutoSizing = true;
                label.fontSizeMin = objectName == "RewardText" ? 14f : 18f;
                label.fontSizeMax = objectName == "RewardText" ? 18f : 24f;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.alignment = TextAlignmentOptions.Center;
                break;
            case "ProfileExperienceText":
            case "ProfileAccountStatusText":
            case "ProfileCancellationDetailsText":
            case "ProfileComplianceText":
                EnsureReadableBodyScroll(label, 18f);
                break;
            case "Explain":
                label.color = new Color(.44f, .35f, .25f, 1f);
                EnsureReadableBodyScroll(label, 22f);
                break;
            case "Data":
                label.enableWordWrapping = false;
                label.enableAutoSizing = false;
                label.fontSize = 22f;
                label.color = new Color(.44f, .35f, .25f, 1f);
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
            case "ShopDetailReward":
            case "ShopDetailPrice":
            case "ShopPurchaseSummary":
            case "ShopOwnedCount":
            case "ShopStorageModeText":
            case "TaskFooterText":
            case "EquipmentDetailType":
            case "EquipmentDetailStatus":
            case "EquipmentModeText":
                if (objectName == "EquipmentModeText") label.color = new Color(.34f, .21f, .12f, 1f);
                label.enableWordWrapping = false;
                label.enableAutoSizing = true;
                label.fontSizeMin = 12f;
                label.fontSizeMax = 18f;
                label.overflowMode = TextOverflowModes.Ellipsis;
                break;
        }
    }

    private static void EnsureReadableBodyScroll(TMP_Text label, float fontSize)
    {
        if (label == null || label.transform.parent == null)
        {
            return;
        }

        string viewportName = label.name + "ReadabilityViewport";
        RectTransform labelRect = label.rectTransform;
        Transform parent = label.transform.parent;
        RectTransform viewport = parent.name == viewportName ? parent as RectTransform : null;
        if (viewport == null)
        {
            GameObject viewportObject = new GameObject(viewportName, typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewportObject.layer = label.gameObject.layer;
            viewport = viewportObject.GetComponent<RectTransform>();
            viewport.SetParent(parent, false);
            viewport.anchorMin = labelRect.anchorMin;
            viewport.anchorMax = labelRect.anchorMax;
            viewport.pivot = labelRect.pivot;
            viewport.anchoredPosition = labelRect.anchoredPosition;
            viewport.sizeDelta = labelRect.sizeDelta;
            viewport.localScale = labelRect.localScale;
            viewport.localRotation = labelRect.localRotation;
            viewport.SetSiblingIndex(labelRect.GetSiblingIndex());
            Image surface = viewport.GetComponent<Image>();
            surface.color = Color.clear;
            surface.raycastTarget = true;
        }

        if (label.transform.parent != viewport)
        {
            label.transform.SetParent(viewport, false);
        }

        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0f, 1f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(-8f, 0f);
        labelRect.localScale = Vector3.one;
        labelRect.localRotation = Quaternion.identity;
        label.enableAutoSizing = false;
        label.fontSize = fontSize;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Overflow;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.raycastTarget = false;

        ContentSizeFitter fitter = label.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = label.gameObject.AddComponent<ContentSizeFitter>();
        }
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = viewport.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = labelRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        scroll.StopMovement();
        label.ForceMeshUpdate();
        float preferredHeight = Mathf.Max(viewport.rect.height, label.preferredHeight + 2f);
        labelRect.sizeDelta = new Vector2(-8f, preferredHeight);
        LayoutRebuilder.ForceRebuildLayoutImmediate(labelRect);
        scroll.verticalNormalizedPosition = 1f;
    }

    private static string BuildCompactMailMeta(MailResponse mail)
    {
        if (mail == null)
        {
            return string.Empty;
        }

        string sender = string.IsNullOrEmpty(mail.senderName) ? "系统" : mail.senderName.Trim();
        string attachment = mail.expired ? "已过期" : !mail.hasAttachment
            ? "无附件"
            : mail.attachmentClaimed ? "附件已领" : "可领取";
        return attachment + "  ·  " + sender;
    }
}
