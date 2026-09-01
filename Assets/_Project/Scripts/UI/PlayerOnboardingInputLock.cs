using UnityEngine;

/// <summary>
/// Reads the shared onboarding input state for the project gameplay assembly.
/// </summary>
public static class PlayerOnboardingInputLock
{
    private const string LockedKey = "weijun.onboarding.inputLocked";
    private const string TargetOnlyKey = "weijun.onboarding.targetOnly";
    private const string ActionKey = "weijun.onboarding.actionKey";
    private const string TargetKey = "weijun.onboarding.targetKey";
    private const string SceneCodeKey = "weijun.onboarding.sceneCode";

    /// <summary>Returns true while the onboarding overlay blocks gameplay input.</summary>
    public static bool IsLocked => PlayerPrefs.GetInt(LockedKey, 0) == 1;

    /// <summary>Returns true while onboarding permits input only on its current target.</summary>
    public static bool IsTargetOnly => PlayerPrefs.GetInt(TargetOnlyKey, 0) == 1;

    /// <summary>Returns true while any gameplay input should be blocked.</summary>
    public static bool IsGameplayInputBlocked => IsLocked || IsTargetOnly;

    /// <summary>Returns the action key published for the current onboarding step.</summary>
    public static string CurrentActionKey => PlayerPrefs.GetString(ActionKey, string.Empty);

    /// <summary>Returns the UI or world target key published for the current step.</summary>
    public static string CurrentTargetKey => PlayerPrefs.GetString(TargetKey, string.Empty);

    /// <summary>Returns the server scene code published for the current step.</summary>
    public static string CurrentSceneCode => PlayerPrefs.GetString(SceneCodeKey, string.Empty);

    /// <summary>
    /// Checks whether a gameplay action is permitted by the current onboarding step.
    /// Outside target-only mode this preserves the existing lock behavior.
    /// </summary>
    public static bool IsInputAllowed(string actionKey, string targetKey)
    {
        if (IsLocked)
        {
            return false;
        }

        if (!IsTargetOnly)
        {
            return true;
        }

        return Matches(CurrentActionKey, actionKey) || Matches(CurrentTargetKey, targetKey);
    }

    /// <summary>Checks only the server-highlighted target, without allowing an action-key shortcut.</summary>
    public static bool IsTargetAllowed(string targetKey)
    {
        if (IsLocked)
        {
            return false;
        }

        return !IsTargetOnly || Matches(CurrentTargetKey, targetKey);
    }

    private static bool Matches(string current, string requested)
    {
        if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(requested))
        {
            return false;
        }

        return string.Equals(current, requested, System.StringComparison.OrdinalIgnoreCase)
            || current.IndexOf(requested, System.StringComparison.OrdinalIgnoreCase) >= 0
            || requested.IndexOf(current, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
