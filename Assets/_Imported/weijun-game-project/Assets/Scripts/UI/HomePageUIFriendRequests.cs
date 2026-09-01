using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using Text = TMPro.TMP_Text;
using InputField = TMPro.TMP_InputField;

public partial class HomePageUI
{
    private enum ServerFriendPanelMode
    {
        Friends,
        IncomingRequests,
        OutgoingRequests
    }

    private ServerFriendPanelMode serverFriendPanelMode = ServerFriendPanelMode.Friends;
    private readonly List<ServerFriendResponse> v37Friends = new List<ServerFriendResponse>();
    private readonly List<ServerFriendRequestResponse> v37IncomingRequests = new List<ServerFriendRequestResponse>();
    private readonly List<ServerFriendRequestResponse> v37OutgoingRequests = new List<ServerFriendRequestResponse>();
    private ServerFriendRequestSummary v37FriendRequestSummary;
    private ServerFriendSearchResponse v37FriendSearchResult;
    private int v37FriendSearchPlayerId;
    private int v37SelectedFriendPlayerId;
    private long v37SelectedRequestId;
    private int v37PageIndex;
    private int v37PanelRevision;
    private bool v37Loading;
    private bool v37MutationRunning;
    private bool v37LastLoadFailed;
    private bool v37FriendRequestCreationBlocked;
    private string v37StatusMessage = string.Empty;
    private Coroutine v38PresenceRefreshCoroutine;

    private void ResetServerFriendRequestPanel()
    {
        v37PanelRevision++;
        serverFriendPanelMode = ServerFriendPanelMode.Friends;
        v37Friends.Clear();
        v37IncomingRequests.Clear();
        v37OutgoingRequests.Clear();
        v37FriendRequestSummary = null;
        v37FriendSearchResult = null;
        v37FriendSearchPlayerId = 0;
        v37SelectedFriendPlayerId = 0;
        v37SelectedRequestId = 0;
        v37PageIndex = 0;
        v37Loading = false;
        v37MutationRunning = false;
        v37LastLoadFailed = false;
        v37FriendRequestCreationBlocked = false;
        v37StatusMessage = string.Empty;
        friendsSearchQuery = string.Empty;
        friendDeleteConfirmation = false;
    }

    private void InvalidateServerFriendRequestPanel()
    {
        v37PanelRevision++;
        v37Loading = false;
        v37MutationRunning = false;
    }

    private IEnumerator RefreshServerFriendRequestState(Transform root, bool reloadFriends, string successStatus)
    {
        if (v37Loading || v37MutationRunning || root == null)
        {
            yield break;
        }

        int revision = v37PanelRevision;
        v37Loading = true;
        v37LastLoadFailed = false;
        RefreshServerFriendRequestPanel(root, "正在同步服务器好友数据...");

        yield return LoadServerFriendRequestSummary(root, revision);
        if (revision != v37PanelRevision)
        {
            yield break;
        }

        if (reloadFriends || serverFriendPanelMode == ServerFriendPanelMode.Friends)
        {
            yield return LoadV37Friends(root, revision);
        }
        else
        {
            yield return LoadV37FriendRequests(root, serverFriendPanelMode, revision);
        }

        if (revision != v37PanelRevision)
        {
            yield break;
        }

        v37Loading = false;
        RefreshServerFriendRequestPanel(root, v37LastLoadFailed ? v37StatusMessage : successStatus);
    }

    private IEnumerator LoadServerFriendRequestSummary(Transform root, int revision)
    {
        UnityWebRequest request = CreateAuthorizedGet("/friends/requests/summary");
        if (request == null)
        {
            SetV37LoadFailure(requestCreationError);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request) || revision != v37PanelRevision)
            {
                yield break;
            }

