using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shard:   small and fast, dies to any ball. The standard enemy.
/// Brute:   huge and slow, takes two ball hits (it barrels on after the first).
/// Missile: homes in on the thruster for as long as it lives, but turns slower than the player
///          can, so it can be outmaneuvered (a Repel blast breaks its lock briefly). Beeps while locked.
/// Flash:   appears on the screen edge, aims at the thruster, locks its aim, then strikes in a
///          straight line at lightning speed, piercing through up to 3 things (electrons or the
///          core), knocking electrons out of the atom. One strike, then it is gone.
///          Gamma Rain rays aim at a fixed spot near the atom instead of tracking it.
///
/// Electrons: an enemy that takes an electron's last hit snatches it. A free radical bonds with
/// it and the pair veers off and sinks away; an alpha particle carries it and keeps coming (with
/// two it is a stable helium atom and leaves); a positron forms positronium with it, the two
/// spiral together and annihilate in a flash. Gamma rays and an isotope's first hit just knock
/// the electron loose.
/// Broken Thruster: 2 hit points. No steady engine: every few seconds it sputters out one
///          push toward the thruster (it can only turn so far per push), then coasts and slows
///          down. Each push is weaker than the last, and after a while the engine dies and it drifts off.
/// </summary>
public enum EnemyType { Shard, Brute, Missile, Flash, Broken }

/// <summary>
/// An enemy. Touching a ball damages both (the ball loses a hit point, the enemy loses
/// one). Reaching the thruster costs the player a heart. (Circle-vs-circle checks.)
/// </summary>
public class EnemyMover : MonoBehaviour
{
    /// <summary>Every live enemy (used by the Repel blast).</summary>
    public static readonly List<EnemyMover> All = new List<EnemyMover>();

    [HideInInspector] public Vector2 moveDirection;
    [HideInInspector] public float speed;
    [HideInInspector] public float impactForce;
    [HideInInspector] public PendulumController pendulumController;
    [HideInInspector] public float radius = 0.2f;
    [HideInInspector] public Color color = Color.red;

    // Missile tuning.
    [HideInInspector] public float missileTurnRate = 60f;   // deg/s, set by the spawner (grows per level)
    [HideInInspector] public float missileGiveUpAngle = 90f; // the atom got this far off its heading: it stops chasing
    public static float MissileRelockTime = 1.2f;    // seconds without lock after a Repel blast

    // Flash tuning.
    public static float FlashAimTime = 1.0f;         // tracks the player
    public static float FlashLockTime = 0.4f;        // aim frozen, line flashes: dodge now!
    [HideInInspector] public float flashDashSpeed = 32f;   // set by the spawner
    [HideInInspector] public int flashHits = 3;            // things (electrons / the core) it can hit before it is spent
    [HideInInspector] public bool rainStrike;              // Gamma Rain: one strike at rainTarget, then gone
    [HideInInspector] public Vector2 rainTarget;
    [HideInInspector] public float flashAimTime = -1f;     // < 0: FlashAimTime

    // Broken Thruster tuning (set by the spawner before the first Update).
    [HideInInspector] public float brokenPushInterval = 1.2f;   // seconds between pushes
    [HideInInspector] public float brokenAimDelay = 0.45f;      // aims at where the atom was this long ago

    // Where the atom has been lately (time, position), for the isotope's delayed aim.
    readonly System.Collections.Generic.Queue<Vector3> aimHistory = new System.Collections.Generic.Queue<Vector3>();
    [HideInInspector] public float brokenPushSpeed = 10f;       // speed right after a push
    [HideInInspector] public float brokenCoastDrag = 2.2f;      // how fast it slows down between pushes
    [HideInInspector] public float brokenActiveTime = 6f;       // seconds of pushing before the engine dies
    [HideInInspector] public float brokenPushFalloff = 0.85f;   // each push is this fraction of the previous
    [HideInInspector] public float brokenDriftSpeed = 2f;       // speed it keeps after the engine dies

    public EnemyType Type => type;

    EnemyType type;
    Transform body;
    SpriteRenderer bodySR, core, glow;
    float spin, age, emitTimer, coreBase, bodyScale;
    int hp = 1;
    float hitCooldown, damageFlash;
    Vector2 knock;                       // Repel knock-back velocity (decays)
    const float KnockDrag = 2.5f;
    bool dead;                           // killed this frame (Destroy is deferred)

    /// <summary>Dead, or bonded and leaving (harmless, not a target any more).</summary>
    public bool IsDead => dead || bond != BondState.None;
    public Vector2 Position => transform.position;
    /// <summary>Current velocity, including any Repel knock-back.</summary>
    public Vector2 Velocity => moveDirection * speed + knock;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    // Missile state.
    bool locked;
    float relockTimer;
    float beepTimer;
    SpriteRenderer lockLight;

    // Flash state.
    enum FlashPhase { Aim, Lock, Dash }
    FlashPhase phase;
    float phaseTime;
    SpriteRenderer aimLine;
    SpriteRenderer radical;
    readonly List<object> pierced = new List<object>();   // electrons this strike already went through

