using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The first-run guide: on the first level ever, a small callout points at a photon, then (once
/// one is collected) an electron, then a quantum, saying what each is for. The game doesn't
/// stop; each callout follows its pickup and stays on screen if the pickup is off it.
/// </summary>
public partial class GameUI
{
    VisualElement guideBox, guideStem;
    Label guideTitle, guideBody;
    GameManager.GuideStep shownGuide = GameManager.GuideStep.None;
    float guideAlpha, guidePop;

    void BuildGuide()
    {
        guideBox = Box(hud);
        guideBox.style.position = Position.Absolute;
        guideBox.style.width = 300;
        guideBox.style.alignItems = Align.Center;

        var panel = Box(guideBox);
        panel.style.alignItems = Align.Center;
        panel.style.paddingLeft = panel.style.paddingRight = 14;
        panel.style.paddingTop = 8;
        panel.style.paddingBottom = 10;
        panel.style.backgroundColor = WithAlpha(Panel, 0.88f);
        Radius(panel, 10);
        guideTitle = Text(panel, "", 20, true, Color.white);
        guideTitle.style.letterSpacing = 5;
        guideBody = Text(panel, "", 15, false, WithAlpha(Color.white, 0.9f), TextAnchor.MiddleCenter, true);
        guideBody.style.marginTop = 3;
        guideBody.style.maxWidth = 272;

        guideStem = Box(guideBox);   // a short line down to the pickup
        guideStem.style.width = 2;
        guideStem.style.height = 22;
        guideBox.style.opacity = 0f;
        shownGuide = GameManager.GuideStep.None;
    }

    void UpdateGuide(GameManager.State st)
    {
        if (guideBox == null) return;
        Pickup target = gm.GuideTarget;
        bool show = target != null && st == GameManager.State.Playing && !gm.Teaching && !gm.AbilityAiming;
        float dt = Time.unscaledDeltaTime;
        guideAlpha = Mathf.MoveTowards(guideAlpha, show ? 1f : 0f, dt / 0.25f);
        guideBox.style.opacity = guideAlpha;
        Show(guideBox, guideAlpha > 0f);
        if (!show && guideAlpha <= 0f) return;

        if (gm.Guide != shownGuide && gm.Guide != GameManager.GuideStep.None)
        {
            shownGuide = gm.Guide;
            guidePop = 1f;
            Color c;
            switch (shownGuide)
            {
                case GameManager.GuideStep.Photon:
                    c = Pickup.Gold;
                    guideTitle.text = Loc.T("PHOTON", "FOTON");
                    guideBody.text = Loc.T("Collect " + gm.StarsNeeded + " to finish the level",
                                           "Bölümü bitirmek için " + gm.StarsNeeded + " tane topla");
                    break;
                case GameManager.GuideStep.Electron:
                    c = Pickup.OrbColor;
                    guideTitle.text = Loc.T("ELECTRON", "ELEKTRON");
                    guideBody.text = Loc.T("It becomes part of you: a new element, and a shield",
                                           "Senin bir parçan olur: yeni bir element ve bir kalkan");
                    break;
                default:
                    c = Pickup.CoinColor;
                    guideTitle.text = Loc.T("QUANTUM", "KUANTUM");
                    guideBody.text = Loc.T("Spend them on upgrades after the level",
                                           "Bölümden sonra geliştirmelere harca");
                    break;
            }
            guideTitle.style.color = c;
            Border(guideTitle.parent, 2f, WithAlpha(c, 0.85f));
            guideStem.style.backgroundColor = WithAlpha(c, 0.8f);
            Sfx.Play("reveal", 0.45f);
        }
        guidePop = Mathf.MoveTowards(guidePop, 0f, dt / 0.35f);
        float bob = Mathf.Sin(Time.unscaledTime * 3f) * 3f;
        float s = 1f + 0.15f * guidePop * guidePop;
        guideBox.style.scale = new Scale(new Vector3(s, s, 1f));

        // Above its pickup, kept on screen.
        var cam = Camera.main;
        if (target == null || cam == null || root.panel == null) return;
        Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, target.transform.position, cam);
        float W = hud.layout.width, H = hud.layout.height;
        float h = guideBox.layout.height;
        if (float.IsNaN(h) || h <= 0f) h = 90f;
        float x = p.x - 150f, y = p.y - h - 26f + bob;
        if (!float.IsNaN(W) && W > 0f)
        {
            x = Mathf.Clamp(x, 12f, W - 312f);
            y = Mathf.Clamp(y, 110f, H - h - 60f);   // below the top HUD, above the speed bar
        }
        guideBox.style.left = x;
        guideBox.style.top = y;
    }
}
