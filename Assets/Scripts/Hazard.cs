using System.Collections.Generic;
using UnityEngine;

public enum HazardType { Asteroid, GravityWell }

/// <summary>
/// Environmental hazards. They drift in from off-screen with NO warning.
///
/// Asteroid:    huge, indestructible, slow. Crushes any ball it touches (ignores armor),
///              costs the thruster a heart and bounces it away, and destroys enemies that
///              fly into it (so it can be used as cover).
/// GravityWell: pulls the thruster, its balls and enemies toward it. Its core crushes
///              balls/enemies and hurts the thruster (which is flung back out).
/// </summary>
public class Hazard : MonoBehaviour
{
    public static readonly List<Hazard> All = new List<Hazard>();

    public HazardType Type { get; private set; }
    /// <summary>Solid/crushing radius (asteroid surface, or the well's core).</summary>
    public float radius;
    public Vector2 velocity;
    public float pullRadius, pullStrength;
    public float corePullMultiplier = 1f;

    static readonly Color RockColor = new Color(0.66f, 0.6f, 0.54f);
    static readonly Color WellColor = new Color(0.6f, 0.35f, 1f);

    Transform body;
    float spin, age;
    SpriteRenderer[] swirl;
    SpriteRenderer coreGlow, disk;
    SpriteRenderer cueIcon, cueGlow, cueArrow;   // Insight: edge-of-screen cue while off-screen

