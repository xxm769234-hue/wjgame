using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 伤害数字（方案 A · 累加计数）。数字字形为自绘「裂刃体」位图字库：Resources/BattleUI/DamageNumbers。
/// 每个目标每次行动只有一个数字：多段命中时往上累加并弹一下，最后一段砸一下后淡出。
/// 徽章与"击杀"文字使用项目主字体思源黑体（SIL OFL），不使用任何系统字体。
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleDamageNumbers : MonoBehaviour
{
    private const string ResourceFolder = "BattleUI/DamageNumbers/";
    private const string BadgeChars = "克制击杀物炎冰风岩星暗毒";
    private const int MaxParticles = 120;

    [Tooltip("总开关；关闭后不显示任何伤害数字。")]
    public bool showNumbers = true;
    [Tooltip("我方被敌人克制时是否也显示属性样式（默认只显示红色受伤数字）。")]
    public bool showRestraintOnPlayerDamage;

    // 1080p 参考尺寸（画布单位）
    private const float SizeNormal = 46f, SizeLight = 36f, SizeHeavy = 58f, SizeKill = 64f, SizeHurt = 40f, SizeHeal = 40f, SizeTick = 26f;

    #region 字库
    [Serializable] private sealed class GlyphJson { public string ch; public int x; public int w; public float originX; public float originY; public float advance; }
    [Serializable] private sealed class FxJson { public int cell; public string[] names; }
    [Serializable] private sealed class FontJson { public float renderSize; public float shear; public float tracking; public int cellHeight; public GlyphJson[] glyphs; public FxJson fx; }

    private sealed class Glyph { public Sprite back, face; public Vector2 cellSize; public Vector2 pivot; public float advance; }

    private FontJson font;
    private readonly Dictionary<string, Dictionary<char, Glyph>> styles = new Dictionary<string, Dictionary<char, Glyph>>();
    private readonly Dictionary<string, Sprite> fx = new Dictionary<string, Sprite>();
    private TMP_FontAsset labelFont;
    private Material labelMaterial;

    private bool EnsureFont()
    {
        if (font != null) return true;
        TextAsset json = Resources.Load<TextAsset>(ResourceFolder + "DamageDigits");
        if (json == null) { Debug.LogWarning("Damage digit metrics missing: Resources/" + ResourceFolder + "DamageDigits.json", this); return false; }
        font = JsonUtility.FromJson<FontJson>(json.text);
        Texture2D fxTexture = Resources.Load<Texture2D>(ResourceFolder + "DamageFx");
        if (fxTexture != null && font.fx != null && font.fx.names != null)
            for (int i = 0; i < font.fx.names.Length; i++)
                fx[font.fx.names[i]] = Sprite.Create(fxTexture, new Rect(i * font.fx.cell, 0, font.fx.cell, font.fx.cell),
                    new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
        labelFont = RuntimeFontAssetProvider.Resolve();
        if (labelFont != null)
        {
            labelFont.TryAddCharacters(BadgeChars);          // 动态字库预热，避免第一次克制时卡一下
            labelMaterial = new Material(labelFont.material) { name = "DamageBadge (Instance)" };
            labelMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
            labelMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, .28f);
            labelMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(18, 8, 4, 255));
            labelMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, .15f);
        }
        return true;
    }

    private Dictionary<char, Glyph> Style(string key)
    {
        if (styles.TryGetValue(key, out Dictionary<char, Glyph> cached)) return cached;
        var glyphs = new Dictionary<char, Glyph>();
        Texture2D texture = Resources.Load<Texture2D>(ResourceFolder + "DamageDigits_" + key);
        if (texture == null) texture = Resources.Load<Texture2D>(ResourceFolder + "DamageDigits_normal");
        if (texture != null)
        {
            float h = font.cellHeight;
            foreach (GlyphJson g in font.glyphs)
            {
                if (string.IsNullOrEmpty(g.ch)) continue;
                var pivot = new Vector2(g.originX / g.w, 1f - g.originY / h);
                glyphs[g.ch[0]] = new Glyph
                {
                    back = Sprite.Create(texture, new Rect(g.x, h, g.w, h), pivot, 100f, 0, SpriteMeshType.FullRect),
                    face = Sprite.Create(texture, new Rect(g.x, 0, g.w, h), pivot, 100f, 0, SpriteMeshType.FullRect),
                    cellSize = new Vector2(g.w, h),
                    pivot = pivot,
                    advance = g.advance
                };
            }
        }
        styles[key] = glyphs;
        return glyphs;
    }

    private static string StyleKeyForAttribute(string attribute) => attribute == "light" ? "light_attr" : attribute;
    #endregion

    #region 属性外观
    // 取色自《属性图标最终版》：图标亮色（内芯）用于徽章文字、描边与粒子；暗/毒的内芯过暗，略提亮以保证在深色徽章上可读
    private static Color AttributeColor(string attribute)
    {
        switch (attribute)
        {
            case "fire": return new Color32(237, 76, 55, 255);     // 炎 #ed4c37
            case "water": return new Color32(108, 227, 255, 255);  // 冰 #6ce3ff
            case "wood": return new Color32(109, 198, 52, 255);    // 风 #6dc634
            case "earth": return new Color32(191, 100, 33, 255);   // 岩 #bf6421
            case "light": return new Color32(255, 230, 0, 255);    // 星 #ffe600
            case "dark": return new Color32(79, 98, 255, 255);     // 暗 #2234dd → 提亮
            case "poison": return new Color32(154, 79, 224, 255);  // 毒 #611da8 → 提亮
            default: return new Color32(206, 206, 206, 255);       // 物理 #cecece
        }
    }

    // 徽章内文字与图标名称一致
    private static string AttributeChar(string attribute)
    {
        switch (attribute)
        {
            case "fire": return "炎";
            case "water": return "冰";
            case "wood": return "风";
            case "earth": return "岩";
            case "light": return "星";
            case "dark": return "暗";
            case "poison": return "毒";
            default: return "物";
        }
    }

    private readonly Dictionary<string, Sprite> attributeIcons = new Dictionary<string, Sprite>();

    private Sprite AttributeIcon(string attribute)
    {
        if (attributeIcons.TryGetValue(attribute, out Sprite cached)) return cached;
        Sprite sprite = Resources.Load<Sprite>("BattleUI/AttributeIcons/" + attribute);
        attributeIcons[attribute] = sprite;
        return sprite;
    }
    #endregion

    #region 视图对象
    private sealed class NumberView
    {
        public RectTransform root;
        public RectTransform backRoot, faceRoot;
        public readonly List<Image> back = new List<Image>(), face = new List<Image>();
        public Image ring;
        public RectTransform badge;
        public Image badgeBorder, badgeFill, badgeDiamond, badgeIcon;
        public TMP_Text badgeChar, badgeLabel, tag;
        public string builtText, builtStyle;
        public float builtSize;
    }

    private sealed class Counter
    {
        public NumberView view;
        public bool tick;
        public int actionKey;
        public BattleDamagePlan.Entry entry;
        public BattleActorAnimation anchor;
        public Vector2 position;
        public float direction;
        public long target;
        public int hits, impactCount;
        public float age, punch, slam, doneAge, shakeAngle;
        public bool done, heavyFinal;
        public string tickStyle;
        public bool tickHeavy;
    }

    private sealed class Particle
    {
        public Image image;
        public Vector2 position, velocity;
        public float age, life, spin, gravity, size, growth;
        public bool ring;
    }

    private RectTransform layer;
    private Transform canvasRoot;
    private Func<bool> pauseQuery;
    private readonly List<Counter> active = new List<Counter>();
    private readonly Stack<NumberView> viewPool = new Stack<NumberView>();
    private readonly List<Particle> particles = new List<Particle>();
    private readonly Stack<Image> particlePool = new Stack<Image>();

    public void Bind(RectTransform battlefield, Func<bool> paused)
    {
        pauseQuery = paused;
        if (layer == null)
        {
            layer = new GameObject("Damage Numbers", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            layer.SetParent(battlefield, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            CanvasGroup group = layer.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
        }
        layer.SetAsLastSibling();
        Canvas owner = battlefield.GetComponentInParent<Canvas>();
        canvasRoot = owner != null ? owner.rootCanvas.transform : battlefield.root;
        EnsureFont();
    }

    private NumberView TakeView()
    {
        NumberView v = viewPool.Count > 0 ? viewPool.Pop() : CreateView();
        v.root.gameObject.SetActive(true);
        v.root.SetAsLastSibling();
        return v;
    }

    private void ReleaseView(NumberView v)
    {
        v.root.gameObject.SetActive(false);
        v.builtText = null;
        viewPool.Push(v);
    }

    private NumberView CreateView()
    {
        var v = new NumberView();
        v.root = NewRect("Damage Number", layer);
        v.ring = NewImage("Ring", v.root, "ring");
        v.backRoot = NewRect("Back", v.root);
        v.faceRoot = NewRect("Face", v.root);
        v.badge = NewRect("Badge", v.root);
        v.badgeBorder = NewImage("Border", v.badge, "badge");
        v.badgeFill = NewImage("Fill", v.badge, "badge");
        v.badgeDiamond = NewImage("Diamond", v.badge, "diamond");
        v.badgeIcon = NewImage("Icon", v.badge, null);
        v.badgeIcon.preserveAspect = true;
        v.badgeIcon.rectTransform.sizeDelta = new Vector2(44f, 44f);
        v.badgeIcon.rectTransform.anchoredPosition = new Vector2(-44f, 1f);
        v.badgeChar = NewLabel("Attribute", v.badge, 17f);
        v.badgeLabel = NewLabel("Label", v.badge, 23f);
        v.badgeBorder.rectTransform.sizeDelta = new Vector2(128f, 36f);
        v.badgeFill.rectTransform.sizeDelta = new Vector2(122f, 31f);
        v.badgeFill.color = new Color(14f / 255f, 8f / 255f, 6f / 255f, .9f);
        v.badgeDiamond.rectTransform.sizeDelta = new Vector2(34f, 34f);
        v.badgeDiamond.rectTransform.anchoredPosition = new Vector2(-42f, 0f);
        v.badgeChar.rectTransform.anchoredPosition = new Vector2(-42f, 1f);
        v.badgeChar.color = new Color32(18, 8, 4, 255);
        v.badgeLabel.rectTransform.anchoredPosition = new Vector2(12f, 1f);
        v.badgeLabel.text = "克制";
        v.tag = NewLabel("Tag", v.root, 24f);
        v.tag.text = "击杀";
        v.tag.color = new Color32(255, 176, 74, 255);
        return v;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    private Image NewImage(string name, Transform parent, string fxName)
    {
        Image image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        image.raycastTarget = false;
        if (fxName != null && fx.TryGetValue(fxName, out Sprite sprite)) image.sprite = sprite;
        return image;
    }

    private TMP_Text NewLabel(string name, Transform parent, float size)
    {
        TextMeshProUGUI label = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.rectTransform.SetParent(parent, false);
        label.rectTransform.sizeDelta = new Vector2(160f, 40f);
        if (labelFont != null) label.font = labelFont;
        if (labelMaterial != null) label.fontSharedMaterial = labelMaterial;
        label.fontSize = size;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        return label;
    }
    #endregion

    #region 对外接口
    /// <summary>由 BattleDamagePlan 在每段命中时调用。</summary>
    public void Hit(int actionKey, BattleDamagePlan plan, BattleDamagePlan.Entry entry, long amount, int impactCount, bool final, bool heavy)
    {
        if (!showNumbers || entry.anchor == null || layer == null || !EnsureFont()) return;
        layer.SetAsLastSibling();
        layer.localScale = Vector3.one * Mathf.Max(.01f, entry.anchor.PresentationScale);
        Counter counter = active.Find(c => !c.tick && c.actionKey == actionKey && c.entry == entry);
        if (counter == null)
        {
            counter = new Counter
            {
                view = TakeView(), actionKey = actionKey, entry = entry, anchor = entry.anchor,
                impactCount = impactCount, direction = DirectionAway(plan, entry)
            };
            counter.position = AnchorPosition(entry, counter.direction);
            active.Add(counter);
        }
        counter.target += amount;
        counter.hits++;
        counter.punch = 1f;
        bool restrainedSkin = Restrained(entry);
        if (restrainedSkin)
        {
            counter.shakeAngle = UnityEngine.Random.Range(-3.5f, 3.5f);
            Burst(counter.position, entry.attribute, final && impactCount > 1 ? 1.3f : 1f);
        }
        if (final)
        {
            counter.done = true;
            counter.doneAge = 0f;
            counter.slam = 1f;
            counter.heavyFinal = heavy;
        }
        if (amount > 0 && impactCount > 1 && entry.kind != BattleDamageNumberKind.Heal)
        {
            var tick = new Counter
            {
                view = TakeView(), tick = true, anchor = entry.anchor, entry = entry, target = amount,
                direction = counter.direction, position = counter.position,
                tickStyle = restrainedSkin ? StyleKeyForAttribute(entry.attribute) : entry.kind == BattleDamageNumberKind.Hurt ? "hurt" : heavy ? "heavy" : "light",
                tickHeavy = heavy
            };
            active.Add(tick);
        }
    }

    public void Clear()
    {
        foreach (Counter c in active) ReleaseView(c.view);
        active.Clear();
        foreach (Particle p in particles) { p.image.gameObject.SetActive(false); particlePool.Push(p.image); }
        particles.Clear();
    }
    #endregion

    private bool Restrained(BattleDamagePlan.Entry entry)
    {
        if (!entry.restrained) return false;
        return entry.kind == BattleDamageNumberKind.Damage || (entry.kind == BattleDamageNumberKind.Hurt && showRestraintOnPlayerDamage);
    }

    private static float DirectionAway(BattleDamagePlan plan, BattleDamagePlan.Entry entry)
    {
        if (entry.kind == BattleDamageNumberKind.Heal || plan.attackerAnchor == null) return 0f;
        float dx = entry.anchor.BodyCenter.x - plan.attackerAnchor.BodyCenter.x;
        return Mathf.Abs(dx) < .01f ? 0f : Mathf.Sign(dx);
    }

    // 数字落点：身体中部偏上，向远离攻击者的方向偏一点（与预览一致）
    private Vector2 AnchorPosition(BattleDamagePlan.Entry entry, float direction)
    {
        BattleActorAnimation anchor = entry.anchor;
        Vector2 local = layer.InverseTransformPoint(anchor.FootPosition);
        float scale = Mathf.Max(.01f, LayerScale());
        float body = anchor.BodyHeight / scale;
        bool friendly = entry.kind == BattleDamageNumberKind.Hurt || entry.kind == BattleDamageNumberKind.Heal;
        return local + new Vector2(direction * body * (friendly ? .32f : .2f) + UnityEngine.Random.Range(-10f, 10f),
            body * (friendly ? .62f : .5f) + UnityEngine.Random.Range(-6f, 6f));
    }

    // BodyHeight 以根画布坐标计；这里换算 layer 相对根画布的缩放（通常为 1）
    private float LayerScale() => canvasRoot == null ? 1f : layer.lossyScale.y / Mathf.Max(.0001f, canvasRoot.lossyScale.y);

    private void LateUpdate()
    {
        if (active.Count == 0 && particles.Count == 0) return;
        bool paused = pauseQuery != null && pauseQuery();
        float dt = paused ? 0f : Time.deltaTime;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Counter c = active[i];
            float local = c.anchor != null && c.anchor.HitStopped ? 0f : dt;   // 与目标的卡肉同步定格
            bool alive = c.tick ? UpdateTick(c, local) : UpdateCounter(c, local);
            if (!alive) { ReleaseView(c.view); active.RemoveAt(i); }
        }
        UpdateParticles(dt);
    }

    private static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

    private bool UpdateCounter(Counter c, float dt)
    {
        c.age += dt;
        if (c.done) c.doneAge += dt;
        if (c.done && c.doneAge > .95f) return false;
        c.punch = Mathf.Max(0f, c.punch - dt / .14f);
        c.slam = Mathf.Max(0f, c.slam - dt / .18f);

        BattleDamagePlan.Entry e = c.entry;
        bool restrained = Restrained(e);
        bool multi = c.impactCount > 1;
        string style; float size;
        switch (e.kind)
        {
            case BattleDamageNumberKind.Heal: style = "heal"; size = SizeHeal; break;
            case BattleDamageNumberKind.Hurt: style = restrained ? StyleKeyForAttribute(e.attribute) : "hurt"; size = SizeHurt; break;
            default:
                if (c.done && e.defeated) { style = "kill"; size = SizeKill; }
                else if (restrained) { style = StyleKeyForAttribute(e.attribute); size = c.done && c.heavyFinal ? SizeHeavy : SizeNormal; }
                else if (c.done && c.heavyFinal) { style = "heavy"; size = SizeHeavy; }
                else { style = "normal"; size = SizeNormal; }
                break;
        }
        if (c.done && multi) size *= 1.1f;
        if (restrained) size *= 1.12f;

        float scale = c.age < .06f ? .6f + .9f * EaseOut(c.age / .06f) : 1f;
        scale *= 1f + .32f * c.punch * c.punch + .45f * c.slam * c.slam;
        float alpha = 1f, rise = 0f;
        if (c.done && c.doneAge > .6f)
        {
            float q = (c.doneAge - .6f) / .35f;
            alpha = 1f - q;
            rise = 34f * EaseOut(q);
        }
        string text = (e.kind == BattleDamageNumberKind.Heal ? "+" : string.Empty) + Format(c.target);
        Build(c.view, text, style, size);
        NumberView v = c.view;
        v.root.anchoredPosition = c.position + Vector2.up * rise;
        v.root.localScale = Vector3.one * scale;
        v.root.localRotation = Quaternion.Euler(0f, 0f, restrained && c.punch > .6f ? c.shakeAngle : 0f);
        SetAlpha(v, alpha);

        bool ring = c.done && multi && c.slam > 0f;
        v.ring.gameObject.SetActive(ring);
        if (ring)
        {
            float grow = 1.4f - c.slam * .4f;
            v.ring.rectTransform.sizeDelta = new Vector2(size * 2.8f * grow, size * 1.2f * grow);
            Color ringColor = restrained ? AttributeColor(e.attribute) : new Color32(255, 224, 140, 255);
            ringColor.a = alpha * c.slam * .8f;
            v.ring.color = ringColor;
        }
        bool showBadge = restrained && style != "kill" && labelFont != null;
        v.badge.gameObject.SetActive(showBadge);
        if (showBadge)
        {
            Color color = AttributeColor(e.attribute);
            v.badge.anchoredPosition = new Vector2(0f, size * .95f);
            v.badgeBorder.color = new Color(color.r, color.g, color.b, alpha);
            Sprite icon = AttributeIcon(e.attribute);
            v.badgeIcon.gameObject.SetActive(icon != null);           // 有属性图标用图标，没有则退回菱形 + 单字
            v.badgeDiamond.gameObject.SetActive(icon == null);
            v.badgeChar.gameObject.SetActive(icon == null);
            if (icon != null) { v.badgeIcon.sprite = icon; v.badgeIcon.color = new Color(1f, 1f, 1f, alpha); }
            v.badgeDiamond.color = new Color(color.r, color.g, color.b, alpha);
            v.badgeLabel.color = new Color(color.r, color.g, color.b, alpha);
            v.badgeChar.text = AttributeChar(e.attribute);
        }
        bool showTag = style == "kill" && labelFont != null;
        v.tag.gameObject.SetActive(showTag);
        if (showTag) v.tag.rectTransform.anchoredPosition = new Vector2(0f, size * .95f);
        return true;
    }

    private bool UpdateTick(Counter c, float dt)
    {
        c.age += dt;
        if (c.age > .5f) return false;
        float q = c.age / .5f;
        Build(c.view, Format(c.target), c.tickStyle, c.tickHeavy ? 30f : SizeTick);
        float side = c.direction == 0f ? 1f : c.direction;
        c.view.root.anchoredPosition = c.position + new Vector2(side * (60f + 40f * EaseOut(q)), 34f + 46f * EaseOut(q));
        c.view.root.localScale = Vector3.one;
        c.view.root.localRotation = Quaternion.identity;
        c.view.ring.gameObject.SetActive(false);
        c.view.badge.gameObject.SetActive(false);
        c.view.tag.gameObject.SetActive(false);
        SetAlpha(c.view, 1f - q * q);
        return true;
    }

    private static string Format(long value) => value.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);

    // 排版：先排全部 back 层，再排全部 face 层，相邻字的描边与厚度自然融合
    private void Build(NumberView v, string text, string style, float size)
    {
        if (v.builtText == text && v.builtStyle == style && Mathf.Approximately(v.builtSize, size)) return;
        v.builtText = text; v.builtStyle = style; v.builtSize = size;
        Dictionary<char, Glyph> glyphs = Style(style);
        float u = size / 10f, k = size / font.renderSize;
        float total = -font.tracking;
        foreach (char ch in text) if (glyphs.TryGetValue(ch, out Glyph g)) total += g.advance;
        float pen = 0f;
        int index = 0;
        foreach (char ch in text)
        {
            if (!glyphs.TryGetValue(ch, out Glyph g)) continue;
            var position = new Vector2(u * (pen - total * .5f) - font.shear * u * 5f, 5f * u);
            Place(v.back, v.backRoot, index, g.back, g, position, k);
            Place(v.face, v.faceRoot, index, g.face, g, position, k);
            pen += g.advance;
            index++;
        }
        for (int i = index; i < v.back.Count; i++) { v.back[i].enabled = false; v.face[i].enabled = false; }
    }

    private void Place(List<Image> list, RectTransform parent, int index, Sprite sprite, Glyph g, Vector2 position, float k)
    {
        while (list.Count <= index) list.Add(NewImage("Glyph", parent, null));
        Image image = list[index];
        image.enabled = true;
        image.sprite = sprite;
        RectTransform rect = image.rectTransform;
        rect.pivot = g.pivot;
        rect.sizeDelta = g.cellSize * k;
        rect.anchoredPosition = position;
    }

    private static void SetAlpha(NumberView v, float alpha)
    {
        Color white = new Color(1f, 1f, 1f, alpha);
        foreach (Image image in v.back) image.color = white;
        foreach (Image image in v.face) image.color = white;
        v.badgeFill.color = new Color(v.badgeFill.color.r, v.badgeFill.color.g, v.badgeFill.color.b, .9f * alpha);
        v.badgeChar.alpha = alpha;
        v.tag.alpha = alpha;
    }

    #region 属性粒子
    private void Burst(Vector2 at, string attribute, float strength)
    {
        if (fx.Count == 0) return;
        string sprite; float gravity; int count = 16;
        switch (attribute)
        {
            case "fire": sprite = "ember"; gravity = 260f; break;      // 画布 y 向上，正值 = 向上飘
            case "water": sprite = "shard"; gravity = -700f; break;   // 冰：冰晶碎片
            case "wood": sprite = "streak"; gravity = 60f; break;     // 风：气流弧线
            case "poison": sprite = "bubble"; gravity = 160f; count = 12; break;
            case "earth": sprite = "rock"; gravity = -1100f; break;
            case "light": sprite = "star"; gravity = 0f; count = 12; break;
            case "dark": sprite = "smoke"; gravity = 80f; count = 10; break;    // 暗：暗影烟雾
            default: sprite = "spark"; gravity = -600f; break;
        }
        Color color = AttributeColor(attribute);
        for (int i = 0; i < count && particles.Count < MaxParticles; i++)
        {
            float angle = UnityEngine.Random.value * Mathf.PI * 2f, speed = UnityEngine.Random.Range(120f, 380f) * strength;
            particles.Add(MakeParticle(sprite, color, at, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * .7f) * speed,
                gravity, UnityEngine.Random.Range(.45f, .8f), UnityEngine.Random.Range(14f, 26f), attribute == "dark" ? 2f : 0f, false));
        }
        if ((attribute == "light" || attribute == "physical") && particles.Count < MaxParticles)
            particles.Add(MakeParticle("ring", color, at, Vector2.zero, 0f, .25f, 70f * strength, 3f, true));
    }

    private Particle MakeParticle(string sprite, Color color, Vector2 at, Vector2 velocity, float gravity, float life, float size, float growth, bool ring)
    {
        Image image = particlePool.Count > 0 ? particlePool.Pop() : NewImage("Particle", layer, null);
        image.gameObject.SetActive(true);
        image.rectTransform.SetAsLastSibling();
        if (fx.TryGetValue(sprite, out Sprite s)) image.sprite = s;
        image.color = color;
        return new Particle
        {
            image = image, position = at, velocity = velocity, gravity = gravity, life = life, size = size,
            growth = growth, ring = ring, spin = UnityEngine.Random.Range(-480f, 480f)
        };
    }

    private void UpdateParticles(float dt)
    {
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            Particle p = particles[i];
            p.age += dt;
            if (p.age >= p.life)
            {
                p.image.gameObject.SetActive(false);
                particlePool.Push(p.image);
                particles.RemoveAt(i);
                continue;
            }
            float k = p.age / p.life;
            p.velocity.y += p.gravity * dt;
            p.velocity *= Mathf.Pow(.98f, dt * 60f);
            p.position += p.velocity * dt;
            RectTransform rect = p.image.rectTransform;
            rect.anchoredPosition = p.position;
            float size = p.size * (1f + p.growth * k);
            rect.sizeDelta = p.ring ? new Vector2(size * 2f, size * 1.1f) : new Vector2(size, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, p.ring ? 0f : p.spin * p.age);
            Color c = p.image.color;
            c.a = 1f - k;
            p.image.color = c;
        }
    }
    #endregion

    private void OnDisable() => Clear();

    private void OnDestroy()
    {
        foreach (var glyphs in styles.Values)
            foreach (Glyph glyph in glyphs.Values)
            {
                Destroy(glyph.back);
                Destroy(glyph.face);
            }
        foreach (Sprite sprite in fx.Values) Destroy(sprite);
        if (labelMaterial != null) Destroy(labelMaterial);
    }
}
