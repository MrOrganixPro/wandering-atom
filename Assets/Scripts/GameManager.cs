using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Owns the game loop: title, levels, stars, lives, pause, level complete, game over,
/// camera follow/shake, hit-stop and the scrolling background. Runs are not saved; only the
/// best level reached is remembered.
/// The HUD and menus are drawn by GameUI (UI Toolkit), which reads this class's state.
/// Created automatically when any scene loads, so it does not need to be in the scene.
/// </summary>
public partial class GameManager : MonoBehaviour
{
    public enum State { Title, Playing, Paused, LevelComplete, Upgrade, GameOver, Dying, Story }

    public static GameManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<GameManager>() == null)
            new GameObject("GameManager").AddComponent<GameManager>();
    }

    /// <summary>Half width/height of the visible area in world units.</summary>
    public static Vector2 HalfExtents
    {
        get
        {
            Camera c = Camera.main;
            if (c == null) return new Vector2(9.8f, 5.5f);
            float h = c.orthographicSize;
            return new Vector2(h * c.aspect, h);
        }
    }

    /// <summary>World position at the center of the screen (ignoring camera shake).</summary>
    public static Vector2 ViewCenter
    {
        get
        {
            if (Instance != null) return Instance.viewCenter;
            Camera c = Camera.main;
            return c != null ? (Vector2)c.transform.position : Vector2.zero;
        }
    }

    const string LevelKey = "Pendulum_Level";
    const string BestKey = "Pendulum_BestLevel";
    const string UpgradeKeyPrefix = "Pendulum_Upg_";
    const string StorySeenKey = "Pendulum_StorySeen";

    public enum UpgradeType
    {
        Heal, MaxHealth, Speed, Torque, Orbital, BallArmor, Challenger, Sluggish,
        BallSize, PickupRange, Repel, Booster,
        Regeneration,              // rare
        Spaceship,                 // character
        FireRate, TurretRange,     // normal, only with the Spaceship
        Insight, CoinRecycler, HealthRecycler,  // rare
        Conductive,                             // character
        HopRange, HopCount, ChargeRate,         // normal, only with the Conductive
        Halogen,                                // character
        HalogenPull, HalogenGrasp, HalogenSnatch,  // normal, only with the Halogen
        Alkali,                                 // character
        AlkaliBlast, AlkaliRange, AlkaliPath,   // normal, only with the Alkali Metal
        Excited,                                // character
        AngularMomentum, GroundState,           // normal (Extras)
        Photoelectric, Resonance,               // rare
        AlkaliReload                            // normal, only with the Alkali Metal
    }

    /// <summary>
    /// Every upgrade belongs to exactly one tier:
    /// Normal (1 coin), Rare (1 coin, special card, rarely offered) and
    /// Character (1 coin, uncommon mystery box, at most one per run, changes how the game plays).
    /// </summary>
    public enum UpgradeTier { Normal, Rare, Character }

    /// <summary>The run's Character (at most one per run). Values are saved, so only append.</summary>
    public enum Character { None = 0, Challenger = 1, Sluggish = 2, Spaceship = 3, Conductive = 4, Halogen = 5, Alkali = 6, Excited = 7 }

    [Header("Rules")]
    [Tooltip("Base max health (before upgrades).")]
    public int startingLives = 3;
    [Tooltip("Stars needed on level N = this + ceil(N / levelsPerExtraStar).")]
    public int baseStarsPerLevel = 2;
    [Tooltip("The star requirement grows by one every this many levels.")]
    public int levelsPerExtraStar = 2;
    [Tooltip("How many stars exist on the map at the same time.")]
    public int maxActiveStars = 3;
    public float starMinDistance = 10f;
    public float starMaxDistance = 16f;
    [Tooltip("How much further away stars may appear per level.")]
    public float starDistancePerLevel = 1.2f;
    public float starMaxDistanceCap = 34f;
    public float starSpacing = 5f;
    public float levelCompleteDelay = 2.5f;

    [Header("Orbs (each adds a ball; all spawn at level start)")]
    [Tooltip("Orbs on level 1 and 2 (before the Sluggish multiplier).")]
    public int orbsPerLevel = 3;
    [Tooltip("One more orb spawns per level every this many levels (levels 4, 8, 12, ...).")]
    public int levelsPerExtraOrb = 4;
    public float orbMinDistance = 5f;
    public float orbMaxDistance = 14f;

    [Header("Hazards: Asteroids (no warning)")]
    public int asteroidUnlockLevel = 2;
    public Vector2 asteroidInterval = new Vector2(9f, 16f);
    public Vector2 asteroidRadius = new Vector2(2.5f, 4.5f);
    public Vector2 asteroidSpeed = new Vector2(0.4f, 1.1f);
    public int maxAsteroids = 4;

    [Header("Hazards: Gravity Wells (no warning)")]
    public int wellUnlockLevel = 4;
    public Vector2 wellInterval = new Vector2(22f, 35f);
    public float wellCoreRadius = 0.7f;
    public float wellPullRadius = 6.5f;
    [Tooltip("Pull strength on the level wells unlock (acceleration near the core before the core boost).")]
    public float wellPullStrength = 6f;
    [Tooltip("Pull strength added every level after wells unlock (no cap).")]
    public float wellPullPerLevel = 0.6f;
    [Tooltip("How much stronger the pull gets right next to the core than a plain linear falloff. "
           + "1 = linear (weak at the edge, strong in the middle); higher = much stronger close in.")]
    public float wellCorePullMultiplier = 2f;
    public Vector2 wellSpeed = new Vector2(0.3f, 0.6f);
    public int maxWells = 1;

    [Header("Coins (each = one upgrade choice after the level)")]
    public int coinsPerLevel = 1;
    public float coinMinDistance = 7f;
    public float coinMaxDistance = 15f;

    [Header("Hearts (extremely rare, only when hurt)")]
    [Tooltip("Chance that a level gets a heart at all (at most one per level).")]
    [Range(0f, 1f)] public float heartChancePerLevel = 0.08f;
    [Tooltip("Chance a level holds a neutrino pickup (before Lab research).")]
    [Range(0f, 1f)] public float neutrinoPickupChance = 0.05f;
    [Tooltip("Seconds into the level before the neutrino pickup appears (random in this range).")]
    public Vector2 neutrinoPickupDelay = new Vector2(8f, 25f);
    [Tooltip("Seconds spent hurt in the level before that heart appears.")]
    public float heartMinInterval = 35f;
    public float heartMaxInterval = 70f;
    public float heartMinDistance = 5f;
    public float heartMaxDistance = 10f;

    [Header("Upgrades")]
    public int upgradeChoices = 3;
    [Tooltip("Speed upgrade: top speed +this fraction each.")]
    public float speedPerUpgrade = 0.1f;
    [Tooltip("Speed upgrade: acceleration (thrust) +this fraction each.")]
    public float thrustPerUpgrade = 0.1f;
    public float torquePerUpgrade = 6f;
    [Tooltip("Each Torque upgrade also adds this much turning/braking authority (base 0.45, max 1).")]
    public float turnAuthorityPerTorque = 0.15f;
    [Tooltip("Ball Armor can be bought this many times (balls have at most 1 + this many hit points).")]
    public int maxBallArmorUpgrades = 2;
    [Tooltip("One Ball Armor purchase is unlocked ('banked') every this many levels.")]
    public int levelsPerBallArmor = 4;

    [Tooltip("Bigger Balls: ball size +this fraction per upgrade.")]
    public float ballSizePerUpgrade = 0.35f;
    [Header("Angular Momentum / Ground State (normal)")]
    [Tooltip("Angular Momentum: orbit speed added per level (0.4 = +40%).")]
    public float spinPerUpgrade = 0.4f;
    public int maxSpinUpgrades = 3;
    [Tooltip("Ground State: every energy level is this much closer per level (0.1 = 10%).")]
    public float groundStatePerUpgrade = 0.1f;
    public int maxGroundStateUpgrades = 3;
    [Header("Photoelectric Effect / Resonance (rare)")]
    [Tooltip("Photoelectric Effect: electrons knocked loose by every photon collected.")]
    public int photoelectricElectrons = 2;
    public float photoelectricMinDistance = 2f, photoelectricMaxDistance = 3.2f;
    public int maxBallSizeUpgrades = 3;
    [Tooltip("Pickup Range: extra reach (world units) for the thruster and every ball, per upgrade.")]
    public float pickupRangePerUpgrade = 0.6f;
    public int maxPickupRangeUpgrades = 3;
    [Tooltip("Repel / Emergency Booster: the first upgrade unlocks it, later ones make it stronger.")]
    public int maxRepelUpgrades = 5;
    public int maxBoosterUpgrades = 5;

    [Header("Rare Upgrades (special card, costs 1 coin)")]
    public int rareUnlockLevel = 3;
    [Tooltip("Chance a set of upgrade cards includes a rare one.")]
    [Range(0f, 1f)] public float rareChance = 0.05f;
    [Tooltip("Coin Recycler: orbs recycled (with full orbitals) per coin.")]
    public int orbsPerRecycledCoin = 5;
    [Tooltip("Health Recycler: orbs recycled (with full orbitals, while hurt) per heart restored.")]
    public int orbsPerRecycledHeart = 3;

    [Header("Orbitals (New Orbital upgrade: +2 electron slots each, no limit)")]
    [Tooltip("Rope length of the innermost shell.")]
    public float firstShellLength = 1.0f;
    [Tooltip("Each further shell sits this much farther out.")]
    public float shellLengthStep = 0.62f;
    [Tooltip("Quanta per level gained (for the rest of the run) for every energy level filled with electrons.")]
    public int coinsPerExtraShell = 1;

    [Header("Upgrade screen (each quantum flies to the center, spins up and opens)")]
    public float upgradeFlyTime = 0.6f;
    public float upgradeSpinTime = 1.6f;
    [Tooltip("Round start: the player's core flies from the map to the center of the screen.")]
    public float upgradeCoreFlyTime = 0.7f;
    [Tooltip("A picked upgrade curls into a ball and flies into the core (scaled like the other timings).")]
    public float upgradeAbsorbTime = 0.9f;
    [Tooltip("Round end: the core flies back to the map.")]
    public float upgradeCoreReturnTime = 0.6f;
    [Tooltip("Each further quantum of the same round plays this much faster (multiplier per pick).")]
    [Range(0.3f, 1f)] public float upgradeSpeedup = 0.72f;
    [Tooltip("The fastest it gets (fraction of the first quantum's timing).")]
    [Range(0.1f, 1f)] public float upgradeMinPace = 0.3f;

    [Header("Characters (uncommon mystery box, costs 1 coin, one per run)")]
    [FormerlySerializedAs("gameChangerUnlockLevel")] public int characterUnlockLevel = 5;
    [Tooltip("Chance a pick includes a Character box.")]
    [FormerlySerializedAs("gameChangerChance")]
    [Range(0f, 1f)] public float characterChance = 0.05f;
    public float mysteryRevealTime = 2.4f;
    [Tooltip("Lonely Ion: quanta per level on levels 1-5 (shells don't add any)...")]
    public int ionBaseQuanta = 2;
    [Tooltip("...and +1 quantum every this many levels after that (6-10: 3, 11-15: 4, ...).")]
    public int ionLevelsPerQuantum = 5;
    [Tooltip("Lonely Ion: free neutrinos every level it completes (on top of the flawless one).")]
    public int ionNeutrinosPerLevel = 1;
    [Tooltip("Lonely Ion: neutrino and stability pickup chances are multiplied by this.")]
    public float ionFindMultiplier = 2f;
    public int sluggishMaxBallArmor = 4;
    [Tooltip("Excited Atom: orbs per level are multiplied by this.")]
    [FormerlySerializedAs("sluggishOrbMultiplier")]
    public int excitedOrbMultiplier = 2;
    [Tooltip("Spaceship: Fire Rate upgrade, +this fraction of shots per second each.")]
    public float fireRatePerUpgrade = 0.25f;
    public int maxFireRateUpgrades = 5;
    [Tooltip("Spaceship: Turret Range upgrade, +this many world units each.")]
    public float turretRangePerUpgrade = 1f;
    public int maxTurretRangeUpgrades = 5;

    [Header("Music")]
    [Tooltip("Volume of the generative ambient score (0 = off).")]
    [Range(0f, 1f)] public float musicVolume = 0.4f;

    [Header("Camera & Look")]
    public float cameraSize = 7f;
    public float cameraSmoothTime = 0.25f;
    [Tooltip("Camera leads ahead of the moving pendulum by this many seconds of its velocity.")]
    public float cameraLookAhead = 0.35f;
    public Color backgroundCenter = new Color(0.10f, 0.12f, 0.22f);
    public Color backgroundEdge = new Color(0.02f, 0.02f, 0.05f);
    public int dustCount = 50;

    public State CurrentState => state;
    /// <summary>Set by GameUI while a full-screen overlay (the Index) is open: menu keys are ignored.</summary>
    public bool OverlayOpen { get; set; }
    public int Level => level;

    State state = State.Title;
    int level = 1, bestLevel = 1;
    int levelStartCoinProgress, levelStartHeartProgress;   // restored when a level is retried
    int lives, starsNeeded, starsCollected, starsSpawned;
    float levelTime, stateTime, shake, hitStop, heartTimer, asteroidTimer, wellTimer;
    bool heartAvailable;   // this level rolled its (rare) heart and hasn't spawned it yet
    readonly List<Pickup> stars = new List<Pickup>();
    readonly List<Pickup> orbs = new List<Pickup>();
    readonly List<Pickup> extras = new List<Pickup>();   // coins + hearts

    // Upgrades (saved).
    int upgMaxHealth, upgSpeed, upgTorque, upgOrbitals, upgBallArmor;
    int upgBallSize, upgPickupRange, upgRepel, upgBooster, upgRegen;
    int upgFireRate, upgTurretRange;
    int upgInsight, upgCoinRecycler, upgHealthRecycler;   // rare, once per run
    int upgSpin, upgGroundState;                          // normal
    int upgPhotoelectric, upgResonance;                   // rare, once per run
    int coinRecycleProgress, heartRecycleProgress;        // orbs recycled toward the next coin / heart
    int rarePity, characterPity;                          // Lab Probability Amplitude: rolls missed in a row this run
    int levelCoins, pendingChoices, choiceIndex, choiceTotal, refundTotal, offerVersion;
    readonly List<UpgradeType> offer = new List<UpgradeType>();
    Character character = Character.None;
    int appliedLayout = -1;
    int pickNumber;          // picks made so far this round (each plays faster)

    // The Lab: what this run is worth (paid out when it ends) and its once-per-run perks.
    int runMaxElectrons;           // highest atomic number reached with real electrons
    int runPaid;                   // Research Data already paid for this run (a retry only pays the difference)
    bool secondWindUsed;
    bool neutrinoPickupPending;
    float neutrinoPickupTimer;
    float ionNeutrinoTimer = -1f;   // Lab Ghost Trail: seconds until the Lonely Ion's guaranteed neutrino (< 0: none)
    public struct Payout { public int atomicNumber, level, value, gain, bank; public bool record; public float bonus; }
    /// <summary>The last run end's Research Data (shown on the game over screen).</summary>
    public Payout LastPayout { get; private set; }
    public int PayoutVersion { get; private set; }

    // Energy levels: filling a shell with electrons gives +1 quantum per level from the next level.
    const string ShellLessonKey = "Pendulum_ShellLessonSeen";
    const string NobleLessonKey = "Pendulum_NobleLessonSeen";
    // The first photon, electron and quantum ever collected each get a lesson; until then, a
    // trail of chevrons around the atom points at the nearest one (GameUI.Guide).
    const string PhotonLessonKey = "Pendulum_PhotonLessonSeen";
    const string ElectronLessonKey = "Pendulum_ElectronLessonSeen";
    const string QuantumLessonKey = "Pendulum_QuantumLessonSeen";

    /// <summary>Dev: the saved "already shown" flags of the lessons.</summary>
    public static readonly string[] TutorialKeys = { ShellLessonKey, NobleLessonKey, HalogenLessonKey, AlkaliLessonKey,
                                                     PhotonLessonKey, ElectronLessonKey, QuantumLessonKey };
    public static string StoryKey => StorySeenKey;

    const string HalogenLessonKey = "Pendulum_HalogenLessonSeen";
    const string AlkaliLessonKey = "Pendulum_AlkaliLessonSeen";

    /// <summary>The one-time lessons (the game freezes and the lines fade in one by one).</summary>
    public enum LessonKind { Shell, Noble, Halogen, Alkali, Photon, Electron, Quantum }
    public LessonKind TeachingKind { get; private set; }
    /// <summary>Goes up with every lesson shown (the UI rebuilds its lines when it changes).</summary>
    public int LessonVersion { get; private set; }
    readonly Queue<LessonKind> lessonQueue = new Queue<LessonKind>();
    int teachEndFrame = -1;
    int shellsFilled;              // this run
    int pendingNewQuanta;          // earned this level, placed from the next one
    int levelQuanta, newQuanta;    // this level
    bool teaching;
    float teachTime;

    // Neutrinos: earned by finishing a level without losing stability; each one opens a
    // locked petal for a single pick (a 4th, 5th or 6th upgrade). They stack without limit.
    int neutrinos;
    bool hurtThisLevel;            // stability was lost this level: no neutrino
    bool levelFlawless;            // the level just completed earned a neutrino
    bool upgradeFinishing;         // last pick made: absorbing it, then the core flies home
    int lastPickIndex = -1;
    UpgradeType lastPicked;
    int lastPickVersion;     // bumped on every pick (the UI shows what was taken)
    int mysteryIndex = -1;   // index in 'offer' of the mystery-box card, or -1

    public int MaxHealth => startingLives + upgMaxHealth + Lab.Level(LabPerk.Stability);
    /// <summary>
    /// Quanta per level from the next level on: the base, +1 for every energy level the player
    /// has filled with electrons this run. The Lonely Ion instead gets 2, +1 every 5 levels.
    /// </summary>
    public int CoinsPerLevel => character == Character.Challenger ? IonQuanta(level)
        : coinsPerLevel + shellsFilled * coinsPerExtraShell;
    int IonQuanta(int l) => ionBaseQuanta + (Lab.Has(LabPerk.IonQuanta) ? 1 : 0)   // Lab: Surplus Charge
                            + Mathf.Max(0, l - 1) / Mathf.Max(1, ionLevelsPerQuantum);
    /// <summary>Pickup chance multiplier (the Lonely Ion finds twice as many neutrinos and stability points).</summary>
    float FindMultiplier => character == Character.Challenger ? ionFindMultiplier : 1f;
    /// <summary>Neutrinos earned by the level just completed (flawless and/or the Lonely Ion's free one).</summary>
    public int LevelNeutrinos { get; private set; }
    public bool IsLonelyIon => character == Character.Challenger;
    /// <summary>Quanta placed in the current level (CoinsPerLevel when it started).</summary>
    public int LevelQuanta => levelQuanta;
    /// <summary>How many of this level's quanta are new (just earned by filling an energy level).</summary>
    public int NewQuantaThisLevel => newQuanta;
    /// <summary>Energy levels (shells) filled with electrons so far this run.</summary>
    public int ShellsFilled => shellsFilled;
    /// <summary>The game is frozen on the "energy level complete" lesson (first time ever).</summary>
    public bool Teaching => teaching;
    public float TeachTime => teachTime;
    int BallArmorCap => character == Character.Sluggish ? sluggishMaxBallArmor : maxBallArmorUpgrades;
    /// <summary>Ball Armor purchases banked so far (one per N levels reached) that haven't been spent.</summary>
    int BallArmorBanked => Mathf.Min(BallArmorCap, level / Mathf.Max(1, levelsPerBallArmor)) - upgBallArmor;
    public static UpgradeTier TierOf(UpgradeType u)
    {
        switch (u)
        {
            case UpgradeType.Challenger:
            case UpgradeType.Sluggish:
            case UpgradeType.Spaceship:
            case UpgradeType.Conductive:
            case UpgradeType.Halogen:
            case UpgradeType.Alkali:
            case UpgradeType.Excited: return UpgradeTier.Character;
            case UpgradeType.Photoelectric:
            case UpgradeType.Resonance: return UpgradeTier.Rare;
            case UpgradeType.Regeneration:
            case UpgradeType.Insight:
            case UpgradeType.CoinRecycler:
            case UpgradeType.HealthRecycler: return UpgradeTier.Rare;
            default: return UpgradeTier.Normal;
        }
    }
    public static bool IsCharacter(UpgradeType u) => TierOf(u) == UpgradeTier.Character;
    public static bool IsRare(UpgradeType u) => TierOf(u) == UpgradeTier.Rare;
    /// <summary>The Character card that gives this Character.</summary>
    public static UpgradeType CharacterCard(Character c) =>
        c == Character.Challenger ? UpgradeType.Challenger
        : c == Character.Sluggish ? UpgradeType.Sluggish
        : c == Character.Conductive ? UpgradeType.Conductive
        : c == Character.Halogen ? UpgradeType.Halogen
        : c == Character.Alkali ? UpgradeType.Alkali
        : c == Character.Excited ? UpgradeType.Excited : UpgradeType.Spaceship;

    // ------------------------------------------------------------- orbitals

    /// <summary>Electron slots per shell for this many New Orbitals (start: 2, each orbital +2).</summary>
    public static int[] ShellLayoutFor(int orbitals)
    {
        var caps = new List<int>();
        int slots = 2 + 2 * Mathf.Max(0, orbitals);
        for (int s = 0; slots > 0; s++)
        {
            int c = Mathf.Min(Elements.ShellSize(s), slots);
            caps.Add(c);
            slots -= c;
        }
        return caps.ToArray();
    }

    public static int ShellsFor(int orbitals) => ShellLayoutFor(orbitals).Length;

    /// <summary>Shells completely filled with slots (the first one always is).</summary>
    public static int FullShellsFor(int orbitals)
    {
        int[] caps = ShellLayoutFor(orbitals);
        int full = 0;
        for (int s = 0; s < caps.Length; s++) if (caps[s] >= Elements.ShellSize(s)) full++;
        return full;
    }



    float[] ShellLengthsFor(int count)
    {
        var lens = new float[count];
        for (int i = 0; i < count; i++) lens[i] = firstShellLength + i * shellLengthStep;
        return lens;
    }

    /// <summary>
    /// Challenger: one extra pick per level beaten (upgrades happen right after beating 'level'),
    /// plus one per ball upgrade bought (they are all taken away).
    /// </summary>
    int ChallengerRefunds => Mathf.Max(0, level) + upgBallArmor + upgBallSize + upgOrbitals;

    // ------------------------------------------------------- read-only state for the UI

    public int Lives => lives;
    public int Neutrinos => neutrinos;
    /// <summary>The level just completed was finished without losing stability (it earned a neutrino).</summary>
    public bool LevelFlawless => levelFlawless;
    public bool HasInsight => upgInsight > 0;
    public bool HasCoinRecycler => upgCoinRecycler > 0;
    public bool HasHealthRecycler => upgHealthRecycler > 0;
    public int CoinRecycleProgress => coinRecycleProgress;
    public int HeartRecycleProgress => heartRecycleProgress;
    public int OrbsPerRecycledCoin => Mathf.Max(1, orbsPerRecycledCoin);
    public int OrbsPerRecycledHeart => Mathf.Max(1, orbsPerRecycledHeart);
    /// <summary>Can an orb be picked up while the orbitals are full (it gets recycled)?</summary>
    public bool CanRecycleOrb => HasCoinRecycler || (HasHealthRecycler && lives < MaxHealth);
    public int StarsCollected => starsCollected;
    public int StarsNeeded => starsNeeded;
    public int LevelCoins => levelCoins;
    public int BestLevel => bestLevel;
    public float StateTime => stateTime;
    public float LevelTime => levelTime;
    public Character ActiveCharacter => character;
    public PendulumController Pendulum => pendulum;
    public IReadOnlyList<UpgradeType> Offer => offer;
    /// <summary>Changes every time a new set of upgrade cards is rolled.</summary>
    public int OfferVersion => offerVersion;
    public int MysteryIndex => mysteryIndex;
    public float MysteryRevealTime => mysteryRevealTime;
    public int ChoiceIndex => choiceIndex;
    /// <summary>Picks paid for with coins this round (refund picks come after these).</summary>
    public int CoinChoices => choiceTotal;
    public int RefundTotal => refundTotal;
    public int NextLevelStars => StarsForLevel(level + 1);

    // Upgrade screen timing: each quantum of a round plays faster than the one before.
    /// <summary>1 for the first quantum of a round, then faster (smaller) for each further one.</summary>
    public float PickPace => Mathf.Max(upgradeMinPace, Mathf.Pow(upgradeSpeedup, pickNumber));
    public float PickFlyTime => upgradeFlyTime * PickPace;
    public int PickNumber => pickNumber;
    /// <summary>Picked upgrade flying into the core (after every pick, scaled by the pace).</summary>
    public float AbsorbTime => upgradeAbsorbTime * PickPace;
    public float CoreFlyTime => upgradeCoreFlyTime;
    public float CoreReturnTime => upgradeCoreReturnTime;
    /// <summary>State time at which the quantum starts flying in: after the core arrives (first pick) or after the last pick was absorbed.</summary>
    public float PickStartTime => pickNumber == 0 ? upgradeCoreFlyTime : AbsorbTime;
    /// <summary>The last pick of the round was made: it is being absorbed, then the core flies back to the map.</summary>
    public bool UpgradeFinishing => upgradeFinishing;
    public int LastPickIndex => lastPickIndex;
    /// <summary>State time at which the atom opens and its upgrades can be picked.</summary>
    public float OfferOpenTime => PickStartTime + (upgradeFlyTime + upgradeSpinTime) * PickPace;
    /// <summary>State time at which a Character box (if any) bursts open.</summary>
    public float MysteryOpenTime => OfferOpenTime + Mathf.Max(0.5f, mysteryRevealTime) * PickPace;
    public UpgradeType LastPicked => lastPicked;
    public int LastPickVersion => lastPickVersion;

    PendulumController pendulum;
    EnemySpawner spawner;
    Music music;
    Camera cam;
    float camZ = -10f;
    Vector2 viewCenter, viewVel;

    Transform background;
    SpriteRenderer grid;
    Transform[] dust;
    Vector2[] dustVel;


    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        Time.timeScale = 1f;

        pendulum = PendulumController.Instance != null ? PendulumController.Instance : FindFirstObjectByType<PendulumController>();
        if (pendulum == null) pendulum = new GameObject("Pendulum").AddComponent<PendulumController>();

        spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner == null) spawner = new GameObject("EnemySpawner").AddComponent<EnemySpawner>();
        if (spawner.pendulumController == null) spawner.pendulumController = pendulum;

        cam = Camera.main;
        viewCenter = pendulum.PivotPosition;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = cameraSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundEdge;
            camZ = cam.transform.position.z;
            cam.transform.position = new Vector3(viewCenter.x, viewCenter.y, camZ);
        }

        BuildBackground();
        Sfx.Init(gameObject);
        if (GetComponent<GameUI>() == null) gameObject.AddComponent<GameUI>();
        ClearOldSave();
        bestLevel = Mathf.Max(1, PlayerPrefs.GetInt(BestKey, 1));
        ResetUpgrades();
        lives = MaxHealth;

        SetState(State.Title);
        StartMusic();
    }

    void StartMusic()
    {
        if (music == null) music = Music.Create(transform);
    }

    // ---------------------------------------------------------------- story

    /// <summary>Replay the story from the title screen (the music fades out while it plays).</summary>
    public void PlayStory()
    {
        if (state != State.Title || OverlayOpen) return;
        runAfterStory = false;
        SetState(State.Story);
    }

    bool runAfterStory;   // the story was opened by the first PLAY: start the run when it ends

    /// <summary>Called by GameUI when the story ends or is skipped: into the first run, or back to the title.</summary>
    public void EndStory()
    {
        if (state != State.Story) return;
        PlayerPrefs.SetInt(StorySeenKey, 1);
        PlayerPrefs.Save();
        StartMusic();
        if (runAfterStory)
        {
            runAfterStory = false;
            BeginRun();
        }
        else SetState(State.Title);
    }

    // ------------------------------------------------------------- upgrades

    /// <summary>Runs are no longer saved: remove what older versions stored.</summary>
    static void ClearOldSave()
    {
        string[] keys = { "MaxHealth", "Speed", "Thrust", "Torque", "BallArmor", "GameChanger", "BallSize",
            "PickupRange", "Repel", "Booster", "Regeneration", "FireRate", "TurretRange", "Insight",
            "CoinRecycler", "HealthRecycler", "CoinRecycleProgress", "HeartRecycleProgress" };
        PlayerPrefs.DeleteKey(LevelKey);
        foreach (var k in keys) PlayerPrefs.DeleteKey(UpgradeKeyPrefix + k);
        PlayerPrefs.Save();
    }

    void SetBest(int reached)
    {
        if (reached <= bestLevel) return;
        bestLevel = reached;
        PlayerPrefs.SetInt(BestKey, bestLevel);
        PlayerPrefs.Save();
    }

    void ResetUpgrades()
    {
        neutrinos = Lab.Level(LabPerk.StartNeutrino);   // Lab: start with neutrinos
        runMaxElectrons = 0;
        runPaid = 0;
        secondWindUsed = false;
        shellsFilled = pendingNewQuanta = newQuanta = 0;
        upgMaxHealth = upgSpeed = upgTorque = upgOrbitals = upgBallArmor = 0;
        upgBallSize = upgPickupRange = upgRepel = upgBooster = upgRegen = 0;
        upgFireRate = upgTurretRange = 0;
        upgInsight = upgCoinRecycler = upgHealthRecycler = 0;
        upgSpin = upgGroundState = upgPhotoelectric = upgResonance = 0;
        coinRecycleProgress = heartRecycleProgress = 0;
        rarePity = characterPity = 0;
        ResetTraits();
        character = Character.None;
        upgOrbitals = Lab.Level(LabPerk.StartOrbital);   // Lab: start with New Orbitals
        ApplyUpgrades();
    }

    void ApplyUpgrades()
    {
        if (pendulum == null) return;

        // New Orbitals grow the shells; Characters reshape them.
        bool allSides = character == Character.Spaceship && Lab.Has(LabPerk.RadiantSides);
        int layoutKey = (int)character + 16 * upgOrbitals + (allSides ? 1 << 20 : 0) + (upgGroundState << 24);
        if (appliedLayout != layoutKey)
        {
            appliedLayout = layoutKey;
            int[] caps = ShellLayoutFor(upgOrbitals);
            float[] lens = ShellLengthsFor(caps.Length);
            // Ground State: every energy level sits closer to the core.
            float closer = Mathf.Max(0.3f, 1f - upgGroundState * groundStatePerUpgrade);
            for (int i = 0; i < lens.Length; i++) lens[i] *= closer;
            // Radiant: the inner orbital's slots become emitters (its balls convert first).
            // Lab Decay on All Sides: that first energy level holds 4 of them instead of 2.
            int emitters = character == Character.Spaceship && caps.Length > 0 ? (allSides ? 4 : caps[0]) : 0;
            pendulum.SetTurretSlots(emitters);
            if (character == Character.Spaceship && caps.Length > 0)
            {
                int n = caps.Length - 1;
                var newCaps = new int[n];
                var newLens = new float[n];
                System.Array.Copy(caps, 1, newCaps, 0, n);
                System.Array.Copy(lens, 1, newLens, 0, Mathf.Min(n, lens.Length - 1));
                pendulum.SetShellLayout(newCaps, newLens);
            }
            else if (character == Character.Challenger)
            {
                pendulum.SetShellLayout(new int[0], new float[0]);          // no balls at all
            }
            else
            {
                pendulum.SetShellLayout(caps, lens);
            }
        }

        pendulum.ApplyUpgrades(
            1f + upgSpeed * speedPerUpgrade,
            1f + upgSpeed * thrustPerUpgrade,
            upgTorque * torquePerUpgrade,
            upgTorque * turnAuthorityPerTorque,
            character == Character.Challenger || character == Character.Conductive ? 0 : upgBallArmor);
        pendulum.ApplyExtraUpgrades(
            1f + upgBallSize * ballSizePerUpgrade,
            upgPickupRange * pickupRangePerUpgrade,
            upgRepel,
            upgBooster);
        pendulum.SetSpinMultiplier(1f + upgSpin * spinPerUpgrade);   // Angular Momentum
        pendulum.ApplyTurretUpgrades(
            1f + upgFireRate * fireRatePerUpgrade,
            upgTurretRange * turretRangePerUpgrade,
            Lab.Has(LabPerk.RadiantWaves));   // Lab Decay Waves
    }

    void BeginUpgradeChoices()
    {
        pendingChoices = levelCoins;
        choiceTotal = levelCoins;
        refundTotal = 0;
        choiceIndex = 0;
        pickNumber = 0;
        upgradeFinishing = false;
        levelCoins = 0;
        RollOffer();
        Time.timeScale = 0f;
        SetState(State.Upgrade);
    }

    /// <summary>
    /// Normal upgrades come in two groups (not shown differently on the cards):
    /// Essentials (repair, max health, speed, torque, new orbital) and Extras (everything else).
    /// </summary>
    public static bool IsEssential(UpgradeType u) =>
        u == UpgradeType.Heal || u == UpgradeType.MaxHealth || u == UpgradeType.Speed
        || u == UpgradeType.Torque || u == UpgradeType.Orbital;
    public static bool IsExtra(UpgradeType u) => TierOf(u) == UpgradeTier.Normal && !IsEssential(u);


    /// <summary>Every normal upgrade that may be offered right now: Essentials and Extras.</summary>
    void OfferCandidates(out List<UpgradeType> essentials, out List<UpgradeType> extras)
    {
        essentials = new List<UpgradeType> { UpgradeType.MaxHealth };
        essentials.Add(UpgradeType.Speed);
        essentials.Add(UpgradeType.Torque);
        // New Orbital: never for the ion (it holds no electrons).
        if (character != Character.Challenger) essentials.Add(UpgradeType.Orbital);
        if (lives <= MaxHealth - 2) essentials.Add(UpgradeType.Heal);   // only when down at least two hearts

        // Extras.
        extras = new List<UpgradeType>();
        // Ball Armor: only if one is banked (one per N levels), under the cap, and balls exist at all.
        if (character != Character.Challenger && character != Character.Conductive && BallArmorBanked > 0) extras.Add(UpgradeType.BallArmor);
        if (character != Character.Challenger && upgBallSize < maxBallSizeUpgrades) extras.Add(UpgradeType.BallSize);
        if (upgPickupRange < maxPickupRangeUpgrades) extras.Add(UpgradeType.PickupRange);
        if (upgRepel < maxRepelUpgrades) extras.Add(UpgradeType.Repel);
        if (upgBooster < maxBoosterUpgrades) extras.Add(UpgradeType.Booster);
        if (character != Character.Challenger && upgSpin < maxSpinUpgrades) extras.Add(UpgradeType.AngularMomentum);
        if (character != Character.Challenger && upgGroundState < maxGroundStateUpgrades) extras.Add(UpgradeType.GroundState);
        if (character == Character.Spaceship)
        {
            if (upgFireRate < maxFireRateUpgrades) extras.Add(UpgradeType.FireRate);
            if (upgTurretRange < maxTurretRangeUpgrades) extras.Add(UpgradeType.TurretRange);
        }
        if (character == Character.Conductive)
        {
            if (upgHopRange < maxConductUpgrades) extras.Add(UpgradeType.HopRange);
            if (upgHopCount < maxConductUpgrades) extras.Add(UpgradeType.HopCount);
            if (upgChargeRate < maxConductUpgrades) extras.Add(UpgradeType.ChargeRate);
        }
        if (character == Character.Halogen)
        {
            if (upgHalogenPull < maxHalogenUpgrades) extras.Add(UpgradeType.HalogenPull);
            if (upgHalogenGrasp < maxHalogenUpgrades) extras.Add(UpgradeType.HalogenGrasp);
            if (upgHalogenSnatch < maxHalogenUpgrades) extras.Add(UpgradeType.HalogenSnatch);
        }
        if (character == Character.Alkali)
        {
            if (upgAlkaliBlast < maxAlkaliUpgrades) extras.Add(UpgradeType.AlkaliBlast);
            if (upgAlkaliRange < maxAlkaliUpgrades) extras.Add(UpgradeType.AlkaliRange);
            if (upgAlkaliPath < maxAlkaliUpgrades) extras.Add(UpgradeType.AlkaliPath);
            if (upgAlkaliReload < maxAlkaliUpgrades) extras.Add(UpgradeType.AlkaliReload);
        }
    }

    void RollOffer()
    {
        offerVersion++;
        OfferCandidates(out var essentials, out var extras);

        offer.Clear();
        // At least one Essential, always.
        if (essentials.Count > 0)
        {
            int k = Random.Range(0, essentials.Count);
            offer.Add(essentials[k]);
            essentials.RemoveAt(k);
        }
        // Fill the rest from both groups (what was just picked is not held back).
        while (offer.Count < upgradeChoices)
        {
            int total = essentials.Count + extras.Count;
            if (total == 0) break;
            int k = Random.Range(0, total);
            if (k < essentials.Count) { offer.Add(essentials[k]); essentials.RemoveAt(k); }
            else { k -= essentials.Count; offer.Add(extras[k]); extras.RemoveAt(k); }
        }
        // Shuffle so the guaranteed Essential isn't always the first card.
        for (int i = offer.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            UpgradeType t = offer[i]; offer[i] = offer[j]; offer[j] = t;
        }

        // Character mystery box: any pick, from the unlock level, until a character is taken.
        // Lab Probability Amplitude: every roll that misses adds +1% until one shows up (then back to base).
        mysteryIndex = -1;
        bool balance = Lab.Has(LabPerk.ProbabilityAmplitude);
        bool eligible = character == Character.None && level >= characterUnlockLevel;
        if (eligible && Random.value < characterChance + Lab.CharacterChancePerLevel * Lab.Level(LabPerk.CharacterChance)
                                       + Lab.AmplitudePerMiss * characterPity)
        {
            UpgradeType[] characters = { UpgradeType.Challenger, UpgradeType.Sluggish, UpgradeType.Spaceship, UpgradeType.Conductive,
                                     UpgradeType.Halogen, UpgradeType.Alkali, UpgradeType.Excited };
            UpgradeType gc = characters[Random.Range(0, characters.Length)];
            if (offer.Count >= upgradeChoices)
            {
                int slot = ReplaceableSlot(-1);
                if (slot >= 0) { mysteryIndex = slot; offer[slot] = gc; }
            }
            else { offer.Add(gc); mysteryIndex = offer.Count - 1; }
        }
        if (eligible) characterPity = mysteryIndex >= 0 ? 0 : balance ? characterPity + 1 : 0;

        // Rare card: replaces one normal card (never the mystery box). Each rare is once per run.
        var rares = new List<UpgradeType>();
        if (upgRegen == 0) rares.Add(UpgradeType.Regeneration);
        if (upgInsight == 0) rares.Add(UpgradeType.Insight);
        if (upgCoinRecycler == 0) rares.Add(UpgradeType.CoinRecycler);
        if (upgHealthRecycler == 0) rares.Add(UpgradeType.HealthRecycler);
        if (upgPhotoelectric == 0 && character != Character.Challenger) rares.Add(UpgradeType.Photoelectric);
        if (upgResonance == 0 && character != Character.Challenger) rares.Add(UpgradeType.Resonance);
        bool rareRoll = rares.Count > 0 && level >= rareUnlockLevel;
        bool rareFound = false;
        if (rareRoll && Random.value < rareChance + Lab.RareChancePerLevel * Lab.Level(LabPerk.RareChance)
                                       + Lab.AmplitudePerMiss * rarePity)
        {
            UpgradeType rare = rares[Random.Range(0, rares.Count)];
            if (offer.Count < upgradeChoices) { offer.Add(rare); rareFound = true; }
            else
            {
                int slot = ReplaceableSlot(mysteryIndex);
                if (slot >= 0) { offer[slot] = rare; rareFound = true; }
            }
        }
        if (rareRoll) rarePity = rareFound ? 0 : balance ? rarePity + 1 : 0;

        for (int i = 0; i < offer.Count; i++)
            if (i != mysteryIndex) Codex.See(Codex.Upgrade(offer[i]));
    }

    /// <summary>
    /// A random card that a special card (Character box, Rare) may replace: never 'except',
    /// and never the offer's last Essential (every pick keeps at least one). -1 if none.
    /// </summary>
    int ReplaceableSlot(int except)
    {
        int essentialCount = 0;
        foreach (var u in offer) if (IsEssential(u)) essentialCount++;
        var slots = new List<int>();
        for (int i = 0; i < offer.Count; i++)
        {
            if (i == except) continue;
            if (IsEssential(offer[i]) && essentialCount <= 1) continue;
            if (!IsEssential(offer[i]) && TierOf(offer[i]) != UpgradeTier.Normal) continue;
            slots.Add(i);
        }
        return slots.Count > 0 ? slots[Random.Range(0, slots.Count)] : -1;
    }

    /// <summary>
    /// Spends a neutrino: one more upgrade joins the current offer, for this pick only (the
    /// next pick rolls a fresh offer with the petals locked again). Returns its offer index,
    /// or -1 if no neutrino can be spent now (none left, petals not open, nothing left to offer).
    /// </summary>
    public int SpendNeutrino()
    {
        if (state != State.Upgrade || upgradeFinishing || neutrinos <= 0 || stateTime < OfferOpenTime) return -1;
        if (offer.Count >= 6) return -1;
        OfferCandidates(out var essentials, out var extras);
        var pool = new List<UpgradeType>();
        foreach (var u in essentials) if (!offer.Contains(u)) pool.Add(u);
        foreach (var u in extras) if (!offer.Contains(u)) pool.Add(u);
        if (pool.Count == 0) return -1;
        UpgradeType pick = pool[Random.Range(0, pool.Count)];
        offer.Add(pick);
        neutrinos--;
        Codex.See(Codex.Upgrade(pick));
        return offer.Count - 1;
    }

    /// <summary>Picks card 'index' of the current offer (ignored while it can't be picked yet).</summary>
    public void ChooseUpgrade(int index)
    {
        if (state != State.Upgrade || upgradeFinishing || index < 0 || index >= offer.Count || stateTime < OfferOpenTime + 0.15f) return;
        if (index == mysteryIndex && stateTime < MysteryOpenTime) return;   // still sealed

        UpgradeType picked = offer[index];
        const int cost = 1;
        switch (picked)
        {
            case UpgradeType.Heal: lives = MaxHealth; break;
            case UpgradeType.MaxHealth: upgMaxHealth++; lives++; break;
            case UpgradeType.Speed: upgSpeed++; break;
            case UpgradeType.Torque: upgTorque++; break;
            case UpgradeType.Orbital: upgOrbitals++; break;
            case UpgradeType.BallArmor: upgBallArmor = Mathf.Min(BallArmorCap, upgBallArmor + 1); break;
            case UpgradeType.BallSize: upgBallSize = Mathf.Min(maxBallSizeUpgrades, upgBallSize + 1); break;
            case UpgradeType.PickupRange: upgPickupRange = Mathf.Min(maxPickupRangeUpgrades, upgPickupRange + 1); break;
            case UpgradeType.Repel: upgRepel = Mathf.Min(maxRepelUpgrades, upgRepel + 1); break;
            case UpgradeType.Booster: upgBooster = Mathf.Min(maxBoosterUpgrades, upgBooster + 1); break;
            case UpgradeType.Regeneration: upgRegen = 1; break;
            case UpgradeType.Insight: upgInsight = 1; break;
            case UpgradeType.CoinRecycler: upgCoinRecycler = 1; break;
            case UpgradeType.HealthRecycler: upgHealthRecycler = 1; break;
            case UpgradeType.AngularMomentum: upgSpin = Mathf.Min(maxSpinUpgrades, upgSpin + 1); break;
            case UpgradeType.GroundState: upgGroundState = Mathf.Min(maxGroundStateUpgrades, upgGroundState + 1); break;
            case UpgradeType.Photoelectric: upgPhotoelectric = 1; break;
            case UpgradeType.Resonance: upgResonance = 1; break;
            case UpgradeType.FireRate: upgFireRate = Mathf.Min(maxFireRateUpgrades, upgFireRate + 1); break;
            case UpgradeType.TurretRange: upgTurretRange = Mathf.Min(maxTurretRangeUpgrades, upgTurretRange + 1); break;
            case UpgradeType.Spaceship: character = Character.Spaceship; break;
            case UpgradeType.Challenger:
            {
                // One pick per level beaten so far, plus every ball / orbital upgrade, which is lost.
                int refunds = ChallengerRefunds;
                character = Character.Challenger;
                upgBallArmor = 0;
                upgBallSize = 0;
                upgOrbitals = 0;
                pendingChoices += refunds;
                refundTotal += refunds;
                break;
            }
            case UpgradeType.Sluggish: character = Character.Sluggish; break;
            case UpgradeType.Conductive:
            {
                // No Electron Binding for the Conductive: any bought is refunded as picks.
                int refunds = upgBallArmor;
                character = Character.Conductive;
                upgBallArmor = 0;
                pendingChoices += refunds;
                refundTotal += refunds;
                ResetConductivity();
                break;
            }
            case UpgradeType.HopRange: upgHopRange = Mathf.Min(maxConductUpgrades, upgHopRange + 1); break;
            case UpgradeType.HopCount: upgHopCount = Mathf.Min(maxConductUpgrades, upgHopCount + 1); break;
            case UpgradeType.ChargeRate: upgChargeRate = Mathf.Min(maxConductUpgrades, upgChargeRate + 1); break;
            case UpgradeType.Halogen: character = Character.Halogen; break;
            case UpgradeType.HalogenPull: upgHalogenPull = Mathf.Min(maxHalogenUpgrades, upgHalogenPull + 1); break;
            case UpgradeType.HalogenGrasp: upgHalogenGrasp = Mathf.Min(maxHalogenUpgrades, upgHalogenGrasp + 1); break;
            case UpgradeType.HalogenSnatch: upgHalogenSnatch = Mathf.Min(maxHalogenUpgrades, upgHalogenSnatch + 1); break;
            case UpgradeType.Alkali: character = Character.Alkali; break;
            case UpgradeType.Excited: character = Character.Excited; break;
            case UpgradeType.AlkaliBlast: upgAlkaliBlast = Mathf.Min(maxAlkaliUpgrades, upgAlkaliBlast + 1); break;
            case UpgradeType.AlkaliRange: upgAlkaliRange = Mathf.Min(maxAlkaliUpgrades, upgAlkaliRange + 1); break;
            case UpgradeType.AlkaliPath: upgAlkaliPath = Mathf.Min(maxAlkaliUpgrades, upgAlkaliPath + 1); break;
            case UpgradeType.AlkaliReload: upgAlkaliReload = Mathf.Min(maxAlkaliUpgrades, upgAlkaliReload + 1); break;
        }
        ApplyUpgrades();
        Sfx.Play(IsCharacter(picked) ? "level" : "pick");
        lastPicked = picked;
        lastPickIndex = index;
        lastPickVersion++;

        pendingChoices -= cost;
        choiceIndex += cost;
        pickNumber++;
        if (pendingChoices <= 0) upgradeFinishing = true;   // absorb it, fly home, then the next level
        else RollOffer();
        SetState(State.Upgrade);   // restarts the timeline for the next quantum (or the finish)
    }

    public static string UpgradeTitle(UpgradeType u)
    {
        switch (u)
        {
            case UpgradeType.Heal: return Loc.T("FULL STABILITY", "TAM KARARLILIK");
            case UpgradeType.MaxHealth: return Loc.T("MAX STABILITY", "AZAMİ KARARLILIK");
            case UpgradeType.Speed: return Loc.T("SPEED", "HIZ");
            case UpgradeType.Torque: return Loc.T("TORQUE", "TORK");
            case UpgradeType.Orbital: return Loc.T("NEW ORBITAL", "YENİ ORBİTAL");
            case UpgradeType.Challenger: return Loc.T("LONELY ION", "YALNIZ İYON");
            case UpgradeType.Sluggish: return Loc.T("NOBLE ATOM", "SOY ATOM");
            case UpgradeType.Spaceship: return Loc.T("RADIANT", "IŞIYAN");
            case UpgradeType.Conductive: return Loc.T("CONDUCTIVE", "İLETKEN");
            case UpgradeType.HopRange: return Loc.T("ZAP RANGE", "ŞOK MENZİLİ");
            case UpgradeType.HopCount: return Loc.T("ZAP HOPS", "ŞOK SEKMESİ");
            case UpgradeType.ChargeRate: return Loc.T("FAST CHARGE", "HIZLI YÜKLEME");
            case UpgradeType.Halogen: return Loc.T("HALOGEN", "HALOJEN");
            case UpgradeType.HalogenPull: return Loc.T("STRONGER PULL", "GÜÇLÜ ÇEKİM");
            case UpgradeType.HalogenGrasp: return Loc.T("WIDER GRASP", "GENİŞ KAVRAYIŞ");
            case UpgradeType.HalogenSnatch: return Loc.T("RADICAL SNATCH", "RADİKAL KAPMA");
            case UpgradeType.Alkali: return Loc.T("ALKALI METAL", "ALKALİ METAL");
            case UpgradeType.Excited: return Loc.T("EXCITED ATOM", "UYARILMIŞ ATOM");
            case UpgradeType.AngularMomentum: return Loc.T("ANGULAR MOMENTUM", "AÇISAL MOMENTUM");
            case UpgradeType.GroundState: return Loc.T("GROUND STATE", "TEMEL HAL");
            case UpgradeType.Photoelectric: return Loc.T("PHOTOELECTRIC EFFECT", "FOTOELEKTRİK ETKİ");
            case UpgradeType.Resonance: return Loc.T("RESONANCE", "REZONANS");
            case UpgradeType.AlkaliBlast: return Loc.T("BIGGER BLAST", "BÜYÜK PATLAMA");
            case UpgradeType.AlkaliRange: return Loc.T("LONGER THROW", "UZUN ATIŞ");
            case UpgradeType.AlkaliPath: return Loc.T("WIDER PATH", "GENİŞ YOL");
            case UpgradeType.AlkaliReload: return Loc.T("QUICK RECOVERY", "HIZLI TOPARLANMA");
            case UpgradeType.BallSize: return Loc.T("BIGGER ELECTRONS", "BÜYÜK ELEKTRONLAR");
            case UpgradeType.PickupRange: return Loc.T("PICKUP RANGE", "TOPLAMA MENZİLİ");
            case UpgradeType.Repel: return Loc.T("REPEL", "İTME");
            case UpgradeType.Booster: return Loc.T("BOOSTER", "HIZLANDIRICI");
            case UpgradeType.Regeneration: return Loc.T("REGENERATION", "YENİLENME");
            case UpgradeType.Insight: return Loc.T("INSIGHT", "ÖNGÖRÜ");
            case UpgradeType.CoinRecycler: return Loc.T("QUANTA RECYCLER", "KUANTA DÖNÜŞTÜRÜCÜ");
            case UpgradeType.HealthRecycler: return Loc.T("STABILITY RECYCLER", "KARARLILIK DÖNÜŞTÜRÜCÜ");
            case UpgradeType.FireRate: return Loc.T("DECAY RATE", "BOZUNMA HIZI");
            case UpgradeType.TurretRange: return Loc.T("RADIATION RANGE", "RADYASYON MENZİLİ");
            default: return Loc.T("ELECTRON BINDING", "ELEKTRON BAĞI");
        }
    }

    string NobleText(bool full) =>
        Loc.T("Full shell (2, 10, 18, 36\u2026):\nunbreakable bonds\nElectron Binding up to " + sluggishMaxBallArmor,
              "Tam kabuk (2, 10, 18, 36\u2026):\nkırılmaz bağlar\nElektron Bağı en fazla " + sluggishMaxBallArmor)
        + (full ? Loc.T("\nIonizing hits and dust can't take electrons; positrons and black holes still can",
                        "\nİyonlaştırıcı darbeler ve toz elektron alamaz; pozitronlar ve kara delikler hâlâ alır") : "")
        + (Lab.Has(LabPerk.NobleLinger) ? Loc.T("\nLingers " + nobleLingerTime.ToString("0") + " s", "\n" + nobleLingerTime.ToString("0") + " sn sürer") : "")
        + (Lab.Has(LabPerk.NobleProclaim) ? Loc.T("\nProclamation wave", "\nİlan dalgası") : "");

    string ExcitedText() =>
        Loc.T(excitedOrbMultiplier + "\u00d7 electrons per level", "Seviye başına " + excitedOrbMultiplier + "\u00d7 elektron")
        + (Lab.ExcitedAbility == LabPerk.NobleDash ? Loc.T("\nAbility: Electrodash", "\nYetenek: Elektro Atılım")
           : Lab.ExcitedAbility == LabPerk.NobleBlast ? Loc.T("\nAbility: Electroblast", "\nYetenek: Elektro Patlama")
           : Loc.T("\nAbilities: research in the Lab", "\nYetenekler: Laboratuvarda araştır"));

    /// <summary>The Lonely Ion's text; the card keeps it short, the Index lists everything.</summary>
    string HydrogenText(string last, bool full) =>
        Loc.T("No electrons, forever\n" + ionBaseQuanta + " quanta/level, +1 per " + ionLevelsPerQuantum + " levels\n+1 neutrino every level\n",
              "Elektron yok, kalıcı olarak\nSeviye başına " + ionBaseQuanta + " kuanta, her " + ionLevelsPerQuantum + " seviyede +1\nHer seviyede +1 nötrino\n")
        + (full ? Loc.T("2× neutrino & stability finds\nEnemies scale with level only\n",
                        "2× nötrino ve kararlılık bulma\nDüşmanlar yalnızca seviyeyle güçlenir\n") : "")
        + last;

    /// <summary>One or two short lines; levels are shown as pips on the card (see UpgradeLevels).</summary>
    public string UpgradeDescription(UpgradeType u)
    {
        switch (u)
        {
            case UpgradeType.Heal: return Loc.T("Stability ", "Kararlılık ") + lives + " \u2192 " + MaxHealth;
            case UpgradeType.MaxHealth: return Loc.T("+1 stability", "+1 kararlılık");
            case UpgradeType.Speed:
                return Loc.Pct(speedPerUpgrade) + Loc.T(" top speed\n", " azami hız\n")
                     + Loc.Pct(thrustPerUpgrade) + Loc.T(" acceleration", " ivme");
            case UpgradeType.Orbital:
                return Loc.T("+2 electron slots", "+2 elektron yuvası");
            case UpgradeType.Torque: return Loc.T("Sharper turns\nand braking", "Daha keskin dönüşler,\ndaha sert fren");
            case UpgradeType.Challenger:
                return HydrogenText(Loc.T("+" + ChallengerRefunds + " picks now", "Hemen +" + ChallengerRefunds + " seçim hakkı"), false);
            case UpgradeType.Sluggish: return NobleText(false);
            case UpgradeType.Excited: return ExcitedText();
            case UpgradeType.AngularMomentum:
                return Loc.Pct(spinPerUpgrade) + Loc.T(" orbit speed\nElectrons sweep harder", " yörünge hızı\nElektronlar daha sert süpürür");
            case UpgradeType.GroundState:
                return Loc.T("Every energy level\n" + Mathf.RoundToInt(groundStatePerUpgrade * 100f) + "% closer to the core",
                             "Tüm enerji seviyeleri\nçekirdeğe %" + Mathf.RoundToInt(groundStatePerUpgrade * 100f) + " daha yakın");
            case UpgradeType.Photoelectric:
                return Loc.T("Every photon knocks\n" + photoelectricElectrons + " electrons loose nearby",
                             "Her foton yakınına\n" + photoelectricElectrons + " elektron saçar");
            case UpgradeType.Resonance:
                return Loc.T("Completing an energy level\nfills your stability", "Bir enerji seviyesini\ntamamlamak kararlılığı doldurur");
            case UpgradeType.Spaceship:
            {
                int emitters = Lab.Has(LabPerk.RadiantSides) ? 4 : 2;
                return Loc.T("Inner shell becomes\n" + emitters + " radiation emitters\nthat shoot enemies",
                             "İç kabuk, düşmanlara\nateş eden " + emitters + " radyasyon\nkaynağına dönüşür");
            }
            case UpgradeType.BallSize: return Loc.Pct(ballSizePerUpgrade) + Loc.T(" electron size", " elektron boyutu");
            case UpgradeType.PickupRange: return Loc.T("Grab pickups\nfrom further away", "Parçacıkları daha\nuzaktan toplarsın");
            case UpgradeType.Repel:
                return upgRepel == 0 ? Loc.T("Getting hit blasts\nenemies & dust away", "Hasar aldığında düşmanları\nve tozları savurursun")
                                     : Loc.T("Bigger blast", "Daha büyük patlama");
            case UpgradeType.Booster:
                return upgBooster == 0 ? Loc.T("Getting hit gives\na burst of speed", "Hasar aldığında\nani bir hız kazanırsın")
                                       : Loc.T("Longer burst", "Hızlanma daha uzun sürer");
            case UpgradeType.Regeneration: return Loc.T("+1 stability\nevery level", "Her seviyede\n+1 kararlılık");
            case UpgradeType.Insight:
                return Loc.T("See dust grains and black\nholes before they arrive", "Toz tanelerini ve kara\ndelikleri gelmeden gör");
            case UpgradeType.CoinRecycler:
                return Loc.T("Spare electrons \u2192 quanta\n" + OrbsPerRecycledCoin + " electrons = 1 quantum",
                             "Fazla elektron \u2192 kuanta\n" + OrbsPerRecycledCoin + " elektron = 1 kuantum");
            case UpgradeType.HealthRecycler:
                return Loc.T("Spare electrons \u2192 stability\n" + OrbsPerRecycledHeart + " electrons = 1 stability",
                             "Fazla elektron \u2192 kararlılık\n" + OrbsPerRecycledHeart + " elektron = 1 kararlılık");
            case UpgradeType.FireRate: return Loc.Pct(fireRatePerUpgrade) + Loc.T(" emission rate", " yayım hızı");
            case UpgradeType.TurretRange:
                return "+" + turretRangePerUpgrade.ToString("0.#") + Loc.T(" radiation range", " radyasyon menzili");
            case UpgradeType.Conductive:
                return Loc.T("Builds charge over time\nHit electrons become zaps\nNo Electron Binding",
                             "Zamanla yük biriktirir\nVurulan elektron şoka dönüşür\nElektron Bağı yok")
                     + (upgBallArmor > 0 ? Loc.T("\n+" + upgBallArmor + " picks refunded", "\n+" + upgBallArmor + " seçim iadesi") : "");
            case UpgradeType.HopRange:
                return "+" + zapRangePerUpgrade.ToString("0.#") + Loc.T(" zap range", " şok menzili");
            case UpgradeType.HopCount: return Loc.T("+1 zap hop", "+1 şok sekmesi");
            case UpgradeType.ChargeRate:
                return "−" + conductTickPerUpgrade.ToString("0.#") + Loc.T(" s per charge", " sn yükleme süresi");
            case UpgradeType.Halogen:
                return Loc.T("At 9, 17, 35, 53\u2026 electrons:\nElectrons drift to you\n2\u00d7 electron reach\nTouching radicals lose theirs",
                             "9, 17, 35, 53\u2026 elektronda:\nElektronlar sana sürüklenir\n2\u00d7 elektron menzili\nDokunan radikal elektronunu kaybeder");
            case UpgradeType.HalogenPull: return Loc.Pct(halogenPullPerUpgrade) + Loc.T(" electron drift", " elektron sürüklenmesi");
            case UpgradeType.HalogenGrasp:
                return "+" + halogenGraspPerUpgrade.ToString("0.#") + Loc.T(" electron reach", " elektron menzili");
            case UpgradeType.HalogenSnatch: return Loc.T("Steal radicals\nfrom further away", "Radikalleri daha\nuzaktan kap");
            case UpgradeType.Alkali:
                return Loc.T("At 3, 11, 19, 37\u2026 electrons:\nThrow your outer electron\nIt explodes and comes back",
                             "3, 11, 19, 37\u2026 elektronda:\nDış elektronunu fırlat\nPatlar ve geri döner");
            case UpgradeType.AlkaliBlast:
                return "+" + alkaliBlastPerUpgrade.ToString("0.#") + Loc.T(" blast radius", " patlama yarıçapı");
            case UpgradeType.AlkaliRange:
                return "+" + alkaliRangePerUpgrade.ToString("0.#") + Loc.T(" throw range", " atış menzili");
            case UpgradeType.AlkaliPath: return Loc.T("Wider path that\ndestroys enemies", "Düşmanları yok eden\ndaha geniş yol");
            case UpgradeType.AlkaliReload:
                return "\u2212" + alkaliCooldownPerUpgrade.ToString("0.#") + Loc.T(" s throw cooldown", " sn atış bekleme süresi");
            default: return Loc.T("Electrons survive\n+1 more hit", "Elektronlar bir darbeye\ndaha dayanır");
        }
    }

    /// <summary>Description for the Index (general, not about the current run).</summary>
    public string IndexDescription(UpgradeType u)
    {
        switch (u)
        {
            case UpgradeType.Heal: return Loc.T("Fully restore\nyour stability", "Kararlılığını\ntamamen doldurur");
            case UpgradeType.Orbital:
                return Loc.T("+2 electron slots\nFill an energy level with electrons: +" + coinsPerExtraShell + " quanta per level",
                             "+2 elektron yuvası\nBir enerji seviyesini elektronla doldur: seviye başına +" + coinsPerExtraShell + " kuanta");
            case UpgradeType.Repel: return Loc.T("Getting hit blasts\nenemies & dust away", "Hasar aldığında düşmanları\nve tozları savurursun");
            case UpgradeType.Booster: return Loc.T("Getting hit gives\na burst of speed", "Hasar aldığında\nani bir hız kazanırsın");
            case UpgradeType.Challenger: return HydrogenText(Loc.T("Earlier picks refunded", "Önceki seçimlerin iade edilir"), true);
            case UpgradeType.Sluggish: return NobleText(true);
            default: return UpgradeDescription(u);
        }
    }

    /// <summary>How many of this upgrade the run owns (characters: 1 if it is the run's character).</summary>
    public int OwnedCount(UpgradeType u)
    {
        switch (u)
        {
            case UpgradeType.MaxHealth: return upgMaxHealth;
            case UpgradeType.Speed: return upgSpeed;
            case UpgradeType.Torque: return upgTorque;
            case UpgradeType.Orbital: return character == Character.Challenger ? 0 : upgOrbitals;
            case UpgradeType.BallArmor: return character == Character.Challenger ? 0 : upgBallArmor;
            case UpgradeType.BallSize: return character == Character.Challenger ? 0 : upgBallSize;
            case UpgradeType.PickupRange: return upgPickupRange;
            case UpgradeType.Repel: return upgRepel;
            case UpgradeType.Booster: return upgBooster;
            case UpgradeType.Regeneration: return upgRegen;
            case UpgradeType.FireRate: return upgFireRate;
            case UpgradeType.TurretRange: return upgTurretRange;
            case UpgradeType.Insight: return upgInsight;
            case UpgradeType.CoinRecycler: return upgCoinRecycler;
            case UpgradeType.HealthRecycler: return upgHealthRecycler;
            case UpgradeType.Challenger: return character == Character.Challenger ? 1 : 0;
            case UpgradeType.Sluggish: return character == Character.Sluggish ? 1 : 0;
            case UpgradeType.Spaceship: return character == Character.Spaceship ? 1 : 0;
            case UpgradeType.Conductive: return character == Character.Conductive ? 1 : 0;
            case UpgradeType.HopRange: return upgHopRange;
            case UpgradeType.HopCount: return upgHopCount;
            case UpgradeType.ChargeRate: return upgChargeRate;
            case UpgradeType.Halogen: return character == Character.Halogen ? 1 : 0;
            case UpgradeType.HalogenPull: return upgHalogenPull;
            case UpgradeType.HalogenGrasp: return upgHalogenGrasp;
            case UpgradeType.HalogenSnatch: return upgHalogenSnatch;
            case UpgradeType.Alkali: return character == Character.Alkali ? 1 : 0;
            case UpgradeType.Excited: return character == Character.Excited ? 1 : 0;
            case UpgradeType.AngularMomentum: return upgSpin;
            case UpgradeType.GroundState: return upgGroundState;
            case UpgradeType.Photoelectric: return upgPhotoelectric;
            case UpgradeType.Resonance: return upgResonance;
            case UpgradeType.AlkaliBlast: return upgAlkaliBlast;
            case UpgradeType.AlkaliRange: return upgAlkaliRange;
            case UpgradeType.AlkaliPath: return upgAlkaliPath;
            case UpgradeType.AlkaliReload: return upgAlkaliReload;
            default: return 0;
        }
    }

    /// <summary>
    /// For upgrades bought repeatedly: how many are owned and the most allowed (-1 = no limit).
    /// False for one-off cards (repair, rares, characters).
    /// </summary>
    public bool UpgradeLevels(UpgradeType u, out int owned, out int cap)
    {
        owned = OwnedCount(u);
        switch (u)
        {
            case UpgradeType.MaxHealth: cap = -1; return true;
            case UpgradeType.Speed:
            case UpgradeType.Torque: cap = -1; return true;
            case UpgradeType.Orbital:
            {
                // Pips = the orbitals of the shell the next one goes into (filled = already open).
                int[] caps = ShellLayoutFor(upgOrbitals);
                int s = caps.Length - 1;
                int full = Elements.ShellSize(s);
                if (caps[s] >= full) { s++; full = Elements.ShellSize(s); owned = 0; }
                else owned = caps[s] / 2;
                cap = full / 2;
                return true;
            }
            case UpgradeType.BallArmor: cap = BallArmorCap; return true;
            case UpgradeType.BallSize: cap = maxBallSizeUpgrades; return true;
            case UpgradeType.AngularMomentum: cap = maxSpinUpgrades; return true;
            case UpgradeType.GroundState: cap = maxGroundStateUpgrades; return true;
            case UpgradeType.PickupRange: cap = maxPickupRangeUpgrades; return true;
            case UpgradeType.Repel: cap = maxRepelUpgrades; return true;
            case UpgradeType.Booster: cap = maxBoosterUpgrades; return true;
            case UpgradeType.FireRate: cap = maxFireRateUpgrades; return true;
            case UpgradeType.TurretRange: cap = maxTurretRangeUpgrades; return true;
            case UpgradeType.HopRange:
            case UpgradeType.HopCount:
            case UpgradeType.ChargeRate: cap = maxConductUpgrades; return true;
            case UpgradeType.HalogenPull:
            case UpgradeType.HalogenGrasp:
            case UpgradeType.HalogenSnatch: cap = maxHalogenUpgrades; return true;
            case UpgradeType.AlkaliBlast:
            case UpgradeType.AlkaliRange:
            case UpgradeType.AlkaliPath:
            case UpgradeType.AlkaliReload: cap = maxAlkaliUpgrades; return true;
            default: cap = 0; return false;
        }
    }

    public static Color UpgradeColor(UpgradeType u)
    {
        switch (u)
        {
            case UpgradeType.Heal:
            case UpgradeType.MaxHealth: return Pickup.HeartColor;
            case UpgradeType.Speed: return new Color(0.5f, 1f, 0.6f);
            case UpgradeType.Orbital: return new Color(0.4f, 0.85f, 1f);
            case UpgradeType.Torque: return new Color(0.75f, 0.6f, 1f);
            case UpgradeType.Challenger: return new Color(1f, 0.3f, 0.3f);
            case UpgradeType.Sluggish: return PendulumController.NobleGold;
            case UpgradeType.BallSize: return new Color(0.35f, 0.7f, 1f);
            case UpgradeType.PickupRange: return Pickup.Gold;
            case UpgradeType.Repel: return new Color(0.75f, 0.9f, 1f);
            case UpgradeType.Booster: return new Color(1f, 0.42f, 0.2f);
            case UpgradeType.Regeneration: return Pickup.HeartColor;
            case UpgradeType.Insight: return new Color(0.7f, 0.55f, 1f);
            case UpgradeType.CoinRecycler: return Pickup.CoinColor;
            case UpgradeType.HealthRecycler: return new Color(1f, 0.5f, 0.62f);
            case UpgradeType.Spaceship: return new Color(0.8f, 1f, 0.35f);
            case UpgradeType.FireRate: return new Color(1f, 0.5f, 0.45f);
            case UpgradeType.TurretRange: return new Color(0.55f, 1f, 0.85f);
            case UpgradeType.Conductive: return ConductiveColor;
            case UpgradeType.HopRange: return new Color(0.6f, 0.95f, 1f);
            case UpgradeType.HopCount: return new Color(1f, 0.78f, 0.4f);
            case UpgradeType.ChargeRate: return new Color(1f, 0.95f, 0.65f);
            case UpgradeType.Halogen: return HalogenColor;
            case UpgradeType.HalogenPull: return new Color(0.55f, 1f, 0.55f);
            case UpgradeType.HalogenGrasp: return Pickup.Gold;
            case UpgradeType.HalogenSnatch: return new Color(1f, 0.55f, 0.35f);
            case UpgradeType.Alkali: return AlkaliColor;
            case UpgradeType.Excited: return ExcitedColor;
            case UpgradeType.AngularMomentum: return new Color(0.45f, 0.9f, 1f);
            case UpgradeType.GroundState: return new Color(0.6f, 0.7f, 1f);
            case UpgradeType.Photoelectric: return new Color(1f, 0.95f, 0.55f);
            case UpgradeType.Resonance: return new Color(1f, 0.6f, 0.85f);
            case UpgradeType.AlkaliBlast: return new Color(1f, 0.45f, 0.3f);
            case UpgradeType.AlkaliRange: return new Color(1f, 0.8f, 0.45f);
            case UpgradeType.AlkaliPath: return new Color(1f, 0.7f, 0.6f);
            case UpgradeType.AlkaliReload: return new Color(1f, 0.88f, 0.55f);
            default: return Pickup.OrbColor;
        }
    }

    // --------------------------------------------------------------- levels

    void SetState(State s)
    {
        if (s != State.Playing && abilityAiming) EndAbilityAim(false);
        if (s != State.Playing && s != State.Paused) AlkaliShot.ReturnAll();   // a thrown electron rejoins at once
        state = s;
        stateTime = 0f;
    }

    /// <summary>3, 3, 4, 4, 5, 5, ... with the default settings.</summary>
    int StarsForLevel(int l)
    {
        int every = Mathf.Max(1, levelsPerExtraStar);
        return baseStarsPerLevel + (l + every - 1) / every;
    }

    /// <summary>+1 orb at levels 3, 6, 9, ... (default); the Excited Atom multiplies the total.</summary>
    int OrbsForLevel(int l)
    {
        int n = orbsPerLevel + l / Mathf.Max(1, levelsPerExtraOrb) + Lab.Level(LabPerk.ExtraElectrons);
        if (character == Character.Excited) n *= Mathf.Max(1, excitedOrbMultiplier);
        return n;
    }

    /// <param name="hardReset">Start fresh: full health, no balls, stopped (used after dying / from the title).
    /// Otherwise health and balls carry over from the previous level.</param>
    void StartLevel(int l, bool hardReset)
    {
        level = Mathf.Max(1, l);
        hurtThisLevel = false;
        levelFlawless = false;
        teaching = false;
        lessonQueue.Clear();
        if (abilityAiming) EndAbilityAim(false);   // the level sets the time scale itself
        abilityCooldown = 0f;
        AlkaliShot.ReturnAll();
        SuperconductOnLevelStart();
        if (hardReset) ResetConductivity();
        // Quanta for this level; the ones earned last level (a filled energy level) are new.
        levelQuanta = CoinsPerLevel;
        newQuanta = character == Character.Challenger ? 0 : Mathf.Min(pendingNewQuanta, levelQuanta);
        pendingNewQuanta = 0;
        if (hardReset) lives = MaxHealth;
        for (int i = 0; i < guideTargets.Length; i++) guideTargets[i] = null;
        if (!(hardReset && level == 1) && upgRegen > 0 && lives < MaxHealth)
        {
            lives++;   // Regeneration (rare upgrade)
            Sfx.Play("heal");
            ParticleBursts.Burst(pendulum.PivotPosition, Pickup.HeartColor, 24, 6f, 0.3f, 0.8f);
        }
        levelCoins = 0;
        float find = FindMultiplier;   // the Lonely Ion finds twice as much
        heartAvailable = Random.value < heartChancePerLevel * find;
        heartTimer = Random.Range(heartMinInterval, heartMaxInterval);
        // Lab Stabilizers: a level that starts below max stability may have a stability point
        // lying around from the start (+5% per level of research).
        if (lives < MaxHealth && Random.value < Lab.StabilityPickupPerLevel * Lab.Level(LabPerk.StabilityPickup) * find)
        {
            heartAvailable = true;
            heartTimer = 0.5f;
        }
        neutrinoPickupPending = Random.value < (neutrinoPickupChance + Lab.NeutrinoPickupPerLevel * Lab.Level(LabPerk.NeutrinoPickup)) * find;
        neutrinoPickupTimer = Random.Range(neutrinoPickupDelay.x, neutrinoPickupDelay.y);
        // Lab Ghost Trail: the Lonely Ion always gets one more neutrino drifting through the level.
        ionNeutrinoTimer = character == Character.Challenger && Lab.Has(LabPerk.IonNeutrino)
            ? Random.Range(neutrinoPickupDelay.x * 0.5f, neutrinoPickupDelay.y * 0.6f) : -1f;
        starsNeeded = StarsForLevel(level);
        starsCollected = 0;
        starsSpawned = 0;
        levelTime = 0f;
        hitStop = 0f;
        Time.timeScale = 1f;

        ClearPickups();
        if (hardReset)
        {
            ParticleBursts.ClearAll();
            ClearHazards();
        }
        asteroidTimer = Random.Range(asteroidInterval.x, asteroidInterval.y) * 0.4f;
        wellTimer = Random.Range(wellInterval.x, wellInterval.y) * 0.5f;
        pendulum.ResetState(hardReset);
        spawner.levelOnly = character == Character.Challenger;   // the Lonely Ion: enemies scale by level alone
        spawner.Begin(level);
        TopUpStars();
        for (int i = 0; i < levelQuanta; i++)
        {
            Pickup q = SpawnPickup(PickupKind.Coin, coinMinDistance, coinMaxDistance);
            if (i >= levelQuanta - newQuanta) q.MarkNew();   // just unlocked: shown off on the map
            extras.Add(q);
        }
        // A fixed number of orbs, all placed at the start. The Lonely Ion holds none, but still
        // gets them: a Recycler can turn them into quanta or stability.
        if (pendulum.Capacity > 0 || character == Character.Challenger)
            for (int i = 0, n = OrbsForLevel(level); i < n; i++)
                orbs.Add(SpawnPickup(PickupKind.Orb, orbMinDistance, orbMaxDistance));

        levelStartCoinProgress = coinRecycleProgress;
        levelStartHeartProgress = heartRecycleProgress;
        SetBest(level);

        Sfx.Play("levelStart");
        SetState(State.Playing);
    }

    void CompleteLevel()
    {
        spawner.ClearAll(true);
        PopLeftoverPickups();
        Sfx.Play("levelComplete");
        AddShake(0.3f);
        ParticleBursts.Burst(pendulum.PivotPosition, Pickup.Gold, 40, 9f, 0.35f, 1f);

        SetBest(level + 1);

        // Not a scratch on the core all level: a neutrino.
        // The Lonely Ion gets a free one every level too.
        levelFlawless = !hurtThisLevel;
        LevelNeutrinos = (levelFlawless ? 1 : 0) + (character == Character.Challenger ? Mathf.Max(0, ionNeutrinosPerLevel) : 0);
        neutrinos += LevelNeutrinos;

        SetState(State.LevelComplete);
    }

    void EndGame()
    {
        // Out of stability: the core shakes apart and explodes; the game over screen follows.
        spawner.StopSpawning();
        pendulum.BeginMeltdown();
        SetState(State.Dying);
    }

    void SetPaused(bool paused)
    {
        if (paused && state == State.Playing)
        {
            Time.timeScale = 0f;
            SetState(State.Paused);
        }
        else if (!paused && state == State.Paused)
        {
            Time.timeScale = 1f;
            SetState(State.Playing);
        }
    }

    // ------------------------------------------------------------- pickups

    void TopUpStars()
    {
        while (stars.Count < maxActiveStars && starsSpawned < starsNeeded)
        {
            float minD = starMinDistance + (level - 1) * starDistancePerLevel * 0.5f;
            float maxD = Mathf.Min(starMaxDistanceCap, starMaxDistance + (level - 1) * starDistancePerLevel);
            stars.Add(SpawnPickup(PickupKind.Star, minD, Mathf.Max(maxD, minD + 1f)));
            starsSpawned++;
        }
    }

    Pickup SpawnPickup(PickupKind kind, float minD, float maxD)
    {
        Vector2 origin = pendulum.PivotPosition;
        Vector2 pos = origin;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            pos = origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(minD, maxD);
            // Never inside / right next to an asteroid or a gravity well (pickups are also
            // pushed clear of hazards that drift onto them later).
            if (!NearHazard(pos) && !TooClose(pos, stars) && !TooClose(pos, orbs) && !TooClose(pos, extras)) break;
        }

        return SpawnPickupAt(kind, pos);
    }

    Pickup SpawnPickupAt(PickupKind kind, Vector2 pos)
    {
        var go = new GameObject(kind.ToString());
        go.transform.position = pos;
        var p = go.AddComponent<Pickup>();
        p.Init(kind, OnPickupCollected);
        return p;
    }

    /// <summary>A collectible electron left where something was destroyed (Lab Electroplating).</summary>
    void DropElectron(Vector2 at)
    {
        if (state != State.Playing) return;
        orbs.Add(SpawnPickupAt(PickupKind.Orb, at));
        ParticleBursts.Burst(at, Pickup.OrbColor, 12, 4f, 0.2f, 0.4f);
    }

    static bool NearHazard(Vector2 pos)
    {
        foreach (var h in Hazard.All)
        {
            float min = h.PickupClearance + 1f;
            if (((Vector2)h.transform.position - pos).sqrMagnitude < min * min) return true;
        }
        return false;
    }

    bool TooClose(Vector2 pos, List<Pickup> list)
    {
        float min = Mathf.Min(starSpacing, 3f);
        foreach (var p in list)
            if (p != null && ((Vector2)p.transform.position - pos).sqrMagnitude < min * min) return true;
        return false;
    }

    // -------------------------------------------------------------- hazards

    void UpdateHazards(float dt)
    {
        int asteroids = 0, wells = 0;
        foreach (var h in Hazard.All)
        {
            if (h.Type == HazardType.Asteroid) asteroids++;
            else wells++;
        }

        if (level >= asteroidUnlockLevel)
        {
            int allowed = Mathf.Min(maxAsteroids, 1 + (level - asteroidUnlockLevel) / 3);
            asteroidTimer -= dt;
            if (asteroidTimer <= 0f && asteroids < allowed)
            {
                SpawnHazard(HazardType.Asteroid);
                asteroidTimer = Random.Range(asteroidInterval.x, asteroidInterval.y);
            }
        }
        if (level >= wellUnlockLevel)
        {
            wellTimer -= dt;
            if (wellTimer <= 0f && wells < maxWells)
            {
                SpawnHazard(HazardType.GravityWell);
                wellTimer = Random.Range(wellInterval.x, wellInterval.y);
            }
        }
    }

    /// <summary>Hazards start off-screen (no warning) and drift toward the player's general area.</summary>
    void SpawnHazard(HazardType type)
    {
        float r = type == HazardType.Asteroid ? Random.Range(asteroidRadius.x, asteroidRadius.y) : wellCoreRadius;
        float extent = type == HazardType.Asteroid ? r : wellPullRadius * 0.6f;
        float a = Random.Range(0f, Mathf.PI * 2f);
        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        Vector2 pos = viewCenter + dir * (HalfExtents.magnitude + extent + 1.5f);
        Vector2 target = pendulum.PivotPosition + Random.insideUnitCircle * 6f;
        Vector2 range = type == HazardType.Asteroid ? asteroidSpeed : wellSpeed;
        Vector2 vel = (target - pos).normalized * Random.Range(range.x, range.y);

        var go = new GameObject(type.ToString());
        go.transform.position = pos;
        var h = go.AddComponent<Hazard>();
        if (type == HazardType.Asteroid) h.InitAsteroid(r, vel);
        else
        {
            float strength = wellPullStrength + wellPullPerLevel * Mathf.Max(0, level - wellUnlockLevel);
            h.InitWell(wellCoreRadius, wellPullRadius, strength, vel, wellCorePullMultiplier);
        }
    }

    void ClearHazards()
    {
        var copy = new List<Hazard>(Hazard.All);
        foreach (var h in copy) if (h != null) Destroy(h.gameObject);
    }

    void UpdateHearts(float dt)
    {
        // At most one heart per level, only on the rare levels that roll one, and only after
        // being hurt for a long while (the timer only runs while below max health).
        extras.RemoveAll(e => e == null);
        if (!heartAvailable || lives >= MaxHealth) return;
        heartTimer -= dt;
        if (heartTimer > 0f) return;
        extras.Add(SpawnPickup(PickupKind.Heart, heartMinDistance, heartMaxDistance));
        heartAvailable = false;
    }

    /// <summary>A level may hold one neutrino pickup (rare; the Lab raises the odds). It appears after a while.</summary>
    void UpdateNeutrinoPickup(float dt)
    {
        if (ionNeutrinoTimer >= 0f)
        {
            ionNeutrinoTimer -= dt;
            if (ionNeutrinoTimer < 0f) extras.Add(SpawnPickup(PickupKind.Neutrino, heartMinDistance, heartMaxDistance));
        }
        if (!neutrinoPickupPending) return;
        neutrinoPickupTimer -= dt;
        if (neutrinoPickupTimer > 0f) return;
        neutrinoPickupPending = false;
        extras.Add(SpawnPickup(PickupKind.Neutrino, heartMinDistance, heartMaxDistance));
    }

    /// <summary>
    /// An orb taken with full orbitals: Health Recycler first (only while hurt), otherwise
    /// Coin Recycler. Enough recycled orbs restore a heart / give a coin.
    /// </summary>
    void RecycleOrb(Vector2 at)
    {
        if (HasHealthRecycler && lives < MaxHealth)
        {
            heartRecycleProgress++;
            if (heartRecycleProgress >= OrbsPerRecycledHeart)
            {
                heartRecycleProgress = 0;
                lives = Mathf.Min(MaxHealth, lives + 1);
                Sfx.Play("heal");
                ParticleBursts.Burst(pendulum.PivotPosition, Pickup.HeartColor, 24, 6f, 0.3f, 0.8f);
                GameUI.FloatText(at, Loc.T("+1 STABILITY", "+1 KARARLILIK"), Pickup.HeartColor, 26f);
            }
            else
            {
                Sfx.Play("recycle");
            }
        }
        else if (HasCoinRecycler)
        {
            coinRecycleProgress++;
            if (coinRecycleProgress >= OrbsPerRecycledCoin)
            {
                coinRecycleProgress = 0;
                levelCoins++;
                Sfx.Play("coin");
                ParticleBursts.Burst(at, Pickup.CoinColor, 24, 6f, 0.3f, 0.8f);
                GameUI.FloatText(at, Loc.T("+1 QUANTUM", "+1 KUANTUM"), Pickup.CoinColor, 26f);
            }
            else
            {
                Sfx.Play("recycle");
            }
        }
    }

    /// <summary>An orb touched with no room for it and nothing to recycle it into.</summary>
    public void WarnOrbitalsFull(Vector2 at)
    {
        Sfx.Play("full", 0.7f);
    }

    void OnPickupCollected(Pickup p)
    {
        stars.Remove(p);
        orbs.Remove(p);
        extras.Remove(p);
        if (state != State.Playing) return;
        Codex.See(Codex.Particle(p.Kind));   // the Index's Particles tab
        // The first ever photon / electron / quantum: what it is (not on the photon that ends the level).
        if (p.Kind == PickupKind.Star && starsCollected + 1 < starsNeeded) Teach(LessonKind.Photon, PhotonLessonKey);
        else if (p.Kind == PickupKind.Orb && ElectronsTeachable && !pendulum.IsFull) Teach(LessonKind.Electron, ElectronLessonKey);
        else if (p.Kind == PickupKind.Coin) Teach(LessonKind.Quantum, QuantumLessonKey);

        switch (p.Kind)
        {
            case PickupKind.Orb:
                if (pendulum.IsFull) RecycleOrb(p.transform.position);
                else if (pendulum.AddBall()) Sfx.Play("orb");
                return;
            case PickupKind.Coin:
                levelCoins++;
                GameUI.GoalCollected(PickupKind.Coin, p.transform.position);   // flies to the HUD
                Sfx.Play("coin");
                return;
            case PickupKind.Heart:
                lives = Mathf.Min(MaxHealth, lives + 1);
                Sfx.Play("heal");
                return;
            case PickupKind.Neutrino:
                neutrinos++;
                Sfx.Play("neutrino", 0.9f);
                GameUI.FloatText(p.transform.position, Loc.T("+1 NEUTRINO", "+1 NÖTRİNO"), Pickup.NeutrinoColor, 24f);
                return;
        }

        starsCollected++;
        GameUI.GoalCollected(PickupKind.Star, p.transform.position);   // flies to the HUD
        Sfx.Play("star");
        AddShake(0.15f);
        if (starsCollected >= starsNeeded) CompleteLevel();
        else
        {
            if (upgPhotoelectric > 0) Photoelectric(p.transform.position);
            TopUpStars();
        }
    }

    // ------------------------------------------------------------- first-run guide

    /// <summary>The kinds the guide points at (photon, electron, quantum), in GameUI's order.</summary>
    public static readonly PickupKind[] GuideKinds = { PickupKind.Star, PickupKind.Orb, PickupKind.Coin };
    readonly Pickup[] guideTargets = new Pickup[3];

    // The Lonely Ion can't hold electrons: no electron lesson (or pointer) while playing it.
    bool ElectronsTeachable => character != Character.Challenger;

    static string GuideLessonKey(int i) => i == 0 ? PhotonLessonKey : i == 1 ? ElectronLessonKey : QuantumLessonKey;

    /// <summary>
    /// The pickup the guide's chevrons for GuideKinds[i] point at: the nearest one, until the
    /// first one of that kind ever is collected (its lesson seen). Null otherwise.
    /// </summary>
    public Pickup GuideTarget(int i)
    {
        if (state != State.Playing || pendulum == null || PlayerPrefs.GetInt(GuideLessonKey(i), 0) != 0) return null;
        PickupKind kind = GuideKinds[i];
        if (kind == PickupKind.Orb && !ElectronsTeachable) return null;
        List<Pickup> list = kind == PickupKind.Star ? stars : kind == PickupKind.Orb ? orbs : extras;
        Pickup best = null;
        float bestD = float.MaxValue;
        Vector2 at = pendulum.PivotPosition;
        foreach (var q in list)
        {
            if (q == null || q.Kind != kind) continue;
            float d = ((Vector2)q.transform.position - at).sqrMagnitude;
            if (d < bestD) { bestD = d; best = q; }
        }
        // Keep pointing at the same one unless another is much closer (no flicker between two).
        Pickup last = guideTargets[i];
        if (last != null && best != null && list.Contains(last)
            && ((Vector2)last.transform.position - at).sqrMagnitude < bestD * 2.25f)
            best = last;
        guideTargets[i] = best;
        return best;
    }

    /// <summary>Photoelectric Effect: the photon knocks electrons loose around where it was caught.</summary>
    void Photoelectric(Vector2 at)
    {
        Color c = UpgradeColor(UpgradeType.Photoelectric);
        float a0 = Random.Range(0f, Mathf.PI * 2f);
        for (int i = 0; i < photoelectricElectrons; i++)
        {
            float a = a0 + i * Mathf.PI * 2f / Mathf.Max(1, photoelectricElectrons) + Random.Range(-0.4f, 0.4f);
            Vector2 pos = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(photoelectricMinDistance, photoelectricMaxDistance);
            var go = new GameObject("Orb");
            go.transform.position = pos;
            var orb = go.AddComponent<Pickup>();
            orb.Init(PickupKind.Orb, OnPickupCollected);
            orbs.Add(orb);
            AbilityAim.Streak(at, pos, 0.1f, c, 0.25f);
            ParticleBursts.Burst(pos, c, 10, 4f, 0.2f, 0.4f);
        }
        Sfx.Play("zap", 0.4f);
    }

    /// <summary>Level over: every pickup still on the map pops, rippling outward from the player.</summary>
    void PopLeftoverPickups()
    {
        var left = new List<Pickup>();
        left.AddRange(stars);
        left.AddRange(orbs);
        left.AddRange(extras);
        left.RemoveAll(p => p == null);
        stars.Clear();
        orbs.Clear();
        extras.Clear();

        Vector2 o = pendulum.PivotPosition;
        left.Sort((a, b) => ((Vector2)a.transform.position - o).sqrMagnitude
                   .CompareTo(((Vector2)b.transform.position - o).sqrMagnitude));
        for (int i = 0; i < left.Count; i++)
            left[i].Pop(0.2f + Mathf.Min(i * 0.07f, 1.2f));
    }

    void ClearPickups()
    {
        foreach (var s in stars) if (s != null) Destroy(s.gameObject);
        foreach (var o in orbs) if (o != null) Destroy(o.gameObject);
        foreach (var e in extras) if (e != null) Destroy(e.gameObject);
        stars.Clear();
        orbs.Clear();
        extras.Clear();
    }

    // ------------------------------------------------------ gameplay events

    public void OnPlayerHit()
    {
        if (state != State.Playing) return;
        lives--;
        hurtThisLevel = true;
        AddShake(0.5f);
        hitStop = 0.08f;
        Sfx.Play("hit");
        if (lives <= 0 && !secondWindUsed && Lab.Level(LabPerk.SecondWind) > 0)
        {
            // Lab: Second Wind. Once per run the core holds together at 1 stability: in slow motion
            // it almost blows apart, then lets it all out as a wave that clears the screen.
            secondWindUsed = true;
            lives = 1;
            float dur = Mathf.Clamp(Sfx.Length("secondWind"), 1f, 6f);
            pendulum.BeginSecondWind(dur);
            Sfx.Play("secondWind");
            GameUI.Announce(Loc.T("SECOND WIND", "İKİNCİ NEFES"), Color.white, dur + 0.6f, "");
            return;
        }
        if (lives <= 0) EndGame();
    }

    /// <summary>Second Wind's release: a wave from the core that destroys every enemy on screen.</summary>
    public void SecondWindBurst()
    {
        Vector2 c = pendulum.PivotPosition;
        Vector2 half = HalfExtents, vc = ViewCenter;
        float radius = 0f;   // out to the farthest corner of the view
        for (int i = 0; i < 4; i++)
        {
            Vector2 corner = vc + new Vector2(i % 2 == 0 ? -half.x : half.x, i < 2 ? -half.y : half.y);
            radius = Mathf.Max(radius, Vector2.Distance(c, corner));
        }
        spawner.CancelWarnings();
        NobleWave.Spawn(c, radius + 0.5f, 14f, Color.white, false);
        ShockRing.Spawn(c, radius * 0.6f, Color.white);
        Sfx.Play("repel");
        Sfx.Play("explode", 0.6f);
        AddShake(0.9f);
    }

    public void AddShake(float amount) => shake = Mathf.Max(shake, amount);

    // ---------------------------------------------------------- energy levels

    /// <summary>Time scale while Second Wind builds up.</summary>
    const float SecondWindSlowMo = 0.12f;

    public const float TeachMinTime = 1f;   // the lesson can't be closed sooner

    /// <summary>
    /// Has the atom just filled another energy level with electrons (2 = He, 10 = Ne, 18 = Ar...)?
    /// Each one counts once per run: +1 quantum per level, from the next level on. The very first
    /// time ever, the game stops to explain it.
    /// </summary>
    void CheckEnergyLevels()
    {
        if (pendulum == null || character == Character.Challenger) return;
        int electrons = pendulum.ElectronTotal;
        int full = 0, total = 0;
        while (total + Elements.ShellSize(full) <= electrons) { total += Elements.ShellSize(full); full++; }
        if (full <= shellsFilled) return;

        int gained = full - shellsFilled;
        shellsFilled = full;
        if (upgResonance > 0 && lives < MaxHealth)
        {
            lives = MaxHealth;
            GameUI.FloatText(pendulum.PivotPosition + Vector2.up * 0.6f, Loc.T("RESONANCE", "REZONANS"),
                             UpgradeColor(UpgradeType.Resonance), 26f);
            ParticleBursts.Burst(pendulum.PivotPosition, Pickup.HeartColor, 30, 7f, 0.3f, 0.7f);
            Sfx.Play("charge", 0.7f);
        }
        pendingNewQuanta += gained * coinsPerExtraShell;
        Sfx.Play("energy", 0.8f);
        AddShake(0.2f);
        ParticleBursts.Burst(pendulum.PivotPosition, Pickup.CoinColor, 36, 8f, 0.3f, 0.9f);

        if (!Teach(LessonKind.Shell, ShellLessonKey))
        {
            GameUI.Announce(Loc.T("ENERGY LEVEL COMPLETE", "ENERJİ SEVİYESİ TAMAMLANDI"), Pickup.CoinColor, 2.6f, "");   // its sound already played
        }
    }

    /// <summary>
    /// Shows a lesson the first time ever (queued if another one is up). False if it was
    /// already seen before.
    /// </summary>
    bool Teach(LessonKind kind, string key)
    {
        if (PlayerPrefs.GetInt(key, 0) != 0) return false;
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
        if (teaching) { lessonQueue.Enqueue(kind); return true; }
        StartTeaching(kind);
        return true;
    }

    void StartTeaching(LessonKind kind)
    {
        if (abilityAiming) EndAbilityAim(false);
        TeachingKind = kind;
        LessonVersion++;
        teaching = true;
        teachTime = 0f;
        Time.timeScale = 0f;
    }

    /// <summary>Closes the lesson (key press here, or the button in GameUI); the next queued one follows.</summary>
    public void EndTeaching()
    {
        if (!teaching || teachTime < TeachMinTime) return;
        teachEndFrame = Time.frameCount;   // this press must not also fire an ability
        if (lessonQueue.Count > 0) { StartTeaching(lessonQueue.Dequeue()); return; }
        teaching = false;
        Time.timeScale = 1f;
        Sfx.Play("start", 0.6f);
    }

    // --------------------------------------------------------------- update

    void Update()
    {
        float udt = Time.unscaledDeltaTime;
        stateTime += udt;
        var kb = Keyboard.current;

        switch (state)
        {
            case State.Title:
                if (OverlayOpen) break;
                if (ConfirmPressed()) NewRun();
                break;
            case State.Playing:
                if (teaching)
                {
                    // Frozen on the lesson (GameUI shows it and closes it via EndTeaching).
                    teachTime += udt;
                    break;
                }
                if (abilityAiming)
                {
                    // Frozen while the Noble Atom's ability is aimed (fired or cancelled from here).
                    UpdateAbilityAim();
                    break;
                }
                abilityCooldown = Mathf.Max(0f, abilityCooldown - Time.deltaTime);
                levelTime += Time.deltaTime;
                UpdateHazards(Time.deltaTime);
                UpdateHearts(Time.deltaTime);
                UpdateNeutrinoPickup(Time.deltaTime);
                CheckEnergyLevels();
                UpdateTraits(Time.deltaTime);
                if (kb != null && kb.spaceKey.wasPressedThisFrame && Ability != AbilityKind.None
                    && Time.frameCount != teachEndFrame && !teaching)
                    ToggleAbilityAim();
                if (character != Character.Challenger)
                    runMaxElectrons = Mathf.Max(runMaxElectrons, pendulum.ElectronTotal);
                if (PausePressed()) SetPaused(true);
                break;
            case State.Upgrade:
                // Picks are made by clicking a petal (GameUI). After the last one, wait for the
                // core to absorb it and fly back to the map.
                if (upgradeFinishing && stateTime >= AbsorbTime + upgradeCoreReturnTime)
                {
                    upgradeFinishing = false;
                    Time.timeScale = 1f;
                    StartLevel(level + 1, false);
                }
                break;
            case State.Paused:
                if (OverlayOpen) break;
                if (kb != null && kb.rKey.wasPressedThisFrame) RetryLevel();
                else if (kb != null && kb.mKey.wasPressedThisFrame) QuitToTitle();
                else if (PausePressed() || ConfirmPressed()) Resume();
                break;
            case State.LevelComplete:
                if (stateTime > levelCompleteDelay)
                {
                    if (levelCoins > 0) BeginUpgradeChoices();
                    else StartLevel(level + 1, false);
                }
                break;
            case State.Dying:
                if (!pendulum.IsAlive && stateTime > pendulum.meltdownTime + 1f)
                {
                    Sfx.Play("over", 0.8f);
                    PayResearch();
                    SetState(State.GameOver);
                }
                break;
            case State.GameOver:
                if (stateTime > 0.8f && kb != null && !OverlayOpen)
                {
                    if (kb.rKey.wasPressedThisFrame || ConfirmPressed()) RetryLevel();
                    else if (kb.nKey.wasPressedThisFrame) NewRun();
                }
                break;
        }

        if (music != null)
        {
            music.volume = state == State.Story ? 0f : musicVolume;
            music.lab = GameUI.LabOpen;
            music.muffled = state == State.Paused;
        }

        if (state != State.Paused && state != State.Upgrade)
        {
            if ((teaching || abilityAiming) && state == State.Playing) Time.timeScale = 0f;   // lesson / aiming an ability
            else if (pendulum != null && pendulum.SecondWinding && state == State.Playing) Time.timeScale = SecondWindSlowMo;
            else if (hitStop > 0f)
            {
                hitStop -= udt;
                Time.timeScale = 0.05f;
            }
            else
            {
                Time.timeScale = 1f;
            }
        }
    }

    void LateUpdate()
    {
        float udt = Time.unscaledDeltaTime;
        UpdateCamera(udt);
        UpdateBackground(udt);
    }

    /// <summary>Keyboard confirm (mouse / touch go through the on-screen buttons).</summary>
    static bool ConfirmPressed()
    {
        var kb = Keyboard.current;
        return kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
    }

    // ------------------------------------------------------ menu actions (UI buttons)

    /// <summary>Fresh run from level 1 with no upgrades.</summary>
    public void NewRun()
    {
        if (state != State.Title && state != State.GameOver && state != State.Paused) return;
        // The first PLAY ever tells the story first; the run starts when it ends.
        if (state == State.Title && PlayerPrefs.GetInt(StorySeenKey, 0) == 0)
        {
            if (OverlayOpen) return;
            runAfterStory = true;
            SetState(State.Story);
            return;
        }
        // Characters bought in the Lab: choose what to start as (GameUI calls StartRun).
        if (state != State.Paused && Lab.AnyCharacterUnlocked)
        {
            if (!OverlayOpen) GameUI.OpenChooser();
            return;
        }
        BeginRun();
    }

    /// <summary>A fresh run, starting as 'c' (a character bought in the Lab, or None).</summary>
    public void StartRun(Character c)
    {
        if (state != State.Title && state != State.GameOver && state != State.Paused) return;
        BeginRun(Lab.CharacterUnlocked(c) ? c : Character.None);
    }

    void BeginRun(Character startAs = Character.None)
    {
        if (state == State.Paused) PayResearch();   // an abandoned run still pays
        Time.timeScale = 1f;
        ResetUpgrades();
        if (startAs != Character.None)
        {
            character = startAs;
            if (startAs == Character.Challenger) upgOrbitals = 0;   // the ion holds no electrons
            Codex.See(Codex.Upgrade(CharacterCard(startAs)));
            ApplyUpgrades();
        }
        StartLevel(1, true);
    }

    /// <summary>
    /// The run ended (or was abandoned): pay its Research Data. A run that is retried and ends
    /// again only pays what it gained since (going deeper, a bigger atom).
    /// </summary>
    void PayResearch()
    {
        int value = Lab.RunValue(runMaxElectrons, level);
        int gain = Mathf.Max(0, value - runPaid);
        runPaid = Mathf.Max(runPaid, value);
        Lab.AddData(gain);
        bool record = Lab.SubmitRun(value);
        LastPayout = new Payout
        {
            atomicNumber = runMaxElectrons, level = level, value = value, gain = gain, bank = Lab.Data,
            record = record, bonus = Lab.ResearchMultiplier - 1f,
        };
        PayoutVersion++;
    }

    /// <summary>Replay the current level from the start, keeping the run's upgrades.</summary>
    public void RetryLevel()
    {
        if (state != State.GameOver && state != State.Paused) return;
        Time.timeScale = 1f;
        coinRecycleProgress = levelStartCoinProgress;
        heartRecycleProgress = levelStartHeartProgress;
        StartLevel(level, true);
    }

    public void Pause() => SetPaused(true);
    public void Resume() => SetPaused(false);

    /// <summary>Abandon the run and go back to the title screen.</summary>
    public void QuitToTitle()
    {
        if (state != State.Paused && state != State.GameOver) return;
        if (state == State.Paused) PayResearch();   // an abandoned run still pays
        Time.timeScale = 1f;
        hitStop = 0f;
        spawner.ClearAll(false);
        ClearPickups();
        ClearHazards();
        ParticleBursts.ClearAll();
        ResetUpgrades();
        pendulum.ResetState(true);
        lives = MaxHealth;
        SetState(State.Title);
    }

    public static bool CanQuitApp =>
        !Application.isEditor && Application.platform != RuntimePlatform.WebGLPlayer;

    public void QuitApp()
    {
        if (CanQuitApp) Application.Quit();
    }

    static bool PausePressed()
    {
        var kb = Keyboard.current;
        return kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame);
    }

    void UpdateCamera(float udt)
    {
        if (pendulum != null && udt > 0f)
        {
            Vector2 target = pendulum.PivotPosition + pendulum.PivotVelocity * cameraLookAhead;
            viewCenter = Vector2.SmoothDamp(viewCenter, target, ref viewVel, cameraSmoothTime, Mathf.Infinity, udt);
        }
        if (cam == null) return;
        shake = Mathf.MoveTowards(shake, 0f, udt * 2.5f);
        Vector2 off = shake > 0f ? Random.insideUnitCircle * shake * 0.35f : Vector2.zero;
        cam.transform.position = new Vector3(viewCenter.x + off.x, viewCenter.y + off.y, camZ);
    }

    // ----------------------------------------------------------- background

    void BuildBackground()
    {
        var bg = new GameObject("Background");
        var sr = bg.AddComponent<SpriteRenderer>();
        sr.sprite = ProcSprites.Background(backgroundCenter, backgroundEdge);
        sr.sortingOrder = -100;
        background = bg.transform;

        var g = new GameObject("Grid");
        grid = g.AddComponent<SpriteRenderer>();
        grid.sprite = ProcSprites.GridTile;
        grid.drawMode = SpriteDrawMode.Tiled;
        grid.color = new Color(0.55f, 0.75f, 1f, 0.06f);
        grid.sortingOrder = -90;

        var dustRoot = new GameObject("Dust").transform;
        dust = new Transform[dustCount];
        dustVel = new Vector2[dustCount];
        Vector2 half = HalfExtents;
        for (int i = 0; i < dustCount; i++)
        {
            var d = new GameObject("d");
            d.transform.SetParent(dustRoot, false);
            d.transform.position = viewCenter + new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y));
            d.transform.localScale = Vector3.one * Random.Range(0.08f, 0.25f);
            var dsr = d.AddComponent<SpriteRenderer>();
            dsr.sprite = ProcSprites.Glow;
            dsr.color = new Color(0.7f, 0.85f, 1f, Random.Range(0.08f, 0.25f));
            dsr.sortingOrder = -80;
            dust[i] = d.transform;
            dustVel[i] = Random.insideUnitCircle * 0.15f;
        }
    }

    void UpdateBackground(float udt)
    {
        Vector2 half = HalfExtents;
        Vector2 vc = viewCenter;

        // Gradient stays fixed to the screen; the grid stays fixed to the world (shows movement).
        if (background != null)
        {
            background.position = new Vector3(vc.x, vc.y, 0f);
            background.localScale = new Vector3(half.x * 2f + 4f, half.y * 2f + 4f, 1f);
        }
        if (grid != null)
        {
            grid.transform.position = new Vector3(Mathf.Round(vc.x), Mathf.Round(vc.y), 0f);
            grid.size = new Vector2(Mathf.Ceil(half.x) * 2f + 4f, Mathf.Ceil(half.y) * 2f + 4f);
        }
        if (dust == null) return;

        // Dust lives in the world and wraps around the view.
        float wx = half.x + 0.5f, wy = half.y + 0.5f;
        for (int i = 0; i < dust.Length; i++)
        {
            Vector2 p = (Vector2)dust[i].position + dustVel[i] * udt;
            Vector2 r = p - vc;
            if (r.x > wx) p.x -= wx * 2f; else if (r.x < -wx) p.x += wx * 2f;
            if (r.y > wy) p.y -= wy * 2f; else if (r.y < -wy) p.y += wy * 2f;
            dust[i].position = p;
        }
    }
}
