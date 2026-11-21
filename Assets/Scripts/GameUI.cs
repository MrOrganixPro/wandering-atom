using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// HUD and menus, built in code with UI Toolkit (no UXML / USS / PanelSettings assets needed).
/// Everything is laid out in a 1280x720 reference space that scales with the screen height.
/// Visual first: counts are shown as rows of icons (pips), menus as big buttons with key hints.
/// Reads its data from GameManager every frame and calls back through its public actions.
/// Created by GameManager.
/// </summary>
[DefaultExecutionOrder(200)]
public partial class GameUI : MonoBehaviour
{
    public const string GameName = "WANDERING ATOM";

    static readonly Color Dim = new Color(1f, 1f, 1f, 0.6f);
    static readonly Color Faint = new Color(1f, 1f, 1f, 0.15f);
    static readonly Color Panel = new Color(0.07f, 0.08f, 0.15f, 0.92f);
    static readonly Color Ink = new Color(0.05f, 0.06f, 0.11f);
    static readonly Color CardGold = new Color(1f, 0.82f, 0.3f);
    static readonly Color Danger = new Color(1f, 0.35f, 0.4f);

    GameManager gm;
    PanelSettings panelSettings;
    GameObject docObject;
    VisualElement root;

    // ------------------------------------------------------------ small widgets

    /// <summary>A row of icons where the first 'filled' are lit (stars, coins, hearts...).</summary>
    class Pips
    {
        public VisualElement row;
        public Sprite sprite;
        public float size, gap;
        public readonly List<VisualElement> items = new List<VisualElement>();
        int shownFilled = -1;
        Color shownOn, shownOff;

        public void Set(int count, int filled, Color on, Color off)
        {
            if (count != items.Count)
            {
                row.Clear();
                items.Clear();
                for (int i = 0; i < count; i++)
                {
                    var e = Icon(row, sprite, size, off);
                    e.style.marginRight = gap;
                    e.style.marginBottom = gap;
                    items.Add(e);
                }
                shownFilled = -1;
            }
            if (filled == shownFilled && on == shownOn && off == shownOff) return;
            shownFilled = filled;
            shownOn = on;
            shownOff = off;
            for (int i = 0; i < items.Count; i++)
                items[i].style.unityBackgroundImageTintColor = i < filled ? on : off;
        }
    }

    class MenuButton
    {
        public VisualElement root;
        public Label label;
        public Color color;
        public bool primary, hover;
    }
    readonly List<MenuButton> buttons = new List<MenuButton>();

    /// <summary>Icons of everything the run owns (pause and game-over screens).</summary>
    class Strip { public VisualElement row; public int sig = -1; }

    // ------------------------------------------------------------------ HUD

    VisualElement hud, shellsRow, speedBox, speedFill, pauseButton;
    Label levelLabel, characterLabel;
    Pips starPips, coinPips, heartPips;
    int shownLevel = -1, shownShellSig = -1;
    GameManager.Character shownCharacter = (GameManager.Character)(-1);

    // Recycler progress (rare upgrades): an icon that fills up, plus a dot per orb.
    class Meter { public VisualElement row, clip; public Pips dots; public int shown = -1, shownMax = -1; }
    Meter heartMeter, coinMeter;

    // Floating world-anchored texts ("ORBITALS FULL", recycling progress).
    static GameUI instance;
    VisualElement floatLayer;
    class Floater { public Label label; public Vector2 world; public float age; }
    readonly List<Floater> floaters = new List<Floater>();
    const float FloatLife = 1.1f;

    // --------------------------------------------------------------- screens

    VisualElement titleScreen, introScreen, pauseScreen, completeScreen, gameOverScreen, upgradeScreen;
    Label titleName, titleBest, introLevel, completeTitle, completeSub, gameOverLevel, gameOverBest;
    VisualElement titleAccent, quitButtonRoot, gameOverButtons, newRunColumn;
    Pips introStars, completeCoins, gameOverStars, upgradePicks;
    MenuButton retryButton;
    Strip pauseStrip, gameOverStrip;


    // ------------------------------------------------------------------ setup

