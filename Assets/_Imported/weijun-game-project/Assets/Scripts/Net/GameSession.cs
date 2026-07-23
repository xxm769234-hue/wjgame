using System;
using UnityEngine;

public static class GameSession
{
    public const string LoginStateKey = "LoginState";
    public const string AuthTokenKey = "AuthToken";
    public const string PlayerIdKey = "PlayerId";
    public const string SessionModeKey = "SessionMode";
    public const string TokenExpiresAtKey = "TokenExpiresAtUtc";

    private const string BackendTokenKey = "weijun.backend.token";
    private const string BackendPlayerIdKey = "weijun.backend.playerId";
    private const string BackendSessionManagedKey = "weijun.backend.managedSession";
    private const string BackendSessionRevisionKey = "weijun.backend.sessionRevision";

    private const string AccountMode = "account";
    private const string GuestMode = "guest";
    private const string LastSessionModeKey = "LastSessionMode";
    private const string GuestProgressPrefix = "GuestProgress.";

    private static readonly string[] GuestProgressKeys =
    {
        "PlayerLv", "Gold", "Wood", "Stone", "Iron", "Crystal", "SoulCrystal"
    };

    private static readonly int[] GuestProgressDefaults =
    {
        1, 1000, 500, 500, 300, 427, 1200
    };

    public static string Token => PlayerPrefs.GetString(AuthTokenKey, string.Empty);
    public static int PlayerId => PlayerPrefs.GetInt(PlayerIdKey, 0);

    public static bool IsAuthenticated
    {
        get
        {
            bool hasCredentials = PlayerPrefs.GetInt(LoginStateKey, 0) == 1
                && PlayerId > 0
                && !string.IsNullOrEmpty(Token)
                && PlayerPrefs.GetString(SessionModeKey, AccountMode) == AccountMode;
            if (!hasCredentials)
            {
                return false;
            }

            if (!TryGetTokenExpiry(out long expiresAt) || expiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                Clear();
                return false;
            }

            return true;
        }
    }

    public static bool IsGuest
    {
        get
        {
            if (PlayerPrefs.GetInt(LoginStateKey, 0) != 1)
            {
                return false;
            }

            string mode = PlayerPrefs.GetString(SessionModeKey, string.Empty);
            if (mode == GuestMode)
            {
                return true;
            }

            return string.IsNullOrEmpty(mode) && PlayerId == 0 && string.IsNullOrEmpty(Token);
        }
    }

    public static bool HasValidSession => IsAuthenticated || IsGuest;

    public static void SaveAuthenticated(
        string token,
        int expiresInSeconds,
        int playerId,
        string username,
        string nickname,
        int level,
        int gold,
        int wood,
        int stone,
        int iron)
    {
        if (IsGuest)
        {
            CaptureGuestProgress();
        }

        long expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Max(1, expiresInSeconds);
        PlayerPrefs.SetInt(LoginStateKey, 1);
        PlayerPrefs.SetString(SessionModeKey, AccountMode);
        PlayerPrefs.SetString(LastSessionModeKey, AccountMode);
        PlayerPrefs.SetString(AuthTokenKey, token ?? string.Empty);
        PlayerPrefs.SetString(TokenExpiresAtKey, expiresAt.ToString());
        PlayerPrefs.SetInt("TokenExpiresIn", Math.Max(0, expiresInSeconds));
        PlayerPrefs.SetInt(PlayerIdKey, playerId);
        SaveBackendSession(token, playerId, true);
        PlayerPrefs.SetString("LoginUsername", username ?? string.Empty);
        PlayerPrefs.SetString("PlayerNickname", nickname ?? string.Empty);
        PlayerPrefs.SetInt("PlayerLv", level);
        PlayerPrefs.SetInt("Gold", gold);
        PlayerPrefs.SetInt("Wood", wood);
        PlayerPrefs.SetInt("Stone", stone);
        PlayerPrefs.SetInt("Iron", iron);
        PlayerPrefs.Save();
    }

