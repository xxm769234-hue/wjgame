#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reads the published avatar catalog, creates the Unity resource evidence file,
/// and optionally reports that evidence to the avatar catalog endpoint.
/// </summary>
public static class AvatarCatalogManifestReporter
{
    private const string RuntimeApiConfigPath = "Assets/_Imported/weijun-game-project/Assets/Resources/ApiConfig.json";
    private const string OutputDirectory = "Temp/AvatarCatalogManifests";
    private const string DefaultPlatform = "WECHAT_MINIGAME";
    private const string DefaultChannel = "DEFAULT";
    private const int RequestTimeoutMilliseconds = 15000;

    /// <summary>
    /// Reads the server catalog and writes a manifest without uploading it.
    /// </summary>
    [MenuItem("Tools/WeiJun Avatar/Generate Catalog Manifest")]
    public static void GenerateManifest()
    {
        int exitCode = Execute(false);
        ShowResult(exitCode == 0 ? "头像清单已生成" : "头像清单生成失败", exitCode == 0);
    }

    /// <summary>
    /// Reads the server catalog, writes the manifest, and reports it to the server.
    /// </summary>
    [MenuItem("Tools/WeiJun Avatar/Report Catalog Manifest")]
    public static void ReportManifest()
    {
        int exitCode = Execute(true);
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(exitCode);
            return;
        }

