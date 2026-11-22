using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Characters added after the first three (traits and characters are the same thing).
/// Halogen and Alkali Metal are Characters (from the character card, one per run) whose powers
/// only hold while the atom's electron count is a real member of that element group:
///  - Halogen (F, Cl, Br, I, At, Ts): every electron on the map drifts toward it, its electron
///    pickup range doubles, and a free radical that touches it is destroyed and its electron taken.
///    Upgrades: Stronger Pull, Wider Grasp, Radical Snatch.
///  - Alkali metal (Li, Na, K, Rb, Cs, Fr): its outermost electron glows differently and can be
///    thrown with the ability button (see GameManager.Abilities and AlkaliShot).
///    Upgrades: Bigger Blast, Longer Throw, Wider Path.
/// And the Conductive character: conductivity builds up over time (to 5). An electron it would
/// lose becomes a zap instead (1 conductivity), hopping between enemies for 1 damage each. One tick
/// past full makes it overcharged: the next enemy that comes close is zapped for free.
/// </summary>
public partial class GameManager
{
    public enum ElementTrait { None, Halogen, Alkali }

    [Header("Halogen and Alkali Metal (characters)")]
    [Tooltip("Halogen: every map electron creeps toward the atom at this speed...")]
    public float halogenDriftSpeed = 0.5f;
    [Tooltip("...plus this / (1 + distance), so it speeds up close by.")]
    public float halogenDriftNear = 6f;
    [Tooltip("Halogen: electron pickup range is doubled, but at least this much extra reach.")]
    public float halogenMinPickupBonus = 1.2f;
    [Tooltip("Halogen Stronger Pull: drift speed multiplier added per upgrade.")]
    public float halogenPullPerUpgrade = 0.5f;
    [Tooltip("Halogen Wider Grasp: extra electron reach per upgrade (world units).")]
    public float halogenGraspPerUpgrade = 0.6f;
    [Tooltip("Halogen Radical Snatch: radicals this much further away get stolen, per upgrade.")]
    public float halogenSnatchPerUpgrade = 0.4f;
    public int maxHalogenUpgrades = 3;
    [Tooltip("Alkali: how far the thrown electron can be aimed (world units).")]
    public float alkaliThrowRange = 7f;
    public float alkaliThrowSpeed = 26f;
    [Tooltip("Alkali: enemies this close to the electron's path are destroyed.")]
    public float alkaliPathRadius = 0.7f;
    public float alkaliBlastRadius = 2.2f;
    public float alkaliCooldown = 1f;
    [Tooltip("Alkali: speed dust and black holes are pushed away at.")]
    public float alkaliHazardSpeed = 18f;
    [Tooltip("Alkali Bigger Blast: blast radius added per upgrade.")]
    public float alkaliBlastPerUpgrade = 0.6f;
    [Tooltip("Alkali Longer Throw: throw range added per upgrade.")]
    public float alkaliRangePerUpgrade = 2f;
    [Tooltip("Alkali Wider Path: path radius added per upgrade.")]
    public float alkaliPathPerUpgrade = 0.25f;
    public int maxAlkaliUpgrades = 3;

    [Header("Conductive (character)")]
    [Tooltip("Seconds per conductivity point.")]
    public float conductTick = 12f;
    [Tooltip("Fast Charge upgrade: seconds taken off the tick, each.")]
    public float conductTickPerUpgrade = 2f;
    [Tooltip("Zap: how far it can jump to the next enemy (world units).")]
    public float zapRange = 3f;
    public float zapRangePerUpgrade = 1f;
    [Tooltip("Zap: hops after the first target (1 = two enemies at most).")]
    public int zapHops = 1;
    public int maxConductUpgrades = 3;
    [Tooltip("Overcharged: an enemy this close to the core gets zapped.")]
    public float overchargeRadius = 2f;
    public const int MaxConductivity = 5;

    public static readonly Color HalogenColor = new Color(0.75f, 1f, 0.4f);
    public static readonly Color AlkaliColor = new Color(1f, 0.62f, 0.25f);
    public static readonly Color ConductiveColor = new Color(0.95f, 0.9f, 0.35f);
    public static readonly Color ExcitedColor = new Color(0.95f, 0.5f, 1f);

