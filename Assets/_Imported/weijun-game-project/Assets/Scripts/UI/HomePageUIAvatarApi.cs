using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting;
using UnityEngine.UI;

public partial class HomePageUI
{
    private const int MaximumAvatarBytes = 2 * 1024 * 1024;
    private const int MaximumSourceAvatarBytes = 16 * 1024 * 1024;
    private const int MaximumPreparedAvatarDimension = 2048;
    private const int MaximumServerAvatarDimension = 4096;
    private const long MaximumServerAvatarPixels = 16000000L;
    private const string DefaultAvatarCode = "AVATAR_001";
    private const string DefaultAvatarResourcePath = "Avatars/avatar_001";
    private const string Avatar002ResourcePath = "UI/AvatarPresets/微信图片_20260812141117_70_71";
    private const string Avatar003ResourcePath = "UI/AvatarPresets/微信图片_20260812141246_71_71";
    private const string AvatarOwnerPlayerIdKey = "PlayerAvatarOwnerId";
    private const string LocalAvatarResourcePathKey = "PlayerAvatarResourcePath";
    private const string AvatarCodeKey = "PlayerAvatarCode";
    private const string PlayerAvatarStateKeyPrefix = "PlayerAvatarState.";
    [SerializeField] private AvatarSpriteBinding[] avatarSpriteBindings = new AvatarSpriteBinding[0];
    private readonly Dictionary<string, Sprite> avatarSpriteCache = new Dictionary<string, Sprite>();
    private readonly Dictionary<long, object> avatarByPlayerId = new Dictionary<long, object>();
    private readonly Dictionary<Image, AvatarImageState> avatarImageStates = new Dictionary<Image, AvatarImageState>();
    private readonly List<AvatarPresetOption> presetAvatarOptions = new List<AvatarPresetOption>();
    private readonly HashSet<string> enabledPresetAvatarCodes = new HashSet<string>(StringComparer.Ordinal);
    private GameObject avatarPresetPickerObject;
    private bool avatarMutationRunning;
    private bool avatarPresetCatalogLoading;
    private bool avatarPresetCatalogLoaded;

    private void ConfigureProfileAvatarControls(Transform root, bool isAuthenticated, string nickname)
    {
        Image avatar = FindSceneComponent<Image>(root, "ProfileAvatarBackground");
        if (avatar == null)
        {
            return;
        }

        Button avatarButtonComponent = avatar.GetComponent<Button>();
        if (avatarButtonComponent == null)
        {
            avatarButtonComponent = avatar.gameObject.AddComponent<Button>();
            avatarButtonComponent.targetGraphic = avatar;
        }
        avatarButtonComponent.interactable = !avatarMutationRunning;
        BindSceneButton(avatarButtonComponent, BeginAvatarSelection);

        ApplyAvatarImage(
            avatar,
            FindSceneComponent<TMP_Text>(root, "ProfileAvatarInitial"),
            GameSession.PlayerId,
            GetCurrentAvatarUrl(),
            GetCurrentAvatarCode(),
            GetCurrentAvatarVersion(),
            true);
    }

    private void BeginAvatarSelection()
    {
        if (avatarMutationRunning)
        {
            return;
        }
        if (!HasAuthSession())
        {
            Debug.LogWarning("[Avatar] Avatar selection rejected because there is no authenticated session.");
            RefreshProfileAvatarPanel("请先登录账号后上传头像");
            return;
        }

        Debug.Log("[Avatar] Opening avatar picker for player " + GameSession.PlayerId + ".");
        RefreshProfileAvatarPanel("正在打开头像选择...");
        ShowPresetAvatarPicker();
    }

