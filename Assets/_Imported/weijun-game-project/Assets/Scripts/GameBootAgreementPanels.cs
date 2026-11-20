using System;
using UnityEngine;
using UnityEngine.UI;

public partial class GameBoot
{
    private LoginAgreementPanel privacyAgreementView;
    private LoginAgreementPanel ageRatingView;
    private LoginAgreementPanel realNameView;
    private LoginAgreementPanel minorNoticeView;

    private bool IsAgreementNoticeOpen => (ageRatingView != null && ageRatingView.gameObject.activeSelf)
        || (minorNoticeView != null && minorNoticeView.gameObject.activeSelf);

    private void EnsureAgeRatingControl(RectTransform root)
    {
        if (root.Find("AgeRatingButton") != null) return;
        Button button = CreateComplianceButton(root, "AgeRatingButton", "16+  适龄提示", Vector2.zero,
            new Vector2(200f, 54f), new Color(0.75f, 0.40f, 0.18f, 0.95f), new Color(1f, 0.97f, 0.86f), 22);
        button.image.sprite = Resources.Load<Sprite>("UI/LoginAgreement/Button");
        button.image.color = Color.white;
        SetRect(button.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(34f, -30f), new Vector2(230f, 75f));
        button.onClick.AddListener(ShowAgeRatingPanel);
    }

    private void ShowAgeRatingPanel()
    {
        if (ageRatingView == null)
        {
            RectTransform root = loginCanvasView != null ? loginCanvasView.PrepareContentRoot() : null;
            if (root == null) return;
            ageRatingView = LoginAgreementPanel.Create(root, "AgeRatingOverlay", "适龄提示", "我知道了",
                HideAgeRatingPanel, HideAgeRatingPanel);
            ageRatingView.SetBody("<b>《书屿梦行》游戏适龄提示</b>\n\n" + ReadAgreementText("AgeRating"));
        }
        ageRatingView.Show();
    }

    private void HideAgeRatingPanel()
    {
        if (ageRatingView != null) ageRatingView.gameObject.SetActive(false);
    }

    private void ShowMinorNoticePanel()
    {
        if (minorNoticeView == null)
        {
            RectTransform root = loginCanvasView != null ? loginCanvasView.PrepareContentRoot() : null;
            if (root == null) return;
            minorNoticeView = LoginAgreementPanel.Create(root, "MinorNoticeOverlay", "温馨提示", "我知道了",
                HideMinorNoticePanel, HideMinorNoticePanel);
            minorNoticeView.SetBody(ReadAgreementText("MinorNotice"));
        }
        minorNoticeView.Show();
    }

    private void HideMinorNoticePanel()
    {
        if (minorNoticeView != null) minorNoticeView.gameObject.SetActive(false);
    }

    private static string ReadAgreementText(string name)
    {
        TextAsset asset = Resources.Load<TextAsset>("UI/LoginAgreement/" + name);
        return asset != null ? asset.text.Trim() : "提示内容暂不可用，请稍后重试。";
    }

    private static bool IsVerifiedMinor(RealNameResult result)
    {
        if (result == null || !result.verified || result.compliance == null) return false;
        string group = result.compliance.ageGroup;
        return string.Equals(group, "UNDER_8", StringComparison.OrdinalIgnoreCase)
            || string.Equals(group, "AGE_8_TO_15", StringComparison.OrdinalIgnoreCase)
            || string.Equals(group, "AGE_16_TO_17", StringComparison.OrdinalIgnoreCase);
    }

    private void HandleRealNameResult(RealNameResult result)
    {
        bool showMinorNotice = IsVerifiedMinor(result);
        if (result != null && result.reloginRequired)
        {
            string message = result.verified ? "实名认证已通过，请重新登录。" : "实名认证状态已更新，请重新登录。";
            CloseAccountManagementPanel();
            SetBusyState(false, message);
        }
        else
        {
            SetRealNameStatus(result != null && result.verified ? "实名认证已通过。" : "实名认证已提交。", MutedTextColor);
        }
        if (showMinorNotice) ShowMinorNoticePanel();
    }
}