    void Start()
    {
        gm = GetComponent<GameManager>();
        instance = this;

        panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        panelSettings.name = "GameUI Panel";
        panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panelSettings.referenceResolution = new Vector2Int(1280, 720);
        panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        panelSettings.match = 1f;   // scale with height
        panelSettings.sortingOrder = 100;
        // Assets/Resources/GameUITheme.tss just imports Unity's default runtime theme.
        var theme = Resources.Load<ThemeStyleSheet>("GameUITheme");
        if (theme != null) panelSettings.themeStyleSheet = theme;

        // Assign the panel before the document is enabled.
        docObject = new GameObject("GameUI");
        docObject.SetActive(false);
        docObject.transform.SetParent(transform, false);
        var doc = docObject.AddComponent<UIDocument>();
        doc.panelSettings = panelSettings;
        docObject.SetActive(true);

        root = doc.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        root.style.unityFontDefinition = FontDefinition.FromFont(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        BuildAll();
        Loc.Changed += BuildAll;
    }

    /// <summary>(Re)builds every screen; also used when the language changes.</summary>
    void BuildAll()
    {
        root.Clear();
        buttons.Clear();
        floaters.Clear();
        builtOffer = -1;
        shownLevel = -1;
        shownShellSig = -1;
        shownCharacter = (GameManager.Character)(-1);
        shownElement = -999;

        BuildHud();
        BuildTitle();
        BuildIntro();
        BuildPause();
        BuildComplete();
        BuildGameOver();
        BuildUpgrade();
        BuildAnnounce();
        BuildIndex();
        BuildLab();
        BuildChooser();
        BuildLesson();
        BuildStory();   // last: it covers everything
    }

    void OnDestroy()
    {
        Loc.Changed -= BuildAll;
        if (docObject != null) Destroy(docObject);
        if (panelSettings != null) Destroy(panelSettings);
    }

    // --------------------------------------------------------------- helpers

    static VisualElement Box(VisualElement parent)
    {
        var e = new VisualElement { pickingMode = PickingMode.Ignore };
        parent?.Add(e);
        return e;
    }

    /// <summary>Absolutely positioned element covering its parent.</summary>
    static VisualElement Layer(VisualElement parent)
    {
        var e = Box(parent);
        e.style.position = Position.Absolute;
        e.style.left = e.style.right = e.style.top = e.style.bottom = 0;
        return e;
    }

    static VisualElement Row(VisualElement parent)
    {
        var e = Box(parent);
        e.style.flexDirection = FlexDirection.Row;
        e.style.alignItems = Align.Center;
        return e;
    }

    /// <summary>Full-width column whose top sits at topPct% of the screen; children are centered.</summary>
    static VisualElement Column(VisualElement parent, float topPct)
    {
        var e = Box(parent);
        e.style.position = Position.Absolute;
        e.style.left = e.style.right = 0;
        e.style.top = Length.Percent(topPct);
        e.style.alignItems = Align.Center;
        return e;
    }

    static Label Text(VisualElement parent, string text, float size, bool bold, Color color,
        TextAnchor align = TextAnchor.MiddleCenter, bool wrap = false)
    {
        var l = new Label(text) { pickingMode = PickingMode.Ignore };
        l.style.fontSize = size;
        l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
        l.style.color = color;
        l.style.unityTextAlign = align;
        l.style.whiteSpace = wrap ? WhiteSpace.Normal : WhiteSpace.NoWrap;
        l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
        l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
        l.style.textShadow = new TextShadow { offset = new Vector2(2f, 2f), blurRadius = 0f, color = new Color(0f, 0f, 0f, 0.6f) };
        parent?.Add(l);
        return l;
    }

    static VisualElement Icon(VisualElement parent, Sprite sprite, float size, Color tint)
    {
        var e = Box(parent);
        e.style.width = e.style.height = size;
        e.style.flexShrink = 0;
        e.style.backgroundImage = new StyleBackground(sprite);
        e.style.unityBackgroundImageTintColor = tint;
        return e;
    }

    static Pips MakePips(VisualElement parent, Sprite sprite, float size, float gap, float maxWidth = 0f)
    {
        var p = new Pips { row = Row(parent), sprite = sprite, size = size, gap = gap };
        p.row.style.flexWrap = Wrap.Wrap;
        p.row.style.justifyContent = Justify.Center;
        if (maxWidth > 0f) p.row.style.maxWidth = maxWidth;
        return p;
    }

    static void Border(VisualElement e, float width, Color c)
    {
        e.style.borderLeftWidth = e.style.borderRightWidth = e.style.borderTopWidth = e.style.borderBottomWidth = width;
        e.style.borderLeftColor = e.style.borderRightColor = e.style.borderTopColor = e.style.borderBottomColor = c;
    }

    static void Radius(VisualElement e, float r)
    {
        e.style.borderTopLeftRadius = e.style.borderTopRightRadius = r;
        e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
    }

    static void Show(VisualElement e, bool on) => e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

    static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    static float Blink => 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);

    /// <summary>A keyboard key drawn as a little keycap.</summary>
    static VisualElement Keycap(VisualElement parent, string key, float size = 26f, Color? color = null)
    {
        Color c = color ?? Dim;
        var e = Box(parent);
        e.style.minWidth = size;
        e.style.height = size;
        e.style.paddingLeft = e.style.paddingRight = size * 0.25f;
        e.style.justifyContent = Justify.Center;
        e.style.flexShrink = 0;
        Border(e, 1.5f, WithAlpha(c, c.a * 0.8f));
        e.style.borderBottomWidth = 3f;
        Radius(e, size * 0.2f);
        var l = Text(e, key, size * 0.5f, true, c);
        l.style.textShadow = new TextShadow { color = Color.clear };
        return e;
    }

