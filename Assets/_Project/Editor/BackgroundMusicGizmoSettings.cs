using UnityEditor;
using UnityEngine;

public static class BackgroundMusicGizmoSettings
{
    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.update += HideAudioSourceGizmos;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            EditorApplication.update -= HideAudioSourceGizmos;
            EditorApplication.update += HideAudioSourceGizmos;
        }
    }

    public static void HideAudioSourceGizmos()
    {
        if (!GizmoUtility.TryGetGizmoInfo(typeof(AudioSource), out _))
        {
            return;
        }

        GizmoUtility.SetIconEnabled(typeof(AudioSource), false);
        GizmoUtility.SetGizmoEnabled(typeof(AudioSource), false);
        EditorApplication.update -= HideAudioSourceGizmos;
        SceneView.RepaintAll();
    }
}
