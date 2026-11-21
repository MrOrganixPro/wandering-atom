using UnityEngine;

/// <summary>
/// An electron knocked out of the atom without being captured (ionized by a gamma ray, shaken
/// loose by an unstable isotope): it flies off, slows down, shrinks and fades into the background.
/// Purely visual.
/// </summary>
public class LooseElectron : MonoBehaviour
{
    const float Life = 1.1f;

    SpriteRenderer body, glow;
    Vector2 velocity;
    float age, size;
    Color color;

    public static void Spawn(Vector2 pos, Vector2 velocity, Color color, float size)
    {
        var go = new GameObject("LooseElectron");
        go.transform.position = pos;
        var e = go.AddComponent<LooseElectron>();
        e.velocity = velocity;
        e.color = color;
        e.size = size;
        e.glow = Sprite(go.transform, ProcSprites.Glow, new Color(color.r, color.g, color.b, 0.5f), -61, size * 2.8f);
        e.body = Sprite(go.transform, ProcSprites.Electron, color, -60, size);
        ParticleBursts.Burst(pos, Color.Lerp(color, Color.white, 0.5f), 8, 3f, 0.18f, 0.35f);
    }

    static SpriteRenderer Sprite(Transform parent, Sprite s, Color c, int order, float scale)
    {
        var go = new GameObject("Sprite");
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.color = c;
        sr.sortingOrder = order;
        return sr;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        float k = Mathf.Clamp01(age / Life);
        transform.position += (Vector3)(velocity * dt);
        velocity *= Mathf.Exp(-2.5f * dt);
        transform.localScale = Vector3.one * (1f - 0.6f * k);
        float a = Mathf.Pow(1f - k, 1.5f);
        body.color = new Color(color.r, color.g, color.b, a);
        glow.color = new Color(color.r, color.g, color.b, 0.5f * a);
        if (k >= 1f) Destroy(gameObject);
    }
}
