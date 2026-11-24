using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum BattleUnitRank { Friendly, Mob, Elite }

/// <summary>
/// 敌人等级表：小怪名单由策划直接指定（填 AppearanceKey 或角色名，不区分大小写）。
/// 不在名单里的敌人一律按精英显示；我方单位恒为 Friendly。
/// </summary>
public static class BattleUnitRankTable
{
    public static readonly HashSet<string> Mobs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // 示例（按实际名单替换）：
        // "enemy_forest_slime",
        // "森林史莱姆",
    };

    public static BattleUnitRank Resolve(IdleAdventureCombatant combatant)
    {
        if (combatant == null || combatant.IsFriendly) return BattleUnitRank.Friendly;
        if (!string.IsNullOrWhiteSpace(combatant.AppearanceKey) && Mobs.Contains(combatant.AppearanceKey.Trim())) return BattleUnitRank.Mob;
        if (!string.IsNullOrWhiteSpace(combatant.CombatantName) && Mobs.Contains(combatant.CombatantName.Trim())) return BattleUnitRank.Mob;
        return BattleUnitRank.Elite;
    }
}

/// <summary>
/// 血条 A「刃锋」：斜切平行四边形，血量数字居中写在条内（小怪不显示数字）。
/// 一个 Graphic 画完外框、凹槽、残影、治疗预显、渐变填充、高光、刻度，整条只占一次绘制。
/// 只负责显示；血量数据仍由战斗结算写入（SetHealth），多段攻击可用 PreviewDamage 先逐段下降。
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class BattleHealthBar : MaskableGraphic
{
    // ---------- 1080p 参考尺寸（画布单位），实际再乘 PresentationScale ----------
    public static Vector2 SizeFor(BattleUnitRank rank)
    {
        switch (rank)
        {
            case BattleUnitRank.Mob: return new Vector2(100f, 7f);
            case BattleUnitRank.Elite: return new Vector2(176f, 20f);
            default: return new Vector2(132f, 18f);
        }
    }

    [Tooltip("我方血条显示为「当前 / 上限」；关闭则只显示当前值。")]
    public bool showMaxForFriendly;

    // 动效参数
    private const float TrailHold = .35f;        // 残影停留（单段 / 最后一段）
    private const float TrailHoldMulti = .5f;    // 多段攻击中间段的残影停留，等最后一下再收
    private const float TrailSpeed = .9f;        // 残影 / 治疗增长速度（每秒占整条的比例）
    private const float FlashTime = .09f;        // 被扣掉那段亮白的时长
    private const float ShakeTime = .14f;        // 横向抖动时长
    private const float PopTime = .18f;          // 数字弹跳时长
    private const float LowRatio = .25f;         // 我方残血阈值

    // 配色
    private static readonly Color32 OutlineColor = new Color32(10, 7, 12, 230);
    private static readonly Color32 GrooveColor = new Color32(36, 32, 46, 255);
    private static readonly Color32 TrailColor = new Color32(255, 214, 160, 242);
    private static readonly Color32 TrailFlashColor = new Color32(255, 250, 240, 255);
    private static readonly Color32 GhostColor = new Color32(190, 255, 210, 217);
    private static readonly Color32 HighlightColor = new Color32(255, 255, 255, 77);
    private static readonly Color32 TickColor = new Color32(10, 7, 12, 107);
    private static readonly Color32 LowPulseColor = new Color32(255, 70, 50, 255);
    private static readonly Color32[] EnemyFill = { new Color32(255, 122, 98, 255), new Color32(194, 47, 40, 255) };
    private static readonly Color32[] FriendHigh = { new Color32(143, 240, 176, 255), new Color32(51, 180, 106, 255) };
    private static readonly Color32[] FriendMid = { new Color32(255, 224, 130, 255), new Color32(209, 154, 34, 255) };
    private static readonly Color32[] FriendLow = { new Color32(255, 138, 106, 255), new Color32(210, 59, 43, 255) };

    private BattleUnitRank rank;
    private bool friendly;
    private TMP_Text label;
    private Func<bool> pauseQuery;

    private int identity = int.MinValue;
    private long max = 1, current, numberTarget;
    private float disp = 1f, trail = 1f, ghost;
    private double shownHp;
    private float hold, flash, shake, pop;
    private float activeTime;
    private long lastNumber = -1;
    private bool lastShowMax;

    public BattleUnitRank Rank => rank;

    public void SetPauseQuery(Func<bool> query)
    {
        pauseQuery = query;
    }

    // ---------- 对外接口 ----------

    /// <summary>设定等级与数字标签（标签作为本条的兄弟节点，排在本条之后）。</summary>
    public void Configure(BattleUnitRank unitRank, bool isFriendly, TMP_Text number)
    {
        bool changed = rank != unitRank || friendly != isFriendly || label != number;
        rank = unitRank;
        friendly = isFriendly;
        label = number;
        raycastTarget = false;
        if (label != null)
        {
            label.gameObject.SetActive(rank != BattleUnitRank.Mob);
            if (changed)
            {
                StyleLabel();
                lastNumber = -1;
            }
        }
        if (changed) SetVerticesDirty();
    }

    /// <summary>每次刷新视图时调用，写入服务器结算后的真实血量。</summary>
    public void SetHealth(int unitIdentity, long currentHealth, long maxHealth)
    {
        maxHealth = Math.Max(1L, maxHealth);
        currentHealth = Math.Max(0L, Math.Min(maxHealth, currentHealth));
        float ratio = currentHealth / (float)maxHealth;
        if (unitIdentity != identity || maxHealth != max)
        {
            // 新单位或上限变化：直接到位，不播动画
            identity = unitIdentity;
            max = maxHealth;
            current = numberTarget = currentHealth;
            shownHp = currentHealth;
            disp = trail = ratio;
            ghost = hold = flash = shake = pop = 0f;
            activeTime = 0f;
            RefreshNumber(true);
            SetVerticesDirty();
            return;
        }
        if (currentHealth == current) return;
        if (currentHealth < current)
        {
            // 掉血：如果多段预演已经降到位，只做校准，不重复闪白
            if (currentHealth < numberTarget) BeginDrop(ratio, TrailHold);
            else
            {
                disp = ratio;
                ghost = 0f;
            }
        }
        else
        {
            // 治疗：先出浅绿预显段，填充随后长上去
            trail = disp;
            hold = flash = 0f;
            ghost = ratio;
            pop = 1f;
        }
        current = numberTarget = currentHealth;
        SetVerticesDirty();
    }

    /// <summary>多段攻击每段命中时调用：血条按本段伤害先降一截，最后一段结算后由 SetHealth 校准。</summary>
    public void PreviewDamage(long amount, bool finalSegment)
    {
        if (amount <= 0) return;
        numberTarget = Math.Max(0L, numberTarget - amount);
        BeginDrop(numberTarget / (float)max, finalSegment ? TrailHold : TrailHoldMulti);
    }

    private void BeginDrop(float ratio, float holdTime)
    {
        trail = Mathf.Max(trail, Mathf.Max(disp, ghost));
        disp = ratio;
        ghost = 0f;
        hold = holdTime;
        flash = FlashTime;
        shake = ShakeTime;
        pop = 1f;
        SetVerticesDirty();
    }

    // ---------- 动画 ----------

    private void Update()
    {
        if (pauseQuery != null && pauseQuery()) return;
        float dt = Time.deltaTime;
        activeTime += dt;
        bool dirty = false;
        if (hold > 0f) hold -= dt;
        else if (trail > disp) { trail = Mathf.Max(disp, trail - TrailSpeed * dt); dirty = true; }
        if (trail < disp) trail = disp;
        if (ghost > 0f)
        {
            disp = Mathf.Min(ghost, disp + TrailSpeed * dt);
            trail = Mathf.Max(trail, disp);
            if (disp >= ghost) ghost = 0f;
            dirty = true;
        }
        if (flash > 0f) { flash -= dt; dirty = true; }
        if (shake > 0f) { shake -= dt; dirty = true; }
        if (friendly && disp > 0f && disp <= LowRatio) dirty = true;   // 残血外框闪烁
        if (pop > 0f) pop = Mathf.Max(0f, pop - dt / PopTime);
        if (Math.Abs(shownHp - numberTarget) > .5d) shownHp += (numberTarget - shownHp) * Math.Min(1d, dt * 18d);
        else shownHp = numberTarget;
        RefreshNumber(false);
        if (dirty) SetVerticesDirty();
    }

    public void ResetPresentation()
    {
        if (identity == int.MinValue) return;
        identity = int.MinValue;
        numberTarget = current;
        shownHp = current;
        disp = trail = current / (float)max;
        ghost = hold = flash = shake = pop = activeTime = 0f;
        RefreshNumber(true);
        SetVerticesDirty();
    }

    private void RefreshNumber(bool force)
    {
        if (label == null || rank == BattleUnitRank.Mob) return;
        long value = (long)Math.Round(shownHp, MidpointRounding.AwayFromZero);
        bool includeMax = friendly && showMaxForFriendly;
        if (force || value != lastNumber || includeMax != lastShowMax)
        {
            lastNumber = value;
            lastShowMax = includeMax;
            string digits = value.ToString("#,0", CultureInfo.InvariantCulture);
            label.text = includeMax ? digits + " / " + max.ToString("#,0", CultureInfo.InvariantCulture) : digits;
        }
        float s = 1f + .18f * pop * pop;
        label.rectTransform.localScale = new Vector3(s, s, 1f);
    }

    private void StyleLabel()
    {
        TMP_FontAsset fontAsset = RuntimeFontAssetProvider.Resolve();
        if (fontAsset != null) label.font = fontAsset;
        label.enableAutoSizing = false;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.richText = false;
        label.raycastTarget = false;
        label.margin = Vector4.zero;
        label.outlineColor = new Color32(24, 16, 14, 255);
        label.outlineWidth = .20f;
        // Widen the white strokes on this label's own SDF material at foot-bar sizes.
        label.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, .42f);
        label.UpdateMeshPadding();
        RectTransform rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        UpdateLabelSize();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        UpdateLabelSize();
    }

    private void UpdateLabelSize()
    {
        if (label == null) return;
        label.fontSize = Mathf.Max(1f, rectTransform.rect.height * .90f);
        label.rectTransform.anchoredPosition = new Vector2(0f, rectTransform.rect.height * .03f);
    }

    // ---------- 网格 ----------

    private float x0, y0, w, h, slant;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        if (r.width <= 0f || r.height <= 0f) return;
        float k = r.height / SizeFor(rank).y;             // 相对 1080p 的缩放
        h = r.height;
        w = r.width;
        slant = -h * .55f;                                 // 顶边相对底边左移，形成右倾刀口
        x0 = r.xMin - slant * .5f;                         // 让平行四边形整体居中
        y0 = r.yMin;
        if (shake > 0f) x0 += Mathf.Sin(activeTime * 95f) * 2.2f * k * (shake / ShakeTime);

        // 外框（残血时闪红）
        Color32 outline = OutlineColor;
        if (friendly && disp > 0f && disp <= LowRatio)
            outline = Color32.Lerp(OutlineColor, LowPulseColor, .4f + .4f * Mathf.Sin(activeTime * 7f));
        Para(vh, x0 - 3f * k, y0 - 2.5f * k, w + 6f * k, h + 5f * k, outline, outline);

        // 凹槽
        Slice(vh, 0f, 1f, 0f, 1f, GrooveColor, GrooveColor);
        // 残影
        if (trail > disp) { Color32 c = flash > 0f ? TrailFlashColor : TrailColor; Slice(vh, disp, trail, 0f, 1f, c, c); }
        // 治疗预显
        if (ghost > disp) Slice(vh, disp, ghost, 0f, 1f, GhostColor, GhostColor);
        // 渐变填充 + 顶部高光
        if (disp > 0f)
        {
            Color32[] fill = FillColors();
            Slice(vh, 0f, disp, 0f, 1f, fill[1], fill[0]);
            Slice(vh, 0f, disp, .7f, 1f, HighlightColor, HighlightColor);
        }
        // 精英刻度：每 10% 一道，只画在下沿，避开居中的数字
        if (rank == BattleUnitRank.Elite)
            for (int i = 1; i < 10; i++)
            {
                float t = i / 10f, half = k / w;
                Slice(vh, t - half, t + half, 0f, .28f, TickColor, TickColor);
            }
    }

    private Color32[] FillColors()
    {
        if (!friendly) return EnemyFill;
        return disp <= LowRatio ? FriendLow : disp <= .5f ? FriendMid : FriendHigh;
    }

    // 内部平行四边形上 [t0,t1] × [v0,v1] 的一片（t 为血量比例，v 为高度比例）
    private void Slice(VertexHelper vh, float t0, float t1, float v0, float v1, Color32 bottom, Color32 top)
    {
        t0 = Mathf.Clamp01(t0); t1 = Mathf.Clamp01(t1);
        if (t1 <= t0) return;
        float yb = y0 + h * v0, yt = y0 + h * v1;
        Quad(vh,
            new Vector2(x0 + w * t0 + slant * v0, yb), new Vector2(x0 + w * t1 + slant * v0, yb),
            new Vector2(x0 + w * t1 + slant * v1, yt), new Vector2(x0 + w * t0 + slant * v1, yt),
            bottom, top);
    }

    private void Para(VertexHelper vh, float x, float y, float width, float height, Color32 bottom, Color32 top)
    {
        Quad(vh,
            new Vector2(x, y), new Vector2(x + width, y),
            new Vector2(x + width + slant, y + height), new Vector2(x + slant, y + height),
            bottom, top);
    }

    private void Quad(VertexHelper vh, Vector2 bl, Vector2 br, Vector2 tr, Vector2 tl, Color32 bottom, Color32 top)
    {
        Color tint = color;                                // Graphic.color 作为整体透明度 / 染色
        Color32 b = (Color)bottom * tint, t = (Color)top * tint;
        int start = vh.currentVertCount;
        vh.AddVert(bl, b, Vector2.zero);
        vh.AddVert(br, b, Vector2.zero);
        vh.AddVert(tr, t, Vector2.zero);
        vh.AddVert(tl, t, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }
}