    // Bonds: electrons this enemy snatched from the atom.
    class Captured
    {
        public Transform t;
        public SpriteRenderer body, glow;
        public Vector2 from;
        public float age, phase;
    }
    readonly List<Captured> captured = new List<Captured>();
    enum BondState { None, Leaving, Positronium }
    BondState bond;
    float bondTime, bondRadius, bondAngle;
    Vector2 bondMid, bondDrift;
    SpriteRenderer[] fadeRenderers;
    float[] fadeAlphas;
    const float LeaveTime = 1.8f, PositroniumTime = 0.6f;
    bool coreStruck;                                       // ... and whether it went through the core
    Vector2 lastPos;

    // Broken Thruster state.
    float pushTimer, kick;
    int pushes;
    bool gaveUp;

    public void Setup(EnemyType enemyType, Color enemyColor, Vector2 dir, float spd, float impact, PendulumController pc,
        float sizeScale = 1f)
    {
        type = enemyType;
        color = enemyColor;
        Codex.See(Codex.Enemy(type));
        moveDirection = dir.normalized;
        speed = spd;
        impactForce = impact;
        pendulumController = pc;

        Sprite bodySprite;
        switch (type)
        {
            case EnemyType.Brute:
                bodyScale = 1.9f * sizeScale;   // grows level by level (set by the spawner)
                radius = 0.82f * sizeScale;
                hp = 2;
                bodySprite = ProcSprites.Alpha;   // alpha particle
                spin = (Random.value < 0.5f ? -1f : 1f) * Random.Range(15f, 30f);
                impactForce *= 2f;
                break;
            case EnemyType.Missile:
                bodyScale = 0.5f;
                radius = 0.16f;
                hp = 1;
                bodySprite = ProcSprites.Positron;
                locked = true;
                break;
            case EnemyType.Flash:
                bodyScale = 0.85f;
                radius = 0.17f;
                hp = 1;
                bodySprite = ProcSprites.Gamma;   // gamma ray
                phase = FlashPhase.Aim;
                speed = 0f;
                break;
            case EnemyType.Broken:
                bodyScale = 0.8f;
                radius = 0.28f;
                hp = 2;
                bodySprite = ProcSprites.Isotope;   // unstable isotope
                pushTimer = 0.15f;   // first push right after it appears
                break;
            default:
                bodyScale = 0.42f * sizeScale;
                radius = 0.14f * sizeScale;
                hp = 1;
                bodySprite = ProcSprites.Shard;
                break;
        }

        glow = Child("Glow", ProcSprites.Glow, new Color(color.r, color.g, color.b, 0.4f), 19, bodyScale * 2.3f);
        bodySR = Child("Body", bodySprite, color, 20, bodyScale);
        body = bodySR.transform;
        coreBase = type == EnemyType.Brute ? bodyScale * 0.3f : bodyScale * 0.2f;
        core = Child("Core", ProcSprites.Ball, Color.Lerp(color, Color.white, 0.6f), 21, coreBase);

        if (type == EnemyType.Shard)   // free radical: its one unpaired electron whirls around it
            radical = Child("Unpaired", ProcSprites.Ball, Color.Lerp(color, Color.white, 0.7f), 22, bodyScale * 0.32f);
        if (type == EnemyType.Missile)
        {
            lockLight = Child("LockLight", ProcSprites.Ball, Color.clear, 22, 0.12f);
            core.color = Color.clear;
        }
        if (type == EnemyType.Flash)
        {
            aimLine = Child("AimLine", ProcSprites.Pixel, Color.clear, 18, 1f);
            Sfx.Play("charge");
            ParticleBursts.Burst(transform.position, color, 16, 4f, 0.25f, 0.5f);
        }
        lastPos = transform.position;
        FaceDirection();
    }

    SpriteRenderer Child(string name, Sprite sprite, Color c, int order, float scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = c;
        sr.sortingOrder = order;
        return sr;
    }

    void FaceDirection()
    {
        if (type == EnemyType.Brute) return;
        float ang = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        // The positron's "+" stays upright.
        body.localRotation = type == EnemyType.Missile ? Quaternion.identity : Quaternion.Euler(0f, 0f, ang);
        if (lockLight != null) lockLight.transform.localPosition = (Vector3)(moveDirection * bodyScale * 0.32f);
    }

