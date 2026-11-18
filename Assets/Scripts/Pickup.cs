using System;
using UnityEngine;

public enum PickupKind { Star, Orb, Coin, Heart }

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

    public PickupKind Kind { get; private set; }
    public float radius = 0.35f;
    [Tooltip("Beyond this distance past the screen edge the indicator is at its smallest.")]
    public float indicatorRange = 25f;

    Action<Pickup> onCollected;
    Transform body;
    Transform[] sparks;   // quanta: one per orbit
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
            default: radius = 0.28f; bodyScale = 2.1f; indicatorMax = 0.55f; break;
        }
        bodyScale *= radius;

        glow = Make("Glow", ProcSprites.Glow, new Color(color.r, color.g, color.b, 0.45f), 15, radius * 5.5f);
        body = Make("Body", sprite, color, 16, bodyScale).transform;
        if (kind == PickupKind.Coin)
        {
            // One spark per orbit, racing around it.
            sparks = new Transform[3];
            for (int i = 0; i < sparks.Length; i++)
            {
                var sp = new GameObject("Spark" + i).transform;
                sp.SetParent(body, false);
                var g = sp.gameObject.AddComponent<SpriteRenderer>();
                g.sprite = ProcSprites.Glow;
                g.color = new Color(1f, 0.95f, 0.75f, 0.9f);
                g.sortingOrder = 17;
                sp.localScale = Vector3.one * 0.32f;
                var dot = new GameObject("Dot").transform;
                dot.SetParent(sp, false);
                dot.localScale = Vector3.one * 0.35f;
                var d = dot.gameObject.AddComponent<SpriteRenderer>();
                d.sprite = ProcSprites.Ball;
                d.color = Color.white;
                d.sortingOrder = 18;
                sparks[i] = sp;
            }
        }
        if (kind == PickupKind.Orb || kind == PickupKind.Heart)
            ring = Make("Ring", ProcSprites.Ring, new Color(1f, 1f, 1f, 0.5f), 17, radius * 3f);

        indGlow = Make("IndicatorGlow", ProcSprites.Glow, Color.clear, 45, 1f);
        indIcon = Make("Indicator", sprite, Color.clear, 46, 1f);
        indArrow = Make("IndicatorArrow", ProcSprites.Arrow, Color.clear, 46, 0.3f);
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
        if (popping)
        {
            UpdatePop(Time.deltaTime);
            return;
        }
        if (collected) return;
        float dt = Time.deltaTime;
        age += dt;
        KeepClearOfHazards();

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
                    float tilt = i * 60f * Mathf.Deg2Rad;
                    float th = age * (3.2f + 0.5f * i) + i * 2.1f;
                    Vector2 e = new Vector2(Mathf.Cos(th) * 0.45f, Mathf.Sin(th) * 0.15f);
                    sparks[i].localPosition = new Vector3(e.x * Mathf.Cos(tilt) - e.y * Mathf.Sin(tilt),
                                                          e.x * Mathf.Sin(tilt) + e.y * Mathf.Cos(tilt), 0f);
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