    [Header("Noble Atom (character)")]
    [Tooltip("Lab Lingering Nobility: seconds the unbreakable bonds last after the full shell is lost.")]
    public float nobleLingerTime = 5f;
    [Tooltip("Lab Proclamation of Nobility: radius of the wave (world units).")]
    public float proclaimRadius = 9f;
    [Tooltip("Proclamation: at most one wave every this many seconds.")]
    public float proclaimCooldown = 10f;
    [Tooltip("Proclamation: speed dust and black holes are pushed away at.")]
    public float proclaimHazardSpeed = 22f;

    static readonly int[] Halogens = { 9, 17, 35, 53, 85, 117 };
    static readonly int[] Alkalis = { 3, 11, 19, 37, 55, 87 };

    int upgHopRange, upgHopCount, upgChargeRate;
    int upgHalogenPull, upgHalogenGrasp, upgHalogenSnatch;
    int upgAlkaliBlast, upgAlkaliRange, upgAlkaliPath;
    bool wasNobleForm;
    ElementTrait lastTrait;
    float nobleLinger, proclaimTimer;
    int conductivity;
    bool overcharged;
    float conductTimer, crackleTimer;

    // ------------------------------------------------------- halogen / alkali metal

    /// <summary>
    /// The element power active right now: only for the Halogen / Alkali Metal character, and only
    /// while the electron count is a member of that group.
    /// </summary>
    public ElementTrait Trait
    {
        get
        {
            if (pendulum == null || !pendulum.IsAlive) return ElementTrait.None;
            ElementTrait want = character == Character.Halogen ? ElementTrait.Halogen
                              : character == Character.Alkali ? ElementTrait.Alkali : ElementTrait.None;
            return want != ElementTrait.None && TraitOf(pendulum.ElectronTotal) == want ? want : ElementTrait.None;
        }
    }

    public static ElementTrait TraitOf(int electrons) =>
        System.Array.IndexOf(Halogens, electrons) >= 0 ? ElementTrait.Halogen
        : System.Array.IndexOf(Alkalis, electrons) >= 0 ? ElementTrait.Alkali : ElementTrait.None;

    public bool IsHalogen => state == State.Playing && Trait == ElementTrait.Halogen;
    public bool IsAlkali => Trait == ElementTrait.Alkali;

    /// <summary>Halogen: drift speed of a map electron this far away.</summary>
    public float HalogenDrift(float distance) =>
        (halogenDriftSpeed + halogenDriftNear / (1f + distance)) * (1f + upgHalogenPull * halogenPullPerUpgrade);

    /// <summary>Halogen: reach added on top of the normal pickup reach, for electrons only.</summary>
    public float HalogenPickupExtra
    {
        get
        {
            float b = pendulum != null ? pendulum.PickupBonus : 0f;
            return Mathf.Max(b * 2f, halogenMinPickupBonus) - b + upgHalogenGrasp * halogenGraspPerUpgrade;
        }
    }

    /// <summary>Halogen: a free radical touching the atom is destroyed and its electron taken.</summary>
    public bool TryHalogenSteal(EnemyMover e)
    {
        if (!IsHalogen || e == null || e.IsDead || e.Type != EnemyType.Shard) return false;
        if (!pendulum.Contacts(e.Position, e.radius + upgHalogenSnatch * halogenSnatchPerUpgrade)) return false;
        Vector2 p = e.Position;
        e.Vaporize(HalogenColor);
        pendulum.AddBall();
        ParticleBursts.Burst(p, HalogenColor, 18, 6f, 0.25f, 0.5f);
        AbilityAim.Streak(p, pendulum.PivotPosition, 0.18f, HalogenColor, 0.3f);
        Sfx.Play("orb");
        Sfx.Play("lock", 0.5f);
        return true;
    }

    public float AlkaliThrowRange => alkaliThrowRange + upgAlkaliRange * alkaliRangePerUpgrade;
    public float AlkaliBlastRadius => alkaliBlastRadius + upgAlkaliBlast * alkaliBlastPerUpgrade;
    public float AlkaliPathRadius => alkaliPathRadius + upgAlkaliPath * alkaliPathPerUpgrade;

    // ------------------------------------------------------------- noble atom

