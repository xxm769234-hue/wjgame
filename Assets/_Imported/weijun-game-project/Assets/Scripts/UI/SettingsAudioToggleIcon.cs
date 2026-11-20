using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SettingsAudioToggleIcon : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Sprite audibleSprite;
    [SerializeField] private Sprite mutedSprite;
    private Sprite approvedAudibleSprite;
    private Sprite approvedMutedSprite;
    private bool approvedSpritesLoaded;

    public void Refresh(bool audible)
    {
        if (icon == null)
        {
            return;
        }

        if (!approvedSpritesLoaded)
        {
            bool music = name.IndexOf("Music", System.StringComparison.OrdinalIgnoreCase) >= 0;
            approvedAudibleSprite = Resources.Load<Sprite>("UI/ApprovedUi/Controls/" + (music ? "01_music_on" : "03_sound_on"));
            approvedMutedSprite = Resources.Load<Sprite>("UI/ApprovedUi/Controls/" + (music ? "02_music_off" : "04_sound_off"));
            approvedSpritesLoaded = true;
        }
        Sprite approved = audible ? approvedAudibleSprite : approvedMutedSprite;
        icon.overrideSprite = null;
        icon.sprite = approved != null ? approved : audible ? audibleSprite : mutedSprite;
        icon.color = Color.white;
        if (approved != null) icon.rectTransform.sizeDelta = new Vector2(40f, 40f);
        if (transform.Find("AudioStateLabel") != null)
        {
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, .5f);
            icon.rectTransform.pivot = new Vector2(.5f, .5f);
            icon.rectTransform.anchoredPosition = new Vector2(36f, 0f);
        }
        icon.preserveAspect = true;
        icon.raycastTarget = false;
    }
}
