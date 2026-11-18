using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The player: an atom's nucleus (the core), with electrons (balls) held on its orbitals.
/// It moves by a pulse drive: wavefronts of energy pushed out behind it.
///
/// Thruster: press on it and drag (like a joystick), or use WASD / arrow keys, to
/// fly. It has momentum and drifts to a stop. Enemies that reach the thruster cost a life.
///
/// Balls: added by Orb pickups and arranged in shells like electrons (2, then 8).
/// Each ball is a point mass on a rope under gravity. A rotating hub drives the
/// balls around automatically, but only through a taut rope, so a ball that gets
/// too slow (or is jerked by the thruster) goes slack and falls inward, then snaps
/// taut again. Balls in the same shell spread out evenly. A ball blocks one enemy:
/// both are destroyed.
///
/// Runs after GameManager (execution order) so screen-anchored visuals don't lag
/// behind the camera.
/// </summary>
[DefaultExecutionOrder(100)]
public class PendulumController : MonoBehaviour
{
    public static PendulumController Instance { get; private set; }

    [Header("Thruster (the player)")]
    [Tooltip("Hit radius of the thruster pod.")]
    public float coreRadius = 0.26f;
    public Color podColor = new Color(0.9f, 0.82f, 0.62f);
    public Color thrusterColor = new Color(1f, 0.55f, 0.15f);
    [Tooltip("Color of the core's pulse drive (the energy wavefronts behind it).")]
    public Color driveColor = new Color(0.6f, 0.88f, 1f);
    [Tooltip("Seconds the core shakes unstably after losing all stability, before it explodes.")]
    public float meltdownTime = 1.2f;
    [Tooltip("Base acceleration (upgradable).")]
    public float thrustAcceleration = 10f;
    [Tooltip("How quickly the thruster drifts to a stop.")]
    public float pivotDrag = 1.1f;
    [Tooltip("Base top speed (upgradable).")]
    public float maxPivotSpeed = 6f;
    [Tooltip("How close to the thruster (world units) a press must be to start dragging.")]
    public float grabRadius = 1f;
    [Tooltip("Drag distance (world units) that gives full thrust.")]
    public float maxDragDistance = 1.4f;
    public float dragDeadzone = 0.1f;
    [Tooltip("'Torque': extra acceleration that cancels motion you are steering away from (sharper turns/braking). Upgradable.")]
    public float counterThrust = 0f;
    [Tooltip("How effective thrust is when it isn't pushing along your current motion (turning/braking). 1 = full. Upgradable via Torque.")]
    [Range(0f, 1f)] public float turnAuthority = 0.45f;

    [Header("Orbiting Balls (shells)")]
    public int[] shellCapacities = { 2, 8 };
    public float[] shellLengths = { 1.3f, 2.3f };
    public Color[] shellColors = { new Color(0.3f, 0.9f, 1f), new Color(0.55f, 0.65f, 1f), new Color(0.55f, 1f, 0.8f) };
    public float ballSize = 0.42f;
    public Color chainColor = new Color(0.78f, 0.7f, 0.52f);
    [Tooltip("Automatic spin speed of the hub (degrees per second).")]
    public float spinSpeed = 160f;
    public bool spinClockwise = false;
    [Tooltip("Max tangential acceleration the hub can apply through a taut rope.")]
    public float spinDrive = 14f;
    [Tooltip("How aggressively the hub corrects a ball's speed toward the spin speed.")]
    public float spinResponse = 6f;
    [Tooltip("How strongly balls in the same shell spread out evenly.")]
    public float spacingStrength = 2.5f;
    [Tooltip("How much the thruster's own acceleration swings the balls. 0 = balls move rigidly with the thruster, 1 = full inertia.")]
    [Range(0f, 1f)] public float ballInertia = 0.2f;
    [Tooltip("How many enemy hits each ball can take. Upgradable.")]
    public int ballMaxHits = 1;

    [Header("Rope Physics")]
    public float gravity = 7f;
    public float airDrag = 0.15f;
    public float maxSpeed = 18f;
    [Tooltip("A ball cannot get closer to the thruster than this.")]
    public float minDistance = 0.35f;
    [Tooltip("Outward speed (units/s) above which a rope snap is shown/heard.")]
    public float snapThreshold = 2.5f;
    [Range(1, 8)] public int substeps = 4;
    [Tooltip("Minimum seconds between two rope-snap sounds (many balls snapping at once play one sound).")]
    public float snapSoundCooldown = 0.12f;
    [Tooltip("How much the thruster's speed drags the balls behind it. 0 = balls keep orbiting normally at any speed, 1 = air is still (balls trail behind when fast).")]
    [Range(0f, 1f)] public float speedTrail = 0.1f;

    [Header("Damage")]
    public float invulnerableTime = 1.5f;
    [Tooltip("How much an enemy hit knocks the thruster.")]
    public float impactScale = 0.8f;

    [Header("Hit Reactions (unlocked by upgrades)")]
    [Tooltip("Repel: blast radius around the thruster at upgrade level 1, and added per extra level.")]
    public float repelRadius = 4.5f;
    public float repelRadiusPerLevel = 1f;
    [Tooltip("Repel: outward speed given to enemies at level 1, and added per extra level.")]
    public float repelForce = 8f;
    public float repelForcePerLevel = 3f;
    [Tooltip("Repel: how far enemies are turned to fly away (0..1) at level 1, and added per extra level.")]
    public float repelTurn = 0.6f;
    public float repelTurnPerLevel = 0.1f;
    public float repelCooldown = 0.5f;
    public Color repelColor = new Color(0.75f, 0.9f, 1f);
    [Tooltip("Repel: how heavy an average asteroid is compared to an enemy (it is pushed by force / mass; bigger rocks are heavier).")]
    public float asteroidRepelMass = 6f;
    [Tooltip("Asteroids: an orb that grazes an asteroid takes one hit, then is safe from asteroids for this many seconds.")]
    public float asteroidHitCooldown = 0.6f;
    [Tooltip("Emergency booster: seconds of boost at level 1, and added per extra level.")]
    public float boostDuration = 1.5f;
    public float boostDurationPerLevel = 0.35f;
    [Tooltip("Emergency booster: top speed & thrust multiplier at level 1, and added per extra level.")]
    public float boostMultiplier = 1.6f;
    public float boostMultiplierPerLevel = 0.2f;
    [Tooltip("How fast speed above the normal limit eases back down after a boost (units/s²).")]
    public float boostSettle = 10f;
    public Color boostColor = new Color(0.45f, 0.85f, 1f);

    [Header("Spaceship Turrets (character)")]
    [Tooltip("Distance from the thruster's center to each turret.")]
    public float turretArmLength = 0.62f;
    [Tooltip("Shots per second per turret, before Fire Rate upgrades.")]
    public float turretFireRate = 1.6f;
    [Tooltip("Targeting range (world units from the turret), before Turret Range upgrades.")]
    public float turretRange = 5.5f;
    public float turretBulletSpeed = 22f;
    [Tooltip("How fast the barrels swivel to aim (degrees per second).")]
    public float turretTurnSpeed = 900f;
    [Tooltip("How fast the turret arms follow the thrust direction (degrees per second).")]
    public float turretMountTurnSpeed = 540f;
    public Color turretColor = new Color(0.8f, 1f, 0.35f);

    class Turret
    {
        public SpriteRenderer symbol;
        public GameObject root;
        public Transform mount, barrel;
        public SpriteRenderer tube, muzzle, hub, glow;
        public float side, aim, cooldown, recoil, flash, appear;
    }

    class Ball
    {
        public int shell, hp;
        public float damageFlash;
        public float rockTimer;   // asteroid hit cooldown
        public SpriteRenderer armor;
        public Vector2 pos, vel;
        public bool taut = true;
        public float spacing, snap, snapFlash;
        public GameObject root;
        public Transform bob;
        public SpriteRenderer body, glow;
        public SpriteRenderer[] links;
        public TrailRenderer trail;
    }