    /// <summary>Noble Atom with a full outer shell (its electron count is a noble gas).</summary>
    public bool IsNobleForm => character == Character.Sluggish && pendulum != null && pendulum.IsAlive
                               && Elements.IsNoble(pendulum.ElectronTotal);
    /// <summary>The bonds can't be broken right now (full shell, or still lingering after it).</summary>
    public bool NobleUnbreakable => pendulum != null && pendulum.NobleBond > 0f;
    /// <summary>Lingering Nobility: how much of the lingering time is left (0..1).</summary>
    public float NobleLinger01 => nobleLingerTime > 0f ? Mathf.Clamp01(nobleLinger / nobleLingerTime) : 0f;

    void UpdateNoble(float dt)
    {
        proclaimTimer -= dt;
        bool now = IsNobleForm;
        if (now)
        {
            nobleLinger = Lab.Has(LabPerk.NobleLinger) ? nobleLingerTime : 0f;
            if (!wasNobleForm)
            {
                // The shell just closed: a gold flash, and the Proclamation if researched.
                ParticleBursts.Burst(pendulum.PivotPosition, PendulumController.NobleGold, 24, 6f, 0.25f, 0.5f);
                Sfx.Play("star", 0.6f);
                Teach(LessonKind.Noble, NobleLessonKey);   // the first time ever: what a full shell does
                if (Lab.Has(LabPerk.NobleProclaim) && proclaimTimer <= 0f)
                {
                    proclaimTimer = proclaimCooldown;
                    NobleWave.Spawn(pendulum.PivotPosition, proclaimRadius, proclaimHazardSpeed);
                    AddShake(0.45f);
                }
            }
        }
        else
        {
            if (wasNobleForm && nobleLinger <= 0f) Sfx.Play("snap", 0.5f);   // the bonds are breakable again
            nobleLinger = Mathf.Max(0f, nobleLinger - dt);
        }
        wasNobleForm = now;

        // Full strength while the shell is full; lingering fades and flickers as it runs out.
        float bond = 0f;
        if (now) bond = 1f;
        else if (nobleLinger > 0f)
        {
            float left = nobleLinger / Mathf.Max(0.01f, nobleLingerTime);
            float flicker = nobleLinger < 1.5f ? 0.5f + 0.5f * Mathf.Sin(Time.time * 22f) : 1f;
            bond = Mathf.Max(0.05f, (0.35f + 0.45f * left) * flicker);
        }
        pendulum.NobleBond = bond;
    }

    // ------------------------------------------------------------- conductive

    public bool IsConductive => character == Character.Conductive;
    public int Conductivity => conductivity;
    public bool Overcharged => overcharged;
    float ConductTickTime => Mathf.Max(1f, conductTick - upgChargeRate * conductTickPerUpgrade);
    float ZapRange => zapRange + upgHopRange * zapRangePerUpgrade;
    int ZapHops => zapHops + upgHopCount;

    void ResetTraits()
    {
        upgHopRange = upgHopCount = upgChargeRate = 0;
        upgHalogenPull = upgHalogenGrasp = upgHalogenSnatch = 0;
        upgAlkaliBlast = upgAlkaliRange = upgAlkaliPath = 0;
        wasNobleForm = false;
        lastTrait = ElementTrait.None;
        nobleLinger = proclaimTimer = 0f;
        if (pendulum != null) pendulum.NobleBond = 0f;
        ResetConductivity();
        AlkaliShot.ReturnAll();
    }

    void ResetConductivity()
    {
        conductivity = 0;
        overcharged = false;
        conductTimer = 0f;
    }

