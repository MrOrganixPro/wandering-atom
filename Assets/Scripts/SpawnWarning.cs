using System;
using UnityEngine;

/// <summary>
/// Telegraph shown before enemies enter: a faint lane along the path (fixed in the
/// world) and a pulsing arrow where the path enters the screen (recomputed every
/// frame so it stays on the screen edge while the camera moves).
///
/// Bundles get a distinct cue: a wide striped lane, a triple chevron, a spinning ring,
/// and one pip per enemy in the bundle.
/// Runs after GameManager so it uses this frame's camera position.
/// </summary>
[DefaultExecutionOrder(100)]
public class SpawnWarning : MonoBehaviour
{
    Vector2 start, dir, perp;
    float length, laneWidth;
    int count;
    Action onDone;
    float duration, t;
    Color color;

    SpriteRenderer lane, laneEdgeA, laneEdgeB, arrowGlow, ring;
    SpriteRenderer[] arrows;
    SpriteRenderer[] pips;

    bool IsBundle => count > 1;

    public void Init(Vector2 startPos, Vector2 direction, float pathLength, Color c, float time,
        int enemyCount, float bundleWidth, Action done)
    {
        start = startPos;
        dir = direction.normalized;
        perp = new Vector2(-dir.y, dir.x);
        length = pathLength;
        color = c;
        duration = Mathf.Max(0.05f, time);
        count = Mathf.Max(1, enemyCount);
        laneWidth = IsBundle ? bundleWidth + 0.4f : Mathf.Max(0.5f, bundleWidth);
        onDone = done;

        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0f, 0f, ang);
        Vector2 mid = start + dir * (length * 0.5f);

        lane = Make("Lane", ProcSprites.Pixel, -20, 1f);
        lane.transform.SetPositionAndRotation(mid, rot);
        if (IsBundle)
        {
            laneEdgeA = Make("LaneEdgeA", ProcSprites.Pixel, -19, 1f);
            laneEdgeB = Make("LaneEdgeB", ProcSprites.Pixel, -19, 1f);
            laneEdgeA.transform.rotation = rot;
            laneEdgeB.transform.rotation = rot;
        }

        arrowGlow = Make("ArrowGlow", ProcSprites.Glow, 39, 1.6f);
        arrows = new SpriteRenderer[IsBundle ? 3 : 1];
        for (int i = 0; i < arrows.Length; i++)
        {
            arrows[i] = Make("Arrow" + i, ProcSprites.Arrow, 40, 0.5f);
            arrows[i].transform.rotation = rot;
        }

        if (IsBundle)
        {
            ring = Make("Ring", ProcSprites.Ring, 39, 1.3f);
            int shown = Mathf.Min(count, 12);
            pips = new SpriteRenderer[shown];
            for (int i = 0; i < shown; i++) pips[i] = Make("Pip" + i, ProcSprites.Ball, 41, 0.12f);
        }