    public Vector2 PivotPosition => transform.position;
    public Vector2 PivotVelocity => pivotVel;
    public float Speed => pivotVel.magnitude;
    public float MaxSpeed => maxPivotSpeed;
    public float CoreRadius => coreRadius;
    public float BallRadius => BallWorldSize * 0.45f;
    /// <summary>Ball diameter including the Bigger Balls upgrade.</summary>
    float BallWorldSize => ballSize * ballScale;
    public bool IsBoosting => boostTimer > 0f;
    public bool CanBeHit => alive && !meltingDown && invulTimer <= 0f;
    public bool IsAlive => alive;
    public bool IsMeltingDown => meltingDown;
    public bool IsDragging => dragSource != DragSource.None;
    public int BallCount => balls.Count;
    public int ShellTotal => shellCapacities.Length;
    public int ShellCapacity(int s) => s >= 0 && s < shellCapacities.Length ? shellCapacities[s] : 0;
    /// <summary>Everything orbs can fill: turret slots (Spaceship) plus every shell.</summary>
    public int Capacity { get { int c = turretSlots; foreach (int x in shellCapacities) c += x; return c; } }
    public bool IsFull => balls.Count + turretCount >= Capacity;
    /// <summary>Spaceship: turret slots (0 = no turrets) and how many are built.</summary>
    public int TurretSlots => turretSlots;
    public int TurretCount => turretCount;
    float TurretRangeNow => turretRange + turretRangeBonus;

    /// <summary>Length of the outermost rope in use (how far the balls reach).</summary>
    public float ReachRadius
    {
        get
        {
            float r = 0f;
            foreach (var b in balls) r = Mathf.Max(r, ShellLength(b.shell));
            return Mathf.Max(r, 1f);
        }
    }

    enum DragSource { None, Mouse, Touch }

    Vector2 pivotVel;
    Vector2 thrust;
    float invulTimer, hitFlash;
    bool alive = true;
    bool meltingDown;          // out of stability: shaking apart, about to explode
    float meltdownT, crackleTimer, sparkTimer;

    DragSource dragSource = DragSource.None;
    int dragTouchId = -1;
    Vector2 dragAnchorScreen, dragCurrentScreen;

    readonly List<Ball> balls = new List<Ball>();
    readonly List<Ball> shellScratch = new List<Ball>();

    Transform pod, ropeRoot, thruster;
    SpriteRenderer[] pulses;
    Vector2[] pulseVel;
    float[] pulseAge;
    int pulseNext;
    float pulseTimer;
    SpriteRenderer podBody, podGlow, grabHint, flame, flameCore, headingArrow, headingGlow;
    SpriteRenderer[] shellRings = new SpriteRenderer[0];
    int[] baseShellCaps;
    float[] baseShellLengths;

    public int[] BaseShellCapacities => baseShellCaps;
    public float[] BaseShellLengths => baseShellLengths;
    Material trailMaterial;
    float thrusterEmit;
    float baseMaxSpeed, baseThrust, baseCounterThrust, baseTurnAuthority;
    int baseBallHits;

    // Upgrade-driven extras.
    float ballScale = 1f, pickupBonus;
    int repelLevel, boostLevel;
    float repelTimer, boostTimer, boostMult = 1f;
    SpriteRenderer reachRing;