    /// <summary>Big clickable menu button: icon, label and the keyboard key that does the same.</summary>
    MenuButton Button(VisualElement parent, Sprite icon, string label, string key, Color color, bool primary, System.Action onClick)
    {
        var b = new MenuButton { color = color, primary = primary };
        var e = new VisualElement { pickingMode = PickingMode.Position };
        e.style.flexDirection = FlexDirection.Row;
        e.style.alignItems = Align.Center;
        e.style.justifyContent = Justify.Center;
        e.style.height = primary ? 66 : 54;
        e.style.minWidth = primary ? 280 : 230;
        e.style.paddingLeft = e.style.paddingRight = 22;
        e.style.marginTop = e.style.marginBottom = 7;
        e.style.marginLeft = e.style.marginRight = 10;
        Radius(e, 12);
        Border(e, 2.5f, color);
        Color fg = primary ? Ink : color;
        if (icon != null) Icon(e, icon, primary ? 30 : 24, fg).style.marginRight = 12;
        b.label = Text(e, label, primary ? 28 : 22, true, fg);
        if (primary) b.label.style.textShadow = new TextShadow { color = Color.clear };
        if (key != null) Keycap(e, key, 24, WithAlpha(fg, 0.75f)).style.marginLeft = 16;
        e.RegisterCallback<PointerEnterEvent>(_ => b.hover = true);
        e.RegisterCallback<PointerLeaveEvent>(_ => b.hover = false);
        e.RegisterCallback<PointerDownEvent>(ev => { if (ev.button == 0) onClick(); });
        parent.Add(e);
        b.root = e;
        buttons.Add(b);
        return b;
    }

    void UpdateButtons()
    {
        foreach (var b in buttons)
        {
            if (b.root.resolvedStyle.display == DisplayStyle.None) { b.hover = false; continue; }
            Color bg = b.primary ? WithAlpha(Color.Lerp(b.color, Color.white, b.hover ? 0.25f : 0f), 0.95f)
                                 : (b.hover ? WithAlpha(b.color, 0.25f) : WithAlpha(Panel, 0.85f));
            b.root.style.backgroundColor = bg;
            float s = b.hover ? 1.05f : 1f;
            b.root.style.scale = new Scale(new Vector3(s, s, 1f));
        }
    }

    Strip MakeStrip(VisualElement parent)
    {
        var s = new Strip { row = Row(parent) };
        s.row.style.flexWrap = Wrap.Wrap;
        s.row.style.justifyContent = Justify.Center;
        s.row.style.maxWidth = 560;
        return s;
    }

    void UpdateStrip(Strip s)
    {
        var types = (GameManager.UpgradeType[])System.Enum.GetValues(typeof(GameManager.UpgradeType));
        int sig = 17;
        foreach (var u in types) sig = sig * 31 + gm.OwnedCount(u);
        if (sig == s.sig) return;
        s.sig = sig;
        s.row.Clear();
        // Character first, then everything else in card order.
        foreach (var u in types) if (GameManager.IsCharacter(u)) AddStripTile(s.row, u);
        foreach (var u in types) if (!GameManager.IsCharacter(u)) AddStripTile(s.row, u);
    }

    void AddStripTile(VisualElement row, GameManager.UpgradeType u)
    {
        int n = gm.OwnedCount(u);
        if (n <= 0) return;
        Color c = GameManager.UpgradeColor(u);
        var tile = Box(row);
        tile.style.width = tile.style.height = 40;
        tile.style.marginLeft = tile.style.marginRight = tile.style.marginTop = tile.style.marginBottom = 3;
        tile.style.alignItems = Align.Center;
        tile.style.justifyContent = Justify.Center;
        tile.style.backgroundColor = WithAlpha(c, 0.14f);
        Border(tile, GameManager.IsCharacter(u) ? 2f : 1f, WithAlpha(c, GameManager.IsCharacter(u) ? 0.9f : 0.45f));
        Radius(tile, 8);
        Icon(tile, IconFor(u), 26, c);
        if (n > 1)
        {
            var count = Text(tile, n.ToString(), 13, true, Color.white);
            count.style.position = Position.Absolute;
            count.style.right = 3;
            count.style.bottom = 1;
        }
    }

    // ------------------------------------------------------------------ build

