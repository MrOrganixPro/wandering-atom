using System;
using UnityEngine;

public enum PickupKind { Star, Orb, Coin, Heart, Neutrino }

/// <summary>
/// A collectible:
///   Star  - a photon: the level goal
///   Orb   - an electron: joins your orbitals
///   Coin  - quanta: one upgrade choice at the end of the level (Rutherford atom with
///           sparks racing around its three orbits)
///   Heart - restores one health (rare)
/// Collected by touching it with the core or any of its electrons.
/// While off-screen, an icon sits on the screen edge pointing toward it and grows
/// as the player gets closer, until the pickup itself comes into view.
/// Runs after GameManager so the indicator uses this frame's camera position.
/// </summary>
[DefaultExecutionOrder(100)]
public class Pickup : MonoBehaviour
{
    public static readonly Color Gold = new Color(1f, 0.84f, 0.2f);
    public static readonly Color OrbColor = new Color(0.3f, 0.9f, 1f);
    public static readonly Color CoinColor = new Color(1f, 0.66f, 0.22f);
    public static readonly Color HeartColor = new Color(1f, 0.35f, 0.5f);
    /// <summary>Neutrino: a pale, barely-there white.</summary>
    public static readonly Color NeutrinoColor = new Color(0.9f, 0.95f, 1f);

    public PickupKind Kind { get; private set; }
    public float radius = 0.35f;
    [Tooltip("Beyond this distance past the screen edge the indicator is at its smallest.")]
    public float indicatorRange = 25f;

    Action<Pickup> onCollected;
    Transform body;
    Transform[] sparks;   // quanta: one comet per orbit (the head)
    SpriteRenderer[][] sparkTrails;   // ... and its tail, dots trailing back along the orbit
    SpriteRenderer glow, ring, indIcon, indGlow, indArrow;
    Color color;
    Sprite sprite;
    float bodyScale, indicatorMax;
    float age, sparkleTimer;
    bool collected;
    bool popping;           // level ended: waiting to pop, then swelling, then gone
    float popTimer;
    Vector2 drift;          // pushed away when it can't be taken (orbitals full)
    float fullWarnCooldown;

    public static Color ColorOf(PickupKind k)
    {
        switch (k)
        {
            case PickupKind.Star: return Gold;
            case PickupKind.Coin: return CoinColor;
            case PickupKind.Heart: return HeartColor;
            case PickupKind.Neutrino: return NeutrinoColor;
            default: return OrbColor;
        }
    }

    public static Sprite SpriteOf(PickupKind k)
    {
        switch (k)
        {
            case PickupKind.Star: return ProcSprites.Photon;
            case PickupKind.Coin: return ProcSprites.Quanta;
            case PickupKind.Heart: return ProcSprites.Stability;
            case PickupKind.Neutrino: return ProcSprites.Neutrino;
            default: return ProcSprites.Electron;
        }
    }