    // Spaceship turrets (they replace the inner orbital).
    readonly List<Turret> turrets = new List<Turret>();
    Transform turretRig;
    int turretSlots, turretCount;
    int shellColorOffset;   // 1 while the inner orbital is replaced, so outer shells keep their colors
    float fireRateMult = 1f, turretRangeBonus, rigAngle;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        baseMaxSpeed = maxPivotSpeed;
        baseThrust = thrustAcceleration;
        baseCounterThrust = counterThrust;
        baseTurnAuthority = turnAuthority;
        baseBallHits = ballMaxHits;
        baseShellCaps = (int[])shellCapacities.Clone();
        baseShellLengths = (float[])shellLengths.Clone();
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        BuildVisuals();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (ropeRoot != null) Destroy(ropeRoot.gameObject);
    }

    float ShellLength(int s)
    {
        if (shellLengths == null || shellLengths.Length == 0) return 1.3f + s;
        if (s < shellLengths.Length) return shellLengths[s];
        return shellLengths[shellLengths.Length - 1] + (s - shellLengths.Length + 1);
    }

    public Color ShellColor(int s)
    {
        if (shellColors == null || shellColors.Length == 0) return Pickup.OrbColor;
        return shellColors[Mathf.Min(s + shellColorOffset, shellColors.Length - 1)];
    }

    public int ShellCount(int s)
    {
        int c = 0;
        foreach (var b in balls) if (b.shell == s) c++;
        return c;
    }

    // ------------------------------------------------------------------ build

    static SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite, Color color, int order, float scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    void BuildVisuals()
    {
        pod = new GameObject("Pod").transform;
        pod.SetParent(transform, false);

        RebuildShellRings();

        grabHint = NewSprite("GrabHint", pod, ProcSprites.Ring, Color.clear, 7, grabRadius / 0.485f);
        reachRing = NewSprite("PickupReach", pod, ProcSprites.Ring, Color.clear, -9, 1f);

        thruster = new GameObject("Thruster").transform;
        thruster.SetParent(pod, false);
        // Drive: a soft energy wake behind the core (no flame, no nozzle: it's a nucleus).
        flame = NewSprite("DriveWake", thruster, ProcSprites.Glow, Color.clear, 3, 1f);
        flameCore = NewSprite("DriveSpot", thruster, ProcSprites.Glow, Color.clear, 6, 1f);

        podGlow = NewSprite("CoreGlow", pod, ProcSprites.Glow, new Color(podColor.r, podColor.g, podColor.b, 0.35f), 4, 1.3f);
        podBody = NewSprite("Nucleus", pod, ProcSprites.Nucleus, podColor, 5, coreRadius * 2.3f);

        // World-space root for electrons and their bonds (not parented to the core, so they can lag and swing).
        ropeRoot = new GameObject("PendulumBalls").transform;

        // Pulse drive wavefronts (pooled, world space).
        pulses = new SpriteRenderer[8];
        pulseVel = new Vector2[pulses.Length];
        pulseAge = new float[pulses.Length];
        for (int i = 0; i < pulses.Length; i++)
        {
            pulses[i] = NewSprite("DrivePulse" + i, ropeRoot, ProcSprites.Ring, Color.clear, 3, 1f);
            pulseAge[i] = 99f;
        }

        // Heading arrow: points where the thruster is moving.
        headingGlow = NewSprite("HeadingGlow", pod, ProcSprites.Glow, Color.clear, 11, 0.7f);
        headingArrow = NewSprite("HeadingArrow", pod, ProcSprites.Arrow, Color.clear, 12, 0.3f);

        trailMaterial = MakeTrailMaterial(podBody);
    }

    static Material MakeTrailMaterial(SpriteRenderer fallback)
    {
        string[] shaders =
        {
            "Universal Render Pipeline/2D/Sprite-Unlit-Default",
            "Sprites/Default",
            "Universal Render Pipeline/Particles/Unlit"
        };
        foreach (string n in shaders)
        {
            Shader sh = Shader.Find(n);
            if (sh != null) return new Material(sh) { name = "PendulumTrail" };
        }
        return fallback.sharedMaterial;
    }

    Ball CreateBall(int shell, Vector2 pos, Vector2 vel)
    {
        var b = new Ball { shell = shell, pos = pos, vel = vel };
        Color c = ShellColor(shell);
        float L = ShellLength(shell);

        b.root = new GameObject("Ball_Shell" + shell);
        b.root.transform.SetParent(ropeRoot, false);

        int count = Mathf.Max(3, Mathf.CeilToInt(L / 0.18f));
        b.links = new SpriteRenderer[count];
        for (int i = 0; i < count; i++)
        {
            bool face = i % 2 == 1;
            b.links[i] = NewSprite("Bond" + i, b.root.transform, ProcSprites.Glow, Color.clear, face ? 1 : 0, 1f);
        }

        b.bob = new GameObject("Bob").transform;
        b.bob.SetParent(b.root.transform, false);
        b.bob.position = pos;
        float size = BallWorldSize;
        b.glow = NewSprite("Glow", b.bob, ProcSprites.Glow, new Color(c.r, c.g, c.b, 0.5f), 9, size * 2.8f);
        b.body = NewSprite("Body", b.bob, ProcSprites.Electron, c, 10, size);
        b.armor = NewSprite("Armor", b.bob, ProcSprites.Ring, Color.clear, 11, size * 1.45f);
        b.hp = Mathf.Max(1, ballMaxHits);

        b.trail = b.bob.gameObject.AddComponent<TrailRenderer>();
        b.trail.time = 0.2f;
        b.trail.minVertexDistance = 0.03f;
        b.trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        b.trail.widthMultiplier = size * 0.55f;
        b.trail.numCapVertices = 3;
        b.trail.sortingOrder = 8;
        b.trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        b.trail.receiveShadows = false;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.Lerp(c, Color.white, 0.3f), 0f), new GradientColorKey(c, 1f) },
            new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) });
        b.trail.colorGradient = grad;
        b.trail.sharedMaterial = trailMaterial;

        balls.Add(b);
        UpdateChain(b);
        return b;
    }

    // ----------------------------------------------------------------- update

    void Update()
    {
        float dt = Time.deltaTime;
        repelTimer -= dt;
        boostTimer -= dt;

        if (alive && !meltingDown && Time.timeScale > 0f) ReadInput();   // no input while paused
        else if (!alive || meltingDown) { thrust = Vector2.zero; EndDrag(); }
        if (meltingDown) pivotVel *= Mathf.Exp(-4f * dt);   // grinds to a halt while it comes apart

        Simulate(dt);
        UpdateTurrets(dt);

        foreach (var b in balls)
        {
            b.bob.position = b.pos;
            UpdateChain(b);
        }
        UpdateVisuals(dt);
        if (meltingDown) UpdateMeltdown(dt);
    }

    /// <summary>Out of stability: the nucleus starts to shake apart (see UpdateMeltdown, Explode).</summary>
    public void BeginMeltdown()
    {
        if (!alive || meltingDown) return;
        meltingDown = true;
        meltdownT = 0f;
        crackleTimer = sparkTimer = 0f;
        EndDrag();
        Sfx.Play("meltdown", 0.9f);
    }

    void UpdateMeltdown(float dt)
    {
        meltdownT += dt;
        float k = Mathf.Clamp01(meltdownT / Mathf.Max(0.1f, meltdownTime));
        Vector2 c = PivotPosition;

        // Violent, growing shake; the nucleus throbs and flickers white / red.
        pod.localPosition = (Vector3)(Random.insideUnitCircle * (0.04f + 0.16f * k * k));
        float throb = 1f + (0.06f + 0.22f * k) * Mathf.Abs(Mathf.Sin(meltdownT * (14f + 40f * k)));
        podBody.transform.localScale *= throb;
        bool hot = Mathf.Repeat(meltdownT * (5f + 22f * k), 1f) > 0.5f;
        podBody.color = Color.Lerp(podColor, hot ? Color.white : new Color(1f, 0.3f, 0.25f), 0.3f + 0.65f * k);
        podGlow.transform.localScale = Vector3.one * (1.3f + 2.2f * k);
        podGlow.color = new Color(1f, 0.55f, 0.4f, 0.35f + 0.5f * k);

        // Bits of energy spitting off, faster and faster.
        sparkTimer -= dt;
        if (sparkTimer <= 0f)
        {
            sparkTimer = Mathf.Lerp(0.06f, 0.015f, k);
            Vector2 dir = Random.insideUnitCircle.normalized;
            Color sc = Color.Lerp(podColor, Color.white, Random.value);
            sc.a = 0.85f;
            ParticleBursts.Emit(c + dir * coreRadius, dir * Random.Range(2f, 3f + 6f * k), sc, Random.Range(0.06f, 0.14f), 0.35f, 3f);
        }
        crackleTimer -= dt;
        if (crackleTimer <= 0f)
        {
            crackleTimer = Mathf.Lerp(0.28f, 0.08f, k);
            Sfx.Play("snap", 0.25f + 0.45f * k);
        }

        // The electrons feel it too.
        foreach (var b in balls) b.snapFlash = Mathf.Max(b.snapFlash, k * Random.value * 0.8f);

        if (GameManager.Instance != null) GameManager.Instance.AddShake(0.1f + 0.25f * k);
        if (meltdownT >= meltdownTime) Explode();
    }

    /// <summary>The core blows apart: a flash and shockwave, every electron is blasted away, and
    /// anything nearby (enemies, dust grains) is shoved back.</summary>
    void Explode()
    {
        meltingDown = false;
        alive = false;
        EndDrag();
        pod.localPosition = Vector3.zero;
        Vector2 c = PivotPosition;

        ParticleBursts.Burst(c, podColor, 60, 12f, 0.4f, 1.2f);
        ParticleBursts.Burst(c, Color.white, 30, 9f, 0.3f, 0.7f);
        ParticleBursts.Burst(c, driveColor, 24, 7f, 0.3f, 0.9f);
        ShockRing.Spawn(c, 6f, Color.Lerp(podColor, Color.white, 0.4f));
        ShockRing.Spawn(c, 3.5f, Color.white);
        Sfx.Play("explode");
        if (GameManager.Instance != null) GameManager.Instance.AddShake(1.1f);

        float size = BallWorldSize;
        foreach (var b in balls)
        {
            Vector2 d = b.pos - c;
            Vector2 dir = d.sqrMagnitude > 1e-4f ? d.normalized : Random.insideUnitCircle.normalized;
            Vector2 vel = dir * Random.Range(8f, 12f) + (b.vel - pivotVel) * 0.3f;
            Debris.Spawn(b.pos, vel, ProcSprites.Electron, ShellColor(b.shell), size, Random.Range(1.3f, 1.9f));
        }
        ClearBalls(false);
        ClearTurrets(true);

        foreach (var e in EnemyMover.All) e.Repel(c, 7f, 14f, 1f);
        foreach (var h in Hazard.All) h.Repel(c, 7f, 14f, asteroidRepelMass);
        pod.gameObject.SetActive(false);
    }

    void UpdateHeadingArrow()
    {
        float spd = pivotVel.magnitude;
        if (!alive || spd < 0.3f)
        {
            headingArrow.color = Color.clear;
            headingGlow.color = Color.clear;
            return;
        }
        float k = Mathf.Clamp01(spd / Mathf.Max(0.01f, maxPivotSpeed));
        Vector2 d = pivotVel / spd;
        float dist = 0.5f + 0.35f * k;
        headingArrow.transform.position = PivotPosition + d * dist;
        headingArrow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        headingArrow.transform.localScale = Vector3.one * (0.24f + 0.12f * k);
        headingArrow.color = new Color(1f, 0.92f, 0.75f, 0.25f + 0.5f * k);
        headingGlow.transform.position = PivotPosition + d * dist;
        headingGlow.color = new Color(1f, 0.85f, 0.5f, 0.1f + 0.2f * k);
    }

    // ------------------------------------------------------------------ input

    void ReadInput()
    {
        Camera cam = Camera.main;
        float wpp = cam != null ? 2f * cam.orthographicSize / Screen.height : 0.01f;

        // Keyboard: WASD / arrows thrust.
        Vector2 keyThrust = Vector2.zero;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) keyThrust.x -= 1f;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) keyThrust.x += 1f;
            if (kb.upArrowKey.isPressed || kb.wKey.isPressed) keyThrust.y += 1f;
            if (kb.downArrowKey.isPressed || kb.sKey.isPressed) keyThrust.y -= 1f;
        }

        // Mouse: press on the thruster to start dragging.
        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 mp = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame && dragSource == DragSource.None && OverPivot(mp, cam))
                BeginDrag(DragSource.Mouse, -1, mp);
            if (dragSource == DragSource.Mouse)
            {
                if (mouse.leftButton.isPressed) dragCurrentScreen = mp;
                else EndDrag();
            }
        }

        // Touch: one dragging finger at a time.
        var ts = Touchscreen.current;
        if (ts != null)
        {
            bool dragFingerFound = false;
            foreach (var t in ts.touches)
            {
                int id = t.touchId.ReadValue();
                Vector2 tp = t.position.ReadValue();
                if (t.press.wasPressedThisFrame && dragSource == DragSource.None && OverPivot(tp, cam))
                {
                    BeginDrag(DragSource.Touch, id, tp);
                    dragFingerFound = true;
                    continue;
                }
                if (dragSource == DragSource.Touch && id == dragTouchId && t.press.isPressed)
                {
                    dragCurrentScreen = tp;
                    dragFingerFound = true;
                }
            }
            if (dragSource == DragSource.Touch && !dragFingerFound) EndDrag();
        }
        else if (dragSource == DragSource.Touch)
        {
            EndDrag();
        }

        Vector2 dragThrust = Vector2.zero;
        if (dragSource != DragSource.None)
        {
            Vector2 d = (dragCurrentScreen - dragAnchorScreen) * wpp;
            float len = d.magnitude;
            if (len > dragDeadzone) dragThrust = d / len * Mathf.Clamp01(len / maxDragDistance);
        }
        thrust = Vector2.ClampMagnitude(dragThrust + keyThrust, 1f);
    }

    bool OverPivot(Vector2 screenPos, Camera cam)
    {
        if (cam == null) return false;
        Vector2 w = cam.ScreenToWorldPoint(screenPos);
        return (w - PivotPosition).sqrMagnitude < grabRadius * grabRadius;
    }

    void BeginDrag(DragSource src, int touchId, Vector2 screenPos)
    {
        dragSource = src;
        dragTouchId = touchId;
        dragAnchorScreen = screenPos;
        dragCurrentScreen = screenPos;
    }

    void EndDrag()
    {
        dragSource = DragSource.None;
        dragTouchId = -1;
    }

    // ---------------------------------------------------------------- physics

    float SpinDir => spinClockwise ? -1f : 1f;
    float nextSnapSound;

    void Simulate(float dt)
    {
        Vector2 oldPivotVel = pivotVel;
        // Thrust along the current motion is full strength; the part that turns or brakes is
        // scaled by turnAuthority (low at base, raised by Torque upgrades).
        Vector2 accel = thrust;
        float curSpeed = pivotVel.magnitude;
        if (curSpeed > 0.3f)
        {
            Vector2 vdir = pivotVel / curSpeed;
            Vector2 forward = vdir * Mathf.Max(Vector2.Dot(thrust, vdir), 0f);
            accel = forward + (thrust - forward) * turnAuthority;
        }
        float boost = boostTimer > 0f ? boostMult : 1f;   // emergency booster
        pivotVel += accel * thrustAcceleration * boost * dt;

        // Torque / counter-thrust: cancel the part of the motion that isn't going where we steer.
        float power = thrust.magnitude;
        if (counterThrust > 0f && power > 0.05f)
        {
            Vector2 dir = thrust / power;
            Vector2 off = pivotVel - dir * Mathf.Max(Vector2.Dot(pivotVel, dir), 0f);
            float offLen = off.magnitude;
            if (offLen > 1e-4f)
                pivotVel -= off / offLen * Mathf.Min(offLen, counterThrust * power * dt);
        }

        pivotVel *= Mathf.Exp(-pivotDrag * dt);
        // Speed limit: raised while boosting; afterwards the extra speed bleeds off smoothly.
        float cap = maxPivotSpeed * boost;
        float spd = pivotVel.magnitude;
        if (spd > cap)
        {
            float target = boostTimer > 0f ? cap : Mathf.Max(cap, spd - boostSettle * dt);
            pivotVel *= target / spd;
        }

        // The balls mostly move with the thruster; only a fraction of its acceleration swings them.
        CarryBalls(pivotVel - oldPivotVel);

        ComputeSpacing();

        Vector2 pivot = PivotPosition;
        int steps = Mathf.Max(1, substeps);
        float h = dt / steps;
        float dragFactor = Mathf.Exp(-airDrag * h);
        foreach (var b in balls) b.snap = 0f;

        for (int s = 0; s < steps; s++)
        {
            pivot += pivotVel * h;
            foreach (var b in balls) StepBall(b, pivot, h, dragFactor);
        }

        transform.position = new Vector3(pivot.x, pivot.y, transform.position.z);

        if (!alive) return;
        float loudest = 0f;
        int snapped = 0;
        foreach (var b in balls)
        {
            if (b.snap <= snapThreshold) continue;
            loudest = Mathf.Max(loudest, b.snap);
            snapped++;
            b.snapFlash = Mathf.Clamp01(b.snap / 8f);
            Vector2 n = (b.pos - pivot).normalized;
            for (int i = 0; i < 4; i++)
                ParticleBursts.Emit(b.pos + n * BallRadius, (n + Random.insideUnitCircle * 0.6f) * 2f,
                    new Color(ShellColor(b.shell).r, ShellColor(b.shell).g, ShellColor(b.shell).b, 0.8f), 0.12f, 0.25f, 5f);
        }
        // One sound for the whole frame, throttled, slightly louder when several snap together.
        if (snapped > 0 && Time.time >= nextSnapSound)
        {
            nextSnapSound = Time.time + snapSoundCooldown;
            float vol = Mathf.Clamp01(loudest / 8f) * 0.6f * Mathf.Min(1.4f, 1f + 0.1f * (snapped - 1));
            Sfx.Play("snap", vol);
        }
    }

    void CarryBalls(Vector2 pivotDeltaV)
    {
        Vector2 carry = pivotDeltaV * (1f - ballInertia);
        foreach (var b in balls) b.vel += carry;
    }

    void StepBall(Ball b, Vector2 pivot, float h, float dragFactor)
    {
        float L = ShellLength(b.shell);
        Vector2 rel = b.pos - pivot;
        float d = rel.magnitude;
        Vector2 n = d > 1e-4f ? rel / d : Vector2.down;
        Vector2 tan = new Vector2(-n.y, n.x) * SpinDir;

        // The hub can only drive the ball through a taut rope.
        if (b.taut && alive)
        {
            float vt = Vector2.Dot(b.vel - pivotVel, tan);
            float target = spinSpeed * Mathf.Deg2Rad * L;
            float acc = Mathf.Clamp((target - vt) * spinResponse + b.spacing, -spinDrive, spinDrive);
            b.vel += tan * acc * h;
        }

        b.vel += Vector2.down * gravity * h;
        // Air drag is measured against a frame that moves with the thruster (mostly), so high
        // thruster speed no longer piles every ball up behind it; speedTrail keeps a little trail.
        Vector2 airVel = pivotVel * (1f - speedTrail);
        b.vel = airVel + (b.vel - airVel) * dragFactor;
        b.pos += b.vel * h;

        rel = b.pos - pivot;
        d = rel.magnitude;
        if (d >= L)
        {
            n = rel / d;
            b.pos = pivot + n * L;
            float vr = Vector2.Dot(b.vel - pivotVel, n);
            if (vr > 0f)
            {
                b.vel -= n * vr;
                b.snap = Mathf.Max(b.snap, vr);
            }
            b.taut = true;
        }
        else
        {
            b.taut = d > L - 0.02f;
            if (d < minDistance)
            {
                n = d > 1e-4f ? rel / d : Vector2.down;
                b.pos = pivot + n * minDistance;
                float vr = Vector2.Dot(b.vel - pivotVel, n);
                if (vr < 0f) b.vel -= n * vr * 1.5f;
            }
        }
        // Speed limit relative to the thruster: a fast thruster must not cap the forward-swinging balls.
        b.vel = pivotVel + Vector2.ClampMagnitude(b.vel - pivotVel, maxSpeed);
    }

    /// <summary>Balls in a shell push toward the middle of the gap between their neighbours.</summary>
    void ComputeSpacing()
    {
        Vector2 pivot = PivotPosition;
        float dir = SpinDir;
        for (int s = 0; s < shellCapacities.Length; s++)
        {
            shellScratch.Clear();
            foreach (var b in balls) if (b.shell == s) shellScratch.Add(b);
            int n = shellScratch.Count;
            if (n == 0) continue;
            if (n == 1) { shellScratch[0].spacing = 0f; continue; }

            shellScratch.Sort((x, y) => Angle(x, pivot, dir).CompareTo(Angle(y, pivot, dir)));
            float L = ShellLength(s);
            for (int i = 0; i < n; i++)
            {
                float a = Angle(shellScratch[i], pivot, dir);
                float aNext = Angle(shellScratch[(i + 1) % n], pivot, dir);
                float aPrev = Angle(shellScratch[(i - 1 + n) % n], pivot, dir);
                float ahead = Mathf.Repeat(aNext - a, Mathf.PI * 2f);
                float behind = Mathf.Repeat(a - aPrev, Mathf.PI * 2f);
                shellScratch[i].spacing = spacingStrength * (ahead - behind) * L;
            }
        }
    }

    static float Angle(Ball b, Vector2 pivot, float dir)
    {
        Vector2 r = b.pos - pivot;
        return Mathf.Atan2(r.y, r.x) * dir;
    }

    /// <summary>Chain from the thruster to the ball's center (end hidden under the ball). Sags when slack.</summary>
    void UpdateChain(Ball b)
    {
        Vector2 pivot = PivotPosition;
        Vector2 rel = b.pos - pivot;
        float d = rel.magnitude;
        float slack = Mathf.Max(0f, ShellLength(b.shell) - d);

        Vector2 c = rel * 0.5f + Vector2.down * slack * 1.1f;
        float approxLen = 0.5f * (d + c.magnitude + (rel - c).magnitude);
        int n = b.links.Length;
        float linkLen = Mathf.Max(0.2f, approxLen / n * 1.45f);

        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f) / n;
            float u = 1f - t;
            Vector2 p = 2f * u * t * c + t * t * rel;
            Vector2 tan = 2f * u * c + 2f * t * (rel - c);
            float ang = tan.sqrMagnitude > 1e-6f ? Mathf.Atan2(tan.y, tan.x) * Mathf.Rad2Deg - 90f : 0f;
            Transform lt = b.links[i].transform;
            lt.position = pivot + p;
            lt.rotation = Quaternion.Euler(0f, 0f, ang);
            lt.localScale = Vector3.one * (i % 2 == 1 ? 0.13f : 0.08f);   // dotted field strand
        }
    }

    // ---------------------------------------------------------------- visuals

    void UpdateVisuals(float dt)
    {
        hitFlash = Mathf.MoveTowards(hitFlash, 0f, dt * 4f);

        // Pod.
        Color body = Color.Lerp(podColor, Color.white, hitFlash);
        if (invulTimer > 0f)
        {
            invulTimer -= dt;
            body.a = Mathf.Repeat(invulTimer * 10f, 1f) > 0.4f ? 1f : 0.25f;
        }
        podBody.color = body;
        float tremble = 1f + 0.035f * Mathf.Sin(Time.time * 23f) + 0.02f * Mathf.Sin(Time.time * 37f);
        podBody.transform.localScale = Vector3.one * coreRadius * 2.3f * tremble;
        podBody.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 18f);
        podGlow.color = new Color(1f, Mathf.Lerp(0.85f, 0.4f, hitFlash), Mathf.Lerp(0.5f, 0.4f, hitFlash),
            0.3f + 0.1f * Mathf.Sin(Time.time * 3f) + 0.4f * hitFlash);

        for (int s = 0; s < shellRings.Length; s++)
        {
            Color c = ShellColor(s);
            c.a = ShellCount(s) > 0 ? 0.1f : 0.025f;
            shellRings[s].color = c;
        }

        grabHint.color = alive && dragSource == DragSource.None
            ? new Color(1f, 0.85f, 0.5f, 0.1f + 0.08f * Mathf.Sin(Time.time * 4f))
            : Color.clear;

        // Faint ring showing the thruster's extra pickup reach (Pickup Range upgrade).
        if (pickupBonus > 0f && alive)
        {
            reachRing.transform.localScale = Vector3.one * (coreRadius + pickupBonus) / 0.485f;
            reachRing.color = new Color(Pickup.Gold.r, Pickup.Gold.g, Pickup.Gold.b, 0.12f);
        }
        else reachRing.color = Color.clear;
        float size = BallWorldSize;

        // Balls.
        foreach (var b in balls)
        {
            b.snapFlash = Mathf.MoveTowards(b.snapFlash, 0f, dt * 5f);
            b.damageFlash = Mathf.MoveTowards(b.damageFlash, 0f, dt * 4f);
            b.rockTimer -= dt;
            float speed01 = (b.vel - pivotVel).magnitude / Mathf.Max(1f, maxSpeed);
            Color c = ShellColor(b.shell);
            b.body.color = Color.Lerp(c, Color.white, b.damageFlash);

            // Armor ring: one extra ring brightness per extra hit the ball can still take.
            if (b.hp > 1)
            {
                b.armor.color = new Color(1f, 1f, 1f, Mathf.Min(0.9f, 0.3f + 0.2f * (b.hp - 2)));
                b.armor.transform.localScale = Vector3.one * size * (1.45f + 0.1f * Mathf.Min(b.hp - 2, 3));
            }
            else
            {
                b.armor.color = Color.clear;
            }
            Color g = c;
            g.a = 0.3f + 0.45f * speed01;
            b.glow.color = g;
            b.glow.transform.localScale = Vector3.one * size * (2.5f + 0.8f * speed01);

            // Bond to the core: a faint dotted strand in the shell's color that shimmers along its length.
            Color bond = Color.Lerp(c, Color.white, b.snapFlash * 0.7f);
            float baseA = (b.taut ? 0.4f : 0.2f) + 0.5f * b.snapFlash;
            for (int i = 0; i < b.links.Length; i++)
            {
                float shimmer = 0.6f + 0.4f * Mathf.Sin(Time.time * 6f - i * 0.9f);
                b.links[i].color = new Color(bond.r, bond.g, bond.b, baseA * shimmer);
            }
        }

        UpdateThruster(dt);
        UpdateHeadingArrow();
    }

    void UpdateThruster(float dt)
    {
        float power = thrust.magnitude;
        if (power > 0.02f)
            thruster.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(thrust.y, thrust.x) * Mathf.Rad2Deg);

        // Emergency boost: a bigger, blue-hot flame along the direction of travel (fades out at the end).
        float bk = alive && boostTimer > 0f ? Mathf.Clamp01(boostTimer / 0.3f) : 0f;
        if (bk > 0f && power <= 0.02f && pivotVel.sqrMagnitude > 0.01f)
            thruster.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(pivotVel.y, pivotVel.x) * Mathf.Rad2Deg);
        float drive = Mathf.Max(power, bk);

        float a = drive > 0.02f ? 0.45f + 0.45f * drive : 0.12f;
        if (!alive) a = 0f;
        Color fc = Color.Lerp(driveColor, boostColor, bk);
        Vector2 back = -(Vector2)thruster.right;

        // Soft wake hugging the back of the nucleus, and a bright spot where the drive pushes off.
        float len = (0.3f + 0.7f * drive) * (1f + 0.5f * bk);
        flame.transform.localPosition = new Vector3(-0.15f - len * 0.3f, 0f, 0f);
        flame.transform.localScale = new Vector3(len * 1.1f, 0.55f + 0.25f * drive, 1f);
        flame.color = new Color(fc.r, fc.g, fc.b, a * 0.55f);
        flameCore.transform.localPosition = new Vector3(-coreRadius * 0.9f, 0f, 0f);
        flameCore.transform.localScale = Vector3.one * (0.3f + 0.25f * drive) * (0.9f + 0.1f * Mathf.Sin(Time.time * 30f));
        flameCore.color = new Color(1f, 1f, 1f, a * 0.8f);

        // Wavefronts: arcs that leave the back of the core, spread and fade.
        if (alive && drive > 0.05f)
        {
            pulseTimer -= dt;
            if (pulseTimer <= 0f)
            {
                pulseTimer = Mathf.Lerp(0.2f, 0.07f, drive) * (bk > 0f ? 0.6f : 1f);
                int i = pulseNext;
                pulseNext = (pulseNext + 1) % pulses.Length;
                pulseAge[i] = 0f;
                pulseVel[i] = pivotVel * 0.3f + back * (2.5f + 3.5f * drive);
                pulses[i].transform.position = PivotPosition + back * (coreRadius + 0.05f);
                pulses[i].transform.rotation = thruster.rotation;
            }
        }
        const float life = 0.5f;
        for (int i = 0; i < pulses.Length; i++)
        {
            if (pulseAge[i] >= life) { pulses[i].color = Color.clear; continue; }
            pulseAge[i] += dt;
            float k = Mathf.Clamp01(pulseAge[i] / life);
            Transform t = pulses[i].transform;
            t.position += (Vector3)(pulseVel[i] * dt);
            float sz = 0.35f + 0.9f * k;
            t.localScale = new Vector3(sz * 0.45f, sz, 1f);   // flattened: reads as an arc / wavefront
            pulses[i].color = new Color(fc.r, fc.g, fc.b, (1f - k) * 0.75f);
        }

        // A few motes of light in the wake.
        if (alive && drive > 0.05f)
        {
            thrusterEmit -= dt;
            if (thrusterEmit <= 0f)
            {
                thrusterEmit = Mathf.Lerp(0.08f, 0.03f, drive) * (bk > 0f ? 0.5f : 1f);
                Vector2 origin = PivotPosition + back * (coreRadius + 0.1f);
                Vector2 v = pivotVel * 0.5f + back * Random.Range(2f, 4f) * drive + Random.insideUnitCircle * 0.8f;
                Color c = Color.Lerp(fc, Color.white, Random.value * 0.6f);
                c.a = 0.7f;
                ParticleBursts.Emit(origin, v, c, Random.Range(0.06f, 0.12f), Random.Range(0.25f, 0.45f), 3f);
            }
        }
    }

    // ------------------------------------------------------------ gameplay api

    /// <summary>Adds a ball to the innermost shell with room. Returns false if every shell is full.</summary>
    public bool AddBall()
    {
        if (!alive) return false;
        if (turretCount < turretSlots)   // Spaceship: the inner orbital's slots are turrets
        {
            AddTurret(true);
            return true;
        }
        for (int s = 0; s < shellCapacities.Length; s++)
        {
            if (ShellCount(s) >= shellCapacities[s]) continue;

            // Place it in the middle of the biggest gap in this shell.
            Vector2 pivot = PivotPosition;
            float dir = SpinDir;
            shellScratch.Clear();
            foreach (var b in balls) if (b.shell == s) shellScratch.Add(b);
            float angle;
            if (shellScratch.Count == 0)
            {
                angle = Random.Range(0f, Mathf.PI * 2f);
            }
            else
            {
                shellScratch.Sort((x, y) => Angle(x, pivot, dir).CompareTo(Angle(y, pivot, dir)));
                float bestGap = -1f;
                angle = 0f;
                for (int i = 0; i < shellScratch.Count; i++)
                {
                    float a = Angle(shellScratch[i], pivot, dir);
                    float gap = shellScratch.Count == 1
                        ? Mathf.PI * 2f
                        : Mathf.Repeat(Angle(shellScratch[(i + 1) % shellScratch.Count], pivot, dir) - a, Mathf.PI * 2f);
                    if (gap > bestGap) { bestGap = gap; angle = a + gap * 0.5f; }
                }
                angle *= dir; // back to world angle
            }

            float L = ShellLength(s);
            Vector2 n = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 tan = new Vector2(-n.y, n.x) * dir;
            var ball = CreateBall(s, pivot + n * L, pivotVel + tan * spinSpeed * Mathf.Deg2Rad * L);
            ParticleBursts.Burst(ball.pos, ShellColor(s), 14, 4f, 0.22f, 0.5f);
            return true;
        }
        return false;
    }

    /// <summary>True if the thruster or any ball overlaps the circle (used for collecting pickups).</summary>
    public bool Touches(Vector2 p, float r)
    {
        if (!alive || meltingDown) return false;
        float rc = r + coreRadius + pickupBonus;
        if ((PivotPosition - p).sqrMagnitude < rc * rc) return true;
        float rb = r + BallRadius + pickupBonus;
        foreach (var b in balls)
            if ((b.pos - p).sqrMagnitude < rb * rb) return true;
        return false;
    }

    /// <summary>
    /// If an enemy at p (radius r) touches a ball, the ball takes a hit (destroyed when out of
    /// hits) and true is returned (the enemy is destroyed either way).
    /// </summary>
    public bool TryAbsorbHit(Vector2 p, float r, Color enemyColor)
    {
        if (!alive || meltingDown) return false;
        float rb = r + BallRadius;
        for (int i = 0; i < balls.Count; i++)
        {
            Ball b = balls[i];
            if ((b.pos - p).sqrMagnitude >= rb * rb) continue;
            b.hp--;
            if (b.hp <= 0)
            {
                RemoveBall(i, enemyColor);
            }
            else
            {
                b.damageFlash = 1f;
                ParticleBursts.Burst(b.pos, Color.white, 10, 4f, 0.2f, 0.4f);
                Sfx.Play("snap", 0.8f);
            }
            OnHitReaction();
            return true;
        }
        return false;
    }

    /// <summary>Applies upgrade levels on top of the base (Inspector) values.</summary>
    public void ApplyUpgrades(float speedMultiplier, float thrustMultiplier, float extraCounterThrust,
        float extraTurnAuthority, int extraBallHits)
    {
        maxPivotSpeed = baseMaxSpeed * speedMultiplier;
        thrustAcceleration = baseThrust * thrustMultiplier;
        counterThrust = baseCounterThrust + extraCounterThrust;
        turnAuthority = Mathf.Clamp01(baseTurnAuthority + extraTurnAuthority);
        int newMax = Mathf.Max(1, baseBallHits + extraBallHits);
        int gained = newMax - ballMaxHits;
        ballMaxHits = newMax;
        if (gained > 0) foreach (var b in balls) b.hp += gained;   // existing balls get the armor too
    }

    /// <summary>
    /// Upgrades beyond movement: ball size multiplier, extra pickup reach (world units),
    /// and the Repel / Emergency Booster levels (0 = locked).
    /// </summary>
    public void ApplyExtraUpgrades(float ballScaleMultiplier, float pickupRangeBonus, int repel, int booster)
    {
        ballScale = Mathf.Max(0.1f, ballScaleMultiplier);
        pickupBonus = Mathf.Max(0f, pickupRangeBonus);
        repelLevel = Mathf.Max(0, repel);
        boostLevel = Mathf.Max(0, booster);
        float size = BallWorldSize;
        foreach (var b in balls)
        {
            b.body.transform.localScale = Vector3.one * size;
            b.trail.widthMultiplier = size * 0.55f;
        }
    }

    // ---------------------------------------------------------- hit reactions

    /// <summary>The thruster or a ball was hit: fire the unlocked Repel blast and Emergency Booster.</summary>
    void OnHitReaction()
    {
        if (!alive) return;
        if (boostLevel > 0) TriggerBoost();
        if (repelLevel > 0 && repelTimer <= 0f) RepelBlast();
    }

    /// <summary>Shockwave around the thruster that knocks enemies back and turns them away (never kills).</summary>
    void RepelBlast()
    {
        repelTimer = repelCooldown;
        int extra = repelLevel - 1;
        float radius = repelRadius + extra * repelRadiusPerLevel;
        float force = repelForce + extra * repelForcePerLevel;
        float turn = Mathf.Clamp01(repelTurn + extra * repelTurnPerLevel);
        Vector2 c = PivotPosition;

        foreach (var e in EnemyMover.All) e.Repel(c, radius, force, turn);
        foreach (var h in Hazard.All) h.Repel(c, radius, force, asteroidRepelMass);

        ShockRing.Spawn(c, radius, repelColor);
        ParticleBursts.Burst(c, repelColor, 26, radius * 2.2f, 0.22f, 0.45f);
        Sfx.Play("repel");
        if (GameManager.Instance != null) GameManager.Instance.AddShake(0.25f);
    }

    /// <summary>Temporarily raises top speed and thrust, with a kick toward where you're steering.</summary>
    void TriggerBoost()
    {
        bool fresh = boostTimer <= 0f;
        int extra = boostLevel - 1;
        boostMult = boostMultiplier + extra * boostMultiplierPerLevel;
        boostTimer = boostDuration + extra * boostDurationPerLevel;

        Vector2 dir = thrust.sqrMagnitude > 0.01f ? thrust.normalized
                    : pivotVel.sqrMagnitude > 0.01f ? pivotVel.normalized : Vector2.zero;
        if (dir != Vector2.zero)
        {
            Vector2 old = pivotVel;
            pivotVel += dir * maxPivotSpeed * (boostMult - 1f) * 0.6f;
            CarryBalls(pivotVel - old);
        }
        if (fresh)
        {
            Sfx.Play("boost");
            ParticleBursts.Burst(PivotPosition, boostColor, 18, 6f, 0.25f, 0.5f);
        }
    }

    void RebuildShellRings()
    {
        foreach (var r in shellRings) if (r != null) Destroy(r.gameObject);
        shellRings = new SpriteRenderer[shellCapacities.Length];
        for (int s = 0; s < shellRings.Length; s++)
            shellRings[s] = NewSprite("ShellRing" + s, pod, ProcSprites.Ring, Color.clear, -10, ShellLength(s) / 0.485f);
    }

    /// <summary>
    /// Replaces the shell layout (used by game-changing upgrades). Balls in shells that no
    /// longer exist are removed; balls in surviving shells simply get the new rope length.
    /// </summary>
    public void SetShellLayout(int[] capacities, float[] lengths)
    {
        shellCapacities = (int[])capacities.Clone();
        shellLengths = (float[])lengths.Clone();
        for (int i = balls.Count - 1; i >= 0; i--)
        {
            if (balls[i].shell < shellCapacities.Length) continue;
            ParticleBursts.Burst(balls[i].pos, ShellColor(balls[i].shell), 12, 6f, 0.28f, 0.7f);
            Destroy(balls[i].root);
            balls.RemoveAt(i);
        }
        RebuildShellRings();
    }

    /// <summary>Gravity well: pulls the thruster and each ball toward the hazard.</summary>
    public void ApplyPull(Hazard h, float dt)
    {
        if (!alive) return;
        pivotVel += h.PullAt(PivotPosition) * dt;
        foreach (var b in balls) b.vel += h.PullAt(b.pos) * dt;
    }

    /// <summary>
    /// Solid circle (asteroid / gravity-well core). A well's core destroys balls outright; an
    /// asteroid bounces them off and costs one hit (armor counts). The thruster is pushed out,
    /// bounced, and takes a hit.
    /// </summary>
    /// <param name="ejectSpeed">Extra outward speed given to the thruster on contact.</param>
    /// <param name="crushBalls">True for a well's core: balls are destroyed regardless of armor.</param>
    public void HazardCircle(Vector2 c, float r, Vector2 hazardVel, Color col, float ejectSpeed, bool crushBalls = true)
    {
        if (!alive || meltingDown) return;

        float rb = r + BallRadius;
        bool hit = false;
        for (int i = balls.Count - 1; i >= 0; i--)
        {
            Ball b = balls[i];
            Vector2 off = b.pos - c;
            if (off.sqrMagnitude >= rb * rb) continue;
            if (crushBalls) { RemoveBall(i, col); hit = true; continue; }

            // Asteroid: bounce off the surface, and lose one hit (then a short grace period).
            float bd = off.magnitude;
            Vector2 bn = bd > 1e-4f ? off / bd : Vector2.up;
            b.pos = c + bn * rb;
            float bvr = Vector2.Dot(b.vel - hazardVel, bn);
            if (bvr < 0f) b.vel -= bn * bvr * 1.5f;
            if (b.rockTimer > 0f) continue;
            b.rockTimer = asteroidHitCooldown;
            hit = true;
            if (--b.hp <= 0) RemoveBall(i, col);
            else
            {
                b.damageFlash = 1f;
                ParticleBursts.Burst(b.pos, col, 10, 4f, 0.2f, 0.4f);
                Sfx.Play("snap", 0.8f);
            }
        }
        if (hit) OnHitReaction();

        Vector2 rel = PivotPosition - c;
        float d = rel.magnitude;
        float rc = r + coreRadius;
        if (d >= rc) return;

        Vector2 n = d > 1e-4f ? rel / d : Vector2.up;
        transform.position = new Vector3(c.x + n.x * rc, c.y + n.y * rc, transform.position.z);

        Vector2 old = pivotVel;
        float vr = Vector2.Dot(pivotVel - hazardVel, n);
        if (vr < 0f) pivotVel -= n * vr * 1.6f;          // bounce off
        pivotVel += n * ejectSpeed;
        CarryBalls(pivotVel - old);

        if (CanBeHit)
        {
            ParticleBursts.Burst(PivotPosition - n * coreRadius, col, 14, 5f, 0.3f, 0.6f);
            TakeHit(n * 3f, col);
        }
    }

    void RemoveBall(int index, Color hitColor)
    {
        Ball b = balls[index];
        ParticleBursts.Burst(b.pos, ShellColor(b.shell), 16, 6f, 0.28f, 0.6f);
        ParticleBursts.Burst(b.pos, hitColor, 10, 5f, 0.25f, 0.5f);
        Sfx.Play("pop");
        if (GameManager.Instance != null) GameManager.Instance.AddShake(0.2f);
        Destroy(b.root);
        balls.RemoveAt(index);
    }

    void ClearBalls(bool burst)
    {
        for (int i = balls.Count - 1; i >= 0; i--)
        {
            if (burst) ParticleBursts.Burst(balls[i].pos, ShellColor(balls[i].shell), 12, 6f, 0.28f, 0.7f);
            Destroy(balls[i].root);
        }
        balls.Clear();
    }

    /// <summary>Called by an enemy that reached the thruster.</summary>
    public void TakeHit(Vector2 force, Color enemyColor)
    {
        if (!CanBeHit) return;
        Vector2 old = pivotVel;
        pivotVel = Vector2.ClampMagnitude(pivotVel + force * impactScale, maxPivotSpeed * 1.5f);
        CarryBalls(pivotVel - old);
        invulTimer = invulnerableTime;
        hitFlash = 1f;
        ParticleBursts.Burst(PivotPosition, enemyColor, 18, 7f, 0.3f, 0.6f);
        ParticleBursts.Burst(PivotPosition, podColor, 10, 4f, 0.25f, 0.5f);
        if (GameManager.Instance != null) GameManager.Instance.OnPlayerHit();
        OnHitReaction();
    }

    /// <summary>Destroys the thruster and all balls (game over).</summary>
    public void Kill()
    {
        if (!alive) return;
        alive = false;
        meltingDown = false;
        pod.localPosition = Vector3.zero;
        EndDrag();
        ParticleBursts.Burst(PivotPosition, podColor, 40, 10f, 0.35f, 1.1f);
        ParticleBursts.Burst(PivotPosition, driveColor, 20, 7f, 0.3f, 0.8f);
        ClearBalls(true);
        ClearTurrets(true);
        pod.gameObject.SetActive(false);
    }

    /// <summary>
    /// Prepares for a run. hard = stop moving and remove all balls (after dying / from the
    /// title); otherwise keep balls and motion. The thruster keeps its world position.
    /// </summary>
    public void ResetState(bool hard = true)
    {
        alive = true;
        meltingDown = false;
        pod.localPosition = Vector3.zero;
        invulTimer = 0f;
        hitFlash = 0f;
        boostTimer = 0f;
        repelTimer = 0f;
        pod.gameObject.SetActive(true);
        if (!hard) return;
        pivotVel = Vector2.zero;
        ClearBalls(false);
        ClearTurrets(false);
    }

    // -------------------------------------------------------- spaceship turrets

    /// <summary>
    /// Spaceship: replaces the inner orbital with 'slots' turrets (0 = back to normal).
    /// Balls already in the inner orbital become turrets; the other balls move in one shell
    /// index (call SetShellLayout with the inner shell removed right after).
    /// </summary>
    public void SetTurretSlots(int slots)
    {
        slots = Mathf.Max(0, slots);
        if (slots == turretSlots) return;

        if (turretSlots > 0)
        {
            // Back to normal: the outer balls move out again.
            ClearTurrets(false);
            foreach (var t in turrets) Destroy(t.root);
            turrets.Clear();
            foreach (var b in balls) b.shell++;
            shellColorOffset = 0;
            turretSlots = 0;
        }
        if (slots == 0) return;

        int converted = 0;
        for (int i = balls.Count - 1; i >= 0; i--)
        {
            if (balls[i].shell != 0) continue;
            converted++;
            ParticleBursts.Burst(balls[i].pos, ShellColor(0), 12, 5f, 0.25f, 0.5f);
            Destroy(balls[i].root);
            balls.RemoveAt(i);
        }
        foreach (var b in balls) b.shell--;
        shellColorOffset = 1;

        if (turretRig == null)
        {
            turretRig = new GameObject("Turrets").transform;
            turretRig.SetParent(pod, false);
        }
        rigAngle = thruster.eulerAngles.z;
        turretRig.localRotation = Quaternion.Euler(0f, 0f, rigAngle);
        turretSlots = slots;
        for (int i = 0; i < slots; i++) turrets.Add(BuildTurret(i, slots));
        for (int i = 0; i < Mathf.Min(converted, slots); i++) AddTurret(true);
    }

    /// <summary>Fire Rate (shots/s multiplier) and Turret Range (extra world units) upgrades.</summary>
    public void ApplyTurretUpgrades(float fireRateMultiplier, float rangeBonus)
    {
        fireRateMult = Mathf.Max(0.05f, fireRateMultiplier);
        turretRangeBonus = Mathf.Max(0f, rangeBonus);
    }

    Turret BuildTurret(int index, int count)
    {
        var t = new Turret();
        t.side = 90f + index * 360f / count;   // 2 turrets: left and right of the thrust direction
        Vector2 dir = new Vector2(Mathf.Cos(t.side * Mathf.Deg2Rad), Mathf.Sin(t.side * Mathf.Deg2Rad));
        Color strand = new Color(turretColor.r, turretColor.g, turretColor.b, 0.35f);

        t.root = new GameObject("Turret" + index);
        t.root.transform.SetParent(turretRig, false);

        // A faint glowing strand from the nucleus out to the emitter.
        float inner = coreRadius * 0.6f;
        var arm = NewSprite("Strand", t.root.transform, ProcSprites.Pixel, strand, 2, 1f);
        arm.transform.localPosition = dir * (inner + turretArmLength) * 0.5f;
        arm.transform.localRotation = Quaternion.Euler(0f, 0f, t.side);
        arm.transform.localScale = new Vector3(turretArmLength - inner, 0.035f, 1f);

        t.mount = new GameObject("Mount").transform;
        t.mount.SetParent(t.root.transform, false);
        t.mount.localPosition = dir * turretArmLength;
        t.glow = NewSprite("Glow", t.mount, ProcSprites.Glow, Color.clear, 6, 0.6f);
        t.symbol = NewSprite("Trefoil", t.mount, ProcSprites.Trefoil, new Color(turretColor.r, turretColor.g, turretColor.b, 0.85f), 7, 0.36f);

        t.barrel = new GameObject("Barrel").transform;
        t.barrel.SetParent(t.mount, false);
        t.tube = NewSprite("Beam", t.barrel, ProcSprites.Glow, new Color(turretColor.r, turretColor.g, turretColor.b, 0.8f), 8, 1f);
        t.tube.transform.localScale = new Vector3(0.36f, 0.1f, 1f);
        t.muzzle = NewSprite("Muzzle", t.barrel, ProcSprites.Glow, Color.clear, 9, 0.4f);
        t.muzzle.transform.localPosition = new Vector3(0.3f, 0f, 0f);
        t.hub = NewSprite("Hub", t.mount, ProcSprites.Ball, turretColor, 9, 0.13f);

        t.aim = t.side;
        t.root.SetActive(false);
        return t;
    }

    void AddTurret(bool effects)
    {
        if (turretCount >= turrets.Count) return;
        Turret t = turrets[turretCount];
        turretCount++;
        t.appear = 0f;
        t.recoil = t.flash = 0f;
        t.aim = rigAngle + t.side;
        t.cooldown = 0.25f + 0.3f * (turretCount - 1);   // staggered so they don't fire in unison
        t.root.SetActive(true);
        if (effects)
        {
            float a = (rigAngle + t.side) * Mathf.Deg2Rad;
            Vector2 at = PivotPosition + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * turretArmLength;
            ParticleBursts.Burst(at, turretColor, 16, 4f, 0.22f, 0.5f);
        }
    }

    void ClearTurrets(bool burst)
    {
        for (int i = 0; i < turretCount && i < turrets.Count; i++)
        {
            if (burst) ParticleBursts.Burst(turrets[i].mount.position, turretColor, 12, 6f, 0.28f, 0.7f);
            turrets[i].root.SetActive(false);
        }
        turretCount = 0;
    }

    void UpdateTurrets(float dt)
    {
        if (turretSlots == 0 || turretRig == null) return;

        // The struts follow the thrust direction (smoothly); the barrels aim on their own.
        rigAngle = Mathf.MoveTowardsAngle(rigAngle, thruster.eulerAngles.z, turretMountTurnSpeed * dt);
        turretRig.localRotation = Quaternion.Euler(0f, 0f, rigAngle);

        float range = TurretRangeNow;
        EnemyMover claimed = null;   // the next turret prefers a different target
        for (int i = 0; i < turretCount && i < turrets.Count; i++)
        {
            Turret t = turrets[i];
            t.appear = Mathf.MoveTowards(t.appear, 1f, dt * 4f);
            t.recoil = Mathf.MoveTowards(t.recoil, 0f, dt * 9f);
            t.flash = Mathf.MoveTowards(t.flash, 0f, dt * 14f);
            t.cooldown -= dt;

            Vector2 mount = t.mount.position;
            float desired = rigAngle + t.side;   // idle: point straight out
            EnemyMover target = alive && dt > 0f ? FindTarget(mount, range, claimed) : null;
            if (target != null)
            {
                Vector2 d = Lead(mount, target) - mount;
                desired = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                claimed = target;
            }
            t.aim = Mathf.MoveTowardsAngle(t.aim, desired, turretTurnSpeed * dt);
            if (target != null && t.cooldown <= 0f && Mathf.Abs(Mathf.DeltaAngle(t.aim, desired)) < 8f)
                Fire(t);

            // Visuals: pop in when built, recoil and muzzle flash when firing.
            float a = t.appear - 1f;
            float pop = 1f + 2.7f * a * a * a + 1.7f * a * a;   // ease-out-back
            t.root.transform.localScale = Vector3.one * Mathf.Max(0.01f, pop);
            t.barrel.rotation = Quaternion.Euler(0f, 0f, t.aim);
            t.tube.transform.localPosition = new Vector3(0.13f - 0.07f * t.recoil, 0f, 0f);
            t.muzzle.color = new Color(turretColor.r, turretColor.g, turretColor.b, 0.9f * t.flash);
            t.muzzle.transform.localScale = Vector3.one * (0.25f + 0.3f * t.flash);
            t.hub.color = Color.Lerp(turretColor, Color.white, t.flash * 0.6f);
            t.glow.color = new Color(turretColor.r, turretColor.g, turretColor.b, 0.2f + 0.1f * Mathf.Sin(Time.time * 4f + i) + 0.3f * t.flash);
            t.symbol.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 40f);
        }
    }

    /// <summary>Nearest living enemy in range; 'avoid' is skipped if there is any other choice.</summary>
    static EnemyMover FindTarget(Vector2 from, float range, EnemyMover avoid)
    {
        EnemyMover best = null, second = null;
        float bestD = range * range, secondD = range * range;
        foreach (var e in EnemyMover.All)
        {
            if (e == null || e.IsDead) continue;
            float d = (e.Position - from).sqrMagnitude;
            if (d >= secondD) continue;
            if (d < bestD)
            {
                second = best; secondD = bestD;
                best = e; bestD = d;
            }
            else
            {
                second = e; secondD = d;
            }
        }
        return best == avoid && second != null ? second : best;
    }

    /// <summary>Where to aim so a bullet meets the enemy (one refinement of the lead).</summary>
    Vector2 Lead(Vector2 from, EnemyMover e)
    {
        float s = Mathf.Max(1f, turretBulletSpeed);
        Vector2 p = e.Position, v = e.Velocity;
        float t = (p - from).magnitude / s;
        t = (p + v * t - from).magnitude / s;
        return p + v * t;
    }

    void Fire(Turret t)
    {
        t.cooldown = 1f / Mathf.Max(0.05f, turretFireRate * fireRateMult);
        t.recoil = t.flash = 1f;
        float a = t.aim * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        Vector2 muzzle = (Vector2)t.mount.position + dir * 0.3f;
        float speed = Mathf.Max(1f, turretBulletSpeed);
        TurretBullet.Spawn(muzzle, dir * speed, (TurretRangeNow + 0.6f) / speed, turretColor);
        for (int i = 0; i < 3; i++)
            ParticleBursts.Emit(muzzle, dir * Random.Range(2f, 4f) + Random.insideUnitCircle * 1.2f + pivotVel * 0.5f,
                turretColor, 0.1f, 0.15f, 6f);
        Sfx.Play("shoot", 0.32f);
    }
}
