using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HomePageScenePanels : MonoBehaviour
{
    [SerializeField] private HomePageScenePanel[] panels = Array.Empty<HomePageScenePanel>();
    [SerializeField, HideInInspector] private int schemaVersion;

    public int SchemaVersion => schemaVersion;

    public HomePageScenePanel Get(HomePageScenePanelType type)
    {
        if (panels == null)
        {
            return null;
        }

        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null && panels[i].PanelType == type)
            {
                return panels[i];
            }
        }

        return null;
    }

    public void HideAll()
    {
        if (panels == null)
        {
            return;
        }

        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null)
            {
                panels[i].SetVisible(false);
            }
        }
    }

#if UNITY_EDITOR
    public void EditorSetPanels(HomePageScenePanel[] scenePanels, int version)
    {
        panels = scenePanels ?? Array.Empty<HomePageScenePanel>();
        schemaVersion = version;
    }

    public void EditorSetSchemaVersion(int version)
    {
        schemaVersion = version;
    }
#endif
}