    void BuildHud()
    {
        hud = Layer(root);

        // Top-left: level, stars, orbs per orbit (and turrets), coins, recyclers, character.
        var left = Box(hud);
        left.style.position = Position.Absolute;
        left.style.left = 24;
        left.style.top = 16;
        left.style.alignItems = Align.FlexStart;
        levelLabel = Text(left, "", 30, true, Color.white, TextAnchor.UpperLeft);
        levelLabel.style.height = 40;

        starPips = MakePips(left, ProcSprites.Photon, 26, 3, 340);
        starPips.row.style.justifyContent = Justify.FlexStart;

        shellsRow = Row(left);
        shellsRow.style.flexWrap = Wrap.Wrap;
        shellsRow.style.maxWidth = 360;
        shellsRow.style.minHeight = 22;
        shellsRow.style.marginTop = 4;

        coinPips = MakePips(left, ProcSprites.Quanta, 20, 4, 340);
        coinPips.row.style.justifyContent = Justify.FlexStart;
        coinPips.row.style.marginTop = 6;
        BuildHudNeutrinos(left);

        heartMeter = MakeMeter(left, ProcSprites.Stability, Pickup.HeartColor);
        coinMeter = MakeMeter(left, ProcSprites.Quanta, Pickup.CoinColor);

        characterLabel = Text(left, "", 16, true, Color.white, TextAnchor.MiddleLeft);
        characterLabel.style.marginTop = 6;
        characterLabel.style.letterSpacing = 2;

        BuildElementTile();

        floatLayer = Layer(hud);

        // Top-right: health, then the pause button.
        var right = Row(hud);
        right.style.position = Position.Absolute;
        right.style.right = 20;
        right.style.top = 18;
        heartPips = MakePips(right, ProcSprites.Stability, 30, 5);
        heartPips.row.style.flexWrap = Wrap.NoWrap;

        pauseButton = new VisualElement { pickingMode = PickingMode.Position };
        pauseButton.style.width = pauseButton.style.height = 40;
        pauseButton.style.marginLeft = 14;
        pauseButton.style.marginTop = -4;
        pauseButton.style.flexDirection = FlexDirection.Row;
        pauseButton.style.justifyContent = Justify.Center;
        pauseButton.style.alignItems = Align.Center;
        Radius(pauseButton, 8);
        Border(pauseButton, 1.5f, WithAlpha(Color.white, 0.35f));
        pauseButton.style.backgroundColor = WithAlpha(Panel, 0.5f);
        for (int i = 0; i < 2; i++)
        {
            var bar = Box(pauseButton);
            bar.style.width = 5;
            bar.style.height = 16;
            bar.style.marginLeft = bar.style.marginRight = 3;
            bar.style.backgroundColor = WithAlpha(Color.white, 0.8f);
            Radius(bar, 1.5f);
        }
        pauseButton.RegisterCallback<PointerDownEvent>(ev => { if (ev.button == 0) gm.Pause(); });
        right.Add(pauseButton);

        // Bottom-middle: faint speed bar (kept faint so it doesn't hide warnings).
        speedBox = Box(hud);
        speedBox.style.position = Position.Absolute;
        speedBox.style.left = speedBox.style.right = 0;
        speedBox.style.bottom = 20;
        speedBox.style.alignItems = Align.Center;
        var bar2 = Box(speedBox);
        bar2.style.width = 160;
        bar2.style.height = 5;
        bar2.style.backgroundColor = new Color(1f, 1f, 1f, 0.1f);
        Radius(bar2, 2.5f);
        bar2.style.overflow = Overflow.Hidden;
        speedFill = Box(bar2);
        speedFill.style.height = Length.Percent(100);
        speedFill.style.backgroundColor = new Color(1f, 0.75f, 0.4f, 0.4f);
    }

    void BuildTitle()
    {
        titleScreen = Layer(root);
        titleScreen.style.backgroundColor = new Color(0f, 0f, 0.02f, 0.35f);

        var col = Column(titleScreen, 17f);
        titleName = Text(col, GameName, 88, true, Pickup.OrbColor);
        titleName.style.letterSpacing = 10;
        titleAccent = Box(col);
        titleAccent.style.height = 3;
        titleAccent.style.width = 420;
        titleAccent.style.marginTop = 6;
        titleAccent.style.backgroundColor = WithAlpha(Pickup.OrbColor, 0.6f);
        Radius(titleAccent, 1.5f);
        titleBest = Text(col, "", 18, true, Dim);
        titleBest.style.marginTop = 12;
        titleBest.style.letterSpacing = 3;

        var buttonsCol = Column(titleScreen, 45f);
        Button(buttonsCol, ProcSprites.Arrow, Loc.T("PLAY", "OYNA"), Loc.T("SPACE", "BOŞLUK"), Pickup.OrbColor, true, () => gm.NewRun());
        Button(buttonsCol, ProcSprites.Photon, Loc.T("INDEX", "DİZİN"), "I", Pickup.Gold, false, () => OpenIndex());
        Button(buttonsCol, ProcSprites.DataChip, Loc.T("LAB", "LABORATUVAR"), "L", Lab.DataColor, false, () => OpenLab());
        Button(buttonsCol, null, Loc.T("STORY", "HİKÂYE"), null, Dim, false, () => gm.PlayStory());
        quitButtonRoot = Button(buttonsCol, null, Loc.T("QUIT", "ÇIKIŞ"), null, Dim, false, () => gm.QuitApp()).root;

        // Top-right: language switch.
        var langs = Row(titleScreen);
        langs.style.position = Position.Absolute;
        langs.style.right = 24;
        langs.style.top = 20;
        LanguageChip(langs, Language.English, "EN");
        LanguageChip(langs, Language.Turkish, "TR");
        Show(quitButtonRoot, GameManager.CanQuitApp);

        // Bottom: controls and what things do, as icons and keys.
        var legend = Row(titleScreen);
        legend.style.position = Position.Absolute;
        legend.style.left = legend.style.right = 0;
        legend.style.bottom = Length.Percent(7);
        legend.style.justifyContent = Justify.Center;

        var move = LegendGroup(legend);
        foreach (var k in new[] { "W", "A", "S", "D" }) Keycap(move, k).style.marginRight = 3;
        LegendWord(move, Loc.T("/ drag  fly", "/ sürükleyerek uç"));

        var pause = LegendGroup(legend);
        Keycap(pause, "ESC");
        LegendWord(pause, Loc.T("pause", "duraklat"));

        var star = LegendGroup(legend);
        Icon(star, ProcSprites.Photon, 26, Pickup.Gold);
        LegendWord(star, Loc.T("photons: next level", "fotonlar: sonraki seviye"));

        var orb = LegendGroup(legend);
        Icon(orb, ProcSprites.Electron, 24, Pickup.OrbColor);
        LegendWord(orb, Loc.T("electrons: shield", "elektronlar: kalkan"));

        var coin = LegendGroup(legend);
        Icon(coin, ProcSprites.Quanta, 22, Pickup.CoinColor);
        LegendWord(coin, Loc.T("quanta: upgrades", "kuanta: geliştirmeler"));
    }

