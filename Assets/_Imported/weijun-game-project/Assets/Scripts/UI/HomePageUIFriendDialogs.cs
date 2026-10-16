using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;

public partial class HomePageUI
{
    private bool resetFriendRequestScroll = true;
    private readonly Dictionary<RectTransform, Vector3> friendRequestButtonScenePositions = new Dictionary<RectTransform, Vector3>();

    private static List<Transform> GetFriendRequestRows(Transform root)
    {
        List<Transform> rows = new List<Transform>();
        Transform parent = FindSceneTransform(root, "FriendRequestRowP");
        if (parent != null)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name.StartsWith("FriendRequestRow", System.StringComparison.Ordinal)) rows.Add(child);
            }
        }
        if (rows.Count == 0)
        {
            for (int index = 0; index < 5; index++)
            {
                Transform row = FindSceneTransform(root, "FriendRequestRow" + index);
                if (row != null) rows.Add(row);
            }
        }
        return rows;
    }

    private List<Transform> EnsureFriendRequestRows(Transform root, int requiredCount)
    {
        List<Transform> rows = GetFriendRequestRows(root);
        if (rows.Count == 0) return rows;
        Transform template = rows.Find(row => row.name == "FriendRequestRow0") ?? rows[0];
        while (rows.Count < requiredCount)
        {
            Transform row = Instantiate(template.gameObject, template.parent, false).transform;
            row.name = "FriendRequestRow" + rows.Count;
            row.gameObject.SetActive(false);
            // Clones must start with the authored state, even when the template displays an outgoing request.
            Button templatePrimary = FindSceneComponent<Button>(template, "RequestPrimaryButton");
            Button primary = FindSceneComponent<Button>(row, "RequestPrimaryButton");
            if (templatePrimary != null && primary != null
                && friendRequestButtonScenePositions.TryGetValue(templatePrimary.transform as RectTransform, out Vector3 scenePosition))
            {
                primary.transform.localPosition = scenePosition;
            }
            foreach (Button button in row.GetComponentsInChildren<Button>(true))
            {
                button.onClick = new Button.ButtonClickedEvent();
            }
            Image templateAvatar = FindSceneComponent<Image>(template, "RequestAvatar");
            Image avatar = FindSceneComponent<Image>(row, "RequestAvatar");
            if (templateAvatar != null && avatar != null)
            {
                AvatarImageState state = GetAvatarImageState(templateAvatar);
                avatar.sprite = state.defaultSprite;
                avatar.color = state.defaultColor;
                avatar.preserveAspect = state.defaultPreserveAspect;
            }
            rows.Add(row);
        }
        return rows;
    }

    private void RefreshFriendRequestScroll(Transform root)
    {
        ScrollRect scroll = FindSceneComponent<ScrollRect>(root, "FriendRequestsViewport");
        if (scroll == null || scroll.content == null || scroll.viewport == null) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        float overflow = Mathf.Max(0f, scroll.content.rect.height - scroll.viewport.rect.height);
        scroll.horizontal = false;
        scroll.vertical = overflow > 0.5f;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        Vector2 position = scroll.content.anchoredPosition;
        float nextY = resetFriendRequestScroll || !scroll.vertical ? 0f : Mathf.Clamp(position.y, 0f, overflow);
        if (resetFriendRequestScroll || !Mathf.Approximately(position.y, nextY) || !scroll.vertical) scroll.StopMovement();
        scroll.content.anchoredPosition = new Vector2(position.x, nextY);
        resetFriendRequestScroll = false;
    }

    private void ScrollToFriendRequest(Transform root, long requestId)
    {
        ScrollRect scroll = FindSceneComponent<ScrollRect>(root, "FriendRequestsViewport");
        if (scroll == null || scroll.content == null || scroll.viewport == null) return;
        List<ServerFriendRequestResponse> requests = GetFilteredV37Requests();
        int index = requests.FindIndex(request => request.requestId == requestId);
        List<Transform> rows = GetFriendRequestRows(scroll.content);
        if (index < 0 || index >= rows.Count || !(rows[index] is RectTransform row)) return;
        float rowTop = scroll.content.InverseTransformPoint(row.TransformPoint(new Vector3(0f, row.rect.yMax, 0f))).y;
        float overflow = Mathf.Max(0f, scroll.content.rect.height - scroll.viewport.rect.height);
        scroll.StopMovement();
        scroll.content.anchoredPosition = new Vector2(scroll.content.anchoredPosition.x,
            Mathf.Clamp(scroll.content.rect.yMax - rowTop, 0f, overflow));
    }

    public static bool EnsureFriendRequestsEntry(Transform root)
    {
        if (root == null) return false;
        bool changed = false;
        Button entry = FindSceneComponent<Button>(root, "FriendsRequestsButton");
        if (entry == null)
        {
            entry = FindSceneComponent<Button>(root, "FriendsFilterOnline");
            if (entry != null)
            {
                entry.name = "FriendsRequestsButton";
                SetSceneButtonText(entry, "申请");
                changed = true;
            }
        }
        Transform oldOutgoing = FindSceneTransform(root, "FriendsFilterOutgoing");
        if (oldOutgoing != null && oldOutgoing.gameObject.activeSelf)
        {
            oldOutgoing.gameObject.SetActive(false);
            changed = true;
        }
        return changed;
    }

    public static bool EnsureFriendRequestsDialog(Transform root)
    {
        if (root == null || FindSceneTransform(root, "FriendRequestsOverlay") != null) return false;
        Image paperTemplate = FindSceneComponent<Image>(root, "FriendSearchPanel");
        Text textTemplate = FindSceneComponent<Text>(root, "FriendSearchResult");
        Button actionTemplate = FindSceneComponent<Button>(root, "FriendSearchActionButton");
        Button cancelTemplate = FindSceneComponent<Button>(root, "FriendSearchCloseButton");
        if (paperTemplate == null || textTemplate == null || actionTemplate == null || cancelTemplate == null) return false;

        Image overlay = CreateFriendDialogImage(null, root, "FriendRequestsOverlay", Vector2.zero, Vector2.zero);
        overlay.color = new Color(0f, 0f, 0f, 0.55f);
        overlay.rectTransform.anchorMin = Vector2.zero;
        overlay.rectTransform.anchorMax = Vector2.one;
        overlay.rectTransform.offsetMin = overlay.rectTransform.offsetMax = Vector2.zero;
        Image paper = CreateFriendDialogImage(paperTemplate, overlay.transform, "FriendRequestsPanel", Vector2.zero, new Vector2(1040f, 740f));
        CreateFriendDialogText(textTemplate, paper.transform, "FriendRequestsTitle", "申请", new Vector2(0f, 310f), new Vector2(540f, 46f), 28);
        CreateFriendDialogButton(cancelTemplate, paper.transform, "FriendRequestsCloseButton", "取消", new Vector2(430f, 310f), new Vector2(120f, 60f));
        CreateFriendDialogButton(actionTemplate, paper.transform, "FriendRequestsIncomingButton", "收到申请", new Vector2(-140f, 240f), new Vector2(230f, 58f));
        CreateFriendDialogButton(actionTemplate, paper.transform, "FriendRequestsOutgoingButton", "发出申请", new Vector2(140f, 240f), new Vector2(230f, 58f));
        CreateFriendDialogText(textTemplate, paper.transform, "FriendRequestsEmpty", "暂无申请", Vector2.zero, new Vector2(800f, 60f), 24);
        SetSceneObjectActive(paper.transform, "FriendRequestsEmpty", false);
        CreateFriendDialogText(textTemplate, paper.transform, "FriendRequestsStatus", "", new Vector2(0f, -273f), new Vector2(920f, 28f), 17);
        Image viewport = CreateFriendDialogImage(null, paper.transform, "FriendRequestsViewport", new Vector2(0f, -34f), new Vector2(944f, 460f));
        viewport.color = Color.clear;
        viewport.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        GameObject content = new GameObject("FriendRequestRowP", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.layer = viewport.gameObject.layer;
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport.rectTransform;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        Image rowTemplate = FindSceneComponent<Image>(root, "FriendRow0");
        Image avatarTemplate = FindSceneComponent<Image>(root, "FriendDetailAvatarBackground");
        Image row = CreateFriendDialogImage(rowTemplate, content.transform, "FriendRequestRow0", Vector2.zero, new Vector2(944f, 84f));
        row.raycastTarget = false;
        Image avatar = CreateFriendDialogImage(avatarTemplate, row.transform, "RequestAvatar", new Vector2(-424f, 0f), new Vector2(64f, 64f));
        avatar.raycastTarget = false;
        CreateFriendDialogText(textTemplate, row.transform, "RequestName", "玩家昵称", new Vector2(-260f, 17f), new Vector2(240f, 30f), 21);
        CreateFriendDialogText(textTemplate, row.transform, "RequestMeta", "等级 / 玩家ID", new Vector2(-260f, -18f), new Vector2(240f, 28f), 17);
        CreateFriendDialogText(textTemplate, row.transform, "RequestStatus", "申请状态 / 有效期", Vector2.zero, new Vector2(264f, 70f), 17);
        CreateFriendDialogButton(actionTemplate, row.transform, "RequestPrimaryButton", "同意", new Vector2(230f, 0f), new Vector2(140f, 58f));
        CreateFriendDialogButton(cancelTemplate, row.transform, "RequestSecondaryButton", "拒绝", new Vector2(390f, 0f), new Vector2(140f, 58f));
        overlay.gameObject.SetActive(false);
        return true;
    }

    private static Image CreateFriendDialogImage(Image template, Transform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        node.layer = parent.gameObject.layer;
        node.transform.SetParent(parent, false);
        Image image = node.GetComponent<Image>();
        CopyFriendDialogImage(image, template);
        SetFriendDialogRect(image.rectTransform, new Vector2(0.5f, 0.5f), position, size);
        return image;
    }

    private void RefreshFriendRequestsDialog(Transform root, bool locked)
    {
        bool visible = serverFriendPanelMode != ServerFriendPanelMode.Friends;
        SetSceneObjectActive(root, "FriendRequestsOverlay", visible);
        if (!visible) return;
        Transform overlay = FindSceneTransform(root, "FriendRequestsOverlay");
        if (overlay == null) return;
        overlay.SetAsLastSibling();
        SetSceneObjectActive(overlay, "FriendRequestsPanel", true);
        Button close = FindSceneComponent<Button>(overlay, "FriendRequestsCloseButton");
        BindSceneButton(close, () => CloseFriendRequestsDialog(root));
        if (close != null) close.interactable = !v37MutationRunning;
        Button incomingButton = FindSceneComponent<Button>(overlay, "FriendRequestsIncomingButton");
        Button outgoingButton = FindSceneComponent<Button>(overlay, "FriendRequestsOutgoingButton");
        bool incoming = serverFriendPanelMode == ServerFriendPanelMode.IncomingRequests;
        SetSceneText(overlay, "FriendRequestsTitle", "申请");
        ConfigureFriendRequestTab(incomingButton, incoming, locked);
        ConfigureFriendRequestTab(outgoingButton, !incoming, locked);
        BindSceneButton(incomingButton, () => ChangeV37FriendPanelMode(root, ServerFriendPanelMode.IncomingRequests));
        BindSceneButton(outgoingButton, () => ChangeV37FriendPanelMode(root, ServerFriendPanelMode.OutgoingRequests));
        List<ServerFriendRequestResponse> requests = GetFilteredV37Requests();
        List<Transform> rows = EnsureFriendRequestRows(overlay, requests.Count);
        SetSceneObjectActive(overlay, "FriendRequestsEmpty", requests.Count == 0);
        SetSceneText(overlay, "FriendRequestsEmpty", v37Loading ? "正在加载申请..." : v37LastLoadFailed ? "申请加载失败" : incoming ? "暂无收到的申请" : "暂无发出的申请");
        SetSceneText(overlay, "FriendRequestsStatus", v37StatusMessage);
        for (int index = 0; index < rows.Count; index++)
        {
            Transform row = rows[index];
            if (row == null) continue;
            Image avatar = FindSceneComponent<Image>(row, "RequestAvatar");
            Button primary = FindSceneComponent<Button>(row, "RequestPrimaryButton");
            Button secondary = FindSceneComponent<Button>(row, "RequestSecondaryButton");
            row.gameObject.SetActive(index < requests.Count);
            if (index >= requests.Count)
            {
                BindSceneButton(primary, null);
                BindSceneButton(secondary, null);
                if (avatar != null && avatarImageStates.TryGetValue(avatar, out AvatarImageState inactiveState))
                {
                    inactiveState.requestKey = string.Empty;
                }
                continue;
            }
            ServerFriendRequestResponse request = requests[index];
            int playerId = incoming ? request.requesterPlayerId : request.recipientPlayerId;
            string nickname = incoming ? request.requesterNickname : request.recipientNickname;
            SetSceneText(row, "RequestName", string.IsNullOrWhiteSpace(nickname) ? "玩家 " + playerId : nickname);
            SetSceneText(row, "RequestMeta", $"Lv.{(incoming ? request.requesterLevel : request.recipientLevel)}  ID {playerId}");
            SetSceneText(row, "RequestStatus", GetFriendRequestStatusLabel(request.status) + "\n有效期至\n" + FormatV37RequestTime(request.expireTime));
            if (avatar != null)
            {
                ApplyBundledDefaultAvatar(avatar, null, GetAvatarImageState(avatar));
                ApplyAvatarImage(avatar, null, playerId,
                    incoming ? request.requesterAvatarUrl : request.recipientAvatarUrl,
                    incoming ? request.requesterAvatarCode : request.recipientAvatarCode,
                    incoming ? request.requesterAvatarVersion : request.recipientAvatarVersion);
            }
            UpdateFriendRequestButtonPosition(primary, secondary, incoming);
            if (primary != null)
            {
                primary.gameObject.SetActive(incoming || IsPendingFriendRequest(request));
                primary.interactable = !locked && IsPendingFriendRequest(request) && (incoming ? request.canAccept : request.canCancel);
                SetSceneButtonText(primary, incoming ? "同意" : "取消申请");
                ConfigureFriendRequestAction(primary, incoming ? null : secondary);
            }
            if (secondary != null)
            {
                secondary.gameObject.SetActive(incoming);
                secondary.interactable = !locked && IsPendingFriendRequest(request) && request.canReject;
                ConfigureFriendRequestAction(secondary);
            }
            BindSceneButton(primary, () => StartCoroutine(MutateV37FriendRequest(root, request.requestId, incoming ? "accept" : "cancel")));
            BindSceneButton(secondary, () => StartCoroutine(MutateV37FriendRequest(root, request.requestId, "reject")));
        }
        RefreshFriendRequestScroll(overlay);
    }

    private static void ConfigureFriendRequestTab(Button button, bool selected, bool locked)
    {
        if (button == null) return;
        button.interactable = !locked && !selected;
        // Keep tab selection independent of keyboard focus and the request loading state.
        button.transition = Selectable.Transition.None;
        if (button.targetGraphic is Image image)
        {
            SpriteState sprites = button.spriteState;
            image.overrideSprite = locked && sprites.disabledSprite != null
                ? sprites.disabledSprite
                : selected ? sprites.selectedSprite : null;
        }
        if (button.targetGraphic != null)
        {
            Color tint = selected ? button.colors.selectedColor : button.colors.normalColor;
            if (locked) tint *= button.colors.disabledColor;
            button.targetGraphic.CrossFadeColor(tint, 0.08f, true, true);
        }
    }

    private static void ConfigureFriendRequestAction(Button button, Button alternateStyle = null)
    {
        if (button == null || !(button.targetGraphic is Image image)) return;
        // Use the scene's secondary style for cancellation without replacing the authored primary sprite.
        Sprite alternateSprite = alternateStyle != null && alternateStyle.targetGraphic is Image alternateImage
            ? alternateImage.sprite : null;
        Sprite disabledSprite = button.spriteState.disabledSprite;
        image.overrideSprite = !button.interactable && disabledSprite != null ? disabledSprite : alternateSprite;
    }

    private void UpdateFriendRequestButtonPosition(Button primary, Button secondary, bool incoming)
    {
        RectTransform rect = primary != null ? primary.transform as RectTransform : null;
        if (rect == null) return;
        if (incoming)
        {
            if (friendRequestButtonScenePositions.TryGetValue(rect, out Vector3 originalPosition))
            {
                rect.localPosition = originalPosition;
                friendRequestButtonScenePositions.Remove(rect);
            }
            return;
        }

        if (secondary == null || secondary.transform.parent != rect.parent) return;
        if (!friendRequestButtonScenePositions.TryGetValue(rect, out Vector3 scenePosition))
        {
            scenePosition = rect.localPosition;
            friendRequestButtonScenePositions.Add(rect, scenePosition);
        }
        // Center the single action within the two authored button positions.
        rect.localPosition = (scenePosition + secondary.transform.localPosition) * 0.5f;
    }

    private void CloseFriendRequestsDialog(Transform root)
    {
        if (v37MutationRunning) return;
        v37PanelRevision++;
        v37Loading = false;
        serverFriendPanelMode = ServerFriendPanelMode.Friends;
        RefreshServerFriendRequestPanel(root, "已关闭申请窗口");
    }
}
