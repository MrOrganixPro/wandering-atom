using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// The Lab: a research map of upgrades that carry over between runs, bought with Research
/// Data. The atom sits in the middle and three branches radiate out of it (Atom lower-left,
/// Field lower-right, Discoveries up); Atom and Field fork and merge into a keystone;
/// Discoveries opens with Probability Amplitude, which forks into Rare Finds and Mutations, and the
/// characters fan out of Mutations. Beams link the nodes: dashed while locked, dim once open,
/// glowing with flowing particles once researched. Researching sends a data pulse from the
/// core along the beams; when it lands the node bursts and the nodes it opens light up.
/// Also here: the character chooser on PLAY and the Research Data payout on game over.
/// </summary>
public partial class GameUI
{
    class LabNode
    {
        public LabPerk perk;
        public int branch, tier, index;
        public float radius, angle;     // polar position around the core (degrees, counter-clockwise from right)
        public Vector2 pos;             // offset from the core in panel pixels (y down)
        public float size;
        public bool keystone;
        public VisualElement holder, root, glow, frame, iconHolder, spark, costRow;
        public Label question, cost;
        public Pips pips;
        public float pop, flash;
        public int shownLevel;          // what the map shows: catches up when the data pulse lands
        public bool shownOpen;
        public float lightAt = -1f;     // when it lights up after its parents were researched
        public readonly List<LabLink> incoming = new List<LabLink>();
    }

    /// <summary>A beam from a node (or the core, when from is null) to a node.</summary>
    class LabLink
    {
        public LabNode from, to;
        public float startAngle, t0, t1, phase;
    }

    class LabPulse { public List<LabLink> path; public LabNode target; public float t; }
    class LabSpark { public Vector2 pos, vel; public float life, maxLife, size; public Color color; }
    struct LabMote { public Vector2 pos, vel; public float size, phase; }

    // Layout: rings around the core and the direction each branch grows in.
    const float LabCenterY = 0.52f;
    const float LabCoreRadius = 62f;
    static readonly float[] LabRings = { 0f, 230f, 440f, 660f };
    static readonly float[] LabBranchAngle = { 210f, 330f, 90f };   // Atom, Field, Discoveries
    const float LabCharUpgradeRadius = 890f;   // each character's own upgrades
    const float LabPanMargin = 90f;            // how far past the map's edge it can be dragged
    static readonly Color[] LabBranchColor =
    {
        new Color(0.38f, 0.72f, 1f),     // Atom: electron blue
        new Color(0.45f, 1f, 0.72f),     // Field: research-data green
        new Color(1f, 0.8f, 0.32f),      // Discoveries: gold
    };

    // (perk, ring, angle offset from its branch, keystone, radius override (0 = the ring's))
    static readonly (LabPerk perk, int tier, float offset, bool keystone, float r)[] LabLayout =
    {
        (LabPerk.Stability, 1, 0f, false, 0f),
        (LabPerk.StartOrbital, 2, 17f, false, 0f),
        (LabPerk.StartNeutrino, 2, -17f, false, 0f),
        (LabPerk.SecondWind, 3, 0f, true, 0f),
        (LabPerk.Research, 1, 0f, false, 0f),
        (LabPerk.StabilityPickup, 2, 17f, false, 0f),
        (LabPerk.NeutrinoPickup, 2, -17f, false, 0f),
        (LabPerk.ExtraElectrons, 3, 0f, true, 0f),
        // Discoveries: Probability Amplitude, then Rare Finds and Mutations, then the characters.
        (LabPerk.ProbabilityAmplitude, 1, 0f, false, 0f),
        (LabPerk.RareChance, 2, 25f, false, 0f),
        (LabPerk.CharacterChance, 2, 0f, false, 0f),
        // Seven characters fan out of Mutations, 16 degrees apart.
        (LabPerk.UnlockHydrogen, 3, 48f, true, 0f),
        (LabPerk.UnlockArgon, 3, 32f, true, 0f),
        (LabPerk.UnlockRadium, 3, 16f, true, 0f),
        (LabPerk.UnlockExcited, 3, 0f, true, 0f),
        (LabPerk.UnlockConductive, 3, -16f, true, 0f),
        (LabPerk.UnlockHalogen, 3, -32f, true, 0f),
        (LabPerk.UnlockAlkali, 3, -48f, true, 0f),
        // Each character's two upgrades fan out above it.
        (LabPerk.IonQuanta, 4, 52f, false, LabCharUpgradeRadius),
        (LabPerk.IonNeutrino, 4, 44f, false, LabCharUpgradeRadius),
        (LabPerk.NobleLinger, 4, 36f, false, LabCharUpgradeRadius),
        (LabPerk.NobleProclaim, 4, 28f, false, LabCharUpgradeRadius),
        (LabPerk.RadiantSides, 4, 20f, false, LabCharUpgradeRadius),
        (LabPerk.RadiantWaves, 4, 12f, false, LabCharUpgradeRadius),
        (LabPerk.NobleDash, 4, 4f, false, LabCharUpgradeRadius),
        (LabPerk.NobleBlast, 4, -4f, false, LabCharUpgradeRadius),
        (LabPerk.ConductSuper, 4, -12f, false, LabCharUpgradeRadius),
        (LabPerk.ConductPlating, 4, -20f, false, LabCharUpgradeRadius),
        (LabPerk.HalogenHunter, 4, -28f, false, LabCharUpgradeRadius),
        (LabPerk.HalogenCorrosion, 4, -36f, false, LabCharUpgradeRadius),
        (LabPerk.AlkaliViolent, 4, -44f, false, LabCharUpgradeRadius),
        (LabPerk.AlkaliBoomerang, 4, -52f, false, LabCharUpgradeRadius),
    };

    // The opening animation: the core first, then each ring grows out in turn.
    const float LabRevealEnd = 1.27f;   // the outermost ring (tier 4) has fully popped in by then
    static float LabTierStart(int tier) => 0.12f + 0.22f * (tier - 1);

    VisualElement labScreen, labCanvas, labFx, labTip, labCore;
    Label labBank, labTipTitle, labTipLevel, labTipBody, labTipStatus;
    readonly Label[] labBranchLabels = new Label[3];
    readonly List<LabNode> labNodes = new List<LabNode>();
    readonly LabNode[] labByPerk = new LabNode[System.Enum.GetValues(typeof(LabPerk)).Length];
    readonly List<LabLink> labLinks = new List<LabLink>();
    readonly List<LabPulse> labPulses = new List<LabPulse>();
    readonly List<LabSpark> labSparks = new List<LabSpark>();
    LabMote[] labMotes;
    bool labOpen;
    int labHover = -1;
    float labBankPop, labReveal = LabRevealEnd, labTipAlpha;
    Vector2 labC;   // the core, in canvas pixels (set every repaint)

    // Dragging the map: the whole map (zones, beams, nodes, fx) sits in labWorld, moved by labPan.
    VisualElement labWorld;
    Vector2 labPan, labPanStart, labDragStart;
    Rect labBounds;                 // the map's extent around the core (panel pixels)
    bool labDragging, labDragMoved;
    int labPressNode = -1;          // the node the press started on (researched on release, if not dragged)

    VisualElement chooserScreen, chooserRow;
    bool chooserOpen;
    int chooserOpenedFrame;

    // Game over payout
    VisualElement payoutBox, payoutTotalRow;
    Label payoutTotal, payoutNote;
    int payoutShownVersion = -1, payoutTicked;

    static readonly string[] BranchNamesEn = { "ATOM", "FIELD", "DISCOVERIES" };
    static readonly string[] BranchNamesTr = { "ATOM", "ALAN", "KEŞİFLER" };

    public static void OpenChooser()
    {
        if (instance != null) instance.ShowChooser();
    }

    bool AnyOverlay => indexOpen || labOpen || chooserOpen;

    /// <summary>The Lab screen is open (the music switches to the Lab's own piece).</summary>
    public static bool LabOpen => instance != null && instance.labOpen;

    // ---------------------------------------------------------------- texts

