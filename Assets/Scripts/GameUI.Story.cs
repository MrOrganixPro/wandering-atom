using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// The opening story: a black screen where lines fade in one by one, in silence, each with a
/// small animated picture above the text (GameUI.StoryArt); the music fades back in once it is
/// over. The crash: "And then" is typed out letter by letter, the screen goes white with the
/// blast, and "It crashed." fades in as the white clears. Plays the first time the player presses PLAY (then the run
/// starts), and again from the title's STORY button. Space / Enter / click shows the whole beat, then moves on; Esc skips it all.
/// </summary>
public partial class GameUI
{
    class StoryLine
    {
        public string text;
        public float size, spacing;
        public bool bold;
        public Color color;
        public bool typed;            // appears letter by letter instead of fading in
        public StoryArt art;          // the picture shown from this line on
        public StoryLine Art(StoryArt a) { art = a; return this; }
        public StoryLine Typed() { typed = true; return this; }
    }

    class StoryBeat
    {
        public readonly List<StoryLine> lines = new List<StoryLine>();
        public bool flash;          // white flash + blast when the beat starts
        public string sfxOnLeave;   // played as the beat fades to black
        public float extraHold;
        public float lineDelay;     // wait before the first line (lets the flash clear first)
        public bool cut;            // no fade to black: cuts straight to the next beat
        public float hold = -1f;    // fixed hold once shown (-1: by text length)
    }

    const float StoryTypeTime = 0.16f;      // seconds per typed letter

    const float StoryLineStagger = 1.15f;   // seconds between the lines of a beat
    const float StoryLineFade = 1.1f;
    const float StoryBeatFadeOut = 0.9f;
    const float StoryBeatGap = 0.5f;        // pure black between beats
    const float StoryLeadIn = 1.6f;         // silence before the first line
    const float StoryCoverIn = 0.8f;        // fading to black (replay from the title)
    const float StoryCoverOut = 1.8f;       // the black lifting off the title at the end

    static readonly Color StoryInk = new Color(1f, 1f, 1f, 0.88f);

    VisualElement storyScreen, storyBody, storyFlash, storyHint;
    Pips storyDots;
    readonly List<Label> storyLabels = new List<Label>();
    List<StoryBeat> storyBeats = new List<StoryBeat>();
    bool storyRunning, storyLeaving, storyClicked;
    int storyBeat = -1;           // -1 = lead-in
    float storyClock, storyLeaveAt, storyLead;
    float storyCover = 0f;        // opacity of the black layer
    float storyFlashAmt, storyShake;

    // ------------------------------------------------------------------ text

    static string Hi(string s, Color c) => "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + s + "</color>";
    static string B(string s) => "<b>" + s + "</b>";

    static StoryLine Line(string en, string tr) =>
        new StoryLine { text = Loc.T(en, tr), size = 30, color = StoryInk };
    static StoryLine Big(string en, string tr) =>
        new StoryLine { text = Loc.T(en, tr), size = 52, bold = true, spacing = 4, color = Color.white };
    static StoryLine Caption(string en, string tr) =>
        new StoryLine { text = Loc.T(en, tr), size = 20, bold = true, spacing = 8, color = Dim };
    static StoryLine Blank() => new StoryLine { text = "", size = 14, color = Color.clear };

    StoryBeat Beat(params StoryLine[] lines)
    {
        var b = new StoryBeat();
        b.lines.AddRange(lines);
        storyBeats.Add(b);
        return b;
    }

