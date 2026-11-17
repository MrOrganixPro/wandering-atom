using UnityEngine;

/// <summary>Expanding, fading shockwave ring (used by the Repel blast).</summary>
public class ShockRing : MonoBehaviour
{
    const float Life = 0.45f;

    SpriteRenderer ring, glow;
    float radius, t;
    Color color;

    public static void Spawn(Vector2 pos, float radius, Color color)
    {
        var go = new GameObject("ShockRing");
        go.transform.position = pos;
        var s = go.AddComponent<ShockRing>();
        s.radius = radius;
        s.color = color;
        s.glow = Make(go.transform, "Glow", ProcSprites.Glow, 26);
        s.ring = Make(go.transform, "Ring", ProcSprites.Ring, 27);
        s.Apply();
    }

    static SpriteRenderer Make(Transform parent, string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        sr.color = Color.clear;
        return sr;
    }

    void Update()
    {
        t += Time.deltaTime;
        if (t >= Life)
        {
            Destroy(gameObject);
            return;
        }
        Apply();
    }

    void Apply()
    {
        float k = t / Life;
        float ease = 1f - (1f - k) * (1f - k) * (1f - k);   // fast out, slow at the edge
        float r = Mathf.Max(0.05f, radius * ease);
        float fade = 1f - k;
        ring.transform.localScale = Vector3.one * r / 0.485f;   // Ring sprite radius is 0.485 units at scale 1
        ring.color = new Color(color.r, color.g, color.b, 0.9f * fade);
        glow.transform.localScale = Vector3.one * r * 2.2f;
        glow.color = new Color(color.r, color.g, color.b, 0.18f * fade);
    }
}
