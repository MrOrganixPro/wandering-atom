using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The upgrade screen. A round plays out like this:
///  1. the player's core leaves the map and flies to the center of the screen (the electrons
///     on the map fade out);
///  2. for every quantum earned: it flies in from the row of picks and grows into a big atom
///     around the core, its comets spin faster and faster, and it bursts open: three of the six
///     petals between its orbits (random) hold an upgrade, the others are locked;
///  3. hovering a petal fades in a panel beside it (name, level, description); clicking it
///     picks it: it curls into a glowing ball that curves into the core along a random path;
///  4. after the last pick, the core flies back to the map and the electrons return.
/// Each further quantum of the same round plays faster. A Character box takes a petal and stays
/// sealed a little longer; a Rare one shimmers.
/// </summary>
public partial class GameUI
{
    const float AtomHalf = 395f;          // half size of the atom's square (px, 720 high reference)
    const float AtomCenterDrop = 8f;      // the atom sits this far below the screen's center
    const float PetalCentroid = 0.70f;    // where a petal's contents sit (fraction of AtomHalf)
    const float CoreViewRadius = 72f;     // the player's core at the center of the atom (px)
    const float CometSize = 40f;
    const int AbsorbTrailDots = 12;
    static readonly int[] PetalOrder = { 3, 2, 1, 0, 5, 4 };   // offers go clockwise from the left

    class Petal
    {
        public int index;                 // 0..5, pointing at index * 60 degrees
        public int offer = -1;            // offer index shown here, -1 = locked
        public VisualElement fill, edge, content, sealedPart, openPart, flash;
        public Label question, rareTag;
        public Color color;
        public bool mystery, rare;
        public float wobble;              // locked petal was clicked: shake
        public float unlockAge = -1f;     // a neutrino is opening it (seconds), -1 = no
        public VisualElement lockContent; // the lock, kept on show until it shatters
    }

    readonly Petal[] petals = new Petal[6];
    VisualElement atomRoot, atomOrbits, atomFlash, cometLayer;
    readonly VisualElement[] cometHeads = new VisualElement[3];
    readonly VisualElement[][] cometTails = new VisualElement[3][];
    readonly float[] cometTheta = new float[3];
    int builtOffer = -1, hoverPetal = -1;
    bool spinSoundDone, openDone, mysteryOpenDone;
    float atomFlashAmt;

    // The player's core, shown in the UI while it is away from the map.
    VisualElement coreView, coreGlow, coreNucleons;
    int coreDotCount = -1;
    float corePulse;

    // The picked upgrade: a glowing ball curving into the core.
    VisualElement absorbBall, absorbIcon;
    readonly VisualElement[] absorbTrail = new VisualElement[AbsorbTrailDots];
    Vector2 absorbP0, absorbP1, absorbP2, absorbP3;
    float absorbAge = 99f, absorbDur = 1f;
    bool absorbLanded = true;
    int shownPickVersion = -1;

    // Hover panel beside a petal.
    VisualElement hoverPanel;
    Label hoverTitle, hoverLevel, hoverDesc;
    int hoverShown = -99;
    float hoverFade;

    // ------------------------------------------------------------------ build