    /// <summary>Small EN / TR toggle; the active one is filled.</summary>
    void LanguageChip(VisualElement parent, Language lang, string code)
    {
        bool on = Loc.Current == lang;
        var e = new VisualElement { pickingMode = PickingMode.Position };
        e.style.width = 52;
        e.style.height = 34;
        e.style.marginLeft = 6;
        e.style.justifyContent = Justify.Center;
        Radius(e, 8);
        Border(e, 2f, on ? Pickup.OrbColor : WithAlpha(Color.white, 0.3f));
        e.style.backgroundColor = on ? WithAlpha(Pickup.OrbColor, 0.9f) : WithAlpha(Panel, 0.7f);
        var l = Text(e, code, 17, true, on ? Ink : Dim);
        l.style.textShadow = new TextShadow { color = Color.clear };
        e.RegisterCallback<PointerDownEvent>(ev => { if (ev.button == 0) Loc.Current = lang; });
        parent.Add(e);
    }

    static VisualElement LegendGroup(VisualElement parent)
    {
        var g = Row(parent);
        g.style.marginLeft = g.style.marginRight = 18;
        return g;
    }

    static void LegendWord(VisualElement parent, string word)
    {
        Text(parent, word, 17, false, Dim).style.marginLeft = 8;
    }

    void BuildIntro()
    {
        introScreen = Layer(root);
        var col = Column(introScreen, 26f);
        introLevel = Text(col, "", 72, true, Color.white);
        introLevel.style.letterSpacing = 6;
        introStars = MakePips(col, ProcSprites.Photon, 34, 6, 700);
        introStars.row.style.marginTop = 10;
        BuildIntroQuanta(col);
    }

    void BuildPause()
    {
        pauseScreen = Layer(root);
        pauseScreen.style.backgroundColor = new Color(0f, 0f, 0.02f, 0.6f);
        var col = Column(pauseScreen, 16f);
        var title = Text(col, Loc.T("PAUSED", "DURAKLATILDI"), 64, true, Color.white);
        title.style.letterSpacing = 8;
        pauseStrip = MakeStrip(col);
        pauseStrip.row.style.marginTop = 14;
        pauseStrip.row.style.minHeight = 46;

        var buttonsCol = Column(pauseScreen, 50f);
        Button(buttonsCol, ProcSprites.Arrow, Loc.T("RESUME", "DEVAM"), "ESC", Pickup.OrbColor, true, () => gm.Resume());
        Button(buttonsCol, ProcSprites.TurnArrow, Loc.T("RESTART", "YENİDEN BAŞLA"), "R", Color.white, false, () => gm.RetryLevel());
        Button(buttonsCol, ProcSprites.Photon, Loc.T("INDEX", "DİZİN"), "I", Pickup.Gold, false, () => OpenIndex());
        Button(buttonsCol, null, Loc.T("MENU", "MENÜ"), "M", Dim, false, () => gm.QuitToTitle());
    }

    void BuildComplete()
    {
        completeScreen = Layer(root);
        var col = Column(completeScreen, 30f);
        completeTitle = Text(col, "", 72, true, Pickup.Gold);
        completeTitle.style.letterSpacing = 6;
        completeSub = Text(col, Loc.T("COMPLETE", "TAMAMLANDI"), 26, true, Color.white);
        completeSub.style.letterSpacing = 12;
        completeCoins = MakePips(col, ProcSprites.Quanta, 34, 8, 700);
        completeCoins.row.style.marginTop = 20;
        BuildNeutrinoAward(col);
    }

    void BuildGameOver()
    {
        gameOverScreen = Layer(root);
        var col = Column(gameOverScreen, 7f);
        var title = Text(col, Loc.T("GAME OVER", "OYUN BİTTİ"), 72, true, Danger);
        title.style.letterSpacing = 6;
        gameOverLevel = Text(col, "", 28, true, Color.white);
        gameOverLevel.style.marginTop = 6;
        gameOverLevel.style.letterSpacing = 4;
        gameOverStars = MakePips(col, ProcSprites.Photon, 24, 4, 600);
        gameOverStars.row.style.marginTop = 8;
        gameOverBest = Text(col, "", 16, true, Dim);
        gameOverBest.style.marginTop = 8;
        gameOverBest.style.letterSpacing = 3;
        BuildPayout(col);

        // Two choices: replay this level with the run's upgrades, or a fresh run from level 1.
        gameOverButtons = Row(gameOverScreen);
        gameOverButtons.style.position = Position.Absolute;
        gameOverButtons.style.left = gameOverButtons.style.right = 0;
        gameOverButtons.style.top = Length.Percent(60);
        gameOverButtons.style.justifyContent = Justify.Center;
        gameOverButtons.style.alignItems = Align.FlexStart;

        var retryCol = Box(gameOverButtons);
        retryCol.style.alignItems = Align.Center;
        retryCol.style.marginLeft = retryCol.style.marginRight = 16;
        retryButton = Button(retryCol, ProcSprites.TurnArrow, "", "R", Pickup.OrbColor, true, () => gm.RetryLevel());
        gameOverStrip = MakeStrip(retryCol);
        gameOverStrip.row.style.maxWidth = 340;
        gameOverStrip.row.style.marginTop = 4;

        newRunColumn = Box(gameOverButtons);
        newRunColumn.style.alignItems = Align.Center;
        newRunColumn.style.marginLeft = newRunColumn.style.marginRight = 16;
        Button(newRunColumn, ProcSprites.Arrow, Loc.T("LEVEL 1", "SEVİYE 1"), "N", Color.white, false, () => gm.NewRun()).root.style.marginTop = 13;
        Text(newRunColumn, Loc.T("new run", "yeni oyun"), 15, false, Dim).style.marginTop = 4;

        var labCol = Box(gameOverButtons);
        labCol.style.alignItems = Align.Center;
        labCol.style.marginLeft = labCol.style.marginRight = 16;
        Button(labCol, ProcSprites.DataChip, Loc.T("LAB", "LABORATUVAR"), "L", Lab.DataColor, false, () => OpenLab()).root.style.marginTop = 13;
        Text(labCol, Loc.T("spend your data", "verini harca"), 15, false, Dim).style.marginTop = 4;
    }

