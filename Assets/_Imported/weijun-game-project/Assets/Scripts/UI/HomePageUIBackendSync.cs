using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public partial class HomePageUI
{
    private const float BackendInventoryRefreshDebounceSeconds = 0.1f;

    private bool serverStaminaLoaded;
    private bool serverStaminaRequestRunning;
    private long serverStaminaCurrent;
    private long serverStaminaMaximum;
    private int serverStaminaSecondsToNextRecovery;
    private bool serverStaminaRecoveryEnabled;
    private double serverStaminaReceivedAt;
    private double nextStaminaMetricUpdateAt;
    private double nextStaminaQueryAt;
    private int staminaSessionPlayerId;
    private string staminaSessionToken;
    private bool backendSnapshotRefreshQueued;
    private bool backendSnapshotRefreshUserQueued;
    private bool backendSnapshotRefreshInventoryQueued;
    private Coroutine backendSnapshotRefreshCoroutine;
    private int backendSnapshotRefreshPlayerId;

    private void QueueBackendInventoryRefresh()
    {
        QueueBackendSnapshotRefresh(false, true);
    }

    private void QueueBackendSnapshotRefresh(bool refreshUser, bool refreshInventory)
    {
        if ((!refreshUser && !refreshInventory) || !isActiveAndEnabled || !HasAuthSession())
        {
            return;
        }

        int playerId = GameSession.PlayerId;
        if (playerId <= 0)
        {
            return;
        }

        backendSnapshotRefreshQueued = true;
        backendSnapshotRefreshUserQueued |= refreshUser;
        backendSnapshotRefreshInventoryQueued |= refreshInventory;
        backendSnapshotRefreshPlayerId = playerId;
        if (backendSnapshotRefreshCoroutine == null)
        {
            backendSnapshotRefreshCoroutine = StartCoroutine(RefreshBackendSnapshotBridge());
        }
    }

    private IEnumerator RefreshBackendSnapshotBridge()
    {
        while (backendSnapshotRefreshQueued)
        {
            backendSnapshotRefreshQueued = false;
            bool refreshUser = backendSnapshotRefreshUserQueued;
            bool refreshInventory = backendSnapshotRefreshInventoryQueued;
            backendSnapshotRefreshUserQueued = false;
            backendSnapshotRefreshInventoryQueued = false;
            int requestedPlayerId = backendSnapshotRefreshPlayerId;
            yield return new WaitForSecondsRealtime(BackendInventoryRefreshDebounceSeconds);

            if (!HasAuthSession())
            {
                backendSnapshotRefreshQueued = false;
                backendSnapshotRefreshUserQueued = false;
                backendSnapshotRefreshInventoryQueued = false;
                break;
            }
            if (GameSession.PlayerId != requestedPlayerId)
            {
                continue;
            }

            object service;
            Type serviceType;
            if (!TryGetBackendGameService(out service, out serviceType))
            {
                break;
            }

            if (refreshUser)
            {
                yield return InvokeBackendServiceCoroutine(service, serviceType, "RefreshUser");
                if (!HasAuthSession())
                {
                    backendSnapshotRefreshQueued = false;
                    backendSnapshotRefreshUserQueued = false;
                    backendSnapshotRefreshInventoryQueued = false;
                    break;
                }
                if (GameSession.PlayerId != requestedPlayerId)
                {
                    continue;
                }
            }

            if (refreshInventory)
            {
                yield return InvokeBackendServiceCoroutine(service, serviceType, "RefreshInventory");
            }
        }

        backendSnapshotRefreshCoroutine = null;
    }

    private IEnumerator RefreshServerStamina()
    {
        if (serverStaminaRequestRunning || !HasAuthSession()) yield break;

        int requestPlayerId = GameSession.PlayerId;
        string requestToken = GameSession.Token;
        serverStaminaRequestRunning = true;
        nextStaminaQueryAt = Time.realtimeSinceStartupAsDouble + 5d;
        UnityWebRequest request = CreateAuthorizedGet($"/idle-adventure/players/{GameSession.PlayerId}/stamina");
        if (request == null)
        {
            serverStaminaRequestRunning = false;
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
            if (!HasAuthSession() || requestPlayerId != GameSession.PlayerId || requestToken != GameSession.Token)
            {
                serverStaminaRequestRunning = false;
                yield break;
            }
            if (HandleUnauthorizedResponse(request))
            {
                serverStaminaRequestRunning = false;
                yield break;
            }

            StaminaApiResponse response = ParseJson<StaminaApiResponse>(request.downloadHandler.text);
            if (request.result == UnityWebRequest.Result.Success
                && response != null
                && response.code == 200
                && response.data != null)
            {
                serverStaminaCurrent = Math.Max(0L, response.data.ResolvedCurrentStamina);
                serverStaminaMaximum = Math.Max(0L, response.data.ResolvedMaxStamina);
                serverStaminaSecondsToNextRecovery = ClampLongToInt(response.data.ResolvedSecondsToNextRecovery);
                serverStaminaRecoveryEnabled = response.data.enabled;
                serverStaminaReceivedAt = Time.realtimeSinceStartupAsDouble;
                staminaSessionPlayerId = requestPlayerId;
                staminaSessionToken = requestToken;
                nextStaminaQueryAt = serverStaminaReceivedAt + (CanShowHomeStaminaCountdown()
                    ? Math.Max(1, serverStaminaSecondsToNextRecovery) : 60d);
                serverStaminaLoaded = true;
                PlayerPrefs.SetInt("PlayerStamina", ClampLongToInt(serverStaminaCurrent));
                PlayerPrefs.Save();
                RefreshHomeResourceMetrics();
                RefreshOpenProfileResourceCards();
            }
            else
            {
                Debug.LogWarning("Authoritative stamina refresh failed: " + ApiRequestUtility.GetErrorMessage(request, "request failed"), this);
            }
        }

        serverStaminaRequestRunning = false;
    }

    private bool CanShowHomeStaminaCountdown()
    {
        return serverStaminaRecoveryEnabled && serverStaminaMaximum > 0L
            && serverStaminaCurrent < serverStaminaMaximum;
    }

    private string GetHomeStaminaDisplay()
    {
        string value = GetHomeStamina().ToString();
        if (!serverStaminaLoaded || !CanShowHomeStaminaCountdown()) return value;
        double elapsed = Math.Max(0d, Time.realtimeSinceStartupAsDouble - serverStaminaReceivedAt);
        int remaining = (int)Math.Max(0d, Math.Ceiling(serverStaminaSecondsToNextRecovery - elapsed));
        return value + "(" + FormatHomeStaminaCountdown(remaining) + ")";
    }

    private static string FormatHomeStaminaCountdown(int seconds)
    {
        TimeSpan remaining = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return seconds >= 3600
            ? ((int)remaining.TotalHours).ToString("00") + ":" + remaining.Minutes.ToString("00") + ":" + remaining.Seconds.ToString("00")
            : remaining.Minutes.ToString("00") + ":" + remaining.Seconds.ToString("00");
    }

    private void UpdateHomeStaminaCountdown()
    {
        if (staminaSessionPlayerId != GameSession.PlayerId || staminaSessionToken != GameSession.Token || !HasAuthSession())
        {
            serverStaminaLoaded = false;
            staminaSessionPlayerId = GameSession.PlayerId;
            staminaSessionToken = GameSession.Token;
            nextStaminaQueryAt = 0d;
        }

        double now = Time.realtimeSinceStartupAsDouble;
        if (now >= nextStaminaMetricUpdateAt)
        {
            nextStaminaMetricUpdateAt = now + 0.2d;
            if (boundBookView != null)
                SetBookMetricValue(boundBookView.MetricTexts, 1, GetHomeStaminaDisplay());
        }
        if (HasAuthSession() && !serverStaminaRequestRunning && now >= nextStaminaQueryAt)
            StartCoroutine(RefreshServerStamina());
    }

    private int GetAuthoritativeHomeStamina()
    {
        return serverStaminaLoaded ? ClampLongToInt(serverStaminaCurrent) : 0;
    }

    private static int ClampLongToInt(long value)
    {
        return (int)Math.Max(0L, Math.Min(int.MaxValue, value));
    }

    private static string FormatStaminaValue(long current, long maximum)
    {
        return maximum > 0L ? current + "/" + maximum : current.ToString();
    }

    private void RefreshStaminaAfterShopPurchase()
    {
        if (HasAuthSession()) StartCoroutine(RefreshServerStamina());
    }

    [Serializable]
    private sealed class StaminaApiResponse : BasicApiResponse
    {
        public StaminaData data;
    }

    [Serializable]
    private sealed class StaminaData
    {
        public bool enabled;
        public long currentStamina;
        public long current_stamina;
        public long maxStamina;
        public long max_stamina;
        public bool over_cap;
        public bool overCap;
        public long overflow_amount;
        public long overflowAmount;
        public int recovery_interval_seconds;
        public int recoveryIntervalSeconds;
        public string next_recovery_at;
        public string nextRecoveryAt;
        public long seconds_to_next_recovery;
        public long secondsToNextRecovery;
        public long seconds_to_full;
        public long secondsToFull;
        public string server_time;
        public string serverTime;
        public long config_version;
        public long configVersion;

        public long ResolvedCurrentStamina => currentStamina > 0L ? currentStamina : current_stamina;
        public long ResolvedMaxStamina => maxStamina > 0L ? maxStamina : max_stamina;
        public long ResolvedSecondsToNextRecovery => secondsToNextRecovery > 0L
            ? secondsToNextRecovery
            : seconds_to_next_recovery;
    }
}
