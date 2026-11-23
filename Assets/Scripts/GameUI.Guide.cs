using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The first-run guide: until the first photon, electron and quantum ever are collected, a
/// short trail of chevrons (the speed bar's arrow) sits just outside the atom for each of them,
/// in its color, pointing at the nearest one. Every so often a shine runs along the trail to
/// catch the eye. Collecting the first one opens its lesson (GameUI.Lesson) and its trail goes.
/// </summary>
public partial class GameUI
{
    const int GuideChevrons = 3;
    const float GuideChevronSize = 22f;
    const float GuideChevronStep = 15f;   // between chevrons along the trail
    const float GuideGap = 26f;           // from the nucleus' edge to the first chevron
    const float GuideShineEvery = 2.4f;   // seconds between shines
    const float GuideShineTime = 0.55f;   // a shine's run along the trail

    class GuideTrail
    {
        public VisualElement root;
        public readonly VisualElement[] chevrons = new VisualElement[GuideChevrons];
        public Color color;
        public float alpha, angle;
        public bool aimed;
    }

    readonly GuideTrail[] guideTrails = new GuideTrail[3];

    void BuildGuide()
    {
        for (int i = 0; i < guideTrails.Length; i++)
        {
            var t = new GuideTrail { color = Pickup.ColorOf(GameManager.GuideKinds[i]) };
            // A zero-size anchor at the atom's center; the trail runs along its +x, rotated to the target.
            t.root = Box(hud);
            t.root.style.position = Position.Absolute;
            t.root.style.width = t.root.style.height = 0;
            t.root.style.transformOrigin = new TransformOrigin(0, 0);
            t.root.pickingMode = PickingMode.Ignore;
            for (int k = 0; k < GuideChevrons; k++)
            {
                var c = Icon(t.root, ProcSprites.Arrow, GuideChevronSize, t.color);
                c.style.position = Position.Absolute;
                c.style.top = -GuideChevronSize * 0.5f;
                c.pickingMode = PickingMode.Ignore;
                t.chevrons[k] = c;
            }
            t.root.style.opacity = 0f;
            Show(t.root, false);
            guideTrails[i] = t;
        }
    }

    void UpdateGuide(GameManager.State st)
    {
        if (guideTrails[0] == null) return;
        float dt = Time.unscaledDeltaTime;
        var cam = Camera.main;
        PendulumController pc = gm.Pendulum;
        bool canShow = st == GameManager.State.Playing && !gm.Teaching && !gm.AbilityAiming
                       && cam != null && pc != null && root.panel != null;

        Vector2 at = Vector2.zero;
        float start = 0f;
        if (canShow)
        {
            at = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, pc.PivotPosition, cam);
            Vector2 edge = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel,
                pc.PivotPosition + Vector2.right * Mathf.Max(pc.NucleusRadius, pc.CoreRadius), cam);
            start = Vector2.Distance(at, edge) + GuideGap;
        }

        for (int i = 0; i < guideTrails.Length; i++)
        {
            GuideTrail t = guideTrails[i];
            Pickup target = canShow ? gm.GuideTarget(i) : null;
            bool show = false;
            if (target != null)
            {
                Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, target.transform.position, cam);
                Vector2 d = p - at;
                // Not once it's right next to the atom (the trail would sit on top of it).
                show = d.magnitude > start + GuideChevrons * GuideChevronStep + 30f;
                if (show)
                {
                    float a = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;   // panel y is down: clockwise degrees
                    t.angle = t.aimed ? Mathf.LerpAngle(t.angle, a, 1f - Mathf.Exp(-14f * dt)) : a;
                    t.aimed = true;
                }
            }
            t.alpha = Mathf.MoveTowards(t.alpha, show ? 1f : 0f, dt / 0.25f);
            if (t.alpha <= 0f) t.aimed = false;
            Show(t.root, t.alpha > 0f);
            if (t.alpha <= 0f) continue;

            t.root.style.left = at.x;
            t.root.style.top = at.y;
            t.root.style.rotate = new Rotate(t.angle);
            t.root.style.opacity = t.alpha;

            // The shine: a bright pulse runs outward along the chevrons (the trails take turns).
            float clock = Mathf.Repeat(Time.unscaledTime + i * GuideShineEvery / 3f, GuideShineEvery);
            float idle = 0.55f + 0.15f * Mathf.Sin(Time.unscaledTime * 3f + i);
            for (int k = 0; k < GuideChevrons; k++)
            {
                float u = (clock - k * 0.12f) / (GuideShineTime * 0.6f);
                float shine = u > 0f && u < 1f ? Mathf.Sin(u * Mathf.PI) : 0f;
                var c = t.chevrons[k];
                c.style.left = start + k * GuideChevronStep - GuideChevronSize * 0.5f;
                c.style.unityBackgroundImageTintColor = Color.Lerp(WithAlpha(t.color, idle + 0.1f * k), Color.white, 0.75f * shine);
                float s = 1f + 0.35f * shine;
                c.style.scale = new Scale(new Vector3(s, s, 1f));
            }
        }
    }
}