    static string PerkName(LabPerk p)
    {
        switch (p)
        {
            case LabPerk.Stability: return Loc.T("REINFORCED CORE", "GÜÇLÜ ÇEKİRDEK");
            case LabPerk.StartOrbital: return Loc.T("PRIMED ORBITAL", "HAZIR ORBİTAL");
            case LabPerk.StartNeutrino: return Loc.T("NEUTRINO STOCK", "NÖTRİNO STOKU");
            case LabPerk.SecondWind: return Loc.T("SECOND WIND", "İKİNCİ NEFES");
            case LabPerk.Research: return Loc.T("DATA ANALYSIS", "VERİ ANALİZİ");
            case LabPerk.ExtraElectrons: return Loc.T("ELECTRON SOURCE", "ELEKTRON KAYNAĞI");
            case LabPerk.StabilityPickup: return Loc.T("STABILIZERS", "DENGELEYİCİLER");
            case LabPerk.NeutrinoPickup: return Loc.T("NEUTRINO DETECTOR", "NÖTRİNO DEDEKTÖRÜ");
            case LabPerk.RareChance: return Loc.T("RARE FINDS", "NADİR BULGULAR");
            case LabPerk.CharacterChance: return Loc.T("MUTATIONS", "DÖNÜŞÜMLER");
            case LabPerk.ProbabilityAmplitude: return Loc.T("PROBABILITY AMPLITUDE", "OLASILIK GENLİĞİ");
            case LabPerk.IonQuanta: return Loc.T("SURPLUS CHARGE", "FAZLA YÜK");
            case LabPerk.IonNeutrino: return Loc.T("GHOST TRAIL", "HAYALET İZİ");
            case LabPerk.NobleDash: return Loc.T("ELECTRODASH", "ELEKTRO ATILIM");
            case LabPerk.NobleBlast: return Loc.T("ELECTROBLAST", "ELEKTRO PATLAMA");
            case LabPerk.NobleLinger: return Loc.T("LINGERING NOBILITY", "KALICI SOYLULUK");
            case LabPerk.NobleProclaim: return Loc.T("PROCLAMATION OF NOBILITY", "SOYLULUK İLANI");
            case LabPerk.RadiantSides: return Loc.T("DECAY ON ALL SIDES", "HER YÖNE BOZUNMA");
            case LabPerk.RadiantWaves: return Loc.T("DECAY WAVES", "BOZUNMA DALGALARI");
            case LabPerk.ConductSuper: return Loc.T("SUPERCONDUCTOR", "SÜPERİLETKEN");
            case LabPerk.ConductPlating: return Loc.T("ELECTROPLATING", "ELEKTROKAPLAMA");
            case LabPerk.HalogenHunter: return Loc.T("RADICAL HUNTER", "RADİKAL AVCISI");
            case LabPerk.HalogenCorrosion: return Loc.T("CORROSION", "AŞINDIRMA");
            case LabPerk.AlkaliViolent: return Loc.T("VIOLENT REACTION", "ŞİDDETLİ TEPKİME");
            case LabPerk.AlkaliBoomerang: return Loc.T("BOOMERANG", "BUMERANG");
            default: return GameManager.UpgradeTitle(GameManager.CharacterCard(Lab.CharacterOf(p)));
        }
    }

    /// <summary>The character a character upgrade belongs to (None for the rest of the Lab).</summary>
    static GameManager.Character UpgradeOwner(LabPerk p)
    {
        switch (p)
        {
            case LabPerk.IonQuanta:
            case LabPerk.IonNeutrino: return GameManager.Character.Challenger;
            case LabPerk.NobleDash:
            case LabPerk.NobleBlast: return GameManager.Character.Excited;
            case LabPerk.NobleLinger:
            case LabPerk.NobleProclaim: return GameManager.Character.Sluggish;
            case LabPerk.RadiantSides:
            case LabPerk.RadiantWaves: return GameManager.Character.Spaceship;
            case LabPerk.ConductSuper:
            case LabPerk.ConductPlating: return GameManager.Character.Conductive;
            case LabPerk.HalogenHunter:
            case LabPerk.HalogenCorrosion: return GameManager.Character.Halogen;
            case LabPerk.AlkaliViolent:
            case LabPerk.AlkaliBoomerang: return GameManager.Character.Alkali;
            default: return GameManager.Character.None;
        }
    }

    static string PerkBody(LabPerk p)
    {
        switch (p)
        {
            case LabPerk.Stability: return Loc.T("Start runs with +1 max stability.", "Oyuna +1 azami kararlılıkla başla.");
            case LabPerk.StartOrbital: return Loc.T("Start runs with a New Orbital.", "Oyuna bir Yeni Orbitalle başla.");
            case LabPerk.StartNeutrino: return Loc.T("Start runs with +1 neutrino.", "Oyuna +1 nötrinoyla başla.");
            case LabPerk.SecondWind: return Loc.T("Survive one fatal hit per run.", "Oyun başına bir ölümcül darbeden kurtul.");
            case LabPerk.Research: return Loc.T("+10% data from every run.", "Her oyundan %10 fazla veri.");
            case LabPerk.ExtraElectrons: return Loc.T("+1 electron on the map every level.", "Her bölümde haritada +1 elektron.");
            case LabPerk.StabilityPickup: return Loc.T("A stability point may be waiting when you start a level hurt.", "Bölüme hasarlı başlarsan seni bir kararlılık bekleyebilir.");
            case LabPerk.NeutrinoPickup: return Loc.T("Neutrinos turn up more often.", "Nötrinolar daha sık çıkar.");
            case LabPerk.RareChance: return Loc.T("+1% rare upgrade chance (base 5%).", "+%1 nadir geliştirme şansı (temel %5).");
            case LabPerk.CharacterChance: return Loc.T("+1% character box chance (base 5%).", "+%1 karakter kutusu şansı (temel %5).");
            case LabPerk.ProbabilityAmplitude: return Loc.T("Every pick without a rare card or character box adds +1% to that chance, until one shows up.",
                                                   "Nadir kart ya da karakter kutusu çıkmayan her seçim o şansa +%1 ekler; biri çıkınca sıfırlanır.");
            case LabPerk.IonQuanta: return Loc.T("Lonely Ion: +1 quantum every level.", "Yalnız İyon: her seviyede +1 kuantum.");
            case LabPerk.IonNeutrino: return Loc.T("Lonely Ion: a neutrino every level.", "Yalnız İyon: her seviyede bir nötrino.");
            case LabPerk.NobleDash: return Loc.T("Burn an electron: dash, untouchable. 4s.", "Bir elektron yak: dokunulmaz atıl. 4 sn.");
            case LabPerk.NobleBlast: return Loc.T("Burn an electron: a ray that vaporizes enemies. 10s.", "Bir elektron yak: düşmanları buharlaştıran ışın. 10 sn.");
            case LabPerk.NobleLinger: return Loc.T("Noble Atom: unbreakable bonds last 5s after the full shell is lost.", "Soy Atom: tam kabuk bozulunca kırılmaz bağlar 5 sn daha sürer.");
            case LabPerk.NobleProclaim: return Loc.T("Noble Atom: reaching a full shell sends out a huge wave that clears enemies. 10s.", "Soy Atom: tam kabuğa ulaşınca düşmanları temizleyen dev bir dalga yayar. 10 sn.");
            case LabPerk.RadiantSides: return Loc.T("Radiant: 4 emitters instead of 2.", "Işıyan: 2 yerine 4 yayıcı.");
            case LabPerk.RadiantWaves: return Loc.T("Radiant: piercing decay waves.", "Işıyan: delip geçen bozunma dalgaları.");
            case LabPerk.ConductSuper: return Loc.T("Conductive: every level starts fully charged.", "İletken: her bölüme tam yüklü başlar.");
            case LabPerk.ConductPlating: return Loc.T("Conductive: enemies destroyed by zaps sometimes drop an electron.",
                                                      "İletken: şokla yok edilen düşmanlar bazen elektron düşürür.");
            case LabPerk.HalogenHunter: return Loc.T("Halogen: free radicals drift toward you.", "Halojen: serbest radikaller sana doğru sürüklenir.");
            case LabPerk.HalogenCorrosion: return Loc.T("Halogen: enemies inside your electron cloud slow down.",
                                                        "Halojen: elektron bulutuna giren düşmanlar yavaşlar.");
            case LabPerk.AlkaliViolent: return Loc.T("Alkali Metal: the blast leaves a fire for 1s that destroys enemies entering it.",
                                                     "Alkali Metal: patlama 1 sn boyunca içine gireni yok eden bir ateş bırakır.");
            case LabPerk.AlkaliBoomerang: return Loc.T("Alkali Metal: the electron destroys enemies on its way back too.",
                                                       "Alkali Metal: elektron dönüş yolunda da düşmanları yok eder.");
            default: return Loc.T("Start any run as " + PerkName(p) + ".", "Oyuna " + PerkName(p) + " olarak başla.");
        }
    }

