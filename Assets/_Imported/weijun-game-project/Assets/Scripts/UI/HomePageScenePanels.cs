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

    public void EditorAppendPanel(HomePageScenePanel panel, int version)
    {
        if (panel == null)
        {
            return;
        }

        HomePageScenePanel[] existing = panels ?? Array.Empty<HomePageScenePanel>();
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] == panel || (existing[i] != null && existing[i].PanelType == panel.PanelType))
            {
                schemaVersion = version;
                return;
            }
        }

        HomePageScenePanel[] updated = new HomePageScenePanel[existing.Length + 1];
        for (int i = 0; i < existing.Length; i++)
        {
            updated[i] = existing[i];
        }

        updated[existing.Length] = panel;
        panels = updated;
        schemaVersion = version;
    }

    public void EditorReplacePanel(HomePageScenePanel panel, int version)
    {
        if (panel == null)
        {
            return;
        }

        HomePageScenePanel[] existing = panels ?? Array.Empty<HomePageScenePanel>();
        int validCount = 0;
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null)
            {
                validCount++;
            }
        }

        if (validCount != existing.Length)
        {
            HomePageScenePanel[] compacted = new HomePageScenePanel[validCount];
            int compactedIndex = 0;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null)
                {
                    compacted[compactedIndex++] = existing[i];
                }
            }
            existing = compacted;
        }

        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].PanelType == panel.PanelType)
            {
                existing[i] = panel;
                panels = existing;
                schemaVersion = version;
                return;
            }
        }

        EditorAppendPanel(panel, version);
    }

    public void EditorSetSchemaVersion(int version)
    {
        schemaVersion = version;
    }
#endif
}
