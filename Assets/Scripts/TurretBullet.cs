using UnityEngine;

/// <summary>
/// A Spaceship turret shot: a fast glowing bolt that damages the first enemy it touches.
/// Stopped by asteroids and gravity-well cores; fades out at the end of its range.
/// </summary>
public class TurretBullet : MonoBehaviour
{
    const float Radius = 0.08f;

    Vector2 vel;
    float life, maxLife;
    Color color;
    SpriteRenderer core, glow;

    public static void Spawn(Vector2 pos, Vector2 velocity, float lifetime, Color color)
    {
        var go = new GameObject("TurretBullet");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
        var b = go.AddComponent<TurretBullet>();
        b.vel = velocity;
        b.maxLife = b.life = Mathf.Max(0.05f, lifetime);
        b.color = color;
        b.glow = Make(go.transform, "Glow", ProcSprites.Glow, new Color(color.r, color.g, color.b, 0.45f), 12, new Vector3(0.7f, 0.35f, 1f));
        b.core = Make(go.transform, "Core", ProcSprites.Ball, Color.Lerp(color, Color.white, 0.55f), 13, new Vector3(0.36f, 0.1f, 1f));
    }

    static SpriteRenderer Make(Transform parent, string name, Sprite sprite, Color c, int order, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = c;
        sr.sortingOrder = order;
        return sr;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Sub-steps so a fast bolt can't skip over a small enemy.
        Vector2 delta = vel * dt;
        int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.15f));
        for (int i = 0; i < steps; i++)
        {
            transform.position += (Vector3)(delta / steps);
            if (CheckHit()) return;
        }

        life -= dt;
        if (life <= 0f)
        {
            Destroy(gameObject);
            return;
        }
        float a = Mathf.Clamp01(life / Mathf.Min(0.12f, maxLife));   // fade over the last bit of range
        core.color = new Color(core.color.r, core.color.g, core.color.b, a);
        glow.color = new Color(color.r, color.g, color.b, 0.45f * a);
    }

    bool CheckHit()
    {
        Vector2 p = transform.position;
        foreach (var h in Hazard.All)
        {
            if (!h.Crushes(p, Radius)) continue;
            ParticleBursts.Burst(p, color, 5, 3f, 0.15f, 0.25f);
            Destroy(gameObject);
            return true;
        }
        foreach (var e in EnemyMover.All)
        {
            if (e == null || e.IsDead) continue;
            float r = e.radius + Radius;
            if ((e.Position - p).sqrMagnitude >= r * r) continue;
            e.Shot(vel);
            ParticleBursts.Burst(p, Color.Lerp(color, Color.white, 0.5f), 6, 3.5f, 0.15f, 0.25f);
            Destroy(gameObject);
            return true;
        }
        return false;
    }
}