    public static void SaveGuest(string nickname)
    {
        bool continuingGuestSession = IsGuest;
        if (continuingGuestSession)
        {
            CaptureGuestProgress();
        }
        else if (!HasGuestProgress()
            && PlayerPrefs.GetString(LastSessionModeKey, string.Empty) != AccountMode
            && PlayerId <= 0
            && string.IsNullOrEmpty(Token))
        {
            CaptureGuestProgress();
        }

        PlayerPrefs.SetInt(LoginStateKey, 1);
        PlayerPrefs.SetString(SessionModeKey, GuestMode);
        PlayerPrefs.SetString(LastSessionModeKey, GuestMode);
        PlayerPrefs.DeleteKey(AuthTokenKey);
        PlayerPrefs.DeleteKey(TokenExpiresAtKey);
        PlayerPrefs.SetInt("TokenExpiresIn", 0);
        PlayerPrefs.SetInt(PlayerIdKey, 0);
        SaveBackendSession(string.Empty, 0, true);
        PlayerPrefs.SetString("PlayerNickname", string.IsNullOrWhiteSpace(nickname) ? "游客" : nickname.Trim());
        PlayerPrefs.SetString("LoginUsername", "Guest");
        RestoreGuestProgress();
        PlayerPrefs.Save();
    }

    public static void Clear()
    {
        string mode = PlayerPrefs.GetString(SessionModeKey, string.Empty);
        if (mode == GuestMode || IsGuest)
        {
            CaptureGuestProgress();
            PlayerPrefs.SetString(LastSessionModeKey, GuestMode);
        }
        else if (mode == AccountMode || !string.IsNullOrEmpty(Token) || PlayerId > 0)
        {
            PlayerPrefs.SetString(LastSessionModeKey, AccountMode);
        }

        PlayerPrefs.SetInt(LoginStateKey, 0);
        PlayerPrefs.DeleteKey(SessionModeKey);
        PlayerPrefs.DeleteKey(AuthTokenKey);
        PlayerPrefs.DeleteKey(TokenExpiresAtKey);
        PlayerPrefs.SetInt("TokenExpiresIn", 0);
        PlayerPrefs.DeleteKey(PlayerIdKey);
        SaveBackendSession(string.Empty, 0, false);
        PlayerPrefs.Save();
    }

    private static void SaveBackendSession(string token, int playerId, bool sessionManaged)
    {
        if (string.IsNullOrEmpty(token) || playerId <= 0)
        {
            PlayerPrefs.DeleteKey(BackendTokenKey);
            PlayerPrefs.DeleteKey(BackendPlayerIdKey);
        }
        else
        {
            PlayerPrefs.SetString(BackendTokenKey, token);
            PlayerPrefs.SetInt(BackendPlayerIdKey, playerId);
        }

        PlayerPrefs.SetInt(BackendSessionManagedKey, sessionManaged ? 1 : 0);
        PlayerPrefs.SetInt(
            BackendSessionRevisionKey,
            PlayerPrefs.GetInt(BackendSessionRevisionKey, 0) + 1);
    }

    private static bool TryGetTokenExpiry(out long expiresAt)
    {
        return long.TryParse(PlayerPrefs.GetString(TokenExpiresAtKey, string.Empty), out expiresAt);
    }

    private static bool HasGuestProgress()
    {
        return PlayerPrefs.HasKey(GuestProgressPrefix + "Gold");
    }

    private static void CaptureGuestProgress()
    {
        for (int i = 0; i < GuestProgressKeys.Length; i++)
        {
            string key = GuestProgressKeys[i];
            PlayerPrefs.SetInt(
                GuestProgressPrefix + key,
                PlayerPrefs.GetInt(key, GuestProgressDefaults[i]));
        }
    }

    private static void RestoreGuestProgress()
    {
        for (int i = 0; i < GuestProgressKeys.Length; i++)
        {
            string key = GuestProgressKeys[i];
            PlayerPrefs.SetInt(
                key,
                PlayerPrefs.GetInt(GuestProgressPrefix + key, GuestProgressDefaults[i]));
        }
    }
}