    /// <summary>Every frame of play.</summary>
    void UpdateTraits(float dt)
    {
        if (pendulum == null) return;
        // Alkali: the throwable electron stands out (none while it's away).
        pendulum.AlkaliHighlight = IsAlkali && !AlkaliShot.Out ? AlkaliColor : (Color?)null;
        // The first time ever a halogen / alkali metal power comes on, the game explains it.
        ElementTrait trait = Trait;
        if (trait != lastTrait)
        {
            if (trait == ElementTrait.Halogen) Teach(LessonKind.Halogen, HalogenLessonKey);
            else if (trait == ElementTrait.Alkali) Teach(LessonKind.Alkali, AlkaliLessonKey);
            lastTrait = trait;
        }
        UpdateNoble(dt);
        if (!IsConductive || !pendulum.IsAlive) return;

        if (!overcharged)
        {
            conductTimer += dt;
            if (conductTimer >= ConductTickTime)
            {
                conductTimer = 0f;
                if (conductivity < MaxConductivity)
                {
                    conductivity++;
                    Sfx.Play("lock", 0.35f);
                }
                else
                {
                    overcharged = true;
                    Sfx.Play("charge", 0.5f);
                    ParticleBursts.Burst(pendulum.PivotPosition, ConductiveColor, 20, 6f, 0.2f, 0.4f);
                }
            }
        }
        if (!overcharged) return;

        // A crackle around the core while it holds the charge.
        crackleTimer -= dt;
        if (crackleTimer <= 0f)
        {
            crackleTimer = Random.Range(0.08f, 0.2f);
            Vector2 at = pendulum.PivotPosition + Random.insideUnitCircle.normalized * Random.Range(0.3f, 0.6f);
            ParticleBursts.Emit(at, Random.insideUnitCircle * 2f, ConductiveColor, 0.08f, 0.15f, 2f);
        }

        EnemyMover near = NearestEnemy(pendulum.PivotPosition, overchargeRadius, null);
        if (near == null) return;
        overcharged = false;
        conductTimer = 0f;
        Zap(pendulum.PivotPosition, near);
    }

    /// <summary>
    /// An electron is about to be lost at 'at', taken by 'attacker' (null: crushed by dust or a
    /// black hole). With conductivity to spend it becomes a zap instead: the attacker is hit first,
    /// otherwise the nearest enemy in range. True if it zapped (the electron is gone either way).
    /// </summary>
    public bool TryConductiveZap(Vector2 at, EnemyMover attacker)
    {
        if (!IsConductive || conductivity <= 0 || state != State.Playing) return false;
        if (attacker == null && NearestEnemy(at, ZapRange, null) == null) return false;
        conductivity--;
        Zap(at, attacker);
        return true;
    }

    /// <summary>A bolt from 'from' to 'first' (or the nearest enemy), then hopping on: 1 damage each.</summary>
    void Zap(Vector2 from, EnemyMover first)
    {
        var hit = new List<EnemyMover>();
        var points = new List<Vector2> { from };
        int targets = 1 + ZapHops;
        Vector2 p = from;
        EnemyMover next = first != null ? first : NearestEnemy(from, ZapRange, hit);
        while (next != null && hit.Count < targets)
        {
            hit.Add(next);
            Vector2 q = next.Position;
            points.Add(q);
            next.Shot(q - p);
            p = q;
            next = NearestEnemy(p, ZapRange, hit);
        }
        ZapBolt.Spawn(points, ConductiveColor);
        Sfx.Play("zap", 0.7f);
        AddShake(0.1f);
    }

    static EnemyMover NearestEnemy(Vector2 p, float range, List<EnemyMover> exclude)
    {
        EnemyMover best = null;
        float bestD = float.MaxValue;
        foreach (var e in EnemyMover.All)
        {
            if (e == null || e.IsDead || (exclude != null && exclude.Contains(e))) continue;
            float d = Vector2.Distance(e.Position, p) - e.radius;
            if (d > range || d >= bestD) continue;
            bestD = d;
            best = e;
        }
        return best;
    }
}

/// <summary>A jagged lightning line through the given points that flickers and fades.</summary>
public class ZapBolt : MonoBehaviour
{
    const float Life = 0.28f;
    LineRenderer core, glow;
    List<Vector2> points;
    Color color;
    float age, rejag;
    static Material material;

    public static void Spawn(List<Vector2> points, Color color)
    {
        if (points == null || points.Count < 2) return;
        if (material == null) material = new Material(Shader.Find("Sprites/Default"));
        var go = new GameObject("ZapBolt");
        var z = go.AddComponent<ZapBolt>();
        z.points = new List<Vector2>(points);
        z.color = color;
        z.glow = z.Line(0.22f, 44);
        z.core = z.Line(0.07f, 45);
        z.Jag();
        foreach (var p in points) ParticleBursts.Burst(p, color, 8, 4f, 0.15f, 0.3f);
    }

    LineRenderer Line(float width, int order)
    {
        var go = new GameObject("Line");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = material;
        lr.widthMultiplier = width;
        lr.numCapVertices = 2;
        lr.sortingOrder = order;
        return lr;
    }

