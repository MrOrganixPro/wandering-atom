using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Big warning banner for level events (Gamma Rain): slams in, blinks with hazard bars on both
/// sides for its duration, then fades. Only counts down while playing (freezes when paused).
/// </summary>
public partial class GameUI
{
    VisualElement announce, announceBarL, announceBarR;
    Label announceTitle;
    float announceAge = 99f, announceLife = 1f;
    bool announceBeeps;

    /// <summary>
    /// Shows a banner for 'duration' seconds of play. With no 'sound' it is a warning that beeps
    /// every half second; otherwise that sound plays once ("" = silent).
    /// </summary>
    public static void Announce(string title, Color color, float duration, string sound = null)
    {
        if (instance == null || instance.announce == null) return;
        instance.ShowAnnounce(title, color, duration, sound);
    }

    void BuildAnnounce()
    {
        announce = Column(root, 24f);
        var row = Row(announce);
        announceBarL = HazardBar(row);
        announceTitle = Text(row, "", 64, true, Color.white);
        announceTitle.style.letterSpacing = 8;
        announceTitle.style.marginLeft = announceTitle.style.marginRight = 22;
        announceBarR = HazardBar(row);
        Show(announce, false);
    }

    static VisualElement HazardBar(VisualElement parent)
    {
        var bar = Row(parent);
        for (int i = 0; i < 3; i++)
        {
            var chevron = Box(bar);
            chevron.style.width = 12;
            chevron.style.height = 40;
            chevron.style.marginLeft = chevron.style.marginRight = 4;
            chevron.style.rotate = new Rotate(20f);
            Radius(chevron, 2);
        }
        return bar;
    }

    void ShowAnnounce(string title, Color color, float duration, string sound)
    {
        announceTitle.text = title;
        announceTitle.style.color = color;
        foreach (var bar in new[] { announceBarL, announceBarR })
            foreach (var chevron in bar.Children()) chevron.style.backgroundColor = color;
        announceLife = Mathf.Max(0.5f, duration);
        announceAge = 0f;
        announceBeeps = sound == null;
        if (announceBeeps) Sfx.Play("warn");
        else if (sound != "") Sfx.Play(sound);
    }

    void UpdateAnnounce(GameManager.State st)
    {
        if (announce == null) return;
        if (st == GameManager.State.Playing || st == GameManager.State.Dying)
        {
            float before = announceAge;
            announceAge += Time.unscaledDeltaTime;
            // A warning beep every half second while it's up.
            if (announceBeeps && announceAge < announceLife && Mathf.Floor(before / 0.5f) != Mathf.Floor(announceAge / 0.5f))
                Sfx.Play("warn", 0.7f);
        }
        else if (st != GameManager.State.Paused) announceAge = 99f;   // level over / menus: drop it

        bool on = announceAge < announceLife + 0.4f;
        Show(announce, on);
        if (!on) return;

        float slam = Mathf.Clamp01(announceAge / 0.18f);
        float s = Mathf.Lerp(1.6f, 1f, slam);
        float fade = Mathf.Clamp01((announceLife + 0.4f - announceAge) / 0.4f);
        bool blink = Mathf.Repeat(announceAge * 4f, 1f) < 0.7f;
        announce.style.opacity = slam * fade * (blink || announceAge > announceLife ? 1f : 0.45f);
        announce.style.scale = new Scale(new Vector3(s, s, 1f));
        float slide = Mathf.Repeat(announceAge * 40f, 20f);
        announceBarL.style.translate = new Translate(-slide, 0f);
        announceBarR.style.translate = new Translate(slide, 0f);
    }
}