    private void ShowPresetAvatarPicker()
    {
        HomePageScenePanel panel = GetRegisteredScenePanel(HomePageScenePanelType.AccountProfile);
        if (panel == null || panel.PanelRoot == null)
        {
            Debug.LogWarning("[Avatar] Account profile panel is unavailable; cannot show preset avatars.", this);
            RefreshProfileAvatarPanel("个人信息面板未找到，无法选择头像");
            return;
        }

        avatarPresetPickerObject = EnsurePresetAvatarPicker(panel.PanelRoot);
        if (avatarPresetPickerObject == null)
        {
            RefreshProfileAvatarPanel("头像选择面板创建失败");
            return;
        }

        BuildPresetAvatarOptions(avatarPresetPickerObject.transform);
        avatarPresetPickerObject.SetActive(true);
        avatarPresetPickerObject.transform.SetAsLastSibling();
        if (!avatarPresetCatalogLoaded && !avatarPresetCatalogLoading)
        {
            StartCoroutine(RefreshPresetAvatarCatalog(true));
        }
        else
        {
            RefreshPresetAvatarPickerTitle();
        }
        SetStatus(avatarPresetCatalogLoading ? "正在读取固定头像目录..." : "请选择固定头像或微信头像");
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
        SetRect(gridObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(-56f, -142f));
        GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(100f, 100f);
        grid.spacing = new Vector2(18f, 18f);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        grid.childAlignment = TextAnchor.UpperCenter;

        Button weChatButton = CreateButton(card.transform, "ProfileWechatAvatarButton", "选择微信头像", new Color(0.25f, 0.58f, 0.42f, 1f), Color.white);
        SetRect(weChatButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(180f, 42f));
        ShrinkButtonLabel(weChatButton, 16);
        BindSceneButton(weChatButton, BeginWeChatAvatarSelection);

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

        for (int i = 0; i < presetAvatarOptions.Count; i++)
        {
            AvatarPresetOption preset = presetAvatarOptions[i];
            if (preset == null || preset.sprite == null)
            {
                continue;
            }

            Image option = CreateImage("ProfileAvatarPreset_" + preset.avatarCode, grid, Color.white);
            option.sprite = preset.sprite;
            option.preserveAspect = true;
            option.raycastTarget = true;
            Button optionButton = option.gameObject.AddComponent<Button>();
            optionButton.targetGraphic = option;
            AvatarPresetOption selectedPreset = preset;
            BindSceneButton(optionButton, () => SelectPresetAvatar(selectedPreset));

            TMP_Text label = CreatePresetAvatarText(
                "Label",
                option.transform,
                string.IsNullOrWhiteSpace(preset.displayName) ? preset.avatarCode : preset.displayName,
                12f,
                new Color(0.28f, 0.14f, 0.06f, 1f),
                TextAlignmentOptions.Bottom);
            SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-4f, -4f));
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

    private void SelectPresetAvatar(AvatarPresetOption preset)
    {
        if (avatarMutationRunning || preset == null || preset.sprite == null || !preset.enabled)
        {
            return;
        }
        if (!HasAuthSession())
        {
            Debug.LogWarning("[Avatar] Preset avatar selection rejected because there is no authenticated session.", this);
            HidePresetAvatarPicker();
            RefreshProfileAvatarPanel("请先登录账号后上传头像");
            return;
        }

        avatarMutationRunning = true;
        HidePresetAvatarPicker();
        RefreshProfileAvatarPanel("正在选择固定头像...");
        StartCoroutine(SelectServerPresetAvatar(preset.avatarCode));
    }

    private void BeginWeChatAvatarSelection()
    {
        if (avatarMutationRunning || !HasAuthSession())
        {
            return;
        }

        avatarMutationRunning = true;
        HidePresetAvatarPicker();
        RefreshProfileAvatarPanel("正在打开微信头像选择...");
        WeChatPlatformBridge.RequestAvatarSelection(this);
    }

    private IEnumerator RefreshPresetAvatarCatalog(bool refreshPicker)
    {
        if (avatarPresetCatalogLoading || !HasAuthSession())
        {
            yield break;
        }

        avatarPresetCatalogLoading = true;
        int requestedPlayerId = GameSession.PlayerId;
        if (refreshPicker)
        {
            RefreshPresetAvatarPickerTitle();
        }

        UnityWebRequest request = CreateAuthorizedGet("/user/avatar/presets");
        if (request == null)
        {
            avatarPresetCatalogLoading = false;
            if (refreshPicker)
            {
                RefreshPresetAvatarPickerTitle(requestCreationError);
                RefreshProfileAvatarPanel(requestCreationError);
            }
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (requestedPlayerId != GameSession.PlayerId)
            {
                avatarPresetCatalogLoading = false;
                yield break;
            }
            if (HandleUnauthorizedResponse(request))
            {
                avatarPresetCatalogLoading = false;
                yield break;
            }

            AvatarPresetCatalogApiResponse response = ParseJson<AvatarPresetCatalogApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                avatarPresetCatalogLoading = false;
                avatarPresetCatalogLoaded = false;
                string message = response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "固定头像目录读取失败")
                    : GetApiError(response);
                if (refreshPicker)
                {
                    RefreshPresetAvatarPickerTitle(message);
                    RefreshProfileAvatarPanel(message);
                }
                yield break;
            }