    void BuildUpgrade()
    {
        upgradeScreen = Layer(root);
        upgradeScreen.style.backgroundColor = new Color(0f, 0f, 0.02f, 0.62f);

        // Picks still waiting: one quantum each (refund picks use the character's color).
        var top = Column(upgradeScreen, 2.5f);
        upgradePicks = MakePips(top, ProcSprites.Quanta, 30, 8, 700);

        atomRoot = new VisualElement { pickingMode = PickingMode.Position };
        CenterOnAtom(atomRoot, AtomHalf);
        upgradeScreen.Add(atomRoot);

        for (int k = 0; k < 6; k++)
        {
            var p = new Petal { index = k };
            p.fill = Layer(atomRoot);
            p.fill.style.backgroundImage = new StyleBackground(ProcSprites.AtomPetal);
            p.fill.style.rotate = new Rotate(-k * 60f);
            p.edge = Layer(atomRoot);
            p.edge.style.backgroundImage = new StyleBackground(ProcSprites.AtomPetalEdge);
            p.edge.style.rotate = new Rotate(-k * 60f);
            petals[k] = p;
        }

        atomOrbits = Layer(atomRoot);
        atomOrbits.style.backgroundImage = new StyleBackground(ProcSprites.AtomOrbits);
        atomOrbits.style.unityBackgroundImageTintColor = Pickup.CoinColor;

        cometLayer = Layer(atomRoot);
        for (int i = 0; i < 3; i++)
        {
            cometTails[i] = new VisualElement[QuantaOrbit.TrailDots];
            for (int j = QuantaOrbit.TrailDots - 1; j >= 0; j--)   // farthest first, so nearer dots draw on top
                cometTails[i][j] = Glow(cometLayer, CometSize, Pickup.CoinColor);
            cometHeads[i] = Glow(cometLayer, CometSize, new Color(1f, 0.92f, 0.7f));
            var hot = Icon(cometHeads[i], ProcSprites.Glow, CometSize * 0.4f, Color.white);
            hot.style.position = Position.Absolute;
            hot.style.left = hot.style.top = CometSize * 0.3f;
        }

        atomFlash = Icon(atomRoot, ProcSprites.Glow, AtomHalf * 2.2f, Color.white);
        atomFlash.style.position = Position.Absolute;
        atomFlash.style.left = atomFlash.style.top = AtomHalf - AtomHalf * 1.1f;
        atomFlash.style.opacity = 0f;

        atomRoot.RegisterCallback<PointerMoveEvent>(ev => hoverPetal = PetalAt(ev.localPosition));
        atomRoot.RegisterCallback<PointerLeaveEvent>(_ => hoverPetal = -1);
        atomRoot.RegisterCallback<PointerDownEvent>(ev =>
        {
            if (ev.button != 0 || !PetalsLive) return;
            int k = PetalAt(ev.localPosition);
            if (k < 0) return;
            Petal p = petals[k];
            if (p.unlockAge >= 0f) return;                 // a neutrino is still opening it
            if (p.offer >= 0) gm.ChooseUpgrade(p.offer);   // GameManager ignores it while sealed
            else if (!TryUnlockPetal(p))
            {
                p.wobble = 1f;
                Sfx.Play("full", 0.5f);
            }
        });

        // The player's core (drawn over the atom's center).
        coreView = new VisualElement { pickingMode = PickingMode.Ignore };
        CenterOnAtom(coreView, CoreViewRadius);
        upgradeScreen.Add(coreView);
        coreGlow = Icon(coreView, ProcSprites.Glow, CoreViewRadius * 4f, new Color(1f, 0.75f, 0.5f, 0.35f));
        coreGlow.style.position = Position.Absolute;
        coreGlow.style.left = coreGlow.style.top = -CoreViewRadius;
        coreNucleons = Layer(coreView);

        // The absorbed upgrade: tail, ball, and the icon curling into it.
        for (int j = AbsorbTrailDots - 1; j >= 0; j--) absorbTrail[j] = Glow(upgradeScreen, 46f, Color.white);
        absorbBall = Glow(upgradeScreen, 70f, Color.white);
        var ballHot = Icon(absorbBall, ProcSprites.Glow, 26f, Color.white);
        ballHot.style.position = Position.Absolute;
        ballHot.style.left = ballHot.style.top = 22f;
        absorbIcon = Icon(upgradeScreen, ProcSprites.Arrow, 64f, Color.white);
        absorbIcon.style.position = Position.Absolute;
        HideAbsorb();

        BuildHoverPanel();
        BuildNeutrinoCounter();
    }

    /// <summary>Square element of half size 'half', centered on the atom's resting center.</summary>
    static void CenterOnAtom(VisualElement e, float half)
    {
        e.style.position = Position.Absolute;
        e.style.left = Length.Percent(50);
        e.style.top = Length.Percent(50);
        e.style.marginLeft = -half;
        e.style.marginTop = -half + AtomCenterDrop;
        e.style.width = e.style.height = half * 2f;
    }

    static VisualElement Glow(VisualElement parent, float size, Color c)
    {
        var e = Icon(parent, ProcSprites.Glow, size, c);
        e.style.position = Position.Absolute;
        return e;
    }

    /// <summary>The atom's center in upgradeScreen coordinates.</summary>
    Vector2 AtomCenter
    {
        get
        {
            Rect r = upgradeScreen.layout;
            if (float.IsNaN(r.width) || r.width <= 0f) return new Vector2(640f, 360f + AtomCenterDrop);
            return new Vector2(r.width * 0.5f, r.height * 0.5f + AtomCenterDrop);
        }
    }

    int PetalAt(Vector2 local)
    {
        float x = (local.x - AtomHalf) / AtomHalf, y = -(local.y - AtomHalf) / AtomHalf;
        return ProcSprites.AtomPetalAt(x, y);
    }

    static Vector2 PetalDirection(int k)
    {
        float a = k * 60f * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }

    /// <summary>A petal's center in upgradeScreen coordinates (atom at rest).</summary>
    Vector2 PetalCenter(int k)
    {
        Vector2 d = PetalDirection(k) * PetalCentroid * AtomHalf;
        return AtomCenter + new Vector2(d.x, -d.y);
    }

    /// <summary>Petals open, nothing in flight: they can be hovered and picked.</summary>
    bool PetalsLive => !gm.UpgradeFinishing && gm.StateTime >= gm.OfferOpenTime;

    // ---------------------------------------------------------------- petals

    /// <summary>A new set of upgrades: scatter them over three random petals, lock the rest.</summary>
    void RebuildAtom()
    {
        builtOffer = gm.OfferVersion;
        ClearUnlocks();
        spinSoundDone = openDone = mysteryOpenDone = false;
        hoverPetal = -1;
        for (int i = 0; i < 3; i++) cometTheta[i] = Random.Range(0f, Mathf.PI * 2f);

        var open = new bool[6];
        var order = new List<int> { 0, 1, 2, 3, 4, 5 };
        int count = Mathf.Min(gm.Offer.Count, 6);
        for (int n = 0; n < count; n++)
        {
            int j = Random.Range(0, order.Count);
            open[order[j]] = true;
            order.RemoveAt(j);
        }
        int next = 0;
        foreach (int k in PetalOrder) petals[k].offer = open[k] ? next++ : -1;

        foreach (var p in petals)
        {
            if (p.content != null) p.content.RemoveFromHierarchy();
            SetupPetal(p);
        }
        atomFlash.BringToFront();
    }