    void Start()
    {
        Sprite icon = Type == HazardType.Asteroid ? ProcSprites.Asteroid(0) : ProcSprites.Ring;
        cueGlow = Make("CueGlow", ProcSprites.Glow, Color.clear, 44, 1f);
        cueIcon = Make("Cue", icon, Color.clear, 45, 1f);
        cueArrow = Make("CueArrow", ProcSprites.Arrow, Color.clear, 45, 0.3f);
        gameObject.AddComponent<HazardCue>().hazard = this;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    SpriteRenderer Make(string name, Sprite sprite, Color c, int order, float scale)
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

    public void InitAsteroid(float r, Vector2 vel)
    {
        Type = HazardType.Asteroid;
        Codex.See(Codex.Hazard(Type));
        radius = r;
        velocity = vel;
        spin = Random.Range(-12f, 12f);
        Color tint = RockColor * Random.Range(0.85f, 1.1f);
        tint.a = 1f;
        body = Make("Rock", ProcSprites.Asteroid(Random.Range(0, 3)), tint, 14, r / 0.42f).transform;
        body.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
    }

    public void InitWell(float coreRadius, float pullR, float strength, Vector2 vel, float coreMultiplier = 1f)
    {
        corePullMultiplier = Mathf.Max(1f, coreMultiplier);
        Type = HazardType.GravityWell;
        Codex.See(Codex.Hazard(Type));
        radius = coreRadius;
        pullRadius = pullR;
        pullStrength = strength;
        velocity = vel;

        Make("Darkness", ProcSprites.Glow, new Color(0.02f, 0f, 0.06f, 0.75f), -15, pullR * 1.7f);
        swirl = new SpriteRenderer[5];
        for (int i = 0; i < swirl.Length; i++)
            swirl[i] = Make("Swirl" + i, ProcSprites.Ring, Color.clear, -14, 1f);
        coreGlow = Make("CoreGlow", ProcSprites.Glow, new Color(WellColor.r, WellColor.g, WellColor.b, 0.6f), 13, coreRadius * 4.5f);
        // Black hole: a tilted accretion disk, a bright photon ring and a pitch-black core.
        float tilt = Random.Range(-35f, 35f);
        var diskGlow = Make("DiskGlow", ProcSprites.Glow, new Color(1f, 0.6f, 0.3f, 0.35f), 13, 1f).transform;
        diskGlow.localScale = new Vector3(coreRadius * 7f, coreRadius * 1.8f, 1f);
        diskGlow.localRotation = Quaternion.Euler(0f, 0f, tilt);
        disk = Make("Disk", ProcSprites.Ring, new Color(1f, 0.75f, 0.45f, 0.7f), 13, 1f);
        disk.transform.localScale = new Vector3(coreRadius * 4.6f, coreRadius * 1.3f, 1f);
        disk.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        Make("PhotonRing", ProcSprites.Ring, new Color(1f, 0.9f, 0.75f, 0.85f), 15, coreRadius * 2.4f);
        Make("Core", ProcSprites.Ball, Color.black, 14, coreRadius * 2.1f);
    }

    /// <summary>
    /// How far from the center a pickup's edge must stay so the thruster can grab it without
    /// touching the hazard: just off an asteroid's surface, well clear of a gravity well's core
    /// (whose pull would otherwise drag the player in).
    /// </summary>
    public float PickupClearance => Type == HazardType.Asteroid ? radius + 0.9f : radius + 1.8f;

    /// <summary>True if a circle at p overlaps the solid/crushing part of this hazard.</summary>
    public bool Crushes(Vector2 p, float r)
    {
        float d = radius + r;
        return ((Vector2)transform.position - p).sqrMagnitude < d * d;
    }

    /// <summary>
    /// Repel blast at 'from' (asteroids only): pushed away if the blast reaches its surface,
    /// stronger when closer, divided by the rock's mass (bigger rocks are heavier).
    /// </summary>
    public void Repel(Vector2 from, float blastRadius, float force, float averageMass)
    {
        if (Type != HazardType.Asteroid) return;
        Vector2 d = (Vector2)transform.position - from;
        float dist = d.magnitude;
        float gap = dist - radius;   // distance to the surface
        if (gap > blastRadius) return;
        Vector2 away = dist > 1e-3f ? d / dist : Vector2.up;
        float falloff = 1f - 0.5f * Mathf.Clamp01(gap / Mathf.Max(0.01f, blastRadius));
        float size = radius / 3.5f;   // 3.5 = an average asteroid
        float mass = Mathf.Max(1f, averageMass * size * size);
        velocity += away * force * falloff / mass;
    }

    /// <summary>Acceleration this hazard applies to something at p (zero for asteroids).</summary>
    public Vector2 PullAt(Vector2 p)
    {
        if (Type != HazardType.GravityWell) return Vector2.zero;
        Vector2 to = (Vector2)transform.position - p;
        float d = to.magnitude;
        if (d >= pullRadius || d < 0.01f) return Vector2.zero;
        // 0 at the edge of the pull, 1 at the center; the boost ramps up sharply toward the core.
        float k = 1f - d / pullRadius;
        float boost = Mathf.Lerp(1f, corePullMultiplier, k * k);
        return to / d * pullStrength * k * boost;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        transform.position += (Vector3)(velocity * dt);
        Vector2 pos = transform.position;

        if (Type == HazardType.Asteroid)
        {
            body.Rotate(0f, 0f, spin * dt);
        }
        else
        {
            // Rings spiral inward again and again.
            for (int i = 0; i < swirl.Length; i++)
            {
                float k = Mathf.Repeat(age * 0.35f + i / (float)swirl.Length, 1f);   // 0 = outer edge, 1 = core
                float rr = Mathf.Lerp(pullRadius, radius, k * k);
                swirl[i].transform.localScale = Vector3.one * rr / 0.485f;
                swirl[i].color = new Color(WellColor.r, WellColor.g, WellColor.b, 0.05f + 0.4f * k * (1f - k) * 2f);
            }
            coreGlow.color = new Color(WellColor.r, WellColor.g, WellColor.b, 0.45f + 0.2f * Mathf.Sin(age * 5f));
            disk.color = new Color(1f, 0.75f, 0.45f, 0.55f + 0.2f * Mathf.Sin(age * 3.1f));
        }

        var pc = PendulumController.Instance;
        if (pc != null && pc.IsAlive)
        {
            if (Type == HazardType.GravityWell) pc.ApplyPull(this, dt);
            pc.HazardCircle(pos, radius, velocity, Type == HazardType.Asteroid ? RockColor : WellColor,
                Type == HazardType.GravityWell ? 9f : 0f, Type == HazardType.GravityWell);
        }

        // Drifted far away and still leaving -> remove.
        Vector2 rel = pos - GameManager.ViewCenter;
        float far = GameManager.HalfExtents.magnitude + Mathf.Max(radius, pullRadius) + 14f;
        if ((rel.sqrMagnitude > far * far && Vector2.Dot(rel, velocity) > 0f) || age > 240f)
            Destroy(gameObject);
    }

    /// <summary>
    /// Insight (rare upgrade): while the hazard is off-screen, a cue sits on the screen edge
    /// pointing at it. It grows and brightens as the hazard gets closer, and pulses faster
    /// when it's heading toward the view.
    /// </summary>
    public void UpdateCue()
    {
        if (cueIcon == null) return;
        var gm = GameManager.Instance;
        Vector2 vc = GameManager.ViewCenter;
        Vector2 half = GameManager.HalfExtents;
        Vector2 d = (Vector2)transform.position - vc;
        float extent = Type == HazardType.Asteroid ? radius : pullRadius * 0.6f;
        bool onScreen = Mathf.Abs(d.x) < half.x + extent && Mathf.Abs(d.y) < half.y + extent;
        if (gm == null || !gm.HasInsight || onScreen || gm.CurrentState != GameManager.State.Playing)
        {
            cueIcon.color = cueGlow.color = cueArrow.color = Color.clear;
            return;
        }

        Vector2 inner = half - new Vector2(0.9f, 0.9f);
        float k = Mathf.Min(inner.x / Mathf.Max(Mathf.Abs(d.x), 1e-4f), inner.y / Mathf.Max(Mathf.Abs(d.y), 1e-4f));
        Vector2 pos = vc + d * k;
        float beyond = Mathf.Max(0f, d.magnitude * (1f - k) - extent);
        float close = 1f - Mathf.Clamp01(beyond / 20f);
        bool incoming = Vector2.Dot(velocity, -d) > 0f;
        float pulse = 1f + (incoming ? 0.15f : 0.05f) * Mathf.Sin(age * (incoming ? 9f : 3f));
        float size = Mathf.Lerp(0.35f, 0.8f, close * close) * pulse;
        Color c = Type == HazardType.Asteroid ? RockColor : WellColor;
        Vector2 n = d.normalized;

        // The cue is a child, so undo this object's own transform.
        cueIcon.transform.position = pos;
        cueIcon.transform.rotation = Quaternion.identity;
        cueIcon.transform.localScale = Vector3.one * size;
        cueIcon.color = new Color(c.r, c.g, c.b, 0.55f + 0.45f * close);

        cueGlow.transform.position = pos;
        cueGlow.transform.localScale = Vector3.one * size * 2.8f;
        cueGlow.color = new Color(c.r, c.g, c.b, 0.15f + 0.3f * close);

        cueArrow.transform.position = pos + n * (size * 0.6f + 0.2f);
        cueArrow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg);
        cueArrow.transform.localScale = Vector3.one * (0.2f + 0.12f * close);
        cueArrow.color = new Color(c.r, c.g, c.b, 0.5f + 0.4f * close);
    }
}
