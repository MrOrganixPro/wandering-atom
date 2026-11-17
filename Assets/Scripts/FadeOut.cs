using UnityEngine;

/// <summary>Fades a SpriteRenderer to transparent over its lifetime, then destroys the object.</summary>
public class FadeOut : MonoBehaviour
{
    public float life = 0.25f;
    float t;
    SpriteRenderer sr;
    Color c0;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) c0 = sr.color;
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = 1f - t / Mathf.Max(0.01f, life);
        if (k <= 0f || sr == null)
        {
            Destroy(gameObject);
            return;
        }
        sr.color = new Color(c0.r, c0.g, c0.b, c0.a * k);
    }
}