    void BuildStoryBeats()
    {
        storyBeats = new List<StoryBeat>();

        Color e = Pickup.OrbColor;

        Beat(Caption("GENEVA  ·  CERN", "CENEVRE  ·  CERN").Art(StoryArt.Ring),
             Line("One ordinary atom, stripped of its " + Hi("electrons", e) + ",",
                  Hi("Elektronları", e) + " sökülmüş sıradan bir atom,").Art(StoryArt.Stripped),
             Line("hurled around the ring at " + B("99.9999991%") + " of light speed.",
                  "halkada ışık hızının " + B("%99,9999991") + "'ine kadar hızlandırıldı.").Art(StoryArt.Racing)
        ).sfxOnLeave = "charge";

        // The crash: typed out slowly, then white, then the line under the fading white.
        var andThen = Beat(Big("And then", "Ve sonra").Typed());
        andThen.cut = true;
        andThen.hold = 0.7f;
        var crash = Beat(Big("It crashed.", "Çarpıştı.").Art(StoryArt.Crash));
        crash.flash = true;
        crash.lineDelay = 1.1f;

        Beat(Line("Your core began to " + B("pull") + " protons and neutrons.",
                  "Çekirdeğin proton ve nötronları " + B("çekmeye") + " başladı.").Art(StoryArt.Nucleons),
             Line("Every " + Hi("electron", e) + " you collected made you a new element.",
                  "Topladığın her " + Hi("elektron", e) + " seni yeni bir elemente dönüştürdü.").Art(StoryArt.Electrons),
             Line(Hi("Hydrogen. Helium. Carbon. Neon...", e), Hi("Hidrojen. Helyum. Karbon. Neon...", e)).Art(StoryArt.Elements));

        Beat(Line("Stray particles fill the chamber.", "Hazne başıboş parçacıklarla dolu.").Art(StoryArt.Stray),
             Line("Your " + Hi("electrons", e) + " are your shield.",
                  Hi("Elektronların", e) + " senin kalkanın.").Art(StoryArt.Shield),
             Line("Your " + Hi("stability", Pickup.HeartColor) + " is your life.",
                  Hi("Kararlılığın", Pickup.HeartColor) + " ise canın.").Art(StoryArt.Stability));

        Beat(Line("Gather " + Hi("photons", Pickup.Gold) + " to move on.",
                  "İlerlemek için " + Hi("foton", Pickup.Gold) + " topla.").Art(StoryArt.Photon),
             Line("Collect " + Hi("quanta", Pickup.CoinColor) + " to evolve.",
                  "Evrimleşmek için " + Hi("kuanta", Pickup.CoinColor) + " biriktir.").Art(StoryArt.Quanta));

        Beat(Line("They will run this experiment again and again,", "Bu deneyi tekrar tekrar yapacaklar,").Art(StoryArt.Loop),
             Line("until you succeed and grow enough.", "sen başarıp yeterince büyüyene kadar.").Art(StoryArt.Grow),
             Blank(),
             Big("Make it count.", "Her birinin hakkını ver.")
        ).extraHold = 1.2f;
    }

    // ----------------------------------------------------------------- build

    void BuildStory()
    {
        BuildStoryBeats();
        storyLabels.Clear();

        storyScreen = Layer(root);
        storyScreen.style.backgroundColor = Color.black;
        storyScreen.RegisterCallback<PointerDownEvent>(ev => { if (ev.button == 0) storyClicked = true; });

        storyBody = Layer(storyScreen);
        storyBody.style.justifyContent = Justify.Center;
        storyBody.style.alignItems = Align.Center;
        storyBody.style.paddingLeft = storyBody.style.paddingRight = 60;

        storyFlash = Layer(storyScreen);
        storyFlash.style.backgroundColor = Color.white;
        storyFlash.style.opacity = 0f;

        // Bottom: progress dots and the keys.
        storyHint = Box(storyScreen);
        storyHint.style.position = Position.Absolute;
        storyHint.style.left = storyHint.style.right = 0;
        storyHint.style.bottom = 26;
        storyHint.style.alignItems = Align.Center;
        storyDots = MakePips(storyHint, ProcSprites.Electron, 9, 5);
        storyDots.row.style.marginBottom = 10;
        var keys = Row(storyHint);
        Keycap(keys, Loc.T("SPACE", "BOŞLUK"), 22);
        Text(keys, Loc.T("continue", "devam"), 15, false, Dim).style.marginLeft = 8;
        Keycap(keys, "ESC", 22).style.marginLeft = 26;
        Text(keys, Loc.T("skip", "geç"), 15, false, Dim).style.marginLeft = 8;

        Show(storyScreen, storyCover > 0f);
        storyScreen.style.opacity = storyCover;
    }

