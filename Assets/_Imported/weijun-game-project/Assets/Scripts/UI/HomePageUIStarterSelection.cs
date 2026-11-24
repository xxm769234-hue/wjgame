using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public partial class HomePageUI
{
    private const string StarterPendingKeyPrefix = "weijun.starter-selection.pending.";
    private int starterSessionPlayerId;
    private string starterSessionToken;
    private StarterGenderSnapshot starterGenderSnapshot;
    private string starterPendingGender;
    private string starterSelectionMessage;
    private string starterSubmissionMessage;
    private bool starterGenderVerified;
    private bool starterSelectionRunning;
    private bool starterWantsSelectionPage;
    private bool starterSelectionRequestedByGuide;
    private bool starterNpcSynchronized;
    private long starterPlayerNpcId;
    private int starterOwnedNpcId;
    private float starterNextQueryTime;
    private Coroutine starterSelectionRoutine;
    private UnityWebRequest starterSelectionRequest;

    public bool HasActiveStarterSelection => starterSelectionPanel != null
        && starterSelectionPanel.gameObject.activeInHierarchy && IsStarterSessionCurrent(starterSessionPlayerId, starterSessionToken);

    private void TickStarterSelection()
    {
        if (!HasAuthSession())
        {
            if (starterSessionPlayerId != 0) ResetStarterSelectionSession();
            return;
        }
        if (starterSessionPlayerId == GameSession.PlayerId && starterSessionToken == GameSession.Token) return;
        ResetStarterSelectionSession();
        starterSessionPlayerId = GameSession.PlayerId;
        starterSessionToken = GameSession.Token;
        string pending = PlayerPrefs.GetString(StarterPendingKeyPrefix + starterSessionPlayerId, string.Empty);
        starterPendingGender = pending == "MALE" || pending == "FEMALE" ? pending : string.Empty;
        starterWantsSelectionPage = true;
        BeginStarterSelectionOperation(false);
    }

    private bool IsStarterSessionCurrent(int playerId, string token)
    {
        return this != null && isActiveAndEnabled && HasAuthSession()
            && GameSession.PlayerId == playerId && GameSession.Token == token
            && starterSessionPlayerId == playerId && starterSessionToken == token;
    }

    private void RefreshStarterSelectionOnFocus()
    {
        TickStarterSelection();
        if (!starterSelectionRunning && (starterGenderSnapshot == null || !starterGenderSnapshot.selected || !starterNpcSynchronized))
            BeginStarterSelectionOperation(false);
    }

    private void ResetStarterSelectionSession()
    {
        if (starterSelectionRequest != null) starterSelectionRequest.Abort();
        if (starterSelectionRoutine != null) StopCoroutine(starterSelectionRoutine);
        starterSelectionRoutine = null;
        if (starterSelectionRequest != null) starterSelectionRequest.Dispose();
        starterSelectionRequest = null;
        starterSelectionRunning = false;
        HideStarterSelection(true);
        starterSessionPlayerId = 0;
        starterSessionToken = null;
        starterGenderSnapshot = null;
        starterGenderVerified = starterNpcSynchronized = false;
        starterSelectionRequestedByGuide = starterWantsSelectionPage = false;
        starterPendingGender = starterSelectionMessage = starterSubmissionMessage = string.Empty;
        starterPlayerNpcId = 0;
        starterOwnedNpcId = 0;
        starterNextQueryTime = 0f;
    }

    private void OnDisable()
    {
        ResetStarterSelectionSession();
    }

    private void ConfirmStarterSelection(string gender)
    {
        if (starterSelectionRunning || !IsStarterSessionCurrent(starterSessionPlayerId, starterSessionToken)
            || !starterGenderVerified || starterGenderSnapshot == null || starterGenderSnapshot.selected || !starterGenderSnapshot.selectable
            || gender != "MALE" && gender != "FEMALE"
            || !string.IsNullOrEmpty(starterPendingGender) && starterPendingGender != gender) return;
        starterPendingGender = gender;
        PlayerPrefs.SetString(StarterPendingKeyPrefix + starterSessionPlayerId, gender);
        PlayerPrefs.Save();
        BeginStarterSelectionOperation(true);
    }

    private void BeginStarterSelectionOperation(bool submit)
    {
        if (starterSelectionRunning || !IsStarterSessionCurrent(starterSessionPlayerId, starterSessionToken)) return;
        starterSelectionRunning = true;
        starterGenderVerified = false;
        starterSelectionMessage = submit ? "正在确认你的选择，请稍候…" : "正在查询角色选择状态…";
        UpdateStarterSelectionView();
        starterSelectionRoutine = StartCoroutine(RunStarterSelectionOperation(submit, starterSessionPlayerId, starterSessionToken));
    }

    private IEnumerator RunStarterSelectionOperation(bool submit, int playerId, string token)
    {
        try
        {
            if (submit) yield return SubmitStarterGender(playerId, token);
            else yield return QueryStarterGender(playerId, token);
            if (IsStarterSessionCurrent(playerId, token) && starterGenderVerified && starterGenderSnapshot != null
                && starterGenderSnapshot.selected && !starterNpcSynchronized)
                yield return RefreshStarterNpcOwnership(playerId, token);
        }
        finally
        {
            if (starterSelectionRequest != null) starterSelectionRequest.Dispose();
            starterSelectionRequest = null;
            if (IsStarterSessionCurrent(playerId, token))
            {
                starterSelectionRunning = false;
                starterSelectionRoutine = null;
                starterNextQueryTime = Time.unscaledTime + 5f;
                UpdateStarterSelectionView();
                ShowStarterSelectionIfNeeded();
            }
        }
    }

    private IEnumerator QueryStarterGender(int playerId, string token)
    {
        if (!ApiRequestUtility.TryCreateGetWithToken("/player/gender", token, out UnityWebRequest request, out string error))
        {
            starterSelectionMessage = FrontendTextLocalization.NormalizePlayerMessage(error, "暂时无法查询角色选择状态，请重试。");
            yield break;
        }
        starterSelectionRequest = request;
        using (request)
        {
            yield return request.SendWebRequest();
            if (!IsStarterSessionCurrent(playerId, token) || HandleStarterSelectionAccess(request)) yield break;
            StarterGenderResponse response = ParseJson<StarterGenderResponse>(request.downloadHandler.text);
            if (!IsValidStarterGenderResponse(request, response))
            {
                starterSelectionMessage = "暂时无法确认选择状态，请重新查询。";
                yield break;
            }
            starterGenderSnapshot = response.data;
            starterGenderVerified = true;
            if (response.data.selected)
            {
                starterSelectionMessage = "已选定角色，正在载入伙伴…";
            }
            else if (!response.data.selectable)
            {
                starterSelectionMessage = string.IsNullOrEmpty(starterSubmissionMessage)
                    ? "角色选择暂未开放，请稍后再来。" : starterSubmissionMessage + "\n角色选择暂未开放，请稍后再来。";
            }
            else
            {
                starterSelectionMessage = !string.IsNullOrEmpty(starterPendingGender)
                    ? (string.IsNullOrEmpty(starterSubmissionMessage) ? "已查询到本次选择尚未完成。" : starterSubmissionMessage) + "\n可重试原选择，或稍后再来。"
                    : "选择后不可更改 · 展开预览后确认启程";
            }
        }
        starterSelectionRequest = null;
        UpdateStarterSelectionView();
    }

    private IEnumerator SubmitStarterGender(int playerId, string token)
    {
        string json = JsonUtility.ToJson(new StarterGenderRequest { gender = starterPendingGender });
        if (!ApiRequestUtility.TryCreateJsonRequestWithToken("/player/gender", UnityWebRequest.kHttpVerbPOST, json, token,
                out UnityWebRequest request, out string error))
        {
            starterSelectionMessage = FrontendTextLocalization.NormalizePlayerMessage(error, "暂时无法提交，请重新查询。");
            yield break;
        }
        starterSelectionRequest = request;
        using (request)
        {
            yield return request.SendWebRequest();
            if (!IsStarterSessionCurrent(playerId, token) || HandleStarterSelectionAccess(request)) yield break;
            StarterGenderResponse response = ParseJson<StarterGenderResponse>(request.downloadHandler.text);
            if (IsValidStarterGenderResponse(request, response) && response.data.selected)
            {
                starterGenderSnapshot = response.data;
                starterGenderVerified = true;
                starterSubmissionMessage = string.Empty;
                starterSelectionMessage = "已选定角色，正在载入伙伴…";
                UpdateStarterSelectionView();
                yield break;
            }
            starterSubmissionMessage = response != null && !string.IsNullOrWhiteSpace(response.message)
                ? FrontendTextLocalization.NormalizePlayerMessage(response.message, "本次选择结果待确认。") : "本次选择结果待确认。";
        }
        starterSelectionRequest = null;
        starterSelectionMessage = "正在查询本次选择结果，请稍候…";
        UpdateStarterSelectionView();
        yield return QueryStarterGender(playerId, token);
    }

    private static bool IsValidStarterGenderResponse(UnityWebRequest request, StarterGenderResponse response)
    {
        if (request.result != UnityWebRequest.Result.Success || request.responseCode != 200
            || response == null || response.code != 200 || response.data == null) return false;
        StarterGenderSnapshot snapshot = response.data;
        return !snapshot.selected || !snapshot.selectable && (snapshot.gender == "MALE" || snapshot.gender == "FEMALE") && snapshot.starterNpcId > 0;
    }

    private bool HandleStarterSelectionAccess(UnityWebRequest request)
    {
        if (ApiRequestUtility.IsUnauthorized(request))
        {
            HideStarterSelection(true);
            return HandleUnauthorizedResponse(request);
        }
        StarterGenderResponse response = ParseJson<StarterGenderResponse>(request.downloadHandler.text);
        int code = response == null ? 0 : response.code;
        if (code == 7708)
        {
            HideStarterSelection(true);
            GameSession.Clear();
            SceneManager.LoadScene(LaunchSceneName);
            return true;
        }
        bool accessFailure = code == 7901 || code == 7903 || code == 7904 || code == 7905
            || code == 7906 || code == 7912 || code == 7913 || code == 7701 || code == 7702 || code == 7705;
        if (request.responseCode != 403 && !accessFailure) return false;
        HideStarterSelection(true);
        PlayerPrefs.SetString("ComplianceBlockedMessage", FrontendTextLocalization.NormalizePlayerMessage(
            ApiRequestUtility.GetErrorMessage(request, "请先完成账号授权与游戏准入检查。")));
        PlayerPrefs.Save();
        SceneManager.LoadScene(LaunchSceneName);
        return true;
    }

    private IEnumerator RefreshStarterNpcOwnership(int playerId, string token)
    {
        RoleCatalogEntry[] catalog = null;
        RolePlayerEntry[] owned = null;
        string[] paths = { "/npc/catalog", "/npc/list/" + playerId };
        foreach (string path in paths)
        {
            if (!IsStarterSessionCurrent(playerId, token)) yield break;
            if (!ApiRequestUtility.TryCreateGetWithToken(path, token, out UnityWebRequest request, out _)) break;
            starterSelectionRequest = request;
            using (request)
            {
                yield return request.SendWebRequest();
                if (!IsStarterSessionCurrent(playerId, token) || HandleStarterSelectionAccess(request)) yield break;
                if (request.result != UnityWebRequest.Result.Success || request.responseCode != 200) break;
                if (path == paths[0])
                {
                    RoleCatalogApiResponse response = ParseJson<RoleCatalogApiResponse>(request.downloadHandler.text);
                    if (response == null || response.code != 200 || response.data == null
                        || Array.Exists(response.data, entry => entry != null && entry.contractVersion > 1)) break;
                    catalog = response.data;
                }
                else
                {
                    RolePlayerApiResponse response = ParseJson<RolePlayerApiResponse>(request.downloadHandler.text);
                    if (response == null || response.code != 200 || response.data == null) break;
                    owned = response.data;
                }
            }
            starterSelectionRequest = null;
        }
        if (!IsStarterSessionCurrent(playerId, token)) yield break;
        RolePlayerEntry starter = owned == null ? null : Array.Find(owned,
            entry => entry != null && entry.npcId == starterGenderSnapshot.starterNpcId && entry.playerNpcId > 0);
        RoleCatalogEntry starterCatalog = catalog == null ? null : Array.Find(catalog,
            entry => entry != null && entry.npcId == starterGenderSnapshot.starterNpcId && entry.owned);
        if (starter == null || starterCatalog == null)
        {
            starterSelectionMessage = "已选定角色，暂时无法查看伙伴，请重试。";
            PlayerOnboardingManager.NotifyBusinessSucceeded();
            yield break;
        }
        starterPlayerNpcId = starter.playerNpcId;
        starterOwnedNpcId = starter.npcId;
        starterNpcSynchronized = true;
        roleRequestVersion++;
        roleCatalogEntries = catalog;
        rolePlayerEntries = owned;
        roleSelectedNpcId = starter.npcId;
        rolePanelLoading = false;
        if (rolePanelObject != null && rolePanelObject.activeInHierarchy) RenderRolePanel();
        PlayerPrefs.DeleteKey(StarterPendingKeyPrefix + playerId);
        PlayerPrefs.Save();
        starterPendingGender = string.Empty;
        starterSelectionMessage = string.Empty;
        HideStarterSelection(true);
        PlayerOnboardingManager.NotifyBusinessSucceeded();
    }

    private void ShowStarterSelectionIfNeeded()
    {
        if (!starterWantsSelectionPage || !IsStarterSessionCurrent(starterSessionPlayerId, starterSessionToken)) return;
        if (starterGenderSnapshot != null && starterGenderSnapshot.selected
            && (starterNpcSynchronized || string.IsNullOrEmpty(starterPendingGender) && !starterSelectionRequestedByGuide)) return;
        if (starterGenderVerified && starterGenderSnapshot != null && !starterGenderSnapshot.selectable
            && !starterGenderSnapshot.selected && !starterSelectionRequestedByGuide) return;
        if (starterSelectionPanel == null)
        {
            StarterCharacterSelectionView view = StarterCharacterSelectionView.Create(transform, () =>
            {
                starterWantsSelectionPage = false;
                PlayerOnboardingManager.Instance?.SnoozeCurrentStep();
            });
            starterSelectionPanel = view.transform as RectTransform;
            starterSelectionPanel.gameObject.SetActive(false);
        }
        starterSelectionPanel.GetComponent<StarterCharacterSelectionView>().ShowLive(ConfirmStarterSelection,
            () => BeginStarterSelectionOperation(false));
        UpdateStarterSelectionView();
        RegisterOnboardingTarget("GENDER_SELECTION", starterSelectionPanel);
    }

    private void UpdateStarterSelectionView()
    {
        if (starterSelectionPanel == null) return;
        bool selected = starterGenderSnapshot != null && starterGenderSnapshot.selected;
        starterSelectionPanel.GetComponent<StarterCharacterSelectionView>().SetLiveState(
            starterGenderVerified && !selected && starterGenderSnapshot != null && starterGenderSnapshot.selectable,
            starterSelectionRunning, selected, selected ? starterGenderSnapshot.gender : starterPendingGender, starterSelectionMessage);
    }

    private RectTransform ResolveStarterSelectionTarget(bool activate)
    {
        TickStarterSelection();
        if (!starterWantsSelectionPage) return null;
        if (starterGenderSnapshot != null && starterGenderSnapshot.selected && starterNpcSynchronized) return null;
        if (activate)
        {
            starterSelectionRequestedByGuide = true;
            starterWantsSelectionPage = true;
            if (!starterSelectionRunning && (!starterGenderVerified || starterGenderSnapshot == null || !starterGenderSnapshot.selectable)
                && Time.unscaledTime >= starterNextQueryTime) BeginStarterSelectionOperation(false);
            ShowStarterSelectionIfNeeded();
        }
        return HasActiveStarterSelection ? starterSelectionPanel : null;
    }

    public void HideStarterSelection(bool force = false)
    {
        if (!force && starterWantsSelectionPage && IsStarterSessionCurrent(starterSessionPlayerId, starterSessionToken)) return;
        if (starterSelectionPanel != null) starterSelectionPanel.gameObject.SetActive(false);
    }

    public void ResumeStarterSelection()
    {
        starterWantsSelectionPage = true;
    }

    [Serializable]
    private sealed class StarterGenderRequest
    {
        public string gender;
    }

#pragma warning disable 0649
    [Serializable]
    private sealed class StarterGenderResponse : BasicApiResponse
    {
        public StarterGenderSnapshot data;
    }

    [Serializable]
    private sealed class StarterGenderSnapshot
    {
        public bool selected;
        public bool selectable;
        public string gender;
        public long starterNpcId;
        public string starterNpcCode;
        public string starterNpcName;
        public string quality;
        public string icon;
        public string portraitKey;
        public string modelKey;
        public string selectedTime;
        public long policyRevision;
    }
#pragma warning restore 0649
}
