using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public partial class HomePageUI
{
    private MailResponse[] currentServerMails = new MailResponse[0];
    private long selectedMailId;
    private long mailDetailLoadingId;
    private bool mailMutationRunning;

    private void CaptureMailList(MailResponse[] mails)
    {
        currentServerMails = mails ?? new MailResponse[0];
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
            SetStatus(error);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (HandleUnauthorizedResponse(request))
            {
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                SetStatus("标记已读失败：" + ApiRequestUtility.GetErrorMessage(request, "请求失败"));
            }
        }
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
                SyncInventoryResourceValues(response.data.items);
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
                SyncInventoryResourceValues(response.data.items);
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

            string name = string.IsNullOrEmpty(attachment.itemName)
                ? (string.IsNullOrEmpty(attachment.name) ? attachment.itemKey : attachment.name)
                : attachment.itemName;
            if (summary.Length > 0)
            {
                summary += "  ";
            }
            summary += $"{name} x{attachment.quantity}";
        }

        return string.IsNullOrEmpty(summary) ? "附件待领取" : summary;
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
        if (updated == null || currentServerMails == null)
        {
            return;
        }

        for (int i = 0; i < currentServerMails.Length; i++)
        {
            if (currentServerMails[i] != null && currentServerMails[i].mailId == updated.mailId)
            {
                currentServerMails[i] = updated;
                return;
            }
        }
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
        public int itemId;
        public string itemKey;
        public string itemName;
        public string name;
        public string icon;
        public int quantity;
    }
#pragma warning restore 0649
}
