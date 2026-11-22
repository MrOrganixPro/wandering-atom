using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// The opening story: a black screen where lines fade in one by one, in silence; the music
/// fades back in once it is over. Plays the first time the player presses PLAY (then the run
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
    }

    class StoryBeat
    {
        public readonly List<StoryLine> lines = new List<StoryLine>();
        public bool flash;          // white flash + blast when the beat starts
        public string sfxOnLeave;   // played as the beat fades to black
        public float extraHold;
    }

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

        Beat(Caption("GENEVA  ·  CERN", "CENEVRE  ·  CERN"),
             Line("One ordinary atom, stripped of its " + Hi("electrons", e) + ",",
                  Hi("Elektronları", e) + " sökülmüş sıradan bir atom,"),
             Line("hurled around the ring at " + B("99.9999991%") + " of light speed.",
                  "halkada ışık hızının " + B("%99,9999991") + "'ine kadar hızlandırıldı.")
        ).sfxOnLeave = "charge";

        Beat(Big("Then — impact.", "Ve sonra... çarpışma.")).flash = true;

        Beat(Line("Your core began to " + B("pull") + ".", "Çekirdeğin " + B("çekmeye") + " başladı."),
             Line("Every " + Hi("electron", e) + " made you a new element.",
                  "Her " + Hi("elektron", e) + " seni yeni bir elemente dönüştürdü."),
             Line(Hi("Hydrogen. Helium. Carbon. Neon...", e), Hi("Hidrojen. Helyum. Karbon. Neon...", e)));

        Beat(Line("Stray particles fill the chamber.", "Hazne başıboş parçacıklarla dolu."),
             Line("Your " + Hi("electrons", e) + " are your shield.",
                  Hi("Elektronların", e) + " senin kalkanın."),
             Line("Your " + Hi("stability", Pickup.HeartColor) + " is your life.",
                  Hi("Kararlılığın", Pickup.HeartColor) + " ise canın."));

        Beat(Line("Gather " + Hi("photons", Pickup.Gold) + " to move on.",
                  "İlerlemek için " + Hi("foton", Pickup.Gold) + " topla."),
             Line("Collect " + Hi("quanta", Pickup.CoinColor) + " to evolve.",
                  "Evrimleşmek için " + Hi("kuanta", Pickup.CoinColor) + " biriktir."));

        Beat(Line("They will run this again. And again.", "Bunu yine yapacaklar. Ve yine."),
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
        foreach (var line in b.lines)
        {
            var l = Text(storyBody, line.text, line.size, line.bold, line.color, TextAnchor.MiddleCenter, true);
            l.style.maxWidth = 1000;
            l.style.marginBottom = line.size * 0.45f;
            l.style.letterSpacing = line.spacing;
            l.style.opacity = 0f;
            storyLabels.Add(l);
        }

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

    static float StoryReveal(StoryBeat b) => (b.lines.Count - 1) * StoryLineStagger + StoryLineFade;

    /// <summary>How long a fully shown beat stays before moving on by itself (longer text, longer read).</summary>
    static float StoryHold(StoryBeat b)
    {
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
            else LeaveStoryBeat();
        }
        else storyClock += dt;

        if (!storyLeaving && storyClock >= reveal + StoryHold(b)) LeaveStoryBeat();
        if (storyLeaving && storyClock >= storyLeaveAt + StoryBeatFadeOut + StoryBeatGap)
        {
            if (storyBeat + 1 >= storyBeats.Count)
            {
                gm.EndStory();
                return;
            }
            StartStoryBeat(storyBeat + 1);
        }

        float fadeOut = storyLeaving ? Mathf.Clamp01(1f - (storyClock - storyLeaveAt) / StoryBeatFadeOut) : 1f;
        for (int i = 0; i < storyLabels.Count; i++)
        {
            float a = Mathf.Clamp01((storyClock - i * StoryLineStagger) / StoryLineFade);
            a = a * a * (3f - 2f * a);
            storyLabels[i].style.opacity = a * fadeOut;
            storyLabels[i].style.translate = new Translate(0f, 12f * (1f - a));
        }
    }
}
