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

    static readonly Color RockColor = new Color(0.66f, 0.6f, 0.54f);
    static readonly Color WellColor = new Color(0.6f, 0.35f, 1f);

    Transform body;
    float spin, age;
    SpriteRenderer[] swirl;
    SpriteRenderer coreGlow;

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
        radius = r;
        velocity = vel;
        spin = Random.Range(-12f, 12f);
        Color tint = RockColor * Random.Range(0.85f, 1.1f);
        tint.a = 1f;
        body = Make("Rock", ProcSprites.Asteroid(Random.Range(0, 3)), tint, 14, r / 0.42f).transform;
        body.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
    }

    public void InitWell(float coreRadius, float pullR, float strength, Vector2 vel)
    {
        Type = HazardType.GravityWell;
        radius = coreRadius;
        pullRadius = pullR;
        pullStrength = strength;
        velocity = vel;

        Make("Darkness", ProcSprites.Glow, new Color(0.02f, 0f, 0.06f, 0.75f), -15, pullR * 1.7f);
        swirl = new SpriteRenderer[5];
        for (int i = 0; i < swirl.Length; i++)
            swirl[i] = Make("Swirl" + i, ProcSprites.Ring, Color.clear, -14, 1f);
        coreGlow = Make("CoreGlow", ProcSprites.Glow, new Color(WellColor.r, WellColor.g, WellColor.b, 0.6f), 13, coreRadius * 4.5f);
        Make("Core", ProcSprites.Ball, new Color(0.05f, 0.02f, 0.1f), 14, coreRadius * 2.1f);
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

    /// <summary>Acceleration this hazard applies to something at p (zero for asteroids).</summary>
    public Vector2 PullAt(Vector2 p)
    {
        if (Type != HazardType.GravityWell) return Vector2.zero;
        Vector2 to = (Vector2)transform.position - p;
        float d = to.magnitude;
        if (d >= pullRadius || d < 0.01f) return Vector2.zero;
        float k = 1f - d / pullRadius;
        return to / d * pullStrength * k;
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
        }

        var pc = PendulumController.Instance;
        if (pc != null && pc.IsAlive)
        {
            if (Type == HazardType.GravityWell) pc.ApplyPull(this, dt);
            pc.HazardCircle(pos, radius, velocity, Type == HazardType.Asteroid ? RockColor : WellColor,
                Type == HazardType.GravityWell ? 9f : 0f);
        }

        // Drifted far away and still leaving -> remove.
        Vector2 rel = pos - GameManager.ViewCenter;
        float far = GameManager.HalfExtents.magnitude + Mathf.Max(radius, pullRadius) + 14f;
        if ((rel.sqrMagnitude > far * far && Vector2.Dot(rel, velocity) > 0f) || age > 240f)
            Destroy(gameObject);
    }
}
