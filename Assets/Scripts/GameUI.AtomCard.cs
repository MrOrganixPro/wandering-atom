using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The atom card (top-left of the HUD): the element tile with the character's badge on its
/// corner, and next to it a small drawing of the atom: one ring per energy level with a dot per
/// electron slot (Radiant's emitters on an inner ring), gold while the Noble Atom's bonds can't
/// break. Around the drawing, the character's own meter: Conductive's charge, the Noble Atom's
/// full shell / lingering time, the Halogen and Alkali Metal powers, the ability's readiness.
/// Under the card: this level's goals (photons, quanta), then the run's resources.
/// </summary>
public partial class GameUI
{
    const float AtomViewSize = 96f;

    VisualElement atomView, characterBadge;
    GameManager.Character shownBadge = (GameManager.Character)(-1);

    VisualElement BuildAtomCard(VisualElement parent)
    {
        var card = Row(parent);
        card.style.alignItems = Align.Center;

        BuildElementTile(card);

        // The character's badge sits on the tile's lower-right corner.
        characterBadge = Box(elementTile);
        characterBadge.style.position = Position.Absolute;
        characterBadge.style.right = -10;
        characterBadge.style.bottom = -10;
        characterBadge.style.width = characterBadge.style.height = 30;
        characterBadge.style.alignItems = Align.Center;
        characterBadge.style.justifyContent = Justify.Center;
        characterBadge.style.backgroundColor = WithAlpha(Panel, 0.95f);
        Radius(characterBadge, 15);
        Show(characterBadge, false);

        atomView = Box(card);
        atomView.style.width = atomView.style.height = AtomViewSize;
        atomView.style.marginLeft = 14;
        atomView.generateVisualContent += DrawAtomView;
        return card;
    }

    void UpdateAtomCard()
    {
        if (shownBadge != gm.ActiveCharacter)
        {
            shownBadge = gm.ActiveCharacter;
            bool any = shownBadge != GameManager.Character.None;
            Show(characterBadge, any);
            characterBadge.Clear();
            if (any)
            {
                GameManager.UpgradeType card = GameManager.CharacterCard(shownBadge);
                Color c = GameManager.UpgradeColor(card);
                Icon(characterBadge, IconFor(card), 20, c);
                Border(characterBadge, 1.5f, WithAlpha(c, 0.8f));
            }
        }
        atomView.MarkDirtyRepaint();
    }

    void DrawAtomView(MeshGenerationContext mgc)
    {
        PendulumController pc = gm != null ? gm.Pendulum : null;
        if (pc == null) return;
        var p = mgc.painter2D;
        p.lineCap = LineCap.Round;
        Vector2 c = new Vector2(AtomViewSize * 0.5f, AtomViewSize * 0.5f);
        float ut = Time.unscaledTime;

        // Noble Atom: the rings shine gold while the bonds can't break.
        float noble = gm.ActiveCharacter == GameManager.Character.Sluggish ? Mathf.Clamp01(pc.NobleBond) : 0f;
        Color gold = PendulumController.NobleGold;

        int shells = pc.ShellTotal;
        bool emitters = pc.TurretSlots > 0;
        const float Inner = 13f, Outer = 39f;
        int rings = shells + (emitters ? 1 : 0);
        float step = rings > 1 ? (Outer - Inner) / (rings - 1) : 0f;

        FillCircle(p, c, 5f, WithAlpha(Color.Lerp(new Color(1f, 0.85f, 0.5f), gold, noble), 0.9f));   // the nucleus

        int ring = 0;
        if (emitters)   // Radiant: its emitters on the innermost ring
        {
            float r = rings > 1 ? Inner : (Inner + Outer) * 0.5f;
            Color rc = GameManager.UpgradeColor(GameManager.UpgradeType.Spaceship);
            StrokeCircle(p, c, r, 1f, WithAlpha(rc, 0.18f));
            DrawSlots(p, c, r, pc.TurretSlots, pc.TurretCount, rc, 3f, ut * 0.6f);
            ring++;
        }
        for (int s = 0; s < shells; s++, ring++)
        {
            float r = rings > 1 ? Inner + step * ring : (Inner + Outer) * 0.5f;
            Color sc = pc.ShellColor(s);
            int cap = pc.ShellCapacity(s), count = pc.ShellCount(s);
            Color line = WithAlpha(sc, count > 0 ? 0.22f : 0.08f);
            if (noble > 0f)
            {
                float shimmer = 0.8f + 0.2f * Mathf.Sin(ut * 4f + s * 1.3f);
                line = Color.Lerp(line, WithAlpha(gold, 0.85f * shimmer), noble);
            }
            StrokeCircle(p, c, r, noble > 0f ? 1.6f : 1f, line);
            float dot = Mathf.Clamp(Mathf.PI * r / Mathf.Max(1, cap) * 0.42f, 1.4f, 3f);
            // Outer levels turn slower, like the real orbits; alternate directions keep it lively.
            DrawSlots(p, c, r, cap, count, sc, dot, ut * (0.9f - 0.12f * s) * (s % 2 == 0 ? 1f : -1f), noble, gold);
        }

        DrawCharacterMeter(p, c, AtomViewSize * 0.5f - 3f, ut);
    }

