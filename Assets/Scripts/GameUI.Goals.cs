using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The level's goals in the HUD (under the atom card): a line of photons, then a line of quanta.
/// Each line stays one row (the pips shrink to fit, never wrap), grouped in fives. An empty
/// slot is an outlined circle in the goal's color; a collected one is its glowing icon, which
/// pops when it fills. A collected photon / quantum flies from the map to its slot first.
/// </summary>
public partial class GameUI
{
    const float GoalLineWidth = 270f;   // a line never gets wider than this
    const float GoalGroupGap = 9f;      // extra space between groups of five
    const float GoalFlyTime = 0.5f;

    class GoalSlot
    {
        public VisualElement root, glow, ring, icon;
        public float pop;
        public bool on;
    }

    class GoalPips
    {
        public VisualElement row;
        public Sprite sprite;
        public Color color;
        public float baseSize, size;
        public readonly List<GoalSlot> slots = new List<GoalSlot>();
        public int inFlight;     // collected, but its icon is still flying to the HUD
        public int lastFilled;
    }

    class GoalFlyer
    {
        public VisualElement e;
        public GoalPips line;
        public int index;
        public Vector2 from;
        public float t;
    }

    GoalPips starGoal, quantaGoal;
    readonly List<GoalFlyer> goalFlyers = new List<GoalFlyer>();

    GoalPips MakeGoalPips(VisualElement parent, Sprite sprite, Color color, float size)
    {
        var g = new GoalPips { row = Row(parent), sprite = sprite, color = color, baseSize = size };
        g.row.style.flexWrap = Wrap.NoWrap;
        g.row.style.alignItems = Align.Center;
        g.row.style.height = size + 4f;
        return g;
    }

    void RebuildGoalPips(GoalPips g, int count)
    {
        g.row.Clear();
        g.slots.Clear();
        // Shrink to keep one row: slots + small gaps + a wider gap between groups of five.
        int groups = Mathf.Max(0, (count - 1) / 5);
        const float Gap = 3f;
        float fit = (GoalLineWidth - groups * GoalGroupGap - count * Gap) / Mathf.Max(1, count);
        g.size = Mathf.Clamp(fit, 10f, g.baseSize);
        g.row.style.height = g.size + 4f;
        for (int i = 0; i < count; i++)
        {
            var s = new GoalSlot { root = Box(g.row) };
            s.root.style.width = s.root.style.height = g.size;
            s.root.style.marginRight = Gap;
            if (i > 0 && i % 5 == 0) s.root.style.marginLeft = GoalGroupGap;
            s.root.style.alignItems = Align.Center;
            s.root.style.justifyContent = Justify.Center;

            s.glow = Icon(s.root, ProcSprites.Glow, g.size * 1.9f, WithAlpha(g.color, 0f));
            s.glow.style.position = Position.Absolute;
            s.glow.style.left = s.glow.style.top = -g.size * 0.45f;

            float rs = g.size * 0.62f;   // the empty slot: a crisp outlined circle
            s.ring = Box(s.root);
            s.ring.style.position = Position.Absolute;
            s.ring.style.width = s.ring.style.height = rs;
            s.ring.style.left = s.ring.style.top = (g.size - rs) * 0.5f;
            Radius(s.ring, rs * 0.5f);
            Border(s.ring, 1.5f, WithAlpha(g.color, 0.5f));

            s.icon = Icon(s.root, g.sprite, g.size, g.color);
            s.icon.style.position = Position.Absolute;
            s.icon.style.left = s.icon.style.top = 0;
            g.slots.Add(s);
        }
    }

