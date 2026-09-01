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

        serverStaminaRequestRunning = true;
        UnityWebRequest request = CreateAuthorizedGet($"/idle-adventure/players/{GameSession.PlayerId}/stamina");
        if (request == null)
        {
            serverStaminaRequestRunning = false;
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();
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