    /// <summary>Colors and contents for what the petal now holds (its offer, or a lock).</summary>
    void SetupPetal(Petal p)
    {
        p.mystery = p.offer >= 0 && p.offer == gm.MysteryIndex;
        p.rare = p.offer >= 0 && !p.mystery && GameManager.IsRare(gm.Offer[p.offer]);
        p.color = p.offer >= 0 ? GameManager.UpgradeColor(gm.Offer[p.offer]) : new Color(0.55f, 0.58f, 0.7f);
        p.wobble = 0f;
        BuildPetalContent(p);
    }

    void BuildPetalContent(Petal p)
    {
        const float W = 180f;
        Vector2 at = PetalDirection(p.index) * PetalCentroid * AtomHalf;
        var c = Box(atomRoot);
        c.style.position = Position.Absolute;
        c.style.width = W;
        c.style.left = AtomHalf + at.x - W * 0.5f;
        c.style.top = AtomHalf - at.y;
        c.style.translate = new Translate(0f, Length.Percent(-50f));
        c.style.alignItems = Align.Center;
        p.content = c;

        if (p.offer < 0)
        {
            Icon(c, ProcSprites.Lock, 44, WithAlpha(Color.white, 0.35f));
            var l = Text(c, Loc.T("LOCKED", "KİLİTLİ"), 15, true, WithAlpha(Color.white, 0.35f));
            l.style.marginTop = 6;
            l.style.letterSpacing = 3;
            return;
        }

        GameManager.UpgradeType u = gm.Offer[p.offer];
        if (p.mystery)
        {
            p.openPart = Box(c);
            p.openPart.style.alignItems = Align.Center;
            var tag = Text(p.openPart, Loc.T("CHARACTER", "KARAKTER"), 13, true, CardGold);
            tag.style.letterSpacing = 3;
            tag.style.marginBottom = 4;
            PetalFace(p.openPart, p, u);

            p.sealedPart = Box(c);
            p.sealedPart.style.position = Position.Absolute;
            p.sealedPart.style.left = p.sealedPart.style.right = 0;
            p.sealedPart.style.top = p.sealedPart.style.bottom = 0;
            p.sealedPart.style.justifyContent = Justify.Center;
            p.question = Text(p.sealedPart, "?", 90, true, CardGold);

            p.flash = Icon(c, ProcSprites.Glow, 260, Color.white);
            p.flash.style.position = Position.Absolute;
            p.flash.style.left = W * 0.5f - 130f;
            p.flash.style.top = Length.Percent(50);
            p.flash.style.marginTop = -130f;
            p.flash.style.opacity = 0f;
            return;
        }
        if (p.rare)
        {
            p.rareTag = Text(c, Loc.T("RARE", "NADİR"), 13, true, Color.white);
            p.rareTag.style.letterSpacing = 4;
            p.rareTag.style.marginBottom = 4;
        }
        PetalFace(c, p, u);
    }

    /// <summary>A petal shows just a big icon and the upgrade's name (details on hover).</summary>
    void PetalFace(VisualElement parent, Petal p, GameManager.UpgradeType u)
    {
        Icon(parent, IconFor(u), 62, p.color);
        var title = Text(parent, GameManager.UpgradeTitle(u), 20, true, p.color, TextAnchor.MiddleCenter, true);
        title.style.marginTop = 6;
        title.style.maxWidth = 180;
    }

    static Sprite IconFor(GameManager.UpgradeType u)
    {
        switch (u)
        {
            case GameManager.UpgradeType.Heal:
            case GameManager.UpgradeType.MaxHealth:
            case GameManager.UpgradeType.Regeneration:
            case GameManager.UpgradeType.HealthRecycler: return ProcSprites.Stability;
            case GameManager.UpgradeType.CoinRecycler:
            case GameManager.UpgradeType.Challenger: return ProcSprites.Quanta;
            case GameManager.UpgradeType.Orbital: return ProcSprites.Electron;
            case GameManager.UpgradeType.Insight: return ProcSprites.Asteroid(0);
            case GameManager.UpgradeType.BallArmor:
            case GameManager.UpgradeType.BallSize:
            case GameManager.UpgradeType.Sluggish: return ProcSprites.Ball;
            case GameManager.UpgradeType.PickupRange: return ProcSprites.Photon;
            case GameManager.UpgradeType.Torque: return ProcSprites.TurnArrow;
            case GameManager.UpgradeType.Repel:
            case GameManager.UpgradeType.TurretRange: return ProcSprites.Ring;
            case GameManager.UpgradeType.FireRate:
            case GameManager.UpgradeType.Spaceship: return ProcSprites.Trefoil;
            case GameManager.UpgradeType.Conductive:
            case GameManager.UpgradeType.ChargeRate: return ProcSprites.Bolt;
            case GameManager.UpgradeType.HopRange: return ProcSprites.Ring;
            case GameManager.UpgradeType.HopCount: return ProcSprites.Link;
            case GameManager.UpgradeType.Halogen:
            case GameManager.UpgradeType.HalogenPull: return ProcSprites.Electron;
            case GameManager.UpgradeType.HalogenGrasp: return ProcSprites.Photon;
            case GameManager.UpgradeType.HalogenSnatch: return ProcSprites.Shard;
            case GameManager.UpgradeType.Alkali: return ProcSprites.Star;
            case GameManager.UpgradeType.Excited: return ProcSprites.Photon;
            case GameManager.UpgradeType.AngularMomentum: return ProcSprites.TurnArrow;
            case GameManager.UpgradeType.GroundState: return ProcSprites.Ring;
            case GameManager.UpgradeType.Photoelectric: return ProcSprites.Photon;
            case GameManager.UpgradeType.Resonance: return ProcSprites.Stability;
            case GameManager.UpgradeType.AlkaliBlast: return ProcSprites.Ring;
            case GameManager.UpgradeType.AlkaliRange:
            case GameManager.UpgradeType.AlkaliPath: return ProcSprites.Arrow;
            case GameManager.UpgradeType.AlkaliReload: return ProcSprites.TurnArrow;
            default: return ProcSprites.Arrow;   // Speed
        }
    }