    /// <summary>'pulseFrom'..'pulseTo' (exclusive) pulse: quanta unlocked this level, not yet collected.</summary>
    void UpdateGoalPips(GoalPips g, int count, int filled, float dt, int pulseFrom = 0, int pulseTo = 0)
    {
        if (count != g.slots.Count) RebuildGoalPips(g, count);
        if (filled < g.lastFilled) { DropFlyers(g); }   // a new level (or retry): start clean
        g.lastFilled = filled;
        int shown = Mathf.Max(0, filled - g.inFlight);
        float ut = Time.unscaledTime;
        for (int i = 0; i < g.slots.Count; i++)
        {
            var s = g.slots[i];
            bool on = i < shown;
            if (on && !s.on) s.pop = 1f;   // just landed
            s.on = on;
            s.pop = Mathf.MoveTowards(s.pop, 0f, dt / 0.4f);
            float p = s.pop * s.pop;

            s.icon.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            s.ring.style.display = on ? DisplayStyle.None : DisplayStyle.Flex;
            s.glow.style.unityBackgroundImageTintColor = WithAlpha(g.color, on ? 0.22f + 0.55f * p : 0f);
            float sc = 1f + 0.6f * p;
            if (!on && i >= pulseFrom && i < pulseTo) sc *= 1.1f + 0.15f * Mathf.Sin(ut * 6f);
            s.root.style.scale = new Scale(new Vector3(sc, sc, 1f));
        }
    }

    void DropFlyers(GoalPips g)
    {
        for (int i = goalFlyers.Count - 1; i >= 0; i--)
            if (goalFlyers[i].line == g) { goalFlyers[i].e.RemoveFromHierarchy(); goalFlyers.RemoveAt(i); }
        g.inFlight = 0;
    }

    /// <summary>A photon / quantum was collected at 'world': its icon flies to the next slot in the HUD.</summary>
    public static void GoalCollected(PickupKind kind, Vector2 world)
    {
        if (instance == null || instance.floatLayer == null) return;
        GoalPips g = kind == PickupKind.Star ? instance.starGoal : kind == PickupKind.Coin ? instance.quantaGoal : null;
        if (g == null) return;
        var cam = Camera.main;
        if (cam == null || instance.root.panel == null) return;
        Vector2 from = RuntimePanelUtils.CameraTransformWorldToPanel(instance.root.panel, world, cam);
        int index = (kind == PickupKind.Star ? instance.gm.StarsCollected : instance.gm.LevelCoins) - 1;
        var e = Icon(instance.floatLayer, g.sprite, 28f, g.color);
        e.style.position = Position.Absolute;
        g.inFlight++;
        instance.goalFlyers.Add(new GoalFlyer { e = e, line = g, index = index, from = from });
        instance.PlaceFlyer(instance.goalFlyers[instance.goalFlyers.Count - 1]);
    }

    void UpdateGoalFlyers(float dt)
    {
        for (int i = goalFlyers.Count - 1; i >= 0; i--)
        {
            var f = goalFlyers[i];
            f.t += dt / GoalFlyTime;
            if (f.t >= 1f)
            {
                f.e.RemoveFromHierarchy();
                goalFlyers.RemoveAt(i);
                f.line.inFlight = Mathf.Max(0, f.line.inFlight - 1);
                Sfx.Play("tick", 0.35f);
                continue;
            }
            PlaceFlyer(f);
        }
    }

    void PlaceFlyer(GoalFlyer f)
    {
        Vector2 to = f.from;
        if (f.index >= 0 && f.index < f.line.slots.Count)
        {
            Rect wb = f.line.slots[f.index].root.worldBound;
            if (!float.IsNaN(wb.x)) to = floatLayer.WorldToLocal(wb.center);
        }
        // Eases in, along a curve that bows outward a little.
        float t = Mathf.Clamp01(f.t);
        float k = t * t * (3f - 2f * t);
        Vector2 mid = (f.from + to) * 0.5f + new Vector2(60f, 40f);
        Vector2 a = Vector2.Lerp(f.from, mid, k), b = Vector2.Lerp(mid, to, k);
        Vector2 p = Vector2.Lerp(a, b, k);
        float size = Mathf.Lerp(28f, f.line.size, k);
        f.e.style.width = f.e.style.height = size;
        f.e.style.left = p.x - size * 0.5f;
        f.e.style.top = p.y - size * 0.5f;
        f.e.style.opacity = Mathf.Clamp01(t * 6f);
    }
}