    void StartStoryBeat(int index)
    {
        storyBeat = index;
        storyClock = 0f;
        storyLeaving = false;
        storyBody.Clear();
        storyLabels.Clear();

        StoryBeat b = storyBeats[index];
        bool anyArt = false;
        foreach (var line in b.lines) if (line.art != StoryArt.None) anyArt = true;
        BeginStoryArt(anyArt);
        foreach (var line in b.lines)
        {
            var l = Text(storyBody, line.text, line.size, line.bold, line.color, TextAnchor.MiddleCenter, true);
            l.style.maxWidth = 1000;
            l.style.marginBottom = line.size * 0.45f;
            l.style.letterSpacing = line.spacing;
            l.style.opacity = 0f;
            if (line.typed) l.text = "";
            storyLabels.Add(l);
        }
        storyTyped = 0;

        if (b.flash)
        {
            storyFlashAmt = 1f;
            storyShake = 1f;
            Sfx.Play("explode");
            Sfx.Play("zap", 0.7f);
        }
    }

    void LeaveStoryBeat()
    {
        if (storyLeaving) return;
        storyLeaving = true;
        storyLeaveAt = storyClock;
        string sfx = storyBeats[storyBeat].sfxOnLeave;
        if (!string.IsNullOrEmpty(sfx)) Sfx.Play(sfx, 0.8f);
    }

    /// <summary>When line i starts to appear, and how long it takes (typed lines: letter by letter).</summary>
    static float StoryLineStart(StoryBeat b, int i) => b.lineDelay + i * StoryLineStagger;
    static float StoryLineTime(StoryLine l) => l.typed ? Mathf.Max(1, PlainLength(l.text)) * StoryTypeTime : StoryLineFade;

    static float StoryReveal(StoryBeat b)
    {
        float end = 0f;
        for (int i = 0; i < b.lines.Count; i++) end = Mathf.Max(end, StoryLineStart(b, i) + StoryLineTime(b.lines[i]));
        return end;
    }

    static int PlainLength(string text)
    {
        int n = 0;
        bool tag = false;
        foreach (char c in text)
        {
            if (c == '<') tag = true;
            else if (c == '>') tag = false;
            else if (!tag) n++;
        }
        return n;
    }

    int storyTyped;   // letters of the typed line shown so far (for the tick sound)

    /// <summary>How long a fully shown beat stays before moving on by itself (longer text, longer read).</summary>
    static float StoryHold(StoryBeat b)
    {
        if (b.hold >= 0f) return b.hold;
        int chars = 0;
        foreach (var line in b.lines)
        {
            bool tag = false;
            foreach (char c in line.text)
            {
                if (c == '<') tag = true;
                else if (c == '>') tag = false;
                else if (!tag) chars++;
            }
        }
        return 2.2f + chars * 0.03f + b.extraHold;
    }

    // ---------------------------------------------------------------- update