    /// <summary>Each segment broken into a few kinks, re-rolled now and then so it flickers.</summary>
    void Jag()
    {
        var pts = new List<Vector3>();
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 a = points[i], b = points[i + 1], d = b - a;
            Vector2 side = new Vector2(-d.y, d.x).normalized;
            int kinks = Mathf.Clamp(Mathf.CeilToInt(d.magnitude / 0.5f), 2, 10);
            for (int k = 0; k < kinks; k++)
            {
                float t = k / (float)kinks;
                float off = k == 0 ? 0f : Random.Range(-0.22f, 0.22f);
                pts.Add(a + d * t + side * off);
            }
        }
        pts.Add(points[points.Count - 1]);
        foreach (var lr in new[] { core, glow })
        {
            lr.positionCount = pts.Count;
            lr.SetPositions(pts.ToArray());
        }
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= Life) { Destroy(gameObject); return; }
        rejag -= Time.deltaTime;
        if (rejag <= 0f) { rejag = 0.05f; Jag(); }
        float a = 1f - age / Life;
        core.startColor = core.endColor = new Color(1f, 1f, 1f, a);
        glow.startColor = glow.endColor = new Color(color.r, color.g, color.b, 0.55f * a);
    }
}

/// <summary>
/// The alkali metal's thrown electron: flies to the aimed point destroying enemies and pushing
/// dust and black holes along its path, explodes there, then flies home harmlessly and rejoins
/// the atom. It still counts as the atom's while out (PendulumController.HeldOut).
/// </summary>
public class AlkaliShot : MonoBehaviour
{
    public static AlkaliShot Current { get; private set; }
    public static bool Out => Current != null;

    GameManager gm;
    Vector2 pos, target, dir;
    bool returning, finished;
    float speed;
    readonly HashSet<Hazard> shoved = new HashSet<Hazard>();
    static Material trailMaterial;

    public static void Launch(Vector2 from, Vector2 to, GameManager gm)
    {
        ReturnAll();
        var go = new GameObject("AlkaliShot");
        var s = go.AddComponent<AlkaliShot>();
        s.gm = gm;
        s.pos = from;
        s.target = to;
        Vector2 d = to - from;
        s.dir = d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.right;
        go.transform.position = from;
        Color c = GameManager.AlkaliColor;
        Sprite(go.transform, ProcSprites.Glow, new Color(c.r, c.g, c.b, 0.6f), 41, 1.1f);
        Sprite(go.transform, ProcSprites.Electron, c, 42, 0.42f);
        var trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.18f;
        trail.widthMultiplier = 0.28f;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        if (trailMaterial == null) trailMaterial = new Material(Shader.Find("Sprites/Default"));
        trail.sharedMaterial = trailMaterial;
        trail.startColor = new Color(c.r, c.g, c.b, 0.8f);
        trail.endColor = new Color(c.r, c.g, c.b, 0f);
        trail.sortingOrder = 40;
        Current = s;
    }

    static void Sprite(Transform parent, Sprite sprite, Color c, int order, float scale)
    {
        var go = new GameObject("Sprite");
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = c;
        sr.sortingOrder = order;
    }

    /// <summary>Level over, death, a new run: it rejoins the atom at once.</summary>
    public static void ReturnAll()
    {
        if (Current != null) Current.Finish();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        var pc = PendulumController.Instance;
        if (!returning)
        {
            Vector2 next = Vector2.MoveTowards(pos, target, gm.alkaliThrowSpeed * dt);
            HitAlong(pos, next);
            pos = next;
            if ((pos - target).sqrMagnitude < 1e-4f)
            {
                Explode();
                returning = true;
                speed = 4f;
            }
        }
        else
        {
            if (pc == null || !pc.IsAlive) { Finish(); return; }
            speed = Mathf.Min(speed + 70f * dt, 34f);
            Vector2 home = pc.PivotPosition;
            pos = Vector2.MoveTowards(pos, home, speed * dt);
            if ((pos - home).sqrMagnitude < 0.3f * 0.3f) { Finish(); return; }
        }
        transform.position = pos;
    }