    // ------------------------------------------------------------- hover panel

    void BuildHoverPanel()
    {
        hoverPanel = Box(upgradeScreen);
        hoverPanel.style.position = Position.Absolute;
        hoverPanel.style.width = 250;
        hoverPanel.style.paddingLeft = hoverPanel.style.paddingRight = 16;
        hoverPanel.style.paddingTop = hoverPanel.style.paddingBottom = 12;
        hoverPanel.style.backgroundColor = WithAlpha(Panel, 0.95f);
        hoverPanel.style.translate = new Translate(0f, Length.Percent(-50f));
        Radius(hoverPanel, 10);
        hoverTitle = Text(hoverPanel, "", 21, true, Color.white, TextAnchor.MiddleLeft, true);
        hoverLevel = Text(hoverPanel, "", 15, true, Dim, TextAnchor.MiddleLeft, true);
        hoverLevel.style.marginTop = 4;
        hoverLevel.style.letterSpacing = 2;
        hoverDesc = Text(hoverPanel, "", 15, false, WithAlpha(Color.white, 0.9f), TextAnchor.UpperLeft, true);
        hoverDesc.style.marginTop = 8;
        hoverPanel.style.opacity = 0f;
    }

    /// <summary>"2 → 3" for upgrades bought repeatedly; a tag for one-off ones.</summary>
    string LevelLine(GameManager.UpgradeType u)
    {
        if (GameManager.IsCharacter(u)) return Loc.T("CHARACTER", "KARAKTER");
        if (GameManager.IsRare(u)) return Loc.T("RARE", "NADİR");
        if (u == GameManager.UpgradeType.Heal) return "";
        int owned = gm.OwnedCount(u);
        return owned + "  →  " + (owned + 1);
    }

    void UpdateHoverPanel(float dt)
    {
        int hp = PetalsLive ? hoverPetal : -1;
        if (hp != hoverShown)
        {
            hoverShown = hp;
            if (hp >= 0) { FillHoverPanel(petals[hp]); hoverFade = 0f; }
        }
        // Content of a sealed box changes when it opens.
        if (hp >= 0 && petals[hp].mystery && gm.StateTime >= gm.MysteryOpenTime && hoverTitle.text == "???")
            FillHoverPanel(petals[hp]);

        Show(hoverLevel, !string.IsNullOrEmpty(hoverLevel.text));   // no gaps for empty lines
        Show(hoverDesc, !string.IsNullOrEmpty(hoverDesc.text));
        hoverFade = Mathf.MoveTowards(hoverFade, hp >= 0 ? 1f : 0f, dt / 0.22f);
        hoverPanel.style.opacity = hoverFade * hoverFade * (3f - 2f * hoverFade);
        Show(hoverPanel, hoverFade > 0f);
    }

    void FillHoverPanel(Petal p)
    {
        // Beside the petal, on the side away from the core.
        Vector2 dir = PetalDirection(p.index);
        bool right = dir.x > 0f;
        float reach = Mathf.Abs(dir.y) < 0.01f ? ProcSprites.AtomA : 0.6f;   // how far the petal sticks out sideways
        Vector2 c = AtomCenter;
        float x = right ? c.x + reach * AtomHalf + 18f : c.x - reach * AtomHalf - 18f - 250f;
        float maxX = upgradeScreen.layout.width;
        if (!float.IsNaN(maxX) && maxX > 0f) x = Mathf.Clamp(x, 8f, maxX - 258f);
        hoverPanel.style.left = x;
        hoverPanel.style.top = PetalCenter(p.index).y;

        Border(hoverPanel, 2f, WithAlpha(p.color, 0.8f));
        if (p.offer < 0)
        {
            bool can = gm.Neutrinos > 0;
            Border(hoverPanel, 2f, WithAlpha(can ? Pickup.NeutrinoColor : p.color, 0.8f));
            hoverTitle.text = Loc.T("LOCKED", "KİLİTLİ");
            hoverTitle.style.color = can ? Pickup.NeutrinoColor : Dim;
            hoverLevel.text = "";
            hoverDesc.text = can
                ? Loc.T("Spend 1 neutrino", "1 nötrino harca")
                : Loc.T("Earn neutrinos: no-damage levels", "Nötrino kazan: hasarsız seviyeler");
            return;
        }
        if (p.unlockAge >= 0f && p.unlockAge < UnlockShatter)
        {
            hoverTitle.text = "...";
            hoverTitle.style.color = Pickup.NeutrinoColor;
            hoverLevel.text = "";
            hoverDesc.text = "";
            return;
        }
        if (p.mystery && gm.StateTime < gm.MysteryOpenTime)
        {
            hoverTitle.text = "???";
            hoverTitle.style.color = CardGold;
            hoverLevel.text = Loc.T("CHARACTER", "KARAKTER");
            hoverDesc.text = "";
            return;
        }
        GameManager.UpgradeType u = gm.Offer[p.offer];
        hoverTitle.text = GameManager.UpgradeTitle(u);
        hoverTitle.style.color = p.color;
        hoverLevel.text = LevelLine(u);
        hoverDesc.text = gm.UpgradeDescription(u);
    }

