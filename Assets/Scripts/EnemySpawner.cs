using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns enemies from any direction around the screen. Each wave is telegraphed by
/// a SpawnWarning, then the enemies fly in straight lines through a point near the
/// player. Enemies get faster every level.
///
/// Types: Shard (small, fast) and Brute (large, slow, 2 hit points).
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
    public float minInterval = 0.5f;
    [Tooltip("Seconds removed from the interval per second spent in the level.")]
    public float intervalDropPerSecond = 0.003f;

    [Header("Enemy Speed")]
    [Tooltip("Base speed on level 1 (before the per-type multiplier).")]
    public float baseSpeed = 3.6f;
    [Tooltip("Base speed grows by this fraction every level (0.08 = +8% per level).")]
    public float speedGrowthPerLevel = 0.08f;
    public float speedGainPerSecond = 0.008f;
    public float maxBaseSpeed = 9f;
    public float shardSpeedMultiplier = 1.45f;
    public float bruteSpeedMultiplier = 0.6f;

    [Header("Types")]
    [Tooltip("Chance a single enemy is a Brute instead of a Shard.")]
    [Range(0f, 1f)] public float bruteChance = 0.25f;
    public float bruteChancePerLevel = 0.02f;
    [Range(0f, 1f)] public float maxBruteChance = 0.45f;

    [Header("Rare: Homing Missile")]
    public int missileUnlockLevel = 3;
    [Range(0f, 1f)] public float missileChance = 0.07f;
    public float missileChancePerLevel = 0.01f;
    [Range(0f, 1f)] public float maxMissileChance = 0.14f;
    public float missileSpeedMultiplier = 1.45f;
    [Tooltip("Missile speed multiplier grows by this every level after it unlocks.")]
    public float missileSpeedPerLevel = 0.03f;
    public float maxMissileSpeedMultiplier = 2f;
    [Tooltip("How fast the missile can turn (deg/s) when it first unlocks.")]
    public float missileTurnRate = 60f;
    public float missileTurnRatePerLevel = 4f;
    public float maxMissileTurnRate = 120f;

    [Header("Rare: Lightning Flash")]
    public int flashUnlockLevel = 4;
    [Range(0f, 1f)] public float flashChance = 0.05f;
    public float flashChancePerLevel = 0.01f;
    [Range(0f, 1f)] public float maxFlashChance = 0.12f;

    [Header("Bundles")]
    public int bundleUnlockLevel = 2;
    [Range(0f, 1f)] public float bundleChance = 0.15f;
    public float bundleChancePerLevel = 0.03f;
    [Range(0f, 1f)] public float maxBundleChance = 0.4f;
    public int bundleStartSize = 3;
    [Tooltip("Bundles grow by one enemy every this many levels.")]
    public int levelsPerExtraBundleEnemy = 2;
    public int maxBundleSize = 12;
    public float bundleSpacing = 0.5f;
    [Tooltip("Bundles get this much extra warning time.")]
    public float bundleExtraWarning = 0.4f;
    public float bundleSpeedMultiplier = 1.15f;

    [Header("Double Waves")]
    public int doubleUnlockLevel = 3;
    [Range(0f, 1f)] public float doubleChance = 0.15f;
    public float doubleChancePerLevel = 0.04f;
    [Range(0f, 1f)] public float maxDoubleChance = 0.4f;

    [Header("Warnings")]
    public float startWarningTime = 1.1f;
    public float warningDropPerLevel = 0.05f;
    public float minWarningTime = 0.6f;

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

    [Header("References")]
    public PendulumController pendulumController;

    bool running;
    int level = 1;
    float elapsed, timer;
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
    }

    public void StopSpawning()
    {
        running = false;
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
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                SpawnWave();
                float interval = startInterval * Mathf.Pow(intervalMultiplierPerLevel, level - 1) - elapsed * intervalDropPerSecond;
                timer = Mathf.Max(minInterval, interval);
            }
        }
        enemies.RemoveAll(e => e == null);
        warnings.RemoveAll(w => w == null);
    }

    // -------------------------------------------------------------- spawning

    float LevelChance(float baseChance, float perLevel, float max, int unlock) =>
        level < unlock ? 0f : Mathf.Min(max, baseChance + (level - unlock) * perLevel);

    float BaseSpeed => Mathf.Min(maxBaseSpeed,
        baseSpeed * (1f + speedGrowthPerLevel * (level - 1)) + elapsed * speedGainPerSecond);

    void SpawnWave()
    {
        float angle = Random.Range(0f, 360f);
        if (Random.value < LevelChance(bundleChance, bundleChancePerLevel, maxBundleChance, bundleUnlockLevel))
            QueueBundle(angle);
        else
            QueueSingle(angle);

        if (Random.value < LevelChance(doubleChance, doubleChancePerLevel, maxDoubleChance, doubleUnlockLevel))
            QueueSingle(angle + Random.Range(90f, 270f));
    }

    float WarnTime => Mathf.Max(minWarningTime, startWarningTime - (level - 1) * warningDropPerLevel);

    void PickPath(float fromAngle, float warnTime, out Vector2 start, out Vector2 dir, out float pathLength)
    {
        Vector2 pivot = pendulumController != null ? pendulumController.PivotPosition : Vector2.zero;
        Vector2 pivotVel = pendulumController != null ? pendulumController.PivotVelocity : Vector2.zero;
        float R = pendulumController != null ? pendulumController.ReachRadius : 1.5f;
        Vector2 target = Random.value < aimedChance ? pivot : pivot + Random.insideUnitCircle * R * laneSpread;
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
        if (Random.value < LevelChance(flashChance, flashChancePerLevel, maxFlashChance, flashUnlockLevel))
        {
            SpawnFlash(fromAngle);
            return;
        }

        EnemyType type;
        Color color;
        float speed;
        if (Random.value < LevelChance(missileChance, missileChancePerLevel, maxMissileChance, missileUnlockLevel))
        {
            type = EnemyType.Missile;
            color = missileColor;
            int over = Mathf.Max(0, level - missileUnlockLevel);
            speed = BaseSpeed * Mathf.Min(maxMissileSpeedMultiplier, missileSpeedMultiplier + over * missileSpeedPerLevel);
        }
        else if (Random.value < LevelChance(bruteChance, bruteChancePerLevel, maxBruteChance, 1))
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
        float laneW = type == EnemyType.Brute ? 1.9f : 0f;   // huge enemy, wide lane
        go.AddComponent<SpawnWarning>().Init(start, dir, len, color, warnTime, 1, laneW,
            () => SpawnEnemy(type, color, start, dir, speed));
    }

    void QueueBundle(float fromAngle)
    {
        int size = Mathf.Min(maxBundleSize,
            bundleStartSize + (level - bundleUnlockLevel) / Mathf.Max(1, levelsPerExtraBundleEnemy));
        float speed = BaseSpeed * shardSpeedMultiplier * bundleSpeedMultiplier;
        float warnTime = WarnTime + bundleExtraWarning;
        float width = (size - 1) * bundleSpacing;

        PickPath(fromAngle, warnTime, out Vector2 start, out Vector2 dir, out float len);
        Vector2 perp = new Vector2(-dir.y, dir.x);

        var go = new GameObject("BundleWarning");
        warnings.Add(go);
        go.AddComponent<SpawnWarning>().Init(start, dir, len, bundleColor, warnTime, size, width, () =>
        {
            // V formation, tip in front.
            for (int i = 0; i < size; i++)
            {
                float lat = (i - (size - 1) * 0.5f) * bundleSpacing;
                Vector2 p = start + perp * lat - dir * Mathf.Abs(lat) * 0.8f;
                SpawnEnemy(EnemyType.Shard, bundleColor, p, dir, speed);
            }
        });
    }

    /// <summary>The Flash is its own telegraph: it appears just inside the screen edge and aims.</summary>
    void SpawnFlash(float fromAngle)
    {
        if (!running) return;
        Vector2 vc = GameManager.ViewCenter;
        Vector2 half = GameManager.HalfExtents - new Vector2(0.8f, 0.8f);
        float a = fromAngle * Mathf.Deg2Rad;
        Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        float k = Mathf.Min(half.x / Mathf.Max(Mathf.Abs(d.x), 1e-4f), half.y / Mathf.Max(Mathf.Abs(d.y), 1e-4f));
        SpawnEnemy(EnemyType.Flash, flashColor, vc + d * k, -d, 0f);
    }

    void SpawnEnemy(EnemyType type, Color color, Vector2 start, Vector2 dir, float speed)
    {
        if (!running) return;
        var enemy = new GameObject(type.ToString());
        enemy.transform.position = start;
        var mover = enemy.AddComponent<EnemyMover>();
        mover.Setup(type, color, dir, speed, impactForce, pendulumController);
        if (type == EnemyType.Missile)
            mover.missileTurnRate = Mathf.Min(maxMissileTurnRate,
                missileTurnRate + Mathf.Max(0, level - missileUnlockLevel) * missileTurnRatePerLevel);
        enemies.Add(enemy);
    }
}