    void UpdateStory(GameManager.State st)
    {
        if (storyScreen == null) return;
        float dt = Time.unscaledDeltaTime;

        storyFlashAmt = Mathf.MoveTowards(storyFlashAmt, 0f, dt / 1.5f);
        storyShake = Mathf.MoveTowards(storyShake, 0f, dt / 0.9f);
        storyFlash.style.opacity = storyFlashAmt * storyFlashAmt;
        float amp = 16f * storyShake * storyShake;
        storyBody.style.translate = new Translate(Random.Range(-amp, amp), Random.Range(-amp, amp));

        if (st != GameManager.State.Story)
        {
            // Over: the black lifts off the title (whatever text is left fades with it).
            storyRunning = false;
            storyClicked = false;
            storyCover = Mathf.MoveTowards(storyCover, 0f, dt / StoryCoverOut);
            storyScreen.style.opacity = storyCover;
            storyScreen.pickingMode = PickingMode.Ignore;
            Show(storyScreen, storyCover > 0f);
            return;
        }

        if (!storyRunning)
        {
            storyRunning = true;
            storyBeat = -1;
            storyLead = StoryLeadIn;
            storyLeaving = false;
            storyBody.Clear();
            storyLabels.Clear();
        }
        Show(storyScreen, true);
        storyScreen.pickingMode = PickingMode.Position;   // swallow clicks meant for the title
        storyCover = Mathf.MoveTowards(storyCover, 1f, dt / StoryCoverIn);
        storyScreen.style.opacity = storyCover;

        // Input (ignored for a moment so the press that opened the story doesn't skip it).
        var kb = Keyboard.current;
        bool next = storyClicked || (kb != null && (kb.spaceKey.wasPressedThisFrame
                    || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame));
        bool skip = kb != null && kb.escapeKey.wasPressedThisFrame;
        storyClicked = false;
        if (gm.StateTime < 0.5f) next = skip = false;
        if (skip)
        {
            gm.EndStory();
            return;
        }

        storyHint.style.opacity = 0.6f * Mathf.Clamp01((gm.StateTime - 2f) / 1.5f);
        storyDots.Set(storyBeats.Count, storyBeat + 1, WithAlpha(Pickup.OrbColor, 0.9f), WithAlpha(Color.white, 0.15f));

        if (storyCover < 1f) return;   // wait until the screen is fully black

        if (storyBeat < 0)
        {
            storyLead -= dt;
            if (storyLead <= 0f || next) StartStoryBeat(0);
            return;
        }

        StoryBeat b = storyBeats[storyBeat];
        float reveal = StoryReveal(b);
        if (next)
        {
            if (storyLeaving) storyClock = Mathf.Max(storyClock, storyLeaveAt + StoryBeatFadeOut + StoryBeatGap);
            else if (storyClock < reveal) storyClock = reveal;   // show the whole beat now
            else if (b.cut && storyBeat + 1 < storyBeats.Count) { StartStoryBeat(storyBeat + 1); return; }
            else LeaveStoryBeat();
        }
        else storyClock += dt;

        if (!storyLeaving && storyClock >= reveal + StoryHold(b))
        {
            if (b.cut && storyBeat + 1 < storyBeats.Count) { StartStoryBeat(storyBeat + 1); return; }
            LeaveStoryBeat();
        }
        if (storyLeaving && storyClock >= storyLeaveAt + StoryBeatFadeOut + StoryBeatGap)
        {
            if (storyBeat + 1 >= storyBeats.Count)
            {
                gm.EndStory();
                return;
            }
            StartStoryBeat(storyBeat + 1);
            b = storyBeats[storyBeat];   // the rest of this frame works on the new beat
        }

        float fadeOut = storyLeaving ? Mathf.Clamp01(1f - (storyClock - storyLeaveAt) / StoryBeatFadeOut) : 1f;
        StoryArt latest = StoryArt.None;   // the picture of the newest line that has started
        for (int i = 0; i < storyLabels.Count && i < b.lines.Count; i++)
        {
            StoryLine line = b.lines[i];
            float since = storyClock - StoryLineStart(b, i);
            if (line.typed)
            {
                // Letter by letter, each with a soft tick.
                int n = Mathf.Clamp(Mathf.FloorToInt(since / StoryTypeTime) + 1, 0, line.text.Length);
                if (since < 0f) n = 0;
                if (n > storyTyped)
                {
                    if (line.text[n - 1] != ' ') Sfx.Play("lock", 0.3f);
                    storyTyped = n;
                }
                storyLabels[i].text = line.text.Substring(0, n);
                storyLabels[i].style.opacity = fadeOut;
                continue;
            }
            float a = Mathf.Clamp01(since / StoryLineFade);
            a = a * a * (3f - 2f * a);
            storyLabels[i].style.opacity = a * fadeOut;
            // Pictures don't wait for the line delay: the crash tracks burst out with the white.
            if (since + b.lineDelay >= 0f && line.art != StoryArt.None) latest = line.art;
        }
        if (latest != StoryArt.None) ShowStoryArt(latest);
        UpdateStoryArt(dt, fadeOut);
    }
}
