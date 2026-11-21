using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// The opening story: a black screen where lines fade in one by one, in silence; the music
/// only begins once it is over. Plays on the very first launch, and again from the title's
/// STORY button. Space / Enter / click shows the whole beat, then moves on; Esc skips it all.
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
    static readonly Color NucleusColor = new Color(1f, 0.62f, 0.42f);

    VisualElement storyScreen, storyBody, storyFlash, storyHint;
    Pips storyDots;
    readonly List<Label> storyLabels = new List<Label>();
    List<StoryBeat> storyBeats = new List<StoryBeat>();
    bool storyRunning, storyLeaving, storyClicked;
    int storyBeat = -1;           // -1 = lead-in
    float storyClock, storyLeaveAt, storyLead;
    float storyCover = 1f;        // opacity of the black layer: starts black for the first launch
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

        var sp = FindFirstObjectByType<EnemySpawner>();
        Color radical = sp != null ? sp.shardColor : new Color(1f, 0.45f, 0.25f);
        Color alpha = sp != null ? sp.bruteColor : new Color(0.85f, 0.25f, 0.6f);
        Color positron = sp != null ? sp.missileColor : new Color(0.55f, 1f, 0.35f);
        Color gamma = sp != null ? sp.flashColor : new Color(1f, 0.95f, 0.3f);
        Color isotope = sp != null ? sp.brokenColor : new Color(0.7f, 0.55f, 1f);
        Color e = Pickup.OrbColor;

        Beat(Caption("GENEVA  ·  CERN", "CENEVRE  ·  CERN"),
             Line("One hundred metres beneath the earth.", "Yerin yüz metre altında."));

        Beat(Line("Twenty-seven kilometres of ring.", "Yirmi yedi kilometrelik bir halka."),
             Line("A vacuum emptier than outer space.", "Uzayın kendisinden bile boş bir vakum."),
             Blank(),
             Big("And you.", "Ve sen."));

        Beat(Line("One ordinary atom,", "Sıradan bir atom."),
             Line("drafted for an experiment", "Kimsenin tam olarak anlamadığı"),
             Line("no one fully understood.", "bir deney için seçilmiş."));

        Beat(Line("They stripped away your " + Hi("electrons", e) + ".",
                  Hi("Elektronlarını", e) + " tek tek söküp aldılar."),
             Line("They hurled you around the ring", "Seni halkanın içinde,"),
             Line("at " + B("99.9999991%") + " of the speed of light.",
                  "ışık hızının " + B("%99,9999991") + "'iyle fırlattılar.")
        ).sfxOnLeave = "charge";

        Beat(Big("Then — impact.", "Sonra... çarpışma.")).flash = true;

        Beat(Line("The readings made no sense.", "Ölçümler hiçbir anlam ifade etmiyordu."),
             Line("Something had gone wrong.", "Bir şeyler ters gitmişti."),
             Line("Or terribly right.", "Ya da korkunç derecede yolunda."));

        Beat(Line("Your core began to " + B("pull") + ".", "Çekirdeğin " + B("çekmeye") + " başladı."),
             Line(Hi("Protons", NucleusColor) + ". " + Hi("Neutrons", NucleusColor) + ".",
                  Hi("Protonları", NucleusColor) + ". " + Hi("Nötronları", NucleusColor) + "."),
             Line("Anything that came too close.", "Fazla yaklaşan her şeyi."));

        Beat(Line("And with every " + Hi("electron", e) + " you caught,",
                  "Ve yakaladığın her " + Hi("elektronla", e)),
             Line("your core grew heavier.", "çekirdeğin daha da ağırlaştı."),
             Line(Hi("Hydrogen. Helium. Carbon. Neon...", e), Hi("Hidrojen. Helyum. Karbon. Neon...", e)),
             Line("A new element, every time.", "Her seferinde yeni bir element."));

        Beat(Line("But you are not alone down here.", "Ama burada yalnız değilsin."),
             Line("The collision tore the chamber open", "Çarpışma deney alanını"),
             Line("and filled it with stray particles.", "başıboş parçacıklarla doldurdu."));

        Beat(Line(Hi("Free radicals", radical) + ", desperate to bond.",
                  "Bağ kurmak için çırpınan " + Hi("serbest radikaller", radical) + "."),
             Line(Hi("Alpha particles", alpha) + ", heavy and brutal.",
                  "Ağır ve acımasız " + Hi("alfa parçacıkları", alpha) + "."),
             Line(Hi("Positrons", positron) + " that hunt your electrons",
                  "Elektronlarının peşini hiç bırakmayan"),
             Line("and annihilate them on contact.",
                  "ve dokunduğunu yok eden " + Hi("pozitronlar", positron) + "."));

        Beat(Line(Hi("Gamma rays", gamma) + " that strike at the speed of light.",
                  "Işık hızında vuran " + Hi("gama ışınları", gamma) + "."),
             Line(Hi("Unstable isotopes", isotope) + ", decaying in fury.",
                  "Öfkeyle bozunan " + Hi("kararsız izotoplar", isotope) + "."),
             Line(Hi("Dust grains", RockColor) + " the size of mountains.",
                  "Dağ büyüklüğünde " + Hi("toz taneleri", RockColor) + "."));

        Beat(Line("And the one thing everyone feared", "Ve herkesin korktuğu o şey;"),
             Line("this machine might someday create...", "bu makinenin bir gün yaratabileceği..."),
             Blank(),
             Big(Hi("A black hole.", WellColor), Hi("Bir kara delik.", WellColor)));

        Beat(Line("Your " + Hi("electrons", e) + " are your shield.",
                  Hi("Elektronların", e) + " kalkanındır."),
             Line("Your " + Hi("stability", Pickup.HeartColor) + " is your life.",
                  Hi("Kararlılığın", Pickup.HeartColor) + " hayatındır."),
             Line("Lose it all, and you come apart.", "Hepsini kaybedersen, parçalanırsın."));

        Beat(Line("Gather " + Hi("photons", Pickup.Gold) + " to break into the next sector.",
                  "Bir sonraki bölgeye geçmek için " + Hi("fotonları", Pickup.Gold) + " topla."),
             Line("Collect " + Hi("quanta", Pickup.CoinColor) + ", and evolve.",
                  Hi("Kuantaları", Pickup.CoinColor) + " topla ve evril."),
             Line("Some say an atom can even become", "Derler ki bir atom,"),
             Line("something else entirely...", "bambaşka bir şeye bile dönüşebilir..."));

        Beat(Line("They built this machine", "Bu makineyi,"),
             Line("to find the smallest things in the universe.", "evrenin en küçük şeylerini bulmak için kurdular."),
             Blank(),
             Big("Show them the biggest.", "Onlara en büyüğünü göster.")
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