    void Update()
    {
        if (dead) return;
        float dt = Time.deltaTime;
        if (bond != BondState.None)
        {
            UpdateBond(dt);
            return;
        }
        age += dt;
        hitCooldown -= dt;
        var pc = pendulumController;

        switch (type)
        {
            case EnemyType.Missile: UpdateMissile(dt, pc); break;
            case EnemyType.Flash: UpdateFlash(dt, pc); break;
            case EnemyType.Broken: UpdateBroken(dt, pc); break;
        }
        if (dead) return;   // a Gamma Rain ray that finished its strike

        // Gravity wells bend the flight path (speed stays the same).
        if (speed > 0f)
        {
            Vector2 pull = Vector2.zero;
            foreach (var h in Hazard.All) pull += h.PullAt(transform.position);
            if (pull != Vector2.zero)
            {
                moveDirection = (moveDirection * speed + pull * dt).normalized;
                FaceDirection();
            }
        }

        // Move in sub-steps no longer than the enemy's radius so fast enemies can't tunnel through.
        lastPos = transform.position;
        Vector2 delta = (moveDirection * speed + knock) * dt;
        knock *= Mathf.Exp(-KnockDrag * dt);
        float move = delta.magnitude;
        int steps = Mathf.Max(1, Mathf.CeilToInt(move / Mathf.Max(0.05f, radius)));
        for (int i = 0; i < steps; i++)
        {
            transform.position += (Vector3)(delta / steps);
            if (CheckCollisions(pc)) return;
        }

        UpdateCaptured(dt);
        if (spin != 0f) body.Rotate(0f, 0f, spin * dt);
        float pulseRate = type == EnemyType.Brute ? 5f : 20f;
        core.transform.localScale = Vector3.one * coreBase * (1f + 0.2f * Mathf.Sin(age * pulseRate));
        if (radical != null)
        {
            float ra = age * 14f;
            radical.transform.localPosition = new Vector3(Mathf.Cos(ra), Mathf.Sin(ra), 0f) * bodyScale * 0.7f;
        }

        damageFlash = Mathf.MoveTowards(damageFlash, 0f, dt * 4f);
        Color bc = Color.Lerp(color, Color.white, damageFlash);
        if ((type == EnemyType.Brute || type == EnemyType.Broken) && hp == 1) bc *= 0.75f;   // cracked / weakened
        if (type == EnemyType.Broken && gaveUp) bc = Color.Lerp(bc, new Color(0.5f, 0.5f, 0.55f), 0.6f);   // decayed: stable, inert
        bc.a = 1f;
        bodySR.color = bc;

        EmitExhaust(dt);

        // Flew past and far off-screen (or lived too long) -> remove.
        Vector2 p = transform.position;
        Vector2 rel = p - GameManager.ViewCenter;
        float far = GameManager.HalfExtents.magnitude + 4f;
        bool movingAway = Vector2.Dot(rel, moveDirection) > 0f && speed > 0f;
        if ((rel.sqrMagnitude > far * far && movingAway) || age > 40f)
        {
            dead = true;
            Destroy(gameObject);
        }
    }