    public void Init(PickupKind kind, Action<Pickup> collectedCallback)
    {
        Kind = kind;
        onCollected = collectedCallback;
        age = UnityEngine.Random.value * 10f;
        color = ColorOf(kind);
        sprite = SpriteOf(kind);

        switch (kind)
        {
            case PickupKind.Star: radius = 0.35f; bodyScale = 2.3f; indicatorMax = 0.85f; break;
            case PickupKind.Coin: radius = 0.28f; bodyScale = 2.6f; indicatorMax = 0.55f; break;
            case PickupKind.Heart: radius = 0.28f; bodyScale = 2.3f; indicatorMax = 0.55f; break;
            case PickupKind.Neutrino:
                radius = 0.3f; bodyScale = 3.2f; indicatorMax = 0.55f;
                float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                wander = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * NeutrinoDrift;
                break;
            default: radius = 0.28f; bodyScale = 2.1f; indicatorMax = 0.55f; break;
        }
        bodyScale *= radius;

        glow = Make("Glow", ProcSprites.Glow, new Color(color.r, color.g, color.b, 0.45f), 15, radius * 5.5f);
        body = Make("Body", sprite, color, 16, bodyScale).transform;
        if (kind == PickupKind.Coin)
        {
            // One comet per orbit: a white-hot head with a glowing tail that follows the curve.
            sparks = new Transform[3];
            sparkTrails = new SpriteRenderer[3][];
            for (int i = 0; i < sparks.Length; i++)
            {
                var sp = new GameObject("Spark" + i).transform;
                sp.SetParent(body, false);
                var g = sp.gameObject.AddComponent<SpriteRenderer>();
                g.sprite = ProcSprites.Glow;
                g.color = new Color(1f, 0.92f, 0.7f, 0.95f);
                g.sortingOrder = 18;
                sp.localScale = Vector3.one * 0.3f;
                var dot = new GameObject("Hot").transform;
                dot.SetParent(sp, false);
                dot.localScale = Vector3.one * 0.3f;
                var d = dot.gameObject.AddComponent<SpriteRenderer>();
                d.sprite = ProcSprites.Glow;
                d.color = Color.white;
                d.sortingOrder = 19;
                sparks[i] = sp;

                sparkTrails[i] = new SpriteRenderer[QuantaOrbit.TrailDots];
                for (int j = 0; j < QuantaOrbit.TrailDots; j++)
                {
                    var tr = new GameObject("Tail" + j);
                    tr.transform.SetParent(body, false);
                    var r = tr.AddComponent<SpriteRenderer>();
                    r.sprite = ProcSprites.Glow;
                    r.sortingOrder = 17;
                    sparkTrails[i][j] = r;
                }
            }
        }
        if (kind == PickupKind.Orb || kind == PickupKind.Heart)
            ring = Make("Ring", ProcSprites.Ring, new Color(1f, 1f, 1f, 0.5f), 17, radius * 3f);

        indGlow = Make("IndicatorGlow", ProcSprites.Glow, Color.clear, 45, 1f);
        indIcon = Make("Indicator", sprite, Color.clear, 46, 1f);
        indArrow = Make("IndicatorArrow", ProcSprites.Arrow, Color.clear, 46, 0.3f);
    }

    // Neutrino pickup: drifts slowly around the area, passing through everything.
    const float NeutrinoDrift = 0.6f;
    Vector2 wander;

    void Wander(float dt)
    {
        Vector2 pos = transform.position;
        // Turn gently; steer back toward the play area if it strays too far.
        wander = (Vector2)(Quaternion.Euler(0f, 0f, Mathf.Sin(age * 0.7f) * 25f * dt) * wander);
        Vector2 home = GameManager.ViewCenter - pos;
        float limit = GameManager.HalfExtents.magnitude * 0.6f;
        if (home.magnitude > limit) wander = Vector2.Lerp(wander, home.normalized * NeutrinoDrift, dt * 0.8f);
        wander = wander.normalized * NeutrinoDrift;
        transform.position = pos + wander * dt;
        // Barely there: an uneven flicker.
        float f = 0.65f + 0.35f * Mathf.PerlinNoise(age * 2.4f, 3.7f);
        if (body != null)
        {
            var sr = body.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(color.r, color.g, color.b, f);
        }
    }

    // A quantum just unlocked by filling an energy level: rings pulse out of it for a while.
    SpriteRenderer[] newRings;
    float newTime = -1f;
    const float NewShowTime = 8f;

    /// <summary>Marks this quantum as newly unlocked: it sends out pulsing rings for a few seconds.</summary>
    public void MarkNew()
    {
        newTime = 0f;
        newRings = new SpriteRenderer[2];
        for (int i = 0; i < newRings.Length; i++)
            newRings[i] = Make("NewRing" + i, ProcSprites.Ring, Color.clear, 14, radius * 3f);
        GameUI.FloatText((Vector2)transform.position + Vector2.up * 0.9f, Loc.T("NEW", "YENİ"), color, 22f);
    }