    static void DrawSlots(Painter2D p, Vector2 c, float r, int cap, int count, Color col, float dot, float spin,
                          float noble = 0f, Color edge = default)
    {
        for (int i = 0; i < cap; i++)
        {
            float a = spin + i * Mathf.PI * 2f / Mathf.Max(1, cap) - Mathf.PI * 0.5f;
            Vector2 q = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            bool on = i < count;
            if (on && noble > 0f) FillCircle(p, q, dot + 1.1f, WithAlpha(edge, noble));   // gold edge
            FillCircle(p, q, dot, on ? col : new Color(1f, 1f, 1f, 0.13f));
        }
    }

    /// <summary>The character's own meter, as a ring around the atom drawing.</summary>
    void DrawCharacterMeter(Painter2D p, Vector2 c, float r, float ut)
    {
        var faint = new Color(1f, 1f, 1f, 0.08f);
        switch (gm.ActiveCharacter)
        {
            case GameManager.Character.Conductive:
            {
                // One segment per conductivity point; all of them flicker white when overcharged.
                int n = GameManager.MaxConductivity;
                bool over = gm.Overcharged;
                Color lit = over ? Color.Lerp(GameManager.ConductiveColor, Color.white, 0.5f + 0.5f * Mathf.Sin(ut * 14f))
                                 : GameManager.ConductiveColor;
                const float Gap = 10f;
                float seg = 360f / n;
                for (int i = 0; i < n; i++)
                {
                    float a0 = -90f + i * seg + Gap * 0.5f;
                    ArcStroke(p, c, r, a0, a0 + seg - Gap, 3f, i < gm.Conductivity || over ? lit : faint);
                }
                break;
            }
            case GameManager.Character.Sluggish:
            {
                // Full while the shell is full; drains while the bonds linger after it.
                float k = gm.IsNobleForm ? 1f : gm.NobleLinger01;
                ArcStroke(p, c, r, -90f, 270f, 3f, faint);
                if (k > 0f) ArcStroke(p, c, r, -90f, -90f + 360f * k, 3f, PendulumController.NobleGold);
                break;
            }
            case GameManager.Character.Halogen:
            case GameManager.Character.Alkali:
            case GameManager.Character.Excited:
            {
                bool halogen = gm.ActiveCharacter == GameManager.Character.Halogen;
                Color col = halogen ? GameManager.HalogenColor
                          : gm.ActiveCharacter == GameManager.Character.Alkali ? GameManager.AlkaliColor : GameManager.ExcitedColor;
                // Halogen: lit while its power is on. Alkali / Excited: fills as the ability recharges.
                bool active = gm.ActiveCharacter == GameManager.Character.Excited ? gm.Ability != GameManager.AbilityKind.None
                            : gm.Trait != GameManager.ElementTrait.None;
                float k = !active ? 0f : halogen ? 1f : 1f - gm.AbilityCooldown01;
                ArcStroke(p, c, r, -90f, 270f, 3f, faint);
                if (k > 0f)
                {
                    float pulse = k >= 1f ? 0.75f + 0.25f * Mathf.Sin(ut * 4f) : 0.6f;
                    ArcStroke(p, c, r, -90f, -90f + 360f * k, 3f, WithAlpha(col, pulse));
                }
                break;
            }
        }
    }

    /// <summary>An arc from a0 to a1 degrees (0 = right, clockwise, y down).</summary>
    static void ArcStroke(Painter2D p, Vector2 c, float r, float a0, float a1, float width, Color col)
    {
        if (col.a <= 0.003f || a1 <= a0) return;
        p.strokeColor = col;
        p.lineWidth = width;
        p.BeginPath();
        p.Arc(c, r, Angle.Degrees(a0), Angle.Degrees(a1));
        p.Stroke();
    }
}
