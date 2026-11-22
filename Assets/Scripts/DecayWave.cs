using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Radiant with the Lab's Decay Waves: an emitter shot that is a widening arc of decay instead
/// of a bolt. It travels outward from where it was fired, its arc growing as it goes, passes
/// through everything and damages every enemy it touches once, until the end of its range.
/// </summary>
public class DecayWave : MonoBehaviour
{
    const int Segments = 18;
    const float HalfAngle = 25f;      // the arc spans 50 degrees
    const float Thickness = 0.22f;    // how deep the band that hits is

    Vector2 origin, dir;
    float speed, range, dist;
    Color color;
    LineRenderer line, glow;
    readonly HashSet<EnemyMover> hit = new HashSet<EnemyMover>();
    static Material material;

    public static void Spawn(Vector2 origin, Vector2 dir, float speed, float range, Color color)
    {
        var go = new GameObject("DecayWave");
        var w = go.AddComponent<DecayWave>();
        w.origin = origin;
        w.dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
        w.speed = Mathf.Max(1f, speed);
        w.range = Mathf.Max(0.5f, range);
        w.dist = 0.25f;
        w.color = color;
        w.glow = w.MakeLine("Glow", 0.32f, new Color(color.r, color.g, color.b, 0.3f), 11);
        w.line = w.MakeLine("Arc", 0.09f, Color.Lerp(color, Color.white, 0.5f), 12);
        w.Draw(1f);
    }

    LineRenderer MakeLine(string name, float width, Color c, int order)
    {
        if (material == null) material = new Material(Shader.Find("Sprites/Default"));
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = material;
        lr.positionCount = Segments + 1;
        lr.useWorldSpace = true;
        lr.widthMultiplier = width;
        lr.numCapVertices = 4;
        lr.startColor = lr.endColor = c;
        lr.sortingOrder = order;
        return lr;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Sub-steps so a fast wave can't skip over a small enemy.
        float step = speed * dt;
        int steps = Mathf.Max(1, Mathf.CeilToInt(step / 0.15f));
        for (int i = 0; i < steps; i++)
        {
            dist += step / steps;
            CheckHits();
        }

        if (dist >= range) { Destroy(gameObject); return; }
        float k = dist / range;
        Draw(1f - k * k);   // fades toward the end of its range
    }

    void CheckHits()
    {
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        foreach (var e in EnemyMover.All)
        {
            if (e == null || e.IsDead || hit.Contains(e)) continue;
            Vector2 rel = e.Position - origin;
            float d = rel.magnitude;
            if (Mathf.Abs(d - dist) > Thickness + e.radius) continue;
            float a = Mathf.Atan2(rel.y, rel.x) * Mathf.Rad2Deg;
            float slack = d > 0.01f ? e.radius / d * Mathf.Rad2Deg : 180f;   // the enemy's own width counts
            if (Mathf.Abs(Mathf.DeltaAngle(baseAngle, a)) > HalfAngle + slack) continue;
            hit.Add(e);
            e.Shot(rel);
            ParticleBursts.Burst(e.Position, Color.Lerp(color, Color.white, 0.5f), 8, 4f, 0.15f, 0.3f);
        }
    }

    void Draw(float alpha)
    {
        float baseAngle = Mathf.Atan2(dir.y, dir.x);
        float half = HalfAngle * Mathf.Deg2Rad;
        for (int i = 0; i <= Segments; i++)
        {
            float a = baseAngle - half + 2f * half * i / Segments;
            Vector3 p = origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dist;
            line.SetPosition(i, p);
            glow.SetPosition(i, p);
        }
        Color lc = Color.Lerp(color, Color.white, 0.5f);
        line.startColor = line.endColor = new Color(lc.r, lc.g, lc.b, alpha);
        glow.startColor = glow.endColor = new Color(color.r, color.g, color.b, 0.3f * alpha);
    }
}