    void UpdateNewRings(float dt)
    {
        if (newRings == null) return;
        bool on = !collected && !popping && newTime < NewShowTime;
        if (on) newTime += dt;
        float fade = Mathf.Clamp01((NewShowTime - newTime) / 1.5f);
        for (int i = 0; i < newRings.Length; i++)
        {
            float k = Mathf.Repeat(newTime * 0.7f + i * 0.5f, 1f);
            newRings[i].transform.localScale = Vector3.one * radius * Mathf.Lerp(2.5f, 9f, k);
            newRings[i].color = on ? new Color(color.r, color.g, color.b, 0.85f * (1f - k) * (1f - k) * fade) : Color.clear;
        }
        if (!on && (collected || popping)) newRings = null;
    }

    SpriteRenderer Make(string name, Sprite s, Color c, int order, float scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.color = c;
        sr.sortingOrder = order;
        return sr;
    }

    /// <summary>
    /// Removes the pickup without collecting it: after 'delay' seconds it swells briefly,
    /// then bursts and disappears. Used to clear leftovers when a level ends.
    /// </summary>
    public void Pop(float delay)
    {
        if (collected) return;
        collected = true;
        popping = true;
        popTimer = Mathf.Max(0f, delay);
        indIcon.color = indGlow.color = indArrow.color = Color.clear;
    }