    // ---------------------------------------------------------------- update

    void LateUpdate()
    {
        if (root == null || gm == null) return;
        var st = gm.CurrentState;

        Show(hud, st != GameManager.State.Title && st != GameManager.State.Story);
        Show(titleScreen, st == GameManager.State.Title);
        Show(introScreen, st == GameManager.State.Playing && gm.LevelTime < 2.5f);
        Show(pauseScreen, st == GameManager.State.Paused);
        Show(completeScreen, st == GameManager.State.LevelComplete);
        Show(gameOverScreen, st == GameManager.State.GameOver && !labOpen && !chooserOpen);
        Show(upgradeScreen, st == GameManager.State.Upgrade);

        if (st != GameManager.State.Title && st != GameManager.State.Story) UpdateHud(st);
        UpdateIndex(st);
        UpdateLab(st);
        UpdateUpgradeWorld(st);
        UpdateAnnounce(st);
        UpdateStory(st);
        UpdateLesson(st);
        UpdateButtons();

        switch (st)
        {
            case GameManager.State.Title:
            {
                float ut = Time.unscaledTime;
                titleName.style.translate = new Translate(0f, 4f * Mathf.Sin(ut * 1.3f));
                titleName.style.color = Color.Lerp(Pickup.OrbColor, Color.white, 0.15f + 0.15f * Mathf.Sin(ut * 2f));
                titleAccent.style.width = 380f + 60f * (0.5f + 0.5f * Mathf.Sin(ut * 1.3f));
                titleBest.text = gm.BestLevel > 1 ? Loc.T("BEST  LEVEL ", "EN YÜKSEK  SEVİYE ") + gm.BestLevel : "";
                break;
            }

            case GameManager.State.Playing:
                if (gm.LevelTime < 2.5f)
                {
                    float t = gm.LevelTime;
                    float a = Mathf.Clamp01(Mathf.Min(t / 0.3f, (2.5f - t) / 0.6f));
                    introLevel.text = Loc.T("LEVEL ", "SEVİYE ") + gm.Level;
                    introLevel.style.opacity = a;
                    introStars.Set(gm.StarsNeeded, gm.StarsNeeded, Pickup.Gold, Pickup.Gold);
                    introStars.row.style.opacity = a;
                    UpdateIntroQuanta(t, a);
                    float pop = 1f + 0.25f * Mathf.Clamp01(1f - t / 0.25f);
                    introLevel.style.scale = new Scale(new Vector3(pop, pop, 1f));
                }
                break;

            case GameManager.State.Paused:
                UpdateStrip(pauseStrip);
                break;

            case GameManager.State.LevelComplete:
            {
                float a = Mathf.Clamp01(gm.StateTime / 0.3f);
                completeScreen.style.backgroundColor = new Color(0f, 0f, 0.02f, 0.3f * a);
                completeTitle.text = Loc.T("LEVEL ", "SEVİYE ") + gm.Level;
                completeTitle.style.opacity = a;
                completeSub.style.opacity = a;
                int coins = gm.LevelCoins;
                // Coins pop in one by one.
                int shown = Mathf.Clamp(Mathf.FloorToInt((gm.StateTime - 0.4f) / 0.25f) + 1, 0, coins);
                completeCoins.Set(coins, shown, Pickup.CoinColor, Color.clear);
                UpdateNeutrinoAward(coins);
                break;
            }

            case GameManager.State.GameOver:
            {
                float a = Mathf.Clamp01(gm.StateTime / 0.5f);
                gameOverScreen.style.backgroundColor = new Color(0.03f, 0f, 0.02f, 0.6f * a);
                gameOverScreen.style.opacity = a;
                gameOverLevel.text = Loc.T("LEVEL ", "SEVİYE ") + gm.Level;
                gameOverStars.Set(gm.StarsNeeded, gm.StarsCollected, Pickup.Gold, Faint);
                gameOverBest.text = Loc.T("BEST  LEVEL ", "EN YÜKSEK  SEVİYE ") + gm.BestLevel;
                retryButton.label.text = Loc.T("LEVEL ", "SEVİYE ") + gm.Level;
                UpdatePayout();
                UpdateStrip(gameOverStrip);
                Show(gameOverButtons, gm.StateTime > 0.8f);
                Show(newRunColumn, gm.Level > 1);
                break;
            }

            case GameManager.State.Upgrade:
            {
                // Picks still waiting (the one in the atom is already spent).
                bool refund = gm.ChoiceIndex >= gm.CoinChoices && gm.RefundTotal > 0;
                Color rc = GameManager.UpgradeColor(GameManager.CharacterCard(gm.ActiveCharacter));
                if (refund)
                    upgradePicks.Set(gm.RefundTotal, gm.RefundTotal - (gm.ChoiceIndex - gm.CoinChoices) - 1, rc, Faint);
                else
                    upgradePicks.Set(gm.CoinChoices, gm.CoinChoices - gm.ChoiceIndex - 1, Pickup.CoinColor, Faint);
                UpdateAtomPicker();
                break;
            }
        }
    }