    // ---------------------------------------------------------------- update

    static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
    static float EaseInOut(float t) => t * t * (3f - 2f * t);

    void UpdateAtomPicker()
    {
        float dt = Time.unscaledDeltaTime, ut = Time.unscaledTime;

        // A pick was just made: launch it into the core (before the petals are rebuilt).
        if (gm.LastPickVersion != shownPickVersion)
        {
            ClearUnlocks();
            if (shownPickVersion >= 0) StartAbsorb();
            shownPickVersion = gm.LastPickVersion;
        }
        if (builtOffer != gm.OfferVersion) RebuildAtom();

        float T = gm.StateTime;
        float start = gm.PickStartTime, fly = Mathf.Max(0.05f, gm.PickFlyTime);
        float open = gm.OfferOpenTime, mOpen = gm.MysteryOpenTime;
        bool finishing = gm.UpgradeFinishing;
        bool isOpen = !finishing && T >= open;

        UpdateCoreView(T, dt, ut);
        UpdateAbsorb(dt);
        UpdateUnlocks(dt);
        UpdateNeutrinoCounter(dt);

        // ---- Before this quantum flies in: the core arrives, or the last atom fades away.
        if (finishing || T < start)
        {
            bool fading = gm.PickNumber > 0 || finishing;   // the previous atom, after a pick
            float fade = fading ? 1f - Mathf.Clamp01(T / Mathf.Max(0.05f, (finishing ? gm.AbsorbTime : start) * 0.6f)) : 0f;
            atomRoot.style.opacity = fade;
            atomRoot.style.translate = new Translate(0f, 0f);
            float s = 1f + 0.08f * (1f - fade);
            atomRoot.style.scale = new Scale(new Vector3(s, s, 1f));
            UpdateComets(dt, 3f);
            UpdatePetals(-1f, 1f, 1f, ut, dt);   // keep them hidden
            UpdateHoverPanel(dt);
            return;
        }
        atomRoot.style.opacity = 1f;

        // ---- 1. Grow out of the core, from the center.
        float tf = T - start;
        float k = EaseOut(Mathf.Clamp01(tf / fly));
        float startScale = CoreViewRadius * 0.6f / (ProcSprites.AtomA * AtomHalf);   // starts inside the core
        float scale = Mathf.Lerp(startScale, 1f, k);

        // ---- 2. Spin up; 3. burst open, then settle.
        float spin = Mathf.Clamp01((T - start - fly) / Mathf.Max(0.05f, open - start - fly));
        float omega;
        if (tf < fly) omega = 3f;
        else if (!isOpen) omega = Mathf.Lerp(3f, 48f, spin * spin);
        else omega = Mathf.Lerp(48f, 2.4f, EaseOut(Mathf.Clamp01((T - open) / 0.9f)));

        float shake = !isOpen && tf >= fly ? 4f * spin * spin : 0f;
        atomRoot.style.translate = new Translate(Random.Range(-shake, shake), Random.Range(-shake, shake));
        float pulse = isOpen ? 0f : 0.04f * spin * spin * Mathf.Sin(ut * 40f);
        atomRoot.style.scale = new Scale(new Vector3(scale + pulse, scale + pulse, 1f));

        if (tf >= fly && !spinSoundDone)
        {
            spinSoundDone = true;
            Sfx.PlaySuspense(Mathf.Max(0.3f, open - start - fly), 0.55f);
        }
        if (isOpen && !openDone)
        {
            openDone = true;
            atomFlashAmt = 1f;
            corePulse = 1f;
            Sfx.Play("reveal", 0.75f);
            gm.AddShake(0.25f);
            if (gm.MysteryIndex >= 0) Sfx.PlaySuspense(Mathf.Max(0.3f, mOpen - open), 0.9f);   // until the box opens
        }

        float heat = isOpen ? Mathf.Clamp01(1f - (T - open) / 0.8f) * 0.6f : spin * spin;
        atomOrbits.style.unityBackgroundImageTintColor = Color.Lerp(Pickup.CoinColor, Color.white, heat * 0.7f);
        atomOrbits.style.opacity = 0.75f + 0.25f * heat;
        coreGlow.style.opacity = 0.6f + 0.4f * heat;

        atomFlashAmt = Mathf.MoveTowards(atomFlashAmt, 0f, dt / 0.7f);
        atomFlash.style.opacity = atomFlashAmt * atomFlashAmt;

        UpdateComets(dt, omega);
        UpdatePetals(T, open, mOpen, ut, dt);
        UpdateHoverPanel(dt);
    }

