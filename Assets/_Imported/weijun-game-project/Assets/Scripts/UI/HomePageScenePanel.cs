using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum HomePageScenePanelType
{
    Content,
    Mail,
    DailySign,
    NewbieSign,
    Lottery,
    Recharge,
    Task,
    Inventory,
    Announcement,
    Settings,
    AccountProfile,
    Friends,
    Equipment,
    Shop,
    Role
}

[DisallowMultipleComponent]
public sealed class HomePageScenePanel : MonoBehaviour
{
    [SerializeField] private HomePageScenePanelType panelType;
    [SerializeField] private RectTransform panelRoot;

    public HomePageScenePanelType PanelType => panelType;
    public RectTransform PanelRoot => panelRoot != null ? panelRoot : transform as RectTransform;

    public Button FindButton(string objectName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].name == objectName)
            {
                return buttons[i];
            }
        }

        return null;
    }

    public TMP_Text FindText(string objectName)
    {
        TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i].name == objectName)
            {
                return labels[i];
            }
        }

        return null;
    }

    public Transform FindTransform(string objectName)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == objectName)
            {
                return transforms[i];
            }
        }

        return null;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

#if UNITY_EDITOR
    public void EditorSetReferences(HomePageScenePanelType type, RectTransform root)
    {
        panelType = type;
        panelRoot = root;
    }
#endif
}