            RebuildPresetAvatarOptions(response.data);
            avatarPresetCatalogLoaded = true;
            avatarPresetCatalogLoading = false;
        }

        ReconcileCurrentPresetAvatar();
        if (avatarPresetPickerObject != null && avatarPresetPickerObject.activeInHierarchy)
        {
            BuildPresetAvatarOptions(avatarPresetPickerObject.transform);
            RefreshPresetAvatarPickerTitle();
        }
        if (refreshPicker)
        {
            SetStatus(presetAvatarOptions.Count > 0 ? "固定头像目录已同步" : "暂无可用固定头像，可继续选择微信头像");
        }
    }

    private void RebuildPresetAvatarOptions(AvatarPresetCatalogEntry[] entries)
    {
        presetAvatarOptions.Clear();
        enabledPresetAvatarCodes.Clear();
        for (int i = 0; i < entries.Length; i++)
        {
            AvatarPresetCatalogEntry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.avatarCode))
            {
                continue;
            }

            string avatarCode = entry.avatarCode.Trim();
            if (!entry.enabled || enabledPresetAvatarCodes.Contains(avatarCode))
            {
                continue;
            }

            Sprite sprite = GetPresetAvatarSprite(avatarCode);
            if (sprite == null)
            {
                Debug.LogWarning("[Avatar] Preset sprite is missing for avatar code: " + avatarCode + ".", this);
                continue;
            }

            enabledPresetAvatarCodes.Add(avatarCode);
            presetAvatarOptions.Add(new AvatarPresetOption
            {
                avatarCode = avatarCode,
                displayName = entry.displayName,
                enabled = true,
                sprite = sprite
            });
        }
    }

    private void RefreshPresetAvatarPickerTitle(string error = null)
    {
        if (avatarPresetPickerObject == null)
        {
            return;
        }

        string title = !string.IsNullOrWhiteSpace(error)
            ? "固定头像不可用"
            : avatarPresetCatalogLoading
                ? "正在读取固定头像目录..."
                : presetAvatarOptions.Count > 0
                    ? "选择固定头像"
                    : "暂无可用固定头像";
        SetSceneText(avatarPresetPickerObject.transform, "ProfileAvatarPickerTitle", title);
    }

    private IEnumerator SelectServerPresetAvatar(string avatarCode)
    {
        int requestedPlayerId = GameSession.PlayerId;
        AvatarPresetSelectionRequest payload = new AvatarPresetSelectionRequest { avatarCode = avatarCode };
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
            if (requestedPlayerId != GameSession.PlayerId)
            {
                avatarMutationRunning = false;
                yield break;
            }
            if (HandleUnauthorizedResponse(request))
            {
                avatarMutationRunning = false;
                yield break;
            }

            AvatarApiResponse response = ParseJson<AvatarApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                avatarMutationRunning = false;
                RefreshProfileAvatarPanel(response == null
                    ? ApiRequestUtility.GetErrorMessage(request, "固定头像选择失败")
                    : GetApiError(response));
                yield break;
            }

            string responseAvatarCode = string.IsNullOrWhiteSpace(response.data.avatarCode)
                ? avatarCode
                : response.data.avatarCode;
            SaveCurrentAvatarState(
                response.data.avatarUrl,
                response.data.avatarSource,
                responseAvatarCode,
                response.data.avatarVersion,
                response.data.avatarUpdatedTime);
        }

        avatarMutationRunning = false;
        RefreshCurrentAvatarVisuals();
        RefreshProfileAvatarPanel("固定头像更新成功");
    }

    private void ReconcileCurrentPresetAvatar()
    {
        if (PlayerPrefs.GetInt(AvatarOwnerPlayerIdKey, 0) != GameSession.PlayerId)
        {
            return;
        }

        string resourcePath = string.Equals(GetCurrentAvatarSource(), "PRESET", StringComparison.Ordinal)
            ? ResolvePresetAvatarResourcePathForDisplay(GetCurrentAvatarCode())
            : string.Empty;
        if (string.IsNullOrEmpty(resourcePath))
        {
            PlayerPrefs.DeleteKey(LocalAvatarResourcePathKey);
        }
        else
        {
            PlayerPrefs.SetString(LocalAvatarResourcePathKey, resourcePath);
        }
        PlayerPrefs.Save();
        RefreshCurrentAvatarVisuals();
    }

    private static string GetPresetAvatarResourcePath(string avatarCode)
    {
        switch (avatarCode)
        {
            case DefaultAvatarCode:
                return DefaultAvatarResourcePath;
            case "AVATAR_002":
                return Avatar002ResourcePath;
            case "AVATAR_003":
                return Avatar003ResourcePath;
            default:
                return string.Empty;
        }
    }

    private Sprite GetPresetAvatarSprite(string avatarCode)
    {
        if (string.IsNullOrWhiteSpace(avatarCode))
        {
            return null;
        }

        if (avatarSpriteBindings != null)
        {
            foreach (AvatarSpriteBinding binding in avatarSpriteBindings)
            {
                if (binding != null && binding.sprite != null
                    && string.Equals(binding.avatarCode, avatarCode, StringComparison.Ordinal))
                {
                    return binding.sprite;
                }
            }
        }

        string resourcePath = GetPresetAvatarResourcePath(avatarCode);
        return string.IsNullOrEmpty(resourcePath) ? null : Resources.Load<Sprite>(resourcePath);
    }

    private string ResolvePresetAvatarResourcePathForDisplay(string avatarCode)
    {
        string resourcePath = GetPresetAvatarResourcePath(avatarCode);
        if (string.IsNullOrEmpty(resourcePath)
            || (avatarPresetCatalogLoaded && !enabledPresetAvatarCodes.Contains(avatarCode)))
        {
            return string.Empty;
        }

        return Resources.Load<Sprite>(resourcePath) == null ? string.Empty : resourcePath;
    }

    private static bool TryEncodePresetAvatar(Sprite preset, out byte[] imageBytes, out string error)
    {
        imageBytes = null;
        error = "预置头像读取失败，请重新选择";
        if (preset == null || preset.texture == null)
        {
            return false;
        }

        Texture source = preset.texture;
        Rect sourceRect = preset.textureRect;
        int width = Mathf.Max(1, Mathf.RoundToInt(sourceRect.width));
        int height = Mathf.Max(1, Mathf.RoundToInt(sourceRect.height));
        RenderTexture previous = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        Texture2D cropped = null;
        try
        {
            Graphics.Blit(
                source,
                target,
                new Vector2(sourceRect.width / source.width, sourceRect.height / source.height),
                new Vector2(sourceRect.x / source.width, sourceRect.y / source.height));
            RenderTexture.active = target;
            cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
            cropped.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            cropped.Apply(false, false);
            imageBytes = cropped.EncodeToPNG();
            if (imageBytes == null || imageBytes.Length == 0)
            {
                imageBytes = null;
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            error = "预置头像处理失败：" + exception.Message;
            return false;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            if (cropped != null)
            {
                UnityEngine.Object.Destroy(cropped);
            }
        }
    }

    [Preserve]
    public void OnAvatarSelected(string payloadJson)
    {
        AvatarSelectionPayload payload = null;
        try
        {
            payload = JsonUtility.FromJson<AvatarSelectionPayload>(payloadJson);
        }
        catch (ArgumentException)
        {
            payload = null;
        }

        if (!TryReadAvatarSelection(payload, out byte[] imageBytes, out string readError))
        {
            avatarMutationRunning = false;
            Debug.LogWarning("[Avatar] Selected image could not be read: " + readError);
            RefreshProfileAvatarPanel(readError);
            return;
        }
        Debug.Log("[Avatar] Selected image bytes: " + imageBytes.Length + ".");

        if (!TryPrepareAvatarUpload(imageBytes, out byte[] uploadBytes, out string fileName, out string mimeType, out string prepareError))
        {
            avatarMutationRunning = false;
            Debug.LogWarning("[Avatar] Selected image could not be prepared: " + prepareError);
            RefreshProfileAvatarPanel(prepareError);
            return;
        }

        Debug.Log("[Avatar] Prepared upload bytes: " + uploadBytes.Length + ", mimeType=" + mimeType + ".");
        RefreshProfileAvatarPanel(uploadBytes.Length == imageBytes.Length ? "正在上传头像..." : "图片已自动压缩，正在上传头像...");
        StartCoroutine(UploadCurrentAvatar(uploadBytes, fileName, mimeType));
    }

    [Preserve]
    public void OnAvatarSelectionFailed(string message)
    {
        avatarMutationRunning = false;
        string resolvedMessage = string.IsNullOrWhiteSpace(message) ? "头像选择已取消" : message;
        Debug.LogWarning("[Avatar] Avatar selection did not complete: " + resolvedMessage);
        RefreshProfileAvatarPanel(resolvedMessage);
    }

    [Preserve]
    public void OnWeChatAvatarSelected(string payloadJson)
    {
        OnAvatarSelected(payloadJson);
    }

    [Preserve]
    public void OnWeChatAvatarSelectionFailed(string message)
    {
        OnAvatarSelectionFailed(message);
    }

    private static bool TryReadAvatarSelection(AvatarSelectionPayload payload, out byte[] imageBytes, out string error)
    {
        imageBytes = null;
        error = "头像文件读取失败，请重新选择";
        if (payload == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(payload.base64Data))
        {
            try
            {
                imageBytes = Convert.FromBase64String(payload.base64Data);
            }
            catch (FormatException)
            {
                imageBytes = null;
            }
            if (imageBytes == null || imageBytes.Length == 0)
            {
                return false;
            }
            if (imageBytes.Length > MaximumSourceAvatarBytes)
            {
                imageBytes = null;
                error = "头像源文件超过 16 MB，请重新选择较小图片";
                return false;
            }
            return true;
        }

        if (string.IsNullOrWhiteSpace(payload.filePath))
        {
            return false;
        }

        try
        {
            FileInfo file = new FileInfo(payload.filePath);
            if (!file.Exists)
            {
                error = "所选头像文件不存在，请重新选择";
                return false;
            }
            if (file.Length > MaximumSourceAvatarBytes)
            {
                error = "头像源文件超过 16 MB，请重新选择较小图片";
                return false;
            }

            imageBytes = File.ReadAllBytes(file.FullName);
            return imageBytes.Length > 0;
        }
        catch (Exception exception)
        {
            error = "头像文件读取失败：" + exception.Message;
            return false;
        }
        finally
        {
            if (payload.temporaryFile)
            {
                try
                {
                    File.Delete(payload.filePath);
                }
                catch (Exception)
                {
                }
            }
        }
    }

    private static bool TryDetectAvatarImageType(byte[] imageBytes, out string fileName, out string mimeType)
    {
        bool isPng = imageBytes != null
            && imageBytes.Length >= 8
            && imageBytes[0] == 0x89
            && imageBytes[1] == 0x50
            && imageBytes[2] == 0x4E
            && imageBytes[3] == 0x47
            && imageBytes[4] == 0x0D
            && imageBytes[5] == 0x0A
            && imageBytes[6] == 0x1A
            && imageBytes[7] == 0x0A;
        if (isPng)
        {
            fileName = "avatar.png";
            mimeType = "image/png";
            return true;
        }

        bool isJpeg = imageBytes != null
            && imageBytes.Length >= 3
            && imageBytes[0] == 0xFF
            && imageBytes[1] == 0xD8
            && imageBytes[2] == 0xFF;
        fileName = "avatar.jpg";
        mimeType = "image/jpeg";
        return isJpeg;
    }

    private static bool TryPrepareAvatarUpload(
        byte[] sourceBytes,
        out byte[] uploadBytes,
        out string fileName,
        out string mimeType,
        out string error)
    {
        uploadBytes = null;
        fileName = string.Empty;
        mimeType = string.Empty;
        error = "头像图片处理失败，请重新选择";
        if (!TryDetectAvatarImageType(sourceBytes, out fileName, out mimeType))
        {
            error = "只支持 JPG 或 PNG 格式的头像图片";
            return false;
        }

        Texture2D sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(sourceTexture, sourceBytes, false)
                || sourceTexture.width <= 0
                || sourceTexture.height <= 0)
            {
                error = "所选文件不是可读取的 JPG 或 PNG 图片";
                return false;
            }

            bool serverDimensionsAccepted = sourceTexture.width <= MaximumServerAvatarDimension
                && sourceTexture.height <= MaximumServerAvatarDimension
                && sourceTexture.width * (long)sourceTexture.height <= MaximumServerAvatarPixels;
            if (sourceBytes.Length <= MaximumAvatarBytes && serverDimensionsAccepted)
            {
                uploadBytes = sourceBytes;
                return true;
            }

            float scale = Mathf.Min(
                1f,
                MaximumPreparedAvatarDimension / (float)Mathf.Max(sourceTexture.width, sourceTexture.height));
            int targetWidth = Mathf.Max(1, Mathf.RoundToInt(sourceTexture.width * scale));
            int targetHeight = Mathf.Max(1, Mathf.RoundToInt(sourceTexture.height * scale));
            int[] qualityLevels = { 85, 75, 65, 55, 45 };

            while (true)
            {
                Texture2D candidate = targetWidth == sourceTexture.width && targetHeight == sourceTexture.height
                    ? sourceTexture
                    : ResizeAvatarTexture(sourceTexture, targetWidth, targetHeight);
                try
                {
                    for (int index = 0; index < qualityLevels.Length; index++)
                    {
                        byte[] encoded = ImageConversion.EncodeToJPG(candidate, qualityLevels[index]);
                        if (encoded != null && encoded.Length > 0 && encoded.Length <= MaximumAvatarBytes)
                        {
                            uploadBytes = encoded;
                            fileName = "avatar.jpg";
                            mimeType = "image/jpeg";
                            return true;
                        }
                    }
                }
                finally
                {
                    if (candidate != sourceTexture)
                    {
                        UnityEngine.Object.Destroy(candidate);
                    }
                }

                if (targetWidth <= 256 && targetHeight <= 256)
                {
                    break;
                }
                targetWidth = Mathf.Max(256, targetWidth / 2);
                targetHeight = Mathf.Max(256, targetHeight / 2);
            }

            error = "头像压缩后仍超过 2 MB，请重新选择其他图片";
            return false;
        }
        catch (Exception exception)
        {
            error = "头像图片处理失败：" + exception.Message;
            return false;
        }
        finally
        {
            UnityEngine.Object.Destroy(sourceTexture);
        }
    }

    private static Texture2D ResizeAvatarTexture(Texture2D source, int width, int height)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        try
        {
            source.filterMode = FilterMode.Bilinear;
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            Texture2D resized = new Texture2D(width, height, TextureFormat.RGB24, false);
            resized.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            resized.Apply(false, false);
            return resized;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private IEnumerator UploadCurrentAvatar(byte[] imageBytes, string fileName, string mimeType)
    {
        if (!ApiRuntimeConfig.TryBuildUrl("/user/avatar", out string url, out string error))
        {
            avatarMutationRunning = false;
            RefreshProfileAvatarPanel(error);
            yield break;
        }

        Debug.Log("[Avatar] Uploading " + imageBytes.Length + " bytes to " + url + ".");

        List<IMultipartFormSection> sections = new List<IMultipartFormSection>
        {
            new MultipartFormFileSection("file", imageBytes, fileName, mimeType)
        };
        byte[] boundary = UnityWebRequest.GenerateBoundary();
        byte[] body = UnityWebRequest.SerializeFormSections(sections, boundary);
        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 20;
            request.SetRequestHeader("Authorization", "Bearer " + GameSession.Token);
            request.SetRequestHeader("X-Client-Channel", ApiRuntimeConfig.GetClientChannel());
            request.SetRequestHeader("X-Client-Version-Code", ApiRuntimeConfig.GetClientVersionCode().ToString(CultureInfo.InvariantCulture));
            request.SetRequestHeader("Content-Type", "multipart/form-data; boundary=" + Encoding.UTF8.GetString(boundary));
            yield return request.SendWebRequest();

            Debug.Log("[Avatar] Upload response: " + GetAvatarRequestDiagnostics(request));
            if (ApiRequestUtility.IsUnauthorized(request))
            {
                avatarMutationRunning = false;
                HandleUnauthorizedResponse(request);
                yield break;
            }

            AvatarApiResponse response = ParseJson<AvatarApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                avatarMutationRunning = false;
                RefreshProfileAvatarPanel(response != null && !string.IsNullOrWhiteSpace(response.message)
                    ? response.message
                    : ApiRequestUtility.GetErrorMessage(request, "头像上传失败"));
                yield break;
            }

            SaveCurrentAvatarState(
                response.data.avatarUrl,
                response.data.avatarSource,
                response.data.avatarCode,
                response.data.avatarVersion,
                response.data.avatarUpdatedTime);
            Debug.Log("[Avatar] Upload succeeded. avatarVersion=" + response.data.avatarVersion + ".");
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
            if (ApiRequestUtility.IsUnauthorized(request))
            {
                avatarMutationRunning = false;
                HandleUnauthorizedResponse(request);
                yield break;
            }
            AvatarApiResponse response = ParseJson<AvatarApiResponse>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || response == null || response.code != 200 || response.data == null)
            {
                avatarMutationRunning = false;
                RefreshProfileAvatarPanel(response != null && !string.IsNullOrWhiteSpace(response.message)
                    ? response.message
                    : ApiRequestUtility.GetErrorMessage(request, "恢复默认头像失败"));
                yield break;
            }

            SaveCurrentAvatarState(
                response.data.avatarUrl,
                response.data.avatarSource,
                response.data.avatarCode,
                response.data.avatarVersion,
                response.data.avatarUpdatedTime);
        }

        ClearLocalPresetAvatar();
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

    private void SaveCurrentAvatarState(
        string avatarUrl,
        string avatarSource,
        string avatarCode,
        long avatarVersion,
        string avatarUpdatedTime)
    {
        int playerId = GameSession.PlayerId;
        bool belongsToCurrentPlayer = playerId > 0
            && PlayerPrefs.GetInt(AvatarOwnerPlayerIdKey, 0) == playerId;
        long currentVersion = GetCurrentAvatarVersion();
        if (belongsToCurrentPlayer && avatarVersion < currentVersion)
        {
            Debug.LogWarning("[Avatar] Ignored stale avatar response version " + avatarVersion + ", current version is " + currentVersion + ".");
            return;
        }

        string resolvedSource = string.IsNullOrWhiteSpace(avatarSource) ? "DEFAULT" : avatarSource.Trim();
        string resolvedCode = string.Equals(resolvedSource, "PRESET", StringComparison.Ordinal)
            ? (avatarCode ?? string.Empty).Trim()
            : string.Empty;
        PlayerPrefs.SetInt(AvatarOwnerPlayerIdKey, playerId);
        PlayerPrefs.SetString("PlayerAvatarUrl", avatarUrl ?? string.Empty);
        PlayerPrefs.SetString("PlayerAvatarSource", resolvedSource);
        PlayerPrefs.SetString(AvatarCodeKey, resolvedCode);
        PlayerPrefs.SetString("PlayerAvatarVersion", Math.Max(0L, avatarVersion).ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.SetString("PlayerAvatarUpdatedTime", avatarUpdatedTime ?? string.Empty);
        string resourcePath = string.Equals(resolvedSource, "PRESET", StringComparison.Ordinal)
            ? ResolvePresetAvatarResourcePathForDisplay(resolvedCode)
            : string.Empty;
        if (string.IsNullOrEmpty(resourcePath))
        {
            PlayerPrefs.DeleteKey(LocalAvatarResourcePathKey);
        }
        else
        {
            PlayerPrefs.SetString(LocalAvatarResourcePathKey, resourcePath);
        }
        PlayerPrefs.Save();
        RefreshCurrentAvatarVisuals();
        if (string.Equals(resolvedSource, "PRESET", StringComparison.Ordinal)
            && !avatarPresetCatalogLoaded
            && !avatarPresetCatalogLoading
            && isActiveAndEnabled)
        {
            StartCoroutine(RefreshPresetAvatarCatalog(false));
        }
    }

    private void ClearLocalPresetAvatar()
    {
        PlayerPrefs.DeleteKey(LocalAvatarResourcePathKey);
        PlayerPrefs.Save();
    }

    private string GetCurrentAvatarResourcePath()
    {
        if (PlayerPrefs.GetInt(AvatarOwnerPlayerIdKey, 0) != GameSession.PlayerId)
        {
            return string.Empty;
        }

        return string.Equals(GetCurrentAvatarSource(), "LOCAL_PRESET", StringComparison.Ordinal)
            ? PlayerPrefs.GetString(LocalAvatarResourcePathKey, string.Empty)
            : string.Empty;
    }

    private string GetCurrentAvatarUrl()
    {
        if (PlayerPrefs.GetInt(AvatarOwnerPlayerIdKey, 0) != GameSession.PlayerId)
        {
            return string.Empty;
        }
        return PlayerPrefs.GetString("PlayerAvatarUrl", string.Empty);
    }

    private string GetCurrentAvatarSource()
    {
        if (PlayerPrefs.GetInt(AvatarOwnerPlayerIdKey, 0) != GameSession.PlayerId)
        {
            return "DEFAULT";
        }
        return PlayerPrefs.GetString("PlayerAvatarSource", "DEFAULT");
    }

    private string GetCurrentAvatarCode()
    {
        if (PlayerPrefs.GetInt(AvatarOwnerPlayerIdKey, 0) != GameSession.PlayerId)
        {
            return string.Empty;
        }
        return PlayerPrefs.GetString(AvatarCodeKey, string.Empty);
    }

    private long GetCurrentAvatarVersion()
    {
        if (PlayerPrefs.GetInt(AvatarOwnerPlayerIdKey, 0) != GameSession.PlayerId)
        {
            return 0L;
        }
        return long.TryParse(PlayerPrefs.GetString("PlayerAvatarVersion", "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long version)
            ? Math.Max(0L, version)
            : 0L;
    }

    private static string GetPlayerAvatarStateKeyPrefix(long playerId)
    {
        return PlayerAvatarStateKeyPrefix + playerId + ".";
    }

    private void RefreshCurrentAvatarVisuals()
    {
        if (mainPageTransform != null)
        {
            Transform headTransform = mainPageTransform.Find(
                "BookAvatarFrame/BookAvatarPortrait/BookAvatarHead");
            Image head = headTransform != null ? headTransform.GetComponent<Image>() : null;
            if (head != null)
            {
                ApplyAvatarImage(
                    head,
                    null,
                    GameSession.PlayerId,
                    GetCurrentAvatarUrl(),
                    GetCurrentAvatarCode(),
                    GetCurrentAvatarVersion());
            }
            else
            {
                Debug.LogWarning(
                    "[Avatar] Home avatar target BookAvatarFrame/BookAvatarPortrait/BookAvatarHead was not found.",
                    this);
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

        if (!TryCreateAuthorizedPost(
                "/friends/avatars/batch",
                JsonUtility.ToJson(payload),
                out UnityWebRequest request,
                out string error))
        {
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

                friend.avatarUrl = snapshot.avatarUrl;
                friend.avatarCode = snapshot.avatarCode;
                friend.avatarVersion = snapshot.avatarVersion;
            }
        }
    }

    private void ApplyAvatarImage(
        Image image,
        Component fallbackText,
        int playerId,
        string avatarUrl,
        string avatarCode,
        long avatarVersion,
        bool preserveSceneStyle = false)
    {
        if (image == null)
        {
            return;
        }

        AvatarImageState state = GetAvatarImageState(image);
        state.preserveSceneStyle = preserveSceneStyle;
        if (!string.IsNullOrWhiteSpace(avatarCode))
        {
            string resolvedCode = avatarCode.Trim();
            Sprite presetSprite = !avatarPresetCatalogLoaded || enabledPresetAvatarCodes.Contains(resolvedCode)
                ? GetPresetAvatarSprite(resolvedCode)
                : null;
            if (presetSprite != null)
            {
                state.requestKey = "preset:" + resolvedCode;
                SetLoadedAvatar(image, fallbackText, presetSprite, state);
                return;
            }

            ApplyBundledDefaultAvatar(image, fallbackText, state);
            return;
        }

        string localResourcePath = playerId == GameSession.PlayerId
            ? GetCurrentAvatarResourcePath()
            : string.Empty;
        if (!string.IsNullOrEmpty(localResourcePath))
        {
            Sprite localSprite = Resources.Load<Sprite>(localResourcePath);
            if (localSprite != null)
            {
                state.requestKey = "local:" + localResourcePath;
                SetLoadedAvatar(image, fallbackText, localSprite, state);
                return;
            }

            Debug.LogWarning("[Avatar] Local avatar resource was not found: Resources/" + localResourcePath + ".", this);
        }

        string resolvedUrl = ResolveAvatarUrl(avatarUrl);
        if (string.IsNullOrEmpty(resolvedUrl))
        {
            ApplyBundledDefaultAvatar(image, fallbackText, state);
            return;
        }

        string key = playerId + ":" + avatarVersion.ToString(CultureInfo.InvariantCulture) + ":" + resolvedUrl;
        state.requestKey = key;
        if (avatarSpriteCache.TryGetValue(key, out Sprite cachedSprite) && cachedSprite != null)
        {
            SetLoadedAvatar(image, fallbackText, cachedSprite, state);
            return;
        }

        StartCoroutine(DownloadAvatarSprite(image, fallbackText, key, resolvedUrl));
    }

    private void ApplyBundledDefaultAvatar(Image image, Component fallbackText, AvatarImageState state)
    {
        Sprite defaultAvatar = GetPresetAvatarSprite(DefaultAvatarCode);
        if (defaultAvatar != null)
        {
            state.requestKey = "default:" + DefaultAvatarCode;
            SetLoadedAvatar(image, fallbackText, defaultAvatar, state);
            return;
        }

        state.requestKey = string.Empty;
        image.sprite = state.defaultSprite;
        if (!state.preserveSceneStyle)
        {
            image.color = state.defaultColor;
            image.preserveAspect = state.defaultPreserveAspect;
        }
        if (fallbackText != null)
        {
            fallbackText.gameObject.SetActive(true);
        }
    }

    private IEnumerator DownloadAvatarSprite(Image image, Component fallbackText, string requestKey, string url)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, true))
        {
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[Avatar] Avatar image download failed: " + GetAvatarRequestDiagnostics(request));
                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null)
            {
                yield break;
            }

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            if (avatarSpriteCache.TryGetValue(requestKey, out Sprite existing) && existing != null)
            {
                Destroy(sprite);
                Destroy(texture);
                sprite = existing;
            }
            else
            {
                avatarSpriteCache[requestKey] = sprite;
            }

            if (image != null
                && avatarImageStates.TryGetValue(image, out AvatarImageState state)
                && state.requestKey == requestKey)
            {
                SetLoadedAvatar(image, fallbackText, sprite, state);
            }
        }
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

    private static void SetLoadedAvatar(Image image, Component fallbackText, Sprite sprite, AvatarImageState state)
    {
        image.sprite = sprite;
        if (!state.preserveSceneStyle)
        {
            image.color = Color.white;
            image.preserveAspect = true;
        }
        if (fallbackText != null)
        {
            fallbackText.gameObject.SetActive(false);
        }
    }

    private static string ResolveAvatarUrl(string avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
        {
            return string.Empty;
        }
        if (Uri.TryCreate(avatarUrl, UriKind.Absolute, out Uri absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute.AbsoluteUri;
        }
        if (!ApiRuntimeConfig.TryGetBaseUrl(out string baseUrl, out string error)
            || !Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri apiUri))
        {
            return string.Empty;
        }

        Uri origin = new Uri(apiUri.GetLeftPart(UriPartial.Authority) + "/");
        return Uri.TryCreate(origin, avatarUrl, out Uri resolved) ? resolved.AbsoluteUri : string.Empty;
    }

    private static string GetAvatarRequestDiagnostics(UnityWebRequest request)
    {
        if (request == null)
        {
            return "request is null";
        }

        string body = request.downloadHandler == null ? string.Empty : request.downloadHandler.text;
        if (body.Length > 500)
        {
            body = body.Substring(0, 500) + "...";
        }
        return "HTTP " + request.responseCode
            + ", result=" + request.result
            + ", error=" + (request.error ?? string.Empty)
            + ", body=" + body;
    }

    private void DisposeAvatarCache()
    {
        foreach (Sprite sprite in avatarSpriteCache.Values)
        {
            if (sprite == null)
            {
                continue;
            }
            Texture texture = sprite.texture;
            Destroy(sprite);
            if (texture != null)
            {
                Destroy(texture);
            }
        }
        avatarSpriteCache.Clear();
        avatarImageStates.Clear();
    }

    [Serializable]
    private class AvatarPresetCatalogApiResponse : BasicApiResponse
    {
        public AvatarPresetCatalogEntry[] data;
    }

    [Serializable]
    private class AvatarPresetCatalogEntry
    {
        public string avatarCode;
        public string displayName;
        public bool enabled;
    }

    [Serializable]
    private class AvatarPresetSelectionRequest
    {
        public string avatarCode;
    }

    [Serializable]
    private sealed class AvatarSpriteBinding
    {
        public string avatarCode;
        public Sprite sprite;
    }

    private sealed class AvatarPresetOption
    {
        public string avatarCode;
        public string displayName;
        public bool enabled;
        public Sprite sprite;
    }

    [Serializable]
    private class AvatarSelectionPayload
    {
        public string filePath;
        public string fileName;
        public string mimeType;
        public string base64Data;
        public bool temporaryFile;
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
        public string avatarUrl;
        public string avatarCode;
        public long avatarVersion;
    }

    private sealed class AvatarImageState
    {
        public Sprite defaultSprite;
        public Color defaultColor;
        public bool defaultPreserveAspect;
        public bool preserveSceneStyle;
        public string requestKey;
    }
}