    static Sprite PerkIcon(LabPerk p)
    {
        switch (p)
        {
            case LabPerk.Stability:
            case LabPerk.StabilityPickup: return ProcSprites.Stability;
            case LabPerk.StartOrbital:
            case LabPerk.ExtraElectrons: return ProcSprites.Electron;
            case LabPerk.StartNeutrino:
            case LabPerk.NeutrinoPickup: return ProcSprites.Neutrino;
            case LabPerk.SecondWind: return ProcSprites.Ring;
            case LabPerk.Research: return ProcSprites.DataChip;
            case LabPerk.RareChance: return ProcSprites.Star;
            case LabPerk.CharacterChance: return ProcSprites.Quanta;
            case LabPerk.ProbabilityAmplitude: return ProcSprites.Ring;
            case LabPerk.IonQuanta: return ProcSprites.Quanta;
            case LabPerk.IonNeutrino: return ProcSprites.Neutrino;
            case LabPerk.NobleDash: return ProcSprites.Arrow;
            case LabPerk.NobleBlast: return ProcSprites.Gamma;
            case LabPerk.NobleLinger: return ProcSprites.Lock;
            case LabPerk.NobleProclaim: return ProcSprites.Ring;
            case LabPerk.RadiantSides: return ProcSprites.Trefoil;
            case LabPerk.RadiantWaves: return ProcSprites.Ring;
            case LabPerk.ConductSuper: return ProcSprites.Bolt;
            case LabPerk.ConductPlating: return ProcSprites.Electron;
            case LabPerk.HalogenHunter: return ProcSprites.Shard;
            case LabPerk.HalogenCorrosion: return ProcSprites.Spiky;
            case LabPerk.AlkaliViolent: return ProcSprites.Ring;
            case LabPerk.AlkaliBoomerang: return ProcSprites.Arrow;
            default: return IconFor(GameManager.CharacterCard(Lab.CharacterOf(p)));
        }
    }

    static Color PerkColor(LabPerk p)
    {
        switch (p)
        {
            case LabPerk.Stability:
            case LabPerk.StabilityPickup: return Pickup.HeartColor;
            case LabPerk.StartOrbital:
            case LabPerk.ExtraElectrons: return Pickup.OrbColor;
            case LabPerk.StartNeutrino:
            case LabPerk.NeutrinoPickup: return Pickup.NeutrinoColor;
            case LabPerk.SecondWind: return Color.white;
            case LabPerk.Research: return Lab.DataColor;
            case LabPerk.RareChance: return new Color(0.85f, 0.65f, 1f);
            case LabPerk.CharacterChance: return CardGold;
            case LabPerk.ProbabilityAmplitude: return new Color(0.62f, 0.72f, 1f);
            default:
                GameManager.Character owner = UpgradeOwner(p);
                return GameManager.UpgradeColor(GameManager.CharacterCard(owner != GameManager.Character.None ? owner : Lab.CharacterOf(p)));
        }
    }

    // ---------------------------------------------------------------- geometry

    /// <summary>Polar position around the core as a panel offset (y down).</summary>
    static Vector2 LabPolar(float r, float deg)
    {
        float a = deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * r;
    }

    /// <summary>A point along a beam (t = 0 at its start, 1 at its end), as a core offset.
    /// Beams interpolate in polar coordinates, so they curve around the core.</summary>
    static Vector2 LabLinkOffset(LabLink l, float t)
    {
        float r0 = l.from == null ? LabCoreRadius : l.from.radius;
        float a0 = l.from == null ? l.startAngle : l.from.angle;
        float r = Mathf.Lerp(r0, l.to.radius, t);
        float a = Mathf.LerpAngle(a0, l.to.angle, t);
        return LabPolar(r, a);
    }

    Vector2 LabLinkPoint(LabLink l, float t) => labC + LabLinkOffset(l, t);