    void UpdateComets(float dt, float omega)
    {
        for (int i = 0; i < 3; i++)
        {
            float speed = omega * (1f + 0.12f * i);
            cometTheta[i] += speed * dt;
            float th = cometTheta[i];
            PlaceComet(cometHeads[i], QuantaOrbit.Point(i, th, ProcSprites.AtomA, ProcSprites.AtomB), 1f, 1f);
            float span = QuantaOrbit.TrailSpan(speed);
            VisualElement[] tail = cometTails[i];
            for (int j = 0; j < tail.Length; j++)
            {
                int n = j + 1;
                QuantaOrbit.TrailShape(n, tail.Length, out float size, out float alpha);
                Vector2 pt = QuantaOrbit.Point(i, th - span * n / tail.Length, ProcSprites.AtomA, ProcSprites.AtomB);
                PlaceComet(tail[j], pt, 0.3f + 0.7f * size, 0.85f * alpha);
                tail[j].style.unityBackgroundImageTintColor =
                    Color.Lerp(Color.Lerp(Pickup.CoinColor, Color.white, 0.55f), Pickup.CoinColor, n / (float)tail.Length);
            }
        }
    }

    static void PlaceComet(VisualElement e, Vector2 unitPos, float scale, float opacity)
    {
        e.style.left = AtomHalf + unitPos.x * AtomHalf - CometSize * 0.5f;
        e.style.top = AtomHalf - unitPos.y * AtomHalf - CometSize * 0.5f;
        e.style.scale = new Scale(new Vector3(scale, scale, 1f));
        e.style.opacity = opacity;
    }

    /// <param name="T">State time; negative keeps every petal hidden.</param>
    void UpdatePetals(float T, float open, float mOpen, float ut, float dt)
    {
        // The petals flash in all at once the moment the atom bursts (no fade), then cool down.
        float reveal = T >= 0f && T >= open ? 1f : 0f;
        float flashIn = reveal > 0f ? Mathf.Clamp01(1f - (T - open) / 0.35f) : 0f;
        flashIn *= flashIn;
        bool mysteryOpen = T >= mOpen;
        if (T >= 0f && mysteryOpen && !mysteryOpenDone && gm.MysteryIndex >= 0)
        {
            mysteryOpenDone = true;
            Codex.See(Codex.Upgrade(gm.Offer[gm.MysteryIndex]));
            Sfx.Play("reveal", 0.9f);
        }
        Color prism = Color.HSVToRGB(Mathf.Repeat(ut * 0.22f, 1f), 0.45f, 1f);

        foreach (var p in petals)
        {
            if (p.content == null) continue;
            bool hover = hoverPetal == p.index && reveal >= 1f && p.unlockAge < 0f;
            p.wobble = Mathf.MoveTowards(p.wobble, 0f, dt * 3f);
            if (reveal > 0f && UpdateUnlockingPetal(p, ut)) continue;
            Color c = p.color;
            if (p.mystery && !mysteryOpen) c = CardGold;
            if (p.rare) c = Color.Lerp(c, prism, 0.5f);

            float fillA = p.offer < 0 ? 0.10f : hover ? 0.42f : 0.2f;
            float edgeA = p.offer < 0 ? 0.22f : hover ? 1f : 0.65f;
            Color edgeC = p.rare ? prism : c;
            // A neutrino to spend: the locks glow faintly, inviting a click.
            if (p.offer < 0 && gm.Neutrinos > 0 && PetalsLive)
            {
                bool lh = hoverPetal == p.index;
                c = edgeC = Pickup.NeutrinoColor;
                fillA = lh ? 0.24f : 0.12f;
                edgeA = (lh ? 0.75f : 0.32f) + 0.12f * Mathf.Sin(ut * 3f + p.index);
            }
            p.fill.style.unityBackgroundImageTintColor = WithAlpha(Color.Lerp(c, Color.white, flashIn), Mathf.Lerp(fillA, 0.6f, flashIn));
            p.edge.style.unityBackgroundImageTintColor = WithAlpha(Color.Lerp(edgeC, Color.white, flashIn), Mathf.Lerp(edgeA, 1f, flashIn));
            p.fill.style.opacity = p.edge.style.opacity = reveal;

            float pop = 1f + 0.22f * flashIn;
            float s = pop * (hover ? 1.08f : 1f);
            p.content.style.opacity = reveal;
            p.content.style.scale = new Scale(new Vector3(s, s, 1f));
            float wob = 7f * p.wobble * Mathf.Sin(ut * 50f);
            p.content.style.translate = new Translate(wob, Length.Percent(-50f));
            if (p.rareTag != null) p.rareTag.style.color = prism;

            if (!p.mystery) continue;
            Show(p.sealedPart, !mysteryOpen);
            p.openPart.style.visibility = mysteryOpen ? Visibility.Visible : Visibility.Hidden;
            if (!mysteryOpen && T >= 0f)
            {
                // Sealed: trembles harder and faster until it bursts.
                float build = Mathf.Clamp01((T - open) / Mathf.Max(0.1f, mOpen - open));
                float amp = 6f * build * build;
                p.content.style.translate = new Translate(Mathf.Sin(ut * 61f) * amp, Length.Percent(-50f));
                p.question.style.color = WithAlpha(CardGold, 0.6f + 0.4f * Mathf.Sin(ut * (4f + 14f * build)));
                float qs = 1f + 0.2f * build * build;
                p.question.style.scale = new Scale(new Vector3(qs, qs, 1f));
            }
            p.flash.style.opacity = T >= 0f && mysteryOpen ? Mathf.Clamp01(1f - (T - mOpen) / 0.4f) : 0f;
        }
    }