        Sfx.Play("warn");
        if (IsBundle) Sfx.Play("warn", 0.7f);
    }

    SpriteRenderer Make(string name, Sprite sprite, int order, float scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        sr.color = Color.clear;
        return sr;
    }

    void Update()
    {
        t += Time.deltaTime;
        if (t >= duration)
        {
            onDone?.Invoke();
            Destroy(gameObject);
        }
    }

    Color C(float a) => new Color(color.r, color.g, color.b, a);

    void LateUpdate()
    {
        float p = Mathf.Clamp01(t / duration);
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * Mathf.Lerp(12f, 30f, p));
        float fadeIn = Mathf.Clamp01(t / 0.12f);

        // Lane: single enemies narrow down; bundles keep their full width with bright edges.
        float w = IsBundle ? laneWidth : Mathf.Lerp(laneWidth, Mathf.Max(0.06f, laneWidth * 0.6f - 0.3f), p);
        lane.transform.localScale = new Vector3(length, w, 1f);
        lane.color = C(fadeIn * Mathf.Lerp(0.04f, IsBundle ? 0.1f : 0.16f, p));
        if (IsBundle)
        {
            Vector2 mid = start + dir * (length * 0.5f);
            float edgeA = 0.05f + 0.25f * p * pulse;
            laneEdgeA.transform.position = mid + perp * (w * 0.5f);
            laneEdgeB.transform.position = mid - perp * (w * 0.5f);
            laneEdgeA.transform.localScale = laneEdgeB.transform.localScale = new Vector3(length, 0.05f, 1f);
            laneEdgeA.color = laneEdgeB.color = C(fadeIn * edgeA);
        }

        // Arrow(s) where the path enters the (inset) screen rectangle.
        const float inset = 0.7f;
        Vector2 vc = GameManager.ViewCenter;
        Vector2 half = GameManager.HalfExtents - new Vector2(inset, inset);
        bool visible = RayRect(start, dir, vc - half, vc + half, out float enter) && enter <= length;
        if (!visible)
        {
            foreach (var a in arrows) a.color = Color.clear;
            arrowGlow.color = Color.clear;
            if (ring != null) ring.color = Color.clear;
            if (pips != null) foreach (var pp in pips) pp.color = Color.clear;
            return;
        }

        Vector2 pos = start + dir * enter;
        for (int i = 0; i < arrows.Length; i++)
        {
            // Bundles: three chevrons that ripple inward.
            float ripple = IsBundle ? 0.5f + 0.5f * Mathf.Sin(t * 14f - i * 1.2f) : pulse;
            arrows[i].transform.position = pos + dir * (0.15f * pulse + i * 0.24f);
            arrows[i].transform.localScale = Vector3.one * (0.45f + 0.12f * p);
            arrows[i].color = C(fadeIn * (0.45f + 0.55f * ripple));
        }
        arrowGlow.transform.position = pos + dir * (IsBundle ? 0.25f : 0f);
        arrowGlow.transform.localScale = Vector3.one * (IsBundle ? 2.4f : 1.6f);
        arrowGlow.color = C(fadeIn * (0.25f + 0.3f * pulse));

        if (IsBundle)
        {
            Vector2 c = pos + dir * 0.25f;
            ring.transform.position = c;
            ring.transform.rotation = Quaternion.Euler(0f, 0f, t * 180f);
            ring.transform.localScale = Vector3.one * (1.3f + 0.25f * pulse);
            ring.color = C(fadeIn * (0.4f + 0.4f * pulse));

            // Pips (one per enemy) in a row across the lane, just inside the arrow.
            int n = pips.Length;
            for (int i = 0; i < n; i++)
            {
                float lat = (i - (n - 1) * 0.5f) * 0.2f;
                pips[i].transform.position = c + dir * 0.9f + perp * lat;
                pips[i].color = new Color(1f, 1f, 1f, fadeIn * (0.6f + 0.4f * pulse));
            }
        }
    }

    /// <summary>Ray vs axis-aligned rectangle; returns distance along the ray where it enters.</summary>
    static bool RayRect(Vector2 o, Vector2 d, Vector2 min, Vector2 max, out float tEnter)
    {
        float t0 = float.NegativeInfinity, t1 = float.PositiveInfinity;
        for (int axis = 0; axis < 2; axis++)
        {
            float oa = o[axis], da = d[axis];
            if (Mathf.Abs(da) < 1e-6f)
            {
                if (oa < min[axis] || oa > max[axis]) { tEnter = 0f; return false; }
            }
            else
            {
                float a = (min[axis] - oa) / da;
                float b = (max[axis] - oa) / da;
                if (a > b) { float tmp = a; a = b; b = tmp; }
                t0 = Mathf.Max(t0, a);
                t1 = Mathf.Min(t1, b);
            }
        }
        tEnter = Mathf.Max(t0, 0f);
        return t1 >= tEnter;
    }
}