    /// <summary>Ball and thruster contact. Returns true if this enemy was destroyed.</summary>
    bool CheckCollisions(PendulumController pc)
    {
        Vector2 p = transform.position;

        // Asteroids and gravity-well cores destroy enemies.
        foreach (var h in Hazard.All)
        {
            if (!h.Crushes(p, radius * 0.7f)) continue;
            ParticleBursts.Burst(p, color, type == EnemyType.Brute ? 30 : 10, 5f, 0.3f, 0.5f);
            dead = true; Destroy(gameObject);
            return true;
        }

        if (pc == null || !pc.IsAlive) return false;
        if (type == EnemyType.Flash) return CheckPierce(pc, p);
        var gm = GameManager.Instance;
        // Halogen: a free radical that touches the atom loses its electron to it, and is gone.
        if (gm != null && gm.TryHalogenSteal(this)) return true;

        // Electron contact: both take a hit; an electron out of hits is snatched (see the class notes).
        Vector2 takenPos = p;
        Color takenColor = Pickup.OrbColor;
        // A positron annihilates any electron it touches, however tightly bound (armor doesn't help).
        var hit = hitCooldown <= 0f ? pc.HitElectron(p, radius, out takenPos, out takenColor, null, type == EnemyType.Missile)
                                    : PendulumController.ElectronHit.None;
        // Conductive: the electron becomes a zap instead of being taken; the zap is the damage dealt.
        if (hit == PendulumController.ElectronHit.Taken && gm != null && gm.TryConductiveZap(takenPos, this))
        {
            if (dead) return true;
            hitCooldown = 0.3f;
            speed *= 0.85f;
        }
        else if (hit != PendulumController.ElectronHit.None)
        {
            if (type == EnemyType.Missile)
            {
                if (hit == PendulumController.ElectronHit.Taken)
                {
                    BeginPositronium(takenPos, takenColor);
                    return true;
                }
                ParticleBursts.Burst(p, Color.white, 22, 8f, 0.25f, 0.35f);   // armored electron: just the flash
                Sfx.Play("zap", 0.35f);
            }
            hp--;
            if (hit == PendulumController.ElectronHit.Taken)
            {
                if (type == EnemyType.Broken && hp > 0)   // the isotope's decay shakes it loose
                    LooseElectron.Spawn(takenPos, (takenPos - p).normalized * 3f + moveDirection * 2f, takenColor, ElectronSize);
                else
                    Capture(takenPos, takenColor);
            }
            if (captured.Count > 0 && (hp <= 0 || (type == EnemyType.Brute && captured.Count >= 2)))
            {
                BeginLeaving();   // bonded (an alpha with two electrons is helium): off it goes
                return true;
            }
            if (hp <= 0)
            {
                ParticleBursts.Burst(p, color, type == EnemyType.Brute ? 40 : 10, type == EnemyType.Brute ? 7f : 5f, 0.3f, 0.6f);
                if (type == EnemyType.Brute && GameManager.Instance != null) GameManager.Instance.AddShake(0.35f);
                dead = true; Destroy(gameObject);
                return true;
            }
            hitCooldown = 0.3f;   // don't chew through the same ball in consecutive frames
            damageFlash = 1f;
            speed *= 0.85f;
            ParticleBursts.Burst(p, color, 16, 5f, 0.3f, 0.5f);
        }

        // Reached the thruster: lose a heart.
        if (pc.CanBeHit)
        {
            float hitDist = radius + pc.CoreRadius;
            if ((pc.PivotPosition - p).sqrMagnitude < hitDist * hitDist)
            {
                pc.TakeHit(moveDirection * impactForce, color);
                ParticleBursts.Burst(p, color, 14, 5f, 0.25f, 0.5f);
                dead = true; Destroy(gameObject);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Gamma ray: goes straight through what it hits, spending one of its hits on each electron
    /// (each only once per strike) and on the core; gone once they are used up.
    /// </summary>
    bool CheckPierce(PendulumController pc, Vector2 p)
    {
        bool spent = false;
        var hit = pc.HitElectron(p, radius, out Vector2 takenPos, out Color takenColor, pierced);
        if (hit != PendulumController.ElectronHit.None)
        {
            ParticleBursts.Burst(p, Color.white, 12, 6f, 0.2f, 0.3f);
            var gm = GameManager.Instance;
            if (hit == PendulumController.ElectronHit.Taken && gm != null && gm.TryConductiveZap(takenPos, this))
            {
                if (dead) return true;   // Conductive: the electron zapped the ray itself
            }
            else if (hit == PendulumController.ElectronHit.Taken)   // ionized: knocked out along the ray
            {
                Vector2 side = new Vector2(-moveDirection.y, moveDirection.x) * Random.Range(-2.5f, 2.5f);
                LooseElectron.Spawn(takenPos, moveDirection * 8f + side, takenColor, ElectronSize);
            }
            spent = --flashHits <= 0;
        }
        if (!spent && !coreStruck && pc.CanBeHit)
        {
            float hitDist = radius + pc.CoreRadius;
            if ((pc.PivotPosition - p).sqrMagnitude < hitDist * hitDist)
            {
                coreStruck = true;
                pc.TakeHit(moveDirection * impactForce, color);
                ParticleBursts.Burst(p, color, 14, 5f, 0.25f, 0.5f);
                spent = --flashHits <= 0;
            }
        }
        if (!spent) return false;
        ParticleBursts.Burst(p, color, 16, 6f, 0.3f, 0.5f);
        dead = true;
        Destroy(gameObject);
        return true;
    }

    // ------------------------------------------------------------------ bonds

    float ElectronSize => pendulumController != null ? pendulumController.ElectronSize : 0.32f;
    float CaptureOrbit => type == EnemyType.Brute ? radius * 1.28f : Mathf.Max(radius * 1.9f, 0.34f);

    /// <summary>A snatched electron: snaps from where it was onto an orbit around this enemy.</summary>
    void Capture(Vector2 from, Color c)
    {
        var root = new GameObject("SnatchedElectron").transform;
        root.SetParent(transform, false);
        root.position = from;
        float size = ElectronSize;
        var cap = new Captured
        {
            t = root, from = from, phase = captured.Count * Mathf.PI,
            glow = Child("Glow", ProcSprites.Glow, new Color(c.r, c.g, c.b, 0.5f), 21, size * 2.8f),
            body = Child("Body", ProcSprites.Electron, c, 23, size),
        };
        cap.glow.transform.SetParent(root, false);
        cap.body.transform.SetParent(root, false);
        cap.glow.transform.localPosition = cap.body.transform.localPosition = Vector3.zero;
        captured.Add(cap);
        ParticleBursts.Burst(from, Color.Lerp(c, Color.white, 0.5f), 10, 3f, 0.2f, 0.35f);
    }

    void UpdateCaptured(float dt)
    {
        Vector2 pos = transform.position;
        float orbit = CaptureOrbit * transform.localScale.x;
        foreach (var c in captured)
        {
            c.age += dt;
            float k = Mathf.Clamp01(c.age / 0.22f);
            k = 1f - (1f - k) * (1f - k);
            float a = age * 7f + c.phase;
            Vector2 slot = pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * orbit;
            c.t.position = Vector2.Lerp(c.from, slot, k);
        }
    }

    /// <summary>Stops being an enemy: no more hits, not targeted, drawn behind everything.</summary>
    void BecomeHarmless(BondState state)
    {
        bond = state;
        bondTime = 0f;
        All.Remove(this);
        if (aimLine != null) aimLine.color = Color.clear;
        fadeRenderers = GetComponentsInChildren<SpriteRenderer>();
        fadeAlphas = new float[fadeRenderers.Length];
        for (int i = 0; i < fadeRenderers.Length; i++)
        {
            fadeAlphas[i] = fadeRenderers[i].color.a;
            fadeRenderers[i].sortingOrder -= 85;   // sink behind the atom and the other enemies
        }
    }

    /// <summary>The bonded pair veers away from the atom, slowing, shrinking and fading.</summary>
    void BeginLeaving()
    {
        BecomeHarmless(BondState.Leaving);
        speed = Mathf.Max(speed, 1.5f);
        Sfx.Play("lock", 0.6f);
    }

    /// <summary>Positron + electron: they circle each other ever tighter, then annihilate.</summary>
    void BeginPositronium(Vector2 electronPos, Color electronColor)
    {
        Capture(electronPos, electronColor);
        BecomeHarmless(BondState.Positronium);
        Vector2 pos = transform.position;
        bondMid = (pos + electronPos) * 0.5f;
        Vector2 d = pos - bondMid;
        bondRadius = Mathf.Max(0.18f, d.magnitude);
        bondAngle = Mathf.Atan2(d.y, d.x);
        bondDrift = moveDirection * speed * 0.25f;
        Sfx.Play("charge", 0.4f);
    }

    void UpdateBond(float dt)
    {
        bondTime += dt;
        if (bond == BondState.Positronium)
        {
            float k = Mathf.Clamp01(bondTime / PositroniumTime);
            bondMid += bondDrift * dt;
            bondDrift *= Mathf.Exp(-3f * dt);
            float r = bondRadius * Mathf.Pow(1f - k, 1.3f);
            bondAngle += Mathf.Lerp(8f, 45f, k * k) * dt;
            Vector2 dir = new Vector2(Mathf.Cos(bondAngle), Mathf.Sin(bondAngle));
            transform.position = bondMid + dir * r;
            if (captured.Count > 0) captured[0].t.position = bondMid - dir * r;
            if (glow != null) glow.transform.localScale = Vector3.one * bodyScale * 2.3f * (1f + 0.6f * k);
            if (k < 1f) return;

            // Annihilation: a flash, and two gamma photons flying apart in opposite directions.
            Vector2 a = Random.insideUnitCircle.normalized;
            ParticleBursts.Burst(bondMid, Color.white, 28, 9f, 0.3f, 0.4f);
            for (int i = 0; i < 3; i++)
            {
                Color photon = new Color(1f, 0.95f, 0.3f);
                ParticleBursts.Emit(bondMid, a * (16f - i * 3f), photon, 0.24f - i * 0.05f, 0.5f, 0.6f);
                ParticleBursts.Emit(bondMid, -a * (16f - i * 3f), photon, 0.24f - i * 0.05f, 0.5f, 0.6f);
            }
            Sfx.Play("zap", 0.6f);
            if (GameManager.Instance != null) GameManager.Instance.AddShake(0.12f);
            dead = true;
            Destroy(gameObject);
            return;
        }

        // Leaving: curve away from the atom, slow down, shrink and fade into the background.
        float t = Mathf.Clamp01(bondTime / LeaveTime);
        var pc = pendulumController;
        Vector2 p = transform.position;
        if (pc != null)
        {
            Vector2 away = p - pc.PivotPosition;
            if (away.sqrMagnitude > 1e-4f)
                moveDirection = Vector2.Lerp(moveDirection, away.normalized, 1f - Mathf.Exp(-2.5f * dt)).normalized;
        }
        speed = Mathf.Max(0.6f, speed * Mathf.Exp(-1.3f * dt));
        transform.position = p + moveDirection * speed * dt;
        transform.localScale = Vector3.one * (1f - 0.55f * t);
        age += dt;
        UpdateCaptured(dt);
        if (body != null) body.Rotate(0f, 0f, 90f * dt);
        for (int i = 0; i < fadeRenderers.Length; i++)
        {
            if (fadeRenderers[i] == null) continue;
            Color c = fadeRenderers[i].color;
            c.a = fadeAlphas[i] * (1f - t);
            fadeRenderers[i].color = c;
        }
        if (t >= 1f)
        {
            dead = true;
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Hit by a Spaceship turret bullet travelling along 'dir': takes one point of damage
    /// (Brutes have two). Returns false if the enemy was already dead.
    /// </summary>
    public bool Shot(Vector2 dir)
    {
        if (dead) return false;
        Vector2 p = transform.position;
        hp--;
        if (hp <= 0)
        {
            dead = true;
            ParticleBursts.Burst(p, color, type == EnemyType.Brute ? 40 : 12, type == EnemyType.Brute ? 7f : 5f, 0.3f, 0.6f);
            if (type == EnemyType.Brute && GameManager.Instance != null) GameManager.Instance.AddShake(0.3f);
            Sfx.Play("pop", 0.35f);
            Destroy(gameObject);
            return true;
        }
        damageFlash = 1f;
        knock += dir.normalized * 1.5f;
        ParticleBursts.Burst(p, color, 12, 4f, 0.25f, 0.4f);
        return true;
    }

    /// <summary>Noble Atom's Electroblast: destroyed outright, whatever its health.</summary>
    public void Vaporize(Color blastColor)
    {
        if (dead) return;
        dead = true;
        Vector2 p = transform.position;
        ParticleBursts.Burst(p, color, type == EnemyType.Brute ? 34 : 14, 7f, 0.3f, 0.6f);
        ParticleBursts.Burst(p, blastColor, 10, 5f, 0.25f, 0.45f);
        Destroy(gameObject);
    }

    /// <summary>
    /// Repel blast at 'from': if within 'radius', knocked away (stronger when closer) and turned
    /// by 'turn' (0..1) toward flying away. Missiles lose their lock; a Flash that hasn't struck
    /// yet starts aiming again. Never destroys the enemy.
    /// </summary>
    public void Repel(Vector2 from, float radius, float force, float turn)
    {
        if (bond != BondState.None) return;
        Vector2 d = (Vector2)transform.position - from;
        float dist = d.magnitude;
        if (dist > radius) return;
        Vector2 away = dist > 1e-3f ? d / dist : -moveDirection;
        float falloff = 1f - 0.5f * dist / radius;   // 1 at the center, 0.5 at the edge
        knock += away * force * falloff * (type == EnemyType.Brute ? 0.6f : 1f);

        if (speed > 0f)
        {
            Vector2 nd = Vector2.Lerp(moveDirection, away, turn);
            moveDirection = nd.sqrMagnitude > 1e-4f ? nd.normalized : away;
            FaceDirection();
        }
        if (type == EnemyType.Missile) { locked = false; relockTimer = MissileRelockTime; }
        if (type == EnemyType.Flash && phase != FlashPhase.Dash)
        {
            phase = FlashPhase.Aim;
            phaseTime = 0f;
        }
        damageFlash = Mathf.Max(damageFlash, 0.5f);
    }

    void EmitExhaust(float dt)
    {
        if (speed <= 0f || type == EnemyType.Broken) return;   // the Broken Thruster only puffs on pushes
        emitTimer -= dt;
        if (emitTimer > 0f) return;
        switch (type)
        {
            case EnemyType.Brute: emitTimer = 0.06f; break;
            case EnemyType.Missile: emitTimer = 0.015f; break;
            case EnemyType.Flash: emitTimer = 0.01f; break;
            default: emitTimer = 0.02f; break;
        }
        Vector2 back = -moveDirection * radius * (type == EnemyType.Missile ? 2.2f : 1f);
        Color c = type == EnemyType.Missile ? new Color(1f, 0.7f, 0.3f, 0.7f) : new Color(color.r, color.g, color.b, 0.5f);
        float size = type == EnemyType.Brute ? 0.45f : type == EnemyType.Missile ? 0.14f : 0.1f;
        ParticleBursts.Emit((Vector2)transform.position + back + Random.insideUnitCircle * radius * 0.3f,
            -moveDirection * (type == EnemyType.Missile ? 2f : 0.5f), c, size, type == EnemyType.Brute ? 0.5f : 0.3f);
    }

    // -------------------------------------------------------- broken thruster

    void UpdateBroken(float dt, PendulumController pc)
    {
        Vector2 pos = transform.position;
        bool target = pc != null && pc.IsAlive;

        // Coast: the last push bleeds off (after giving up it keeps a slow drift so it leaves).
        float floor = gaveUp ? brokenDriftSpeed : 0f;
        speed = floor + (speed - floor) * Mathf.Exp(-brokenCoastDrag * dt);
        if (speed < 0.05f) speed = 0.05f;   // keep a direction for the off-screen check

        if (target)
        {
            // Remember the atom's path; the next push aims at an old point on it.
            aimHistory.Enqueue(new Vector3(age, pc.PivotPosition.x, pc.PivotPosition.y));
            while (aimHistory.Count > 1 && aimHistory.Peek().x < age - brokenAimDelay - 0.5f) aimHistory.Dequeue();
        }

        if (!gaveUp)
        {
            pushTimer -= dt;
            if (!target || age > brokenActiveTime)
            {
                // Engine dies: a last cough of smoke, then it drifts on (and leaves the screen).
                gaveUp = true;
                speed = Mathf.Max(speed, brokenDriftSpeed);
                ParticleBursts.Burst(pos, new Color(0.6f, 0.6f, 0.65f, 0.6f), 12, 2f, 0.2f, 0.6f);
                Sfx.Play("sputter", 0.25f);
            }
            else if (pushTimer <= 0f)
            {
                Push(pc, pos);
            }
        }

        // Charge-up: the core and glow flicker brighter right before the next push.
        float charge = gaveUp ? 0f : Mathf.Clamp01(1f - pushTimer / Mathf.Min(0.45f, brokenPushInterval));
        bool flicker = charge > 0f && Mathf.Repeat(age * 22f, 1f) < 0.5f;
        core.color = Color.Lerp(Color.Lerp(color, Color.white, 0.6f), Color.white, flicker ? charge : charge * 0.4f);
        glow.color = new Color(color.r, color.g, color.b, (gaveUp ? 0.2f : 0.35f) + 0.4f * charge);

        // Push kick: the hull jolts forward and squashes.
        kick = Mathf.MoveTowards(kick, 0f, dt * 5f);
        body.localScale = new Vector3(bodyScale * (1f + 0.25f * kick), bodyScale * (1f - 0.15f * kick), 1f);

        // Damaged engine: smoke while coasting, sparks now and then.
        emitTimer -= dt;
        if (emitTimer <= 0f)
        {
            emitTimer = 0.07f;
            Vector2 back = -moveDirection * radius * 1.3f;
            ParticleBursts.Emit(pos + back + Random.insideUnitCircle * 0.08f, -moveDirection * 0.4f + Random.insideUnitCircle * 0.3f,
                new Color(0.6f, 0.6f, 0.65f, 0.35f), 0.16f, 0.6f);
            if (Random.value < 0.15f)
                ParticleBursts.Emit(pos + Random.insideUnitCircle * radius, Random.insideUnitCircle * 3f,
                    new Color(1f, 0.85f, 0.4f, 1f), 0.05f, 0.25f);
        }
    }

    /// <summary>The atom's position brokenAimDelay seconds ago (or the oldest one remembered).</summary>
    Vector2 DelayedTarget(PendulumController pc)
    {
        Vector2 best = pc.PivotPosition;
        float want = age - brokenAimDelay;
        foreach (Vector3 h in aimHistory)
        {
            best = new Vector2(h.y, h.z);
            if (h.x >= want) break;   // the first sample at or after that moment
        }
        return best;
    }

    void Push(PendulumController pc, Vector2 pos)
    {
        pushTimer = brokenPushInterval;
        pushes++;

        // Re-aim at where the atom was a moment ago, not where it is now: keep moving and it misses.
        Vector2 aimAt = DelayedTarget(pc);
        Vector2 to = aimAt - pos;
        if (to.sqrMagnitude > 1e-4f)
        {
            float angle = Vector2.SignedAngle(moveDirection, to) + Random.Range(-8f, 8f);
            moveDirection = (Quaternion.Euler(0f, 0f, angle) * moveDirection).normalized;
            FaceDirection();
        }
        // Every push is weaker than the one before.
        float strength = Mathf.Pow(brokenPushFalloff, pushes - 1);
        speed = Mathf.Max(brokenPushSpeed * strength, brokenDriftSpeed);
        kick = Mathf.Lerp(0.4f, 1f, strength);

        // Flame burst out the back.
        Vector2 back = pos - moveDirection * radius * 1.4f;
        int flames = Mathf.RoundToInt(Mathf.Lerp(5f, 14f, strength));
        for (int i = 0; i < flames; i++)
        {
            Vector2 v = (-moveDirection + Random.insideUnitCircle * 0.5f) * Random.Range(3f, 7f);
            Color c = Color.Lerp(new Color(1f, 0.9f, 0.5f, 1f), new Color(color.r, color.g, color.b, 0.8f), Random.value);
            ParticleBursts.Emit(back, v, c, Random.Range(0.08f, 0.16f), Random.Range(0.2f, 0.4f));
        }
        Sfx.Play("sputter", Mathf.Lerp(0.25f, 0.5f, strength));
    }

    // ---------------------------------------------------------------- missile

    void UpdateMissile(float dt, PendulumController pc)
    {
        bool target = pc != null && pc.IsAlive;
        if (!target) locked = false;
        else if (!locked && !gaveUp)
        {
            relockTimer -= dt;
            if (relockTimer <= 0f) locked = true;
        }

        if (locked)
        {
            Vector2 to = pc.PivotPosition - (Vector2)transform.position;
            float angle = Vector2.SignedAngle(moveDirection, to);
            // Outmaneuvered (the atom got behind it): it gives up for good and flies on straight.
            if (age > 1f && Mathf.Abs(angle) > missileGiveUpAngle)
            {
                locked = false;
                gaveUp = true;
            }
        }

        if (locked)
        {
            Vector2 to = pc.PivotPosition - (Vector2)transform.position;
            float angle = Vector2.SignedAngle(moveDirection, to);
            float turn = Mathf.Clamp(angle, -missileTurnRate * dt, missileTurnRate * dt);
            moveDirection = (Quaternion.Euler(0f, 0f, turn) * moveDirection).normalized;
            FaceDirection();

            // Beep faster the closer it gets.
            beepTimer -= dt;
            if (beepTimer <= 0f)
            {
                beepTimer = Mathf.Lerp(0.12f, 0.6f, Mathf.Clamp01(to.magnitude / 12f));
                Sfx.Play("lock", 0.6f);
            }
        }

        bool blinkOn = locked && Mathf.Repeat(age * 8f, 1f) < 0.5f;
        lockLight.color = blinkOn ? new Color(1f, 0.2f, 0.2f, 1f) : (locked ? new Color(0.5f, 0.1f, 0.1f, 1f) : new Color(0.3f, 0.3f, 0.3f, 1f));
        glow.color = new Color(color.r, color.g, color.b, locked ? 0.45f : 0.2f);
    }

    // ------------------------------------------------------------------ flash

    void UpdateFlash(float dt, PendulumController pc)
    {
        phaseTime += dt;
        Vector2 pos = transform.position;
        float lineLen = GameManager.HalfExtents.magnitude * 2.2f;

        switch (phase)
        {
            case FlashPhase.Aim:
                if (rainStrike)
                {
                    Vector2 toTarget = rainTarget - pos;
                    if (toTarget.sqrMagnitude > 0.01f) moveDirection = toTarget.normalized;
                    FaceDirection();
                }
                else if (pc != null && pc.IsAlive)
                {
                    // Aim where the thruster WILL be if it keeps its current velocity: it keeps
                    // moving during the lock delay, then the strike has to catch it.
                    Vector2 to = PredictIntercept(pos, pc.PivotPosition, pc.PivotVelocity) - pos;
                    if (to.sqrMagnitude > 0.01f) moveDirection = to.normalized;
                    FaceDirection();
                }
                SetAimLine(pos, lineLen, 0.03f, 0.15f + 0.15f * Mathf.Repeat(age * 12f, 1f));
                if (phaseTime >= (flashAimTime >= 0f ? flashAimTime : FlashAimTime)) { phase = FlashPhase.Lock; phaseTime = 0f; }
                break;

            case FlashPhase.Lock:
                bool on = Mathf.Repeat(phaseTime * 16f, 1f) < 0.5f;
                SetAimLine(pos, lineLen, on ? 0.09f : 0.05f, on ? 0.75f : 0.35f);
                if (phaseTime >= FlashLockTime)
                {
                    phase = FlashPhase.Dash;
                    phaseTime = 0f;
                    speed = flashDashSpeed;
                    pierced.Clear();
                    coreStruck = false;
                    aimLine.color = Color.clear;
                    Sfx.Play("zap");
                    if (GameManager.Instance != null) GameManager.Instance.AddShake(0.25f);
                }
                break;

            case FlashPhase.Dash:
                // Lightning trail from where it was last frame.
                Vector2 a = lastPos, b = pos;
                Vector2 mid = (a + b) * 0.5f + new Vector2(-moveDirection.y, moveDirection.x) * Random.Range(-0.25f, 0.25f);
                Segment(a, mid, color, 0.09f);
                Segment(mid, b, color, 0.09f);
                Segment(a, b, Color.white, 0.035f);

                // Reached the far edge: stop there and line up the next strike.
                Vector2 vc = GameManager.ViewCenter;
                Vector2 half = GameManager.HalfExtents - new Vector2(0.8f, 0.8f);
                Vector2 rel = pos - vc;
                bool outside = Mathf.Abs(rel.x) > half.x || Mathf.Abs(rel.y) > half.y;
                if (outside && Vector2.Dot(rel, moveDirection) > 0f)
                {
                    dead = true;   // one strike, then it is gone
                    ParticleBursts.Burst(pos, color, 10, 3f, 0.2f, 0.4f);
                    Destroy(gameObject);
                    return;
                }
                break;
        }
    }

    /// <summary>
    /// Where a target moving at constant velocity will be when the strike reaches it, if the
    /// aim were locked right now (lock delay first, then a dash at flashDashSpeed).
    /// </summary>
    Vector2 PredictIntercept(Vector2 from, Vector2 targetPos, Vector2 targetVel)
    {
        float t0 = FlashLockTime;
        float s = Mathf.Max(0.1f, flashDashSpeed);
        Vector2 d = targetPos + targetVel * t0 - from;      // target relative to us when the dash starts
        // Solve |d + v*tau| = s*tau for the smallest positive tau.
        float a = Vector2.Dot(targetVel, targetVel) - s * s;
        float b = 2f * Vector2.Dot(d, targetVel);
        float c = Vector2.Dot(d, d);
        float tau;
        if (Mathf.Abs(a) < 1e-4f)
        {
            tau = Mathf.Abs(b) > 1e-4f ? -c / b : 0f;
        }
        else
        {
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return targetPos;
            float sq = Mathf.Sqrt(disc);
            float r1 = (-b - sq) / (2f * a), r2 = (-b + sq) / (2f * a);
            tau = r1 > 0f && r2 > 0f ? Mathf.Min(r1, r2) : Mathf.Max(r1, r2);
        }
        if (tau < 0f) return targetPos;
        return targetPos + targetVel * (t0 + tau);
    }

    void SetAimLine(Vector2 from, float length, float width, float alpha)
    {
        Transform t = aimLine.transform;
        t.position = from + moveDirection * (length * 0.5f);
        t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg);
        t.localScale = new Vector3(length, width, 1f);
        aimLine.color = new Color(color.r, color.g, color.b, alpha);
    }

    static void Segment(Vector2 a, Vector2 b, Color c, float width)
    {
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 0.001f) return;
        var go = new GameObject("Bolt");
        go.transform.position = (a + b) * 0.5f;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        go.transform.localScale = new Vector3(len + width, width, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ProcSprites.Pixel;
        sr.color = new Color(c.r, c.g, c.b, 0.9f);
        sr.sortingOrder = 25;
        go.AddComponent<FadeOut>().life = 0.3f;
    }
}