    static float LabEaseBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c = 1.9f;
        float u = t - 1f;
        return 1f + (c + 1f) * u * u * u + c * u * u;
    }

    // ---------------------------------------------------------------- build

    void BuildLab()
    {
        labScreen = Layer(root);
        labScreen.pickingMode = PickingMode.Position;   // blocks the screens behind it
        labScreen.style.backgroundColor = new Color(0f, 0.008f, 0.025f, 0.97f);
        labScreen.style.overflow = Overflow.Hidden;
        labScreen.RegisterCallback<PointerDownEvent>(OnLabPointerDown);
        labScreen.RegisterCallback<PointerMoveEvent>(OnLabPointerMove);
        labScreen.RegisterCallback<PointerUpEvent>(OnLabPointerUp);
        labScreen.RegisterCallback<PointerLeaveEvent>(_ => { labDragging = false; labPressNode = -1; });

        labNodes.Clear();
        labLinks.Clear();
        labPulses.Clear();
        labSparks.Clear();
        labHover = -1;

        labWorld = Layer(labScreen);

        // Soft colored zones behind each branch.
        var zones = LabAnchor(labWorld);
        for (int b = 0; b < 3; b++)
        {
            Vector2 zc = LabPolar(b == 2 ? 580f : 430f, LabBranchAngle[b]);
            var z = Icon(zones, ProcSprites.Glow, 900, WithAlpha(LabBranchColor[b], 0.085f));
            z.style.position = Position.Absolute;
            z.style.left = zc.x - 450;
            z.style.top = zc.y - 450;
        }
        var coreGlow = Icon(zones, ProcSprites.Glow, 260, WithAlpha(Lab.DataColor, 0.16f));
        coreGlow.style.position = Position.Absolute;
        coreGlow.style.left = coreGlow.style.top = -130;

        // Background canvas: drifting motes, orbit rings, beams, the core's orbits.
        labCanvas = Layer(labWorld);
        labCanvas.generateVisualContent += DrawLabCanvas;
        var rng = new System.Random(11);
        labMotes = new LabMote[70];
        for (int i = 0; i < labMotes.Length; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            labMotes[i] = new LabMote
            {
                pos = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()),
                vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (0.004f + 0.01f * (float)rng.NextDouble()),
                size = 0.8f + 1.8f * (float)rng.NextDouble(),
                phase = (float)rng.NextDouble() * 10f,
            };
        }

        // Nodes and branch names, positioned around the core.
        var map = LabAnchor(labWorld);
        foreach (var spec in LabLayout)
        {
            var n = new LabNode
            {
                perk = spec.perk,
                branch = Lab.Branch(spec.perk),
                tier = spec.tier,
                keystone = spec.keystone,
                index = labNodes.Count,
            };
            n.radius = spec.r > 0f ? spec.r : LabRings[spec.tier];
            n.angle = LabBranchAngle[n.branch] + spec.offset;
            n.pos = LabPolar(n.radius, n.angle);
            n.size = spec.keystone ? 64f : spec.tier == 1 ? 60f : spec.tier == 4 ? 48f : 52f;
            BuildLabNode(map, n);
            labNodes.Add(n);
            labByPerk[(int)n.perk] = n;
        }

        // Beams: from the core to each root, and from each parent to its child.
        foreach (var n in labNodes)
        {
            LabPerk[] parents = Lab.Parents(n.perk);
            if (parents.Length == 0)
            {
                float baseAngle = LabBranchAngle[n.branch];
                AddLabLink(null, n, baseAngle + Mathf.DeltaAngle(baseAngle, n.angle) * 0.35f);
            }
            foreach (LabPerk parent in parents) AddLabLink(labByPerk[(int)parent], n, 0f);
        }

        // The map's extent (the drag limits): every node plus room for its label.
        labBounds = new Rect(-LabCoreRadius, -LabCoreRadius, LabCoreRadius * 2f, LabCoreRadius * 2f);
        foreach (var n in labNodes)
        {
            labBounds.xMin = Mathf.Min(labBounds.xMin, n.pos.x - 80f);
            labBounds.xMax = Mathf.Max(labBounds.xMax, n.pos.x + 80f);
            labBounds.yMin = Mathf.Min(labBounds.yMin, n.pos.y - 70f);
            labBounds.yMax = Mathf.Max(labBounds.yMax, n.pos.y + 90f);
        }

        for (int b = 0; b < 3; b++)
        {
            Vector2 lp = b == 2 ? LabPolar(LabCharUpgradeRadius + 90f, 90f) : LabPolar(LabRings[3] + 110f, b == 0 ? 210f : 330f);
            labBounds = Rect.MinMaxRect(Mathf.Min(labBounds.xMin, lp.x - 110f), Mathf.Min(labBounds.yMin, lp.y - 30f),
                                        Mathf.Max(labBounds.xMax, lp.x + 110f), Mathf.Max(labBounds.yMax, lp.y + 30f));
            var holder = Box(map);
            holder.style.position = Position.Absolute;
            holder.style.left = lp.x - 100;
            holder.style.top = lp.y - 12;
            holder.style.width = 200;
            holder.style.alignItems = Align.Center;
            labBranchLabels[b] = Text(holder, "", 17, true, LabBranchColor[b]);
            labBranchLabels[b].style.letterSpacing = 5;
        }

        // The core: the nucleus, with the data bank under it.
        labCore = Box(map);
        labCore.style.position = Position.Absolute;
        labCore.style.left = labCore.style.top = -LabCoreRadius;
        labCore.style.width = labCore.style.height = LabCoreRadius * 2f;
        labCore.style.alignItems = Align.Center;
        labCore.style.justifyContent = Justify.Center;
        Icon(labCore, ProcSprites.Nucleus, 34, Color.white);

        var under = Box(map);
        under.style.position = Position.Absolute;
        under.style.left = -110;
        under.style.width = 220;
        under.style.top = LabCoreRadius + 10;
        under.style.alignItems = Align.Center;
        var bankRow = Row(under);
        Icon(bankRow, ProcSprites.DataChip, 24, Lab.DataColor);
        labBank = Text(bankRow, "0", 26, true, Lab.DataColor);
        labBank.style.marginLeft = 6;

        // FX canvas above the nodes: data pulses and bursts.
        labFx = Layer(labWorld);
        labFx.generateVisualContent += DrawLabFx;

        // Top-left: the title (fixed on screen, the map moves under it).
        var head = Box(labScreen);
        head.style.position = Position.Absolute;
        head.style.left = 30;
        head.style.top = 22;
        var title = Text(head, Loc.T("THE LAB", "LABORATUVAR"), 40, true, Lab.DataColor, TextAnchor.MiddleLeft);
        title.style.letterSpacing = 8;
        var dragHint = Text(head, Loc.T("Drag to move around", "Gezinmek için sürükle"), 14, false, Dim, TextAnchor.MiddleLeft);
        dragHint.style.marginTop = 2;

        var bottom = Column(labScreen, 89f);
        Button(bottom, null, Loc.T("BACK", "GERİ"), "ESC", Dim, false, CloseLab);

        BuildLabTip();
        SyncLab();
        labReveal = LabRevealEnd;
        Show(labScreen, false);
    }

    /// <summary>A zero-size element at the core's position: its children are placed relative to the core.</summary>
    static VisualElement LabAnchor(VisualElement parent)
    {
        var e = Box(parent);
        e.style.position = Position.Absolute;
        e.style.left = Length.Percent(50);
        e.style.top = Length.Percent(LabCenterY * 100f);
        e.style.width = e.style.height = 0;
        e.style.overflow = Overflow.Visible;
        return e;
    }

    void AddLabLink(LabNode from, LabNode to, float startAngle)
    {
        var l = new LabLink { from = from, to = to, startAngle = startAngle, phase = labLinks.Count * 0.37f };
        float len = Vector2.Distance(LabLinkOffset(l, 0f), LabLinkOffset(l, 1f));
        float startGap = from == null ? 4f : from.size * 0.5f + (from.keystone ? 11f : 5f);
        float endGap = to.size * 0.5f + (to.keystone ? 11f : 5f);
        l.t0 = Mathf.Clamp01(startGap / Mathf.Max(1f, len));
        l.t1 = Mathf.Clamp01(1f - endGap / Mathf.Max(1f, len));
        labLinks.Add(l);
        to.incoming.Add(l);
    }

    void BuildLabNode(VisualElement map, LabNode n)
    {
        float s = n.size;
        Color c = PerkColor(n.perk);
        n.holder = Box(map);
        n.holder.style.position = Position.Absolute;
        n.holder.style.left = n.pos.x - s * 0.5f;
        n.holder.style.top = n.pos.y - s * 0.5f;
        n.holder.style.width = n.holder.style.height = s;

        n.glow = Icon(n.holder, ProcSprites.Glow, s * 2.2f, WithAlpha(c, 0f));
        n.glow.style.position = Position.Absolute;
        n.glow.style.left = n.glow.style.top = -s * 0.6f;

        if (n.keystone)
        {
            n.frame = Box(n.holder);
            n.frame.style.position = Position.Absolute;
            n.frame.style.left = n.frame.style.top = -8;
            n.frame.style.width = n.frame.style.height = s + 16;
            Radius(n.frame, (s + 16) * 0.5f);
        }

        n.root = new VisualElement { pickingMode = PickingMode.Position };
        n.root.style.position = Position.Absolute;
        n.root.style.left = n.root.style.top = 0;
        n.root.style.width = n.root.style.height = s;
        n.root.style.justifyContent = Justify.Center;
        n.root.style.alignItems = Align.Center;
        Radius(n.root, s * 0.5f);
        n.holder.Add(n.root);
        n.iconHolder = Box(n.root);
        Icon(n.iconHolder, PerkIcon(n.perk), s * 0.54f, c);
        n.question = Text(n.root, "?", s * 0.5f, true, WithAlpha(Color.white, 0.4f));
        n.question.style.position = Position.Absolute;

        n.spark = Icon(n.holder, ProcSprites.Ball, 8, Color.white);
        n.spark.style.position = Position.Absolute;
        n.spark.style.left = n.spark.style.top = s * 0.5f - 4f;

        var below = Box(n.holder);
        below.style.position = Position.Absolute;
        below.style.top = s + (n.keystone ? 11 : 5);
        below.style.left = s * 0.5f - 60f;
        below.style.width = 120;
        below.style.alignItems = Align.Center;
        n.pips = MakePips(below, ProcSprites.Ball, 7, 2);
        n.costRow = Row(below);
        n.costRow.style.justifyContent = Justify.Center;
        n.costRow.style.marginTop = 1;
        Icon(n.costRow, ProcSprites.DataChip, 13, Lab.DataColor);
        n.cost = Text(n.costRow, "", 13, true, Lab.DataColor);
        n.cost.style.marginLeft = 3;

        int index = n.index;
        n.root.RegisterCallback<PointerEnterEvent>(_ =>
        {
            if (labDragging && labDragMoved) return;   // no tips while dragging the map
            if (labHover != index && labReveal >= LabRevealEnd) Sfx.Play("lock", 0.25f);
            labHover = index;
        });
        n.root.RegisterCallback<PointerLeaveEvent>(_ => { if (labHover == index) labHover = -1; });
        // The press bubbles on to the screen (it may become a drag); the release researches it.
        n.root.RegisterCallback<PointerDownEvent>(ev =>
        {
            if (ev.button == 0 && labReveal >= LabRevealEnd) labPressNode = index;
        });
    }

    // ------------------------------------------------------------- dragging

    void OnLabPointerDown(PointerDownEvent ev)
    {
        if (labReveal < LabRevealEnd) { SkipLabReveal(); labPressNode = -1; return; }
        if (ev.button != 0) return;
        labDragging = true;
        labDragMoved = false;
        labDragStart = ev.position;
        labPanStart = labPan;
    }

    void OnLabPointerMove(PointerMoveEvent ev)
    {
        if (!labDragging) return;
        Vector2 d = (Vector2)ev.position - labDragStart;
        if (!labDragMoved && d.sqrMagnitude < 6f * 6f) return;   // a small wobble is still a click
        labDragMoved = true;
        labHover = -1;
        labPan = ClampLabPan(labPanStart + d);
    }

    void OnLabPointerUp(PointerUpEvent ev)
    {
        if (ev.button != 0) return;
        bool click = labDragging && !labDragMoved;
        int press = labPressNode;
        labDragging = false;
        labPressNode = -1;
        if (click && press >= 0 && press < labNodes.Count) TryResearch(labNodes[press]);
    }

    /// <summary>Keeps the map on screen: it can't be dragged further than its edge (plus a margin).</summary>
    Vector2 ClampLabPan(Vector2 pan)
    {
        float W = labScreen.layout.width, H = labScreen.layout.height;
        if (float.IsNaN(W) || W <= 0f) return Vector2.zero;
        return new Vector2(ClampLabAxis(pan.x, W * 0.5f, W, labBounds.xMin, labBounds.xMax),
                           ClampLabAxis(pan.y, H * LabCenterY, H, labBounds.yMin, labBounds.yMax));
    }

    static float ClampLabAxis(float pan, float core, float size, float min, float max)
    {
        float lo = size - LabPanMargin - core - max;   // the far edge reaches the screen's edge
        float hi = LabPanMargin - core - min;          // the near edge reaches the screen's edge
        return lo > hi ? (lo + hi) * 0.5f : Mathf.Clamp(pan, lo, hi);
    }

    void BuildLabTip()
    {
        labTip = Box(labScreen);
        labTip.style.position = Position.Absolute;
        labTip.style.width = 300;
        labTip.style.paddingLeft = labTip.style.paddingRight = 16;
        labTip.style.paddingTop = labTip.style.paddingBottom = 11;
        labTip.style.backgroundColor = new Color(0.05f, 0.06f, 0.12f, 0.97f);
        Radius(labTip, 10);
        labTipTitle = Text(labTip, "", 20, true, Color.white, TextAnchor.MiddleLeft, true);
        labTipTitle.style.letterSpacing = 2;
        labTipLevel = Text(labTip, "", 12, true, Dim, TextAnchor.MiddleLeft);
        labTipLevel.style.letterSpacing = 3;
        labTipLevel.style.marginTop = 1;
        labTipBody = Text(labTip, "", 15, false, WithAlpha(Color.white, 0.9f), TextAnchor.UpperLeft, true);
        labTipBody.style.marginTop = 6;
        labTipStatus = Text(labTip, "", 15, true, Lab.DataColor, TextAnchor.MiddleLeft, true);
        labTipStatus.style.marginTop = 8;
        labTip.style.opacity = 0f;
    }

    // ------------------------------------------------------------- research

    void TryResearch(LabNode n)
    {
        // A researched Excited Atom ability: clicking it makes it the one the atom brings.
        if (Lab.IsExcitedAbility(n.perk) && Lab.Has(n.perk))
        {
            if (Lab.Equip(n.perk))
            {
                n.pop = 0.8f;
                n.flash = 1f;
                LabBurst(n.pos, PerkColor(n.perk), 14, 0.7f);
                Sfx.Play("star", 0.6f);
            }
            return;
        }
        if (Lab.Buy(n.perk))
        {
            if (Lab.IsExcitedAbility(n.perk)) Lab.Equip(n.perk);   // a fresh ability is equipped right away
            labBankPop = 1f;
            Sfx.Play("orb", 0.55f);
            labPulses.Add(new LabPulse { path = LabPathTo(n), target = n });
        }
        else
        {
            Sfx.Play("full", 0.5f);
            n.flash = -1f;   // a red blink
        }
    }

    /// <summary>The beams from the core to this node, through researched nodes.</summary>
    List<LabLink> LabPathTo(LabNode n)
    {
        var path = new List<LabLink>();
        LabNode cur = n;
        for (int guard = 0; cur != null && guard < 10; guard++)
        {
            LabLink pick = null;
            foreach (var l in cur.incoming)
                if (l.from == null || l.from.shownLevel > 0) { pick = l; break; }
            if (pick == null) break;
            path.Add(pick);
            cur = pick.from;
        }
        path.Reverse();
        return path;
    }

    void LabPulseLanded(LabPulse p)
    {
        LabNode n = p.target;
        n.shownLevel = Lab.Level(n.perk);
        n.pop = 1f;
        n.flash = 1f;
        LabBurst(n.pos, PerkColor(n.perk), n.keystone ? 30 : 20, 1f);
        bool maxed = n.shownLevel >= Lab.MaxLevel(n.perk);
        Sfx.Play(maxed ? "level" : "coin", 0.9f);
        if (Lab.CharacterOf(n.perk) != GameManager.Character.None) Sfx.Play("reveal", 0.6f);
    }

    void LabBurst(Vector2 at, Color c, int count, float power)
    {
        for (int i = 0; i < count; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            float sp = (60f + Random.value * 190f) * power;
            float life = 0.35f + Random.value * 0.45f;
            labSparks.Add(new LabSpark
            {
                pos = at,
                vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp,
                life = life,
                maxLife = life,
                size = 1.5f + Random.value * 2.5f,
                color = Random.value < 0.3f ? Color.white : c,
            });
        }
    }

    /// <summary>Makes the map show the saved state at once (no animations pending).</summary>
    void SyncLab()
    {
        labPulses.Clear();
        labSparks.Clear();
        foreach (var n in labNodes)
        {
            n.shownLevel = Lab.Level(n.perk);
            n.shownOpen = Lab.IsOpen(n.perk);
            n.lightAt = -1f;
            n.pop = n.flash = 0f;
        }
    }

    void SkipLabReveal()
    {
        if (labOpen) labReveal = Mathf.Max(labReveal, LabRevealEnd);
    }

    // ------------------------------------------------------------- open / close

    void OpenLab()
    {
        if (labOpen || indexOpen || chooserOpen) return;
        labOpen = true;
        labHover = -1;
        labReveal = 0f;
        labTipAlpha = 0f;
        labDragging = false;
        labPressNode = -1;
        labPan = new Vector2(0f, 200f);   // opens with the core low, so the characters show
        SyncLab();
        Sfx.Play("orb", 0.5f);
    }

    void CloseLab()
    {
        if (!labOpen) return;
        labOpen = false;
    }

    // ---------------------------------------------------------------- update

    void UpdateLab(GameManager.State st)
    {
        if (labScreen == null) return;
        if (labOpen && st != GameManager.State.Title && st != GameManager.State.GameOver) labOpen = false;
        if (chooserOpen && st != GameManager.State.Title && st != GameManager.State.GameOver) HideChooser();
        Show(labScreen, labOpen);
        UpdateChooser();
        var lk = Keyboard.current;
        if (!AnyOverlay && lk != null && lk.lKey.wasPressedThisFrame
            && (st == GameManager.State.Title || (st == GameManager.State.GameOver && gm.StateTime > 0.8f)))
        {
            OpenLab();
            return;
        }
        if (!labOpen) return;

        var kb = Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)) { CloseLab(); return; }
        if (kb != null && labReveal < LabRevealEnd && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            SkipLabReveal();

        float dt = Time.unscaledDeltaTime, ut = Time.unscaledTime;
        labReveal = Mathf.Min(LabRevealEnd, labReveal + dt);
        labPan = ClampLabPan(labPan);   // also when the window is resized
        labWorld.style.translate = new Translate(labPan.x, labPan.y);

        // Bank and overall progress.
        labBankPop = Mathf.MoveTowards(labBankPop, 0f, dt / 0.35f);
        labBank.text = Lab.Data.ToString();
        float bs = 1f + 0.25f * labBankPop * labBankPop;
        labBank.style.scale = new Scale(new Vector3(bs, bs, 1f));
        float coreK = LabEaseBack(labReveal / 0.25f);
        labCore.style.scale = new Scale(new Vector3(coreK, coreK, 1f));

        // Data pulses travel out along the beams (about 6 beams a second).
        for (int i = labPulses.Count - 1; i >= 0; i--)
        {
            var p = labPulses[i];
            p.t += dt * 6.5f;
            if (p.t >= p.path.Count) { LabPulseLanded(p); labPulses.RemoveAt(i); }
        }
        for (int i = labSparks.Count - 1; i >= 0; i--)
        {
            var s = labSparks[i];
            s.life -= dt;
            s.pos += s.vel * dt;
            s.vel *= Mathf.Exp(-3.5f * dt);
            if (s.life <= 0f) labSparks.RemoveAt(i);
        }
        for (int i = 0; i < labMotes.Length; i++)
        {
            Vector2 mp = labMotes[i].pos + labMotes[i].vel * dt;
            mp.x = Mathf.Repeat(mp.x, 1f);
            mp.y = Mathf.Repeat(mp.y, 1f);
            labMotes[i].pos = mp;
        }

        // Nodes light up (one after another) once every parent shows a level.
        int stagger = 0;
        foreach (var n in labNodes)
        {
            bool parentsDone = true;
            foreach (LabPerk parent in Lab.Parents(n.perk))
                if (labByPerk[(int)parent].shownLevel < 1) parentsDone = false;
            if (!parentsDone) { n.shownOpen = false; n.lightAt = -1f; continue; }
            if (n.shownOpen) continue;
            if (n.lightAt < 0f) n.lightAt = ut + 0.12f + 0.14f * stagger++;
            if (ut >= n.lightAt)
            {
                n.shownOpen = true;
                n.lightAt = -1f;
                n.pop = 0.8f;
                n.flash = 1f;
                LabBurst(n.pos, LabBranchColor[n.branch], 10, 0.6f);
                Sfx.Play("star", 0.35f);
            }
        }

        int[] done = labBranchDone, max = labBranchMax;
        System.Array.Clear(done, 0, 3);
        System.Array.Clear(max, 0, 3);
        foreach (var n in labNodes)
        {
            UpdateLabNode(n, ut, dt);
            int m = Lab.MaxLevel(n.perk);
            done[n.branch] += Mathf.Min(n.shownLevel, m);
            max[n.branch] += m;
        }
        for (int b = 0; b < 3; b++)
        {
            labBranchLabels[b].text = Loc.T(BranchNamesEn[b], BranchNamesTr[b]) + "  " + done[b] + "/" + max[b];
            labBranchLabels[b].style.opacity = Mathf.Clamp01((labReveal - LabTierStart(2)) / 0.3f) * (done[b] >= max[b] ? 1f : 0.8f);
        }

        UpdateLabTip(dt);
        labCanvas.MarkDirtyRepaint();
        labFx.MarkDirtyRepaint();
    }

    readonly int[] labBranchDone = new int[3], labBranchMax = new int[3];

    void UpdateLabNode(LabNode n, float ut, float dt)
    {
        int lvl = n.shownLevel, max = Lab.MaxLevel(n.perk);
        bool open = n.shownOpen, disc = Lab.Discovered(n.perk);
        bool maxed = lvl >= max;
        bool can = open && disc && Lab.CanBuy(n.perk);
        Color c = PerkColor(n.perk), bc = LabBranchColor[n.branch];
        bool hover = labHover == n.index;
        float pulse = 0.5f + 0.5f * Mathf.Sin(ut * 4f + n.index);

        n.pop = Mathf.MoveTowards(n.pop, 0f, dt / 0.4f);
        n.flash = Mathf.MoveTowards(n.flash, 0f, dt / 0.45f);

        n.pips.Set(max, lvl, c, WithAlpha(Color.white, open ? 0.2f : 0.1f));
        Show(n.iconHolder, disc);
        Show(n.question, !disc);

        Color border, bg;
        float iconAlpha, glowAlpha;
        if (!open || !disc)
        {
            border = WithAlpha(Color.white, 0.14f);
            bg = new Color(0.04f, 0.05f, 0.09f, 0.9f);
            iconAlpha = 0.2f;
            glowAlpha = 0f;
        }
        else if (maxed)
        {
            border = c;
            bg = Color.Lerp(new Color(0.04f, 0.05f, 0.09f, 1f), c, 0.42f);
            iconAlpha = 1f;
            glowAlpha = 0.2f + 0.05f * pulse;
        }
        else if (lvl > 0)
        {
            border = WithAlpha(c, can ? 0.65f + 0.35f * pulse : 0.85f);
            bg = Color.Lerp(new Color(0.04f, 0.05f, 0.09f, 1f), c, 0.2f);
            iconAlpha = 1f;
            glowAlpha = can ? 0.12f + 0.18f * pulse : 0.08f;
        }
        else
        {
            border = WithAlpha(c, can ? 0.45f + 0.55f * pulse : 0.32f);
            bg = Color.Lerp(new Color(0.04f, 0.05f, 0.09f, 1f), c, 0.07f);
            iconAlpha = 0.7f;
            glowAlpha = can ? 0.1f + 0.18f * pulse : 0f;
        }
        // The Excited Atom brings one ability: the researched one that isn't equipped is dimmed.
        bool benched = Lab.IsExcitedAbility(n.perk) && lvl > 0 && Lab.ExcitedAbility != n.perk;
        if (benched)
        {
            border = WithAlpha(c, 0.35f);
            bg = Color.Lerp(new Color(0.04f, 0.05f, 0.09f, 1f), c, 0.1f);
            iconAlpha = 0.45f;
            glowAlpha = 0f;
            maxed = false;   // no orbiting spark either
        }
        if (n.flash > 0f) border = Color.Lerp(border, Color.white, n.flash);
        else if (n.flash < 0f) border = Color.Lerp(border, Danger, -n.flash);
        if (hover) border = Color.Lerp(border, Color.white, 0.45f);

        Border(n.root, hover ? 3f : 2f, border);
        n.root.style.backgroundColor = bg;
        n.iconHolder.style.opacity = iconAlpha;
        n.glow.style.unityBackgroundImageTintColor = WithAlpha(c, glowAlpha + 0.35f * Mathf.Max(0f, n.flash));
        if (n.frame != null)
            Border(n.frame, 1.5f, WithAlpha(maxed ? c : bc, !open ? 0.12f : maxed ? 0.85f : 0.3f + 0.25f * (lvl > 0 ? 1f : 0f)));

        // A spark slowly circles fully researched nodes.
        Show(n.spark, maxed && disc);
        if (maxed && disc)
        {
            float a = ut * 1.3f + n.index * 1.7f;
            float r = n.size * 0.5f + (n.keystone ? 8f : 5f);
            n.spark.style.translate = new Translate(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            n.spark.style.unityBackgroundImageTintColor = Color.Lerp(c, Color.white, 0.6f);
        }

        bool showCost = hover && open && disc && lvl < max;   // only while hovered, to keep the map clean
        Show(n.costRow, showCost);
        if (showCost)
        {
            int cost = Lab.NextCost(n.perk);
            n.cost.text = cost.ToString();
            n.cost.style.color = Lab.Data >= cost ? Lab.DataColor : WithAlpha(Danger, 0.85f);
        }

        // Opening: each ring pops in after its beams have grown.
        float k = (labReveal - LabTierStart(n.tier) - 0.17f) / 0.2f;
        float s = LabEaseBack(k) * (hover ? 1.08f : 1f) * (1f + 0.3f * n.pop * n.pop);
        n.holder.style.scale = new Scale(new Vector3(s, s, 1f));
        n.holder.style.opacity = Mathf.Clamp01(k * 3f);
    }

    void UpdateLabTip(float dt)
    {
        bool show = labHover >= 0 && labHover < labNodes.Count && labReveal >= LabRevealEnd;
        labTipAlpha = Mathf.MoveTowards(labTipAlpha, show ? 1f : 0f, dt / 0.12f);
        labTip.style.opacity = labTipAlpha;
        if (!show) return;

        LabNode n = labNodes[labHover];
        LabPerk p = n.perk;
        Color c = PerkColor(p);
        int lvl = Lab.Level(p), max = Lab.MaxLevel(p);
        bool disc = Lab.Discovered(p), open = Lab.IsOpen(p);
        Border(labTip, 2f, WithAlpha(disc ? c : Color.white, disc ? 0.8f : 0.3f));
        labTipTitle.text = disc ? PerkName(p) : "???";
        labTipTitle.style.color = disc ? c : Dim;
        labTipLevel.text = max > 1 ? lvl + " / " + max : "";
        Show(labTipLevel, max > 1);
        labTipLevel.style.color = WithAlpha(LabBranchColor[n.branch], 0.85f);
        labTipBody.text = disc ? PerkBody(p)
            : Loc.T("Find it in a character box first.", "Önce bir karakter kutusunda bul.");

        string status;
        Color sc = Lab.DataColor;
        if (lvl >= max && Lab.IsExcitedAbility(p))
        {
            bool equipped = Lab.ExcitedAbility == p;
            status = equipped ? Loc.T("EQUIPPED", "TAKILI") : Loc.T("CLICK TO EQUIP", "TAKMAK İÇİN TIKLA");
            sc = equipped ? c : Dim;
        }
        else if (lvl >= max) { status = Loc.T("MAXED", "TAMAMLANDI"); sc = c; }
        else if (!open)
        {
            var needs = new List<string>();
            foreach (LabPerk parent in Lab.Parents(p))
                if (Lab.Level(parent) < 1) needs.Add(PerkName(parent));
            status = Loc.T("Needs: ", "Gerekli: ") + string.Join(" + ", needs);
            sc = Dim;
        }
        else if (!disc) { status = Loc.T("UNDISCOVERED", "KEŞFEDİLMEDİ"); sc = Dim; }
        else
        {
            int cost = Lab.NextCost(p);
            bool can = Lab.Data >= cost;
            status = (can ? "" : Loc.T("NEED ", "GEREKLİ: ")) + cost + Loc.T(" DATA", " VERİ");
            sc = can ? Lab.DataColor : Danger;
        }
        labTipStatus.text = status;
        labTipStatus.style.color = sc;

        // Beside the node, on its outer side; flipped if it would leave the screen.
        float W = labScreen.layout.width, H = labScreen.layout.height;
        if (float.IsNaN(W) || W <= 0f) return;
        float w = 300f;
        float h = labTip.layout.height;
        if (float.IsNaN(h) || h <= 0f) h = 150f;
        float cx = W * 0.5f + labPan.x + n.pos.x, cy = H * LabCenterY + labPan.y + n.pos.y;
        float gap = n.size * 0.5f + (n.keystone ? 22f : 16f);
        bool right = n.pos.x > 0f || (Mathf.Abs(n.pos.x) < 1f);
        float x = right ? cx + gap : cx - gap - w;
        if (x + w > W - 10f) x = cx - gap - w;
        if (x < 10f) x = cx + gap;
        float y = Mathf.Clamp(cy - h * 0.5f, 10f, H - h - 10f);
        labTip.style.left = x;
        labTip.style.top = y;
    }

    // ---------------------------------------------------------------- drawing

    void DrawLabCanvas(MeshGenerationContext mgc)
    {
        Rect r = labCanvas.contentRect;
        if (r.width <= 0f || r.height <= 0f) return;
        labC = new Vector2(r.width * 0.5f, r.height * LabCenterY);
        var p = mgc.painter2D;
        p.lineCap = LineCap.Round;
        p.lineJoin = LineJoin.Round;
        float ut = Time.unscaledTime;
        float fade = Mathf.Clamp01(labReveal / 0.4f);

        // Drifting motes: they fill the screen wherever the map is, moving slower than it (depth).
        for (int i = 0; i < labMotes.Length; i++)
        {
            LabMote m = labMotes[i];
            float tw = 0.5f + 0.5f * Mathf.Sin(ut * 1.3f + m.phase);
            Vector2 sp = new Vector2(Mathf.Repeat(m.pos.x * r.width + labPan.x * 0.35f, r.width),
                                     Mathf.Repeat(m.pos.y * r.height + labPan.y * 0.35f, r.height));
            FillCircle(p, sp - labPan, m.size, new Color(0.7f, 0.9f, 1f, (0.08f + 0.22f * tw) * fade));
        }

        // Orbit rings (depth tiers).
        for (int t = 1; t < LabRings.Length; t++)
        {
            float k = Mathf.Clamp01((labReveal - LabTierStart(t)) / 0.3f);
            if (k <= 0f) continue;
            StrokeCircle(p, labC, LabRings[t] * Mathf.Lerp(0.85f, 1f, k), 1f, new Color(1f, 1f, 1f, 0.05f * k));
        }

        // Beams.
        foreach (var l in labLinks)
        {
            float grow = Mathf.Clamp01((labReveal - LabTierStart(l.to.tier)) / 0.22f);
            if (grow <= 0f) continue;
            float end = Mathf.Lerp(l.t0, l.t1, grow);
            Color bc = LabBranchColor[l.to.branch];
            bool researched = l.to.shownLevel > 0 && (l.from == null || l.from.shownLevel > 0);
            if (researched)
            {
                bool maxed = l.to.shownLevel >= Lab.MaxLevel(l.to.perk);
                StrokeLink(p, l, l.t0, end, maxed ? 10f : 8f, WithAlpha(bc, maxed ? 0.2f : 0.12f));
                StrokeLink(p, l, l.t0, end, 2.6f, WithAlpha(Color.Lerp(bc, Color.white, maxed ? 0.35f : 0.1f), 0.9f));
                if (grow >= 1f)
                    for (int k = 0; k < 3; k++)   // particles flowing outward
                    {
                        float f = Mathf.Repeat(ut * 0.45f + l.phase + k / 3f, 1f);
                        Vector2 q = LabLinkPoint(l, Mathf.Lerp(l.t0, l.t1, f));
                        float a = Mathf.Sin(f * Mathf.PI);
                        FillCircle(p, q, 5f, WithAlpha(bc, 0.18f * a));
                        FillCircle(p, q, 2.2f, WithAlpha(Color.Lerp(bc, Color.white, 0.6f), 0.95f * a));
                    }
            }
            else if (l.to.shownOpen)
                StrokeLink(p, l, l.t0, end, 2f, WithAlpha(bc, 0.38f));
            else
                DashLink(p, l, l.t0, end, 1.5f, new Color(1f, 1f, 1f, 0.13f));
        }

        // The core: tilted orbits (one per branch color) with electrons, and the progress ring.
        float coreK = LabEaseBack(labReveal / 0.25f);
        if (coreK > 0.01f)
        {
            for (int i = 0; i < 3; i++)
            {
                float tilt = (i * 60f + 15f) * Mathf.Deg2Rad;
                float rx = 46f * coreK, ry = 16f * coreK;
                Color oc = LabBranchColor[(i + 2) % 3];
                p.strokeColor = WithAlpha(oc, 0.35f);
                p.lineWidth = 1.3f;
                p.BeginPath();
                for (int sgm = 0; sgm <= 40; sgm++)
                {
                    float a = sgm / 40f * Mathf.PI * 2f;
                    Vector2 q = labC + LabEllipse(a, rx, ry, tilt);
                    if (sgm == 0) p.MoveTo(q); else p.LineTo(q);
                }
                p.Stroke();
                float ea = ut * (1.7f + 0.35f * i) + i * 2.1f;
                Vector2 e = labC + LabEllipse(ea, rx, ry, tilt);
                FillCircle(p, e, 6f, WithAlpha(oc, 0.25f));
                FillCircle(p, e, 3f, Color.Lerp(oc, Color.white, 0.4f));
            }
            StrokeCircle(p, labC, (LabCoreRadius - 4f) * coreK, 1.5f, WithAlpha(Lab.DataColor, 0.18f));
        }
    }

    void DrawLabFx(MeshGenerationContext mgc)
    {
        Rect r = labFx.contentRect;
        if (r.width <= 0f || r.height <= 0f) return;
        Vector2 c = new Vector2(r.width * 0.5f, r.height * LabCenterY);
        var p = mgc.painter2D;

        foreach (var pulse in labPulses)
        {
            if (pulse.path.Count == 0) continue;
            Color bc = LabBranchColor[pulse.target.branch];
            for (int k = 5; k >= 0; k--)   // a short fading trail behind the head
            {
                float t = pulse.t - k * 0.06f;
                if (t < 0f) continue;
                int i = Mathf.Min(pulse.path.Count - 1, Mathf.FloorToInt(t));
                float f = Mathf.Clamp01(t - i);
                LabLink l = pulse.path[i];
                Vector2 q = c + LabLinkOffset(l, Mathf.Lerp(l.t0, l.t1, f));
                float a = 1f - k / 6f;
                if (k == 0) FillCircle(p, q, 11f, WithAlpha(bc, 0.3f));
                FillCircle(p, q, 4.5f * a + 1f, WithAlpha(Color.Lerp(bc, Color.white, 0.7f), a));
            }
        }

        foreach (var s in labSparks)
        {
            float a = Mathf.Clamp01(s.life / s.maxLife);
            FillCircle(p, c + s.pos, s.size * (0.4f + 0.6f * a), WithAlpha(s.color, a));
        }
    }

    static Vector2 LabEllipse(float a, float rx, float ry, float tilt)
    {
        Vector2 e = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
        float cs = Mathf.Cos(tilt), sn = Mathf.Sin(tilt);
        return new Vector2(e.x * cs - e.y * sn, e.x * sn + e.y * cs);
    }

    static void FillCircle(Painter2D p, Vector2 c, float radius, Color col)
    {
        if (col.a <= 0.003f || radius <= 0f) return;
        p.fillColor = col;
        p.BeginPath();
        p.Arc(c, radius, Angle.Degrees(0f), Angle.Degrees(360f));
        p.ClosePath();
        p.Fill();
    }

    static void StrokeCircle(Painter2D p, Vector2 c, float radius, float width, Color col)
    {
        if (col.a <= 0.003f || radius <= 0f) return;
        p.strokeColor = col;
        p.lineWidth = width;
        p.BeginPath();
        p.Arc(c, radius, Angle.Degrees(0f), Angle.Degrees(360f));
        p.ClosePath();
        p.Stroke();
    }

    void StrokeLink(Painter2D p, LabLink l, float a, float b, float width, Color col)
    {
        if (b <= a) return;
        const int seg = 20;
        p.strokeColor = col;
        p.lineWidth = width;
        p.BeginPath();
        for (int i = 0; i <= seg; i++)
        {
            Vector2 q = LabLinkPoint(l, Mathf.Lerp(a, b, i / (float)seg));
            if (i == 0) p.MoveTo(q); else p.LineTo(q);
        }
        p.Stroke();
    }

    void DashLink(Painter2D p, LabLink l, float a, float b, float width, Color col)
    {
        const float dash = 0.05f, gap = 0.045f;
        for (float t = a; t < b; t += dash + gap)
            StrokeLink(p, l, t, Mathf.Min(b, t + dash), width, col);
    }

    // ------------------------------------------------------------- chooser

    void BuildChooser()
    {
        chooserScreen = Layer(root);
        chooserScreen.style.backgroundColor = new Color(0f, 0f, 0.02f, 0.88f);
        var col = Column(chooserScreen, 24f);
        var t = Text(col, Loc.T("START AS", "NE OLARAK BAŞLA"), 40, true, Color.white);
        t.style.letterSpacing = 6;
        Text(col, Loc.T("Characters you researched in the Lab", "Laboratuvarda araştırdığın karakterler"), 16, false, Dim).style.marginTop = 4;
        chooserRow = Row(col);
        chooserRow.style.marginTop = 30;
        chooserRow.style.justifyContent = Justify.Center;
        chooserRow.style.flexWrap = Wrap.Wrap;   // up to seven cards
        chooserRow.style.maxWidth = 1100;
        var bottom = Column(chooserScreen, 80f);
        Button(bottom, null, Loc.T("BACK", "GERİ"), "ESC", Dim, false, HideChooser);
        Show(chooserScreen, false);
    }

    void ShowChooser()
    {
        if (chooserOpen || labOpen || indexOpen) return;
        chooserOpen = true;
        chooserOpenedFrame = Time.frameCount;   // the key that opened it must not also pick
        chooserRow.Clear();
        AddChooserCard(GameManager.Character.None);
        foreach (GameManager.Character c in Lab.Characters)
            if (Lab.CharacterUnlocked(c)) AddChooserCard(c);
        Sfx.Play("orb", 0.5f);
    }

    void HideChooser()
    {
        chooserOpen = false;
    }

    void AddChooserCard(GameManager.Character c)
    {
        bool normal = c == GameManager.Character.None;
        GameManager.UpgradeType card = GameManager.CharacterCard(c);
        Color col = normal ? Pickup.OrbColor : GameManager.UpgradeColor(card);
        var e = new VisualElement { pickingMode = PickingMode.Position };
        e.style.width = 190;
        e.style.height = 220;
        e.style.marginLeft = e.style.marginRight = 10;
        e.style.marginTop = e.style.marginBottom = 8;
        e.style.alignItems = Align.Center;
        e.style.justifyContent = Justify.Center;
        e.style.backgroundColor = WithAlpha(Panel, 0.9f);
        Radius(e, 14);
        Border(e, 2f, WithAlpha(col, 0.6f));
        Icon(e, normal ? ProcSprites.Electron : IconFor(card), 70, col);
        var name = Text(e, normal ? Loc.T("ATOM", "ATOM") : GameManager.UpgradeTitle(card), 20, true, col, TextAnchor.MiddleCenter, true);
        name.style.marginTop = 12;
        name.style.maxWidth = 180;
        var sub = Text(e, normal ? Loc.T("The usual start", "Her zamanki başlangıç") : Loc.T("Researched", "Araştırıldı"), 14, false, Dim);
        sub.style.marginTop = 4;
        if (normal) Keycap(e, Loc.T("SPACE", "BOŞLUK"), 22).style.marginTop = 10;
        e.RegisterCallback<PointerEnterEvent>(_ => { Border(e, 3f, col); e.style.scale = new Scale(new Vector3(1.05f, 1.05f, 1f)); });
        e.RegisterCallback<PointerLeaveEvent>(_ => { Border(e, 2f, WithAlpha(col, 0.6f)); e.style.scale = new Scale(Vector3.one); });
        e.RegisterCallback<PointerDownEvent>(ev => { if (ev.button == 0) ChooseStart(c); });
        chooserRow.Add(e);
    }

    void ChooseStart(GameManager.Character c)
    {
        HideChooser();
        gm.OverlayOpen = false;
        gm.StartRun(c);
    }

    void UpdateChooser()
    {
        if (chooserScreen == null) return;
        Show(chooserScreen, chooserOpen);
        if (!chooserOpen) return;
        var kb = Keyboard.current;
        if (kb == null || Time.frameCount == chooserOpenedFrame) return;
        if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) HideChooser();
        else if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) ChooseStart(GameManager.Character.None);
    }

    // ------------------------------------------------------- game over payout

    void BuildPayout(VisualElement parent)
    {
        payoutBox = Box(parent);
        payoutBox.style.marginTop = 18;
        payoutBox.style.alignItems = Align.Center;

        payoutTotalRow = Row(payoutBox);
        payoutTotalRow.style.alignItems = Align.Center;
        Icon(payoutTotalRow, ProcSprites.DataChip, 30, Lab.DataColor);
        payoutTotal = Text(payoutTotalRow, "", 30, true, Lab.DataColor);
        payoutTotal.style.marginLeft = 8;
        var word = Text(payoutTotalRow, Loc.T("DATA", "VERİ"), 15, true, Lab.DataColor);
        word.style.marginLeft = 10;
        word.style.letterSpacing = 3;

        payoutNote = Text(payoutBox, "", 14, true, Pickup.Gold);
        payoutNote.style.letterSpacing = 2;
        payoutNote.style.marginTop = 4;
    }

    void UpdatePayout()
    {
        if (payoutBox == null) return;
        GameManager.Payout p = gm.LastPayout;
        if (payoutShownVersion != gm.PayoutVersion) { payoutShownVersion = gm.PayoutVersion; payoutTicked = 0; }
        float t = gm.StateTime;

        // The total counts up into the bank.
        const float From = 0.9f;
        float k = Mathf.Clamp01((t - From) / 0.9f);
        int shown = Mathf.RoundToInt(p.gain * (1f - (1f - k) * (1f - k)));
        payoutTotalRow.style.opacity = t >= From ? 1f : 0f;
        payoutTotal.text = "+" + shown;
        if (shown > payoutTicked && t >= From)
        {
            if (shown / 3 > payoutTicked / 3 || shown == p.gain) Sfx.Play("lock", 0.5f);
            payoutTicked = shown;
            if (shown == p.gain) Sfx.Play("coin", 0.9f);
        }
        float pop = k >= 1f ? 1f + 0.25f * Mathf.Clamp01(1f - (t - From - 0.9f) / 0.3f) : 1f;
        payoutTotal.style.scale = new Scale(new Vector3(pop, pop, 1f));

        payoutNote.text = t >= From + 0.95f && p.record ? Loc.T("NEW RECORD", "YENİ REKOR") : "";
    }
}
