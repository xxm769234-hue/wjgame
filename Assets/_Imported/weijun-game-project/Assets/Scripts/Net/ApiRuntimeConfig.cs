using System;
using UnityEngine;

public static class ApiRuntimeConfig
{
    private const string ResourceName = "ApiConfig";
    private const string OverrideKey = "ApiBaseUrlOverride";
    private const string EnvironmentVariableName = "WEIJUN_API_BASE_URL";
    private const string DefaultDevelopmentUrl = "http://localhost:8080/api";
    private const string DefaultClientChannel = "DEFAULT";
    private const long DefaultClientVersionCode = 10005L;

    private static ApiConfigData cachedConfig;
    private static bool configLoaded;

    public static bool TryGetBaseUrl(out string baseUrl, out string error)
    {
        string playerOverride = PlayerPrefs.GetString(OverrideKey, string.Empty);
        if (TryNormalizeUrl(playerOverride, out baseUrl, out error))
        {
            return true;
        }

        string environmentOverride = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (TryNormalizeUrl(environmentOverride, out baseUrl, out error))
        {
            return true;
        }

        ApiConfigData config = LoadConfig();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string configuredUrl = string.IsNullOrWhiteSpace(config.developmentBaseUrl)
            ? DefaultDevelopmentUrl
            : config.developmentBaseUrl;
#else
        string configuredUrl = config.productionBaseUrl;
#endif

        if (TryNormalizeUrl(configuredUrl, out baseUrl, out error))
        {
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        error = "开发接口地址无效，请检查 Resources/ApiConfig.json。";
#else
        error = "尚未配置生产接口地址，请设置 ApiConfig.json 的 productionBaseUrl。";
#endif
        return false;
    }

    public static string GetDisplayBaseUrl()
    {
        return TryGetBaseUrl(out string baseUrl, out string error) ? baseUrl : error;
    }

    public static string GetClientChannel()
    {
        string channel = LoadConfig().clientChannel;
        return string.IsNullOrWhiteSpace(channel)
            ? DefaultClientChannel
            : channel.Trim().ToUpperInvariant();
    }

    public static long GetClientVersionCode()
    {
        long versionCode = LoadConfig().clientVersionCode;
        return versionCode > 0L ? versionCode : DefaultClientVersionCode;
    }

    public static void SetRuntimeOverride(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            PlayerPrefs.DeleteKey(OverrideKey);
        }
        else
        {
            PlayerPrefs.SetString(OverrideKey, baseUrl.Trim());
        }

        PlayerPrefs.Save();
    }

    public static bool TryBuildUrl(string path, out string url, out string error)
    {
        url = string.Empty;
        if (!TryGetBaseUrl(out string baseUrl, out error))
        {
            return false;
        }

        string normalizedPath = string.IsNullOrEmpty(path)
            ? string.Empty
            : (path[0] == '/' ? path : "/" + path);
        url = baseUrl + normalizedPath;
        return true;
    }

    public static bool TryBuildRealtimeUrl(out string url, out string error)
    {
        url = string.Empty;
        if (!TryBuildUrl("/ws/realtime", out string httpUrl, out error))
        {
            return false;
        }

        UriBuilder builder = new UriBuilder(httpUrl)
        {
            Scheme = httpUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws",
            Port = new Uri(httpUrl).IsDefaultPort ? -1 : new Uri(httpUrl).Port
        };
        url = builder.Uri.AbsoluteUri;
        return true;
    }

    private static ApiConfigData LoadConfig()
    {
        if (configLoaded)
        {
            return cachedConfig;
        }

        configLoaded = true;
        cachedConfig = new ApiConfigData
        {
            developmentBaseUrl = DefaultDevelopmentUrl,
            productionBaseUrl = string.Empty,
            clientChannel = DefaultClientChannel,
            clientVersionCode = DefaultClientVersionCode
        };

        TextAsset configAsset = Resources.Load<TextAsset>(ResourceName);
        if (configAsset == null || string.IsNullOrWhiteSpace(configAsset.text))
        {
            return cachedConfig;
        }

        try
        {
            ApiConfigData parsed = JsonUtility.FromJson<ApiConfigData>(configAsset.text);
            if (parsed != null)
            {
                cachedConfig = parsed;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"ApiConfig.json 解析失败：{exception.Message}");
        }

        return cachedConfig;
    }

    private static bool TryNormalizeUrl(string rawUrl, out string normalizedUrl, out string error)
    {
        normalizedUrl = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return false;
        }

        string candidate = rawUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = $"接口地址格式无效：{candidate}";
            return false;
        }

        normalizedUrl = candidate;
        return true;
    }

    [Serializable]
    private class ApiConfigData
    {
        public string developmentBaseUrl;
        public string productionBaseUrl;
        public string clientChannel;
        public long clientVersionCode;
    }
}
