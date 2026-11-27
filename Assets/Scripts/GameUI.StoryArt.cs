using UnityEngine;
using UnityEngine.UIElements;

/// <summary>The picture a story line shows above the text (None: keeps the one before).</summary>
public enum StoryArt
{
    None, Ring, Stripped, Racing, Crash, Nucleons, Electrons, Elements,
    Stray, Shield, Stability, Photon, Quanta, Loop, Grow,
}

/// <summary>
/// Simple animated pictures for the story, one per line, drawn above the text: the accelerator
/// ring, the stripped atom, the race, the crash tracks, the pull, and so on. A new line's picture
/// crossfades in over the one before; they all fade out with the beat.
/// </summary>
public partial class GameUI
{
    const float StoryArtW = 440f, StoryArtH = 250f;
    const float StoryArtCross = 0.45f;

    VisualElement storyArt, storyArtCanvas, storyArtIcon;
    StoryArt artCur, artPrev;
    float artCurAge, artPrevAge, artPrevAlpha, artFade = 1f;

    static readonly Color ProtonColor = new Color(1f, 0.5f, 0.45f);
    static readonly Color NeutronColor = new Color(0.82f, 0.84f, 0.9f);
    static readonly Color[] StrayColors =
    {
        new Color(1f, 0.55f, 0.25f), new Color(0.85f, 0.3f, 0.6f), new Color(0.55f, 1f, 0.35f), new Color(0.7f, 0.55f, 1f),
    };

    /// <summary>Called as a beat starts: a picture area above its lines, if any line has a picture.</summary>
    void BeginStoryArt(bool any)
    {
        artCur = artPrev = StoryArt.None;
        artPrevAlpha = 0f;
        storyArt = storyArtCanvas = storyArtIcon = null;
        if (!any) return;
        storyArt = Box(storyBody);
        storyArt.style.width = StoryArtW;
        storyArt.style.height = StoryArtH;
        storyArt.style.marginBottom = 28;
        storyArtCanvas = Layer(storyArt);
        storyArtCanvas.generateVisualContent += DrawStoryArt;
        storyArtIcon = Icon(storyArt, ProcSprites.Photon, 130f, Color.white);
        storyArtIcon.style.position = Position.Absolute;
        storyArtIcon.style.left = (StoryArtW - 130f) * 0.5f;
        storyArtIcon.style.top = (StoryArtH - 130f) * 0.5f;
        storyArtIcon.style.opacity = 0f;
    }

    void ShowStoryArt(StoryArt a)
    {
        if (a == artCur || storyArt == null) return;
        artPrev = artCur;
        artPrevAge = artCurAge;
        artPrevAlpha = artCur == StoryArt.None ? 0f : 1f;
        artCur = a;
        artCurAge = 0f;
    }

    void UpdateStoryArt(float dt, float fade)
    {
        if (storyArt == null) return;
        artCurAge += dt;
        artPrevAge += dt;
        artPrevAlpha = Mathf.MoveTowards(artPrevAlpha, 0f, dt / StoryArtCross);
        artFade = fade;

        // Pictures that are a sprite (photon, quantum, stability) use the icon element.
        float curA = Mathf.Clamp01(artCurAge / StoryArtCross) * fade;
        Sprite sprite = null;
        Color tint = Color.white;
        switch (artCur)
        {
            case StoryArt.Photon: sprite = ProcSprites.Photon; tint = Pickup.Gold; break;
            case StoryArt.Quanta: sprite = ProcSprites.Quanta; tint = Pickup.CoinColor; break;
            case StoryArt.Stability: sprite = ProcSprites.Stability; tint = Pickup.HeartColor; break;
        }
        if (sprite != null)
        {
            storyArtIcon.style.backgroundImage = new StyleBackground(sprite);
            storyArtIcon.style.unityBackgroundImageTintColor = tint;
            float beat = artCur == StoryArt.Stability ? 1f + 0.08f * Mathf.Max(0f, Mathf.Sin(artCurAge * 5f)) : 1f + 0.03f * Mathf.Sin(artCurAge * 2f);
            storyArtIcon.style.scale = new Scale(new Vector3(beat, beat, 1f));
        }
        storyArtIcon.style.opacity = sprite != null ? curA : 0f;
        storyArtCanvas.MarkDirtyRepaint();
    }