    // ------------------------------------------------------------ element tile

    static readonly Color RadiumGlow = new Color(0.6f, 1f, 0.45f);

    VisualElement elementTile;
    Label elementNumber, elementSymbol, elementName, elementCharge;
    int shownElement = -999;
    float elementPop;

    /// <summary>Periodic-table tile (top center): the element your electron count makes you.</summary>
    void BuildElementTile()
    {
        elementTile = Box(hud);
        elementTile.style.position = Position.Absolute;
        elementTile.style.top = 12;
        elementTile.style.left = Length.Percent(50);
        elementTile.style.marginLeft = -40;
        elementTile.style.width = 80;
        elementTile.style.height = 88;
        elementTile.style.alignItems = Align.Center;
        elementTile.style.backgroundColor = WithAlpha(Panel, 0.85f);
        Radius(elementTile, 8);
        elementNumber = Text(elementTile, "", 13, true, Dim, TextAnchor.UpperLeft);
        elementNumber.style.position = Position.Absolute;
        elementNumber.style.left = 7;
        elementNumber.style.top = 4;
        elementCharge = Text(elementTile, "", 18, true, Color.white, TextAnchor.UpperRight);
        elementCharge.style.position = Position.Absolute;
        elementCharge.style.right = 7;
        elementCharge.style.top = 2;
        elementSymbol = Text(elementTile, "", 38, true, Color.white);
        elementSymbol.style.marginTop = 16;
        elementSymbol.style.height = 44;
        elementName = Text(elementTile, "", 11, true, Dim);
        elementName.style.marginTop = 2;
    }

    void UpdateElementTile()
    {
        PendulumController pc = gm.Pendulum;
        int electrons = pc != null ? pc.BallCount + pc.TurretCount : 0;
        bool radium = gm.ActiveCharacter == GameManager.Character.Spaceship;
        bool ion = gm.ActiveCharacter == GameManager.Character.Challenger || (!radium && electrons == 0);
        int key = radium ? 88 : ion ? 0 : electrons;

        if (key != shownElement)
        {
            bool gained = key > shownElement && shownElement != -999;
            shownElement = key;
            elementPop = 1f;
            bool noble = !radium && Elements.IsNoble(key);   // a full shell
            Color accent = radium ? RadiumGlow : ion ? Danger : noble ? Pickup.Gold : Pickup.OrbColor;
            elementNumber.text = radium ? "88" : (ion ? 1 : key).ToString();
            elementSymbol.text = radium ? "Ra" : Elements.Symbol(ion ? 1 : key);
            elementCharge.text = ion ? "+" : "";
            elementCharge.style.color = accent;
            elementName.text = radium ? Loc.T("Radium", "Radyum") : Elements.Name(key);
            elementSymbol.style.color = accent;
            Border(elementTile, noble || radium ? 2.5f : 1.5f, WithAlpha(accent, noble || radium ? 0.95f : 0.55f));
            if (gained && noble && pc != null)   // shell completed: celebrate the noble gas
                FloatText(pc.PivotPosition + Vector2.up * 1.2f, elementName.text.ToUpperInvariant(), Pickup.Gold, 26f);
        }
        elementPop = Mathf.MoveTowards(elementPop, 0f, Time.unscaledDeltaTime * 4f);
        float s = 1f + 0.25f * elementPop * elementPop;
        elementTile.style.scale = new Scale(new Vector3(s, s, 1f));
    }

    /// <summary>HUD row: an icon that fills from the bottom, plus a dot per recycled orb.</summary>
    Meter MakeMeter(VisualElement parent, Sprite sprite, Color color)
    {
        var m = new Meter { row = Row(parent) };
        m.row.style.height = 28;
        m.row.style.marginTop = 4;
        var holder = Box(m.row);
        holder.style.width = holder.style.height = 24;
        holder.style.flexShrink = 0;
        var empty = Icon(holder, sprite, 24, new Color(1f, 1f, 1f, 0.18f));
        empty.style.position = Position.Absolute;
        m.clip = Box(holder);
        m.clip.style.position = Position.Absolute;
        m.clip.style.left = 0;
        m.clip.style.bottom = 0;
        m.clip.style.width = 24;
        m.clip.style.overflow = Overflow.Hidden;
        var full = Icon(m.clip, sprite, 24, color);
        full.style.position = Position.Absolute;
        full.style.left = 0;
        full.style.bottom = 0;
        m.dots = MakePips(m.row, ProcSprites.Ball, 9, 3);
        m.dots.row.style.marginLeft = 8;
        m.dots.row.style.flexWrap = Wrap.NoWrap;
        Show(m.row, false);
        return m;
    }