    void Update()
    {
        UpdateNewRings(Time.deltaTime);
        if (popping)
        {
            UpdatePop(Time.deltaTime);
            return;
        }
        if (collected) return;
        float dt = Time.deltaTime;
        age += dt;
        if (Kind == PickupKind.Neutrino) Wander(dt);   // drifts, and phases straight through hazards
        else KeepClearOfHazards();

        switch (Kind)
        {
            case PickupKind.Star:
                body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(age * 1.5f) * 15f);
                body.localScale = Vector3.one * bodyScale * (1f + 0.08f * Mathf.Sin(age * 4f));
                break;
            case PickupKind.Coin:
                // Rutherford atom: turns slowly; a spark races around each orbit.
                body.localRotation = Quaternion.Euler(0f, 0f, age * 20f);
                body.localScale = Vector3.one * bodyScale * (1f + 0.05f * Mathf.Sin(age * 4f));
                for (int i = 0; i < sparks.Length; i++)
                {
                    float speed = 3.2f + 0.5f * i;
                    float th = age * speed + i * 2.1f;
                    sparks[i].localPosition = QuantaOrbit.Point(i, th, QuantaOrbit.A, QuantaOrbit.B);
                    QuantaOrbit.LayTrail(sparkTrails[i], i, th, speed, QuantaOrbit.A, QuantaOrbit.B, 0.26f, CoinColor);
                }
                break;
            case PickupKind.Heart:
                float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(age * 6f)), 8f);
                body.localScale = Vector3.one * bodyScale * (1f + 0.15f * beat);
                break;
            default:
                body.localScale = Vector3.one * bodyScale * (1f + 0.08f * Mathf.Sin(age * 4f));
                break;
        }
        glow.color = new Color(color.r, color.g, color.b, 0.35f + 0.15f * Mathf.Sin(age * 3f));
        if (ring != null)
        {
            float k = Mathf.Repeat(age * 0.8f, 1f);
            ring.transform.localScale = Vector3.one * radius * Mathf.Lerp(2.4f, 5f, k);
            ring.color = new Color(color.r, color.g, color.b, 0.6f * (1f - k));
        }

        sparkleTimer -= dt;
        if (sparkleTimer <= 0f)
        {
            sparkleTimer = 0.15f;
            Color sc = Color.Lerp(color, Color.white, 0.5f);
            sc.a = 0.8f;
            ParticleBursts.Emit((Vector2)transform.position + UnityEngine.Random.insideUnitCircle * radius * 1.2f,
                Vector2.up * 0.3f, sc, 0.11f, 0.6f, 1f);
        }

        // Drift (from being bounced off) slows down quickly.
        if (drift.sqrMagnitude > 1e-4f)
        {
            transform.position += (Vector3)(drift * dt);
            drift *= Mathf.Exp(-3.5f * dt);
        }
        fullWarnCooldown -= dt;

        var pc = PendulumController.Instance;
        if (pc == null || !pc.Touches(transform.position, radius)) return;

        // Orb with full orbitals: recycled if a Recycler can use it, otherwise it's bounced away
        // with an "Orbitals full" warning.
        if (Kind == PickupKind.Orb && pc.IsFull)
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.CanRecycleOrb) { Collect(); return; }
            Vector2 away = (Vector2)transform.position - pc.PivotPosition;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : UnityEngine.Random.insideUnitCircle.normalized;
            drift = away * 4f;
            if (fullWarnCooldown <= 0f)
            {
                fullWarnCooldown = 1f;
                if (gm != null) gm.WarnOrbitalsFull(transform.position);
                ParticleBursts.Burst(transform.position, new Color(1f, 0.3f, 0.3f), 10, 3f, 0.2f, 0.4f);
            }
            return;
        }
        Collect();
    }

    /// <summary>
    /// Asteroids and gravity wells shove pickups out of their way, so nothing is ever
    /// buried in rock or parked on a lethal core where it can't be collected safely.
    /// </summary>
    void KeepClearOfHazards()
    {
        if (Hazard.All.Count == 0) return;
        Vector2 p = transform.position;
        bool moved = false;
        for (int pass = 0; pass < 2; pass++)   // a second pass settles pickups squeezed between two hazards
        {
            foreach (var h in Hazard.All)
            {
                Vector2 c = h.transform.position;
                float min = h.PickupClearance + radius;
                Vector2 d = p - c;
                float dist = d.magnitude;
                if (dist >= min) continue;
                // Push out along the line from the center (or along the hazard's motion if dead center).
                Vector2 n = dist > 1e-3f ? d / dist
                          : (h.velocity.sqrMagnitude > 1e-6f ? h.velocity.normalized : Vector2.up);
                p = c + n * min;
                moved = true;
            }
        }
        if (moved) transform.position = p;
    }

    const float PopSwell = 0.12f;

    void UpdatePop(float dt)
    {
        popTimer -= dt;
        if (popTimer > 0f) return;

        // Swell for a moment, then burst.
        float k = Mathf.Clamp01(-popTimer / PopSwell);
        body.localScale = Vector3.one * bodyScale * (1f + 0.5f * k);
        glow.color = new Color(color.r, color.g, color.b, 0.45f + 0.4f * k);
        if (k < 1f) return;

        Vector2 p = transform.position;
        ParticleBursts.Burst(p, color, 16, 5f, 0.26f, 0.6f);
        ParticleBursts.Burst(p, Color.white, 6, 3f, 0.2f, 0.4f);
        Sfx.Play("pop", 0.45f);
        Destroy(gameObject);
    }

    void Collect()
    {
        collected = true;
        Vector2 p = transform.position;
        ParticleBursts.Burst(p, color, 22, 6f, 0.3f, 0.8f);
        ParticleBursts.Burst(p, Color.white, 8, 4f, 0.22f, 0.5f);
        onCollected?.Invoke(this);
        Destroy(gameObject);
    }

    void LateUpdate()
    {
        if (collected) return;

        Vector2 vc = GameManager.ViewCenter;
        Vector2 half = GameManager.HalfExtents;
        Vector2 d = (Vector2)transform.position - vc;

        bool onScreen = Mathf.Abs(d.x) < half.x + radius * 0.5f && Mathf.Abs(d.y) < half.y + radius * 0.5f;
        if (onScreen)
        {
            indIcon.color = indGlow.color = indArrow.color = Color.clear;
            return;
        }

        Vector2 inner = half - new Vector2(0.7f, 0.7f);
        float k = Mathf.Min(inner.x / Mathf.Max(Mathf.Abs(d.x), 1e-4f), inner.y / Mathf.Max(Mathf.Abs(d.y), 1e-4f));
        Vector2 pos = vc + d * k;
        float beyond = d.magnitude * (1f - k);
        float close = 1f - Mathf.Clamp01(beyond / indicatorRange);

        float size = Mathf.Lerp(0.25f, indicatorMax, close * close);
        float pulse = 1f + 0.08f * Mathf.Sin(age * 5f);
        Vector2 n = d.normalized;

        indIcon.transform.position = pos;
        indIcon.transform.rotation = Quaternion.identity;
        indIcon.transform.localScale = Vector3.one * size * pulse;
        indIcon.color = new Color(color.r, color.g, color.b, 0.6f + 0.4f * close);

        indGlow.transform.position = pos;
        indGlow.transform.localScale = Vector3.one * size * 2.6f;
        indGlow.color = new Color(color.r, color.g, color.b, 0.15f + 0.3f * close);

        indArrow.transform.position = pos + n * (size * 0.55f + 0.18f);
        indArrow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg);
        indArrow.transform.localScale = Vector3.one * (0.18f + 0.12f * close);
        indArrow.color = new Color(color.r, color.g, color.b, 0.5f + 0.4f * close);
    }
}

