using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class SummonRevealPlayer : MonoBehaviour
{
    private Texture2D texture;
    private AudioSource sound;

    private CanvasGroup curtain;

    public static SummonRevealPlayer Show(Transform parent, string clipName, Action showRewards)
    {
        if (parent == null || !parent.gameObject.activeInHierarchy) return null;
        var overlay = new GameObject("Summon Reveal", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(SummonRevealPlayer));
        overlay.layer = parent.gameObject.layer;
        var rect = (RectTransform)overlay.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = Color.black;
        var reveal = overlay.GetComponent<SummonRevealPlayer>();
        reveal.curtain = overlay.GetComponent<CanvasGroup>();
        reveal.curtain.alpha = 0f;
        reveal.StartCoroutine(reveal.Play(clipName, showRewards));
        return reveal;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            curtain.alpha = Mathf.SmoothStep(from, to, elapsed / duration);
            yield return null;
        }
        curtain.alpha = to;
    }

    private IEnumerator Play(string clipName, Action showRewards)
    {
        string path = "UI/SummonBook/Reveal/" + clipName;
        ResourceRequest framesRequest = Resources.LoadAsync<TextAsset>(path);
        ResourceRequest soundRequest = Resources.LoadAsync<AudioClip>(path + "Sound");
        yield return Fade(0f, .45f, .18f);
        yield return framesRequest;
        yield return soundRequest;
        var asset = framesRequest.asset as TextAsset;
        if (asset != null) yield return PlayFilm(asset, soundRequest.asset as AudioClip);
        showRewards?.Invoke();
        transform.SetAsLastSibling();
        yield return Fade(curtain.alpha, 0f, .32f);
        Destroy(gameObject);
    }

    private IEnumerator PlayFilm(TextAsset asset, AudioClip audio)
    {
        byte[] bytes = asset.bytes;
        Resources.UnloadAsset(asset);
        if (bytes.Length < 12 || BitConverter.ToInt32(bytes, 0) != 0x31584653) yield break;
        int count = BitConverter.ToInt32(bytes, 4);
        int duration = BitConverter.ToInt32(bytes, 8);
        if (count <= 0 || count > (bytes.Length - 12) / 8 || duration <= 0) yield break;
        int[] offsets = new int[count + 1];
        int[] times = new int[count];
        offsets[0] = 12 + count * 8;
        for (int index = 0; index < count; index++)
        {
            times[index] = BitConverter.ToInt32(bytes, 12 + index * 8);
            int size = BitConverter.ToInt32(bytes, 16 + index * 8);
            if (size <= 0 || size > bytes.Length - offsets[index]) yield break;
            offsets[index + 1] = offsets[index] + size;
        }
        var screen = new GameObject("Film", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        screen.transform.SetParent(transform, false);
        screen.layer = gameObject.layer;
        var picture = screen.GetComponent<RawImage>();
        picture.raycastTarget = false;
        var fit = screen.GetComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio = 16f / 9f;
        texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        picture.texture = texture;
        sound = gameObject.AddComponent<AudioSource>();
        sound.playOnAwake = false;
        sound.clip = audio;
        GameSettingsRuntime.RegisterAudioSource(sound, false, 1f);
        int displayed = -1;
        int next = 0;
        float elapsed = 0f;
        float end = Mathf.Max(duration * .001f, sound.clip != null ? sound.clip.length : 0f);
        while (elapsed < end || displayed < count - 1)
        {
            while (next + 1 < count && times[next + 1] <= elapsed * 1000f) next++;
            if (next != displayed)
            {
                byte[] frame = new byte[offsets[next + 1] - offsets[next]];
                Buffer.BlockCopy(bytes, offsets[next], frame, 0, frame.Length);
                if (!texture.LoadImage(frame, true)) break;
                if (displayed < 0)
                {
                    yield return Fade(.45f, 1f, .15f);
                    if (sound.clip != null) sound.Play();
                }
                displayed = next;
            }
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
    }

    private void OnDisable()
    {
        if (sound != null) sound.Stop();
        GameSettingsRuntime.UnregisterAudioSource(sound);
        if (texture != null) Destroy(texture);
        Destroy(gameObject);
    }
}
