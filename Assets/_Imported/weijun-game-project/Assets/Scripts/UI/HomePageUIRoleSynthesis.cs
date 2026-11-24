using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public partial class HomePageUI
{
    [Serializable] private sealed class SynthesisPreviewResponse : BasicApiResponse { public SynthesisPreview data; }
    [Serializable] private sealed class SynthesisPreview
    {
        public int contractVersion, npcId;
        public string npcCode, name, quality, reasonMessage, planFingerprint;
        public bool owned, synthesisEnabled, canSynthesize;
        public SynthesisFragment fragment;
    }
    [Serializable] private sealed class SynthesisFragment
    {
        public string itemKey, name, icon, quality;
        public long requiredAmount, currentAmount;
    }
    [Serializable] private sealed class SynthesisPayload { public string planFingerprint; }
    [Serializable] private sealed class SynthesisIntent { public string key, fingerprint; }

    private GameObject synthesisOverlay;
    private Image synthesisIcon;
    private TMP_Text synthesisName, synthesisCount, synthesisMessage;
    private Button synthesisAction, synthesisBack;
    private int synthesisNpcId, synthesisViewVersion;
    private bool synthesisSending;
    private SynthesisPreview synthesisPlan;

    private void CloseSynthesisPanel()
    {
        synthesisViewVersion++;
        synthesisNpcId = 0;
        synthesisPlan = null;
        if (synthesisOverlay == null) return;
        synthesisOverlay.SetActive(false);
        Destroy(synthesisOverlay);
        synthesisOverlay = null;
    }

    private void OpenSynthesisPanel(int npcId)
    {
        if (rolePanelObject == null || synthesisSending || !HasAuthSession()) return;
        CloseSynthesisPanel();
        synthesisNpcId = npcId;
        var root = rolePanelObject.GetComponent<HomePageScenePanel>().PanelRoot;
        Image shade = CreateImage("Role Synthesis",root,new Color32(40,27,36,178));
        synthesisOverlay = shade.gameObject;
        shade.raycastTarget = true;
        BookArea(shade.transform,0,0,1,1);
        Image page = CreateImage("Synthesis Paper",shade.transform,Color.white);
        page.sprite = Resources.Load<Sprite>("UI/ApprovedUi/Panels/PaperPanel");
        BookArea(page.transform,.22f,.13f,.78f,.87f);
        SynthesisLabel(page.transform,"Title","角色合成",36,.12f,.77f,.88f,.91f).fontStyle = FontStyles.Bold;
        synthesisIcon = CreateImage("Fragment",page.transform,Color.white);
        synthesisIcon.raycastTarget = false;
        synthesisIcon.preserveAspect = true;
        synthesisIcon.enabled = false;
        BookArea(synthesisIcon.transform,.34f,.45f,.66f,.76f);
        synthesisName = SynthesisLabel(page.transform,"Name","",29,.10f,.37f,.90f,.46f);
        synthesisCount = SynthesisLabel(page.transform,"Amount","",24,.12f,.29f,.88f,.37f);
        synthesisMessage = SynthesisLabel(page.transform,"Status","",21,.08f,.20f,.92f,.29f);
        synthesisBack = SynthesisButton(page.transform,"Back","返回",.19f,.46f);
        synthesisAction = SynthesisButton(page.transform,"Confirm","合成",.52f,.81f);
        BindSceneButton(synthesisBack,CloseSynthesisPanel);
        StartCoroutine(ReadSynthesisPreview());
    }

    private static TMP_Text SynthesisLabel(Transform parent,string name,string caption,float size,float left,float bottom,float right,float top)
    {
        TMP_Text label = EnsurePresentationCaption(parent,name,caption);
        BookArea(label.transform,left,bottom,right,top);
        BookLabel(label,size,SummonInk);
        return label;
    }

    private Button SynthesisButton(Transform page,string name,string caption,float left,float right)
    {
        Button button = CreateButton(page,name,caption,Color.white,SummonInk);
        SetSceneObjectActive(button.transform,"ButtonShine",false);
        BookArea(button.transform,left,.073f,right,.193f);
        BookButton(button,name == "Back" ? "Single" : "Multiple",23);
        return button;
    }

    private void SynthesisState(string message,string caption,Action action)
    {
        synthesisMessage.text = message;
        SetSceneButtonText(synthesisAction,caption);
        BindSceneButton(synthesisAction,action);
        synthesisAction.interactable = action != null;
    }

    private static string SynthesisStorage(int playerId,int npcId)
    {
        return "weijun.npc.synthesis." + playerId + "." + npcId;
    }

    private bool SynthesisViewCurrent(int version,int playerId,string token)
    {
        return synthesisOverlay != null && synthesisOverlay.activeInHierarchy && version == synthesisViewVersion && IsRoleGrowthSessionCurrent(playerId,token);
    }

    private IEnumerator ReadSynthesisPreview(string notice = "")
    {
        int version = ++synthesisViewVersion;
        int npcId = synthesisNpcId, playerId = GameSession.PlayerId;
        string token = GameSession.Token;
        synthesisPlan = null;
        SynthesisState("正在读取专属碎片…","合成",null);
        using (var request = CreateAuthorizedGet("/npc/" + npcId + "/synthesis-preview"))
        {
            if (request != null) yield return request.SendWebRequest();
            if (!SynthesisViewCurrent(version,playerId,token)) yield break;
            if (request != null && HandleUnauthorizedResponse(request)) yield break;
            var reply = request == null ? null : ParseJson<SynthesisPreviewResponse>(request.downloadHandler.text);
            SynthesisPreview plan = reply?.data;
            if (request == null || request.result != UnityWebRequest.Result.Success || reply?.code != 200 || plan == null
                || plan.npcId != npcId || plan.contractVersion != 1)
            {
                SynthesisState("合成信息暂未读取，请重试","重新读取",() => StartCoroutine(ReadSynthesisPreview()));
                yield break;
            }
            synthesisName.text = plan.name;
            var cost = plan.fragment;
            synthesisIcon.sprite = RoleFragmentArt.Resolve(cost?.itemKey,cost?.name,cost?.icon,cost?.quality,"NPC_FRAGMENT",plan.npcCode,plan.quality);
            synthesisIcon.enabled = synthesisIcon.sprite != null;
            synthesisCount.text = cost != null ? "专属碎片  " + cost.currentAmount.ToString("N0") + " / " + cost.requiredAmount.ToString("N0") : "";
            string storage = SynthesisStorage(playerId,npcId);
            if (plan.owned)
            {
                ClearSynthesisIntent(storage);
                SynthesisState("已拥有该角色","已拥有",null);
                yield return RefreshSynthesisState(playerId,token);
                yield break;
            }
            if (!plan.synthesisEnabled || !plan.canSynthesize || cost == null || cost.requiredAmount <= 0 || cost.currentAmount < cost.requiredAmount || string.IsNullOrEmpty(plan.planFingerprint))
            {
                string reason = plan.reasonMessage;
                if (string.IsNullOrWhiteSpace(reason)) reason = plan.synthesisEnabled ? "专属碎片不足" : "暂未开放合成";
                SynthesisState(reason,"合成",null);
                yield break;
            }
            synthesisPlan = plan;
            string prompt = string.IsNullOrWhiteSpace(notice) ? "消耗专属碎片，获得该角色" : notice;
            SynthesisState(prompt,"合成角色",() => StartCoroutine(SubmitSynthesis()));
        }
    }

    private static void ClearSynthesisIntent(string storage)
    {
        PlayerPrefs.DeleteKey(storage);
        PlayerPrefs.Save();
    }

    private IEnumerator SubmitSynthesis()
    {
        var plan = synthesisPlan;
        if (synthesisSending || synthesisOverlay == null || plan == null || plan.npcId != synthesisNpcId || !HasAuthSession()) yield break;
        int playerId = GameSession.PlayerId, version = synthesisViewVersion;
        string token = GameSession.Token, storage = SynthesisStorage(playerId,plan.npcId);
        SynthesisIntent intent = ParseJson<SynthesisIntent>(PlayerPrefs.GetString(storage,""));
        if (intent == null || !Guid.TryParse(intent.key,out _) || intent.fingerprint != plan.planFingerprint)
            intent = new SynthesisIntent { key = Guid.NewGuid().ToString(), fingerprint = plan.planFingerprint };
        try
        {
            PlayerPrefs.SetString(storage,JsonUtility.ToJson(intent));
            PlayerPrefs.Save();
        }
        catch (PlayerPrefsException)
        {
            SynthesisState("本次合成记录无法保存，请重试","重试",() => StartCoroutine(SubmitSynthesis()));
            yield break;
        }
        string body = JsonUtility.ToJson(new SynthesisPayload { planFingerprint = intent.fingerprint });
        if (!TryCreateAuthorizedPost("/npc/" + plan.npcId + "/synthesize",body,out UnityWebRequest request,out string error))
        {
            SynthesisState(error,"重试",() => StartCoroutine(SubmitSynthesis()));
            yield break;
        }
        synthesisSending = true;
        synthesisBack.interactable = false;
        SynthesisState("正在合成…","合成中",null);
        using (request)
        {
            request.SetRequestHeader("Idempotency-Key",intent.key);
            yield return request.SendWebRequest();
            synthesisSending = false;
            if (!IsRoleGrowthSessionCurrent(playerId,token)) yield break;
            bool visible = SynthesisViewCurrent(version,playerId,token);
            if (visible) synthesisBack.interactable = true;
            if (HandleUnauthorizedResponse(request)) yield break;
            BasicApiResponse reply = ParseJson<BasicApiResponse>(request.downloadHandler.text);
            if (request.result == UnityWebRequest.Result.Success && reply?.code == 200)
            {
                ClearSynthesisIntent(storage);
                if (visible)
                {
                    synthesisPlan = null;
                    synthesisCount.text = "";
                    SynthesisState("已获得 " + plan.name,"完成",CloseSynthesisPanel);
                }
                yield return RefreshSynthesisState(playerId,token);
                yield break;
            }
            if (reply != null && reply.code >= 6110 && reply.code <= 6115)
            {
                ClearSynthesisIntent(storage);
                if (visible)
                {
                    string notice = reply.code == 6113 ? "合成条件已更新，请重新确认" : GetApiError(reply);
                    yield return ReadSynthesisPreview(notice);
                }
                yield break;
            }
            if (visible) SynthesisState("结果尚未确认，可重试本次合成","重试",() => StartCoroutine(SubmitSynthesis()));
        }
    }

    private IEnumerator RefreshSynthesisState(int playerId,string token)
    {
        var synced = new List<string>();
        var failed = new List<string>();
        if (IsRoleGrowthSessionCurrent(playerId,token)) yield return RefreshCardDrawInventoryState(synced,failed);
        if (IsRoleGrowthSessionCurrent(playerId,token)) yield return RefreshCardDrawNpcState(synced,failed);
        if (IsRoleGrowthSessionCurrent(playerId,token) && rolePanelObject != null && rolePanelObject.activeInHierarchy)
            yield return LoadRolePanelData(++roleRequestVersion,playerId);
        if (failed.Count > 0 && IsRoleGrowthSessionCurrent(playerId,token) && synthesisOverlay != null)
            synthesisMessage.text = "角色已获得，部分资料暂未刷新";
    }
}