    void DrawStoryArt(MeshGenerationContext mgc)
    {
        var p = mgc.painter2D;
        p.lineCap = LineCap.Round;
        p.lineJoin = LineJoin.Round;
        Vector2 c = new Vector2(StoryArtW * 0.5f, StoryArtH * 0.5f);
        if (artPrevAlpha > 0f) DrawArt(p, c, artPrev, artPrevAge, artPrevAlpha * artFade);
        DrawArt(p, c, artCur, artCurAge, Mathf.Clamp01(artCurAge / StoryArtCross) * artFade);
    }

    // ------------------------------------------------------------- the pictures

    void DrawArt(Painter2D p, Vector2 c, StoryArt art, float t, float a)
    {
        if (a <= 0.003f) return;
        Color e = Pickup.OrbColor, gold = Pickup.Gold, white = Color.white;
        switch (art)
        {
            case StoryArt.Ring:   // the accelerator, with its detectors
                StrokeCircle(p, c, 100f, 2.5f, WithAlpha(white, 0.4f * a));
                StrokeCircle(p, c, 92f, 1f, WithAlpha(white, 0.15f * a));
                for (int i = 0; i < 4; i++)
                {
                    float ang = (45f + i * 90f) * Mathf.Deg2Rad;
                    Vector2 q = c + Dir(ang) * 96f;
                    float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2f + i * 1.6f);
                    FillCircle(p, q, 5f, WithAlpha(gold, (0.4f + 0.4f * pulse) * a));
                }
                break;

            case StoryArt.Stripped:   // the nucleus, its electrons flying off
                DrawNucleus(p, c, 16f, 9, a);
                for (int i = 0; i < 4; i++)
                {
                    float ph = Mathf.Repeat(t * 0.45f + i / 4f, 1f);
                    float ang = (30f + i * 90f) * Mathf.Deg2Rad;
                    float d = 24f + 160f * ph;
                    Seg(p, c + Dir(ang) * Mathf.Max(24f, d - 30f), c + Dir(ang) * d, 2f, WithAlpha(e, 0.35f * (1f - ph) * a));
                    FillCircle(p, c + Dir(ang) * d, 4.5f, WithAlpha(e, (1f - ph) * a));
                }
                break;

            case StoryArt.Racing:   // round and round the ring, near light speed
            {
                StrokeCircle(p, c, 100f, 2f, WithAlpha(white, 0.25f * a));
                float head = t * 5f;
                for (int k = 14; k >= 0; k--)
                {
                    float ang = head - k * 0.07f;
                    float f = 1f - k / 15f;
                    FillCircle(p, c + Dir(ang) * 100f, 2f + 4f * f, WithAlpha(Color.Lerp(gold, white, f), f * f * a));
                }
                FillCircle(p, c + Dir(head) * 100f, 12f, WithAlpha(gold, 0.25f * a));
                break;
            }

            case StoryArt.Crash:   // tracks spraying out from the collision point
            {
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.7f), 3f);
                for (int i = 0; i < 22; i++)
                {
                    float h1 = Hash(i), h2 = Hash(i + 50), h3 = Hash(i + 100);
                    float ang = i * Mathf.PI * 2f / 22f + h1 * 0.4f;
                    float len = 170f * k * (0.45f + 0.55f * h2);
                    float bend = (h3 - 0.5f) * 40f;
                    Color col = h3 < 0.33f ? gold : h3 < 0.66f ? e : white;
                    Vector2 dir = Dir(ang), side = new Vector2(-dir.y, dir.x);
                    p.strokeColor = WithAlpha(col, 0.8f * a);
                    p.lineWidth = 1.6f;
                    p.BeginPath();
                    for (int s = 0; s <= 10; s++)
                    {
                        float u = s / 10f;
                        Vector2 q = c + dir * len * u + side * Mathf.Sin(u * Mathf.PI) * bend * u;
                        if (s == 0) p.MoveTo(q); else p.LineTo(q);
                    }
                    p.Stroke();
                }
                FillCircle(p, c, 30f * (1f - k) + 4f, WithAlpha(white, (0.9f - 0.6f * k) * a));
                break;
            }

            case StoryArt.Nucleons:   // protons and neutrons drawn into the core
            {
                float core = 10f + 8f * Mathf.Clamp01(t / 3f);
                DrawNucleus(p, c, core, 6 + Mathf.Min(10, Mathf.FloorToInt(t * 2f)), a);
                for (int i = 0; i < 10; i++)
                {
                    float ph = Mathf.Repeat(t * 0.35f + i / 10f, 1f);
                    float ang = (i * 36f + 25f * ph) * Mathf.Deg2Rad;
                    float d = Mathf.Lerp(170f, core, ph * ph);
                    FillCircle(p, c + Dir(ang) * d, 4.5f, WithAlpha(i % 2 == 0 ? ProtonColor : NeutronColor, Mathf.Clamp01(ph * 4f) * a));
                }
                break;
            }

            case StoryArt.Electrons:   // electrons arriving one by one onto the orbit
            {
                DrawNucleus(p, c, 15f, 10, a);
                StrokeCircle(p, c, 64f, 1.2f, WithAlpha(e, 0.25f * a));
                int n = Mathf.Min(8, 1 + Mathf.FloorToInt(t / 0.55f));
                float pop = 1f - Mathf.Clamp01(Mathf.Repeat(t, 0.55f) / 0.3f);
                for (int k2 = 0; k2 < n; k2++)
                {
                    float ang = t * 1.2f + k2 * Mathf.PI * 2f / n;
                    float r = 5f + (k2 == n - 1 && n < 8 ? 4f * pop : 0f);
                    FillCircle(p, c + Dir(ang) * 64f, r, WithAlpha(e, a));
                }
                break;
            }

            case StoryArt.Elements:   // hydrogen, helium, carbon, neon: the atom changes
            {
                int[] counts = { 1, 2, 6, 10 };
                int idx = Mathf.FloorToInt(t / 0.9f) % counts.Length;
                int n = counts[idx];
                float pop = 1f + 0.15f * (1f - Mathf.Clamp01(Mathf.Repeat(t, 0.9f) / 0.25f));
                DrawNucleus(p, c, (8f + n * 1.1f) * pop, 3 + n * 2, a);
                int inner = Mathf.Min(n, 2), outer = n - inner;
                StrokeCircle(p, c, 44f * pop, 1.2f, WithAlpha(e, 0.25f * a));
                if (outer > 0) StrokeCircle(p, c, 78f * pop, 1.2f, WithAlpha(e, 0.25f * a));
                for (int k2 = 0; k2 < inner; k2++)
                    FillCircle(p, c + Dir(t * 1.6f + k2 * Mathf.PI) * 44f * pop, 5f, WithAlpha(e, a));
                for (int k2 = 0; k2 < outer; k2++)
                    FillCircle(p, c + Dir(-t * 1.1f + k2 * Mathf.PI * 2f / outer) * 78f * pop, 5f, WithAlpha(e, a));
                break;
            }

            case StoryArt.Stray:   // particles drifting through the chamber
                for (int i = 0; i < 16; i++)
                {
                    float h1 = Hash(i), h2 = Hash(i + 7), h3 = Hash(i + 13);
                    float x = Mathf.Repeat(h2 * 480f + t * (35f + 55f * h3), 480f) - 240f;
                    float y = (h1 - 0.5f) * 210f;
                    float edge = Mathf.Clamp01((240f - Mathf.Abs(x)) / 50f);
                    FillCircle(p, c + new Vector2(x, y), 3f + 2.5f * h3, WithAlpha(StrayColors[i % StrayColors.Length], 0.85f * edge * a));
                }
                break;

            case StoryArt.Shield:   // a particle bounces off the orbiting electrons
            {
                DrawNucleus(p, c, 12f, 8, a);
                StrokeCircle(p, c, 58f, 1.2f, WithAlpha(e, 0.25f * a));
                for (int k2 = 0; k2 < 5; k2++)
                    FillCircle(p, c + Dir(t * 1.6f + k2 * Mathf.PI * 0.4f) * 58f, 5f, WithAlpha(e, a));
                float u = Mathf.Repeat(t / 2.2f, 1f);
                float x = u < 0.5f ? Mathf.Lerp(-215f, -72f, u / 0.5f) : Mathf.Lerp(-72f, -215f, (u - 0.5f) / 0.5f);
                FillCircle(p, c + new Vector2(x, -18f + 30f * u), 6f, WithAlpha(StrayColors[0], a));
                float hit = Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) * 10f);
                if (hit > 0f) StrokeCircle(p, c, 58f, 2.5f, WithAlpha(white, 0.8f * hit * a));
                break;
            }

            case StoryArt.Stability:
                StrokeCircle(p, c, 78f, 1.5f, WithAlpha(Pickup.HeartColor, (0.2f + 0.15f * Mathf.Sin(t * 5f)) * a));
                break;

            case StoryArt.Photon:   // soft waves around the photon icon
            case StoryArt.Quanta:
            {
                Color col = art == StoryArt.Photon ? gold : Pickup.CoinColor;
                for (int i = 0; i < 3; i++)
                {
                    float ph = Mathf.Repeat(t * 0.4f + i / 3f, 1f);
                    StrokeCircle(p, c, 60f + 70f * ph, 1.2f, WithAlpha(col, 0.35f * (1f - ph) * a));
                }
                break;
            }

            case StoryArt.Loop:   // lap after lap; each lap leaves a dot in the middle
            {
                float ang = t * 6f;
                int laps = Mathf.FloorToInt(ang / (Mathf.PI * 2f));
                float lapFlash = 1f - Mathf.Clamp01(Mathf.Repeat(ang, Mathf.PI * 2f) / 1.2f);
                StrokeCircle(p, c, 95f, 2f, WithAlpha(Color.Lerp(white, gold, lapFlash), (0.25f + 0.4f * lapFlash) * a));
                FillCircle(p, c + Dir(ang) * 95f, 6f, WithAlpha(gold, a));
                int dots = Mathf.Min(laps, 12);
                for (int k2 = 0; k2 < dots; k2++)
                    FillCircle(p, c + Dir(k2 * Mathf.PI * 2f / 12f - Mathf.PI * 0.5f) * 32f, 3.5f, WithAlpha(white, 0.7f * a));
                break;
            }

            case StoryArt.Grow:   // the atom gains shell after shell
            {
                int shells = Mathf.Min(4, 1 + Mathf.FloorToInt(t / 0.8f));
                float pop = 1f - Mathf.Clamp01(Mathf.Repeat(t, 0.8f) / 0.35f);
                DrawNucleus(p, c, 8f + shells * 4f, 4 + shells * 5, a);
                for (int s = 0; s < shells; s++)
                {
                    float r = 30f + s * 21f;
                    float sa = s == shells - 1 && shells < 4 ? 1f - 0.6f * pop : 1f;
                    StrokeCircle(p, c, r, 1.2f, WithAlpha(e, 0.25f * sa * a));
                    int n = 2 + s * 2;
                    float dir = s % 2 == 0 ? 1f : -1f;
                    for (int k2 = 0; k2 < n; k2++)
                        FillCircle(p, c + Dir(dir * t * (1.5f - 0.2f * s) + k2 * Mathf.PI * 2f / n) * r, 4f, WithAlpha(e, sa * a));
                }
                break;
            }
        }
    }

    /// <summary>A small cluster of protons and neutrons.</summary>
    static void DrawNucleus(Painter2D p, Vector2 c, float radius, int count, float a)
    {
        count = Mathf.Max(1, count);
        for (int i = 0; i < count; i++)
        {
            float ang = i * 2.39996f;   // golden angle: an even, round cluster
            float d = radius * 0.62f * Mathf.Sqrt((i + 0.5f) / count);
            FillCircle(p, c + Dir(ang) * d, Mathf.Max(2.5f, radius * 0.36f), WithAlpha(i % 2 == 0 ? ProtonColor : NeutronColor, a));
        }
    }

    static Vector2 Dir(float rad) => new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

    static float Hash(int i) => Mathf.Repeat(Mathf.Sin(i * 12.9898f + 4.1f) * 43758.5453f, 1f);

    static void Seg(Painter2D p, Vector2 a, Vector2 b, float width, Color col)
    {
        if (col.a <= 0.003f) return;
        p.strokeColor = col;
        p.lineWidth = width;
        p.BeginPath();
        p.MoveTo(a);
        p.LineTo(b);
        p.Stroke();
    }
}