            ServerFriendRequestSummaryApiResponse response = ParseJson<ServerFriendRequestSummaryApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                UpdateV37FriendRequestAvailability(response);
                SetV37LoadFailure(GetV37RequestError(request, response, "好友申请摘要读取失败"));
                yield break;
            }

            v37FriendRequestSummary = response.data;
            v37FriendRequestCreationBlocked = !response.data.enabled;
        }
    }

    private IEnumerator LoadV37Friends(Transform root, int revision)
    {
        List<ServerFriendResponse> loaded = new List<ServerFriendResponse>();
        int cursorLevel = 0;
        int cursorPlayerId = 0;
        bool hasCursor = false;
        bool hasMore = true;
        int pageGuard = 0;

        while (hasMore && pageGuard++ < 50)
        {
            string path = hasCursor
                ? $"/friends?cursorLevel={cursorLevel}&cursorPlayerId={cursorPlayerId}&limit=100"
                : "/friends?limit=100";
            UnityWebRequest request = CreateAuthorizedGet(path);
            if (request == null)
            {
                SetV37LoadFailure(requestCreationError);
                yield break;
            }

            using (request)
            {
                yield return request.SendWebRequest();
                if (HandleUnauthorizedResponse(request) || revision != v37PanelRevision)
                {
                    yield break;
                }

                V37FriendListApiResponse response = ParseJson<V37FriendListApiResponse>(request.downloadHandler.text);
                if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
                {
                    SetV37LoadFailure(GetV37RequestError(request, response, "正式好友列表读取失败"));
                    yield break;
                }

                if (response.data.items != null)
                {
                    for (int i = 0; i < response.data.items.Length; i++)
                    {
                        if (response.data.items[i] != null)
                        {
                            loaded.Add(response.data.items[i]);
                        }
                    }
                }

                if (response.data.dailyGift != null)
                {
                    serverDailyGiftStatus = response.data.dailyGift;
                }

                hasMore = response.data.hasMore && response.data.nextCursor != null;
                if (hasMore)
                {
                    int nextLevel = response.data.nextCursor.level;
                    int nextPlayerId = response.data.nextCursor.playerId;
                    if (hasCursor && nextLevel == cursorLevel && nextPlayerId == cursorPlayerId)
                    {
                        hasMore = false;
                    }
                    else
                    {
                        cursorLevel = nextLevel;
                        cursorPlayerId = nextPlayerId;
                        hasCursor = true;
                    }
                }
            }
        }

        PreserveNewerV38Presence(loaded);
        v37Friends.Clear();
        v37Friends.AddRange(loaded);
        yield return RefreshV39FriendAvatarSnapshots(revision);
    }

    private IEnumerator LoadV37FriendRequests(Transform root, ServerFriendPanelMode mode, int revision)
    {
        bool incoming = mode == ServerFriendPanelMode.IncomingRequests;
        List<ServerFriendRequestResponse> loaded = new List<ServerFriendRequestResponse>();
        long beforeId = 0;
        bool hasMore = true;
        int pageGuard = 0;

        while (hasMore && pageGuard++ < 50)
        {
            string path = incoming
                ? "/friends/requests/incoming?status=PENDING&limit=100"
                : "/friends/requests/outgoing?status=PENDING&limit=100";
            if (beforeId > 0)
            {
                path += "&beforeId=" + beforeId;
            }

            UnityWebRequest request = CreateAuthorizedGet(path);
            if (request == null)
            {
                SetV37LoadFailure(requestCreationError);
                yield break;
            }

            using (request)
            {
                yield return request.SendWebRequest();
                if (HandleUnauthorizedResponse(request) || revision != v37PanelRevision)
                {
                    yield break;
                }

                ServerFriendRequestListApiResponse response = ParseJson<ServerFriendRequestListApiResponse>(request.downloadHandler.text);
                if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
                {
                    SetV37LoadFailure(GetV37RequestError(request, response, "好友申请列表读取失败"));
                    yield break;
                }

                if (response.data.items != null)
                {
                    for (int i = 0; i < response.data.items.Length; i++)
                    {
                        if (response.data.items[i] != null)
                        {
                            loaded.Add(response.data.items[i]);
                        }
                    }
                }

                hasMore = response.data.hasMore && response.data.nextCursor > 0;
                if (hasMore)
                {
                    if (response.data.nextCursor == beforeId)
                    {
                        hasMore = false;
                    }
                    else
                    {
                        beforeId = response.data.nextCursor;
                    }
                }
            }
        }

        List<ServerFriendRequestResponse> target = incoming ? v37IncomingRequests : v37OutgoingRequests;
        target.Clear();
        target.AddRange(loaded);
    }

    private void RefreshServerFriendRequestPanel(Transform root, string status)
    {
        if (root == null)
        {
            return;
        }

        EnsureServerFriendRequestControls(root);
        if (!string.IsNullOrEmpty(status))
        {
            v37StatusMessage = status;
        }

        bool interactionLocked = v37Loading || v37MutationRunning;
        SetSceneText(root, "FriendsTitle", "游戏好友");
        SetSceneText(root, "FriendsSubtitle", GetV37ModeLabel());
        SetSceneText(root, "FriendsCount", BuildV37FriendCountText());
        SetSceneText(root, "FriendsStatus", v37StatusMessage);

        ConfigureV37TopControls(root, interactionLocked);
        if (serverFriendPanelMode == ServerFriendPanelMode.Friends)
        {
            RenderV37FriendRows(root, interactionLocked);
            RefreshV37FriendDetail(root, interactionLocked);
        }
        else
        {
            RenderV37RequestRows(root, interactionLocked);
            RefreshV37RequestDetail(root, interactionLocked);
        }
    }

    private void ConfigureV37TopControls(Transform root, bool interactionLocked)
    {
        InputField filterInput = FindSceneComponent<InputField>(root, "FriendsSearchInput");
        if (filterInput != null)
        {
            filterInput.SetTextWithoutNotify(friendsSearchQuery);
            filterInput.onValueChanged.RemoveAllListeners();
            filterInput.onValueChanged.AddListener(value =>
            {
                friendsSearchQuery = value.Trim();
                v37PageIndex = 0;
                v37SelectedFriendPlayerId = 0;
                v37SelectedRequestId = 0;
                RefreshServerFriendRequestPanel(root, null);
            });
            if (filterInput.placeholder is Text placeholder)
            {
                placeholder.text = "筛选当前列表";
            }
            filterInput.interactable = !interactionLocked;
        }

        InputField playerIdInput = FindSceneComponent<InputField>(root, "FriendsAddInput");
        if (playerIdInput != null)
        {
            playerIdInput.characterLimit = 10;
            playerIdInput.contentType = InputField.ContentType.IntegerNumber;
            playerIdInput.onValueChanged.RemoveAllListeners();
            playerIdInput.onValueChanged.AddListener(value =>
            {
                int.TryParse(value.Trim(), out int currentPlayerId);
                if (currentPlayerId != v37FriendSearchPlayerId)
                {
                    v37FriendSearchResult = null;
                    v37FriendSearchPlayerId = 0;
                    v37SelectedFriendPlayerId = 0;
                }
                RefreshServerFriendRequestPanel(root, null);
            });
            if (playerIdInput.placeholder is Text placeholder)
            {
                placeholder.text = "输入玩家 ID";
            }
            playerIdInput.interactable = !interactionLocked;
        }

        Button friendTab = FindSceneComponent<Button>(root, "FriendsFilterAll");
        Button incomingTab = FindSceneComponent<Button>(root, "FriendsFilterOnline");
        Button outgoingTab = FindSceneComponent<Button>(root, "FriendsFilterOutgoing");
        ConfigureFriendFilterButton(friendTab, serverFriendPanelMode == ServerFriendPanelMode.Friends);
        ConfigureFriendFilterButton(incomingTab, serverFriendPanelMode == ServerFriendPanelMode.IncomingRequests);
        ConfigureFriendFilterButton(outgoingTab, serverFriendPanelMode == ServerFriendPanelMode.OutgoingRequests);
        SetSceneButtonText(friendTab, "好友");
        SetSceneButtonText(incomingTab, BuildV37IncomingTabLabel());
        SetSceneButtonText(outgoingTab, BuildV37OutgoingTabLabel());
        if (friendTab != null) friendTab.interactable = !interactionLocked;
        if (incomingTab != null) incomingTab.interactable = !interactionLocked;
        if (outgoingTab != null) outgoingTab.interactable = !interactionLocked;
        BindSceneButton(friendTab, () => ChangeV37FriendPanelMode(root, ServerFriendPanelMode.Friends));
        BindSceneButton(incomingTab, () => ChangeV37FriendPanelMode(root, ServerFriendPanelMode.IncomingRequests));
        BindSceneButton(outgoingTab, () => ChangeV37FriendPanelMode(root, ServerFriendPanelMode.OutgoingRequests));
        BindSceneButton(FindSceneComponent<Button>(root, "FriendsClearSearchButton"), () =>
        {
            friendsSearchQuery = string.Empty;
            v37PageIndex = 0;
            RefreshServerFriendRequestPanel(root, "筛选条件已清除");
        });
        BindSceneButton(FindSceneComponent<Button>(root, "FriendsAddButton"), () => HandleV37SearchOrRequest(root));
        BindSceneButton(FindSceneComponent<Button>(root, "FriendsCloseButton"), HideFriendsPanel);
        SetSceneObjectActive(root, "FriendsWeChatGiftButton", false);

        Button addButton = FindSceneComponent<Button>(root, "FriendsAddButton");
        ConfigureV37SearchButton(addButton, interactionLocked);

        Button previousButton = FindSceneComponent<Button>(root, "FriendsPreviousButton");
        Button nextButton = FindSceneComponent<Button>(root, "FriendsNextButton");
        int pageCount = GetV37PageCount();
        if (previousButton != null)
        {
            previousButton.interactable = !interactionLocked && v37PageIndex > 0;
        }
        if (nextButton != null)
        {
            nextButton.interactable = !interactionLocked && v37PageIndex < pageCount - 1;
        }
        SetSceneText(root, "FriendsPageText", $"{v37PageIndex + 1} / {pageCount}");
        BindSceneButton(previousButton, () => ChangeV37FriendPage(root, -1));
        BindSceneButton(nextButton, () => ChangeV37FriendPage(root, 1));
    }

    private void ConfigureV37SearchButton(Button button, bool interactionLocked)
    {
        if (button == null)
        {
            return;
        }

        string label = "搜索玩家";
        bool enabled = !interactionLocked;
        if (v37FriendSearchResult != null)
        {
            switch (v37FriendSearchResult.relationshipStatus)
            {
                case "NONE":
                    label = IsV37FriendRequestCreationAvailable() ? "发送申请" : "申请未开放";
                    enabled &= IsV37FriendRequestCreationAvailable();
                    break;
                case "OUTGOING_PENDING":
                    label = "查看发出申请";
                    break;
                case "INCOMING_PENDING":
                    label = "查看收到申请";
                    break;
                case "FRIEND":
                    label = "已是好友";
                    break;
                case "SELF":
                    label = "当前玩家";
                    enabled = false;
                    break;
            }
        }

        button.interactable = enabled;
        SetSceneButtonText(button, label);
    }

    private static void ConfigureFriendFilterButton(Button button, bool selected)
    {
        if (button == null)
        {
            return;
        }

        Image background = button.GetComponent<Image>();
        if (background != null)
        {
            background.color = selected
                ? new Color(0.20f, 0.56f, 0.66f, 1f)
                : new Color(0.90f, 0.79f, 0.59f, 1f);
        }

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.color = selected ? Color.white : new Color(0.36f, 0.20f, 0.10f, 1f);
        }
    }

    private static Color GetFriendAvatarColor(string id)
    {
        Color[] colors =
        {
            new Color(0.28f, 0.62f, 0.76f, 1f),
            new Color(0.88f, 0.48f, 0.30f, 1f),
            new Color(0.38f, 0.66f, 0.36f, 1f),
            new Color(0.64f, 0.42f, 0.72f, 1f),
            new Color(0.86f, 0.66f, 0.22f, 1f)
        };
        int hash = 0;
        if (!string.IsNullOrEmpty(id))
        {
            for (int index = 0; index < id.Length; index++)
            {
                hash = (hash * 31 + id[index]) & int.MaxValue;
            }
        }

        return colors[hash % colors.Length];
    }

    private void RenderV37FriendRows(Transform root, bool interactionLocked)
    {
        List<ServerFriendResponse> visibleFriends = GetFilteredV37Friends();
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(visibleFriends.Count / (float)FriendsPerPage));
        v37PageIndex = Mathf.Clamp(v37PageIndex, 0, pageCount - 1);
        int firstIndex = v37PageIndex * FriendsPerPage;
        int rowCount = Mathf.Min(FriendsPerPage, Mathf.Max(0, visibleFriends.Count - firstIndex));
        if (v37SelectedFriendPlayerId == 0 && rowCount > 0 && v37FriendSearchResult == null)
        {
            v37SelectedFriendPlayerId = visibleFriends[firstIndex].playerId;
        }

        for (int rowIndex = 0; rowIndex < FriendsPerPage; rowIndex++)
        {
            Transform row = FindSceneTransform(root, "FriendRow" + rowIndex);
            if (row == null)
            {
                continue;
            }

            bool visible = rowIndex < rowCount;
            row.gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            ServerFriendResponse friend = visibleFriends[firstIndex + rowIndex];
            bool selected = v37FriendSearchResult == null && friend.playerId == v37SelectedFriendPlayerId;
            RenderV37Row(
                row,
                rowIndex,
                selected,
                friend.playerId,
                friend.nickname,
                friend.avatarCode,
                friend.avatarVersion,
                BuildV38FriendRowMetadata(friend),
                GetV38FriendPresenceLabel(friend),
                GetV38FriendPresenceColor(friend),
                interactionLocked,
                () =>
                {
                    v37FriendSearchResult = null;
                    v37SelectedFriendPlayerId = friend.playerId;
                    friendDeleteConfirmation = false;
                    RefreshServerFriendRequestPanel(root, "已选择好友");
                });
        }
    }

    private void RenderV37RequestRows(Transform root, bool interactionLocked)
    {
        List<ServerFriendRequestResponse> requests = GetFilteredV37Requests();
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(requests.Count / (float)FriendsPerPage));
        v37PageIndex = Mathf.Clamp(v37PageIndex, 0, pageCount - 1);
        int firstIndex = v37PageIndex * FriendsPerPage;
        int rowCount = Mathf.Min(FriendsPerPage, Mathf.Max(0, requests.Count - firstIndex));
        if (v37SelectedRequestId == 0 && rowCount > 0)
        {
            v37SelectedRequestId = requests[firstIndex].requestId;
        }

        for (int rowIndex = 0; rowIndex < FriendsPerPage; rowIndex++)
        {
            Transform row = FindSceneTransform(root, "FriendRow" + rowIndex);
            if (row == null)
            {
                continue;
            }

            bool visible = rowIndex < rowCount;
            row.gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            ServerFriendRequestResponse request = requests[firstIndex + rowIndex];
            bool incoming = serverFriendPanelMode == ServerFriendPanelMode.IncomingRequests;
            int otherPlayerId = incoming ? request.requesterPlayerId : request.recipientPlayerId;
            string otherName = incoming ? request.requesterNickname : request.recipientNickname;
            int otherLevel = incoming ? request.requesterLevel : request.recipientLevel;
            string otherAvatarCode = incoming ? request.requesterAvatarCode : request.recipientAvatarCode;
            long otherAvatarVersion = incoming ? request.requesterAvatarVersion : request.recipientAvatarVersion;
            RenderV37Row(
                row,
                rowIndex,
                request.requestId == v37SelectedRequestId,
                otherPlayerId,
                otherName,
                otherAvatarCode,
                otherAvatarVersion,
                $"Lv.{Mathf.Max(1, otherLevel)}    {FormatV37RequestTime(request.createTime)}",
                incoming ? "等待你处理" : "等待对方同意",
                incoming ? new Color(0.91f, 0.46f, 0.16f, 1f) : new Color(0.26f, 0.52f, 0.82f, 1f),
                interactionLocked,
                () =>
                {
                    v37SelectedRequestId = request.requestId;
                    friendDeleteConfirmation = false;
                    RefreshServerFriendRequestPanel(root, "已选择好友申请");
                });
        }
    }

    private void RenderV37Row(
        Transform row,
        int rowIndex,
        bool selected,
        int playerId,
        string nickname,
        string avatarCode,
        long avatarVersion,
        string metadata,
        string state,
        Color stateColor,
        bool interactionLocked,
        Action onSelected)
    {
        Image background = row.GetComponent<Image>();
        if (background != null)
        {
            background.color = selected
                ? new Color(1f, 0.80f, 0.42f, 1f)
                : new Color(1f, 0.95f, 0.78f, 1f);
        }

        Image avatar = FindSceneComponent<Image>(row, "FriendRowAvatarBackground" + rowIndex);
        if (avatar != null)
        {
            avatar.color = GetFriendAvatarColor(playerId.ToString());
            Text avatarInitial = FindSceneComponent<Text>(row, "FriendRowAvatar" + rowIndex);
            ApplyAvatarImage(avatar, avatarInitial, playerId, avatarCode, avatarVersion);
        }

        Image mark = FindSceneComponent<Image>(row, "FriendRowOnlineMark" + rowIndex);
        if (mark != null)
        {
            mark.color = stateColor;
        }

        SetSceneText(row, "FriendRowAvatar" + rowIndex, GetNicknameInitial(nickname));
        SetSceneText(row, "FriendRowName" + rowIndex, string.IsNullOrWhiteSpace(nickname) ? "玩家 " + playerId : nickname);
        SetSceneText(row, "FriendRowMeta" + rowIndex, metadata);
        SetSceneText(row, "FriendRowState" + rowIndex, state);
        Button rowButton = row.GetComponent<Button>();
        if (rowButton != null)
        {
            rowButton.interactable = !interactionLocked;
        }
        BindSceneButton(rowButton, onSelected);
    }

    private void RefreshV37FriendDetail(Transform root, bool interactionLocked)
    {
        ServerFriendResponse friend = FindV37Friend(v37SelectedFriendPlayerId);
        if (friend == null && v37FriendSearchResult != null)
        {
            RefreshV37SearchDetail(root, interactionLocked);
            return;
        }

        if (friend == null)
        {
            SetV37DetailEmpty(root, "暂无正式好友", "搜索玩家 ID 并发送申请后，等待对方同意");
            return;
        }

        SetV37DetailBase(
            root,
            friend.playerId,
            friend.nickname,
            friend.level,
            friend.avatarCode,
            friend.avatarVersion,
            GetV38FriendPresenceLabel(friend),
            BuildV38FriendPresenceDetail(friend),
            friend.dailyStaminaGiftReceived ? "今日已收到该好友体力" : "今日尚未收到该好友体力");
        SetV38FriendPresenceDetailColor(root, friend);

        bool giftedToday = friend.dailyStaminaGiftSent;
        bool giftEnabled = serverDailyGiftStatus != null && serverDailyGiftStatus.enabled && !giftedToday && !interactionLocked;
        string giftHint = BuildServerStaminaGiftHint(friend);
        SetSceneText(root, "FriendGiftHint", giftHint);
        SetSceneObjectActive(root, "FriendsGiftButton", true);
        SetSceneObjectActive(root, "FriendsDeleteButton", true);
        SetSceneObjectActive(root, "FriendsRequestPrimaryButton", false);
        SetSceneObjectActive(root, "FriendsRequestSecondaryButton", false);
        Button giftButton = FindSceneComponent<Button>(root, "FriendsGiftButton");
        Button deleteButton = FindSceneComponent<Button>(root, "FriendsDeleteButton");
        if (giftButton != null) giftButton.interactable = giftEnabled;
        if (deleteButton != null) deleteButton.interactable = !interactionLocked;
        SetSceneButtonText(giftButton, giftedToday ? "今日已赠体力" : "赠送体力");
        SetSceneButtonText(deleteButton, friendDeleteConfirmation ? "确认删除" : "删除好友");
        BindSceneButton(giftButton, () => StartCoroutine(SendV37StaminaGift(root, friend.playerId)));
        BindSceneButton(deleteButton, () => RequestV37FriendDeletion(root, friend));
    }

    private string BuildServerStaminaGiftHint(ServerFriendResponse friend)
    {
        if (serverDailyGiftStatus == null || !serverDailyGiftStatus.enabled)
        {
            return "好友体力赠送当前未开启";
        }

        if (friend != null && friend.dailyStaminaGiftSent)
        {
            return "今天已经向该好友赠送过体力";
        }

        return "今日可赠送 " + Mathf.Max(0, serverDailyGiftStatus.quantity) + " 点体力";
    }

    private void RefreshV37SearchDetail(Transform root, bool interactionLocked)
    {
        ServerFriendSearchResponse result = v37FriendSearchResult;
        if (result == null)
        {
            SetV37DetailEmpty(root, "搜索玩家", "请输入准确的玩家 ID，不支持昵称或全服模糊搜索");
            return;
        }

        SetV37DetailBase(
            root,
            result.playerId,
            result.nickname,
            result.level,
            result.avatarCode,
            result.avatarVersion,
            "搜索结果",
            "当前关系：" + GetV37RelationshipLabel(result.relationshipStatus),
            result.pendingRequestId > 0 ? "关联申请 #" + result.pendingRequestId : "可根据服务器关系状态继续操作");
        SetSceneObjectActive(root, "FriendsGiftButton", false);
        SetSceneObjectActive(root, "FriendsDeleteButton", false);

        Action primaryAction = null;
        string primaryLabel = string.Empty;
        switch (result.relationshipStatus)
        {
            case "NONE":
                primaryLabel = IsV37FriendRequestCreationAvailable() ? "发送申请" : "申请未开放";
                primaryAction = IsV37FriendRequestCreationAvailable()
                    ? (Action)(() => StartCoroutine(CreateV37FriendRequest(root, result.playerId)))
                    : null;
                break;
            case "INCOMING_PENDING":
                primaryLabel = "查看收到申请";
                primaryAction = () => ChangeV37FriendPanelMode(root, ServerFriendPanelMode.IncomingRequests);
                break;
            case "OUTGOING_PENDING":
                primaryLabel = "查看发出申请";
                primaryAction = () => ChangeV37FriendPanelMode(root, ServerFriendPanelMode.OutgoingRequests);
                break;
            case "FRIEND":
                primaryLabel = "查看好友";
                primaryAction = () =>
                {
                    v37FriendSearchResult = null;
                    v37SelectedFriendPlayerId = result.playerId;
                    RefreshServerFriendRequestPanel(root, "已定位正式好友");
                };
                break;
        }

        ConfigureV37RequestButtons(root, primaryLabel, primaryAction, string.Empty, null, interactionLocked || result.relationshipStatus == "SELF");
    }

    private void RefreshV37RequestDetail(Transform root, bool interactionLocked)
    {
        ServerFriendRequestResponse request = FindV37Request(v37SelectedRequestId);
        if (request == null)
        {
            string emptyTitle = serverFriendPanelMode == ServerFriendPanelMode.IncomingRequests ? "暂无收到的申请" : "暂无发出的申请";
            SetV37DetailEmpty(root, emptyTitle, "好友申请会在服务器按最新状态更新");
            return;
        }

        bool incoming = serverFriendPanelMode == ServerFriendPanelMode.IncomingRequests;
        int otherPlayerId = incoming ? request.requesterPlayerId : request.recipientPlayerId;
        string otherName = incoming ? request.requesterNickname : request.recipientNickname;
        int otherLevel = incoming ? request.requesterLevel : request.recipientLevel;
        string otherAvatarCode = incoming ? request.requesterAvatarCode : request.recipientAvatarCode;
        long otherAvatarVersion = incoming ? request.requesterAvatarVersion : request.recipientAvatarVersion;
        SetV37DetailBase(
            root,
            otherPlayerId,
            otherName,
            otherLevel,
            otherAvatarCode,
            otherAvatarVersion,
            incoming ? "收到的好友申请" : "发出的好友申请",
            "申请状态：" + request.status,
            "过期时间：" + FormatV37RequestTime(request.expireTime));
        SetSceneObjectActive(root, "FriendsGiftButton", false);
        SetSceneObjectActive(root, "FriendsDeleteButton", false);

        if (incoming)
        {
            ConfigureV37RequestButtons(
                root,
                "同意",
                request.canAccept ? (Action)(() => StartCoroutine(MutateV37FriendRequest(root, request.requestId, "accept"))) : null,
                "拒绝",
                request.canReject ? (Action)(() => StartCoroutine(MutateV37FriendRequest(root, request.requestId, "reject"))) : null,
                interactionLocked);
        }
        else
        {
            ConfigureV37RequestButtons(
                root,
                "取消申请",
                request.canCancel ? (Action)(() => StartCoroutine(MutateV37FriendRequest(root, request.requestId, "cancel"))) : null,
                string.Empty,
                null,
                interactionLocked);
        }
    }

    private void SetV37DetailBase(
        Transform root,
        int playerId,
        string nickname,
        int level,
        string avatarCode,
        long avatarVersion,
        string title,
        string detail,
        string hint)
    {
        SetSceneObjectActive(root, "FriendDetailEmpty", false);
        SetSceneObjectActive(root, "FriendDetailContent", true);
        Image avatar = FindSceneComponent<Image>(root, "FriendDetailAvatarBackground");
        if (avatar != null)
        {
            avatar.color = GetFriendAvatarColor(playerId.ToString());
            ApplyAvatarImage(
                avatar,
                FindSceneComponent<Text>(root, "FriendDetailAvatar"),
                playerId,
                avatarCode,
                avatarVersion);
        }

        SetSceneText(root, "FriendDetailAvatar", GetNicknameInitial(nickname));
        SetSceneText(root, "FriendDetailName", string.IsNullOrWhiteSpace(nickname) ? "玩家 " + playerId : nickname);
        SetSceneText(root, "FriendDetailId", "玩家编号  " + playerId);
        SetSceneText(root, "FriendDetailLevel", "领主等级  Lv." + Mathf.Max(1, level));
        SetSceneText(root, "FriendDetailOnline", title);
        SetSceneText(root, "FriendDetailIntimacy", detail);
        SetSceneText(root, "FriendGiftHint", hint);
    }

    private void SetV37DetailEmpty(Transform root, string title, string hint)
    {
        SetSceneObjectActive(root, "FriendDetailEmpty", true);
        SetSceneObjectActive(root, "FriendDetailContent", false);
        SetSceneText(root, "FriendDetailEmptyTitle", title);
        SetSceneText(root, "FriendDetailEmptyHint", hint);
    }

    private void ConfigureV37RequestButtons(
        Transform root,
        string primaryLabel,
        Action primaryAction,
        string secondaryLabel,
        Action secondaryAction,
        bool interactionLocked)
    {
        Button primary = FindSceneComponent<Button>(root, "FriendsRequestPrimaryButton");
        Button secondary = FindSceneComponent<Button>(root, "FriendsRequestSecondaryButton");
        bool showPrimary = !string.IsNullOrEmpty(primaryLabel);
        bool showSecondary = !string.IsNullOrEmpty(secondaryLabel);
        SetSceneObjectActive(root, "FriendsRequestPrimaryButton", showPrimary);
        SetSceneObjectActive(root, "FriendsRequestSecondaryButton", showSecondary);
        if (primary != null)
        {
            primary.interactable = !interactionLocked && primaryAction != null;
            SetSceneButtonText(primary, primaryLabel);
        }
        if (secondary != null)
        {
            secondary.interactable = !interactionLocked && secondaryAction != null;
            SetSceneButtonText(secondary, secondaryLabel);
        }
        BindSceneButton(primary, primaryAction);
        BindSceneButton(secondary, secondaryAction);
    }

    private void ChangeV37FriendPanelMode(Transform root, ServerFriendPanelMode mode)
    {
        if (v37Loading || v37MutationRunning)
        {
            return;
        }

        serverFriendPanelMode = mode;
        v37PageIndex = 0;
        v37SelectedFriendPlayerId = 0;
        v37SelectedRequestId = 0;
        v37FriendSearchResult = null;
        friendDeleteConfirmation = false;
        StartCoroutine(RefreshServerFriendRequestState(root, mode == ServerFriendPanelMode.Friends, "已切换到" + GetV37ModeLabel()));
    }

    private void ChangeV37FriendPage(Transform root, int offset)
    {
        v37PageIndex = Mathf.Clamp(v37PageIndex + offset, 0, GetV37PageCount() - 1);
        RefreshServerFriendRequestPanel(root, "已切换列表页");
    }

    private void HandleV37SearchOrRequest(Transform root)
    {
        if (v37Loading || v37MutationRunning)
        {
            return;
        }

        InputField input = FindSceneComponent<InputField>(root, "FriendsAddInput");
        string value = input == null ? string.Empty : input.text.Trim();
        if (!int.TryParse(value, out int playerId) || playerId <= 0)
        {
            RefreshServerFriendRequestPanel(root, "请输入正确的玩家 ID");
            return;
        }

        if (playerId == GameSession.PlayerId)
        {
            RefreshServerFriendRequestPanel(root, "不能向自己发送好友申请");
            return;
        }

        if (v37FriendSearchResult == null || v37FriendSearchPlayerId != playerId)
        {
            StartCoroutine(SearchV37Friend(root, playerId));
            return;
        }

        switch (v37FriendSearchResult.relationshipStatus)
        {
            case "NONE":
                if (!IsV37FriendRequestCreationAvailable())
                {
                    RefreshServerFriendRequestPanel(root, "好友申请当前不可用");
                    break;
                }
                StartCoroutine(CreateV37FriendRequest(root, playerId));
                break;
            case "INCOMING_PENDING":
                ChangeV37FriendPanelMode(root, ServerFriendPanelMode.IncomingRequests);
                break;
            case "OUTGOING_PENDING":
                ChangeV37FriendPanelMode(root, ServerFriendPanelMode.OutgoingRequests);
                break;
            case "FRIEND":
                v37FriendSearchResult = null;
                v37SelectedFriendPlayerId = playerId;
                RefreshServerFriendRequestPanel(root, "已是正式好友");
                break;
            default:
                RefreshServerFriendRequestPanel(root, "当前玩家不能发送好友申请");
                break;
        }
    }

    private IEnumerator SearchV37Friend(Transform root, int playerId)
    {
        v37Loading = true;
        RefreshServerFriendRequestPanel(root, "正在精确搜索玩家...");
        UnityWebRequest request = CreateAuthorizedGet("/friends/search?playerId=" + playerId);
        if (request == null)
        {
            v37Loading = false;
            RefreshServerFriendRequestPanel(root, requestCreationError);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                v37Loading = false;
                yield break;
            }

            ServerFriendSearchApiResponse response = ParseJson<ServerFriendSearchApiResponse>(request.downloadHandler.text);
            v37Loading = false;
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                v37FriendSearchResult = null;
                v37FriendSearchPlayerId = 0;
                HandleV37FriendRequestFailure(root, request, response, "玩家搜索失败");
                yield break;
            }

            v37FriendSearchResult = response.data;
            v37FriendSearchPlayerId = response.data.playerId;
            v37SelectedFriendPlayerId = 0;
            v37SelectedRequestId = 0;
            RefreshServerFriendRequestPanel(root, "已找到玩家，请确认关系状态后继续操作");
        }
    }

    private IEnumerator CreateV37FriendRequest(Transform root, int recipientPlayerId)
    {
        if (v37MutationRunning)
        {
            yield break;
        }

        v37MutationRunning = true;
        RefreshServerFriendRequestPanel(root, "正在提交好友申请...");
        V37FriendRequestCreatePayload payload = new V37FriendRequestCreatePayload { recipientPlayerId = recipientPlayerId };
        if (!TryCreateAuthorizedPost("/friends/requests", JsonUtility.ToJson(payload), out UnityWebRequest request, out string error))
        {
            v37MutationRunning = false;
            RefreshServerFriendRequestPanel(root, error);
            yield break;
        }

        ServerFriendRequestResponse created = null;
        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                v37MutationRunning = false;
                yield break;
            }

            ServerFriendRequestMutationApiResponse response = ParseJson<ServerFriendRequestMutationApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                v37MutationRunning = false;
                HandleV37FriendRequestFailure(root, request, response, "好友申请提交失败");
                yield break;
            }
            created = response.data;
        }

        v37MutationRunning = false;
        v37FriendSearchResult = null;
        v37FriendSearchPlayerId = 0;
        if (created != null)
        {
            serverFriendPanelMode = string.Equals(created.direction, "INCOMING", StringComparison.OrdinalIgnoreCase)
                ? ServerFriendPanelMode.IncomingRequests
                : ServerFriendPanelMode.OutgoingRequests;
            v37SelectedRequestId = created.requestId;
        }
        InputField input = FindSceneComponent<InputField>(root, "FriendsAddInput");
        if (input != null)
        {
            input.SetTextWithoutNotify(string.Empty);
        }
        yield return RefreshServerFriendRequestState(root, false, "好友申请已提交，等待对方同意");
    }

    private IEnumerator MutateV37FriendRequest(Transform root, long requestId, string action)
    {
        if (v37MutationRunning || requestId <= 0)
        {
            yield break;
        }

        string path = action == "cancel"
            ? "/friends/requests/" + requestId
            : "/friends/requests/" + requestId + "/" + action;
        string method = action == "cancel" ? UnityWebRequest.kHttpVerbDELETE : UnityWebRequest.kHttpVerbPOST;
        v37MutationRunning = true;
        RefreshServerFriendRequestPanel(root, "正在处理好友申请...");
        if (!ApiRequestUtility.TryCreateJsonRequest(path, method, string.Empty, true, out UnityWebRequest request, out string error))
        {
            v37MutationRunning = false;
            RefreshServerFriendRequestPanel(root, error);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                v37MutationRunning = false;
                yield break;
            }

            ServerFriendRequestMutationApiResponse response = ParseJson<ServerFriendRequestMutationApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                v37MutationRunning = false;
                HandleV37FriendRequestFailure(root, request, response, "好友申请处理失败");
                yield break;
            }
        }

        v37MutationRunning = false;
        v37SelectedRequestId = 0;
        bool accepted = action == "accept";
        if (accepted)
        {
            serverFriendPanelMode = ServerFriendPanelMode.Friends;
        }
        yield return RefreshServerFriendRequestState(root, accepted, accepted ? "好友申请已同意，已成为正式好友" : "好友申请状态已更新");
    }

    private void RequestV37FriendDeletion(Transform root, ServerFriendResponse friend)
    {
        if (friend == null || v37MutationRunning)
        {
            return;
        }

        if (!friendDeleteConfirmation)
        {
            friendDeleteConfirmation = true;
            RefreshServerFriendRequestPanel(root, "再次点击“确认删除”移除 " + friend.nickname);
            return;
        }

        StartCoroutine(DeleteV37Friend(root, friend));
    }

    private IEnumerator DeleteV37Friend(Transform root, ServerFriendResponse friend)
    {
        v37MutationRunning = true;
        RefreshServerFriendRequestPanel(root, "正在删除正式好友...");
        if (!ApiRequestUtility.TryCreateJsonRequest(
                "/friends/" + friend.playerId,
                UnityWebRequest.kHttpVerbDELETE,
                string.Empty,
                true,
                out UnityWebRequest request,
                out string error))
        {
            v37MutationRunning = false;
            RefreshServerFriendRequestPanel(root, error);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                v37MutationRunning = false;
                yield break;
            }

            BasicApiResponse response = ParseJson<BasicApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200)
            {
                v37MutationRunning = false;
                RefreshServerFriendRequestPanel(root, GetV37RequestError(request, response, "删除好友失败"));
                yield break;
            }
        }

        v37MutationRunning = false;
        friendDeleteConfirmation = false;
        v37SelectedFriendPlayerId = 0;
        yield return RefreshServerFriendRequestState(root, true, "已删除好友：" + friend.nickname);
    }

    private IEnumerator SendV37StaminaGift(Transform root, int friendPlayerId)
    {
        if (v37MutationRunning || friendPlayerId <= 0)
        {
            yield break;
        }

        v37MutationRunning = true;
        RefreshServerFriendRequestPanel(root, "正在赠送好友体力...");
        if (!TryCreateAuthorizedPost("/friends/" + friendPlayerId + "/gifts/stamina", string.Empty, out UnityWebRequest request, out string error))
        {
            v37MutationRunning = false;
            RefreshServerFriendRequestPanel(root, error);
            yield break;
        }

        ServerDailyGiftSendResponse gift = null;
        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                v37MutationRunning = false;
                yield break;
            }

            ServerDailyGiftApiResponse response = ParseJson<ServerDailyGiftApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                v37MutationRunning = false;
                RefreshServerFriendRequestPanel(root, GetV37RequestError(request, response, "赠礼发送失败"));
                yield break;
            }
            gift = response.data;
        }

        v37MutationRunning = false;
        string replay = gift.replayed ? "（今日已赠送，返回原结果）" : string.Empty;
        yield return RefreshServerFriendRequestState(root, true, "已为好友赠送 " + gift.quantity + " 点体力" + replay);
    }

    private List<ServerFriendResponse> GetFilteredV37Friends()
    {
        List<ServerFriendResponse> result = new List<ServerFriendResponse>();
        for (int i = 0; i < v37Friends.Count; i++)
        {
            ServerFriendResponse friend = v37Friends[i];
            if (friend != null && MatchesV37Filter(friend.playerId.ToString(), friend.nickname))
            {
                result.Add(friend);
            }
        }
        return result;
    }

    private List<ServerFriendRequestResponse> GetFilteredV37Requests()
    {
        List<ServerFriendRequestResponse> source = serverFriendPanelMode == ServerFriendPanelMode.IncomingRequests
            ? v37IncomingRequests
            : v37OutgoingRequests;
        List<ServerFriendRequestResponse> result = new List<ServerFriendRequestResponse>();
        for (int i = 0; i < source.Count; i++)
        {
            ServerFriendRequestResponse request = source[i];
            if (request == null)
            {
                continue;
            }
            bool incoming = serverFriendPanelMode == ServerFriendPanelMode.IncomingRequests;
            int otherPlayerId = incoming ? request.requesterPlayerId : request.recipientPlayerId;
            string otherName = incoming ? request.requesterNickname : request.recipientNickname;
            if (MatchesV37Filter(otherPlayerId.ToString(), otherName))
            {
                result.Add(request);
            }
        }
        return result;
    }

    private bool MatchesV37Filter(string playerId, string nickname)
    {
        return string.IsNullOrEmpty(friendsSearchQuery)
            || (!string.IsNullOrEmpty(playerId) && playerId.IndexOf(friendsSearchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
            || (!string.IsNullOrEmpty(nickname) && nickname.IndexOf(friendsSearchQuery, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private int GetV37PageCount()
    {
        int count = serverFriendPanelMode == ServerFriendPanelMode.Friends
            ? GetFilteredV37Friends().Count
            : GetFilteredV37Requests().Count;
        return Mathf.Max(1, Mathf.CeilToInt(count / (float)FriendsPerPage));
    }

    private ServerFriendResponse FindV37Friend(int playerId)
    {
        for (int i = 0; i < v37Friends.Count; i++)
        {
            if (v37Friends[i] != null && v37Friends[i].playerId == playerId)
            {
                return v37Friends[i];
            }
        }
        return null;
    }

    private void PreserveNewerV38Presence(List<ServerFriendResponse> loaded)
    {
        for (int i = 0; i < loaded.Count; i++)
        {
            ServerFriendResponse snapshot = loaded[i];
            ServerFriendResponse current = snapshot == null ? null : FindV37Friend(snapshot.playerId);
            if (current != null && current.presenceVersion > snapshot.presenceVersion)
            {
                CopyV38Presence(current, snapshot);
            }
        }
    }

    private void ApplyV38FriendPresence(FriendPresenceChange presence)
    {
        if (presence == null || presence.playerId <= 0 || presence.presenceVersion <= 0)
        {
            return;
        }

        ServerFriendResponse friend = FindV37Friend(presence.playerId);
        if (friend == null || presence.presenceVersion <= friend.presenceVersion)
        {
            return;
        }

        friend.onlineStatus = NormalizeV38OnlineStatus(presence.onlineStatus);
        friend.presenceVersion = presence.presenceVersion;
        friend.lastActiveTime = presence.lastActiveTime;
        friend.lastOfflineTime = presence.lastOfflineTime;

        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Friends);
        if (panel != null && panel.gameObject.activeInHierarchy && serverFriendPanelMode == ServerFriendPanelMode.Friends)
        {
            RefreshServerFriendRequestPanel(panel.PanelRoot, null);
        }
    }

    private void RefreshV38FriendPresenceAfterRealtimeConnected()
    {
        if (!HasAuthSession() || !realtimeConnected)
        {
            return;
        }

        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Friends);
        if (panel != null)
        {
            if (v38PresenceRefreshCoroutine != null)
            {
                StopCoroutine(v38PresenceRefreshCoroutine);
            }

            v38PresenceRefreshCoroutine = StartCoroutine(
                RefreshV38FriendPresenceAfterRealtimeReconnect(panel.PanelRoot));
        }
    }

    private IEnumerator RefreshV38FriendPresenceAfterRealtimeReconnect(Transform root)
    {
        while ((v37Loading || v37MutationRunning) && realtimeConnected && HasAuthSession())
        {
            yield return null;
        }

        if (realtimeConnected && HasAuthSession())
        {
            yield return RefreshServerFriendRequestState(
                root,
                true,
                "实时连接已恢复，好友在线状态已刷新");
        }

        v38PresenceRefreshCoroutine = null;
    }

    private static void CopyV38Presence(ServerFriendResponse source, ServerFriendResponse target)
    {
        target.onlineStatus = source.onlineStatus;
        target.presenceVersion = source.presenceVersion;
        target.lastActiveTime = source.lastActiveTime;
        target.lastOfflineTime = source.lastOfflineTime;
    }

    private static string NormalizeV38OnlineStatus(string value)
    {
        if (string.Equals(value, "ONLINE", StringComparison.OrdinalIgnoreCase))
        {
            return "ONLINE";
        }
        if (string.Equals(value, "OFFLINE", StringComparison.OrdinalIgnoreCase))
        {
            return "OFFLINE";
        }
        return "UNKNOWN";
    }

    private static string GetV38FriendPresenceLabel(ServerFriendResponse friend)
    {
        switch (NormalizeV38OnlineStatus(friend == null ? null : friend.onlineStatus))
        {
            case "ONLINE":
                return "在线";
            case "OFFLINE":
                return "离线";
            default:
                return "状态未知";
        }
    }

    private static Color GetV38FriendPresenceColor(ServerFriendResponse friend)
    {
        switch (NormalizeV38OnlineStatus(friend == null ? null : friend.onlineStatus))
        {
            case "ONLINE":
                return new Color(0.22f, 0.70f, 0.34f, 1f);
            case "OFFLINE":
                return new Color(0.58f, 0.55f, 0.50f, 1f);
            default:
                return new Color(0.86f, 0.58f, 0.18f, 1f);
        }
    }

    private static string BuildV38FriendRowMetadata(ServerFriendResponse friend)
    {
        string date = ShortV38PresenceTime(friend == null ? null : friend.friendsSince);
        return string.IsNullOrEmpty(date)
            ? "Lv." + Mathf.Max(1, friend == null ? 1 : friend.level) + "    正式好友"
            : "Lv." + Mathf.Max(1, friend.level) + "    好友始于 " + date;
    }

    private static string BuildV38FriendPresenceDetail(ServerFriendResponse friend)
    {
        string status = NormalizeV38OnlineStatus(friend == null ? null : friend.onlineStatus);
        string time = status == "OFFLINE" ? friend.lastOfflineTime : friend == null ? null : friend.lastActiveTime;
        string label = status == "OFFLINE" ? "最后离线" : "最近活跃";
        return string.IsNullOrWhiteSpace(time)
            ? (status == "UNKNOWN" ? "在线状态暂不可用" : "正式好友")
            : label + "：" + time.Trim();
    }

    private static string ShortV38PresenceTime(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Substring(0, Mathf.Min(10, value.Trim().Length));
    }

    private static void SetV38FriendPresenceDetailColor(Transform root, ServerFriendResponse friend)
    {
        Text label = FindSceneComponent<Text>(root, "FriendDetailOnline");
        if (label != null)
        {
            label.color = GetV38FriendPresenceColor(friend);
        }
    }

    private ServerFriendRequestResponse FindV37Request(long requestId)
    {
        List<ServerFriendRequestResponse> source = serverFriendPanelMode == ServerFriendPanelMode.IncomingRequests
            ? v37IncomingRequests
            : v37OutgoingRequests;
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null && source[i].requestId == requestId)
            {
                return source[i];
            }
        }
        return null;
    }

    private string BuildV37FriendCountText()
    {
        if (v37FriendRequestSummary == null)
        {
            return "好友 " + v37Friends.Count + "    申请摘要同步中";
        }

        return $"好友 {v37FriendRequestSummary.friendCount}/{v37FriendRequestSummary.maxFriendCount}    收到 {v37FriendRequestSummary.pendingIncomingCount}    发出 {v37FriendRequestSummary.pendingOutgoingCount}";
    }

    private string BuildV37IncomingTabLabel()
    {
        int count = v37FriendRequestSummary == null ? v37IncomingRequests.Count : v37FriendRequestSummary.pendingIncomingCount;
        return "收到 " + count;
    }

    private string BuildV37OutgoingTabLabel()
    {
        int count = v37FriendRequestSummary == null ? v37OutgoingRequests.Count : v37FriendRequestSummary.pendingOutgoingCount;
        return "发出 " + count;
    }

    private string GetV37ModeLabel()
    {
        switch (serverFriendPanelMode)
        {
            case ServerFriendPanelMode.IncomingRequests:
                return "收到的申请";
            case ServerFriendPanelMode.OutgoingRequests:
                return "发出的申请";
            default:
                return "正式好友";
        }
    }

    private string GetV37RelationshipLabel(string status)
    {
        switch (status)
        {
            case "FRIEND": return "已是正式好友";
            case "OUTGOING_PENDING": return "已发出申请";
            case "INCOMING_PENDING": return "收到对方申请";
            case "SELF": return "当前玩家";
            default: return "可发送申请";
        }
    }

    private string FormatV37RequestTime(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "时间未知" : value.Trim();
    }

    private void SetV37LoadFailure(string message)
    {
        v37LastLoadFailed = true;
        v37StatusMessage = string.IsNullOrWhiteSpace(message) ? "好友数据读取失败" : message;
    }

    private bool IsV37FriendRequestCreationAvailable()
    {
        return !v37FriendRequestCreationBlocked
            && (v37FriendRequestSummary == null || v37FriendRequestSummary.enabled);
    }

    private void UpdateV37FriendRequestAvailability(BasicApiResponse response)
    {
        if (response != null && (response.code == 6709 || response.code == 6719))
        {
            v37FriendRequestCreationBlocked = true;
        }
    }

    private void HandleV37FriendRequestFailure(
        Transform root,
        UnityWebRequest request,
        BasicApiResponse response,
        string fallback)
    {
        string message = GetV37RequestError(request, response, fallback);
        UpdateV37FriendRequestAvailability(response);
        if (ShouldRefreshV37FriendRequestState(response))
        {
            StartCoroutine(RefreshServerFriendRequestState(root, false, message));
            return;
        }

        RefreshServerFriendRequestPanel(root, message);
    }

    private static bool ShouldRefreshV37FriendRequestState(BasicApiResponse response)
    {
        if (response == null)
        {
            return false;
        }

        switch (response.code)
        {
            case 6708:
            case 6709:
            case 6710:
            case 6711:
            case 6712:
            case 6713:
            case 6714:
            case 6715:
            case 6716:
            case 6717:
            case 6718:
            case 6719:
            case 6720:
                return true;
            default:
                return false;
        }
    }

    private string GetV37RequestError(UnityWebRequest request, BasicApiResponse response, string fallback)
    {
        return response != null && !string.IsNullOrWhiteSpace(response.message)
            ? response.message
            : ApiRequestUtility.GetErrorMessage(request, fallback);
    }

    private void EnsureServerFriendRequestControls(Transform root)
    {
        Transform panel = FindSceneTransform(root, "FriendsPanel");
        if (panel == null)
        {
            return;
        }

        Button friendTab = FindSceneComponent<Button>(root, "FriendsFilterAll");
        Button incomingTab = FindSceneComponent<Button>(root, "FriendsFilterOnline");
        Button weChatGift = FindSceneComponent<Button>(root, "FriendsWeChatGiftButton");
        InputField playerInput = FindSceneComponent<InputField>(root, "FriendsAddInput");
        Button searchButton = FindSceneComponent<Button>(root, "FriendsAddButton");
        SetV37ControlRect(friendTab == null ? null : friendTab.GetComponent<RectTransform>(), new Vector2(382f, -122f), new Vector2(90f, 50f));
        SetV37ControlRect(incomingTab == null ? null : incomingTab.GetComponent<RectTransform>(), new Vector2(482f, -122f), new Vector2(100f, 50f));
        SetV37ControlRect(weChatGift == null ? null : weChatGift.GetComponent<RectTransform>(), new Vector2(692f, -122f), new Vector2(176f, 50f));
        SetV37ControlRect(playerInput == null ? null : playerInput.GetComponent<RectTransform>(), new Vector2(880f, -122f), new Vector2(246f, 50f));
        SetV37ControlRect(searchButton == null ? null : searchButton.GetComponent<RectTransform>(), new Vector2(1140f, -122f), new Vector2(318f, 50f));

        if (FindSceneComponent<Button>(root, "FriendsFilterOutgoing") == null)
        {
            Button outgoing = CreateButton(panel, "FriendsFilterOutgoing", "发出", new Color(0.90f, 0.79f, 0.59f, 1f), new Color(0.36f, 0.20f, 0.10f, 1f));
            SetV37ControlRect(outgoing.GetComponent<RectTransform>(), new Vector2(592f, -122f), new Vector2(90f, 50f));
            ShrinkButtonLabel(outgoing, 17);
        }

        Transform detail = FindSceneTransform(root, "FriendDetailContent");
        if (detail == null)
        {
            return;
        }

        if (FindSceneComponent<Button>(root, "FriendsRequestPrimaryButton") == null)
        {
            Button primary = CreateButton(detail, "FriendsRequestPrimaryButton", "同意", new Color(0.32f, 0.66f, 0.38f, 1f), Color.white);
            SetV37ControlRect(primary.GetComponent<RectTransform>(), new Vector2(72f, -416f), new Vector2(174f, 50f));
            ShrinkButtonLabel(primary, 18);
        }
        if (FindSceneComponent<Button>(root, "FriendsRequestSecondaryButton") == null)
        {
            Button secondary = CreateButton(detail, "FriendsRequestSecondaryButton", "拒绝", new Color(0.84f, 0.38f, 0.30f, 1f), Color.white);
            SetV37ControlRect(secondary.GetComponent<RectTransform>(), new Vector2(276f, -416f), new Vector2(174f, 50f));
            ShrinkButtonLabel(secondary, 18);
        }
    }

    private void SetV37ControlRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect != null)
        {
            SetRect(rect, TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), position, size);
        }
    }

