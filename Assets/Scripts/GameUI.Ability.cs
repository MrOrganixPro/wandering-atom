using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The Noble Atom's ability button (bottom middle of the HUD): its icon, a cooldown shade that
/// drains away, greyed out without an electron to burn. Clicking it stops time to aim; while
/// aiming a hint explains how to fire or cancel.
/// </summary>
public partial class GameUI
{
    VisualElement abilityBox, abilityButton, abilityShade, abilityIconHolder;
    Label abilityHint;
    GameManager.AbilityKind? shownAbility;
    float abilityReadyPop;
    bool abilityWasReady;

    void BuildAbilityButton()
    {
        abilityBox = Box(hud);
        abilityBox.style.position = Position.Absolute;
        abilityBox.style.left = abilityBox.style.right = 0;
        abilityBox.style.bottom = 36;
        abilityBox.style.alignItems = Align.Center;

        abilityHint = Text(abilityBox, "", 15, true, Color.white);
        abilityHint.style.letterSpacing = 2;
        abilityHint.style.marginBottom = 8;

        abilityButton = new VisualElement { pickingMode = PickingMode.Position };
        abilityButton.style.width = abilityButton.style.height = 66;
        abilityButton.style.justifyContent = Justify.Center;
        abilityButton.style.alignItems = Align.Center;
        abilityButton.style.overflow = Overflow.Hidden;
        Radius(abilityButton, 33);
        abilityBox.Add(abilityButton);

        abilityIconHolder = Box(abilityButton);
        abilityShade = Box(abilityButton);   // the cooldown: a dark shade that drains from the top
        abilityShade.style.position = Position.Absolute;
        abilityShade.style.left = abilityShade.style.right = abilityShade.style.bottom = 0;
        abilityShade.style.backgroundColor = new Color(0f, 0f, 0.02f, 0.7f);

        abilityButton.RegisterCallback<PointerEnterEvent>(_ => gm.PointerOverAbilityButton = true);
        abilityButton.RegisterCallback<PointerLeaveEvent>(_ => gm.PointerOverAbilityButton = false);
        abilityButton.RegisterCallback<PointerDownEvent>(ev => { if (ev.button == 0) gm.ToggleAbilityAim(); });
        shownAbility = null;
        Show(abilityBox, false);
    }

    void UpdateAbilityButton(GameManager.State st)
    {
        if (abilityBox == null) return;
        GameManager.AbilityKind ability = gm.Ability;
        bool show = ability != GameManager.AbilityKind.None && st == GameManager.State.Playing && !gm.Teaching;
        Show(abilityBox, show);
        if (!show) { gm.PointerOverAbilityButton = false; return; }

        Color c = ability == GameManager.AbilityKind.Dash ? GameManager.DashColor
                : ability == GameManager.AbilityKind.Blast ? GameManager.BlastColor : GameManager.AlkaliColor;
        if (shownAbility != ability)
        {
            shownAbility = ability;
            abilityIconHolder.Clear();
            Sprite icon = ability == GameManager.AbilityKind.Dash ? ProcSprites.Arrow
                        : ability == GameManager.AbilityKind.Blast ? ProcSprites.Gamma : ProcSprites.Electron;
            Icon(abilityIconHolder, icon, 34, c);
        }

        bool aiming = gm.AbilityAiming, ready = gm.AbilityReady;
        float cd = gm.AbilityCooldown01;
        if (ready && !abilityWasReady && cd <= 0f) abilityReadyPop = 1f;   // ready again: a little pop
        abilityWasReady = ready;
        abilityReadyPop = Mathf.MoveTowards(abilityReadyPop, 0f, Time.unscaledDeltaTime / 0.35f);

        abilityShade.style.height = Length.Percent(cd * 100f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
        Color border = aiming ? Color.Lerp(c, Color.white, pulse * 0.6f) : ready ? c : WithAlpha(Color.white, 0.25f);
        Border(abilityButton, aiming ? 3.5f : 2.5f, border);
        abilityButton.style.backgroundColor = aiming ? WithAlpha(c, 0.3f) : ready ? WithAlpha(c, 0.14f) : WithAlpha(Panel, 0.6f);
        abilityIconHolder.style.opacity = ready || aiming ? 1f : 0.35f;
        float s = 1f + 0.18f * abilityReadyPop * abilityReadyPop + (aiming ? 0.06f * pulse : 0f);
        abilityButton.style.scale = new Scale(new Vector3(s, s, 1f));

        if (aiming)
            abilityHint.text = Loc.T("CLICK  ·  ESC CANCEL", "TIKLA  ·  ESC İPTAL");
        else if (!gm.AbilityHasElectron)
            abilityHint.text = Loc.T("NO ELECTRON", "ELEKTRON YOK");
        else abilityHint.text = "";
        abilityHint.style.color = aiming ? Color.white : WithAlpha(Danger, 0.8f);
    }
}
