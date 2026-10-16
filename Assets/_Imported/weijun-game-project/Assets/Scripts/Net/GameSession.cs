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
    private const string AreaSelectionOwnerPlayerIdKey = "weijun.areaSelection.playerId";
    private const string OpenAreaLandTypeKey = "OpenAreaLandType";
    private const string CurrentAreaLandTypeKey = "CurrentAreaLandType";

    private static readonly string[] AccountSnapshotPreferenceKeys =
    {
        "LoginUsername",
        "PlayerNickname",
        "PlayerNicknameVersion",
        "PlayerNicknameUpdatedTime",
        "PlayerNicknameChangeCount",
        "PlayerNicknameChangeRequired",
        "PlayerAvatarOwnerId",
        "PlayerAvatarUrl",
        "PlayerAvatarSource",
        "PlayerAvatarCode",
        "PlayerAvatarVersion",
        "PlayerAvatarUpdatedTime",
        "PlayerAvatarResourcePath",
        "PlayerTotalExperience",
        "PlayerLevelCurveRevision",
        "PlayerCurrentLevelExperience",
        "PlayerRequiredExperience",
        "PlayerExperienceProgressBps",
        "PlayerMaxLevel",
        "PlayerEconomyFrozen",
        "PlayerStamina",
        "ProfileLastSync",
        "weijun.backend.sceneId",
        "ServerMapWidth",
        "ServerMapHeight",
        "ServerLandCount",
        "ServerBuildingCount",
        "ServerMapLastSync",
        "weijun.idleAdventure.highestClearedStage",
        "Notification.DeepLink.Type",
        "Notification.DeepLink.SceneId",
        "Notification.DeepLink.PlayerBuildingId",
        "Notification.DeepLink.PlayerLandId",
        "weijun.npcAcquisition.routeKey",
        "weijun.npcAcquisition.playerId",
        "weijun.npcAcquisition.expiresAt",
        "weijun.npcAcquisition.poolCode",
        "weijun.npcAcquisition.highlightNpcId",
        "weijun.npcAcquisition.taskCode",
        "weijun.npcAcquisition.planCode",
        "weijun.npcAcquisition.modeCode",
        "weijun.npcAcquisition.stage",
        "weijun.npcAcquisition.poolKey",
        "weijun.npcAcquisition.mailId"
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

    public static bool HasValidSession => IsAuthenticated;

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
        int iron,
        int diamond = 0,
        long totalExperience = 0,
        int levelCurveRevision = 0,
        int currentLevelExperience = 0,
        int requiredExperienceForNextLevel = 0,
        int experienceProgressBps = 0,
        bool maxLevel = false,
        bool economyFrozen = false)
    {
        ReconcileAreaSelectionForPlayer(playerId);
        long expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Max(1, expiresInSeconds);
        PlayerPrefs.SetInt(LoginStateKey, 1);
        PlayerPrefs.SetString(SessionModeKey, AccountMode);
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
        PlayerPrefs.SetInt("Crystal", diamond);
        PlayerPrefs.SetString("PlayerTotalExperience", Math.Max(0L, totalExperience).ToString());
        PlayerPrefs.SetInt("PlayerLevelCurveRevision", Math.Max(0, levelCurveRevision));
        PlayerPrefs.SetInt("PlayerCurrentLevelExperience", Math.Max(0, currentLevelExperience));
        PlayerPrefs.SetInt("PlayerRequiredExperience", Math.Max(0, requiredExperienceForNextLevel));
        PlayerPrefs.SetInt("PlayerExperienceProgressBps", Mathf.Clamp(experienceProgressBps, 0, 10000));
        PlayerPrefs.SetInt("PlayerMaxLevel", maxLevel ? 1 : 0);
        PlayerPrefs.SetInt("PlayerEconomyFrozen", economyFrozen ? 1 : 0);
        PlayerPrefs.Save();
        WeChatPlatformBridge.SyncFriendLevel(level);
    }

    public static void Clear()
    {
        ReconcileAreaSelectionForPlayer(PlayerId);
        bool wasAccountSession = PlayerPrefs.GetInt(LoginStateKey, 0) == 1
            && string.Equals(PlayerPrefs.GetString(SessionModeKey, string.Empty), AccountMode, StringComparison.Ordinal);
        if (wasAccountSession)
        {
            ClearAccountSnapshot();
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

    private static void ReconcileAreaSelectionForPlayer(int playerId)
    {
        if (playerId <= 0)
        {
            return;
        }

        int ownerPlayerId = PlayerPrefs.GetInt(AreaSelectionOwnerPlayerIdKey, 0);
        if (ownerPlayerId <= 0)
        {
            ownerPlayerId = PlayerPrefs.GetInt(PlayerIdKey, 0);
        }

        if (ownerPlayerId > 0 && ownerPlayerId != playerId)
        {
            PlayerPrefs.DeleteKey(OpenAreaLandTypeKey);
            PlayerPrefs.DeleteKey(CurrentAreaLandTypeKey);
        }

        PlayerPrefs.SetInt(AreaSelectionOwnerPlayerIdKey, playerId);
    }

    private static void ClearAccountSnapshot()
    {
        for (int index = 0; index < AccountSnapshotPreferenceKeys.Length; index++)
        {
            PlayerPrefs.DeleteKey(AccountSnapshotPreferenceKeys[index]);
        }
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

}
