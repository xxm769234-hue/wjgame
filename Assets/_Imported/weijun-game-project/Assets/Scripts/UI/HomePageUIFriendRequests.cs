using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
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
    private readonly Dictionary<Transform, int> v37FriendRowPlayerIds = new Dictionary<Transform, int>();
    private readonly Dictionary<Text, Color> friendPresenceSceneColors = new Dictionary<Text, Color>();
    private ServerFriendRequestSummary v37FriendRequestSummary;
    private ServerDailyGiftStatus serverDailyStaminaGiftStatus;
    private ServerFriendSearchResponse v37FriendSearchResult;
    private int v37FriendSearchPlayerId;
    private int v37SelectedFriendPlayerId;
    private long v37SelectedRequestId;
    private bool resetFriendListScroll = true;
    private int v37PanelRevision;
    private bool v37Loading;
    private bool v37MutationRunning;
    private bool v37LastLoadFailed;
    private bool v37FriendRequestCreationBlocked;
    private string v37StatusMessage = string.Empty;
    private Coroutine v38PresenceRefreshCoroutine;
    private bool friendSearchDialogOpen;
    private int friendSearchDialogRevision;
    private bool friendSearchLoading;
    private string friendSearchStatus = string.Empty;

    public static bool EnsureFriendSearchDialog(Transform root)
    {
        if (root == null) return false;
        bool inputUpdated = EnsureFriendInputViewport(FindSceneComponent<InputField>(root, "FriendsSearchInput"));
        if (FindSceneTransform(root, "FriendSearchOverlay") != null)
        {
            return EnsureFriendInputViewport(FindSceneComponent<InputField>(root, "FriendSearchPlayerIdInput"))
                || inputUpdated;
        }
        InputField inputTemplate = FindSceneComponent<InputField>(root, "FriendsAddInput");
        Button buttonTemplate = FindSceneComponent<Button>(root, "FriendsAddButton");
        Text textTemplate = FindSceneComponent<Text>(root, "FriendDetailId");
        Image paperTemplate = FindSceneComponent<Image>(root, "FriendDetailArea");
        if (inputTemplate == null || buttonTemplate == null || textTemplate == null) return inputUpdated;

        GameObject overlay = new GameObject("FriendSearchOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.layer = root.gameObject.layer;
        overlay.transform.SetParent(root, false);
        RectTransform overlayRect = (RectTransform)overlay.transform;
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
        GameObject paper = new GameObject("FriendSearchPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        paper.layer = overlay.layer;
        paper.transform.SetParent(overlay.transform, false);
        SetFriendDialogRect((RectTransform)paper.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660f, 430f));
        Image background = paper.GetComponent<Image>();
        background.sprite = paperTemplate != null ? paperTemplate.sprite : null;
        background.color = paperTemplate != null ? paperTemplate.color : Color.white;
        background.type = paperTemplate != null ? paperTemplate.type : Image.Type.Simple;

        CreateFriendDialogText(textTemplate, paper.transform, "FriendSearchTitle", "添加好友", new Vector2(0f, 170f), new Vector2(430f, 40f), 28);
        InputField input = Instantiate(inputTemplate, paper.transform, false);
        input.name = "FriendSearchPlayerIdInput";
        EnsureFriendInputViewport(input);
        input.gameObject.SetActive(true);
        input.SetTextWithoutNotify(string.Empty);
        input.onValueChanged.RemoveAllListeners();
        input.onEndEdit.RemoveAllListeners();
        SetFriendDialogRect((RectTransform)input.transform, new Vector2(0.5f, 0.5f), new Vector2(-80f, 105f), new Vector2(380f, 54f));
        CreateFriendDialogButton(buttonTemplate, paper.transform, "FriendSearchQueryButton", "搜索", new Vector2(205f, 105f), new Vector2(130f, 54f));
        CreateFriendDialogButton(buttonTemplate, paper.transform, "FriendSearchCloseButton", "关闭", new Vector2(245f, 173f), new Vector2(96f, 44f));
        CreateFriendDialogButton(buttonTemplate, paper.transform, "FriendSearchActionButton", "发送申请", new Vector2(0f, -90f), new Vector2(180f, 56f));
        CreateFriendDialogText(textTemplate, paper.transform, "FriendSearchResult", "", new Vector2(65f, 10f), new Vector2(390f, 100f), 20);
        CreateFriendDialogText(textTemplate, paper.transform, "FriendSearchStatus", "请输入玩家ID", new Vector2(0f, -158f), new Vector2(560f, 60f), 17);
        Image avatarTemplate = FindSceneComponent<Image>(root, "FriendDetailAvatarBackground");
        if (avatarTemplate != null)
        {
            Image avatar = Instantiate(avatarTemplate, paper.transform, false);
            avatar.name = "FriendSearchAvatar";
            for (int index = avatar.transform.childCount - 1; index >= 0; index--) avatar.transform.GetChild(index).gameObject.SetActive(false);
            SetFriendDialogRect(avatar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-215f, 10f), new Vector2(100f, 100f));
            avatar.raycastTarget = false;
        }
        UpgradeFriendSearchDialogStyle(root);
        overlay.SetActive(false);
        return true;
    }

    private static bool EnsureFriendInputViewport(InputField input)
    {
        if (input == null || input.textViewport != null) return false;
        input.textViewport = input.transform as RectTransform;
        return true;
    }

    public static bool UpgradeFriendSearchDialogStyle(Transform root)
    {
        RectTransform paper = FindSceneTransform(root, "FriendSearchPanel") as RectTransform;
        if (paper == null || Vector2.Distance(paper.sizeDelta, new Vector2(660f, 430f)) > 0.1f) return false;
        Transform shop = null;
        foreach (GameObject sceneRoot in root.gameObject.scene.GetRootGameObjects())
        {
            shop = FindSceneTransform(sceneRoot.transform, "ShopDetail");
            if (shop != null) break;
        }
        if (shop == null) return false;
        CopyFriendDialogImage(paper.GetComponent<Image>(), shop.GetComponent<Image>());
        SetFriendDialogRect(paper, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800.36f, 450.775f));
        Image overlay = FindSceneComponent<Image>(root, "FriendSearchOverlay");
        if (overlay != null) overlay.color = new Color(0f, 0f, 0f, 0.55f);
        StyleFriendDialogButton(root, shop, "FriendSearchQueryButton", "ShopBuyGoldButton", new Vector2(220f, 105f));
        StyleFriendDialogButton(root, shop, "FriendSearchCloseButton", "ShopDetailCloseButton", new Vector2(-130f, -170f));
        StyleFriendDialogButton(root, shop, "FriendSearchActionButton", "ShopBuyGoldButton", new Vector2(120f, -170f));
        SetSceneButtonText(FindSceneComponent<Button>(root, "FriendSearchCloseButton"), "取消");
        RectTransform title = FindSceneTransform(root, "FriendSearchTitle") as RectTransform;
        if (title != null) title.anchoredPosition = new Vector2(0f, 180f);
        RectTransform status = FindSceneTransform(root, "FriendSearchStatus") as RectTransform;
        if (status != null) SetFriendDialogRect(status, new Vector2(0.5f, 0.5f), new Vector2(0f, -85f), new Vector2(620f, 50f));
        return true;
    }

    private static void CopyFriendDialogImage(Image target, Image source)
    {
        if (target == null || source == null) return;
        target.sprite = source.sprite;
        target.overrideSprite = null;
        target.color = source.color;
        target.material = source.material;
        target.type = source.type;
        target.preserveAspect = source.preserveAspect;
        target.raycastTarget = true;
    }

    private static void StyleFriendDialogButton(Transform root, Transform shop, string name, string templateName, Vector2 position)
    {
        Button button = FindSceneComponent<Button>(root, name);
        Button template = FindSceneComponent<Button>(shop, templateName);
        if (button == null || template == null) return;
        CopyFriendDialogImage(button.GetComponent<Image>(), template.GetComponent<Image>());
        button.transition = template.transition;
        button.colors = template.colors;
        button.spriteState = template.spriteState;
        SetFriendDialogRect((RectTransform)button.transform, new Vector2(0.5f, 0.5f), position, new Vector2(150f, 64f));
    }

    private static void SetFriendDialogRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void CreateFriendDialogText(Text template, Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize)
    {
        Text label = Instantiate(template, parent, false);
        label.name = name;
        label.gameObject.SetActive(true);
        label.text = value;
        label.fontSize = label.fontSizeMax = fontSize;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12;
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.raycastTarget = false;
        SetFriendDialogRect(label.rectTransform, new Vector2(0.5f, 0.5f), position, size);
    }

    private static void CreateFriendDialogButton(Button template, Transform parent, string name, string value, Vector2 position, Vector2 size)
    {
        Button button = Instantiate(template, parent, false);
        button.name = name;
        button.gameObject.SetActive(true);
        button.onClick = new Button.ButtonClickedEvent();
        SetSceneButtonText(button, value);
        SetFriendDialogRect((RectTransform)button.transform, new Vector2(0.5f, 0.5f), position, size);
    }

    private void OpenFriendSearchDialog(Transform root)
    {
        if (v37MutationRunning || root == null) return;
        EnsureFriendSearchDialog(root);
        Transform overlay = FindSceneTransform(root, "FriendSearchOverlay");
        if (overlay == null)
        {
            Debug.LogWarning("[Friends] Cannot open add-friend dialog: required scene controls are missing.", this);
            SetSceneText(root, "FriendsStatus", "添加好友窗口组件缺失，请同步好友窗口");
            return;
        }
        Transform paper = FindSceneTransform(overlay, "FriendSearchPanel");
        if (paper == null)
        {
            Debug.LogWarning("[Friends] Add-friend dialog panel is missing under FriendSearchOverlay.", this);
            return;
        }
        friendSearchDialogOpen = true;
        friendSearchDialogRevision++;
        friendSearchLoading = false;
        friendSearchStatus = "请输入玩家ID";
        v37FriendSearchResult = null;
        v37FriendSearchPlayerId = 0;
        InputField input = FindSceneComponent<InputField>(root, "FriendSearchPlayerIdInput");
        if (input != null) input.SetTextWithoutNotify(string.Empty);
        RefreshServerFriendRequestPanel(root, null);
    }

    private void RefreshFriendSearchDialog(Transform root, bool locked)
    {
        SetSceneObjectActive(root, "FriendSearchOverlay", friendSearchDialogOpen);
        if (!friendSearchDialogOpen) return;
        Transform overlay = FindSceneTransform(root, "FriendSearchOverlay");
        if (overlay == null) return;
        overlay.SetAsLastSibling();
        SetSceneObjectActive(overlay, "FriendSearchPanel", true);
        bool searchLocked = friendSearchLoading || v37MutationRunning;
        Button close = FindSceneComponent<Button>(overlay, "FriendSearchCloseButton");
        BindSceneButton(close, () =>
        {
            if (v37MutationRunning) return;
            friendSearchDialogOpen = false;
            friendSearchDialogRevision++;
            friendSearchLoading = false;
            v37FriendSearchResult = null;
            RefreshServerFriendRequestPanel(root, "已关闭添加好友");
        });
        if (close != null) close.interactable = !v37MutationRunning;
        Button search = FindSceneComponent<Button>(overlay, "FriendSearchQueryButton");
        if (search != null) search.interactable = !searchLocked;
        BindSceneButton(search, () =>
        {
            if (searchLocked) return;
            v37FriendSearchResult = null;
            HandleV37SearchOrRequest(root);
        });
        Button action = FindSceneComponent<Button>(overlay, "FriendSearchActionButton");
        ConfigureV37SearchButton(action, locked);
        if (action != null)
        {
            action.gameObject.SetActive(true);
            action.interactable = action.interactable && !searchLocked && v37FriendSearchResult != null;
            if (v37FriendSearchResult == null) SetSceneButtonText(action, "发送申请");
        }
        BindSceneButton(action, () => HandleV37SearchOrRequest(root));
        ServerFriendSearchResponse result = v37FriendSearchResult;
        SetSceneText(overlay, "FriendSearchResult", result == null ? "暂无搜索结果" :
            result.nickname + "\n玩家ID " + result.playerId + "    Lv." + result.level + "\n" + GetV37RelationshipLabel(result.relationshipStatus));
        SetSceneText(overlay, "FriendSearchStatus", v37MutationRunning ? v37StatusMessage : friendSearchStatus);
        Image avatar = FindSceneComponent<Image>(overlay, "FriendSearchAvatar");
        if (avatar != null)
        {
            avatar.gameObject.SetActive(result != null);
            if (result != null) ApplyAvatarImage(avatar, null, result.playerId, result.avatarUrl, result.avatarCode, result.avatarVersion);
        }
    }

    private static bool IsPendingFriendRequest(ServerFriendRequestResponse request)
    {
        return request != null && string.Equals(request.status, "PENDING", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetFriendRequestStatusLabel(string status)
    {
        switch ((status ?? string.Empty).ToUpperInvariant())
        {
            case "PENDING": return "待处理";
            case "ACCEPTED": return "已同意";
            case "REJECTED": return "已拒绝";
            case "CANCELLED": return "已取消";
            case "EXPIRED": return "已过期";
            case "INVALIDATED": return "已失效";
            default: return "状态未知";
        }
    }

    private void ResetServerFriendRequestPanel()
    {
        friendSearchDialogRevision++;
        friendSearchLoading = false;
        friendSearchStatus = string.Empty;
        resetFriendRequestScroll = true;
        v37PanelRevision++;
        serverFriendPanelMode = ServerFriendPanelMode.Friends;
        friendSearchDialogOpen = false;
        v37Friends.Clear();
        v37IncomingRequests.Clear();
        v37OutgoingRequests.Clear();
        v37FriendRowPlayerIds.Clear();
        v37FriendRequestSummary = null;
        serverDailyGiftStatus = null;
        serverDailyStaminaGiftStatus = null;
        v37FriendSearchResult = null;
        v37FriendSearchPlayerId = 0;
        v37SelectedFriendPlayerId = 0;
        v37SelectedRequestId = 0;
        resetFriendListScroll = true;
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
        friendSearchDialogRevision++;
        friendSearchLoading = false;
        friendSearchDialogOpen = false;
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
        if (revision == v37PanelRevision && serverFriendPanelMode != ServerFriendPanelMode.Friends)
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
                if (response.data.dailyStaminaGift != null)
                {
                    serverDailyStaminaGiftStatus = response.data.dailyStaminaGift;
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
                : "/friends/requests/outgoing?status=ALL&limit=100";
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

        if (!string.IsNullOrEmpty(status))
        {
            v37StatusMessage = status;
        }

        bool interactionLocked = v37Loading || v37MutationRunning;
        SetSceneText(root, "FriendsCount", BuildV37FriendCountText());
        RefreshFriendSearchDialog(root, interactionLocked);
        RefreshFriendRequestsDialog(root, interactionLocked);
        if (serverFriendPanelMode == ServerFriendPanelMode.Friends)
        {
            SetSceneText(root, "FriendsTitle", "游戏好友");
            SetSceneText(root, "FriendsSubtitle", "正式好友");
            SetSceneText(root, "FriendsStatus", v37StatusMessage);
            ConfigureV37TopControls(root, interactionLocked);
            RenderV37FriendRows(root, interactionLocked);
            RefreshV37FriendDetail(root, interactionLocked);
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
                resetFriendListScroll = true;
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

        SetSceneObjectActive(root, "FriendsAddInput", false);
        InputField playerIdInput = FindSceneComponent<InputField>(root, "FriendSearchPlayerIdInput");
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
                    friendSearchStatus = "请输入玩家ID并搜索";
                }
                RefreshServerFriendRequestPanel(root, null);
            });
            if (playerIdInput.placeholder is Text placeholder)
            {
                placeholder.text = "输入玩家 ID";
            }
            playerIdInput.interactable = !friendSearchLoading && !v37MutationRunning;
        }

        Button friendTab = FindSceneComponent<Button>(root, "FriendsFilterAll");
        Button requestsButton = FindSceneComponent<Button>(root, "FriendsRequestsButton");
        SetSceneButtonText(friendTab, "好友");
        SetSceneButtonText(requestsButton, "申请");
        if (friendTab != null) friendTab.interactable = !interactionLocked;
        if (requestsButton != null) requestsButton.interactable = !v37MutationRunning;
        BindSceneButton(friendTab, () => ChangeV37FriendPanelMode(root, ServerFriendPanelMode.Friends));
        BindSceneButton(requestsButton, () => ChangeV37FriendPanelMode(root, ServerFriendPanelMode.IncomingRequests));
        BindSceneButton(FindSceneComponent<Button>(root, "FriendsClearSearchButton"), () =>
        {
            friendsSearchQuery = string.Empty;
            resetFriendListScroll = true;
            RefreshServerFriendRequestPanel(root, "筛选条件已清除");
        });
        BindSceneButton(FindSceneComponent<Button>(root, "FriendsAddButton"), () => OpenFriendSearchDialog(root));
        BindSceneButton(FindSceneComponent<Button>(root, "FriendsCloseButton"), HideFriendsPanel);
        SetSceneObjectActive(root, "FriendsWeChatGiftButton", false);

        Button addButton = FindSceneComponent<Button>(root, "FriendsAddButton");
        SetSceneButtonText(addButton, "添加好友");
        if (addButton != null) addButton.interactable = !v37MutationRunning;

    }

    private void ConfigureV37SearchButton(Button button, bool interactionLocked)
    {
        if (button == null)
        {
            return;
        }

        string label = "发送申请";
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
                    label = "查看好友";
                    break;
                case "SELF":
                    label = "当前玩家";
                    enabled = false;
                    break;
                default:
                    label = "暂不可操作";
                    enabled = false;
                    break;
            }
        }

        button.interactable = enabled;
        SetSceneButtonText(button, label);
    }


    private void RenderV37FriendRows(Transform root, bool interactionLocked)
    {
        List<ServerFriendResponse> visibleFriends = GetFilteredV37Friends();
        int rowCount = visibleFriends.Count;
        List<Transform> rows = EnsureV37FriendRows(root, rowCount);
        bool selectionInList = false;
        for (int index = 0; index < rowCount; index++)
        {
            if (visibleFriends[index].playerId == v37SelectedFriendPlayerId)
            {
                selectionInList = true;
                break;
            }
        }
        if (!selectionInList)
        {
            v37SelectedFriendPlayerId = rowCount > 0 ? visibleFriends[0].playerId : 0;
        }

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            Transform row = rows[rowIndex];
            if (row == null)
            {
                continue;
            }

            if (rowIndex >= rowCount)
            {
                ReleaseV37FriendRow(row, rowIndex);
                continue;
            }

            ServerFriendResponse friend = visibleFriends[rowIndex];
            bool selected = v37FriendSearchResult == null && friend.playerId == v37SelectedFriendPlayerId;
            RenderV37Row(
                row,
                rowIndex,
                selected,
                friend.playerId,
                friend.nickname,
                friend.avatarUrl,
                friend.avatarCode,
                friend.avatarVersion,
                friend.level,
                GetV38FriendPresenceLabel(friend),
                GetV38FriendPresenceColor(friend),
                NormalizeV38OnlineStatus(friend.onlineStatus) == "OFFLINE",
                interactionLocked,
                () =>
                {
                    v37FriendSearchResult = null;
                    v37SelectedFriendPlayerId = friend.playerId;
                    friendDeleteConfirmation = false;
                    RefreshServerFriendRequestPanel(root, "已选择好友");
                });
            row.gameObject.SetActive(true);
        }
        RefreshFriendListScroll(root);
    }

    private List<Transform> EnsureV37FriendRows(Transform root, int requiredCount)
    {
        List<Transform> rows = new List<Transform>();
        Transform parent = FindSceneTransform(root, "RowParent");
        Transform template = parent != null ? parent.Find("FriendRow0") : FindSceneTransform(root, "FriendRow0");
        RectTransform templateRect = template as RectTransform;
        if (templateRect == null || template.parent == null)
        {
            if (requiredCount > 0)
            {
                Debug.LogWarning("[Friends] FriendRow0 is missing; cannot create friend rows.", this);
            }
            return rows;
        }

        parent = template.parent;
        float rowHeight = templateRect.rect.height * Mathf.Abs(templateRect.localScale.y);
        VerticalLayoutGroup layout = parent.GetComponent<VerticalLayoutGroup>();
        float rowStep = rowHeight + (layout != null ? layout.spacing : rowHeight * 0.1f);
        rows.Add(template);
        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child == template || !child.name.StartsWith("FriendRow", StringComparison.Ordinal)) continue;
            string suffix = child.name.Substring("FriendRow".Length);
            if (int.TryParse(suffix, out _) || child.name.StartsWith("FriendRow0 (", StringComparison.Ordinal))
            {
                rows.Add(child);
            }
        }
        int totalRows = Mathf.Max(requiredCount, rows.Count);
        for (int index = 0; index < totalRows; index++)
        {
            bool created = index >= rows.Count;
            Transform row = created ? Instantiate(template.gameObject, parent, false).transform : rows[index];
            string oldSuffix = row.name.Substring("FriendRow".Length);
            foreach (string prefix in new[] { "FriendRowAvatarBackground", "FriendRowAvatar", "FriendRowOnlineMark", "FriendRowName", "FriendRowId", "FriendRowMeta", "FriendRowState" })
            {
                Transform child = FindSceneTransform(row, prefix + oldSuffix) ?? FindSceneTransform(row, prefix + "0");
                if (child != null) child.name = prefix + index;
            }
            row.name = "FriendRow" + index;
            if (created)
            {
                rows.Add(row);
                row.gameObject.SetActive(false);
                RectTransform rowRect = row as RectTransform;
                if (layout == null) rowRect.anchoredPosition = templateRect.anchoredPosition - new Vector2(0f, rowStep * index);
                Button button = row.GetComponent<Button>();
                if (button != null) button.onClick = new Button.ButtonClickedEvent();
                Image templateAvatar = FindSceneComponent<Image>(template, "FriendRowAvatarBackground0");
                Image avatar = FindSceneComponent<Image>(row, "FriendRowAvatarBackground" + index);
                if (templateAvatar != null && avatar != null)
                {
                    AvatarImageState templateState = GetAvatarImageState(templateAvatar);
                    avatar.sprite = templateState.defaultSprite;
                    avatar.color = templateState.defaultColor;
                    avatar.preserveAspect = templateState.defaultPreserveAspect;
                }
                Text templatePresence = FindSceneComponent<Text>(template, "FriendRowState0");
                Text rowPresence = FindSceneComponent<Text>(row, "FriendRowState" + index);
                if (templatePresence != null && rowPresence != null
                    && friendPresenceSceneColors.TryGetValue(templatePresence, out Color sceneColor))
                {
                    rowPresence.color = sceneColor;
                }
            }
        }
        return rows;
    }

    private void RefreshFriendListScroll(Transform root)
    {
        ScrollRect scroll = FindSceneComponent<ScrollRect>(root, "FriendsListViewport");
        if (scroll == null || scroll.content == null || scroll.viewport == null) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        float overflow = Mathf.Max(0f, scroll.content.rect.height - scroll.viewport.rect.height);
        scroll.horizontal = false;
        scroll.vertical = overflow > 0.5f;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        Vector2 position = scroll.content.anchoredPosition;
        float nextY = resetFriendListScroll || !scroll.vertical ? 0f : Mathf.Clamp(position.y, 0f, overflow);
        if (resetFriendListScroll || !Mathf.Approximately(position.y, nextY) || !scroll.vertical) scroll.StopMovement();
        scroll.content.anchoredPosition = new Vector2(position.x, nextY);
        resetFriendListScroll = false;
    }

    private void ScrollToSelectedFriend(Transform root)
    {
        ScrollRect scroll = FindSceneComponent<ScrollRect>(root, "FriendsListViewport");
        if (scroll == null || scroll.content == null || scroll.viewport == null) return;
        foreach (KeyValuePair<Transform, int> binding in v37FriendRowPlayerIds)
        {
            RectTransform row = binding.Key as RectTransform;
            if (binding.Value != v37SelectedFriendPlayerId || row == null || !row.gameObject.activeSelf) continue;
            float rowTop = scroll.content.InverseTransformPoint(row.TransformPoint(new Vector3(0f, row.rect.yMax, 0f))).y;
            float overflow = Mathf.Max(0f, scroll.content.rect.height - scroll.viewport.rect.height);
            scroll.StopMovement();
            scroll.content.anchoredPosition = new Vector2(scroll.content.anchoredPosition.x,
                Mathf.Clamp(scroll.content.rect.yMax - rowTop, 0f, overflow));
            break;
        }
    }

    private void ReleaseV37FriendRow(Transform row, int rowIndex)
    {
        BindSceneButton(row.GetComponent<Button>(), null);
        v37FriendRowPlayerIds.Remove(row);
        Image avatar = FindSceneComponent<Image>(row, "FriendRowAvatarBackground" + rowIndex);
        if (avatar != null && avatarImageStates.TryGetValue(avatar, out AvatarImageState state))
        {
            state.requestKey = string.Empty;
        }
        row.gameObject.SetActive(false);
    }


    private void RenderV37Row(
        Transform row,
        int rowIndex,
        bool selected,
        int playerId,
        string nickname,
        string avatarUrl,
        string avatarCode,
        long avatarVersion,
        int level,
        string state,
        Color stateColor,
        bool offline,
        bool interactionLocked,
        Action onSelected)
    {
        Image avatar = FindSceneComponent<Image>(row, "FriendRowAvatarBackground" + rowIndex);
        if (avatar != null)
        {
            Text avatarInitial = FindSceneComponent<Text>(row, "FriendRowAvatar" + rowIndex);
            if (!v37FriendRowPlayerIds.TryGetValue(row, out int boundPlayerId) || boundPlayerId != playerId)
            {
                ApplyBundledDefaultAvatar(avatar, avatarInitial, GetAvatarImageState(avatar));
            }
            ApplyAvatarImage(avatar, avatarInitial, playerId, avatarUrl, avatarCode, avatarVersion);
        }
        v37FriendRowPlayerIds[row] = playerId;

        Image mark = FindSceneComponent<Image>(row, "FriendRowOnlineMark" + rowIndex);
        if (mark != null)
        {
            mark.color = stateColor;
        }

        SetSceneText(row, "FriendRowAvatar" + rowIndex, GetNicknameInitial(nickname));
        SetSceneText(row, "FriendRowName" + rowIndex, string.IsNullOrWhiteSpace(nickname) ? "玩家 " + playerId : nickname);
        Text idLabel = FindSceneComponent<Text>(row, "FriendRowId" + rowIndex);
        string levelText = "Lv." + Mathf.Max(1, level);
        if (idLabel != null) idLabel.text = "ID " + playerId;
        SetSceneText(row, "FriendRowMeta" + rowIndex, idLabel != null ? levelText : levelText + "\nID " + playerId);
        SetSceneText(row, "FriendRowState" + rowIndex, state);
        SetFriendPresenceTextColor(FindSceneComponent<Text>(row, "FriendRowState" + rowIndex), offline);
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
            friend.avatarUrl,
            friend.avatarCode,
            friend.avatarVersion,
            GetV38FriendPresenceLabel(friend),
            BuildV38FriendPresenceDetail(friend),
            friend.dailyStaminaGiftReceived ? "今日已收到该好友体力" : "今日尚未收到该好友体力");
        SetFriendPresenceTextColor(FindSceneComponent<Text>(root, "FriendDetailOnline"),
            NormalizeV38OnlineStatus(friend.onlineStatus) == "OFFLINE");
        bool giftedToday = friend.dailyStaminaGiftSent;
        ServerDailyGiftStatus staminaGiftStatus = GetServerStaminaGiftStatus();
        bool giftEnabled = staminaGiftStatus != null && staminaGiftStatus.enabled && !giftedToday && !interactionLocked;
        string giftHint = BuildServerStaminaGiftHint(friend)
            + (friend.dailyStaminaGiftReceived ? "；今日已收体力" : "；今日未收体力");
        SetSceneText(root, "FriendGiftHint", giftHint);
        SetSceneObjectActive(root, "FriendsGiftButton", true);
        SetSceneObjectActive(root, "FriendsDailyGiftButton", false);
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
        ServerDailyGiftStatus staminaGiftStatus = GetServerStaminaGiftStatus();
        if (staminaGiftStatus == null || !staminaGiftStatus.enabled)
        {
            return "好友体力赠送当前未开启";
        }

        if (friend != null && friend.dailyStaminaGiftSent)
        {
            return "今天已经向该好友赠送过体力";
        }

        return "今日可赠送 " + Math.Max(0L, staminaGiftStatus.quantity) + " 点体力";
    }

    private ServerDailyGiftStatus GetServerStaminaGiftStatus()
    {
        return serverDailyStaminaGiftStatus;
    }



    private void SetV37DetailBase(
        Transform root,
        int playerId,
        string nickname,
        int level,
        string avatarUrl,
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
            ApplyAvatarImage(
                avatar,
                FindSceneComponent<Text>(root, "FriendDetailAvatar"),
                playerId,
                avatarUrl,
                avatarCode,
                avatarVersion);
        }

        SetSceneText(root, "FriendDetailAvatar", GetNicknameInitial(nickname));
        SetSceneText(root, "FriendDetailName", string.IsNullOrWhiteSpace(nickname) ? "玩家 " + playerId : nickname);
        SetSceneText(root, "FriendDetailId", "玩家ID  " + playerId);
        Button copyIdButton = FindSceneComponent<Button>(root, "FriendCopyIdButton");
        if (copyIdButton != null)
        {
            copyIdButton.interactable = playerId > 0;
            BindSceneButton(copyIdButton, () =>
            {
                if (playerId <= 0) return;
                GUIUtility.systemCopyBuffer = playerId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                SetSceneText(root, "FriendsStatus", "好友ID已复制");
            });
        }
        SetSceneText(root, "FriendDetailLevel", "领主等级  Lv." + Mathf.Max(1, level));
        SetSceneText(root, "FriendDetailOnline", title);
        SetSceneText(root, "FriendGiftHint", hint);
    }

    private void SetV37DetailEmpty(Transform root, string title, string hint)
    {
        SetSceneObjectActive(root, "FriendDetailEmpty", true);
        SetSceneObjectActive(root, "FriendDetailContent", false);
        SetSceneText(root, "FriendDetailEmptyTitle", title);
        SetSceneText(root, "FriendDetailEmptyHint", hint);
    }


    private void ChangeV37FriendPanelMode(Transform root, ServerFriendPanelMode mode)
    {
        if (root == null || v37MutationRunning)
        {
            return;
        }

        if (mode == ServerFriendPanelMode.Friends)
        {
            CloseFriendRequestsDialog(root);
            return;
        }
        EnsureFriendRequestsDialog(root);
        if (FindSceneTransform(root, "FriendRequestsOverlay") == null)
        {
            Debug.LogWarning("[Friends] FriendRequestsOverlay is missing.", this);
            SetSceneText(root, "FriendsStatus", "申请窗口组件缺失");
            return;
        }
        if (v37Loading)
        {
            v37PanelRevision++;
            v37Loading = false;
        }
        serverFriendPanelMode = mode;
        friendSearchDialogOpen = false;
        friendSearchDialogRevision++;
        friendSearchLoading = false;
        resetFriendRequestScroll = true;
        v37SelectedRequestId = 0;
        v37FriendSearchResult = null;
        friendDeleteConfirmation = false;
        StartCoroutine(RefreshServerFriendRequestState(root, mode == ServerFriendPanelMode.Friends, "已切换到" + GetV37ModeLabel()));
    }

    private void HandleV37SearchOrRequest(Transform root)
    {
        if (root == null || !friendSearchDialogOpen || friendSearchLoading || v37MutationRunning)
        {
            return;
        }

        InputField input = FindSceneComponent<InputField>(root, "FriendSearchPlayerIdInput");
        string value = input == null ? string.Empty : input.text.Trim();
        if (!int.TryParse(value, out int playerId) || playerId <= 0)
        {
            SetFriendSearchStatus(root, "请输入正确的玩家 ID");
            return;
        }

        if (playerId == GameSession.PlayerId)
        {
            SetFriendSearchStatus(root, "不能向自己发送好友申请");
            return;
        }

        if (v37FriendSearchResult == null || v37FriendSearchPlayerId != playerId)
        {
            StartCoroutine(SearchV37Friend(root, playerId));
            return;
        }

        if (v37Loading)
        {
            SetFriendSearchStatus(root, "好友数据正在同步，请稍后操作");
            return;
        }

        switch (v37FriendSearchResult.relationshipStatus)
        {
            case "NONE":
                if (!IsV37FriendRequestCreationAvailable())
                {
                    SetFriendSearchStatus(root, "好友申请当前不可用");
                    break;
                }
                StartCoroutine(CreateV37FriendRequest(root, playerId));
                break;
            case "INCOMING_PENDING":
                StartCoroutine(OpenSearchedFriendRequest(root, ServerFriendPanelMode.IncomingRequests, v37FriendSearchResult.pendingRequestId));
                break;
            case "OUTGOING_PENDING":
                StartCoroutine(OpenSearchedFriendRequest(root, ServerFriendPanelMode.OutgoingRequests, v37FriendSearchResult.pendingRequestId));
                break;
            case "FRIEND":
                friendSearchDialogOpen = false;
                serverFriendPanelMode = ServerFriendPanelMode.Friends;
                friendsSearchQuery = string.Empty;
                v37FriendSearchResult = null;
                v37SelectedFriendPlayerId = playerId;
                RefreshServerFriendRequestPanel(root, "已是正式好友");
                ScrollToSelectedFriend(root);
                break;
            default:
                SetFriendSearchStatus(root, "当前玩家不能发送好友申请");
                break;
        }
    }

    private void SetFriendSearchStatus(Transform root, string message)
    {
        friendSearchStatus = message;
        RefreshServerFriendRequestPanel(root, null);
    }

    private IEnumerator OpenSearchedFriendRequest(Transform root, ServerFriendPanelMode mode, long requestId)
    {
        EnsureFriendRequestsDialog(root);
        serverFriendPanelMode = mode;
        friendSearchDialogOpen = false;
        friendSearchDialogRevision++;
        v37FriendSearchResult = null;
        v37SelectedRequestId = requestId;
        resetFriendRequestScroll = true;
        int revision = v37PanelRevision;
        yield return RefreshServerFriendRequestState(root, false, "已打开对应申请");
        if (revision != v37PanelRevision || root == null || !root.gameObject.activeInHierarchy) yield break;
        RefreshServerFriendRequestPanel(root, null);
        ScrollToFriendRequest(root, requestId);
    }

    private IEnumerator SearchV37Friend(Transform root, int playerId)
    {
        int revision = v37PanelRevision;
        int dialogRevision = friendSearchDialogRevision;
        friendSearchLoading = true;
        SetFriendSearchStatus(root, "正在精确搜索玩家...");
        UnityWebRequest request = CreateAuthorizedGet("/friends/search?playerId=" + playerId);
        if (request == null)
        {
            friendSearchLoading = false;
            SetFriendSearchStatus(root, requestCreationError);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (revision != v37PanelRevision || root == null || !root.gameObject.activeInHierarchy)
            {
                yield break;
            }
            if (HandleUnauthorizedResponse(request))
            {
                friendSearchLoading = false;
                yield break;
            }

            if (!friendSearchDialogOpen || dialogRevision != friendSearchDialogRevision)
            {
                yield break;
            }
            ServerFriendSearchApiResponse response = ParseJson<ServerFriendSearchApiResponse>(request.downloadHandler.text);
            friendSearchLoading = false;
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                v37FriendSearchResult = null;
                v37FriendSearchPlayerId = 0;
                SetFriendSearchStatus(root, GetV37RequestError(request, response, "玩家搜索失败"));
                yield break;
            }

            v37FriendSearchResult = response.data;
            v37FriendSearchPlayerId = response.data.playerId;
            SetFriendSearchStatus(root, "已找到玩家，请确认关系状态后继续操作");
        }
    }

    private IEnumerator CreateV37FriendRequest(Transform root, int recipientPlayerId)
    {
        int revision = v37PanelRevision;
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
            SetFriendSearchStatus(root, error);
            yield break;
        }

        ServerFriendRequestResponse created = null;
        using (request)
        {
            yield return request.SendWebRequest();
            if (revision != v37PanelRevision || root == null || !root.gameObject.activeInHierarchy)
            {
                yield break;
            }
            if (HandleUnauthorizedResponse(request))
            {
                v37MutationRunning = false;
                yield break;
            }

            ServerFriendRequestMutationApiResponse response = ParseJson<ServerFriendRequestMutationApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                v37MutationRunning = false;
                friendSearchStatus = GetV37RequestError(request, response, "好友申请提交失败");
                HandleV37FriendRequestFailure(root, request, response, "好友申请提交失败");
                yield break;
            }
            created = response.data;
        }

        v37MutationRunning = false;
        friendSearchDialogOpen = false;
        v37FriendSearchResult = null;
        v37FriendSearchPlayerId = 0;
        if (created != null)
        {
            serverFriendPanelMode = string.Equals(created.direction, "INCOMING", StringComparison.OrdinalIgnoreCase)
                ? ServerFriendPanelMode.IncomingRequests
                : ServerFriendPanelMode.OutgoingRequests;
            v37SelectedRequestId = created.requestId;
        }
        InputField input = FindSceneComponent<InputField>(root, "FriendSearchPlayerIdInput");
        if (input != null)
        {
            input.SetTextWithoutNotify(string.Empty);
        }
        EnsureFriendRequestsDialog(root);
        resetFriendRequestScroll = true;
        yield return RefreshServerFriendRequestState(root, false,
            serverFriendPanelMode == ServerFriendPanelMode.IncomingRequests
                ? "对方已向你发出申请，请选择同意或拒绝"
                : "好友申请已提交，等待对方同意");
    }

    private IEnumerator MutateV37FriendRequest(Transform root, long requestId, string action)
    {
        int revision = v37PanelRevision;
        if (v37Loading || v37MutationRunning || root == null || requestId <= 0)
        {
            yield break;
        }

        ServerFriendRequestResponse selected = FindV37Request(requestId);
        bool incoming = serverFriendPanelMode == ServerFriendPanelMode.IncomingRequests;
        bool allowed = IsPendingFriendRequest(selected) && (incoming
            ? (action == "accept" && selected.canAccept) || (action == "reject" && selected.canReject)
            : action == "cancel" && selected.canCancel);
        if (!allowed)
        {
            RefreshServerFriendRequestPanel(root, "申请状态已变化或无操作权限，请重新打开申请列表");
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
            if (revision != v37PanelRevision || root == null || !root.gameObject.activeInHierarchy)
            {
                yield break;
            }
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
        int revision = v37PanelRevision;
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
            if (revision != v37PanelRevision || root == null || !root.gameObject.activeInHierarchy)
            {
                yield break;
            }
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
        int revision = v37PanelRevision;
        ServerFriendResponse friend = FindV37Friend(friendPlayerId);
        ServerDailyGiftStatus status = GetServerStaminaGiftStatus();
        if (v37Loading || v37MutationRunning || root == null || friend == null
            || status == null || !status.enabled || friend.dailyStaminaGiftSent)
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
            if (revision != v37PanelRevision || root == null || !root.gameObject.activeInHierarchy)
            {
                yield break;
            }
            if (HandleUnauthorizedResponse(request))
            {
                v37MutationRunning = false;
                yield break;
            }

            ServerDailyGiftApiResponse response = ParseJson<ServerDailyGiftApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                if (response != null && (response.code == 6721 || response.code == 6722))
                {
                    status.enabled = false;
                }
                v37MutationRunning = false;
                string failure = GetV37RequestError(request, response, "赠礼发送失败");
                if (response != null && response.code == 6702)
                {
                    v37Friends.Remove(friend);
                    yield return RefreshServerFriendRequestState(root, true, failure);
                }
                else
                {
                    RefreshServerFriendRequestPanel(root, failure);
                }
                yield break;
            }
            gift = response.data;
        }
        friend.dailyStaminaGiftSent = true;
        if (string.Equals(status.periodKey, gift.periodKey, StringComparison.Ordinal))
        {
            if (!gift.replayed) status.sentCount++;
        }
        else
        {
            status.enabled = false;
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
            result.Add(request);
        }
        return result;
    }

    private bool MatchesV37Filter(string playerId, string nickname)
    {
        return string.IsNullOrEmpty(friendsSearchQuery)
            || (!string.IsNullOrEmpty(playerId) && playerId.IndexOf(friendsSearchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
            || (!string.IsNullOrEmpty(nickname) && nickname.IndexOf(friendsSearchQuery, StringComparison.OrdinalIgnoreCase) >= 0);
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

    private void SetFriendPresenceTextColor(Text label, bool offline)
    {
        if (label == null) return;
        if (!friendPresenceSceneColors.TryGetValue(label, out Color sceneColor))
        {
            sceneColor = label.color;
            friendPresenceSceneColors.Add(label, sceneColor);
        }
        label.color = offline ? new Color32(0x8E, 0x86, 0x7B, 0xFF) : sceneColor;
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
            case "NONE": return "可发送申请";
            default: return "关系状态未知";
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
        public ServerDailyGiftStatus dailyStaminaGift;
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
        public string avatarUrl;
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
        public string requesterAvatarUrl;
        public string requesterAvatarCode;
        public long requesterAvatarVersion;
        public string recipientAvatarUrl;
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

    [Serializable]
    private class ServerFriendDailyGiftApiResponse : BasicApiResponse
    {
        public ServerFriendDailyGiftResponse data;
    }

    [Serializable]
    private class ServerFriendDailyGiftResponse
    {
        public long recordId;
        public long senderPlayerId;
        public long receiverPlayerId;
        public string periodKey;
        public long itemId;
        public string itemKey;
        public long quantity;
        public long receiverBalanceAfter;
        public long configVersion;
        public string sentTime;
        public bool replayed;
    }
#pragma warning restore 0649
}
