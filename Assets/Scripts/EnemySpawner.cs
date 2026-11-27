using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Spawns enemies from any direction around the screen. Each wave is telegraphed by
/// a SpawnWarning, then the enemies fly in straight lines through a point near the
/// player. Enemies get faster every level.
///
/// Types: Shard (small, fast) and Brute (large, slow, 2 hit points), plus rare Missiles,
/// Lightning Flashes and Broken Thrusters (dash in bursts, 2 hit points).
/// Bundles: formations of Shards with their own warning cue; larger at higher levels.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Pacing")]
    public float firstSpawnDelay = 1.5f;
    [Tooltip("Seconds between waves on level 1.")]
    public float startInterval = 1.8f;
    [Tooltip("Interval is multiplied by this for every level after 1.")]
    public float intervalMultiplierPerLevel = 0.92f;
    [Tooltip("How fast the interval shrinks while you stay in a level (it approaches 0, never reaches it).")]
    public float intervalDropPerSecond = 0.003f;

    [Header("Warning Sounds")]
    [Tooltip("Seconds between waves at which free radicals and alpha particles play the lowest approach sound.")]
    public float approachSlowInterval = 1.8f;
    [Tooltip("Seconds between waves at which they play the highest one (the sounds in between split the range evenly).")]
    public float approachFastInterval = 0.4f;

    [Header("Endless Scaling")]
    [Tooltip("Nothing is capped: every per-level stat keeps growing forever, just more slowly over time. "
           + "After this many levels, a stat's growth per level has halved (after 3x this many, it's a quarter).")]
    public float growthSoftness = 20f;

    [Header("Enemy Speed")]
    [Tooltip("Base speed on level 1 (before the per-type multiplier).")]
    public float baseSpeed = 3.6f;
    [Tooltip("Base speed grows by this fraction every level (0.08 = +8% per level).")]
    public float speedGrowthPerLevel = 0.08f;
    public float speedGainPerSecond = 0.008f;
    public float shardSpeedMultiplier = 1.45f;
    [Tooltip("Shard size (visual and hitbox), single and in bundles. 1 = the original small shard.")]
    [Range(0.5f, 3f)] public float shardSize = 1.5f;
    public float bruteSpeedMultiplier = 0.6f;
    [Tooltip("Broken Thruster: speed right after its first push (later pushes get weaker).")]
    [FormerlySerializedAs("brokenPushSpeedMultiplier")]
    public float brokenSpeedMultiplier = 2.8f;
    [Tooltip("Lightning Flash: dash speed (x base speed). 8.9 = the old fixed 32 at level 1.")]
    public float flashSpeedMultiplier = 8.9f;
    [Tooltip("Gamma ray: how many things (electrons or the core) it can hit before it is spent. 3 = it pierces two, the third stops it.")]
    public int gammaHits = 3;

    [Header("Types")]
    [Tooltip("Alpha particles grow this much bigger every level (x1 on level 1; growth slows down but never stops).")]
    public float bruteSizePerLevel = 0.04f;
    [Tooltip("Chance a single enemy is a Brute instead of a Shard.")]
    [Range(0f, 1f)] public float bruteChance = 0.25f;
    [Range(0f, 1f)] public float maxBruteChance = 0.45f;
    public float bruteChancePerLevel = 0.02f;

    [Header("Rare: Homing Missile")]
    public int missileUnlockLevel = 3;
    [Range(0f, 1f)] public float missileChance = 0.07f;
    [Range(0f, 1f)] public float maxMissileChance = 0.14f;
    public float missileChancePerLevel = 0.01f;
    public float missileSpeedMultiplier = 1.25f;
    [Tooltip("Missile speed multiplier grows by this every level after it unlocks.")]
    public float missileSpeedPerLevel = 0.02f;
    [Tooltip("How fast the missile can turn (deg/s) when it first unlocks.")]
    public float missileTurnRate = 20f;
    public float missileTurnRatePerLevel = 1f;
    [Tooltip("The turn rate never grows past this (deg/s).")]
    public float missileMaxTurnRate = 35f;
    [Tooltip("Once the atom is more than this many degrees off its heading, the positron gives up and flies on straight.")]
    public float missileGiveUpAngle = 75f;

    [Header("Rare: Lightning Flash")]
    public int flashUnlockLevel = 4;
    [Range(0f, 1f)] public float flashChance = 0.05f;
    [Range(0f, 1f)] public float maxFlashChance = 0.12f;
    public float flashChancePerLevel = 0.01f;

    [Header("Rare: Broken Thruster")]
    public int brokenUnlockLevel = 7;
    [Range(0f, 1f)] public float brokenChance = 0.08f;
    [Range(0f, 1f)] public float maxBrokenChance = 0.16f;
    public float brokenChancePerLevel = 0.01f;
    [Tooltip("Seconds between its pushes.")]
    public float brokenPushInterval = 1.2f;
    [Tooltip("It aims each push at where the atom was this many seconds ago (not where it is now), so moving dodges it.")]
    public float brokenAimDelay = 0.45f;
    [Tooltip("How quickly it slows down between pushes (higher = shorter coasts).")]
    public float brokenCoastDrag = 2.2f;
    [Tooltip("Seconds it keeps pushing before its engine dies (on its unlock level).")]
    public float brokenActiveTime = 6f;
    public float brokenActiveTimePerLevel = 0.5f;
    [Tooltip("Each push is this fraction of the previous one (on its unlock level). Lower = weakens faster.")]
    [Range(0.5f, 1f)] public float brokenPushFalloff = 0.85f;
    [Tooltip("The falloff fraction rises by this every level (pushes weaken more slowly).")]
    public float brokenPushFalloffPerLevel = 0.015f;

    [Header("Bundles")]
    public int bundleUnlockLevel = 2;
    [Range(0f, 1f)] public float bundleChance = 0.15f;
    public float bundleChancePerLevel = 0.03f;
    public int bundleStartSize = 3;
    [Tooltip("Bundles grow by one enemy every this many levels.")]
    public int levelsPerExtraBundleEnemy = 2;
    public float bundleSpacing = 0.5f;
    [Tooltip("Bundles get this much extra warning time.")]
    public float bundleExtraWarning = 0.4f;
    public float bundleSpeedMultiplier = 1.15f;

    [Header("Double Waves")]
    public int doubleUnlockLevel = 3;
    [Range(0f, 1f)] public float doubleChance = 0.15f;
    public float doubleChancePerLevel = 0.04f;

    [Header("Attraction (a bigger atom pulls in more particles)")]
    [Tooltip("Electrons the atom can hold before it starts attracting extra enemies.")]
    public int attractionFreeElectrons = 4;
    [Tooltip("Waves come this much more often per electron above the free amount (0.03: 20 extra electrons = 1.6x as often).")]
    public float attractionRatePerElectron = 0.03f;
    [Tooltip("Chance an enemy flies straight at the core, added per extra electron.")]
    public float attractionAimPerElectron = 0.012f;
    [Range(0f, 1f)] public float attractionMaxAimedChance = 0.75f;
    [Tooltip("Chance a wave is a bundle, added per extra electron (once bundles are unlocked).")]
    public float attractionBundlePerElectron = 0.008f;

    [Header("Event: Gamma Rain")]
    public int gammaRainUnlockLevel = 6;
    [Tooltip("Chance a level has a Gamma Rain (on the unlock level; grows per level).")]
    [Range(0f, 1f)] public float gammaRainChance = 0.3f;
    public float gammaRainChancePerLevel = 0.04f;
    [Tooltip("Seconds into the level it starts (random in this range).")]
    public Vector2 gammaRainStart = new Vector2(8f, 20f);
    [Tooltip("Seconds the warning is on screen before the first ray. Normal waves pause meanwhile.")]
    public float gammaRainWarning = 2.5f;
    public int gammaRainRays = 6;
    public float gammaRainRaysPerLevel = 0.5f;
    [Tooltip("Seconds over which the rays appear.")]
    public float gammaRainDuration = 3.5f;
    [Tooltip("Rays aim this close to the core (fraction of the reach of the electrons).")]
    public float gammaRainSpread = 0.8f;
    [Tooltip("Seconds a rain ray aims before it locks (normal gamma rays: 1).")]
    public float gammaRainAimTime = 0.7f;

    [Header("Warnings")]
    public float startWarningTime = 1.1f;
    [Tooltip("Shrinks by about this much per level at first (approaches 0, never reaches it).")]
    public float warningDropPerLevel = 0.05f;

    [Header("Aim")]
    [Tooltip("How far (as a fraction of the balls' reach) random paths can pass from the thruster.")]
    public float laneSpread = 0.9f;
    [Range(0f, 1f)] public float aimedChance = 0.35f;
    [Tooltip("Paths aim ahead of a moving player by this many seconds of its velocity (x warning time).")]
    public float leadFactor = 0.6f;

    [Header("Impact")]
    public float impactForce = 6f;

    [Header("Colors")]
    public Color shardColor = new Color(1f, 0.45f, 0.25f);
    public Color bruteColor = new Color(0.85f, 0.25f, 0.6f);
    public Color bundleColor = new Color(1f, 0.25f, 0.25f);
    public Color missileColor = new Color(0.55f, 1f, 0.35f);
    public Color flashColor = new Color(1f, 0.95f, 0.3f);
    public Color brokenColor = new Color(0.7f, 0.55f, 1f);

    [Header("References")]
    public PendulumController pendulumController;

    bool running;
    int level = 1;
    float elapsed, timer;

    enum Rain { None, Waiting, Warning, Raining }
    Rain rain;
    float rainClock, rainAt, rainSide;
    int rainSpawned, rainTotal;
    public bool GammaRainActive => rain == Rain.Warning || rain == Rain.Raining;
    readonly List<GameObject> enemies = new List<GameObject>();
    readonly List<GameObject> warnings = new List<GameObject>();

    void Start()
    {
        if (pendulumController == null) pendulumController = PendulumController.Instance;
        if (pendulumController == null) pendulumController = FindFirstObjectByType<PendulumController>();
    }

    // -------------------------------------------------------------- control

    public void Begin(int levelNumber)
    {
        ClearAll(false);
        level = Mathf.Max(1, levelNumber);
        running = true;
        elapsed = 0f;
        timer = firstSpawnDelay;

        rain = Rain.None;
        if (level >= gammaRainUnlockLevel
            && Random.value < GrowFraction(gammaRainChance, gammaRainChancePerLevel, level - gammaRainUnlockLevel))
        {
            rain = Rain.Waiting;
            rainClock = 0f;
            rainAt = Random.Range(gammaRainStart.x, gammaRainStart.y);
        }
    }

    /// <summary>Drops every pending spawn warning (and what it would have spawned); spawning carries on.</summary>
    public void CancelWarnings()
    {
        foreach (var w in warnings) if (w != null) Destroy(w);
        warnings.Clear();
    }

    public void StopSpawning()
    {
        running = false;
        rain = Rain.None;
        foreach (var w in warnings) if (w != null) Destroy(w);
        warnings.Clear();
    }

    /// <summary>Removes all enemies and pending warnings (optionally popping enemies with a burst).</summary>
    public void ClearAll(bool burst)
    {
        StopSpawning();
        foreach (var e in enemies)
        {
            if (e == null) continue;
            if (burst)
            {
                var m = e.GetComponent<EnemyMover>();
                ParticleBursts.Burst(e.transform.position, m != null ? m.color : Color.white, 10, 4f, 0.3f, 0.5f);
            }
            Destroy(e);
        }
        enemies.Clear();
    }

    // --------------------------------------------------------------- update

    void Update()
    {
        if (running)
        {
            elapsed += Time.deltaTime;
            UpdateRain(Time.deltaTime);
            timer -= Time.deltaTime;   // normal waves keep coming during the rain too
            if (timer <= 0f)
            {
                SpawnWave();
                timer = WaveInterval();
            }
        }
        enemies.RemoveAll(e => e == null);
        warnings.RemoveAll(w => w == null);
    }

    /// <summary>Seconds between waves right now.</summary>
    float WaveInterval()
    {
        float interval = startInterval * Mathf.Pow(intervalMultiplierPerLevel, Soft(level - 1));
        interval *= Mathf.Exp(-elapsed * intervalDropPerSecond / Mathf.Max(0.01f, interval));
        interval /= 1f + attractionRatePerElectron * ExtraElectrons;   // a bigger atom pulls harder
        return interval;
    }

    // -------------------------------------------------------- warning sounds

    /// <summary>
    /// How fast enemies are spawning, 0..1 between approachSlowInterval and approachFastInterval
    /// (on a log scale, so each of the approach sounds covers the same ratio of speed-up).
    /// </summary>
    float SpawnPace()
    {
        float slow = Mathf.Max(0.01f, approachSlowInterval);
        float fast = Mathf.Clamp(approachFastInterval, 0.005f, slow * 0.99f);
        return Mathf.Clamp01(Mathf.Log(slow / Mathf.Max(0.001f, WaveInterval())) / Mathf.Log(slow / fast));
    }

    /// <summary>
    /// Isotopes and positrons give a short taste of their own sound; everything else plays an
    /// approach sound that climbs in pitch as the spawning speeds up. (Gamma rays need none:
    /// they appear on screen and announce themselves with their charge-up.)
    /// </summary>
    void PlayWarningSound(EnemyType type, bool bundle)
    {
        switch (type)
        {
            case EnemyType.Broken:
                Sfx.PlayHint("sputter", 0.12f, 0.35f);
                break;
            case EnemyType.Missile:
                Sfx.PlayHint("lock", 0.05f, 0.45f, 2, 0.1f);   // two quick lock-on beeps
                break;
            default:
                Sfx.PlayApproach(SpawnPace(), bundle ? 1f : 0.8f);
                break;
        }
    }

    // ------------------------------------------------------------ attraction

    /// <summary>Electrons held above the free amount: each one draws in more (and better aimed) enemies.</summary>
    /// <summary>Set for the Lonely Ion: enemies scale by level alone (no attraction from the atom's size).</summary>
    [System.NonSerialized] public bool levelOnly;

    int ExtraElectrons => pendulumController == null || levelOnly ? 0
        : Mathf.Max(0, pendulumController.BallCount + pendulumController.TurretCount - attractionFreeElectrons);

    float AimedChance => Mathf.Min(Mathf.Max(aimedChance, attractionMaxAimedChance),
                                   aimedChance + attractionAimPerElectron * ExtraElectrons);

    // ------------------------------------------------------------ gamma rain

    void UpdateRain(float dt)
    {
        if (rain == Rain.None) return;
        rainClock += dt;
        switch (rain)
        {
            case Rain.Waiting:
                if (rainClock < rainAt) return;
                rain = Rain.Warning;
                rainClock = 0f;
                rainSide = Random.Range(0f, 360f);
                GameUI.Announce(Loc.T("GAMMA RAIN", "GAMA YAĞMURU"), flashColor, gammaRainWarning, "gammaRain");
                break;
            case Rain.Warning:
                if (rainClock < gammaRainWarning) return;
                rain = Rain.Raining;
                rainClock = 0f;
                rainSpawned = 0;
                rainTotal = Mathf.Max(1, Mathf.RoundToInt(Grow(gammaRainRays, gammaRainRaysPerLevel, level - gammaRainUnlockLevel)));
                break;
            case Rain.Raining:
                float every = gammaRainDuration / rainTotal;
                while (rainSpawned < rainTotal && rainClock >= rainSpawned * every)
                {
                    SpawnRainRay();
                    rainSpawned++;
                }
                if (rainSpawned >= rainTotal && rainClock >= gammaRainDuration + gammaRainAimTime + EnemyMover.FlashLockTime + 0.5f)
                {
                    rain = Rain.None;
                    timer = Mathf.Min(timer, 0.8f);   // the waves resume shortly after
                }
                break;
        }
    }

    /// <summary>
    /// One ray of the rain, aimed at a spot near the atom. The rays come from all around: each one
    /// a golden angle (~137.5 deg) on from the last, so no side is ever safe for long.
    /// </summary>
    void SpawnRainRay()
    {
        float angle = rainSide + rainSpawned * 137.508f + Random.Range(-12f, 12f);
        var mover = SpawnFlash(angle);
        if (mover == null) return;
        Vector2 pivot = pendulumController != null ? pendulumController.PivotPosition : GameManager.ViewCenter;
        float R = pendulumController != null ? pendulumController.ReachRadius : 1.5f;
        mover.rainStrike = true;
        mover.rainTarget = pivot + Random.insideUnitCircle * R * gammaRainSpread;
        mover.flashAimTime = gammaRainAimTime;
    }

    // -------------------------------------------------------------- spawning

    // ---- Endless scaling: no caps, growth slows down but never stops.

    /// <summary>"Effective levels" after n levels: n at first, then growing ever more slowly (but without limit).</summary>
    float Soft(float n) => n <= 0f ? 0f : growthSoftness * Mathf.Log(1f + n / Mathf.Max(0.01f, growthSoftness));

    /// <summary>Alpha particle size this level: 1 on level 1, a little bigger every level.</summary>
    float BruteScale => Grow(1f, bruteSizePerLevel, level - 1);

    /// <summary>start + perLevel for each (softened) level.</summary>
    float Grow(float start, float perLevel, float n) => start + perLevel * Soft(n);

    /// <summary>A fraction that grows by about perLevel per level at first and approaches 1 (never passes it).</summary>
    float GrowFraction(float start, float perLevel, float n)
    {
        float room = Mathf.Max(1e-4f, 1f - start);
        return 1f - room * Mathf.Exp(-perLevel * Soft(n) / room);
    }

    float LevelChance(float baseChance, float perLevel, int unlock) =>
        level < unlock ? 0f : GrowFraction(baseChance, perLevel, level - unlock);

    /// <summary>Enemy-type chances keep a hard cap so the mix stays varied.</summary>
    float TypeChance(float baseChance, float perLevel, float max, int unlock) =>
        Mathf.Min(max, LevelChance(baseChance, perLevel, unlock));

    float BaseSpeed => baseSpeed * (1f + speedGrowthPerLevel * Soft(level - 1)) + elapsed * speedGainPerSecond;

    void SpawnWave()
    {
        float angle = Random.Range(0f, 360f);
        float bundle = LevelChance(bundleChance, bundleChancePerLevel, bundleUnlockLevel);
        if (level >= bundleUnlockLevel) bundle = Mathf.Min(0.9f, bundle + attractionBundlePerElectron * ExtraElectrons);
        if (Random.value < bundle)
            QueueBundle(angle);
        else
            QueueSingle(angle);

        if (Random.value < LevelChance(doubleChance, doubleChancePerLevel, doubleUnlockLevel))
            QueueSingle(angle + Random.Range(90f, 270f));
    }

    float WarnTime => startWarningTime * Mathf.Exp(-warningDropPerLevel * Soft(level - 1) / Mathf.Max(0.01f, startWarningTime));

    void PickPath(float fromAngle, float warnTime, out Vector2 start, out Vector2 dir, out float pathLength)
    {
        Vector2 pivot = pendulumController != null ? pendulumController.PivotPosition : Vector2.zero;
        Vector2 pivotVel = pendulumController != null ? pendulumController.PivotVelocity : Vector2.zero;
        float R = pendulumController != null ? pendulumController.ReachRadius : 1.5f;
        Vector2 target = Random.value < AimedChance ? pivot : pivot + Random.insideUnitCircle * R * laneSpread;
        target += pivotVel * warnTime * leadFactor;

        float a = fromAngle * Mathf.Deg2Rad;
        dir = -new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        float D = GameManager.HalfExtents.magnitude + 2.5f;
        start = target - dir * D;
        pathLength = D * 2f;
    }

    void QueueSingle(float fromAngle)
    {
        // Rare specials first.
        if (Random.value < TypeChance(flashChance, flashChancePerLevel, maxFlashChance, flashUnlockLevel))
        {
            SpawnFlash(fromAngle);
            return;
        }

        EnemyType type;
        Color color;
        float speed;
        if (Random.value < TypeChance(brokenChance, brokenChancePerLevel, maxBrokenChance, brokenUnlockLevel))
        {
            type = EnemyType.Broken;
            color = brokenColor;
            speed = 0f;   // it moves only by its own pushes
        }
        else if (Random.value < TypeChance(missileChance, missileChancePerLevel, maxMissileChance, missileUnlockLevel))
        {
            type = EnemyType.Missile;
            color = missileColor;
            int over = Mathf.Max(0, level - missileUnlockLevel);
            speed = BaseSpeed * Grow(missileSpeedMultiplier, missileSpeedPerLevel, over);
        }
        else if (Random.value < TypeChance(bruteChance, bruteChancePerLevel, maxBruteChance, 1))
        {
            type = EnemyType.Brute;
            color = bruteColor;
            speed = BaseSpeed * bruteSpeedMultiplier;
        }
        else
        {
            type = EnemyType.Shard;
            color = shardColor;
            speed = BaseSpeed * shardSpeedMultiplier;
        }
        float warnTime = WarnTime;

        PickPath(fromAngle, warnTime, out Vector2 start, out Vector2 dir, out float len);
        var go = new GameObject("Warning");
        warnings.Add(go);
        float laneW = type == EnemyType.Brute ? 1.9f * BruteScale : 0f;   // huge enemy, wide lane
        go.AddComponent<SpawnWarning>().Init(start, dir, len, color, warnTime, 1, laneW,
            () => SpawnEnemy(type, color, start, dir, speed));
        PlayWarningSound(type, false);
    }

    void QueueBundle(float fromAngle)
    {
        int size = bundleStartSize + Mathf.FloorToInt(Soft(level - bundleUnlockLevel) / Mathf.Max(1, levelsPerExtraBundleEnemy));
        float speed = BaseSpeed * shardSpeedMultiplier * bundleSpeedMultiplier;
        float warnTime = WarnTime + bundleExtraWarning;
        float spacing = bundleSpacing * Mathf.Max(1f, shardSize);   // bigger shards need more room
        float width = (size - 1) * spacing;

        PickPath(fromAngle, warnTime, out Vector2 start, out Vector2 dir, out float len);
        Vector2 perp = new Vector2(-dir.y, dir.x);

        var go = new GameObject("BundleWarning");
        warnings.Add(go);
        go.AddComponent<SpawnWarning>().Init(start, dir, len, bundleColor, warnTime, size, width, () =>
        {
            // V formation, tip in front.
            for (int i = 0; i < size; i++)
            {
                float lat = (i - (size - 1) * 0.5f) * spacing;
                Vector2 p = start + perp * lat - dir * Mathf.Abs(lat) * 0.8f;
                SpawnEnemy(EnemyType.Shard, bundleColor, p, dir, speed);
            }
        });
        PlayWarningSound(EnemyType.Shard, true);
    }

    /// <summary>The Flash is its own telegraph: it appears just inside the screen edge and aims.</summary>
    EnemyMover SpawnFlash(float fromAngle)
    {
        if (!running) return null;
        Vector2 vc = GameManager.ViewCenter;
        Vector2 half = GameManager.HalfExtents - new Vector2(0.8f, 0.8f);
        float a = fromAngle * Mathf.Deg2Rad;
        Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        float k = Mathf.Min(half.x / Mathf.Max(Mathf.Abs(d.x), 1e-4f), half.y / Mathf.Max(Mathf.Abs(d.y), 1e-4f));
        return SpawnEnemy(EnemyType.Flash, flashColor, vc + d * k, -d, 0f);
    }

    EnemyMover SpawnEnemy(EnemyType type, Color color, Vector2 start, Vector2 dir, float speed)
    {
        if (!running) return null;
        var enemy = new GameObject(type.ToString());
        enemy.transform.position = start;
        var mover = enemy.AddComponent<EnemyMover>();
        mover.Setup(type, color, dir, speed, impactForce, pendulumController,
            type == EnemyType.Shard ? shardSize : type == EnemyType.Brute ? BruteScale : 1f);
        if (type == EnemyType.Broken)
        {
            mover.brokenPushInterval = Mathf.Max(0.1f, brokenPushInterval);
            mover.brokenAimDelay = Mathf.Max(0f, brokenAimDelay);
            int over = Mathf.Max(0, level - brokenUnlockLevel);
            mover.brokenPushSpeed = BaseSpeed * brokenSpeedMultiplier;
            mover.brokenCoastDrag = brokenCoastDrag;
            mover.brokenActiveTime = Grow(brokenActiveTime, brokenActiveTimePerLevel, over);
            mover.brokenPushFalloff = GrowFraction(brokenPushFalloff, brokenPushFalloffPerLevel, over);
            mover.brokenDriftSpeed = BaseSpeed * 0.5f;
        }
        if (type == EnemyType.Flash)
        {
            mover.flashDashSpeed = BaseSpeed * flashSpeedMultiplier;
            mover.flashHits = Mathf.Max(1, gammaHits);
        }
        if (type == EnemyType.Missile)
        {
            mover.missileTurnRate = Mathf.Min(missileMaxTurnRate,
                Grow(missileTurnRate, missileTurnRatePerLevel, Mathf.Max(0, level - missileUnlockLevel)));
            mover.missileGiveUpAngle = missileGiveUpAngle;
        }
        enemies.Add(enemy);
        return mover;
    }
}
