using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public partial class HomePageUI
{
    private const string DefaultAvatarResourcePath = "Avatars/avatar_001";
    private const string PlayerAvatarStateKeyPrefix = "PlayerAvatarState.";

    private static readonly Dictionary<string, string> AvatarResourcePathByCode =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "AVATAR_001", "Avatars/avatar_001" },
            { "AVATAR_002", "Avatars/avatar_002" },
            { "AVATAR_003", "Avatars/avatar_003" }
        };

    private readonly Dictionary<string, Sprite> avatarSpriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
    private readonly Dictionary<long, PlayerAvatarState> avatarByPlayerId = new Dictionary<long, PlayerAvatarState>();
    private readonly Dictionary<Image, AvatarImageState> avatarImageStates = new Dictionary<Image, AvatarImageState>();
    private readonly Dictionary<string, string> avatarResourceKeyByCode = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly List<AvatarPresetData> avatarPresets = new List<AvatarPresetData>();
    private GameObject avatarPresetPickerObject;
    private bool avatarMutationRunning;
    private bool avatarPresetsLoaded;
    private bool avatarPresetsRequestRunning;
    private bool avatarSelectionEnabled = true;
    private bool avatarPickerPending;
    private bool avatarCatalogRefreshPending;
    private bool avatarDisplayForcedDefault;

    private void ConfigureProfileAvatarControls(Transform root, bool isAuthenticated, string nickname)
    {
        Image avatar = FindSceneComponent<Image>(root, "ProfileAvatarBackground");
        if (avatar == null)
        {
            Debug.LogWarning("[Avatar] ProfileAvatarBackground was not found.", this);
            return;
        }

        Button avatarButton = avatar.GetComponent<Button>();
        if (avatarButton == null)
        {
            avatarButton = avatar.gameObject.AddComponent<Button>();
            avatarButton.targetGraphic = avatar;
        }
        avatarButton.interactable = isAuthenticated && avatarSelectionEnabled && !avatarMutationRunning;
        BindSceneButton(avatarButton, BeginAvatarSelection);

        Transform parent = avatar.transform.parent;
        Button chooseButton = FindSceneComponent<Button>(parent, "ProfileChooseAvatarButton");
        if (chooseButton == null)
        {
            chooseButton = CreateButton(parent, "ProfileChooseAvatarButton", "选择头像", new Color(0.28f, 0.55f, 0.76f, 1f), Color.white);
            SetRect(chooseButton.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(260f, -40f), new Vector2(100f, 40f));
            ShrinkButtonLabel(chooseButton, 15);
        }

        Button resetButton = FindSceneComponent<Button>(parent, "ProfileResetAvatarButton");
        if (resetButton == null)
        {
            resetButton = CreateButton(parent, "ProfileResetAvatarButton", "恢复默认", new Color(0.65f, 0.45f, 0.27f, 1f), Color.white);
            SetRect(resetButton.GetComponent<RectTransform>(), TopLeftAnchor(), TopLeftAnchor(), TopLeftAnchor(), new Vector2(260f, -88f), new Vector2(100f, 40f));
            ShrinkButtonLabel(resetButton, 15);
        }

        bool canChoose = isAuthenticated && avatarSelectionEnabled && !avatarMutationRunning;
        chooseButton.gameObject.SetActive(canChoose);
        chooseButton.interactable = canChoose;
        resetButton.gameObject.SetActive(isAuthenticated);
        resetButton.interactable = isAuthenticated && !avatarMutationRunning && !string.IsNullOrEmpty(GetCurrentAvatarCode());
        SetSceneButtonText(chooseButton, avatarMutationRunning ? "处理中" : "选择头像");
        BindSceneButton(chooseButton, BeginAvatarSelection);
        BindSceneButton(resetButton, () => StartCoroutine(ResetCurrentAvatar()));

        Component fallbackText = FindSceneComponent<TMP_Text>(root, "ProfileAvatarInitial");
        if (avatarDisplayForcedDefault)
        {
            ApplyDefaultAvatarImage(avatar, fallbackText);
        }
        else
        {
            ApplyAvatarImage(avatar, fallbackText, GameSession.PlayerId, GetCurrentAvatarCode(), GetCurrentAvatarVersion());
        }
    }

    private void BeginAvatarSelection()
    {
        if (avatarMutationRunning || !HasAuthSession())
        {
            return;
        }

        if (!avatarPresetsLoaded)
        {
            avatarPickerPending = true;
            if (!avatarPresetsRequestRunning)
            {
                StartCoroutine(RefreshAvatarPresets());
            }
            SetStatus("正在读取头像目录...");
            return;
        }

        ShowPresetAvatarPicker();
    }

    private IEnumerator RefreshAvatarPresets()
    {
        if (avatarPresetsRequestRunning || !HasAuthSession())
        {
            yield break;
        }

        avatarPresetsRequestRunning = true;
        if (!ApiRequestUtility.TryCreateGet("/user/avatar/presets", true, out UnityWebRequest request, out string error))
        {
            avatarPresetsRequestRunning = false;
            avatarPresetsLoaded = false;
            avatarPickerPending = false;
            avatarCatalogRefreshPending = false;
            MarkAvatarUnavailableAndShowDefault();
            Debug.LogWarning("[Avatar] Could not create preset directory request: " + error, this);
            SetStatus("头像目录暂时不可用，将使用默认头像");
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            AvatarPresetsApiResponse response = ParseJson<AvatarPresetsApiResponse>(request.downloadHandler.text);
            bool success = request.result == UnityWebRequest.Result.Success
                && response != null
                && response.code == 200
                && response.data != null;
            if (!success)
            {
                avatarPresetsRequestRunning = false;
                avatarPresetsLoaded = false;
                avatarPickerPending = false;
                avatarCatalogRefreshPending = false;
                if (ApiRequestUtility.IsUnauthorized(request))
                {
                    HandleUnauthorizedResponse(request);
                }
                MarkAvatarUnavailableAndShowDefault();
                Debug.LogWarning("[Avatar] Preset directory request failed: " + GetAvatarRequestError(request, response, "头像目录读取失败"), this);
                SetStatus("头像目录暂时不可用，将使用默认头像");
                yield break;
            }

            avatarPresets.Clear();
            avatarResourceKeyByCode.Clear();
            avatarSelectionEnabled = false;
            for (int i = 0; i < response.data.Length; i++)
            {
                AvatarPresetData preset = response.data[i];
                if (preset == null || string.IsNullOrEmpty(preset.avatarCode))
                {
                    continue;
                }

                avatarPresets.Add(preset);
                if (!string.IsNullOrWhiteSpace(preset.resourceKey))
                {
                    avatarResourceKeyByCode[preset.avatarCode] = preset.resourceKey.Trim();
                }
                avatarSelectionEnabled |= CanSelectAvatarPreset(preset);
            }
            avatarPresetsLoaded = true;
            avatarPresetsRequestRunning = false;
            avatarDisplayForcedDefault = false;
        }

        RefreshCurrentAvatarVisuals();
        if (avatarPickerPending)
        {
            avatarPickerPending = false;
            ShowPresetAvatarPicker();
        }

        if (avatarCatalogRefreshPending)
        {
            avatarCatalogRefreshPending = false;
            StartCoroutine(RefreshAvatarPresets());
        }
    }

    private void ShowPresetAvatarPicker()
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.AccountProfile);
        if (panel == null || panel.PanelRoot == null)
        {
            Debug.LogWarning("[Avatar] Account profile panel is unavailable; cannot show preset avatars.", this);
            SetStatus("个人信息面板不可用");
            return;
        }

        if (!avatarSelectionEnabled)
        {
            SetStatus("头像选择功能当前已关闭");
            return;
        }

        if (avatarPresets.Count == 0)
        {
            SetStatus("服务器没有可用头像");
            return;
        }

        avatarPresetPickerObject = EnsurePresetAvatarPicker(panel.PanelRoot);
        if (avatarPresetPickerObject == null)
        {
            SetStatus("头像选择面板创建失败");
            return;
        }

        BuildPresetAvatarOptions(avatarPresetPickerObject.transform);
        avatarPresetPickerObject.SetActive(true);
        avatarPresetPickerObject.transform.SetAsLastSibling();
        SetStatus("请选择一个预置头像");
    }

    private GameObject EnsurePresetAvatarPicker(Transform profileRoot)
    {
        if (profileRoot == null)
        {
            return null;
        }

        if (avatarPresetPickerObject != null)
        {
            return avatarPresetPickerObject;
        }

        GameObject overlay = new GameObject("ProfileAvatarPickerPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(profileRoot, false);
        Stretch(overlay.GetComponent<RectTransform>());

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0.05f, 0.035f, 0.02f, 0.62f);
        overlayImage.raycastTarget = true;
        Button overlayButton = overlay.AddComponent<Button>();
        overlayButton.targetGraphic = overlayImage;
        BindSceneButton(overlayButton, HidePresetAvatarPicker);

        Image card = CreateImage("ProfileAvatarPickerCard", overlay.transform, new Color(1f, 0.94f, 0.76f, 1f));
        card.raycastTarget = true;
        SetRect(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 410f));

        TMP_Text title = CreatePresetAvatarText("ProfileAvatarPickerTitle", card.transform, "选择预置头像", 26f, new Color(0.30f, 0.16f, 0.08f, 1f), TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(-100f, 42f));

        Image closeImage = CreateImage("ProfileAvatarPickerCloseButton", card.transform, new Color(0.78f, 0.30f, 0.22f, 1f));
        closeImage.raycastTarget = true;
        SetRect(closeImage.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(42f, 42f));
        Button closeButton = closeImage.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        BindSceneButton(closeButton, HidePresetAvatarPicker);
        TMP_Text closeText = CreatePresetAvatarText("Text", closeImage.transform, "X", 22f, Color.white, TextAlignmentOptions.Center);
        Stretch(closeText.rectTransform);

        GameObject gridObject = new GameObject("ProfileAvatarPickerGrid", typeof(RectTransform));
        gridObject.transform.SetParent(card.transform, false);
        SetRect(gridObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -16f), new Vector2(-56f, -98f));
        GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(100f, 100f);
        grid.spacing = new Vector2(18f, 18f);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        grid.childAlignment = TextAnchor.UpperCenter;
        return overlay;
    }

    private void BuildPresetAvatarOptions(Transform pickerRoot)
    {
        Transform grid = FindSceneTransform(pickerRoot, "ProfileAvatarPickerGrid");
        if (grid == null)
        {
            Debug.LogWarning("[Avatar] Preset avatar grid is missing.", pickerRoot);
            return;
        }

        for (int i = grid.childCount - 1; i >= 0; i--)
        {
            Destroy(grid.GetChild(i).gameObject);
        }

        int optionIndex = 0;
        for (int i = 0; i < avatarPresets.Count; i++)
        {
            AvatarPresetData preset = avatarPresets[i];
            if (!CanSelectAvatarPreset(preset))
            {
                continue;
            }

            Sprite sprite = LoadAvatarSprite(preset.avatarCode, preset.resourceKey);
            if (sprite == null)
            {
                Debug.LogWarning("[Avatar] No local resource mapping exists for " + preset.avatarCode + ".", this);
                continue;
            }

            Image option = CreateImage("ProfileAvatarPreset" + optionIndex, grid, Color.white);
            option.sprite = sprite;
            option.preserveAspect = true;
            option.raycastTarget = true;
            Button optionButton = option.gameObject.AddComponent<Button>();
            optionButton.targetGraphic = option;
            string avatarCode = preset.avatarCode;
            BindSceneButton(optionButton, () => SelectPresetAvatar(avatarCode));
            optionIndex++;
        }
    }

    private TMP_Text CreatePresetAvatarText(string name, Transform parent, string value, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset resolvedFont = RuntimeFontAssetProvider.Resolve();
        text.font = resolvedFont != null ? resolvedFont : TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Normal;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private void HidePresetAvatarPicker()
    {
        if (avatarPresetPickerObject != null)
        {
            avatarPresetPickerObject.SetActive(false);
        }
    }

    private void SelectPresetAvatar(string avatarCode)
    {
        if (avatarMutationRunning || string.IsNullOrEmpty(avatarCode) || !HasAuthSession())
        {
            return;
        }

        AvatarPresetData selected = FindAvatarPreset(avatarCode);
        if (selected == null || !selected.enabled)
        {
            Debug.LogWarning("[Avatar] Selected avatar is not enabled: " + avatarCode, this);
            SetStatus("头像不可用，请刷新头像目录");
            return;
        }

        if (!IsAvatarPresetClientVersionSupported(selected))
        {
            ShowAvatarClientUpdateRequired();
            return;
        }

        if (LoadAvatarSprite(selected.avatarCode, selected.resourceKey) == null)
        {
            Debug.LogWarning("[Avatar] Selected avatar resource could not be loaded: " + avatarCode, this);
            SetStatus("Avatar resource unavailable; using the default avatar.");
            return;
        }

        avatarMutationRunning = true;
        HidePresetAvatarPicker();
        RefreshProfileAvatarPanel("正在设置头像...");
        StartCoroutine(SetPresetAvatar(avatarCode));
    }

    private IEnumerator SetPresetAvatar(string avatarCode)
    {
        AvatarPresetRequest payload = new AvatarPresetRequest { avatarCode = avatarCode };
        if (!ApiRequestUtility.TryCreateJsonRequest(
                "/user/avatar/preset",
                UnityWebRequest.kHttpVerbPUT,
                JsonUtility.ToJson(payload),
                true,
                out UnityWebRequest request,
                out string error))
        {
            avatarMutationRunning = false;
            RefreshProfileAvatarPanel(error);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            AvatarApiResponse response = ParseJson<AvatarApiResponse>(request.downloadHandler.text);
            if (ApiRequestUtility.IsUnauthorized(request))
            {
                avatarMutationRunning = false;
                HandleUnauthorizedResponse(request);
                yield break;
            }

            bool success = request.result == UnityWebRequest.Result.Success
                && response != null
                && response.code == 200
                && response.data != null;
            if (!success)
            {
                avatarMutationRunning = false;
                if ((response != null && response.code == 7307) || request.responseCode == 409)
                {
                    HomePageScenePanel profilePanel = GetRegisteredScenePanel(HomePageScenePanelType.AccountProfile);
                    if (profilePanel != null && profilePanel.PanelRoot != null)
                    {
                        StartCoroutine(RefreshCurrentUserProfile(profilePanel.PanelRoot));
                    }
                }
                if ((response != null && response.code == 400) || request.responseCode == 400)
                {
                    avatarPresetsLoaded = false;
                    if (!avatarPresetsRequestRunning)
                    {
                        StartCoroutine(RefreshAvatarPresets());
                    }
                }
                if ((response != null && response.code == 7310) || request.responseCode == 426)
                {
                    ShowAvatarClientUpdateRequired();
                }
                if ((response != null && response.code == 7308) || request.responseCode == 503)
                {
                    avatarSelectionEnabled = false;
                    MarkAvatarUnavailableAndShowDefault();
                }
                RefreshProfileAvatarPanel(GetAvatarRequestError(request, response, "头像设置失败"));
                yield break;
            }

            SaveCurrentAvatarState(
                response.data.avatarCode,
                response.data.avatarSource,
                response.data.avatarVersion,
                response.data.avatarUpdatedTime);
            avatarDisplayForcedDefault = false;
        }

        avatarMutationRunning = false;
        RefreshCurrentAvatarVisuals();
        RefreshProfileAvatarPanel("头像更新成功");
    }

    private IEnumerator ResetCurrentAvatar()
    {
        if (avatarMutationRunning || !HasAuthSession())
        {
            yield break;
        }

        avatarMutationRunning = true;
        RefreshProfileAvatarPanel("正在恢复默认头像...");
        if (!ApiRequestUtility.TryCreateJsonRequest(
                "/user/avatar",
                UnityWebRequest.kHttpVerbDELETE,
                string.Empty,
                true,
                out UnityWebRequest request,
                out string error))
        {
            avatarMutationRunning = false;
            RefreshProfileAvatarPanel(error);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            AvatarApiResponse response = ParseJson<AvatarApiResponse>(request.downloadHandler.text);
            if (ApiRequestUtility.IsUnauthorized(request))
            {
                avatarMutationRunning = false;
                HandleUnauthorizedResponse(request);
                yield break;
            }

            bool success = request.result == UnityWebRequest.Result.Success
                && response != null
                && response.code == 200
                && response.data != null;
            if (!success)
            {
                avatarMutationRunning = false;
                RefreshProfileAvatarPanel(GetAvatarRequestError(request, response, "恢复默认头像失败"));
                yield break;
            }

            SaveCurrentAvatarState(
                response.data.avatarCode,
                response.data.avatarSource,
                response.data.avatarVersion,
                response.data.avatarUpdatedTime);
            avatarDisplayForcedDefault = false;
        }

        avatarMutationRunning = false;
        RefreshCurrentAvatarVisuals();
        RefreshProfileAvatarPanel("已恢复默认头像");
    }

    private void RefreshProfileAvatarPanel(string status)
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.AccountProfile);
        if (panel != null && panel.PanelRoot != null && panel.gameObject.activeInHierarchy)
        {
            RefreshAccountProfileScenePanel(panel.PanelRoot, status);
        }
        SetStatus(status);
    }

    private void SaveCurrentAvatarState(string avatarCode, string avatarSource, long avatarVersion, string avatarUpdatedTime)
    {
        SavePlayerAvatarState(GameSession.PlayerId, avatarCode, avatarSource, avatarVersion, avatarUpdatedTime, false);
    }

    private bool SavePlayerAvatarState(long playerId, string avatarCode, string avatarSource, long avatarVersion, string avatarUpdatedTime, bool onlyIfNewer)
    {
        if (playerId <= 0)
        {
            Debug.LogWarning("[Avatar] Cannot cache avatar state without a valid player ID.", this);
            return false;
        }

        PlayerAvatarState current = GetPlayerAvatarState(playerId);
        long normalizedVersion = Math.Max(0L, avatarVersion);
        if (onlyIfNewer && normalizedVersion <= current.avatarVersion)
        {
            return false;
        }
        if (!onlyIfNewer && normalizedVersion < current.avatarVersion)
        {
            Debug.LogWarning("[Avatar] Ignored stale avatar response version " + normalizedVersion + ".", this);
            return false;
        }

        current.avatarCode = NormalizeAvatarCode(avatarCode);
        current.avatarSource = string.IsNullOrEmpty(avatarSource)
            ? (string.IsNullOrEmpty(current.avatarCode) ? "DEFAULT" : "PRESET")
            : avatarSource;
        current.avatarVersion = normalizedVersion;
        current.avatarUpdatedTime = avatarUpdatedTime ?? string.Empty;
        current.hasServerState = true;
        SavePlayerAvatarStateToPrefs(playerId, current);
        return true;
    }

    private PlayerAvatarState GetPlayerAvatarState(long playerId)
    {
        if (playerId <= 0)
        {
            return new PlayerAvatarState();
        }
        if (avatarByPlayerId.TryGetValue(playerId, out PlayerAvatarState cached))
        {
            return cached;
        }

        string keyPrefix = GetPlayerAvatarStateKeyPrefix(playerId);
        PlayerAvatarState state = new PlayerAvatarState
        {
            avatarCode = NormalizeAvatarCode(PlayerPrefs.GetString(keyPrefix + "Code", string.Empty)),
            avatarSource = PlayerPrefs.GetString(keyPrefix + "Source", string.Empty),
            avatarUpdatedTime = PlayerPrefs.GetString(keyPrefix + "UpdatedTime", string.Empty),
            hasServerState = PlayerPrefs.GetInt(keyPrefix + "HasState", 0) == 1
        };
        if (!long.TryParse(PlayerPrefs.GetString(keyPrefix + "Version", "0"), out state.avatarVersion))
        {
            state.avatarVersion = 0L;
        }
        state.avatarVersion = Math.Max(0L, state.avatarVersion);
        avatarByPlayerId[playerId] = state;
        return state;
    }

    private static string GetPlayerAvatarStateKeyPrefix(long playerId)
    {
        return PlayerAvatarStateKeyPrefix + playerId + ".";
    }

    private static void SavePlayerAvatarStateToPrefs(long playerId, PlayerAvatarState state)
    {
        string keyPrefix = GetPlayerAvatarStateKeyPrefix(playerId);
        PlayerPrefs.SetString(keyPrefix + "Code", state.avatarCode ?? string.Empty);
        PlayerPrefs.SetString(keyPrefix + "Source", state.avatarSource ?? string.Empty);
        PlayerPrefs.SetString(keyPrefix + "Version", state.avatarVersion.ToString());
        PlayerPrefs.SetString(keyPrefix + "UpdatedTime", state.avatarUpdatedTime ?? string.Empty);
        PlayerPrefs.SetInt(keyPrefix + "HasState", state.hasServerState ? 1 : 0);
        PlayerPrefs.Save();
    }

    private string GetCurrentAvatarCode()
    {
        return GetPlayerAvatarState(GameSession.PlayerId).avatarCode;
    }

    private long GetCurrentAvatarVersion()
    {
        return GetPlayerAvatarState(GameSession.PlayerId).avatarVersion;
    }

    private void RefreshCurrentAvatarVisuals()
    {
        if (mainPageTransform != null)
        {
            Transform headTransform = mainPageTransform.Find("BookAvatarFrame/BookAvatarPortrait/Image/BookAvatarHead");
            Image head = headTransform != null ? headTransform.GetComponent<Image>() : null;
            if (head != null)
            {
                if (avatarDisplayForcedDefault)
                {
                    ApplyDefaultAvatarImage(head, null);
                }
                else
                {
                    ApplyAvatarImage(head, null, GameSession.PlayerId, GetCurrentAvatarCode(), GetCurrentAvatarVersion());
                }
            }
            else
            {
                Debug.LogWarning("[Avatar] Home avatar target BookAvatarHead was not found.", this);
            }
        }

        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.AccountProfile);
        if (panel != null && panel.PanelRoot != null && panel.gameObject.activeInHierarchy)
        {
            ConfigureProfileAvatarControls(
                panel.PanelRoot,
                HasAuthSession(),
                PlayerPrefs.GetString("PlayerNickname", "玩家"));
        }
    }

    private IEnumerator RefreshV39FriendAvatarSnapshots(int revision)
    {
        if (v37Friends.Count == 0 || revision != v37PanelRevision)
        {
            yield break;
        }

        int count = Mathf.Min(100, v37Friends.Count);
        FriendAvatarBatchRequest payload = new FriendAvatarBatchRequest { playerIds = new int[count] };
        for (int i = 0; i < count; i++)
        {
            payload.playerIds[i] = v37Friends[i].playerId;
        }

        if (!TryCreateAuthorizedPost("/friends/avatars/batch", JsonUtility.ToJson(payload), out UnityWebRequest request, out string error))
        {
            Debug.LogWarning("[Avatar] Could not create friend avatar batch request: " + error, this);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (revision != v37PanelRevision || HandleUnauthorizedResponse(request))
            {
                yield break;
            }

            FriendAvatarBatchApiResponse response = ParseJson<FriendAvatarBatchApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                Debug.LogWarning("[Avatar] Friend avatar batch request failed: " + ApiRequestUtility.GetErrorMessage(request, "好友头像同步失败"), this);
                yield break;
            }

            for (int i = 0; i < response.data.Length; i++)
            {
                FriendAvatarSnapshot snapshot = response.data[i];
                ServerFriendResponse friend = snapshot == null ? null : FindV37Friend(snapshot.playerId);
                if (friend == null || snapshot.avatarVersion < friend.avatarVersion)
                {
                    continue;
                }

                friend.avatarCode = NormalizeAvatarCode(snapshot.avatarCode);
                friend.avatarVersion = Math.Max(0L, snapshot.avatarVersion);
                SavePlayerAvatarState(friend.playerId, friend.avatarCode, snapshot.avatarSource, friend.avatarVersion, string.Empty, true);
            }
        }
    }

    private void RefreshAvatarCatalogFromRealtime()
    {
        avatarPresetsLoaded = false;
        avatarCatalogRefreshPending = true;
        if (avatarPresetPickerObject != null && avatarPresetPickerObject.activeInHierarchy)
        {
            avatarPickerPending = true;
        }
        if (!avatarPresetsRequestRunning)
        {
            avatarCatalogRefreshPending = false;
            StartCoroutine(RefreshAvatarPresets());
        }
    }

    private void ApplyAvatarImage(Image image, Component fallbackText, long playerId, string avatarCode, long avatarVersion)
    {
        if (image == null)
        {
            return;
        }

        PlayerAvatarState state = GetPlayerAvatarState(playerId);
        string suppliedCode = NormalizeAvatarCode(avatarCode);
        long suppliedVersion = Math.Max(0L, avatarVersion);
        if (playerId > 0 && suppliedVersion > state.avatarVersion)
        {
            SavePlayerAvatarState(playerId, suppliedCode, "", suppliedVersion, string.Empty, false);
            state = GetPlayerAvatarState(playerId);
        }

        string effectiveCode = state.avatarVersion > suppliedVersion ? state.avatarCode : suppliedCode;
        long effectiveVersion = Math.Max(state.avatarVersion, suppliedVersion);
        Sprite sprite = LoadAvatarSprite(effectiveCode, GetAvatarResourceKey(effectiveCode));
        AvatarImageState imageState = GetAvatarImageState(image);
        imageState.requestKey = playerId + ":" + effectiveVersion + ":" + effectiveCode;
        if (sprite != null)
        {
            SetLoadedAvatar(image, fallbackText, sprite);
            return;
        }

        Sprite defaultSprite = LoadAvatarSprite(string.Empty);
        if (defaultSprite != null)
        {
            SetLoadedAvatar(image, fallbackText, defaultSprite);
            return;
        }

        image.sprite = imageState.defaultSprite;
        image.color = imageState.defaultColor;
        image.preserveAspect = imageState.defaultPreserveAspect;
        if (fallbackText != null)
        {
            fallbackText.gameObject.SetActive(true);
        }
    }

    private Sprite LoadAvatarSprite(string avatarCode, string resourceKey = null)
    {
        string normalizedCode = NormalizeAvatarCode(avatarCode);
        if (string.IsNullOrEmpty(normalizedCode))
        {
            resourceKey = DefaultAvatarResourcePath;
        }
        else if (string.IsNullOrWhiteSpace(resourceKey))
        {
            resourceKey = GetAvatarResourceKey(normalizedCode);
        }

        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            Debug.LogWarning("[Avatar] No resource key exists for " + normalizedCode + ".", this);
            return null;
        }

        string resourcePath = resourceKey.Trim();

        string cacheKey = string.IsNullOrEmpty(normalizedCode) ? "DEFAULT" : resourcePath;
        if (avatarSpriteCache.TryGetValue(cacheKey, out Sprite cachedSprite) && cachedSprite != null)
        {
            return cachedSprite;
        }

        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
        {
            Debug.LogWarning("[Avatar] Avatar resource was not found: Resources/" + resourcePath + ".", this);
            return null;
        }

        avatarSpriteCache[cacheKey] = sprite;
        return sprite;
    }

    private string GetAvatarResourceKey(string avatarCode)
    {
        string normalizedCode = NormalizeAvatarCode(avatarCode);
        if (string.IsNullOrEmpty(normalizedCode))
        {
            return DefaultAvatarResourcePath;
        }

        if (avatarResourceKeyByCode.TryGetValue(normalizedCode, out string resourceKey)
            && !string.IsNullOrWhiteSpace(resourceKey))
        {
            return resourceKey;
        }

        if (AvatarResourcePathByCode.TryGetValue(normalizedCode, out string legacyResourcePath))
        {
            return legacyResourcePath;
        }

        return string.Empty;
    }

    private bool CanSelectAvatarPreset(AvatarPresetData preset)
    {
        if (preset == null || !preset.enabled || !IsAvatarPresetClientVersionSupported(preset))
        {
            return false;
        }

        return LoadAvatarSprite(preset.avatarCode, preset.resourceKey) != null;
    }

    private bool IsAvatarPresetClientVersionSupported(AvatarPresetData preset)
    {
        return preset != null
            && Math.Max(0L, preset.minimumClientVersionCode) <= ApiRuntimeConfig.GetClientVersionCode();
    }

    private void ShowAvatarClientUpdateRequired()
    {
        SetStatus("This client version does not support this avatar. Please update.");
    }

    private void MarkAvatarUnavailableAndShowDefault()
    {
        avatarSelectionEnabled = false;
        avatarDisplayForcedDefault = true;
        RefreshCurrentAvatarVisuals();
    }

    private void ApplyDefaultAvatarImage(Image image, Component fallbackText)
    {
        if (image == null)
        {
            return;
        }

        AvatarImageState imageState = GetAvatarImageState(image);
        Sprite defaultSprite = LoadAvatarSprite(string.Empty);
        if (defaultSprite != null)
        {
            SetLoadedAvatar(image, fallbackText, defaultSprite);
            return;
        }

        image.sprite = imageState.defaultSprite;
        image.color = imageState.defaultColor;
        image.preserveAspect = imageState.defaultPreserveAspect;
        if (fallbackText != null)
        {
            fallbackText.gameObject.SetActive(true);
        }
    }

    private static string NormalizeAvatarCode(string avatarCode)
    {
        if (string.IsNullOrEmpty(avatarCode) || string.Equals(avatarCode, "DEFAULT", StringComparison.Ordinal))
        {
            return string.Empty;
        }
        return avatarCode;
    }

    private AvatarPresetData FindAvatarPreset(string avatarCode)
    {
        for (int i = 0; i < avatarPresets.Count; i++)
        {
            if (avatarPresets[i] != null && string.Equals(avatarPresets[i].avatarCode, avatarCode, StringComparison.Ordinal))
            {
                return avatarPresets[i];
            }
        }
        return null;
    }

    private AvatarImageState GetAvatarImageState(Image image)
    {
        if (!avatarImageStates.TryGetValue(image, out AvatarImageState state))
        {
            state = new AvatarImageState
            {
                defaultSprite = image.sprite,
                defaultColor = image.color,
                defaultPreserveAspect = image.preserveAspect
            };
            avatarImageStates.Add(image, state);
        }
        return state;
    }

    private static void SetLoadedAvatar(Image image, Component fallbackText, Sprite sprite)
    {
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        if (fallbackText != null)
        {
            fallbackText.gameObject.SetActive(false);
        }
    }

    private static string GetAvatarRequestError(UnityWebRequest request, BasicApiResponse response, string fallback)
    {
        if (response != null && !string.IsNullOrWhiteSpace(response.message))
        {
            return response.message;
        }
        return ApiRequestUtility.GetErrorMessage(request, fallback);
    }

    private void ApplyAvatarChangedRealtime(AvatarChangedRealtimeData message)
    {
        if (message == null || message.playerId <= 0 || message.avatarVersion <= 0)
        {
            return;
        }

        if (!SavePlayerAvatarState(
                message.playerId,
                message.avatarCode,
                message.avatarSource,
                message.avatarVersion,
                message.avatarUpdatedTime,
                true))
        {
            return;
        }

        ServerFriendResponse friend = message.playerId <= int.MaxValue ? FindV37Friend((int)message.playerId) : null;
        if (friend != null)
        {
            friend.avatarCode = NormalizeAvatarCode(message.avatarCode);
            friend.avatarVersion = message.avatarVersion;
        }
        if (v37FriendSearchResult != null && v37FriendSearchResult.playerId == message.playerId)
        {
            v37FriendSearchResult.avatarCode = NormalizeAvatarCode(message.avatarCode);
            v37FriendSearchResult.avatarVersion = message.avatarVersion;
        }
        UpdateAvatarCodeInFriendRequests(message);

        if (message.playerId == GameSession.PlayerId)
        {
            RefreshCurrentAvatarVisuals();
        }

        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.Friends);
        if (panel != null && panel.PanelRoot != null && panel.gameObject.activeInHierarchy)
        {
            RefreshServerFriendRequestPanel(panel.PanelRoot, "好友头像已更新");
        }
    }

    private void UpdateAvatarCodeInFriendRequests(AvatarChangedRealtimeData message)
    {
        for (int i = 0; i < v37IncomingRequests.Count; i++)
        {
            UpdateAvatarCodeInRequest(v37IncomingRequests[i], message);
        }
        for (int i = 0; i < v37OutgoingRequests.Count; i++)
        {
            UpdateAvatarCodeInRequest(v37OutgoingRequests[i], message);
        }
    }

    private static void UpdateAvatarCodeInRequest(ServerFriendRequestResponse request, AvatarChangedRealtimeData message)
    {
        if (request == null || message == null)
        {
            return;
        }
        if (request.requesterPlayerId == message.playerId && request.requesterAvatarVersion < message.avatarVersion)
        {
            request.requesterAvatarCode = message.avatarCode;
            request.requesterAvatarVersion = message.avatarVersion;
        }
        if (request.recipientPlayerId == message.playerId && request.recipientAvatarVersion < message.avatarVersion)
        {
            request.recipientAvatarCode = message.avatarCode;
            request.recipientAvatarVersion = message.avatarVersion;
        }
    }

    private void DisposeAvatarCache()
    {
        avatarSpriteCache.Clear();
        avatarResourceKeyByCode.Clear();
        avatarImageStates.Clear();
        avatarByPlayerId.Clear();
    }

    [Serializable]
    private class AvatarPresetRequest
    {
        public string avatarCode;
    }

    [Serializable]
    private class AvatarPresetsApiResponse : BasicApiResponse
    {
        public AvatarPresetData[] data;
    }

    [Serializable]
    private class AvatarPresetData
    {
        public string avatarCode;
        public string displayName;
        public string resourceKey;
        public bool enabled;
        public long minimumClientVersionCode;
    }

    [Serializable]
    private class AvatarApiResponse : BasicApiResponse
    {
        public AvatarResponse data;
    }

    [Serializable]
    private class AvatarResponse
    {
        public string avatarUrl;
        public string avatarSource;
        public string avatarCode;
        public long avatarVersion;
        public string avatarUpdatedTime;
    }

    [Serializable]
    private class FriendAvatarBatchRequest
    {
        public int[] playerIds;
    }

    [Serializable]
    private class FriendAvatarBatchApiResponse : BasicApiResponse
    {
        public FriendAvatarSnapshot[] data;
    }

    [Serializable]
    private class FriendAvatarSnapshot
    {
        public int playerId;
        public string avatarSource;
        public string avatarCode;
        public long avatarVersion;
    }

    [Serializable]
    private class AvatarChangedRealtimeData
    {
        public long playerId;
        public string avatarSource;
        public string avatarCode;
        public long avatarVersion;
        public string avatarUpdatedTime;
    }

    private sealed class PlayerAvatarState
    {
        public string avatarCode = string.Empty;
        public string avatarSource = "DEFAULT";
        public long avatarVersion;
        public string avatarUpdatedTime = string.Empty;
        public bool hasServerState;
    }

    private sealed class AvatarImageState
    {
        public Sprite defaultSprite;
        public Color defaultColor;
        public bool defaultPreserveAspect;
        public string requestKey;
    }
}
