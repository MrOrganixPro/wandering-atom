using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Owns the game loop: title, levels, stars, lives, pause, level complete, game over,
/// saved progress, camera follow/shake, hit-stop and the scrolling background.
/// The HUD and menus are drawn by GameUI (UI Toolkit), which reads this class's state.
/// Created automatically when any scene loads, so it does not need to be in the scene.
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum State { Title, Playing, Paused, LevelComplete, Upgrade, GameOver }

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

    public enum UpgradeType
    {
        Heal, MaxHealth, Speed, Thrust, Torque, BallArmor, Challenger, Sluggish,
        BallSize, PickupRange, Repel, Booster,
        Regeneration,              // rare
        Spaceship,                 // character
        FireRate, TurretRange,     // normal, only with the Spaceship
        Insight, CoinRecycler, HealthRecycler   // rare
    }

    /// <summary>
    /// Every upgrade belongs to exactly one tier:
    /// Normal (1 coin), Rare (1 coin, special card, rarely offered) and
    /// Character (2 coins, uncommon mystery box, at most one per run, changes how the game plays).
    /// </summary>
    public enum UpgradeTier { Normal, Rare, Character }

    /// <summary>The run's Character (at most one per run). Values are saved, so only append.</summary>
    public enum Character { None = 0, Challenger = 1, Sluggish = 2, Spaceship = 3 }

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
    public int coinsPerLevel = 2;
    public float coinMinDistance = 7f;
    public float coinMaxDistance = 15f;

    [Header("Hearts (extremely rare, only when hurt)")]
    [Tooltip("Chance that a level gets a heart at all (at most one per level).")]
    [Range(0f, 1f)] public float heartChancePerLevel = 0.08f;
    [Tooltip("Seconds spent hurt in the level before that heart appears.")]
    public float heartMinInterval = 35f;
    public float heartMaxInterval = 70f;
    public float heartMinDistance = 5f;
    public float heartMaxDistance = 10f;

    [Header("Upgrades")]
    public int upgradeChoices = 3;
    public float speedPerUpgrade = 0.2f;
    public float thrustPerUpgrade = 0.2f;
    public float torquePerUpgrade = 6f;
    [Tooltip("Each Torque upgrade also adds this much turning/braking authority (base 0.45, max 1).")]
    public float turnAuthorityPerTorque = 0.15f;
    [Tooltip("Ball Armor can be bought this many times (balls have at most 1 + this many hit points).")]
    public int maxBallArmorUpgrades = 2;
    [Tooltip("One Ball Armor purchase is unlocked ('banked') every this many levels.")]
    public int levelsPerBallArmor = 4;

    [Tooltip("Bigger Balls: ball size +this fraction per upgrade.")]
    public float ballSizePerUpgrade = 0.25f;
    public int maxBallSizeUpgrades = 4;
    [Tooltip("Pickup Range: extra reach (world units) for the thruster and every ball, per upgrade.")]
    public float pickupRangePerUpgrade = 0.6f;
    public int maxPickupRangeUpgrades = 3;
    [Tooltip("Repel / Emergency Booster: the first upgrade unlocks it, later ones make it stronger.")]
    public int maxRepelUpgrades = 5;
    public int maxBoosterUpgrades = 5;

    [Header("Rare Upgrades (special card, costs 1 coin)")]
    public int rareUnlockLevel = 3;
    [Tooltip("Chance a set of upgrade cards includes a rare one.")]
    [Range(0f, 1f)] public float rareChance = 0.1f;
    [Tooltip("Coin Recycler: orbs recycled (with full orbitals) per coin.")]
    public int orbsPerRecycledCoin = 5;
    [Tooltip("Health Recycler: orbs recycled (with full orbitals, while hurt) per heart restored.")]
    public int orbsPerRecycledHeart = 3;

    [Header("Characters (uncommon mystery box, costs 2 coins, one per run)")]
    [FormerlySerializedAs("gameChangerUnlockLevel")] public int characterUnlockLevel = 5;
    [Tooltip("Chance the first pick after a level (with every coin collected) includes a Character box.")]
    [FormerlySerializedAs("gameChangerChance")]
    [Range(0f, 1f)] public float characterChance = 0.25f;
    public float mysteryRevealTime = 2.4f;
    [Tooltip("Challenger: coins per level.")]
    public int challengerCoins = 3;
    [Tooltip("Sluggish: rope lengths are multiplied by this.")]
    public float sluggishTighten = 0.78f;
    public int sluggishExtraShellCapacity = 8;
    public int sluggishMaxBallArmor = 4;
    [Tooltip("Sluggish: Thrust and Torque can each be upgraded at most this many times (2 = level 3). Upgrades above this are refunded as extra picks.")]
    public int sluggishUpgradeCap = 2;
    [Tooltip("Sluggish: orbs per level are multiplied by this.")]
    public int sluggishOrbMultiplier = 2;
    [Tooltip("Spaceship: Fire Rate upgrade, +this fraction of shots per second each.")]
    public float fireRatePerUpgrade = 0.25f;
    public int maxFireRateUpgrades = 5;
    [Tooltip("Spaceship: Turret Range upgrade, +this many world units each.")]
    public float turretRangePerUpgrade = 1f;
    public int maxTurretRangeUpgrades = 5;

    [Header("Camera & Look")]
    public float cameraSize = 7f;
    public float cameraSmoothTime = 0.25f;
    [Tooltip("Camera leads ahead of the moving pendulum by this many seconds of its velocity.")]
    public float cameraLookAhead = 0.35f;
    public Color backgroundCenter = new Color(0.10f, 0.12f, 0.22f);
    public Color backgroundEdge = new Color(0.02f, 0.02f, 0.05f);
    public int dustCount = 50;

    public State CurrentState => state;
    public int Level => level;

    State state = State.Title;
    int level = 1, savedLevel = 1, bestLevel = 1;
    int lives, starsNeeded, starsCollected, starsSpawned;
    float levelTime, stateTime, shake, hitStop, heartTimer, asteroidTimer, wellTimer;
    bool heartAvailable;   // this level rolled its (rare) heart and hasn't spawned it yet
    readonly List<Pickup> stars = new List<Pickup>();
    readonly List<Pickup> orbs = new List<Pickup>();
    readonly List<Pickup> extras = new List<Pickup>();   // coins + hearts

    // Upgrades (saved).
    int upgMaxHealth, upgSpeed, upgThrust, upgTorque, upgBallArmor;
    int upgBallSize, upgPickupRange, upgRepel, upgBooster, upgRegen;
    int upgFireRate, upgTurretRange;
    int upgInsight, upgCoinRecycler, upgHealthRecycler;   // rare, once per run
    int coinRecycleProgress, heartRecycleProgress;        // orbs recycled toward the next coin / heart
    int levelCoins, pendingChoices, choiceIndex, choiceTotal, refundTotal, offerVersion;
    readonly List<UpgradeType> offer = new List<UpgradeType>();
    Character character = Character.None;
    int appliedLayout = -1;
    int mysteryIndex = -1;   // index in 'offer' of the mystery-box card, or -1

    public int MaxHealth => startingLives + upgMaxHealth;
    public int CoinsPerLevel => character == Character.Challenger ? challengerCoins : coinsPerLevel;
    int BallArmorCap => character == Character.Sluggish ? sluggishMaxBallArmor : maxBallArmorUpgrades;
    /// <summary>Ball Armor purchases banked so far (one per N levels reached) that haven't been spent.</summary>
    int BallArmorBanked => Mathf.Min(BallArmorCap, level / Mathf.Max(1, levelsPerBallArmor)) - upgBallArmor;
    public static UpgradeTier TierOf(UpgradeType u)
    {
        switch (u)
        {
            case UpgradeType.Challenger:
            case UpgradeType.Sluggish:
            case UpgradeType.Spaceship: return UpgradeTier.Character;
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
        : c == Character.Sluggish ? UpgradeType.Sluggish : UpgradeType.Spaceship;
    /// <summary>Most Thrust / Torque upgrades allowed (only limited by The Sluggish).</summary>
    int ThrustTorqueCap => character == Character.Sluggish ? Mathf.Max(0, sluggishUpgradeCap) : int.MaxValue;
    /// <summary>Thrust + Torque upgrades above the Sluggish cap (refunded as extra picks when it is taken).</summary>
    /// <summary>
    /// Challenger: one extra pick per level beaten (upgrades happen right after beating 'level'),
    /// plus one per ball upgrade bought (they are all taken away).
    /// </summary>
    int ChallengerRefunds => Mathf.Max(0, level) + upgBallArmor + upgBallSize;
    int SluggishRefunds =>Mathf.Max(0, upgThrust - sluggishUpgradeCap) + Mathf.Max(0, upgTorque - sluggishUpgradeCap);

    // ------------------------------------------------------- read-only state for the UI

    public int Lives => lives;
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
    public int SavedLevel => savedLevel;
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

    PendulumController pendulum;
    EnemySpawner spawner;
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
        savedLevel = Mathf.Max(1, PlayerPrefs.GetInt(LevelKey, 1));
        bestLevel = Mathf.Max(savedLevel, PlayerPrefs.GetInt(BestKey, 1));
        LoadUpgrades();
        ApplyUpgrades();
        lives = MaxHealth;
        SetState(State.Title);
    }

    // ------------------------------------------------------------- upgrades

    void LoadUpgrades()
    {
        upgMaxHealth = PlayerPrefs.GetInt(UpgradeKeyPrefix + "MaxHealth", 0);
        upgSpeed = PlayerPrefs.GetInt(UpgradeKeyPrefix + "Speed", 0);
        upgThrust = PlayerPrefs.GetInt(UpgradeKeyPrefix + "Thrust", 0);
        upgTorque = PlayerPrefs.GetInt(UpgradeKeyPrefix + "Torque", 0);
        character = (Character)Mathf.Clamp(PlayerPrefs.GetInt(UpgradeKeyPrefix + "GameChanger", 0), 0, 3);
        upgBallArmor = Mathf.Min(BallArmorCap, PlayerPrefs.GetInt(UpgradeKeyPrefix + "BallArmor", 0));
        upgThrust = Mathf.Min(upgThrust, ThrustTorqueCap);
        upgTorque = Mathf.Min(upgTorque, ThrustTorqueCap);
        upgBallSize = Mathf.Clamp(PlayerPrefs.GetInt(UpgradeKeyPrefix + "BallSize", 0), 0, maxBallSizeUpgrades);
        upgPickupRange = Mathf.Clamp(PlayerPrefs.GetInt(UpgradeKeyPrefix + "PickupRange", 0), 0, maxPickupRangeUpgrades);
        upgRepel = Mathf.Clamp(PlayerPrefs.GetInt(UpgradeKeyPrefix + "Repel", 0), 0, maxRepelUpgrades);
        upgBooster = Mathf.Clamp(PlayerPrefs.GetInt(UpgradeKeyPrefix + "Booster", 0), 0, maxBoosterUpgrades);
        upgRegen = Mathf.Clamp(PlayerPrefs.GetInt(UpgradeKeyPrefix + "Regeneration", 0), 0, 1);
        upgFireRate = Mathf.Clamp(PlayerPrefs.GetInt(UpgradeKeyPrefix + "FireRate", 0), 0, maxFireRateUpgrades);
        upgTurretRange = Mathf.Clamp(PlayerPrefs.GetInt(UpgradeKeyPrefix + "TurretRange", 0), 0, maxTurretRangeUpgrades);
        upgInsight = Mathf.Clamp(PlayerPrefs.GetInt(UpgradeKeyPrefix + "Insight", 0), 0, 1);
        upgCoinRecycler = Mathf.Clamp(PlayerPrefs.GetInt(UpgradeKeyPrefix + "CoinRecycler", 0), 0, 1);
        upgHealthRecycler = Mathf.Clamp(PlayerPrefs.GetInt(UpgradeKeyPrefix + "HealthRecycler", 0), 0, 1);
        coinRecycleProgress = Mathf.Max(0, PlayerPrefs.GetInt(UpgradeKeyPrefix + "CoinRecycleProgress", 0));
        heartRecycleProgress = Mathf.Max(0, PlayerPrefs.GetInt(UpgradeKeyPrefix + "HeartRecycleProgress", 0));
    }

    void SaveUpgrades()
    {
        PlayerPrefs.SetInt(UpgradeKeyPrefix + "MaxHealth", upgMaxHealth);
        PlayerPrefs.SetInt(UpgradeKeyPrefix + "Speed", upgSpeed);
        PlayerPrefs.SetInt(UpgradeKeyPrefix + "Thrust", upgThrust);
        PlayerPrefs.SetInt(UpgradeKeyPrefix + "Torque", upgTorque);
        PlayerPrefs.SetInt(UpgradeKeyPrefix + "BallArmor", upgBallArmor);
        PlayerPrefs.SetInt(UpgradeKeyPrefix + "GameChanger", (int)character);
        PlayerPrefs.SetInt(UpgradeKeyPrefix + "BallSize", upgBallSize);
        PlayerPrefs.SetInt(UpgradeKeyPrefix + "PickupRange", upgPickupRange);
        PlayerPrefs.SetInt(UpgradeKeyPrefix + "Repel", upgRepel);
        PlayerPrefs.SetInt(UpgradeKeyPrefix + "Booster", upgBooster);
        PlayerPrefs.SetInt(UpgradeKeyPrefix + "Regeneration", upgRegen);
        PlayerPrefs.SetInt(UpgradeKeyPrefix + "FireRate", upgFireRate);
        PlayerPrefs.SetInt(UpgradeKeyPrefix + "TurretRange", upgTurretRange);
        PlayerPrefs.SetInt(UpgradeKeyPrefix + "Insight", upgInsight);
        PlayerPrefs.SetInt(UpgradeKeyPrefix + "CoinRecycler", upgCoinRecycler);
        PlayerPrefs.SetInt(UpgradeKeyPrefix + "HealthRecycler", upgHealthRecycler);
        PlayerPrefs.SetInt(UpgradeKeyPrefix + "CoinRecycleProgress", coinRecycleProgress);
        PlayerPrefs.SetInt(UpgradeKeyPrefix + "HeartRecycleProgress", heartRecycleProgress);
        PlayerPrefs.Save();
    }

    void ResetUpgrades()
    {
        lastPickWasExtra = false;
        upgMaxHealth = upgSpeed = upgThrust = upgTorque = upgBallArmor = 0;
        upgBallSize = upgPickupRange = upgRepel = upgBooster = upgRegen = 0;
        upgFireRate = upgTurretRange = 0;
        upgInsight = upgCoinRecycler = upgHealthRecycler = 0;
        coinRecycleProgress = heartRecycleProgress = 0;
        character = Character.None;
        SaveUpgrades();
        ApplyUpgrades();
    }

    void ApplyUpgrades()
    {
        if (pendulum == null) return;

        // Characters reshape the orbitals.
        if (appliedLayout != (int)character)
        {
            appliedLayout = (int)character;
            int[] caps = pendulum.BaseShellCapacities;
            float[] lens = pendulum.BaseShellLengths;
            // Spaceship: the inner orbital's slots become turrets (its balls convert first).
            pendulum.SetTurretSlots(character == Character.Spaceship && caps.Length > 0 ? caps[0] : 0);
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
            else if (character == Character.Sluggish)
            {
                int n = caps.Length;
                var newCaps = new int[n + 1];
                var newLens = new float[n + 1];
                for (int i = 0; i < n; i++)
                {
                    newCaps[i] = caps[i];
                    newLens[i] = lens[i] * sluggishTighten;                 // tighter orbitals
                }
                newCaps[n] = sluggishExtraShellCapacity;                    // one more orbital
                newLens[n] = n >= 2 ? newLens[n - 1] + (newLens[n - 1] - newLens[n - 2])
                                    : (n == 1 ? newLens[0] + 0.8f : 1f);
                pendulum.SetShellLayout(newCaps, newLens);
            }
            else
            {
                pendulum.SetShellLayout(caps, lens);
            }
        }

        int thrust = Mathf.Min(upgThrust, ThrustTorqueCap);   // capped by The Sluggish
        int torque = Mathf.Min(upgTorque, ThrustTorqueCap);
        pendulum.ApplyUpgrades(
            1f + upgSpeed * speedPerUpgrade,
            1f + thrust * thrustPerUpgrade,
            torque * torquePerUpgrade,
            torque * turnAuthorityPerTorque,
            character == Character.Challenger ? 0 : upgBallArmor);
        pendulum.ApplyExtraUpgrades(
            1f + upgBallSize * ballSizePerUpgrade,
            upgPickupRange * pickupRangePerUpgrade,
            upgRepel,
            upgBooster);
        pendulum.ApplyTurretUpgrades(
            1f + upgFireRate * fireRatePerUpgrade,
            upgTurretRange * turretRangePerUpgrade);
    }

    void BeginUpgradeChoices()
    {
        pendingChoices = levelCoins;
        choiceTotal = levelCoins;
        refundTotal = 0;
        choiceIndex = 0;
        levelCoins = 0;
        RollOffer();
        Time.timeScale = 0f;
        SetState(State.Upgrade);
    }

    /// <summary>
    /// Normal upgrades come in two groups (not shown differently on the cards):
    /// Essentials (repair, max health, top speed, thrust, torque) and Extras (everything else).
    /// </summary>
    public static bool IsEssential(UpgradeType u) =>
        u == UpgradeType.Heal || u == UpgradeType.MaxHealth || u == UpgradeType.Speed
        || u == UpgradeType.Thrust || u == UpgradeType.Torque;
    public static bool IsExtra(UpgradeType u) => TierOf(u) == UpgradeTier.Normal && !IsEssential(u);

    bool lastPickWasExtra;   // the next offer then holds at most one Extra

    void RollOffer()
    {
        offerVersion++;

        // Essentials.
        var essentials = new List<UpgradeType> { UpgradeType.MaxHealth, UpgradeType.Speed };
        if (upgThrust < ThrustTorqueCap) essentials.Add(UpgradeType.Thrust);   // capped by The Sluggish
        if (upgTorque < ThrustTorqueCap) essentials.Add(UpgradeType.Torque);
        if (lives <= MaxHealth - 2) essentials.Add(UpgradeType.Heal);   // only when down at least two hearts

        // Extras.
        var extras = new List<UpgradeType>();
        // Ball Armor: only if one is banked (one per N levels), under the cap, and balls exist at all.
        if (character != Character.Challenger && BallArmorBanked > 0) extras.Add(UpgradeType.BallArmor);
        if (character != Character.Challenger && upgBallSize < maxBallSizeUpgrades) extras.Add(UpgradeType.BallSize);
        if (upgPickupRange < maxPickupRangeUpgrades) extras.Add(UpgradeType.PickupRange);
        if (upgRepel < maxRepelUpgrades) extras.Add(UpgradeType.Repel);
        if (upgBooster < maxBoosterUpgrades) extras.Add(UpgradeType.Booster);
        if (character == Character.Spaceship)
        {
            if (upgFireRate < maxFireRateUpgrades) extras.Add(UpgradeType.FireRate);
            if (upgTurretRange < maxTurretRangeUpgrades) extras.Add(UpgradeType.TurretRange);
        }

        offer.Clear();
        // At least one Essential, always.
        if (essentials.Count > 0)
        {
            int k = Random.Range(0, essentials.Count);
            offer.Add(essentials[k]);
            essentials.RemoveAt(k);
        }
        // Fill the rest from both groups; right after an Extra was picked, at most one Extra.
        int extraLimit = lastPickWasExtra ? 1 : int.MaxValue;
        int extraCount = 0;
        while (offer.Count < upgradeChoices)
        {
            bool extrasAllowed = extraCount < extraLimit && extras.Count > 0;
            int total = essentials.Count + (extrasAllowed ? extras.Count : 0);
            if (total == 0) break;
            int k = Random.Range(0, total);
            if (k < essentials.Count) { offer.Add(essentials[k]); essentials.RemoveAt(k); }
            else { k -= essentials.Count; offer.Add(extras[k]); extras.RemoveAt(k); extraCount++; }
        }
        // Shuffle so the guaranteed Essential isn't always the first card.
        for (int i = offer.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            UpgradeType t = offer[i]; offer[i] = offer[j]; offer[j] = t;
        }

        // Character mystery box: only on the first pick, only if every coin of the level was
        // collected (it costs two), only from the unlock level, and only once per run.
        mysteryIndex = -1;
        bool eligible = character == Character.None && level >= characterUnlockLevel
                        && choiceIndex == 0 && pendingChoices >= 2 && choiceTotal >= CoinsPerLevel;
        if (eligible && Random.value < characterChance)
        {
            UpgradeType[] characters = { UpgradeType.Challenger, UpgradeType.Sluggish, UpgradeType.Spaceship };
            UpgradeType gc = characters[Random.Range(0, characters.Length)];
            if (offer.Count >= upgradeChoices)
            {
                int slot = ReplaceableSlot(-1);
                if (slot >= 0) { mysteryIndex = slot; offer[slot] = gc; }
            }
            else { offer.Add(gc); mysteryIndex = offer.Count - 1; }
        }

        // Rare card: replaces one normal card (never the mystery box). Each rare is once per run.
        var rares = new List<UpgradeType>();
        if (upgRegen == 0) rares.Add(UpgradeType.Regeneration);
        if (upgInsight == 0) rares.Add(UpgradeType.Insight);
        if (upgCoinRecycler == 0) rares.Add(UpgradeType.CoinRecycler);
        if (upgHealthRecycler == 0) rares.Add(UpgradeType.HealthRecycler);
        if (rares.Count > 0 && level >= rareUnlockLevel && Random.value < rareChance)
        {
            UpgradeType rare = rares[Random.Range(0, rares.Count)];
            if (offer.Count < upgradeChoices) offer.Add(rare);
            else
            {
                int slot = ReplaceableSlot(mysteryIndex);
                if (slot >= 0) offer[slot] = rare;
            }
        }
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

    /// <summary>Picks card 'index' of the current offer (ignored while it can't be picked yet).</summary>
    public void ChooseUpgrade(int index)
    {
        if (state != State.Upgrade || index < 0 || index >= offer.Count || stateTime < 0.35f) return;
        if (index == mysteryIndex && stateTime < mysteryRevealTime) return;   // still sealed

        UpgradeType picked = offer[index];
        int cost = IsCharacter(picked) ? 2 : 1;
        lastPickWasExtra = IsExtra(picked);
        switch (picked)
        {
            case UpgradeType.Heal: lives = MaxHealth; break;
            case UpgradeType.MaxHealth: upgMaxHealth++; lives++; break;
            case UpgradeType.Speed: upgSpeed++; break;
            case UpgradeType.Thrust: upgThrust++; break;
            case UpgradeType.Torque: upgTorque++; break;
            case UpgradeType.BallArmor: upgBallArmor = Mathf.Min(BallArmorCap, upgBallArmor + 1); break;
            case UpgradeType.BallSize: upgBallSize = Mathf.Min(maxBallSizeUpgrades, upgBallSize + 1); break;
            case UpgradeType.PickupRange: upgPickupRange = Mathf.Min(maxPickupRangeUpgrades, upgPickupRange + 1); break;
            case UpgradeType.Repel: upgRepel = Mathf.Min(maxRepelUpgrades, upgRepel + 1); break;
            case UpgradeType.Booster: upgBooster = Mathf.Min(maxBoosterUpgrades, upgBooster + 1); break;
            case UpgradeType.Regeneration: upgRegen = 1; break;
            case UpgradeType.Insight: upgInsight = 1; break;
            case UpgradeType.CoinRecycler: upgCoinRecycler = 1; break;
            case UpgradeType.HealthRecycler: upgHealthRecycler = 1; break;
            case UpgradeType.FireRate: upgFireRate = Mathf.Min(maxFireRateUpgrades, upgFireRate + 1); break;
            case UpgradeType.TurretRange: upgTurretRange = Mathf.Min(maxTurretRangeUpgrades, upgTurretRange + 1); break;
            case UpgradeType.Spaceship: character = Character.Spaceship; break;
            case UpgradeType.Challenger:
            {
                // One pick per level beaten so far, plus every ball upgrade, which is lost.
                int refunds = ChallengerRefunds;
                character = Character.Challenger;
                upgBallArmor = 0;
                upgBallSize = 0;
                pendingChoices += refunds;
                refundTotal += refunds;
                break;
            }
            case UpgradeType.Sluggish:
            {
                // Thrust & Torque are capped; every level above the cap is refunded as an extra pick.
                int refunds = SluggishRefunds;
                character = Character.Sluggish;
                upgThrust = Mathf.Min(upgThrust, ThrustTorqueCap);
                upgTorque = Mathf.Min(upgTorque, ThrustTorqueCap);
                pendingChoices += refunds;
                refundTotal += refunds;
                break;
            }
        }
        SaveUpgrades();
        ApplyUpgrades();
        Sfx.Play(IsCharacter(picked) ? "level" : "orb");

        pendingChoices -= cost;
        choiceIndex += cost;
        if (pendingChoices > 0)
        {
            RollOffer();
            SetState(State.Upgrade);   // resets the click guard timer
        }
        else
        {
            Time.timeScale = 1f;
            StartLevel(level + 1, false);
        }
    }

    public static string UpgradeTitle(UpgradeType u)
    {
        switch (u)
        {
            case UpgradeType.Heal: return "FULL REPAIR";
            case UpgradeType.MaxHealth: return "+1 MAX HEALTH";
            case UpgradeType.Speed: return "TOP SPEED";
            case UpgradeType.Thrust: return "THRUST";
            case UpgradeType.Torque: return "TORQUE";
            case UpgradeType.Challenger: return "THE CHALLENGER";
            case UpgradeType.Sluggish: return "THE SLUGGISH";
            case UpgradeType.BallSize: return "BIGGER BALLS";
            case UpgradeType.PickupRange: return "PICKUP RANGE";
            case UpgradeType.Repel: return "REPEL";
            case UpgradeType.Booster: return "EMERGENCY BOOSTER";
            case UpgradeType.Regeneration: return "REGENERATION";
            case UpgradeType.Insight: return "INSIGHT";
            case UpgradeType.CoinRecycler: return "COIN RECYCLER";
            case UpgradeType.HealthRecycler: return "HEALTH RECYCLER";
            case UpgradeType.Spaceship: return "THE SPACESHIP";
            case UpgradeType.FireRate: return "FIRE RATE";
            case UpgradeType.TurretRange: return "TURRET RANGE";
            default: return "BALL ARMOR";
        }
    }

    /// <summary>Upgrade levels are shown 1-based: no upgrades = level 1.</summary>
    string LevelStep(int upgrades, int cap)
    {
        string s = "(level " + (upgrades + 1) + " → " + (upgrades + 2);
        if (cap != int.MaxValue) s += ", max " + (cap + 1);
        return s + ")";
    }

    /// <summary>For unlockable abilities: level 0 = locked, so the first purchase reads "(unlock)".</summary>
    static string UnlockStep(int upgrades, int cap) =>
        upgrades == 0 ? "(unlock)" : "(level " + upgrades + " → " + (upgrades + 1) + ", max " + cap + ")";

    public string UpgradeDescription(UpgradeType u)
    {
        switch (u)
        {
            case UpgradeType.Heal: return "Restore health to max\n(" + lives + " → " + MaxHealth + ")";
            case UpgradeType.MaxHealth: return "Permanently gain\none extra heart";
            case UpgradeType.Speed: return "+" + Mathf.RoundToInt(speedPerUpgrade * 100) + "% max speed\n" + LevelStep(upgSpeed, int.MaxValue);
            case UpgradeType.Thrust: return "+" + Mathf.RoundToInt(thrustPerUpgrade * 100) + "% acceleration\n" + LevelStep(upgThrust, ThrustTorqueCap);
            case UpgradeType.Torque: return "Sharper turns\nand braking\n" + LevelStep(upgTorque, ThrustTorqueCap);
            case UpgradeType.Challenger:
                return "Lose ALL balls and\nball upgrades, forever.\n" + challengerCoins + " coins per level\ninstead of " + coinsPerLevel + "."
                     + "\nRefunds " + ChallengerRefunds + (ChallengerRefunds == 1 ? " pick" : " picks") + "\n(1 per level beaten"
                     + (upgBallArmor + upgBallSize > 0 ? ",\n+" + (upgBallArmor + upgBallSize) + " for ball upgrades)." : ").");
            case UpgradeType.Sluggish:
            {
                int refunds = SluggishRefunds;
                return "Thrust & Torque capped\nat level " + (sluggishUpgradeCap + 1) + "."
                     + (refunds > 0 ? "\nRefunds " + refunds + (refunds == 1 ? " pick." : " picks.") : "")
                     + "\nTighter orbitals, a 3rd\nshell (+" + sluggishExtraShellCapacity + "), ball armor\nup to " + (1 + sluggishMaxBallArmor)
                     + " hits, " + sluggishOrbMultiplier + "× orbs.";
            }
            case UpgradeType.BallSize:
                return "+" + Mathf.RoundToInt(ballSizePerUpgrade * 100) + "% ball size:\nblocks more, grabs more\n" + LevelStep(upgBallSize, maxBallSizeUpgrades);
            case UpgradeType.PickupRange:
                return "Collect pickups from\nmuch further away\n(thruster and balls)\n" + LevelStep(upgPickupRange, maxPickupRangeUpgrades);
            case UpgradeType.Repel:
                return (upgRepel == 0 ? "Getting hit releases\na blast that pushes\nenemies away"
                                      : "Wider, stronger\nrepel blast")
                     + "\n" + UnlockStep(upgRepel, maxRepelUpgrades);
            case UpgradeType.Booster:
                return (upgBooster == 0 ? "Getting hit fires a\nspeed boost past\nyour top speed"
                                        : "Longer, stronger\nemergency boost")
                     + "\n" + UnlockStep(upgBooster, maxBoosterUpgrades);
            case UpgradeType.Regeneration:
                return "Regain one heart\nat the start of\nevery level";
            case UpgradeType.Insight:
                return "See asteroids and\ngravity wells coming\nfrom off-screen";
            case UpgradeType.CoinRecycler:
                return "Orbs you have no\nroom for are recycled:\nevery " + OrbsPerRecycledCoin + " make a coin";
            case UpgradeType.HealthRecycler:
                return "Orbs you have no\nroom for are recycled\nwhile hurt: every " + OrbsPerRecycledHeart + "\nrestore a heart";
            case UpgradeType.Spaceship:
                return "Your inner orbital\nbecomes two turrets\nthat shoot enemies.\nOrbs build them; they\ncan't be hit or block."
                     + "\nUnlocks Fire Rate and\nTurret Range upgrades.";
            case UpgradeType.FireRate:
                return "+" + Mathf.RoundToInt(fireRatePerUpgrade * 100) + "% turret\nshots per second\n" + LevelStep(upgFireRate, maxFireRateUpgrades);
            case UpgradeType.TurretRange:
                return "Turrets aim and shoot\n+" + turretRangePerUpgrade.ToString("0.#") + " units further\n" + LevelStep(upgTurretRange, maxTurretRangeUpgrades);
            default: return "Each ball takes\none more hit\n(" + (1 + upgBallArmor) + " → " + (2 + upgBallArmor) + " hits, max " + (1 + BallArmorCap) + ")";
        }
    }

    public static Color UpgradeColor(UpgradeType u)
    {
        switch (u)
        {
            case UpgradeType.Heal:
            case UpgradeType.MaxHealth: return Pickup.HeartColor;
            case UpgradeType.Speed: return new Color(0.5f, 1f, 0.6f);
            case UpgradeType.Thrust: return new Color(1f, 0.6f, 0.25f);
            case UpgradeType.Torque: return new Color(0.75f, 0.6f, 1f);
            case UpgradeType.Challenger: return new Color(1f, 0.3f, 0.3f);
            case UpgradeType.Sluggish: return new Color(0.45f, 0.75f, 1f);
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
            default: return Pickup.OrbColor;
        }
    }

    // --------------------------------------------------------------- levels

    void SetState(State s)
    {
        state = s;
        stateTime = 0f;
    }

    /// <summary>3, 3, 4, 4, 5, 5, ... with the default settings.</summary>
    int StarsForLevel(int l)
    {
        int every = Mathf.Max(1, levelsPerExtraStar);
        return baseStarsPerLevel + (l + every - 1) / every;
    }

    /// <summary>+1 orb at levels 3, 6, 9, ... (default); The Sluggish multiplies the total.</summary>
    int OrbsForLevel(int l)
    {
        int n = orbsPerLevel + l / Mathf.Max(1, levelsPerExtraOrb);
        if (character == Character.Sluggish) n *= Mathf.Max(1, sluggishOrbMultiplier);
        return n;
    }

    /// <param name="hardReset">Start fresh: full health, no balls, stopped (used after dying / from the title).
    /// Otherwise health and balls carry over from the previous level.</param>
    void StartLevel(int l, bool hardReset)
    {
        level = Mathf.Max(1, l);
        if (hardReset) lives = MaxHealth;
        else if (upgRegen > 0 && lives < MaxHealth)
        {
            lives++;   // Regeneration (rare upgrade)
            Sfx.Play("orb");
            ParticleBursts.Burst(pendulum.PivotPosition, Pickup.HeartColor, 24, 6f, 0.3f, 0.8f);
        }
        levelCoins = 0;
        heartAvailable = Random.value < heartChancePerLevel;
        heartTimer = Random.Range(heartMinInterval, heartMaxInterval);
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
        spawner.Begin(level);
        TopUpStars();
        for (int i = 0; i < CoinsPerLevel; i++)
            extras.Add(SpawnPickup(PickupKind.Coin, coinMinDistance, coinMaxDistance));
        // A fixed number of orbs, all placed at the start (none if the player can't have balls).
        if (pendulum.Capacity > 0)
            for (int i = 0, n = OrbsForLevel(level); i < n; i++)
                orbs.Add(SpawnPickup(PickupKind.Orb, orbMinDistance, orbMaxDistance));

        savedLevel = level;
        bestLevel = Mathf.Max(bestLevel, level);
        PlayerPrefs.SetInt(LevelKey, savedLevel);
        PlayerPrefs.SetInt(BestKey, bestLevel);
        PlayerPrefs.Save();

        Sfx.Play("start");
        SetState(State.Playing);
    }

    void CompleteLevel()
    {
        spawner.ClearAll(true);
        PopLeftoverPickups();
        Sfx.Play("level");
        AddShake(0.3f);
        ParticleBursts.Burst(pendulum.PivotPosition, Pickup.Gold, 40, 9f, 0.35f, 1f);

        savedLevel = level + 1;
        bestLevel = Mathf.Max(bestLevel, savedLevel);
        PlayerPrefs.SetInt(LevelKey, savedLevel);
        PlayerPrefs.SetInt(BestKey, bestLevel);
        PlayerPrefs.Save();

        SetState(State.LevelComplete);
    }

    void EndGame()
    {
        spawner.StopSpawning();
        pendulum.Kill();
        AddShake(0.9f);
        Sfx.Play("over");
        SetState(State.GameOver);
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

        var go = new GameObject(kind.ToString());
        go.transform.position = pos;
        var p = go.AddComponent<Pickup>();
        p.Init(kind, OnPickupCollected);
        return p;
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
                Sfx.Play("orb");
                ParticleBursts.Burst(pendulum.PivotPosition, Pickup.HeartColor, 24, 6f, 0.3f, 0.8f);
                GameUI.FloatText(at, "+1 HEART", Pickup.HeartColor, 26f);
            }
            else
            {
                Sfx.Play("recycle");
                GameUI.FloatText(at, "RECYCLED  " + heartRecycleProgress + "/" + OrbsPerRecycledHeart, Pickup.HeartColor);
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
                GameUI.FloatText(at, "+1 COIN", Pickup.CoinColor, 26f);
            }
            else
            {
                Sfx.Play("recycle");
                GameUI.FloatText(at, "RECYCLED  " + coinRecycleProgress + "/" + OrbsPerRecycledCoin, Pickup.CoinColor);
            }
        }
        SaveUpgrades();
    }

    /// <summary>An orb touched with no room for it and nothing to recycle it into.</summary>
    public void WarnOrbitalsFull(Vector2 at)
    {
        Sfx.Play("full", 0.7f);
        GameUI.FloatText(at, "ORBITALS FULL", new Color(1f, 0.25f, 0.25f), 24f);
    }

    void OnPickupCollected(Pickup p)
    {
        stars.Remove(p);
        orbs.Remove(p);
        extras.Remove(p);
        if (state != State.Playing) return;

        switch (p.Kind)
        {
            case PickupKind.Orb:
                if (pendulum.IsFull) RecycleOrb(p.transform.position);
                else if (pendulum.AddBall()) Sfx.Play("orb");
                return;
            case PickupKind.Coin:
                levelCoins++;
                Sfx.Play("coin");
                return;
            case PickupKind.Heart:
                lives = Mathf.Min(MaxHealth, lives + 1);
                Sfx.Play("orb");
                return;
        }

        starsCollected++;
        Sfx.Play("star");
        AddShake(0.15f);
        if (starsCollected >= starsNeeded) CompleteLevel();
        else TopUpStars();
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
        AddShake(0.5f);
        hitStop = 0.08f;
        Sfx.Play("hit");
        if (lives <= 0) EndGame();
    }

    public void AddShake(float amount) => shake = Mathf.Max(shake, amount);

    // --------------------------------------------------------------- update

    void Update()
    {
        float udt = Time.unscaledDeltaTime;
        stateTime += udt;
        var kb = Keyboard.current;

        switch (state)
        {
            case State.Title:
                if (kb != null && (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame))
                {
                    ResetUpgrades();   // starting over = fresh run
                    StartLevel(1, true);
                }
                else if (StartPressed()) StartLevel(savedLevel, true);
                break;
            case State.Playing:
                levelTime += Time.deltaTime;
                UpdateHazards(Time.deltaTime);
                UpdateHearts(Time.deltaTime);
                if (PausePressed()) SetPaused(true);
                break;
            case State.Upgrade:
                if (kb != null)
                {
                    if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) ChooseUpgrade(0);
                    else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) ChooseUpgrade(1);
                    else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) ChooseUpgrade(2);
                }
                break;
            case State.Paused:
                if (kb != null && kb.rKey.wasPressedThisFrame) { SetPaused(false); StartLevel(level, true); }
                else if (PausePressed() || StartPressed()) SetPaused(false);
                break;
            case State.LevelComplete:
                if (stateTime > levelCompleteDelay)
                {
                    if (levelCoins > 0) BeginUpgradeChoices();
                    else StartLevel(level + 1, false);
                }
                break;
            case State.GameOver:
                if (stateTime > 0.8f && (StartPressed() || (kb != null && kb.rKey.wasPressedThisFrame))) StartLevel(level, true);
                break;
        }

        if (state != State.Paused && state != State.Upgrade)
        {
            if (hitStop > 0f)
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

    static bool StartPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            return true;
        var m = Mouse.current;
        if (m != null && m.leftButton.wasPressedThisFrame) return true;
        var ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) return true;
        return false;
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
