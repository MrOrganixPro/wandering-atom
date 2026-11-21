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

    /// <summary>
    /// Neutrino, the ghost particle: a pinpoint white core in a wide, very pale halo, ringed by
    /// a few faint specks (it barely interacts with anything). 1 world unit across.
    /// </summary>
    public static Sprite Neutrino => Build("Neutrino", 128, 128, 128f, (x, y) =>
    {
        float r = Mathf.Sqrt(x * x + y * y);
        float core = Mathf.Exp(-(r * r) / (0.11f * 0.11f));
        float halo = 0.42f * Mathf.Pow(Mathf.Clamp01(1f - r), 2.6f);
        float a = Mathf.Atan2(y, x);
        // Two faint rings of specks, offset from each other.
        float specks = 0f;
        for (int ring = 0; ring < 2; ring++)
        {
            float rr = ring == 0 ? 0.52f : 0.78f;
            int n = ring == 0 ? 6 : 9;
            float seg = Mathf.PI * 2f / n;
            float ang = Mathf.Repeat(a + ring * 0.35f, seg) - seg * 0.5f;
            float d = new Vector2(ang * rr, r - rr).magnitude;
            specks = Mathf.Max(specks, (ring == 0 ? 0.55f : 0.32f) * Mathf.Clamp01(1f - d / 0.045f));
        }
        float alpha = Mathf.Clamp01(core + halo + specks);
        float v = Mathf.Clamp01(0.82f + 0.18f * core);
        return new Color(v, v, v, alpha);
    });

    /// <summary>Research Data: a little chip, a rounded square with a core and pins on every side.</summary>
    public static Sprite DataChip => Build("DataChip", 128, 128, 128f, (x, y) =>
    {
        float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
        // Body: rounded square.
        float qx = Mathf.Max(ax - 0.42f, 0f), qy = Mathf.Max(ay - 0.42f, 0f);
        float body = 0.14f - Mathf.Sqrt(qx * qx + qy * qy);
        float a = AA(body, 0.03f);
        // Pins: three short bars on each side.
        float pin = 0f;
        for (int i = -1; i <= 1; i++)
        {
            float c = i * 0.3f;
            pin = Mathf.Max(pin, AA(Mathf.Min(0.07f - Mathf.Abs(x - c), 0.13f - Mathf.Abs(ay - 0.78f)), 0.03f));
            pin = Mathf.Max(pin, AA(Mathf.Min(0.07f - Mathf.Abs(y - c), 0.13f - Mathf.Abs(ax - 0.78f)), 0.03f));
        }
        // Inner core: a darker square with a bright dot.
        float core = AA(Mathf.Min(0.26f - ax, 0.26f - ay), 0.03f);
        float dot = AA(0.1f - Mathf.Sqrt(x * x + y * y), 0.03f);
        float v = Mathf.Lerp(1f, 0.45f, core);
        v = Mathf.Lerp(v, 1f, dot);
        return Gray(Mathf.Max(v * a, pin), Mathf.Max(a, pin));
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

    /// <summary>
    /// Shaded spheres packed together (a nucleus). Each entry is (x, y, brightness); drawn in
    /// order, so later ones sit on top. Brightness 1 = proton, ~0.6 = neutron.
    /// </summary>
    static Color Nucleons(float x, float y, Vector3[] n, float rad)
    {
        Color result = Color.clear;
        for (int i = 0; i < n.Length; i++)
        {
            float dx = x - n[i].x, dy = y - n[i].y;
            float r = Mathf.Sqrt(dx * dx + dy * dy) / rad;
            float a = AA((1f - r) * rad, 0.02f);
            if (a <= 0f) continue;
            float dh = Vector2.Distance(new Vector2(dx, dy) / rad, new Vector2(-0.35f, 0.4f));
            float v = (0.45f + 0.55f * Mathf.Clamp01(1f - dh / 1.2f)) * Mathf.Lerp(1f, 0.6f, Mathf.InverseLerp(0.6f, 1f, r));
            v += 0.45f * Mathf.Pow(Mathf.Clamp01(1f - dh / 0.3f), 2f);
            v *= n[i].z;
            if (r > 0.86f) v *= 0.55f;                                        // outline
            v = Mathf.Clamp01(v);
            Color c = new Color(v, v, v, 1f);
            if (result.a <= 0f) result = new Color(v, v, v, a);
            else result = new Color(Mathf.Lerp(result.r, c.r, a), Mathf.Lerp(result.g, c.g, a), Mathf.Lerp(result.b, c.b, a),
                                    1f - (1f - result.a) * (1f - a));
        }
        return result;
    }

    /// <summary>Alpha particle: two protons (bright) and two neutrons (dark) in a tight cluster.</summary>
    public static Sprite Alpha => Build("Alpha", 128, 128, 128f, (x, y) => Nucleons(x, y, new[]
    {
        new Vector3(-0.27f, -0.27f, 0.6f), new Vector3(0.29f, -0.25f, 1f),
        new Vector3(-0.25f, 0.29f, 1f), new Vector3(0.27f, 0.27f, 0.6f),
    }, 0.42f));

    /// <summary>Unstable isotope: a lumpy, lopsided nucleus with one nucleon working loose.</summary>
    public static Sprite Isotope => Build("Isotope", 128, 128, 128f, (x, y) => Nucleons(x, y, new[]
    {
        new Vector3(0.62f, 0.18f, 1f),                                         // the loose one
        new Vector3(-0.36f, 0.2f, 0.6f), new Vector3(-0.3f, -0.3f, 1f), new Vector3(0.12f, -0.4f, 0.6f),
        new Vector3(0.38f, -0.08f, 1f), new Vector3(0.14f, 0.38f, 0.6f), new Vector3(-0.05f, 0.02f, 1f),
    }, 0.3f));

    /// <summary>Positron: a glowing sphere marked with a bright "+".</summary>
    public static Sprite Positron => Build("Positron", 128, 128, 128f, (x, y) =>
    {
        float r = Mathf.Sqrt(x * x + y * y);
        float a = AA(0.96f - r, 0.03f);
        float dh = Vector2.Distance(new Vector2(x, y), new Vector2(-0.35f, 0.4f));
        float v = (0.35f + 0.4f * Mathf.Clamp01(1f - dh / 1.2f)) * Mathf.Lerp(1f, 0.65f, Mathf.InverseLerp(0.7f, 0.96f, r));
        float plus = Mathf.Max(Mathf.Min(0.48f - Mathf.Abs(x), 0.12f - Mathf.Abs(y)),
                               Mathf.Min(0.48f - Mathf.Abs(y), 0.12f - Mathf.Abs(x)));
        v = Mathf.Lerp(v, 1f, AA(plus, 0.03f));
        return Gray(v, a);
    });

    /// <summary>Gamma ray pointing toward +x: a tight wave that brightens into a glowing head.</summary>
    public static Sprite Gamma => Build("Gamma", 128, 128, 128f, (x, y) =>
    {
        const float k = Mathf.PI * 3.2f;
        float env = Mathf.Clamp01((x + 0.95f) / 0.9f);
        float wave = 0.32f * Mathf.Sin(x * k) * env;
        float slope = 0.32f * k * Mathf.Cos(x * k) * env;
        float d = Mathf.Abs(y - wave) / Mathf.Sqrt(1f + slope * slope);
        float body = AA(0.075f - d, 0.025f) * Mathf.Clamp01((x + 0.95f) / 0.7f) * (x < 0.92f ? 1f : 0f);
        Vector2 head = new Vector2(0.72f, 0.32f * Mathf.Sin(0.72f * k));
        float hr = Vector2.Distance(new Vector2(x, y), head);
        float glow = AA(0.2f - hr, 0.05f);
        float v = 0.7f + 0.3f * env + 0.4f * glow;
        return Gray(v, Mathf.Max(body, glow));
    });

    /// <summary>Electron: a shaded sphere marked with a dark "−".</summary>
    public static Sprite Electron => Build("Electron", 128, 128, 128f, (x, y) =>
    {
        float r = Mathf.Sqrt(x * x + y * y);
        float a = AA(0.96f - r, 0.03f);
        Vector2 hl = new Vector2(-0.35f, 0.4f);
        float dh = Vector2.Distance(new Vector2(x, y), hl);
        float v = 0.5f + 0.5f * Mathf.Clamp01(1f - dh / 1.2f);
        v *= Mathf.Lerp(1f, 0.7f, Mathf.InverseLerp(0.7f, 0.96f, r));
        v += 0.5f * Mathf.Pow(Mathf.Clamp01(1f - dh / 0.28f), 2f);
        float minus = Mathf.Min(0.46f - Mathf.Abs(x), 0.11f - Mathf.Abs(y));
        v = Mathf.Lerp(v, 0.18f, AA(minus, 0.03f));
        return Gray(v, a);
    });

    /// <summary>The player's core: an atomic nucleus of packed protons (bright) and neutrons (dark).</summary>
    public static Sprite Nucleus => Build("Nucleus", 128, 128, 128f, (x, y) =>
    {
        var n = new List<Vector3>();
        for (int i = 0; i < 3; i++)                                           // back
        {
            float a = (30f + i * 120f) * Mathf.Deg2Rad;
            n.Add(new Vector3(Mathf.Cos(a) * 0.6f, Mathf.Sin(a) * 0.6f, i % 2 == 0 ? 0.6f : 1f));
        }
        for (int i = 0; i < 6; i++)                                           // middle ring
        {
            float a = (i * 60f + 10f) * Mathf.Deg2Rad;
            n.Add(new Vector3(Mathf.Cos(a) * 0.42f, Mathf.Sin(a) * 0.42f, i % 2 == 0 ? 1f : 0.6f));
        }
        n.Add(new Vector3(-0.04f, 0.03f, 1f));                                // front
        return Nucleons(x, y, n.ToArray(), 0.31f);
    });

    /// <summary>Photon: a glowing wave packet (a short wave under a bell-shaped envelope).</summary>
    public static Sprite Photon => Build("Photon", 128, 128, 128f, (x, y) =>
    {
        const float k = Mathf.PI * 4f;
        float env = Mathf.Exp(-x * x / 0.3f);
        float wave = 0.34f * Mathf.Sin(x * k) * env;
        float slope = 0.34f * k * Mathf.Cos(x * k) * env;
        float d = Mathf.Abs(y - wave) / Mathf.Sqrt(1f + slope * slope);
        float line = AA(0.075f - d, 0.025f) * Mathf.Clamp01(env * 2.5f);
        float halo = Mathf.Exp(-(x * x / 0.3f + y * y / 0.09f));
        float v = 0.8f + 0.2f * line;
        return Gray(v, Mathf.Max(line, halo * 0.55f));
    });

    /// <summary>Quanta: Rutherford's atom: three tilted elliptical orbits around a small nucleus.</summary>
    public static Sprite Quanta => Build("Quanta", 128, 128, 128f, (x, y) =>
    {
        const float A = 0.9f, B = 0.3f;
        float best = -1f;
        for (int i = 0; i < 3; i++)
        {
            float t = i * 60f * Mathf.Deg2Rad;
            float u = x * Mathf.Cos(t) + y * Mathf.Sin(t), w = -x * Mathf.Sin(t) + y * Mathf.Cos(t);
            float f = Mathf.Sqrt(u * u / (A * A) + w * w / (B * B));
            float g = Mathf.Sqrt(u * u / (A * A * A * A) + w * w / (B * B * B * B)) / Mathf.Max(f, 1e-4f);
            float d = Mathf.Abs(f - 1f) / Mathf.Max(g, 1e-4f);
            best = Mathf.Max(best, 0.055f - d);
        }
        float r = Mathf.Sqrt(x * x + y * y);
        float core = 0.17f - r;
        float a = AA(Mathf.Max(best, core), 0.025f);
        float v = core > best ? 0.75f + 0.25f * Mathf.Clamp01(1f - r / 0.17f) : 0.9f;
        return Gray(v, a);
    });

    // ----------------------------------------------------------- big atom (upgrade screen)

    /// <summary>The upgrade screen's quanta atom: orbit half-axes in sprite units ([-1, 1] across).</summary>
    public const float AtomA = 0.96f, AtomB = 0.384f;
    const float PetalInset = 0.02f;   // gap between a petal and the orbit lines around it

    /// <summary>Approximate signed distance to orbit k (tilted k * 60 degrees), positive inside.</summary>
    public static float AtomOrbitInside(float x, float y, int k)
    {
        float t = k * 60f * Mathf.Deg2Rad;
        float c = Mathf.Cos(t), sn = Mathf.Sin(t);
        float u = x * c + y * sn, w = -x * sn + y * c;
        const float A2 = AtomA * AtomA, B2 = AtomB * AtomB;
        float f = Mathf.Sqrt(u * u / A2 + w * w / B2);
        float g = Mathf.Sqrt(u * u / (A2 * A2) + w * w / (B2 * B2)) / Mathf.Max(f, 1e-4f);
        return (1f - f) / Mathf.Max(g, 1e-4f);
    }

    /// <summary>
    /// Signed distance (positive inside) to petal 0: the tip of orbit 0 that sticks out past the
    /// other two orbits, on the +x side. Petal k is this shape turned by k * 60 degrees.
    /// </summary>
    static float PetalInside(float x, float y)
    {
        float d = Mathf.Min(AtomOrbitInside(x, y, 0), Mathf.Min(-AtomOrbitInside(x, y, 1), -AtomOrbitInside(x, y, 2)));
        return x > 0f ? d - PetalInset : -1f;
    }

    /// <summary>Which petal (0..5, pointing at index * 60 degrees) contains the point, or -1.</summary>
    public static int AtomPetalAt(float x, float y)
    {
        for (int k = 0; k < 3; k++)
        {
            if (AtomOrbitInside(x, y, k) <= 0f) continue;
            if (AtomOrbitInside(x, y, (k + 1) % 3) >= 0f || AtomOrbitInside(x, y, (k + 2) % 3) >= 0f) continue;
            float t = k * 60f * Mathf.Deg2Rad;
            return x * Mathf.Cos(t) + y * Mathf.Sin(t) > 0f ? k : k + 3;
        }
        return -1;
    }

    /// <summary>The three orbits as thin glowing lines.</summary>
    public static Sprite AtomOrbits => Build("AtomOrbits", 768, 768, 768f, (x, y) =>
    {
        float a = 0f;
        for (int k = 0; k < 3; k++)
        {
            float d = Mathf.Abs(AtomOrbitInside(x, y, k));
            a = Mathf.Max(a, Mathf.Max(AA(0.006f - d, 0.004f), 0.3f * Mathf.Exp(-(d / 0.025f) * (d / 0.025f))));
        }
        return new Color(1f, 1f, 1f, a);
    });

    /// <summary>Petal 0's fill: brighter toward the outer tip.</summary>
    public static Sprite AtomPetal => Build("AtomPetal", 512, 512, 512f, (x, y) =>
    {
        float cov = AA(PetalInside(x, y), 0.006f);
        float toward = Mathf.InverseLerp(0.4f, AtomA, x);
        return new Color(1f, 1f, 1f, cov * (0.5f + 0.5f * toward * toward));
    });

    /// <summary>Petal 0's outline.</summary>
    public static Sprite AtomPetalEdge => Build("AtomPetalEdge", 512, 512, 512f, (x, y) =>
    {
        float d = PetalInside(x, y);
        if (x <= 0f) return Color.clear;
        return new Color(1f, 1f, 1f, AA(0.006f - Mathf.Abs(d), 0.004f));
    });

    /// <summary>Padlock (locked petals).</summary>
    public static Sprite Lock => Build("Lock", 128, 128, 128f, (x, y) =>
    {
        // Body: rounded rectangle.
        float qx = Mathf.Abs(x) - 0.62f + 0.14f, qy = Mathf.Abs(y + 0.32f) - 0.44f + 0.14f;
        float body = -(new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - 0.14f);
        // Shackle: upper half of a ring, with legs down into the body.
        float ry = Mathf.Max(0f, y - 0.25f);
        float ring = 0.1f - Mathf.Abs(new Vector2(x, ry).magnitude - 0.38f);
        if (y < 0.12f) ring = -1f;
        // Keyhole cut out of the body.
        float round = 0.11f - new Vector2(x, y + 0.24f).magnitude;
        float slot = Mathf.Min(0.05f - Mathf.Abs(x), Mathf.Min(y + 0.55f, -0.24f - y));
        float hole = Mathf.Max(round, slot);
        float shape = Mathf.Max(Mathf.Min(body, -hole), ring);
        return new Color(1f, 1f, 1f, AA(shape, 0.025f));
    });

    /// <summary>Stability (health): a calm, tightly bound nucleus inside a closed ring.</summary>
    public static Sprite Stability => Build("Stability", 128, 128, 128f, (x, y) =>
    {
        var n = new Vector3[7];
        for (int i = 0; i < 6; i++)
        {
            float a = (i * 60f + 30f) * Mathf.Deg2Rad;
            n[i] = new Vector3(Mathf.Cos(a) * 0.36f, Mathf.Sin(a) * 0.36f, i % 2 == 0 ? 1f : 0.65f);
        }
        n[6] = new Vector3(0f, 0f, 1f);
        Color c = Nucleons(x, y, n, 0.25f);
        float r = Mathf.Sqrt(x * x + y * y);
        float ring = AA(0.06f - Mathf.Abs(r - 0.84f), 0.025f);
        return ring > c.a ? Gray(0.95f, ring) : c;
    });

    /// <summary>Radiation trefoil (Radium / emitters).</summary>
    public static Sprite Trefoil => Build("Trefoil", 128, 128, 128f, (x, y) =>
    {
        float r = Mathf.Sqrt(x * x + y * y);
        float ang = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
        float diff = 180f;
        foreach (float c in new[] { 90f, 210f, 330f }) diff = Mathf.Min(diff, Mathf.Abs(Mathf.DeltaAngle(ang, c)));
        float blade = Mathf.Min(Mathf.Min(r - 0.3f, 0.95f - r), (30f - diff) * Mathf.Deg2Rad * r);
        float hub = 0.19f - r;
        return new Color(1f, 1f, 1f, AA(Mathf.Max(blade, hub), 0.02f));
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
