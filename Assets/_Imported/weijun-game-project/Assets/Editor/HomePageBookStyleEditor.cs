using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(HomePageBookStyle))]
public sealed class HomePageBookStyleEditor : Editor
{
    public override void OnInspectorGUI()
    {
        HomePageBookStyle style = (HomePageBookStyle)target;
        if (DrawDefaultInspector())
        {
            style.ApplyToHierarchy();
            MarkBookDirty(style);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Apply Style To Preview"))
        {
            Undo.RegisterFullObjectHierarchyUndo(style.gameObject, "Apply Book Home Style");
            style.ApplyToHierarchy();
            MarkBookDirty(style);
        }

        if (GUILayout.Button("Rebuild Book UI Components"))
        {
            Undo.RegisterFullObjectHierarchyUndo(style.gameObject, "Rebuild Book Home UI");
            HomePageUI homePage = style.GetComponent<HomePageUI>();
            if (homePage == null)
            {
                homePage = Undo.AddComponent<HomePageUI>(style.gameObject);
            }

            homePage.EditorRebuildBookPreview();
            MarkBookDirty(style);
        }
    }

    private static void MarkBookDirty(HomePageBookStyle style)
    {
        Component[] components = style.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            EditorUtility.SetDirty(components[i]);
        }

        if (style.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(style.gameObject.scene);
        }
    }
}
