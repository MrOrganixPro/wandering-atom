using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tiny pooled sprite-particle system (avoids needing particle materials).
/// Created automatically the first time it is used.
/// </summary>
public class ParticleBursts : MonoBehaviour
{
    class P
    {
        public Transform t;
        public SpriteRenderer sr;
        public Vector2 v;
        public float life, maxLife, size, drag;
        public Color c;
    }

    static ParticleBursts inst;
    readonly List<P> active = new List<P>();
    readonly Stack<P> pool = new Stack<P>();

    static ParticleBursts Get()
    {
        if (inst == null) inst = new GameObject("ParticleBursts").AddComponent<ParticleBursts>();
        return inst;
    }

    /// <summary>Radial explosion of particles.</summary>
    public static void Burst(Vector2 pos, Color color, int count, float speed, float size = 0.3f, float life = 0.6f)
    {
        var g = Get();
        for (int i = 0; i < count; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir == Vector2.zero) dir = Vector2.up;
            g.Spawn(pos, dir * speed * Random.Range(0.3f, 1f), color,
                size * Random.Range(0.6f, 1.25f), life * Random.Range(0.6f, 1.2f), 3f);
        }
    }

    /// <summary>Emit a single particle.</summary>
    public static void Emit(Vector2 pos, Vector2 velocity, Color color, float size, float life, float drag = 2f)
    {
        Get().Spawn(pos, velocity, color, size, life, drag);
    }

    public static void ClearAll()
    {
        if (inst == null) return;
        for (int i = inst.active.Count - 1; i >= 0; i--) inst.Recycle(i);
    }

    void Spawn(Vector2 pos, Vector2 vel, Color c, float size, float life, float drag)
    {
        P p = pool.Count > 0 ? pool.Pop() : Create();
        p.t.gameObject.SetActive(true);
        p.t.position = pos;
        p.v = vel;
        p.c = c;
        p.size = size;
        p.life = p.maxLife = Mathf.Max(0.01f, life);
        p.drag = drag;
        p.sr.color = c;
        p.t.localScale = Vector3.one * size;
        active.Add(p);
    }

    P Create()
    {
        var go = new GameObject("p");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ProcSprites.Glow;
        sr.sortingOrder = 30;
        return new P { t = go.transform, sr = sr };
    }

    void Recycle(int i)
    {
        P p = active[i];
        p.t.gameObject.SetActive(false);
        active.RemoveAt(i);
        pool.Push(p);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            P p = active[i];
            p.life -= dt;
            if (p.life <= 0f) { Recycle(i); continue; }
            p.v *= Mathf.Exp(-p.drag * dt);
            p.t.position += (Vector3)(p.v * dt);
            float k = p.life / p.maxLife;
            p.t.localScale = Vector3.one * p.size * (0.35f + 0.65f * k);
            Color c = p.c;
            c.a *= k;
            p.sr.color = c;
        }
    }
}
