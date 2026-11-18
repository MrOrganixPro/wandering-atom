using UnityEngine;

/// <summary>
/// A loose piece flung away by an explosion (electrons blasted off a dying core): flies
/// outward, slows, spins, leaves a faint trail and fades out.
/// </summary>
public class Debris : MonoBehaviour
{
    Vector2 velocity;
    float life, age, spin, trailTimer;
    Color color;
    SpriteRenderer body, glow;

    public static void Spawn(Vector2 pos, Vector2 vel, Sprite sprite, Color color, float size, float life = 1.6f)
    {
        var go = new GameObject("Debris");
        go.transform.position = pos;
        var d = go.AddComponent<Debris>();
        d.velocity = vel;
        d.life = life;
        d.color = color;
        d.spin = Random.Range(-360f, 360f);
        d.glow = Make(go.transform, ProcSprites.Glow, new Color(color.r, color.g, color.b, 0.5f), 9, size * 2.6f);
        d.body = Make(go.transform, sprite, color, 10, size);
    }

    static SpriteRenderer Make(Transform parent, Sprite sprite, Color c, int order, float scale)
    {
        var go = new GameObject("Part");
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = c;
        sr.sortingOrder = order;
        return sr;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age >= life) { Destroy(gameObject); return; }
        transform.position += (Vector3)(velocity * dt);
        velocity *= Mathf.Exp(-1.6f * dt);
        body.transform.Rotate(0f, 0f, spin * dt);

        float k = age / life;
        float fade = 1f - k * k;
        body.color = new Color(color.r, color.g, color.b, fade);
        glow.color = new Color(color.r, color.g, color.b, 0.5f * fade);

        trailTimer -= dt;
        if (trailTimer <= 0f && velocity.sqrMagnitude > 1f)
        {
            trailTimer = 0.03f;
            Color c = new Color(color.r, color.g, color.b, 0.6f * fade);
            ParticleBursts.Emit(transform.position, velocity * 0.1f, c, 0.08f, 0.3f, 4f);
        }
    }
}
