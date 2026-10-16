using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public partial class HomePageUI
{
    private MailResponse[] currentServerMails = new MailResponse[0];
    private long selectedMailId;
    private long pendingMailRouteId;
    private long mailDetailLoadingId;
    private bool mailMutationRunning;

    private void CaptureMailList(MailResponse[] mails)
    {
        currentServerMails = mails ?? new MailResponse[0];
        if (pendingMailRouteId > 0L)
        {
            selectedMailId = pendingMailRouteId;
            return;
        }
        if (FindCurrentMail(selectedMailId) == null)
        {
            selectedMailId = currentServerMails.Length > 0 && currentServerMails[0] != null
                ? currentServerMails[0].mailId
                : 0;
        }
    }

    private void SelectMailForActions(MailResponse mail)
    {
        if (mail == null)
        {
            return;
        }

        selectedMailId = mail.mailId;
        if (!HasAuthSession() || mailDetailLoadingId == mail.mailId)
        {
            return;
        }

        if (string.IsNullOrEmpty(mail.content))
        {
            StartCoroutine(GetMailDetailAndMarkRead(mail.mailId));
        }
        else if (!mail.readFlag)
        {
            mail.readFlag = true;
            StartCoroutine(MarkMailRead(mail.mailId));
        }
    }

    private void ClaimSelectedMailAttachment()
    {
        MailResponse mail = FindCurrentMail(selectedMailId);
        if (mailMutationRunning || mail == null)
        {
            return;
        }

        if (!mail.hasAttachment || mail.attachmentClaimed || mail.expired)
        {
            SetStatus(mail != null && mail.expired ? "邮件已过期，不能领取附件。" : "当前邮件没有可领取附件。");
            return;
        }

        StartCoroutine(ClaimMailAttachment(mail.mailId));
    }

    private void ClaimAllMailAttachments()
    {
        if (!mailMutationRunning)
        {
            StartCoroutine(ClaimAllMailAttachmentsRequest());
        }
    }

    private void DeleteSelectedMail()
    {
        MailResponse mail = FindCurrentMail(selectedMailId);
        if (mailMutationRunning || mail == null)
        {
            return;
        }

        if (mail.hasAttachment && !mail.attachmentClaimed)
        {
            SetStatus("请先领取附件，再删除邮件。");
            return;
        }

        StartCoroutine(DeleteMailRequest(mail.mailId));
    }

    private IEnumerator GetMailDetailAndMarkRead(long mailId)
    {
        mailDetailLoadingId = mailId;
        int playerId = GameSession.PlayerId;
        UnityWebRequest request = CreateAuthorizedGet($"/mail/detail/{playerId}/{mailId}");
        if (request == null)
        {
            mailDetailLoadingId = 0;
            SetStatus(requestCreationError);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                mailDetailLoadingId = 0;
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                mailDetailLoadingId = 0;
                SetStatus("邮件详情读取失败：" + ApiRequestUtility.GetErrorMessage(request, "请求失败"));
                yield break;
            }

            MailDetailApiResponse response = ParseJson<MailDetailApiResponse>(request.downloadHandler.text);
            if (response == null || response.code != 200 || response.data == null)
            {
                mailDetailLoadingId = 0;
                SetStatus("邮件详情读取失败：" + (response == null ? "返回数据格式错误" : GetApiError(response)));
                yield break;
            }

            bool shouldMarkRead = !response.data.readFlag;
            response.data.readFlag = true;
            ReplaceCurrentMail(response.data);
            mailDetailLoadingId = 0;
            ShowSystemMailPanel(currentServerMails, "邮件详情已加载");
            if (shouldMarkRead)
            {
                StartCoroutine(MarkMailRead(mailId));
            }
        }
    }

    private IEnumerator MarkMailRead(long mailId)
    {
        MailActionRequest payload = new MailActionRequest
        {
            playerId = GameSession.PlayerId,
            mailId = mailId
        };

        if (!TryCreateAuthorizedPost("/mail/read", JsonUtility.ToJson(payload), out UnityWebRequest request, out string error))
        {
            SetLocalMailReadState(mailId, false);
            SetStatus(error);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                SetLocalMailReadState(mailId, false);
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                SetLocalMailReadState(mailId, false);
                SetStatus("标记已读失败：" + ApiRequestUtility.GetErrorMessage(request, "请求失败"));
                yield break;
            }

            SetLocalMailReadState(mailId, true);
        }
    }

    private void SetLocalMailReadState(long mailId, bool readFlag)
    {
        MailResponse mail = FindCurrentMail(mailId);
        if (mail != null)
        {
            mail.readFlag = readFlag;
        }

        RefreshSystemMailSceneUnreadMarks();
    }

    private IEnumerator ClaimMailAttachment(long mailId)
    {
        mailMutationRunning = true;
        MailActionRequest payload = new MailActionRequest
        {
            playerId = GameSession.PlayerId,
            mailId = mailId
        };

        if (!TryCreateAuthorizedPost("/mail/claim", JsonUtility.ToJson(payload), out UnityWebRequest request, out string error))
        {
            mailMutationRunning = false;
            SetStatus(error);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                mailMutationRunning = false;
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                mailMutationRunning = false;
                SetStatus("附件领取失败：" + ApiRequestUtility.GetErrorMessage(request, "请求失败"));
                yield break;
            }

            MailClaimApiResponse response = ParseJson<MailClaimApiResponse>(request.downloadHandler.text);
            if (response == null || response.code != 200)
            {
                mailMutationRunning = false;
                SetStatus("附件领取失败：" + (response == null ? "返回数据格式错误" : GetApiError(response)));
                yield break;
            }

            if (response.data != null)
            {
                if (response.data.mail != null)
                {
                    ReplaceCurrentMail(response.data.mail);
                }

                ApplyMailInventorySnapshot(response.data.items);
                MailAttachmentResponse[] claimedAttachments = response.data.claimedAttachments;
                if ((claimedAttachments == null || claimedAttachments.Length == 0)
                    && response.data.attachments != null)
                {
                    claimedAttachments = response.data.attachments;
                }
                if ((claimedAttachments == null || claimedAttachments.Length == 0)
                    && response.data.mail != null)
                {
                    claimedAttachments = response.data.mail.attachments;
                }
                yield return RefreshMailRewardState(claimedAttachments, null, response.data.items != null);
            }
        }

        mailMutationRunning = false;
        SetStatus("附件领取成功，正在刷新邮件列表...");
        yield return GetMailList();
    }

    private IEnumerator ClaimAllMailAttachmentsRequest()
    {
        mailMutationRunning = true;
        MailClaimAllRequest payload = new MailClaimAllRequest
        {
            playerId = GameSession.PlayerId,
            limit = 50
        };

        if (!TryCreateAuthorizedPost("/mail/claim_all", JsonUtility.ToJson(payload), out UnityWebRequest request, out string error))
        {
            mailMutationRunning = false;
            SetStatus(error);
            yield break;
        }

        int claimedCount = 0;
        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                mailMutationRunning = false;
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                mailMutationRunning = false;
                SetStatus("一键领取失败：" + ApiRequestUtility.GetErrorMessage(request, "请求失败"));
                yield break;
            }

            MailClaimAllApiResponse response = ParseJson<MailClaimAllApiResponse>(request.downloadHandler.text);
            if (response == null || response.code != 200)
            {
                mailMutationRunning = false;
                SetStatus("一键领取失败：" + (response == null ? "返回数据格式错误" : GetApiError(response)));
                yield break;
            }

            if (response.data != null)
            {
                claimedCount = response.data.claimedCount;
                ApplyMailInventorySnapshot(response.data.items);
                yield return RefreshMailRewardState(null, response.data.mails, response.data.items != null);
            }
        }

        mailMutationRunning = false;
        SetStatus($"一键领取完成，共领取 {claimedCount} 封邮件附件。正在刷新列表...");
        yield return GetMailList();
    }

    private IEnumerator DeleteMailRequest(long mailId)
    {
        mailMutationRunning = true;
        MailActionRequest payload = new MailActionRequest
        {
            playerId = GameSession.PlayerId,
            mailId = mailId
        };

        if (!TryCreateAuthorizedPost("/mail/delete", JsonUtility.ToJson(payload), out UnityWebRequest request, out string error))
        {
            mailMutationRunning = false;
            SetStatus(error);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                mailMutationRunning = false;
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                mailMutationRunning = false;
                SetStatus("删除邮件失败：" + ApiRequestUtility.GetErrorMessage(request, "请求失败"));
                yield break;
            }

            BasicApiResponse response = ParseJson<BasicApiResponse>(request.downloadHandler.text);
            if (response == null || response.code != 200)
            {
                mailMutationRunning = false;
                SetStatus("删除邮件失败：" + (response == null ? "返回数据格式错误" : GetApiError(response)));
                yield break;
            }
        }

        mailMutationRunning = false;
        selectedMailId = 0;
        SetStatus("邮件已删除，正在刷新列表...");
        yield return GetMailList();
    }

    private string BuildMailAttachmentSummary(MailResponse mail)
    {
        if (mail == null || !mail.hasAttachment)
        {
            return "无附件";
        }

        if (mail.attachments == null || mail.attachments.Length == 0)
        {
            return mail.attachmentClaimed ? "附件已领取" : "附件待领取";
        }

        string summary = string.Empty;
        for (int i = 0; i < mail.attachments.Length; i++)
        {
            MailAttachmentResponse attachment = mail.attachments[i];
            if (attachment == null)
            {
                continue;
            }

            bool claimed = !string.IsNullOrEmpty(attachment.claimedTime)
                || !string.IsNullOrEmpty(attachment.grantedRewardType);
            string name = claimed && !string.IsNullOrEmpty(attachment.grantedRewardName)
                ? attachment.grantedRewardName
                : (!string.IsNullOrEmpty(attachment.rewardName)
                    ? attachment.rewardName
                    : (string.IsNullOrEmpty(attachment.itemName)
                        ? (string.IsNullOrEmpty(attachment.name) ? attachment.itemKey : attachment.name)
                        : attachment.itemName));
            long quantity = claimed && attachment.grantedQuantity > 0
                ? attachment.grantedQuantity
                : attachment.quantity;
            if (summary.Length > 0)
            {
                summary += "  ";
            }
            summary += $"{name} x{quantity}";
        }

        return string.IsNullOrEmpty(summary) ? "附件待领取" : summary;
    }

    private IEnumerator RefreshMailRewardState(
        MailAttachmentResponse[] attachments,
        MailResponse[] mails,
        bool inventoryAlreadySynced)
    {
        bool needsInventoryRefresh = false;
        bool needsEquipmentRefresh = false;
        bool needsNpcRefresh = false;
        AccumulateMailRewardRefreshFlags(
            attachments,
            ref needsInventoryRefresh,
            ref needsEquipmentRefresh,
            ref needsNpcRefresh);
        if (mails != null)
        {
            for (int i = 0; i < mails.Length; i++)
            {
                AccumulateMailRewardRefreshFlags(
                    mails[i] == null ? null : mails[i].attachments,
                    ref needsInventoryRefresh,
                    ref needsEquipmentRefresh,
                    ref needsNpcRefresh);
            }
        }

        List<string> syncedCategories = new List<string>();
        List<string> failedCategories = new List<string>();
        if (needsInventoryRefresh)
        {
            if (inventoryAlreadySynced)
            {
                syncedCategories.Add("背包");
            }
            else
            {
                yield return RefreshCardDrawInventoryState(syncedCategories, failedCategories);
            }
        }
        if (needsEquipmentRefresh)
        {
            yield return RefreshCardDrawEquipmentState(syncedCategories, failedCategories);
        }
        if (needsNpcRefresh)
        {
            yield return RefreshCardDrawNpcState(syncedCategories, failedCategories);
        }

        if (inventoryPanelObject != null && inventoryPanelObject.activeInHierarchy && needsInventoryRefresh)
        {
            inventoryStatusMessage = "邮件奖励已同步";
            RenderInventoryPanel();
        }
        if (equipmentPanelObject != null && equipmentPanelObject.activeInHierarchy && (needsEquipmentRefresh || needsNpcRefresh))
        {
            RebuildServerEquipmentItems();
            equipmentStatusMessage = "邮件奖励已同步";
            RenderEquipmentScenePanel(GetEquipmentPanelRoot());
        }
        if (failedCategories.Count > 0)
        {
            SetStatus("邮件领取成功，但部分奖励同步失败：" + string.Join("；", failedCategories.ToArray()));
        }
    }

    private void ApplyMailInventorySnapshot(InventoryItemResponse[] items)
    {
        if (items == null)
        {
            return;
        }

        currentInventoryItems = items;
        inventoryServerBacked = true;
        SyncInventoryResourceValues(items);
    }

    private static void AccumulateMailRewardRefreshFlags(
        MailAttachmentResponse[] attachments,
        ref bool needsInventoryRefresh,
        ref bool needsEquipmentRefresh,
        ref bool needsNpcRefresh)
    {
        if (attachments == null)
        {
            return;
        }

        for (int i = 0; i < attachments.Length; i++)
        {
            MailAttachmentResponse attachment = attachments[i];
            if (attachment == null)
            {
                continue;
            }

            string rewardType = string.IsNullOrWhiteSpace(attachment.grantedRewardType)
                ? attachment.attachmentType
                : attachment.grantedRewardType;
            switch ((rewardType ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "ITEM":
                    needsInventoryRefresh = true;
                    break;
                case "EQUIPMENT":
                    needsEquipmentRefresh = true;
                    break;
                case "NPC":
                    needsNpcRefresh = true;
                    break;
            }
        }
    }

    private MailResponse FindCurrentMail(long mailId)
    {
        if (mailId <= 0 || currentServerMails == null)
        {
            return null;
        }

        for (int i = 0; i < currentServerMails.Length; i++)
        {
            if (currentServerMails[i] != null && currentServerMails[i].mailId == mailId)
            {
                return currentServerMails[i];
            }
        }
        return null;
    }

    private void ReplaceCurrentMail(MailResponse updated)
    {
        if (updated == null)
        {
            return;
        }

        if (currentServerMails == null)
        {
            currentServerMails = new MailResponse[0];
        }

        for (int i = 0; i < currentServerMails.Length; i++)
        {
            if (currentServerMails[i] != null && currentServerMails[i].mailId == updated.mailId)
            {
                currentServerMails[i] = updated;
                return;
            }
        }

        Array.Resize(ref currentServerMails, currentServerMails.Length + 1);
        currentServerMails[currentServerMails.Length - 1] = updated;
    }

    private bool TryConsumePendingMailRoute()
    {
        if (pendingMailRouteId <= 0L)
        {
            return false;
        }

        long targetMailId = pendingMailRouteId;
        if (FindCurrentMail(targetMailId) != null)
        {
            selectedMailId = targetMailId;
            pendingMailRouteId = 0L;
            return false;
        }

        pendingMailRouteId = 0L;
        StartCoroutine(GetMailDetailAndMarkRead(targetMailId));
        return true;
    }

#pragma warning disable 0649
    [Serializable]
    private class MailDetailApiResponse : BasicApiResponse
    {
        public MailResponse data;
    }

    [Serializable]
    private class MailActionRequest
    {
        public int playerId;
        public long mailId;
    }

    [Serializable]
    private class MailClaimAllRequest
    {
        public int playerId;
        public int limit;
    }

    [Serializable]
    private class MailClaimApiResponse : BasicApiResponse
    {
        public MailClaimData data;
    }

    [Serializable]
    private class MailClaimData
    {
        public MailResponse mail;
        public MailAttachmentResponse[] claimedAttachments;
        public MailAttachmentResponse[] attachments;
        public InventoryItemResponse[] items;
    }

    [Serializable]
    private class MailClaimAllApiResponse : BasicApiResponse
    {
        public MailClaimAllData data;
    }

    [Serializable]
    private class MailClaimAllData
    {
        public int claimedCount;
        public int skippedCount;
        public MailResponse[] mails;
        public InventoryItemResponse[] items;
    }

    [Serializable]
    private class MailAttachmentResponse
    {
        public long attachmentId;
        public string attachmentType;
        public long assetId;
        public string rewardKey;
        public string rewardName;
        public string rewardCategory;
        public string rewardIcon;
        public string rewardQuality;
        public long itemId;
        public string itemKey;
        public string itemName;
        public string name;
        public string itemType;
        public string icon;
        public long quantity;
        public string grantedRewardType;
        public long grantedAssetId;
        public string grantedRewardKey;
        public string grantedRewardName;
        public string grantedRewardCategory;
        public string grantedRewardIcon;
        public string grantedRewardQuality;
        public long grantedQuantity;
        public bool fallbackApplied;
        public string claimedTime;
    }
#pragma warning restore 0649
}