    static void UpdateMeter(Meter m, bool owned, int progress, int max, Color color)
    {
        Show(m.row, owned);
        if (!owned || (m.shown == progress && m.shownMax == max)) return;
        m.shown = progress;
        m.shownMax = max;
        m.clip.style.height = 24f * Mathf.Clamp01(progress / (float)Mathf.Max(1, max));
        m.dots.Set(max, progress, color, Faint);
    }

    /// <summary>Short text that pops up at a world position, rises a little and fades.</summary>
    public static void FloatText(Vector2 world, string text, Color color, float size = 20f)
    {
        if (instance == null || instance.floatLayer == null) return;
        var l = Text(instance.floatLayer, text, size, true, color);
        l.style.position = Position.Absolute;
        l.style.width = 300;
        l.style.height = 30;
        instance.floaters.Add(new Floater { label = l, world = world });
        instance.PlaceFloater(instance.floaters[instance.floaters.Count - 1]);
    }

    void PlaceFloater(Floater f)
    {
        var cam = Camera.main;
        if (cam == null || root.panel == null) return;
        Vector2 w = f.world + Vector2.up * (0.6f + f.age * 0.8f);
        Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, w, cam);
        f.label.style.left = p.x - 150f;
        f.label.style.top = p.y - 15f;
        float pop = f.age < 0.12f ? 1f + 0.35f * (1f - f.age / 0.12f) : 1f;
        f.label.style.scale = new Scale(Vector3.one * pop);
        f.label.style.opacity = Mathf.Clamp01((FloatLife - f.age) / 0.35f);
    }

    void UpdateFloaters()
    {
        for (int i = floaters.Count - 1; i >= 0; i--)
        {
            var f = floaters[i];
            f.age += Time.unscaledDeltaTime;
            if (f.age >= FloatLife || gm.CurrentState == GameManager.State.Title)
            {
                f.label.RemoveFromHierarchy();
                floaters.RemoveAt(i);
                continue;
            }
            PlaceFloater(f);
        }
    }

    void UpdateHud(GameManager.State st)
    {
        UpdateFloaters();
        UpdateMeter(heartMeter, gm.HasHealthRecycler, gm.HeartRecycleProgress, gm.OrbsPerRecycledHeart, Pickup.HeartColor);
        UpdateMeter(coinMeter, gm.HasCoinRecycler, gm.CoinRecycleProgress, gm.OrbsPerRecycledCoin, Pickup.CoinColor);
        Show(pauseButton, st == GameManager.State.Playing);

        UpdateElementTile();
        UpdateHudNeutrinos();

        if (shownLevel != gm.Level) { shownLevel = gm.Level; levelLabel.text = Loc.T("LEVEL ", "SEVİYE ") + shownLevel; }
        starPips.Set(gm.StarsNeeded, gm.StarsCollected, Pickup.Gold, Faint);
        coinPips.Set(Mathf.Max(gm.LevelQuanta, gm.LevelCoins), gm.LevelCoins, Pickup.CoinColor, Faint);
        GlowNewQuantaPips();
        heartPips.Set(gm.MaxHealth, gm.Lives, Pickup.HeartColor, Faint);

        if (shownCharacter != gm.ActiveCharacter)
        {
            shownCharacter = gm.ActiveCharacter;
            GameManager.UpgradeType card = GameManager.CharacterCard(shownCharacter);
            Show(characterLabel, shownCharacter != GameManager.Character.None);
            characterLabel.text = GameManager.UpgradeTitle(card);
            characterLabel.style.color = GameManager.UpgradeColor(card);
        }

        PendulumController pc = gm.Pendulum;
        if (pc == null) return;

        // Turrets (Spaceship), then one dot per orb slot, grouped by orbit.
        int sig = pc.ShellTotal * 7 + pc.TurretSlots * 1009 + pc.TurretCount * 31337;
        for (int i = 0; i < pc.ShellTotal; i++) sig = sig * 31 + pc.ShellCount(i) * 97 + pc.ShellCapacity(i);
        if (sig != shownShellSig)
        {
            shownShellSig = sig;
            shellsRow.Clear();
            Color turret = GameManager.UpgradeColor(GameManager.UpgradeType.Spaceship);
            for (int t = 0; t < pc.TurretSlots; t++)
                Icon(shellsRow, ProcSprites.Trefoil, 16, t < pc.TurretCount ? turret : Faint).style.marginRight = 3;
            for (int s = 0; s < pc.ShellTotal; s++)
            {
                var group = Row(shellsRow);
                group.style.flexWrap = Wrap.Wrap;
                group.style.marginBottom = 3;
                if (s > 0 || pc.TurretSlots > 0) group.style.marginLeft = 10;
                Color c = pc.ShellColor(s);
                for (int i = 0; i < pc.ShellCapacity(s); i++)
                    Icon(group, ProcSprites.Ball, 12, i < pc.ShellCount(s) ? c : Faint).style.marginRight = 2;
            }
            Show(shellsRow, pc.ShellTotal > 0 || pc.TurretSlots > 0);
        }

        Show(speedBox, st == GameManager.State.Playing);
        speedFill.style.width = Length.Percent(100f * Mathf.Clamp01(pc.Speed / Mathf.Max(0.01f, pc.MaxSpeed)));
    }
}