#pragma warning disable 0649
    [Serializable]
    private class V37FriendListApiResponse : BasicApiResponse
    {
        public V37FriendListData data;
    }

    [Serializable]
    private class V37FriendListData
    {
        public ServerFriendResponse[] items;
        public ServerFriendCursor nextCursor;
        public bool hasMore;
        public ServerDailyGiftStatus dailyGift;
    }

    [Serializable]
    private class ServerFriendSearchApiResponse : BasicApiResponse
    {
        public ServerFriendSearchResponse data;
    }

    [Serializable]
    private class ServerFriendSearchResponse
    {
        public int playerId;
        public string nickname;
        public int level;
        public string relationshipStatus;
        public long pendingRequestId;
        public string avatarCode;
        public long avatarVersion;
    }

    [Serializable]
    private class V37FriendRequestCreatePayload
    {
        public int recipientPlayerId;
    }

    [Serializable]
    private class ServerFriendRequestMutationApiResponse : BasicApiResponse
    {
        public ServerFriendRequestResponse data;
    }

    [Serializable]
    private class ServerFriendRequestListApiResponse : BasicApiResponse
    {
        public ServerFriendRequestListData data;
    }

    [Serializable]
    private class ServerFriendRequestListData
    {
        public ServerFriendRequestResponse[] items;
        public long nextCursor;
        public bool hasMore;
    }

    [Serializable]
    private class ServerFriendRequestResponse
    {
        public long requestId;
        public string direction;
        public int requesterPlayerId;
        public string requesterNickname;
        public int requesterLevel;
        public int recipientPlayerId;
        public string recipientNickname;
        public int recipientLevel;
        public string requesterAvatarCode;
        public long requesterAvatarVersion;
        public string recipientAvatarCode;
        public long recipientAvatarVersion;
        public string status;
        public string createTime;
        public string expireTime;
        public string resolvedTime;
        public string resolutionReason;
        public string relationshipEndedTime;
        public bool canAccept;
        public bool canReject;
        public bool canCancel;
    }

    [Serializable]
    private class ServerFriendRequestSummaryApiResponse : BasicApiResponse
    {
        public ServerFriendRequestSummary data;
    }

    [Serializable]
    private class ServerFriendRequestSummary
    {
        public bool enabled;
        public int friendCount;
        public int maxFriendCount;
        public int pendingOutgoingCount;
        public int maxPendingOutgoing;
        public int pendingIncomingCount;
        public int maxPendingIncoming;
        public string usageDate;
        public int dailyRequestCount;
        public int dailyRequestLimit;
        public int resendCooldownHours;
        public int requestExpirationDays;
        public int configVersion;
    }
#pragma warning restore 0649
}