    // ------------------------------------------------------------- the core

    /// <summary>The player's core on screen (upgradeScreen coordinates) and its size there, in px.</summary>
    bool PlayerCoreOnScreen(out Vector2 pos, out float radiusPx)
    {
        pos = AtomCenter;
        radiusPx = CoreViewRadius;
        PendulumController pc = gm.Pendulum;
        Camera cam = Camera.main;
        if (pc == null || cam == null || root.panel == null) return false;
        Vector3 sp = cam.WorldToScreenPoint(pc.PivotPosition);
        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(sp.x, Screen.height - sp.y));
        pos = upgradeScreen.WorldToLocal(panelPos);
        float h = root.layout.height;
        if (float.IsNaN(h) || h <= 0f) h = 720f;
        radiusPx = pc.NucleusRadius * h / (2f * cam.orthographicSize);
        return true;
    }

    /// <summary>(Re)builds the nucleon dots to match the player's core.</summary>
    void BuildCoreDots()
    {
        PendulumController pc = gm.Pendulum;
        float shown = pc != null ? Mathf.Max(1f, pc.NucleusShown) : 1f;
        int n = Mathf.CeilToInt(shown - 0.001f);
        if (n == coreDotCount) return;
        coreDotCount = n;
        coreNucleons.Clear();
        Color proton = pc != null ? pc.protonColor : new Color(1f, 0.62f, 0.45f);
        Color neutron = pc != null ? pc.neutronColor : new Color(0.62f, 0.6f, 0.58f);
        for (int i = n - 1; i >= 0; i--)   // outer first, so the inner ones sit on top
        {
            Vector2 at = PendulumController.NucleonLayout(i, shown, CoreViewRadius, out float d);
            var dot = Icon(coreNucleons, ProcSprites.Ball, d, PendulumController.IsProton(i) ? proton : neutron);
            dot.style.position = Position.Absolute;
            dot.style.left = CoreViewRadius + at.x - d * 0.5f;
            dot.style.top = CoreViewRadius - at.y - d * 0.5f;
        }
    }

    /// <summary>
    /// The core: flies in from the map at the start of a round, sits in the atom's center (and
    /// pulses when an upgrade lands in it), and flies back to the map after the last pick.
    /// </summary>
    void UpdateCoreView(float T, float dt, float ut)
    {
        BuildCoreDots();
        PlayerCoreOnScreen(out Vector2 home, out float homeRadius);
        float homeScale = Mathf.Max(0.05f, homeRadius / CoreViewRadius);

        float k = 1f;   // 0 = on the map, 1 = in the center
        if (gm.UpgradeFinishing)
            k = 1f - EaseInOut(Mathf.Clamp01((T - gm.AbsorbTime) / Mathf.Max(0.05f, gm.CoreReturnTime)));
        else if (gm.PickNumber == 0)
            k = EaseInOut(Mathf.Clamp01(T / Mathf.Max(0.05f, gm.CoreFlyTime)));

        Vector2 off = (home - AtomCenter) * (1f - k);
        // A gentle arc on the way, so it doesn't just slide.
        off += new Vector2(0f, -60f) * Mathf.Sin(k * Mathf.PI);
        corePulse = Mathf.MoveTowards(corePulse, 0f, dt / 0.5f);
        float s = Mathf.Lerp(homeScale, 1f, k) * (1f + 0.22f * corePulse * corePulse)
                  * (1f + 0.03f * Mathf.Sin(ut * 23f) + 0.02f * Mathf.Sin(ut * 37f));
        coreView.style.translate = new Translate(off.x, off.y);
        coreView.style.scale = new Scale(new Vector3(s, s, 1f));
        coreNucleons.style.rotate = new Rotate(-ut * 18f);
        coreGlow.style.unityBackgroundImageTintColor = Color.Lerp(new Color(1f, 0.75f, 0.5f, 0.35f), new Color(1f, 1f, 1f, 0.8f), corePulse);
    }

    /// <summary>Keeps the map in sync: the atom there hides while its core is in the UI.</summary>
    void UpdateUpgradeWorld(GameManager.State st)
    {
        PendulumController pc = gm.Pendulum;
        if (pc == null) return;
        bool away = st == GameManager.State.Upgrade;
        // Back on the map a moment before the screen closes, so it never blinks out.
        if (away && gm.UpgradeFinishing && gm.StateTime >= gm.AbsorbTime + gm.CoreReturnTime - 0.05f) away = false;
        pc.upgradeHidden = away;
        if (st != GameManager.State.Upgrade)
        {
            if (unlocks.Count > 0) ClearUnlocks();
            HideAbsorb();
            absorbAge = 99f;
            shownPickVersion = gm.LastPickVersion;
            coreDotCount = -1;   // rebuilt from the real core next round
        }
    }

    // ---------------------------------------------------------------- absorb

    /// <summary>The picked petal curls into a glowing ball, which curves into the core.</summary>
    void StartAbsorb()
    {
        Petal picked = null;
        foreach (var p in petals) if (p.offer == gm.LastPickIndex) picked = p;
        GameManager.UpgradeType u = gm.LastPicked;
        Color c = GameManager.UpgradeColor(u);

        absorbP0 = picked != null ? PetalCenter(picked.index) : AtomCenter + new Vector2(0f, -200f);
        absorbP3 = AtomCenter;
        // Random smooth path: out and to one side first, then sweeping into the core.
        Vector2 outward = (absorbP0 - absorbP3).normalized;
        Vector2 side = new Vector2(-outward.y, outward.x) * (Random.value < 0.5f ? -1f : 1f);
        absorbP1 = absorbP0 + outward * Random.Range(40f, 130f) + side * Random.Range(80f, 200f);
        absorbP2 = absorbP3 + Random.insideUnitCircle.normalized * Random.Range(120f, 220f);
        absorbDur = Mathf.Max(0.3f, gm.AbsorbTime);
        absorbAge = 0f;
        absorbLanded = false;

        absorbBall.style.unityBackgroundImageTintColor = Color.Lerp(c, Color.white, 0.25f);
        foreach (var d in absorbTrail) d.style.unityBackgroundImageTintColor = c;
        absorbIcon.style.backgroundImage = new StyleBackground(IconFor(u));
        absorbIcon.style.unityBackgroundImageTintColor = c;
        hoverPetal = -1;
    }

    Vector2 AbsorbPath(float t)
    {
        float u = 1f - t;
        return u * u * u * absorbP0 + 3f * u * u * t * absorbP1 + 3f * u * t * t * absorbP2 + t * t * t * absorbP3;
    }

    void HideAbsorb()
    {
        absorbBall.style.opacity = 0f;
        absorbIcon.style.opacity = 0f;
        foreach (var d in absorbTrail) d.style.opacity = 0f;
    }

    static void PlaceAt(VisualElement e, Vector2 p, float size)
    {
        e.style.left = p.x - size * 0.5f;
        e.style.top = p.y - size * 0.5f;
    }

    void UpdateAbsorb(float dt)
    {
        if (absorbAge >= absorbDur) { HideAbsorb(); return; }
        absorbAge += dt;
        float t = Mathf.Clamp01(absorbAge / absorbDur);
        const float Curl = 0.3f;   // first part: the upgrade curls up into the ball

        if (t < Curl)
        {
            float c = t / Curl;
            PlaceAt(absorbIcon, absorbP0, 64f);
            float s = Mathf.Lerp(1.1f, 0.15f, c * c);
            absorbIcon.style.scale = new Scale(new Vector3(s, s, 1f));
            absorbIcon.style.rotate = new Rotate(c * c * 540f);
            absorbIcon.style.opacity = 1f - c * c;
            PlaceAt(absorbBall, absorbP0, 70f);
            float b = EaseOut(c) * (1f + 0.3f * Mathf.Sin(c * Mathf.PI));
            absorbBall.style.scale = new Scale(new Vector3(b, b, 1f));
            absorbBall.style.opacity = 1f;
            foreach (var d in absorbTrail) d.style.opacity = 0f;
            return;
        }

        absorbIcon.style.opacity = 0f;
        float m = EaseInOut((t - Curl) / (1f - Curl));
        PlaceAt(absorbBall, AbsorbPath(m), 70f);
        float flick = 0.9f + 0.2f * Mathf.Sin(Time.unscaledTime * 50f);
        float bs = Mathf.Lerp(1f, 0.55f, m) * flick;
        absorbBall.style.scale = new Scale(new Vector3(bs, bs, 1f));
        absorbBall.style.opacity = 1f;
        for (int j = 0; j < absorbTrail.Length; j++)
        {
            float back = m - (j + 1) * 0.022f;
            if (back <= 0f) { absorbTrail[j].style.opacity = 0f; continue; }
            float f = 1f - (j + 1) / (float)(absorbTrail.Length + 1);
            PlaceAt(absorbTrail[j], AbsorbPath(back), 46f);
            absorbTrail[j].style.scale = new Scale(new Vector3(0.3f + 0.7f * f, 0.3f + 0.7f * f, 1f));
            absorbTrail[j].style.opacity = 0.8f * f * f;
        }

        if (t >= 1f && !absorbLanded)
        {
            absorbLanded = true;
            corePulse = 1f;
            Sfx.Play("coin", 0.8f);
            gm.AddShake(0.15f);
        }
    }
}