        ShowResult(exitCode == 0 ? "头像清单已上报" : "头像清单上报失败", exitCode == 0);
    }

    private static int Execute(bool upload)
    {
        try
        {
            RuntimeApiConfig config = LoadRuntimeApiConfig();
            string token = GetRequiredEnvironment("WEIJUN_CONFIG_ADMIN_TOKEN", "CONFIG_ADMIN_TOKEN");
            string baseUrl = GetApiBaseUrl(config);
            string platform = GetEnvironmentOrDefault("WEIJUN_AVATAR_PLATFORM", DefaultPlatform).ToUpperInvariant();
            string channelCode = GetEnvironmentOrDefault(
                "WEIJUN_AVATAR_CHANNEL_CODE",
                string.IsNullOrWhiteSpace(config.clientChannel) ? DefaultChannel : config.clientChannel).ToUpperInvariant();
            long clientVersionCode = GetClientVersionCode(config);
            string clientVersionName = GetEnvironmentOrDefault("WEIJUN_CLIENT_VERSION_NAME", Application.version);
            string manifestVersion = GetEnvironmentOrDefault(
                "WEIJUN_AVATAR_MANIFEST_VERSION",
                BuildDefaultManifestVersion(platform, channelCode, clientVersionCode));

            OpsCatalogResponse catalogResponse = ReadPublishedCatalog(baseUrl, token);
            CatalogPayload currentPayload = GetCurrentPayload(catalogResponse);
            if (HasDraftPayload(catalogResponse))
            {
                throw new InvalidOperationException("头像目录存在未发布草稿，请先发布或丢弃草稿后再验收。");
            }

            List<ManifestItem> items = BuildManifestItems(currentPayload, clientVersionCode);
            if (items.Count == 0)
            {
                throw new InvalidOperationException("当前客户端没有可生成清单的头像资源。");
            }

            ManifestEvidence evidence = new ManifestEvidence
            {
                schemaVersion = 1,
                platform = platform,
                channelCode = channelCode,
                clientVersionCode = clientVersionCode,
                clientVersionName = clientVersionName,
                manifestVersion = manifestVersion,
                items = items.ToArray()
            };

            string evidenceJson = JsonUtility.ToJson(evidence, true);
            string manifestSha256 = ComputeSha256(Encoding.UTF8.GetBytes(evidenceJson));
            ManifestPayload manifest = new ManifestPayload
            {
                schemaVersion = evidence.schemaVersion,
                platform = evidence.platform,
                channelCode = evidence.channelCode,
                clientVersionCode = evidence.clientVersionCode,
                clientVersionName = evidence.clientVersionName,
                manifestVersion = evidence.manifestVersion,
                manifestSha256 = manifestSha256,
                items = evidence.items
            };

            string outputDirectory = GetProjectPath(OutputDirectory);
            Directory.CreateDirectory(outputDirectory);
            string evidencePath = Path.Combine(outputDirectory, manifestVersion + ".evidence.json");
            string requestPath = Path.Combine(outputDirectory, manifestVersion + ".request.json");
            WriteUtf8(evidencePath, evidenceJson);
            WriteUtf8(requestPath, JsonUtility.ToJson(manifest, true));

            Debug.Log(
                "[AvatarManifest] Generated " + items.Count + " item(s), version "
                + clientVersionCode.ToString(CultureInfo.InvariantCulture)
                + ", evidence: " + evidencePath);

            if (!upload)
            {
                return 0;
            }

            ReportToServer(baseUrl, token, manifest);
            return 0;
        }
        catch (Exception exception)
        {
            Debug.LogError("[AvatarManifest] " + exception.Message + "\n" + exception.StackTrace);
            return 1;
        }
    }

    private static RuntimeApiConfig LoadRuntimeApiConfig()
    {
        string configPath = GetProjectPath(RuntimeApiConfigPath);
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("找不到运行时 API 配置文件", configPath);
        }

        string json = File.ReadAllText(configPath, Encoding.UTF8);
        RuntimeApiConfig config = JsonUtility.FromJson<RuntimeApiConfig>(json);
        if (config == null)
        {
            throw new InvalidOperationException("ApiConfig.json 无法解析。");
        }

        return config;
    }

    private static string GetApiBaseUrl(RuntimeApiConfig config)
    {
        string environmentUrl = Environment.GetEnvironmentVariable("WEIJUN_API_BASE_URL");
        string baseUrl = string.IsNullOrWhiteSpace(environmentUrl)
            ? config.developmentBaseUrl
            : environmentUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("未配置 WEIJUN_API_BASE_URL，且 ApiConfig.json 没有 developmentBaseUrl。");
        }

        return baseUrl.TrimEnd('/');
    }

    private static long GetClientVersionCode(RuntimeApiConfig config)
    {
        string environmentVersion = Environment.GetEnvironmentVariable("WEIJUN_CLIENT_VERSION_CODE");
        if (!string.IsNullOrWhiteSpace(environmentVersion))
        {
            if (!long.TryParse(environmentVersion, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedVersion)
                || parsedVersion <= 0L)
            {
                throw new InvalidOperationException("WEIJUN_CLIENT_VERSION_CODE 必须是正整数。");
            }

            return parsedVersion;
        }

        if (config.clientVersionCode <= 0L)
        {
            throw new InvalidOperationException("ApiConfig.json 的 clientVersionCode 必须是正整数。");
        }

        return config.clientVersionCode;
    }

    private static string GetRequiredEnvironment(string primaryName, string fallbackName)
    {
        string value = Environment.GetEnvironmentVariable(primaryName);
        if (string.IsNullOrWhiteSpace(value))
        {
            value = Environment.GetEnvironmentVariable(fallbackName);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "未配置 " + primaryName + "。清单验收凭据只能放在 Editor/CI 环境变量中，不能写入项目。");
        }

        return value.Trim();
    }

    private static string GetEnvironmentOrDefault(string name, string fallback)
    {
        string value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string BuildDefaultManifestVersion(string platform, string channelCode, long clientVersionCode)
    {
        return platform.ToLowerInvariant().Replace('_', '-')
            + "-" + channelCode.ToLowerInvariant().Replace('_', '-')
            + "-" + clientVersionCode.ToString(CultureInfo.InvariantCulture);
    }

    private static OpsCatalogResponse ReadPublishedCatalog(string baseUrl, string token)
    {
        HttpResult result = SendHttpRequest(
            CombineUrl(baseUrl, "/ops/config/AVATAR_CATALOG/PRESETS"),
            "GET",
            token,
            null);
        EnsureHttpSuccess(result, "读取头像正式目录");

        OpsCatalogResponse response = JsonUtility.FromJson<OpsCatalogResponse>(result.body);
        if (response == null || response.code != 200)
        {
            throw new InvalidOperationException(
                "读取头像正式目录失败：" + GetResponseMessage(response == null ? null : response.message, result.body));
        }

        return response;
    }

    private static CatalogPayload GetCurrentPayload(OpsCatalogResponse response)
    {
        CatalogPayload currentPayload = response.currentPayload;
        if (response.data != null && response.data.currentPayload != null)
        {
            currentPayload = response.data.currentPayload;
        }

        if (currentPayload == null || currentPayload.entries == null)
        {
            throw new InvalidOperationException("头像正式目录缺少 currentPayload.entries。");
        }

        return currentPayload;
    }

    private static bool HasDraftPayload(OpsCatalogResponse response)
    {
        if (response.draftPayload != null)
        {
            return true;
        }

        return response.data != null && response.data.draftPayload != null;
    }

    private static List<ManifestItem> BuildManifestItems(CatalogPayload payload, long clientVersionCode)
    {
        List<ManifestItem> items = new List<ManifestItem>();
        HashSet<string> seenCodes = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < payload.entries.Length; index++)
        {
            AvatarCatalogEntry entry = payload.entries[index];
            if (entry == null || string.IsNullOrWhiteSpace(entry.avatarCode))
            {
                continue;
            }

            string avatarCode = entry.avatarCode.Trim();
            if (!seenCodes.Add(avatarCode))
            {
                throw new InvalidOperationException("头像目录包含重复编号：" + avatarCode);
            }

            string status = string.IsNullOrWhiteSpace(entry.status)
                ? string.Empty
                : entry.status.Trim().ToUpperInvariant();
            bool isActiveOrRetired = status == "ACTIVE" || status == "RETIRED";
            bool isStaged = status == "STAGED";
            if (!isActiveOrRetired && !isStaged)
            {
                continue;
            }

            if (isActiveOrRetired && entry.minimumClientVersionCode > clientVersionCode)
            {
                continue;
            }

            string assetPath = FindResourceAsset(entry.unityResourcePath);
            if (string.IsNullOrEmpty(assetPath))
            {
                if (isStaged)
                {
                    Debug.LogWarning(
                        "[AvatarManifest] 跳过尚未进入本次包的 STAGED 头像：" + avatarCode);
                    continue;
                }

                throw new FileNotFoundException(
                    "ACTIVE/RETIRED 头像缺少本地资源：" + avatarCode + " -> " + entry.unityResourcePath);
            }

            string fullPath = GetProjectPath(assetPath);
            items.Add(new ManifestItem
            {
                avatarCode = avatarCode,
                unityResourcePath = NormalizePath(entry.unityResourcePath),
                resourceSha256 = ComputeSha256(fullPath)
            });
        }

        SortItems(items);
        return items;
    }

    private static string FindResourceAsset(string unityResourcePath)
    {
        string normalizedResourcePath = NormalizePath(unityResourcePath);
        if (!normalizedResourcePath.StartsWith("Resources/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "头像资源路径必须以 Resources/ 开头：" + unityResourcePath);
        }

        string relativePath = normalizedResourcePath.Substring("Resources/".Length);
        string expectedWithoutExtension = RemoveExtension(relativePath);
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D");
        for (int index = 0; index < textureGuids.Length; index++)
        {
            string assetPath = NormalizePath(AssetDatabase.GUIDToAssetPath(textureGuids[index]));
            int resourcesMarkerIndex = assetPath.IndexOf("/Resources/", StringComparison.Ordinal);
            if (resourcesMarkerIndex < 0)
            {
                continue;
            }

            string assetResourcePath = assetPath.Substring(resourcesMarkerIndex + "/Resources/".Length);
            if (!string.Equals(RemoveExtension(assetResourcePath), expectedWithoutExtension, StringComparison.Ordinal))
            {
                continue;
            }

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null || importer.textureType != TextureImporterType.Sprite)
            {
                throw new InvalidOperationException(
                    "头像资源必须使用 Sprite 导入类型：" + assetPath);
            }

            if (AssetDatabase.LoadAssetAtPath<Sprite>(assetPath) == null)
            {
                throw new InvalidOperationException(
                    "头像资源无法加载为 Sprite：" + assetPath);
            }

            return assetPath;
        }

        return string.Empty;
    }

    private static void SortItems(List<ManifestItem> items)
    {
        for (int index = 1; index < items.Count; index++)
        {
            ManifestItem current = items[index];
            int compareIndex = index - 1;
            while (compareIndex >= 0
                && string.CompareOrdinal(items[compareIndex].avatarCode, current.avatarCode) > 0)
            {
                items[compareIndex + 1] = items[compareIndex];
                compareIndex--;
            }

            items[compareIndex + 1] = current;
        }
    }

    private static void ReportToServer(string baseUrl, string token, ManifestPayload manifest)
    {
        HttpResult result = SendHttpRequest(
            CombineUrl(baseUrl, "/ops/config/avatar-presets/manifests/report"),
            "POST",
            token,
            JsonUtility.ToJson(manifest, true));
        EnsureHttpSuccess(result, "上报头像资源清单");

        BasicResponse response = JsonUtility.FromJson<BasicResponse>(result.body);
        if (response == null || response.code != 200)
        {
            throw new InvalidOperationException(
                "上报头像资源清单失败：" + GetResponseMessage(response == null ? null : response.message, result.body));
        }

        Debug.Log("[AvatarManifest] Server accepted the avatar resource manifest: " + result.body);
    }

    private static HttpResult SendHttpRequest(string url, string method, string token, string body)
    {
        HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
        if (request == null)
        {
            throw new InvalidOperationException("无法创建 HTTP 请求：" + url);
        }

        request.Method = method;
        request.Timeout = RequestTimeoutMilliseconds;
        request.ReadWriteTimeout = RequestTimeoutMilliseconds;
        request.Accept = "application/json";
        request.Headers["Authorization"] = "Bearer " + token;
        if (!string.IsNullOrEmpty(body))
        {
            request.ContentType = "application/json; charset=UTF-8";
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            request.ContentLength = bodyBytes.Length;
            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(bodyBytes, 0, bodyBytes.Length);
            }
        }

        try
        {
            using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
            {
                return new HttpResult
                {
                    statusCode = response == null ? 0 : (int)response.StatusCode,
                    body = response == null ? string.Empty : ReadStream(response.GetResponseStream())
                };
            }
        }
        catch (WebException exception)
        {
            HttpWebResponse errorResponse = exception.Response as HttpWebResponse;
            if (errorResponse == null)
            {
                return new HttpResult { statusCode = 0, body = exception.Message };
            }

            using (errorResponse)
            {
                return new HttpResult
                {
                    statusCode = (int)errorResponse.StatusCode,
                    body = ReadStream(errorResponse.GetResponseStream())
                };
            }
        }
    }

    private static void EnsureHttpSuccess(HttpResult result, string operation)
    {
        if (result.statusCode >= 200 && result.statusCode < 300)
        {
            return;
        }

        string statusText = result.statusCode <= 0
            ? "网络请求失败"
            : "HTTP " + result.statusCode.ToString(CultureInfo.InvariantCulture);
        throw new InvalidOperationException(operation + "失败（" + statusText + "）：" + result.body);
    }

    private static string ReadStream(Stream stream)
    {
        if (stream == null)
        {
            return string.Empty;
        }

        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
        {
            return reader.ReadToEnd();
        }
    }

    private static string GetResponseMessage(string message, string body)
    {
        return string.IsNullOrWhiteSpace(message) ? body : message;
    }

    private static string ComputeSha256(string filePath)
    {
        using (FileStream stream = File.OpenRead(filePath))
        using (SHA256 sha256 = SHA256.Create())
        {
            return ToLowerHex(sha256.ComputeHash(stream));
        }
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            return ToLowerHex(sha256.ComputeHash(bytes));
        }
    }

    private static string ToLowerHex(byte[] bytes)
    {
        StringBuilder builder = new StringBuilder(bytes.Length * 2);
        for (int index = 0; index < bytes.Length; index++)
        {
            builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static void WriteUtf8(string path, string content)
    {
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Replace('\\', '/').Trim('/');
    }

    private static string RemoveExtension(string path)
    {
        string normalized = NormalizePath(path);
        string extension = Path.GetExtension(normalized);
        return string.IsNullOrEmpty(extension)
            ? normalized
            : normalized.Substring(0, normalized.Length - extension.Length);
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        return baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
    }

    private static string GetProjectPath(string relativePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static void ShowResult(string title, bool success)
    {
        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog(title, success ? "请在控制台查看清单文件位置。" : "请在控制台查看具体错误。", "确定");
        }
    }

    [Serializable]
    private sealed class RuntimeApiConfig
    {
        public string developmentBaseUrl;
        public string clientChannel;
        public long clientVersionCode;
    }

    [Serializable]
    private class BasicResponse
    {
        public int code;
        public string message;
    }

    [Serializable]
    private sealed class OpsCatalogResponse : BasicResponse
    {
        public CatalogPayload currentPayload;
        public CatalogPayload draftPayload;
        public OpsCatalogData data;
    }

    [Serializable]
    private sealed class OpsCatalogData
    {
        public CatalogPayload currentPayload;
        public CatalogPayload draftPayload;
    }

    [Serializable]
    private sealed class CatalogPayload
    {
        public AvatarCatalogEntry[] entries;
    }

    [Serializable]
    private sealed class AvatarCatalogEntry
    {
        public string avatarCode;
        public string unityResourcePath;
        public long minimumClientVersionCode;
        public string status;
    }

    [Serializable]
    private class ManifestEvidence
    {
        public int schemaVersion;
        public string platform;
        public string channelCode;
        public long clientVersionCode;
        public string clientVersionName;
        public string manifestVersion;
        public ManifestItem[] items;
    }

    [Serializable]
    private sealed class ManifestPayload : ManifestEvidence
    {
        public string manifestSha256;
    }

    [Serializable]
    private sealed class ManifestItem
    {
        public string avatarCode;
        public string unityResourcePath;
        public string resourceSha256;
    }

    private sealed class HttpResult
    {
        public int statusCode;
        public string body;
    }
}
#endif