    void HitAlong(Vector2 a, Vector2 b)
    {
        Color c = GameManager.AlkaliColor;
        for (int i = EnemyMover.All.Count - 1; i >= 0; i--)
        {
            var e = EnemyMover.All[i];
            if (e == null || e.IsDead) continue;
            if (SegmentDistance(e.Position, a, b) <= gm.AlkaliPathRadius + e.radius) e.Vaporize(c);
        }
        foreach (var h in Hazard.All)
        {
            if (h == null || shoved.Contains(h)) continue;
            if (SegmentDistance(h.transform.position, a, b) > gm.AlkaliPathRadius + h.radius) continue;
            shoved.Add(h);
            h.velocity = dir * gm.alkaliHazardSpeed + h.velocity * 0.2f;
            ParticleBursts.Burst(h.transform.position, c, 14, 6f, 0.25f, 0.45f);
        }
    }

    void Explode()
    {
        Color c = GameManager.AlkaliColor;
        float r = gm.AlkaliBlastRadius;
        for (int i = EnemyMover.All.Count - 1; i >= 0; i--)
        {
            var e = EnemyMover.All[i];
            if (e == null || e.IsDead) continue;
            if (Vector2.Distance(e.Position, pos) <= r + e.radius) e.Vaporize(c);
        }
        foreach (var h in Hazard.All)
        {
            if (h == null) continue;
            Vector2 d = (Vector2)h.transform.position - pos;
            if (d.magnitude > r + h.radius) continue;
            Vector2 away = d.sqrMagnitude > 1e-4f ? d.normalized : dir;
            h.velocity = away * gm.alkaliHazardSpeed + h.velocity * 0.2f;
        }
        ShockRing.Spawn(pos, r, c);
        ParticleBursts.Burst(pos, c, 36, 9f, 0.3f, 0.55f);
        ParticleBursts.Burst(pos, Color.white, 14, 6f, 0.2f, 0.35f);
        Sfx.Play("explode", 0.5f);
        gm.AddShake(0.3f);
    }

    static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    void Finish()
    {
        if (finished) return;
        finished = true;
        if (Current == this) Current = null;
        var pc = PendulumController.Instance;
        if (pc != null) pc.ReturnHeld();
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }
}

/// <summary>
/// Noble Atom, Lab Proclamation of Nobility: a huge gold wave that grows out of the atom,
/// vaporizing every enemy it passes and pushing dust and black holes away.
/// </summary>
public class NobleWave : MonoBehaviour
{
    const float Life = 0.6f;
    Vector2 center;
    float radius, hazardSpeed, t;
    readonly HashSet<Hazard> shoved = new HashSet<Hazard>();

    public static void Spawn(Vector2 at, float radius, float hazardSpeed)
    {
        var go = new GameObject("NobleWave");
        var w = go.AddComponent<NobleWave>();
        w.center = at;
        w.radius = radius;
        w.hazardSpeed = hazardSpeed;
        Color c = PendulumController.NobleGold;
        ShockRing.Spawn(at, radius, c);
        ShockRing.Spawn(at, radius * 0.7f, Color.white);
        ParticleBursts.Burst(at, c, 50, 12f, 0.3f, 0.7f);
        ParticleBursts.Burst(at, Color.white, 20, 8f, 0.2f, 0.4f);
        Sfx.Play("explode", 0.6f);
        Sfx.Play("level", 0.5f);
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / Life);
        float r = radius * (1f - (1f - k) * (1f - k) * (1f - k));   // same easing as the ShockRing
        Color c = PendulumController.NobleGold;
        for (int i = EnemyMover.All.Count - 1; i >= 0; i--)
        {
            var e = EnemyMover.All[i];
            if (e == null || e.IsDead) continue;
            if (Vector2.Distance(e.Position, center) <= r + e.radius) e.Vaporize(c);
        }
        foreach (var h in Hazard.All)
        {
            if (h == null || shoved.Contains(h)) continue;
            Vector2 d = (Vector2)h.transform.position - center;
            if (d.magnitude > r + h.radius) continue;
            shoved.Add(h);
            Vector2 away = d.sqrMagnitude > 1e-4f ? d.normalized : Random.insideUnitCircle.normalized;
            h.velocity = away * hazardSpeed + h.velocity * 0.2f;
            ParticleBursts.Burst(h.transform.position, c, 12, 6f, 0.25f, 0.45f);
        }
        if (t >= Life) Destroy(gameObject);
    }
}