/// <summary>
/// The quanta atom's three orbits (tilted 0, 60 and 120 degrees, like the Quanta sprite) and the
/// comet streaks that race along them. Shared by the pickup and the upgrade screen.
/// </summary>
public static class QuantaOrbit
{
    /// <summary>Orbit half-axes in the pickup body's local units (the sprite's 0.9 / 0.3 of its half size).</summary>
    public const float A = 0.45f, B = 0.15f;
    public const int TrailDots = 12;
    /// <summary>Seconds of travel the tail covers (so faster comets have longer tails).</summary>
    public const float TrailTime = 0.2f;
    /// <summary>The tail never wraps more than this much of the orbit (radians).</summary>
    public const float MaxTrailAngle = 5.2f;

    public static Vector2 Point(int orbit, float th, float a, float b)
    {
        float tilt = orbit * 60f * Mathf.Deg2Rad;
        float ex = Mathf.Cos(th) * a, ey = Mathf.Sin(th) * b;
        float c = Mathf.Cos(tilt), s = Mathf.Sin(tilt);
        return new Vector2(ex * c - ey * s, ex * s + ey * c);
    }

    /// <summary>Angle the tail spans for a comet moving at 'speed' radians per second.</summary>
    public static float TrailSpan(float speed) => Mathf.Min(MaxTrailAngle, Mathf.Abs(speed) * TrailTime + 0.25f);

    /// <summary>Tail size (0..1 of the head) and opacity of tail dot j (1 = right behind the head).</summary>
    public static void TrailShape(int j, int count, out float size, out float alpha)
    {
        float k = j / (float)(count + 1);
        size = Mathf.Pow(1f - k, 0.8f);
        alpha = Mathf.Pow(1f - k, 1.6f);
    }

    /// <summary>Places a world-space comet tail (sprites under the same parent as the head).</summary>
    public static void LayTrail(SpriteRenderer[] dots, int orbit, float th, float speed, float a, float b, float headSize, Color color)
    {
        float span = TrailSpan(speed);
        for (int j = 0; j < dots.Length; j++)
        {
            int n = j + 1;
            TrailShape(n, dots.Length, out float size, out float alpha);
            dots[j].transform.localPosition = Point(orbit, th - span * n / dots.Length, a, b);
            dots[j].transform.localScale = Vector3.one * headSize * (0.35f + 0.65f * size);
            Color c = Color.Lerp(Color.Lerp(color, Color.white, 0.5f), color, n / (float)dots.Length);
            c.a = 0.85f * alpha;
            dots[j].color = c;
        }
    }
}
