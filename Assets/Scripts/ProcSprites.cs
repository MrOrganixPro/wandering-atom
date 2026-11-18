using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates all of the game's sprites procedurally at runtime, so the project
/// needs no imported art. Sprites are mostly grayscale so they can be tinted
/// with SpriteRenderer.color.
/// </summary>
public static class ProcSprites
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    delegate Color PixelFunc(float x, float y); // x, y in [-1, 1]

    static Sprite Build(string key, int w, int h, float ppu, PixelFunc f)
    {
        if (cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = key,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = ((y + 0.5f) / h) * 2f - 1f;
            for (int x = 0; x < w; x++)
            {
                float u = ((x + 0.5f) / w) * 2f - 1f;
                px[y * w + x] = f(u, v);
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, false);

        Sprite s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        s.name = key;
        cache[key] = s;
        return s;
    }

    // Anti-aliased coverage: insideDist > 0 means inside the shape (in uv units).
    static float AA(float insideDist, float soft) => Mathf.Clamp01(insideDist / soft + 0.5f);

    static Color Gray(float v, float a)
    {
        v = Mathf.Clamp01(v);
        return new Color(v, v, v, Mathf.Clamp01(a));
    }

    /// <summary>Shaded sphere with a highlight. 1 world unit across.</summary>
    public static Sprite Ball => Build("Ball", 128, 128, 128f, (x, y) =>
    {
        float r = Mathf.Sqrt(x * x + y * y);
        float a = AA(0.96f - r, 0.03f);
        Vector2 hl = new Vector2(-0.35f, 0.4f);
        float dh = Vector2.Distance(new Vector2(x, y), hl);
        float v = 0.45f + 0.55f * Mathf.Clamp01(1f - dh / 1.2f);
        v *= Mathf.Lerp(1f, 0.65f, Mathf.InverseLerp(0.7f, 0.96f, r));
        v += 0.7f * Mathf.Pow(Mathf.Clamp01(1f - dh / 0.28f), 2f);
        return Gray(v, a);
    });

    /// <summary>Soft radial glow. 1 world unit across.</summary>
    public static Sprite Glow => Build("Glow", 128, 128, 128f, (x, y) =>
    {
        float r = Mathf.Sqrt(x * x + y * y);
        return new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f));
    });

    /// <summary>Thin circle outline at radius 0.97 (of half-size). 1 world unit across.</summary>
    public static Sprite Ring => Build("Ring", 512, 512, 512f, (x, y) =>
    {
        float r = Mathf.Sqrt(x * x + y * y);
        float d = Mathf.Abs(r - 0.97f);
        return new Color(1, 1, 1, AA(0.008f - d, 0.006f));
    });

    /// <summary>Turning arrow (Torque icon): a thick circular arc, open at the upper right,
    /// running counter-clockwise into an arrowhead.</summary>
    public static Sprite TurnArrow => Build("TurnArrow", 128, 128, 128f, (x, y) =>
    {
        const float R = 0.58f, half = 0.12f;          // arc radius and half-thickness
        const float gapFrom = 30f, gapTo = 105f;      // open sector (degrees); the head sits at gapFrom
        float r = Mathf.Sqrt(x * x + y * y);
        float ang = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
        if (ang < 0f) ang += 360f;

        // Arc: inside the ring band and outside the gap (distance to the gap measured along the arc).
        float band = half - Mathf.Abs(r - R);
        float toGap = ang <= gapFrom ? gapFrom - ang
                    : ang >= gapTo ? Mathf.Min(ang - gapTo, 360f - ang + gapFrom)
                    : -Mathf.Min(ang - gapFrom, gapTo - ang);
        float arc = Mathf.Min(band, toGap * Mathf.Deg2Rad * r);

        // Arrowhead at the gap's start, pointing counter-clockwise along the circle.
        float a0 = gapFrom * Mathf.Deg2Rad;
        Vector2 n = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
        Vector2 t = new Vector2(-n.y, n.x);
        Vector2 b = n * R;
        Vector2 p0 = b + t * 0.36f, p1 = b + n * 0.27f, p2 = b - n * 0.27f;
        Vector2 q = new Vector2(x, y);
        float head = Mathf.Min(EdgeIn(q, p0, p2), Mathf.Min(EdgeIn(q, p2, p1), EdgeIn(q, p1, p0)));   // p0→p2→p1 is CCW

        float inside = Mathf.Max(arc, head);
        return new Color(1f, 1f, 1f, AA(inside, 0.02f));
    });

    /// <summary>Signed distance from edge a→b of a counter-clockwise triangle (positive = inside).</summary>
    static float EdgeIn(Vector2 q, Vector2 a, Vector2 b)
    {
        Vector2 e = b - a;
        return (e.x * (q.y - a.y) - e.y * (q.x - a.x)) / e.magnitude;
    }

    /// <summary>Spiky mine-like enemy body.</summary>
    public static Sprite Spiky => Build("Spiky", 128, 128, 128f, (x, y) =>
    {
        const float spikes = 8f;
        float r = Mathf.Sqrt(x * x + y * y);
        float th = Mathf.Atan2(y, x);
        float edge = 0.6f + 0.36f * Mathf.Pow(Mathf.Abs(Mathf.Cos(th * spikes * 0.5f)), 5f);
        float inside = edge - r;
        float a = AA(inside, 0.03f);
        float v = 0.55f + 0.45f * Mathf.Clamp01(1f - r / edge);
        if (inside < 0.07f) v *= 0.6f;                       // dark outline
        if (r < 0.42f && r > 0.36f) v *= 0.75f;             // inner ring groove
        float dh = Vector2.Distance(new Vector2(x, y), new Vector2(-0.2f, 0.22f));
        v += 0.35f * Mathf.Pow(Mathf.Clamp01(1f - dh / 0.3f), 2f);
        return Gray(v, a);
    });

    /// <summary>Elongated diamond (points along +x) for fast enemies.</summary>
    public static Sprite Dart => Build("Dart", 128, 128, 128f, (x, y) =>
    {
        float d = Mathf.Abs(x) / 0.95f + Mathf.Abs(y) / 0.42f;
        float inside = (1f - d) * 0.42f;
        float a = AA(inside, 0.02f);
        float v = 0.5f + 0.5f * Mathf.Clamp01(1f - d);
        if (inside < 0.05f) v *= 0.6f;
        if (Mathf.Abs(y) < 0.04f && x > -0.6f) v += 0.3f;   // ridge
        return Gray(v, a);
    });

    /// <summary>Small fast enemy: notched shard pointing toward +x.</summary>
    public static Sprite Shard => Build("Shard", 128, 128, 128f, (x, y) =>
    {
        float ay = Mathf.Abs(y);
        float side = (0.55f * (0.95f - x) / 1.65f - ay) / 1.054f;   // top/bottom edges from the tip
        float back = -0.25f - 0.818f * ay;                          // notched back edge
        float rear = (x - back) / 1.29f;
        float inside = Mathf.Min(side, rear);
        float a = AA(inside, 0.02f);
        float v = 0.6f + 0.4f * Mathf.Clamp01(inside / 0.2f);
        if (inside < 0.04f) v *= 0.6f;                              // dark rim
        if (ay < 0.035f && x > -0.2f) v += 0.35f;                   // bright spine
        return Gray(v, a);
    });

    /// <summary>
    /// Broken Thruster pointing toward +x: round dented pod with a cracked hull and a wide,
    /// bent engine bell at the back.
    /// </summary>
    public static Sprite BrokenThruster => Build("BrokenThruster", 128, 128, 128f, (x, y) =>
    {
        // Pod: a circle with a dent on the upper-front.
        Vector2 c = new Vector2(0.12f, 0f);
        float r = Vector2.Distance(new Vector2(x, y), c);
        float th = Mathf.Atan2(y, x - c.x);
        float dent = 0.08f * Mathf.Exp(-Mathf.Pow((th - 0.9f) / 0.25f, 2f));
        float podEdge = 0.55f - dent;
        float pod = podEdge - r;
        // Engine bell: widens toward the back, bent slightly downward.
        float bx = -0.35f - x;                                   // 0 at the throat, grows backward
        float by = y + 0.08f * Mathf.Clamp01(bx / 0.5f);         // bent
        float bellHalf = 0.16f + 0.3f * Mathf.Clamp01(bx / 0.5f);
        float bell = Mathf.Min(Mathf.Min(bellHalf - Mathf.Abs(by), bx + 0.2f), 0.5f - bx);
        float inside = Mathf.Max(pod, bell);
        float a = AA(inside, 0.02f);

        float v;
        if (pod >= bell)
        {
            v = 0.55f + 0.35f * Mathf.Clamp01(1f - r / podEdge);
            if (pod < 0.05f) v *= 0.55f;                                   // outline
            float dh = Vector2.Distance(new Vector2(x, y), new Vector2(0f, 0.22f));
            v += 0.25f * Mathf.Pow(Mathf.Clamp01(1f - dh / 0.3f), 2f);     // highlight
            // Crack: a jagged line across the hull.
            float crackY = -0.05f + 0.12f * Mathf.Sin(x * 14f) + 0.05f * Mathf.Sin(x * 31f);
            if (Mathf.Abs(y - crackY) < 0.025f && x > -0.25f && x < 0.5f) v *= 0.35f;
            // Rivets.
            for (int i = 0; i < 4; i++)
            {
                float ra = i * 1.57f + 0.4f;
                Vector2 rp = c + new Vector2(Mathf.Cos(ra), Mathf.Sin(ra)) * 0.4f;
                if (Vector2.Distance(new Vector2(x, y), rp) < 0.035f) v *= 0.6f;
            }
        }
        else
        {
            v = 0.45f + 0.2f * Mathf.Clamp01(1f - Mathf.Abs(by) / bellHalf);
            if (bell < 0.04f) v *= 0.55f;
            if (Mathf.Abs(bx - 0.2f) < 0.025f) v *= 0.6f;                   // bell seam
        }
        return Gray(v, a);
    });

    /// <summary>Large slow enemy: heavy armored ball with blunt studs and an inner plate ring.</summary>
    public static Sprite Brute => Build("Brute", 128, 128, 128f, (x, y) =>
    {
        float r = Mathf.Sqrt(x * x + y * y);
        float th = Mathf.Atan2(y, x);
        float stud = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Cos(th * 10f) - 0.3f) / 0.7f));
        float edge = 0.8f + 0.15f * stud;
        float inside = edge - r;
        float a = AA(inside, 0.025f);
        float v = 0.5f + 0.35f * Mathf.Clamp01(1f - r / edge);
        if (inside < 0.05f) v *= 0.55f;                              // outline
        if (Mathf.Abs(r - 0.55f) < 0.035f) v *= 0.6f;                // plate seam
        float plate = Mathf.Abs(Mathf.Sin(th * 3f));
        if (r < 0.52f && r > 0.3f && plate < 0.08f) v *= 0.65f;      // plate dividers
        float dh = Vector2.Distance(new Vector2(x, y), new Vector2(-0.3f, 0.35f));
        v += 0.3f * Mathf.Pow(Mathf.Clamp01(1f - dh / 0.35f), 2f);
        return Gray(v, a);
    });

    /// <summary>Homing missile pointing toward +x: capsule body, pointed nose, rear fins.</summary>
    public static Sprite Missile => Build("Missile", 128, 128, 128f, (x, y) =>
    {
        const float o = 0.5f / 128f;
        float cover = 0f;
        for (int i = 0; i < 4; i++)
        {
            float sx = x + ((i & 1) == 0 ? -o : o);
            float sy = Mathf.Abs(y + ((i & 2) == 0 ? -o : o));
            bool body = sx > -0.6f && sx < 0.5f && sy < 0.17f;
            bool nose = sx >= 0.5f && sx < 0.92f && sy < 0.17f * (0.92f - sx) / 0.42f;
            bool fin = sx > -0.78f && sx < -0.3f && sy < 0.17f + (-0.3f - sx) * 0.75f;
            if (body || nose || fin) cover += 0.25f;
        }
        float ay = Mathf.Abs(y);
        float v = 0.65f + 0.35f * Mathf.Clamp01(1f - ay / 0.17f);
        if (y > 0.04f && y < 0.1f && x > -0.5f && x < 0.6f) v += 0.25f;   // highlight stripe
        if (Mathf.Abs(x - 0.3f) < 0.03f && ay < 0.17f) v *= 0.6f;         // warhead seam
        if (x < -0.3f && ay > 0.17f) v *= 0.75f;                          // darker fins
        return Gray(v, cover);
    });

    /// <summary>Lumpy cratered asteroid (3 variants). Surface is at roughly 0.42 world units from the center.</summary>
    public static Sprite Asteroid(int variant)
    {
        variant = Mathf.Abs(variant) % 3;
        float p1 = 1.3f + variant * 2.1f, p2 = 0.4f + variant * 1.7f, p3 = 2.2f + variant * 0.9f;
        // Craters: x, y, radius
        Vector3[] craters =
        {
            new Vector3(-0.3f + 0.2f * variant, 0.25f, 0.2f),
            new Vector3(0.35f, -0.2f + 0.15f * variant, 0.15f),
            new Vector3(-0.1f, -0.45f + 0.1f * variant, 0.12f),
            new Vector3(0.15f - 0.25f * variant, 0.5f - 0.2f * variant, 0.09f),
            new Vector3(-0.5f + 0.1f * variant, -0.15f, 0.1f),
        };
        return Build("Asteroid" + variant, 256, 256, 256f, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float th = Mathf.Atan2(y, x);
            float edge = 0.84f + 0.06f * Mathf.Sin(3f * th + p1) + 0.04f * Mathf.Sin(5f * th + p2) + 0.025f * Mathf.Sin(9f * th + p3);
            float inside = edge - r;
            float a = AA(inside, 0.015f);
            // Lit from the upper-left, darker toward the lower-right.
            float v = 0.55f + 0.3f * (-x * 0.6f + y * 0.6f) + 0.1f * (1f - r / edge);
            v += 0.04f * Mathf.Sin(x * 23f + p1) * Mathf.Sin(y * 19f + p2);   // surface mottling
            foreach (Vector3 c in craters)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c.x, c.y)) / c.z;
                if (d < 1f) v -= 0.18f * (1f - d * d) - 0.12f * Mathf.Clamp01((y - c.y) / c.z * -1f) * 0.5f;
                else if (d < 1.2f) v += 0.1f;                                  // raised rim
            }
            if (inside < 0.03f) v *= 0.55f;                                    // outline
            return Gray(v, a);
        });
    }

    /// <summary>Chevron arrow pointing toward +x.</summary>
    public static Sprite Arrow => Build("Arrow", 128, 128, 128f, (x, y) =>
    {
        float d = x + Mathf.Abs(y) * 1.1f;
        float inside = Mathf.Min(0.75f - d, d - 0.05f, x + 0.85f) * 0.6f;
        return new Color(1, 1, 1, AA(inside, 0.02f));
    });

    /// <summary>Single chain link (ellipse ring, tall).</summary>
    public static Sprite Link => Build("Link", 64, 64, 64f, (x, y) =>
    {
        float e = Mathf.Sqrt((x / 0.6f) * (x / 0.6f) + (y / 0.95f) * (y / 0.95f));
        float inside = 0.25f - Mathf.Abs(e - 0.72f);
        float a = AA(inside, 0.08f);
        float v = 0.6f + 0.4f * Mathf.Clamp01(-x * 0.8f + y * 0.4f + 0.3f);
        return Gray(v, a);
    });

    /// <summary>Five-pointed star (points up), shaded. 1 world unit across.</summary>
    public static Sprite Star => Build("Star", 128, 128, 128f, (x, y) =>
    {
        float sd = SdStar5(new Vector2(x, y), 0.92f, 0.45f);
        float a = AA(-sd, 0.025f);
        float v = 0.75f + 0.6f * Mathf.Clamp01(-sd / 0.25f);
        if (-sd < 0.06f) v *= 0.7f;                                   // darker rim
        float dh = Vector2.Distance(new Vector2(x, y), new Vector2(-0.15f, 0.25f));
        v += 0.3f * Mathf.Clamp01(1f - dh / 0.35f);
        return Gray(v, a);
    });

    /// <summary>Heart shape, shaded. 1 world unit across.</summary>
    public static Sprite Heart => Build("Heart", 128, 128, 128f, (x, y) =>
    {
        // 4x supersampled implicit heart: (x^2 + y^2 - 1)^3 - x^2 y^3 <= 0
        const float o = 0.5f / 128f;
        float cover = 0f;
        for (int i = 0; i < 4; i++)
        {
            float sx = x + ((i & 1) == 0 ? -o : o);
            float sy = y + ((i & 2) == 0 ? -o : o);
            float px = sx * 1.2f, py = sy * 1.2f + 0.15f;
            float q = px * px + py * py - 1f;
            if (q * q * q - px * px * py * py * py <= 0f) cover += 0.25f;
        }
        float dh = Vector2.Distance(new Vector2(x, y), new Vector2(-0.35f, 0.3f));
        float v = 0.75f + 0.25f * (1f - Mathf.Clamp01((y + 0.8f) / 1.6f) * 0.5f);
        v += 0.5f * Mathf.Pow(Mathf.Clamp01(1f - dh / 0.3f), 2f);
        return Gray(v, cover);
    });

    /// <summary>Coin: disc with a raised rim and an engraved inner ring. 1 world unit across.</summary>
    public static Sprite Coin => Build("Coin", 128, 128, 128f, (x, y) =>
    {
        float r = Mathf.Sqrt(x * x + y * y);
        float a = AA(0.95f - r, 0.03f);
        float v = r > 0.76f ? 0.95f : 0.8f;                       // rim brighter than face
        if (Mathf.Abs(r - 0.76f) < 0.04f) v = 0.55f;               // rim edge
        if (Mathf.Abs(r - 0.45f) < 0.05f) v = 0.6f;                // engraved ring
        if (Mathf.Abs(x) < 0.07f && Mathf.Abs(y) < 0.3f) v = 0.6f; // engraved bar
        float dh = Vector2.Distance(new Vector2(x, y), new Vector2(-0.4f, 0.45f));
        v += 0.35f * Mathf.Pow(Mathf.Clamp01(1f - dh / 0.35f), 2f);
        return Gray(v, a);
    });

    // Signed distance to a 5-pointed star (negative inside). After Inigo Quilez.
    static float SdStar5(Vector2 p, float r, float rf)
    {
        Vector2 k1 = new Vector2(0.809016994375f, -0.587785252292f);
        Vector2 k2 = new Vector2(-k1.x, k1.y);
        p.x = Mathf.Abs(p.x);
        p -= 2f * Mathf.Max(Vector2.Dot(k1, p), 0f) * k1;
        p -= 2f * Mathf.Max(Vector2.Dot(k2, p), 0f) * k2;
        p.x = Mathf.Abs(p.x);
        p.y -= r;
        Vector2 ba = rf * new Vector2(-k1.y, k1.x) - new Vector2(0f, 1f);
        float h = Mathf.Clamp(Vector2.Dot(p, ba) / Vector2.Dot(ba, ba), 0f, r);
        return (p - ba * h).magnitude * Mathf.Sign(p.y * ba.x - p.x * ba.y);
    }

    /// <summary>Plain white square, 1 world unit.</summary>
    public static Sprite Pixel => Build("Pixel", 4, 4, 4f, (x, y) => Color.white);

    /// <summary>Gradient strip: opaque at the left edge (x=-1), fading to the right.</summary>
    public static Sprite EdgeGlow => Build("EdgeGlow", 64, 64, 64f, (x, y) =>
    {
        float a = Mathf.Pow((1f - x) * 0.5f, 2.5f) * (1f - Mathf.Pow(Mathf.Abs(y), 6f));
        return new Color(1, 1, 1, a);
    });

    /// <summary>Grid tile with lines on its border, 1 world unit.</summary>
    public static Sprite GridTile => Build("GridTile", 64, 64, 64f, (x, y) =>
    {
        const float px = 2f / 64f;
        float dx = 1f - Mathf.Abs(x);
        float dy = 1f - Mathf.Abs(y);
        float a = (dx < px * 1.2f || dy < px * 1.2f) ? 1f : 0f;
        return new Color(1, 1, 1, a);
    });

    /// <summary>Opaque radial-gradient background with vignette, 1 world unit.</summary>
    public static Sprite Background(Color center, Color edge)
    {
        string key = "Background_" + ColorUtility.ToHtmlStringRGB(center) + ColorUtility.ToHtmlStringRGB(edge);
        return Build(key, 256, 256, 256f, (x, y) =>
        {
            float r = Mathf.Clamp01(Mathf.Sqrt(x * x + y * y) / 1.35f);
            Color c = Color.Lerp(center, edge, Mathf.SmoothStep(0f, 1f, r));
            c.a = 1f;
            return c;
        });
    }
}
