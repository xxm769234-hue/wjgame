using UnityEngine;

/// <summary>
/// Reads the shared onboarding input state for the project gameplay assembly.
/// </summary>
public static class PlayerOnboardingInputLock
{
    private const string LockedKey = "weijun.onboarding.inputLocked";

    /// <summary>Returns true while the onboarding overlay blocks gameplay input.</summary>
    public static bool IsLocked => PlayerPrefs.GetInt(LockedKey, 0) == 1;
}
