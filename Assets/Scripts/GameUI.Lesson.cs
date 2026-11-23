using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The one-time lessons (the game freezes, the screen sinks into black and the lines fade in one
/// by one): the first filled energy level (+1 quantum per level for the rest of the run, but a
/// bigger pull), and the first noble gas, halogen and alkali metal powers of those characters; and
/// showing off a newly unlocked quantum at the start of the next level (level intro + HUD).
/// </summary>
public partial class GameUI
{
    // The lesson: the frozen game sinks into darkness and the lines fade in one by one, like the story.
    VisualElement lessonScreen, lessonBody, lessonContinue;
    readonly System.Collections.Generic.List<Label> lessonLines = new System.Collections.Generic.List<Label>();
    float lessonFade, lessonClock;
    bool lessonOn, lessonClicked;

    const float LessonDarken = 0.9f;     // seconds to sink into the dark
    const float LessonFirstLine = 0.7f;  // first line after the screen has mostly darkened
    const float LessonStagger = 1.0f;    // between lines
    const float LessonLineFade = 0.8f;

    Pips introQuanta;
    Label introQuantaLabel;
    bool introQuantaSoundDone;

    // ---------------------------------------------------------------- lesson

    void BuildLesson()
    {
        lessonScreen = Layer(root);
        lessonScreen.style.backgroundColor = Color.black;
        lessonScreen.RegisterCallback<PointerDownEvent>(ev => { if (ev.button == 0) lessonClicked = true; });

        lessonBody = Layer(lessonScreen);
        lessonBody.style.justifyContent = Justify.Center;
        lessonBody.style.alignItems = Align.Center;
        lessonBody.style.paddingLeft = lessonBody.style.paddingRight = 60;

        FillLesson(GameManager.LessonKind.Shell);

        // Bottom: the keys, as in the story.
        lessonContinue = Row(lessonScreen);
        lessonContinue.style.position = Position.Absolute;
        lessonContinue.style.left = lessonContinue.style.right = 0;
        lessonContinue.style.bottom = 30;
        lessonContinue.style.justifyContent = Justify.Center;
        lessonContinue.style.alignItems = Align.Center;
        Keycap(lessonContinue, Loc.T("SPACE", "BOŞLUK"), 22);
        Text(lessonContinue, Loc.T("continue", "devam"), 15, false, Dim).style.marginLeft = 8;

        Show(lessonScreen, false);
    }

    int lessonShownVersion = -1;

    /// <summary>The lines of one lesson: a title, then what it does, then the catch.</summary>
    void FillLesson(GameManager.LessonKind kind)
    {
        lessonBody.Clear();
        lessonLines.Clear();
        switch (kind)
        {
            case GameManager.LessonKind.Noble:
            {
                Color g = PendulumController.NobleGold;
                string unbreak = Hi(Loc.T("can't be broken", "kırılamaz"), g);
                AddLessonLine(Loc.T("NOBLE GAS", "SOY GAZ"), 48, true, g, 6).style.marginBottom = 34;
                AddLessonLine(Loc.T("A full shell: your bonds " + unbreak + ".", "Tam kabuk: bağların " + unbreak + "."), 30, false, StoryInk, 0);
                AddLessonLine(Loc.T("Ionizing particles and dust bounce off.", "İyonlaştırıcı parçacıklar ve toz sekip gider."), 30, false, StoryInk, 0).style.marginBottom = 34;
                AddLessonLine(Loc.T("Positrons and black holes still take electrons.", "Pozitronlar ve kara delikler yine elektron alır."), 30, true, Danger, 0);
                AddLessonLine(Loc.T("Gain or lose an electron and it's over.", "Bir elektron al ya da ver, biter."), 30, true, Danger, 0);
                break;
            }
            case GameManager.LessonKind.Halogen:
            {
                Color h = GameManager.HalogenColor;
                AddLessonLine(Loc.T("HALOGEN", "HALOJEN"), 48, true, h, 6).style.marginBottom = 34;
                AddLessonLine(Loc.T("Electrons drift to you, and you grab them from further away.",
                                    "Elektronlar sana sürüklenir, onları daha uzaktan kaparsın."), 30, false, StoryInk, 0);
                AddLessonLine(Loc.T("A free radical that touches you " + Hi("loses its electron", h) + " to you.",
                                    "Sana dokunan serbest radikal " + Hi("elektronunu sana kaptırır", h) + "."), 30, false, StoryInk, 0).style.marginBottom = 34;
                AddLessonLine(Loc.T("Only at 9, 17, 35, 53\u2026 electrons.", "Yalnızca 9, 17, 35, 53\u2026 elektronda."), 30, true, Danger, 0);
                break;
            }
            case GameManager.LessonKind.Alkali:
            {
                Color a = GameManager.AlkaliColor;
                AddLessonLine(Loc.T("ALKALI METAL", "ALKALİ METAL"), 48, true, a, 6).style.marginBottom = 34;
                AddLessonLine(Loc.T("Your outer electron is " + Hi("loose", a) + ".", "Dış elektronun " + Hi("gevşek", a) + "."), 30, false, StoryInk, 0);
                AddLessonLine(Loc.T("Throw it with SPACE or the ability button: it explodes and comes back.",
                                    "BOŞLUK ya da yetenek tuşuyla fırlat: patlar ve geri döner."), 30, false, StoryInk, 0).style.marginBottom = 34;
                AddLessonLine(Loc.T("Only at 3, 11, 19, 37\u2026 electrons.", "Yalnızca 3, 11, 19, 37\u2026 elektronda."), 30, true, Danger, 0);
                break;
            }
            default:
            {
                string q = Hi(Loc.T("+1 quantum", "+1 kuanta"), Pickup.CoinColor);
                AddLessonLine(Loc.T("ENERGY LEVEL COMPLETE", "ENERJİ SEVİYESİ TAMAMLANDI"), 48, true, Pickup.CoinColor, 6).style.marginBottom = 34;
                AddLessonLine(Loc.T(q + " every level.", "Her seviyede " + q + "."), 30, false, StoryInk, 0).style.marginBottom = 34;
                AddLessonLine(Loc.T("A fuller atom pulls harder.", "Dolu atom daha güçlü çeker."), 30, true, Danger, 0);
                AddLessonLine(Loc.T("More particles will come.", "Daha çok parçacık gelir."), 30, true, Danger, 0);
                break;
            }
        }
    }

    Label AddLessonLine(string text, float size, bool bold, Color c, float spacing)
    {
        var l = Text(lessonBody, text, size, bold, c, TextAnchor.MiddleCenter, true);
        l.style.maxWidth = 1000;
        l.style.marginBottom = size * 0.4f;
        l.style.letterSpacing = spacing;
        l.style.opacity = 0f;
        lessonLines.Add(l);
        return l;
    }

    float LessonAllShown => LessonFirstLine + (lessonLines.Count - 1) * LessonStagger + LessonLineFade;

    void UpdateLesson(GameManager.State st)
    {
        if (lessonScreen == null) return;
        float dt = Time.unscaledDeltaTime;
        bool on = gm.Teaching && st == GameManager.State.Playing;
        if (on && (!lessonOn || lessonShownVersion != gm.LessonVersion))
        {
            lessonShownVersion = gm.LessonVersion;
            FillLesson(gm.TeachingKind);
            lessonClock = 0f;   // a fresh lesson
            Sfx.Play("reveal", 0.7f);
        }
        lessonOn = on;

        lessonFade = Mathf.MoveTowards(lessonFade, on ? 1f : 0f, dt / (on ? LessonDarken : 0.6f));
        Show(lessonScreen, lessonFade > 0f);
        lessonScreen.pickingMode = on ? PickingMode.Position : PickingMode.Ignore;
        if (lessonFade <= 0f) { lessonClicked = false; return; }
        float e = lessonFade * lessonFade * (3f - 2f * lessonFade);
        lessonScreen.style.backgroundColor = new Color(0f, 0f, 0f, 0.9f * e);

        if (on)
        {
            // Space / Enter / click: show every line now; once all are shown, carry on playing.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            bool next = lessonClicked || (kb != null && (kb.spaceKey.wasPressedThisFrame
                        || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame));
            if (gm.TeachTime < 0.4f) next = false;   // not the press that was already happening
            if (next)
            {
                if (lessonClock < LessonAllShown) lessonClock = LessonAllShown;
                else gm.EndTeaching();
            }
            else lessonClock += dt;
        }
        lessonClicked = false;

        for (int i = 0; i < lessonLines.Count; i++)
        {
            float a = Mathf.Clamp01((lessonClock - LessonFirstLine - i * LessonStagger) / LessonLineFade);
            a = a * a * (3f - 2f * a);
            lessonLines[i].style.opacity = a * e;
            lessonLines[i].style.translate = new Translate(0f, 12f * (1f - a));
        }
        float hint = Mathf.Clamp01((lessonClock - LessonAllShown) / 0.4f);
        lessonContinue.style.opacity = e * hint * (0.45f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f));
    }

    // ----------------------------------------------------- level intro + HUD

    void BuildIntroQuanta(VisualElement parent)
    {
        var row = Row(parent);
        row.style.marginTop = 14;
        row.style.alignItems = Align.Center;
        row.style.justifyContent = Justify.Center;
        introQuanta = MakePips(row, ProcSprites.Quanta, 30, 6, 500);
        introQuantaLabel = Text(row, "", 22, true, Pickup.CoinColor, TextAnchor.MiddleLeft);
        introQuantaLabel.style.marginLeft = 12;
        introQuantaLabel.style.letterSpacing = 2;
    }

    /// <summary>The level intro lists this level's quanta when new ones were just unlocked; those flash in.</summary>
    void UpdateIntroQuanta(float t, float a)
    {
        int n = gm.LevelQuanta, fresh = gm.NewQuantaThisLevel;
        bool on = fresh > 0;
        Show(introQuanta.row.parent, on);
        if (!on) { introQuantaSoundDone = false; return; }

        const float At = 0.55f;   // the new ones appear after the old ones
        bool shown = t >= At;
        introQuanta.Set(n, shown ? n : n - fresh, Pickup.CoinColor, Color.clear);
        introQuanta.row.parent.style.opacity = a;
        float k = Mathf.Clamp01((t - At) / 0.3f);
        for (int i = 0; i < introQuanta.items.Count; i++)
        {
            bool isNew = i >= n - fresh;
            float sc = isNew && shown ? 1f + 0.8f * (1f - k) * (1f - k) + 0.08f * Mathf.Sin(t * 9f) : 1f;
            introQuanta.items[i].style.scale = new Scale(new Vector3(sc, sc, 1f));
        }
        introQuantaLabel.text = shown ? (fresh == 1 ? Loc.T("+1 QUANTUM", "+1 KUANTA") : Loc.T("+" + fresh + " QUANTA", "+" + fresh + " KUANTA")) : "";
        float ls = 1f + 0.4f * (1f - k) * (1f - k);
        introQuantaLabel.style.scale = new Scale(new Vector3(ls, ls, 1f));
        if (shown && !introQuantaSoundDone)
        {
            introQuantaSoundDone = true;
            Sfx.Play("coin", 0.9f);
        }
        if (!shown) introQuantaSoundDone = false;
    }

}
