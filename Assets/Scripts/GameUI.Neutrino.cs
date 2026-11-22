using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Neutrinos: earned by finishing a level without losing stability (shown on the level complete
/// screen), counted in the HUD, and spent on the upgrade screen by clicking a locked petal. A
/// neutrino then flies from the counter into that petal, the lock trembles and shatters in a
/// pale flash, and an extra upgrade pops out in its place. It lasts for that one pick only.
/// </summary>
public partial class GameUI
{
    const float UnlockFly = 0.5f;       // counter -> petal
    const float UnlockShatter = 0.85f;  // the lock breaks
    const float UnlockDone = 1.25f;     // the new upgrade can be picked
    const int UnlockTrailDots = 8;
    const int UnlockShards = 10;

    class Unlock
    {
        public Petal petal;
        public float age;
        public Vector2 from, ctrl, to;               // upgradeScreen coordinates
        public VisualElement head;
        public readonly VisualElement[] trail = new VisualElement[UnlockTrailDots];
        public VisualElement burst;
        public readonly List<VisualElement> shards = new List<VisualElement>();
        public readonly List<Vector2> shardVel = new List<Vector2>();
        public bool arrived, shattered;
    }

    readonly List<Unlock> unlocks = new List<Unlock>();

    // HUD
    VisualElement hudNeutrinoRow, hudNeutrinoIcon;
    Label hudNeutrinoLabel;

    // Level complete
    VisualElement neutrinoAward, neutrinoAwardIcon;
    Label neutrinoAwardWhy, neutrinoAwardCount;

    // Upgrade screen
    VisualElement neutrinoCounter, neutrinoCounterIcon;
    Label neutrinoCounterLabel, neutrinoCounterHint;
    float neutrinoCounterPop;

    /// <summary>A gentle, uneven flicker: the ghost particle is barely there.</summary>
    static float GhostFlicker(float ut, float seed) => 0.7f + 0.3f * Mathf.PerlinNoise(ut * 2.6f, seed);

    // ------------------------------------------------------------------- HUD

    void BuildHudNeutrinos(VisualElement parent)
    {
        hudNeutrinoRow = Row(parent);
        hudNeutrinoRow.style.marginTop = 6;
        hudNeutrinoRow.style.alignItems = Align.Center;
        hudNeutrinoIcon = Icon(hudNeutrinoRow, ProcSprites.Neutrino, 30, Pickup.NeutrinoColor);
        hudNeutrinoLabel = Text(hudNeutrinoRow, "", 18, true, Pickup.NeutrinoColor, TextAnchor.MiddleLeft);
        hudNeutrinoLabel.style.marginLeft = 2;
        Show(hudNeutrinoRow, false);
    }

    void UpdateHudNeutrinos()
    {
        if (hudNeutrinoRow == null) return;
        int n = gm.Neutrinos;
        Show(hudNeutrinoRow, n > 0);
        if (n <= 0) return;
        hudNeutrinoLabel.text = "×" + n;
        hudNeutrinoIcon.style.opacity = GhostFlicker(Time.unscaledTime, 1.3f);
    }

    // -------------------------------------------------------- level complete

    void BuildNeutrinoAward(VisualElement parent)
    {
        neutrinoAward = Row(parent);
        neutrinoAward.style.marginTop = 18;
        neutrinoAward.style.alignItems = Align.Center;
        neutrinoAward.style.justifyContent = Justify.Center;
        neutrinoAwardIcon = Icon(neutrinoAward, ProcSprites.Neutrino, 54, Pickup.NeutrinoColor);
        var words = Box(neutrinoAward);
        words.style.marginLeft = 6;
        words.style.alignItems = Align.FlexStart;
        neutrinoAwardWhy = Text(words, "", 15, true, Dim, TextAnchor.MiddleLeft);
        neutrinoAwardWhy.style.letterSpacing = 6;
        neutrinoAwardCount = Text(words, "", 24, true, Pickup.NeutrinoColor, TextAnchor.MiddleLeft);
        neutrinoAwardCount.style.letterSpacing = 3;
        neutrinoAward.style.opacity = 0f;
    }

    bool neutrinoAwardSoundDone;

    void UpdateNeutrinoAward(int coins)
    {
        if (neutrinoAward == null) return;
        int earned = gm.LevelNeutrinos;
        if (earned <= 0) { Show(neutrinoAward, false); return; }
        Show(neutrinoAward, true);
        // Flawless, the Lonely Ion's free one, or both.
        neutrinoAwardWhy.text = gm.LevelFlawless && gm.IsLonelyIon ? Loc.T("FLAWLESS + LONELY ION", "KUSURSUZ + YALNIZ İYON")
                              : gm.LevelFlawless ? Loc.T("FLAWLESS", "KUSURSUZ") : Loc.T("LONELY ION", "YALNIZ İYON");
        neutrinoAwardCount.text = "+" + earned + (earned == 1 ? Loc.T(" NEUTRINO", " NÖTRİNO") : Loc.T(" NEUTRINOS", " NÖTRİNO"));
        float at = 0.55f + coins * 0.25f;   // after the quanta have popped in
        float t = gm.StateTime - at;
        if (t < 0f)
        {
            neutrinoAward.style.opacity = 0f;
            neutrinoAwardSoundDone = false;
            return;
        }
        if (!neutrinoAwardSoundDone)
        {
            neutrinoAwardSoundDone = true;
            Sfx.Play("neutrino", 0.9f);
        }
        float k = Mathf.Clamp01(t / 0.25f);
        float pop = 1f + 0.35f * (1f - k) * (1f - k);
        neutrinoAward.style.opacity = k;
        neutrinoAward.style.scale = new Scale(new Vector3(pop, pop, 1f));
        neutrinoAwardIcon.style.opacity = GhostFlicker(Time.unscaledTime, 2.1f);
    }

    // -------------------------------------------------------- upgrade screen

    void BuildNeutrinoCounter()
    {
        neutrinoCounter = Box(upgradeScreen);
        neutrinoCounter.style.position = Position.Absolute;
        neutrinoCounter.style.left = 34;
        neutrinoCounter.style.bottom = 30;
        neutrinoCounter.style.alignItems = Align.FlexStart;
        var row = Row(neutrinoCounter);
        row.style.alignItems = Align.Center;
        neutrinoCounterIcon = Icon(row, ProcSprites.Neutrino, 64, Pickup.NeutrinoColor);
        neutrinoCounterLabel = Text(row, "", 30, true, Pickup.NeutrinoColor, TextAnchor.MiddleLeft);
        neutrinoCounterLabel.style.marginLeft = 2;
        neutrinoCounterHint = Text(neutrinoCounter, Loc.T("Click a locked slot to open it", "Açmak için kilitli bir yuvaya tıkla"),
                                   15, false, Dim, TextAnchor.MiddleLeft);
        neutrinoCounterHint.style.marginLeft = 10;
        Show(neutrinoCounter, false);
    }

    void UpdateNeutrinoCounter(float dt)
    {
        if (neutrinoCounter == null) return;
        int n = gm.Neutrinos;
        bool on = (n > 0 || unlocks.Count > 0) && !gm.UpgradeFinishing && gm.StateTime >= gm.OfferOpenTime;
        Show(neutrinoCounter, on);
        if (!on) return;
        neutrinoCounterLabel.text = "×" + n;
        neutrinoCounterPop = Mathf.MoveTowards(neutrinoCounterPop, 0f, dt / 0.35f);
        float s = 1f + 0.3f * neutrinoCounterPop * neutrinoCounterPop;
        neutrinoCounterIcon.style.scale = new Scale(new Vector3(s, s, 1f));
        neutrinoCounterIcon.style.opacity = n > 0 ? GhostFlicker(Time.unscaledTime, 0.4f) : 0.25f;
        neutrinoCounterHint.style.opacity = n > 0 && PetalsLive ? 1f : 0f;
    }

    /// <summary>A locked petal was clicked: spend a neutrino on it, if there is one to spend.</summary>
    bool TryUnlockPetal(Petal p)
    {
        if (gm.Neutrinos <= 0) return false;
        int index = gm.SpendNeutrino();
        if (index < 0) return false;

        // The old lock stays on show until it shatters; the new upgrade is built beside it, hidden.
        p.lockContent = p.content;
        p.offer = index;
        SetupPetal(p);
        p.unlockAge = 0f;

        var u = new Unlock { petal = p, to = PetalCenter(p.index) };
        Rect icon = neutrinoCounterIcon.worldBound;
        u.from = float.IsNaN(icon.width) ? new Vector2(60f, AtomCenter.y * 2f - 60f) : upgradeScreen.WorldToLocal(icon.center);
        // Arc up and over, bending toward the atom's center so it seems to pass the core.
        Vector2 mid = (u.from + u.to) * 0.5f;
        u.ctrl = Vector2.Lerp(mid, AtomCenter, 0.55f) + new Vector2(0f, -90f);
        for (int j = UnlockTrailDots - 1; j >= 0; j--)
            u.trail[j] = Glow(upgradeScreen, 40f, Pickup.NeutrinoColor);
        u.head = Icon(upgradeScreen, ProcSprites.Neutrino, 70f, Color.white);
        u.head.style.position = Position.Absolute;
        unlocks.Add(u);

        neutrinoCounterPop = 1f;
        Sfx.Play("charge", 0.55f);
        hoverShown = -99;   // refresh the panel if it's open on this petal
        return true;
    }

    Vector2 UnlockPath(Unlock u, float t)
    {
        float v = 1f - t;
        return v * v * u.from + 2f * v * t * u.ctrl + t * t * u.to;
    }

    void UpdateUnlocks(float dt)
    {
        for (int i = unlocks.Count - 1; i >= 0; i--)
        {
            Unlock u = unlocks[i];
            u.age += dt;
            u.petal.unlockAge = u.age;

            // ---- Flight: counter -> petal, with a pale tail.
            float f = Mathf.Clamp01(u.age / UnlockFly);
            float m = EaseInOut(f);
            if (f < 1f)
            {
                float flick = GhostFlicker(Time.unscaledTime * 3f, i + 0.5f);
                PlaceAt(u.head, UnlockPath(u, m), 70f);
                float hs = 0.8f + 0.25f * Mathf.Sin(f * Mathf.PI);
                u.head.style.scale = new Scale(new Vector3(hs, hs, 1f));
                u.head.style.opacity = flick;
                for (int j = 0; j < UnlockTrailDots; j++)
                {
                    float back = m - (j + 1) * 0.035f;
                    if (back <= 0f) { u.trail[j].style.opacity = 0f; continue; }
                    float w = 1f - (j + 1) / (float)(UnlockTrailDots + 1);
                    PlaceAt(u.trail[j], UnlockPath(u, back), 40f);
                    u.trail[j].style.scale = new Scale(new Vector3(0.3f + 0.7f * w, 0.3f + 0.7f * w, 1f));
                    u.trail[j].style.opacity = 0.6f * w * w;
                }
            }
            else if (!u.arrived)
            {
                u.arrived = true;
                u.head.style.opacity = 0f;
                foreach (var d in u.trail) d.style.opacity = 0f;
                Sfx.Play("orb", 0.8f);
            }

            // ---- The lock breaks.
            if (u.age >= UnlockShatter && !u.shattered)
            {
                u.shattered = true;
                Petal p = u.petal;
                if (p.lockContent != null) { p.lockContent.RemoveFromHierarchy(); p.lockContent = null; }
                Vector2 local = u.to - AtomCenter + new Vector2(AtomHalf, AtomHalf);   // atomRoot coordinates
                u.burst = Icon(atomRoot, ProcSprites.Glow, 320f, Color.white);
                u.burst.style.position = Position.Absolute;
                PlaceAt(u.burst, local, 320f);
                for (int s = 0; s < UnlockShards; s++)
                {
                    var shard = Icon(atomRoot, s % 2 == 0 ? ProcSprites.Neutrino : ProcSprites.Glow, s % 2 == 0 ? 30f : 18f, Pickup.NeutrinoColor);
                    shard.style.position = Position.Absolute;
                    PlaceAt(shard, local, 24f);
                    u.shards.Add(shard);
                    float a = (s + Random.Range(-0.3f, 0.3f)) / UnlockShards * Mathf.PI * 2f;
                    u.shardVel.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(160f, 300f));
                }
                Sfx.Play("reveal", 0.9f);
                Sfx.Play("zap", 0.35f);
                gm.AddShake(0.12f);
                hoverShown = -99;
            }
            if (u.shattered)
            {
                float st = u.age - UnlockShatter;
                float b = Mathf.Clamp01(1f - st / 0.45f);
                u.burst.style.opacity = b * b;
                float bs = 0.6f + 0.6f * (1f - b);
                u.burst.style.scale = new Scale(new Vector3(bs, bs, 1f));
                Vector2 local = u.to - AtomCenter + new Vector2(AtomHalf, AtomHalf);
                float k = EaseOut(Mathf.Clamp01(st / 0.55f));
                for (int s = 0; s < u.shards.Count; s++)
                {
                    PlaceAt(u.shards[s], local + new Vector2(u.shardVel[s].x, -u.shardVel[s].y) * 0.55f * k, 24f);
                    u.shards[s].style.opacity = Mathf.Clamp01(1f - st / 0.55f);
                }
            }

            if (u.age >= Mathf.Max(UnlockDone, UnlockShatter + 0.55f))
            {
                u.petal.unlockAge = -1f;
                RemoveUnlockElements(u);
                unlocks.RemoveAt(i);
                hoverShown = -99;
            }
        }
    }

    static void RemoveUnlockElements(Unlock u)
    {
        u.head?.RemoveFromHierarchy();
        foreach (var d in u.trail) d?.RemoveFromHierarchy();
        u.burst?.RemoveFromHierarchy();
        foreach (var s in u.shards) s.RemoveFromHierarchy();
    }

    /// <summary>A pick was made (or the screen closed): any unlock still playing just ends.</summary>
    void ClearUnlocks()
    {
        foreach (var u in unlocks)
        {
            RemoveUnlockElements(u);
            u.petal.unlockAge = -1f;
            if (u.petal.lockContent != null) { u.petal.lockContent.RemoveFromHierarchy(); u.petal.lockContent = null; }
        }
        unlocks.Clear();
    }

    /// <summary>
    /// The look of a petal mid-unlock (fill, edge, contents). Returns false when the petal isn't
    /// being unlocked, so the normal look applies.
    /// </summary>
    bool UpdateUnlockingPetal(Petal p, float ut)
    {
        if (p.unlockAge < 0f) return false;
        float a = p.unlockAge;
        Color locked = new Color(0.55f, 0.58f, 0.7f);
        if (a < UnlockShatter)
        {
            // Charging: the lock trembles harder and whitens; the petal fills with pale light.
            float c = Mathf.Clamp01((a - UnlockFly * 0.7f) / (UnlockShatter - UnlockFly * 0.7f));
            float amp = 9f * c * c;
            if (p.lockContent != null)
            {
                p.lockContent.style.translate = new Translate(Mathf.Sin(ut * 70f) * amp, Length.Percent(-50f));
                float ls = 1f + 0.15f * c * c;
                p.lockContent.style.scale = new Scale(new Vector3(ls, ls, 1f));
                p.lockContent.style.opacity = 1f;
            }
            p.fill.style.unityBackgroundImageTintColor = WithAlpha(Color.Lerp(locked, Pickup.NeutrinoColor, c), 0.10f + 0.35f * c);
            p.edge.style.unityBackgroundImageTintColor = WithAlpha(Color.Lerp(locked, Color.white, c), 0.22f + 0.78f * c);
            p.fill.style.opacity = p.edge.style.opacity = 1f;
            p.content.style.opacity = 0f;
            return true;
        }
        // Open: the new upgrade pops out of the flash, the petal cools to its color.
        float o = Mathf.Clamp01((a - UnlockShatter) / 0.4f);
        float heat = (1f - o) * (1f - o);
        p.fill.style.unityBackgroundImageTintColor = WithAlpha(Color.Lerp(p.color, Color.white, heat), 0.2f + 0.3f * heat);
        p.edge.style.unityBackgroundImageTintColor = WithAlpha(Color.Lerp(p.color, Color.white, heat), 0.65f + 0.35f * heat);
        p.fill.style.opacity = p.edge.style.opacity = 1f;
        float pop = 1f + 0.4f * heat;
        p.content.style.opacity = 1f;
        p.content.style.scale = new Scale(new Vector3(pop, pop, 1f));
        p.content.style.translate = new Translate(0f, Length.Percent(-50f));
        return true;
    }
}
