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
    private const string PresetAvatarResourcesPath = "UI/AvatarPresets";
    private const string AvatarOwnerPlayerIdKey = "PlayerAvatarOwnerId";
    private readonly Dictionary<string, Sprite> avatarSpriteCache = new Dictionary<string, Sprite>();
    private readonly Dictionary<Image, AvatarImageState> avatarImageStates = new Dictionary<Image, AvatarImageState>();
    private readonly List<Sprite> presetAvatarSprites = new List<Sprite>();
    private GameObject avatarPresetPickerObject;
    private bool avatarMutationRunning;

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

        chooseButton.gameObject.SetActive(true);
        resetButton.gameObject.SetActive(isAuthenticated);
        chooseButton.interactable = !avatarMutationRunning;
        resetButton.interactable = !avatarMutationRunning && !string.IsNullOrEmpty(GetCurrentAvatarUrl());
        SetSceneButtonText(chooseButton, avatarMutationRunning ? "处理中" : "选择头像");
        BindSceneButton(chooseButton, BeginAvatarSelection);
        BindSceneButton(resetButton, () => StartCoroutine(ResetCurrentAvatar()));

        ApplyAvatarImage(
            avatar,
            FindSceneComponent<TMP_Text>(root, "ProfileAvatarInitial"),
            GameSession.PlayerId,
            GetCurrentAvatarUrl(),
            GetCurrentAvatarVersion());
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

        Sprite[] presets = LoadPresetAvatarSprites();
        if (presets.Length == 0)
        {
            Debug.LogWarning("[Avatar] No preset avatar sprites were found in Resources/" + PresetAvatarResourcesPath + ".", this);
            RefreshProfileAvatarPanel("未配置预置头像，请将图片放入 Resources/UI/AvatarPresets");
            return;
        }

        avatarPresetPickerObject = EnsurePresetAvatarPicker(panel.PanelRoot);
        if (avatarPresetPickerObject == null)
        {
            RefreshProfileAvatarPanel("头像选择面板创建失败");
            return;
        }

        BuildPresetAvatarOptions(avatarPresetPickerObject.transform, presets);
        avatarPresetPickerObject.SetActive(true);
        avatarPresetPickerObject.transform.SetAsLastSibling();
        SetStatus("请选择一个预置头像");
    }

    private Sprite[] LoadPresetAvatarSprites()
    {
        presetAvatarSprites.Clear();
        Sprite[] loadedSprites = Resources.LoadAll<Sprite>(PresetAvatarResourcesPath);
        for (int i = 0; i < loadedSprites.Length; i++)
        {
            if (loadedSprites[i] != null && !presetAvatarSprites.Contains(loadedSprites[i]))
            {
                presetAvatarSprites.Add(loadedSprites[i]);
            }
        }

        return presetAvatarSprites.ToArray();
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

    private void BuildPresetAvatarOptions(Transform pickerRoot, Sprite[] presets)
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

        for (int i = 0; i < presets.Length; i++)
        {
            Sprite preset = presets[i];
            if (preset == null)
            {
                continue;
            }

            Image option = CreateImage("ProfileAvatarPreset" + i, grid, Color.white);
            option.sprite = preset;
            option.preserveAspect = true;
            option.raycastTarget = true;
            Button optionButton = option.gameObject.AddComponent<Button>();
            optionButton.targetGraphic = option;
            int selectedIndex = i;
            BindSceneButton(optionButton, () => SelectPresetAvatar(preset, selectedIndex));
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

    private void SelectPresetAvatar(Sprite preset, int presetIndex)
    {
        if (avatarMutationRunning || preset == null)
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

        if (!TryEncodePresetAvatar(preset, out byte[] imageBytes, out string encodeError))
        {
            Debug.LogWarning("[Avatar] Preset avatar could not be encoded: " + encodeError, preset);
            RefreshProfileAvatarPanel(encodeError);
            return;
        }

        if (!TryPrepareAvatarUpload(imageBytes, out byte[] uploadBytes, out string fileName, out string mimeType, out string prepareError))
        {
            Debug.LogWarning("[Avatar] Preset avatar could not be prepared: " + prepareError, preset);
            RefreshProfileAvatarPanel(prepareError);
            return;
        }

        avatarMutationRunning = true;
        HidePresetAvatarPicker();
        RefreshProfileAvatarPanel("正在上传预置头像...");
        Debug.Log("[Avatar] Uploading preset avatar " + presetIndex + ".");
        StartCoroutine(UploadCurrentAvatar(uploadBytes, fileName, mimeType));
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

            SaveCurrentAvatarState(response.data.avatarUrl, response.data.avatarSource, response.data.avatarVersion, response.data.avatarUpdatedTime);
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

            SaveCurrentAvatarState(response.data.avatarUrl, response.data.avatarSource, response.data.avatarVersion, response.data.avatarUpdatedTime);
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

    private void SaveCurrentAvatarState(string avatarUrl, string avatarSource, long avatarVersion, string avatarUpdatedTime)
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

        PlayerPrefs.SetInt(AvatarOwnerPlayerIdKey, playerId);
        PlayerPrefs.SetString("PlayerAvatarUrl", avatarUrl ?? string.Empty);
        PlayerPrefs.SetString("PlayerAvatarSource", string.IsNullOrWhiteSpace(avatarSource) ? "DEFAULT" : avatarSource);
        PlayerPrefs.SetString("PlayerAvatarVersion", Math.Max(0L, avatarVersion).ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.SetString("PlayerAvatarUpdatedTime", avatarUpdatedTime ?? string.Empty);
        PlayerPrefs.Save();
        RefreshCurrentAvatarVisuals();
    }

    private string GetCurrentAvatarUrl()
    {
        if (PlayerPrefs.GetInt(AvatarOwnerPlayerIdKey, 0) != GameSession.PlayerId)
        {
            return string.Empty;
        }
        return PlayerPrefs.GetString("PlayerAvatarUrl", string.Empty);
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

    private void RefreshCurrentAvatarVisuals()
    {
        if (mainPageTransform != null)
        {
            Image head = FindSceneComponent<Image>(mainPageTransform, "BookAvatarPortrait/BookAvatarHead");
            if (head != null)
            {
                ApplyAvatarImage(head, null, GameSession.PlayerId, GetCurrentAvatarUrl(), GetCurrentAvatarVersion());
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
                friend.avatarVersion = snapshot.avatarVersion;
            }
        }
    }

    private void ApplyAvatarImage(Image image, Component fallbackText, int playerId, string avatarUrl, long avatarVersion)
    {
        if (image == null)
        {
            return;
        }

        AvatarImageState state = GetAvatarImageState(image);
        string resolvedUrl = ResolveAvatarUrl(avatarUrl);
        if (string.IsNullOrEmpty(resolvedUrl))
        {
            state.requestKey = string.Empty;
            image.sprite = state.defaultSprite;
            image.color = state.defaultColor;
            image.preserveAspect = state.defaultPreserveAspect;
            if (fallbackText != null)
            {
                fallbackText.gameObject.SetActive(true);
            }
            return;
        }

        string key = playerId + ":" + avatarVersion.ToString(CultureInfo.InvariantCulture) + ":" + resolvedUrl;
        state.requestKey = key;
        if (avatarSpriteCache.TryGetValue(key, out Sprite cachedSprite) && cachedSprite != null)
        {
            SetLoadedAvatar(image, fallbackText, cachedSprite);
            return;
        }

        StartCoroutine(DownloadAvatarSprite(image, fallbackText, key, resolvedUrl));
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
                SetLoadedAvatar(image, fallbackText, sprite);
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
        public long avatarVersion;
    }

    private sealed class AvatarImageState
    {
        public Sprite defaultSprite;
        public Color defaultColor;
        public bool defaultPreserveAspect;
        public string requestKey;
    }
}
