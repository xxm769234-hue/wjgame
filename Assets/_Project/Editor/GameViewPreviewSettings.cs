using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class GameViewPreviewSettings
{
    private static readonly Type GameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
    private static readonly PropertyInfo DrawGizmos = GameViewType?.GetProperty(
        "drawGizmos", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.delayCall += HideGameViewGizmos;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            EditorApplication.delayCall += HideGameViewGizmos;
        }
    }

    [MenuItem("Tools/Game Preview/Hide Game View Gizmos")]
    public static void HideGameViewGizmos()
    {
        if (GameViewType == null || DrawGizmos == null || !DrawGizmos.CanWrite)
        {
            return;
        }

        foreach (UnityEngine.Object view in Resources.FindObjectsOfTypeAll(GameViewType))
        {
            DrawGizmos.SetValue(view, false);
            ((EditorWindow)view).Repaint();
        }
    }
}
