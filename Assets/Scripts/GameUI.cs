using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// HUD and menus, built in code with UI Toolkit (no UXML / USS / PanelSettings assets needed).
/// Everything is laid out in a 1280x720 reference space that scales with the screen height.
/// Reads its data from GameManager every frame; the only thing it calls back is ChooseUpgrade.
/// Created by GameManager.
/// </summary>
[DefaultExecutionOrder(200)]
public class GameUI : MonoBehaviour
{
    static readonly Color Dim = new Color(1f, 1f, 1f, 0.7f);
    static readonly Color CardGold = new Color(1f, 0.82f, 0.3f);

    GameManager gm;
    PanelSettings panelSettings;
    GameObject docObject;
    VisualElement root;

    // HUD
    VisualElement hud, shellsRow, heartsRow, speedBox, speedFill;
    Label levelLabel, starsLabel, shellsLabel, coinsLabel, characterLabel, bestLabel, speedLabel;
    readonly List<VisualElement> hearts = new List<VisualElement>();

    // Screens
    VisualElement titleScreen, introScreen, pauseScreen, completeScreen, gameOverScreen, upgradeScreen;
    VisualElement completeBand, gameOverBand, cardRow;
    Label titleStart, titleRestart, introLevel, introStars;
    Label completeTitle, completeNext, gameOverTitle, gameOverInfo, gameOverRetry;
    Label upgradeCounter, upgradeHint;
    VisualElement upgradeCounterIcon;

    class Card
    {
        public VisualElement root, inner, sealedPart, openPart, flash;
        public Label bigQuestion;
        public Color color;
        public bool hover, mystery, rare;
        public VisualElement ribbon, shimmer;
    }
    readonly List<Card> cards = new List<Card>();
    int builtOffer = -1;
    bool revealPending;   // a Character box is sealed: play the reveal sound when it opens

    // Change detection so text isn't rebuilt every frame.
    int shownLevel = -1, shownStars = -1, shownNeeded = -1, shownCoins = -1, shownCoinMax = -1, shownBest = -1;
    int shownShellSig = -1, shownSpeed = -1, shownLives = -1;
    GameManager.Character shownCharacter = (GameManager.Character)(-1);

    // ------------------------------------------------------------------ setup

    void Start()
    {
        gm = GetComponent<GameManager>();

        panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        panelSettings.name = "GameUI Panel";
        panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panelSettings.referenceResolution = new Vector2Int(1280, 720);
        panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        panelSettings.match = 1f;   // scale with height, like the old OnGUI HUD
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

        BuildHud();
        BuildTitle();
        BuildIntro();
        BuildPause();
        BuildComplete();
        BuildGameOver();
        BuildUpgrade();
    }

    void OnDestroy()
    {
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

    /// <summary>Full-width horizontal dark band (top and height in % of the screen).</summary>
    static VisualElement Band(VisualElement parent, float topPct, float heightPct, float alpha)
    {
        var e = Box(parent);
        e.style.position = Position.Absolute;
        e.style.left = e.style.right = 0;
        e.style.top = Length.Percent(topPct);
        e.style.height = Length.Percent(heightPct);
        e.style.backgroundColor = new Color(0f, 0f, 0f, alpha);
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

    /// <summary>Full-width centered line at topPct% of the screen, pushed down by offset (reference px).</summary>
    static Label Line(VisualElement parent, string text, float topPct, float offset, float height, float size, bool bold, Color color)
    {
        var l = Text(parent, text, size, bold, color);
        l.style.position = Position.Absolute;
        l.style.left = l.style.right = 0;
        l.style.top = Length.Percent(topPct);
        l.style.marginTop = offset;
        l.style.height = height;
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

    static void Border(VisualElement e, float width, Color c)
    {
        e.style.borderLeftWidth = e.style.borderRightWidth = e.style.borderTopWidth = e.style.borderBottomWidth = width;
        e.style.borderLeftColor = e.style.borderRightColor = e.style.borderTopColor = e.style.borderBottomColor = c;
    }

    static void Show(VisualElement e, bool on) => e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

    static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    static float Blink => 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);

    // ------------------------------------------------------------------ build

    void BuildHud()
    {
        hud = Layer(root);

        // Top-left: level, stars, balls per shell (and turrets), coins, character.
        var left = Box(hud);
        left.style.position = Position.Absolute;
        left.style.left = 24;
        left.style.top = 14;
        levelLabel = Text(left, "", 34, true, Color.white, TextAnchor.UpperLeft);
        levelLabel.style.height = 46;

        var starRow = Row(left);
        starRow.style.height = 36;
        Icon(starRow, ProcSprites.Star, 30, Pickup.Gold);
        starsLabel = Text(starRow, "", 24, true, Pickup.Gold, TextAnchor.MiddleLeft);
        starsLabel.style.marginLeft = 6;

        shellsRow = Row(left);
        shellsRow.style.height = 30;
        Icon(shellsRow, ProcSprites.Ball, 24, Pickup.OrbColor).style.marginLeft = 3;
        shellsLabel = Text(shellsRow, "", 18, false, Pickup.OrbColor, TextAnchor.MiddleLeft);
        shellsLabel.style.marginLeft = 9;

        var coinRow = Row(left);
        coinRow.style.height = 30;
        Icon(coinRow, ProcSprites.Coin, 24, Pickup.CoinColor).style.marginLeft = 3;
        coinsLabel = Text(coinRow, "", 18, false, Pickup.CoinColor, TextAnchor.MiddleLeft);
        coinsLabel.style.marginLeft = 9;

        characterLabel = Text(left, "", 18, false, Color.white, TextAnchor.MiddleLeft);
        characterLabel.style.height = 26;
        characterLabel.style.marginLeft = 3;

        // Top-right: best level and health.
        var right = Box(hud);
        right.style.position = Position.Absolute;
        right.style.right = 24;
        right.style.top = 20;
        right.style.alignItems = Align.FlexEnd;
        bestLabel = Text(right, "", 18, false, Dim, TextAnchor.UpperRight);
        heartsRow = Row(right);
        heartsRow.style.marginTop = 10;

        // Bottom-middle: faint speed readout (kept faint so it doesn't hide warnings).
        speedBox = Box(hud);
        speedBox.style.position = Position.Absolute;
        speedBox.style.left = speedBox.style.right = 0;
        speedBox.style.bottom = 18;
        speedBox.style.alignItems = Align.Center;
        speedLabel = Text(speedBox, "", 14, false, new Color(1f, 1f, 1f, 0.35f));
        speedLabel.style.height = 24;
        var bar = Box(speedBox);
        bar.style.width = 140;
        bar.style.height = 4;
        bar.style.backgroundColor = new Color(1f, 1f, 1f, 0.1f);
        speedFill = Box(bar);
        speedFill.style.height = Length.Percent(100);
        speedFill.style.backgroundColor = new Color(1f, 0.75f, 0.4f, 0.35f);
    }

    void BuildTitle()
    {
        titleScreen = Layer(root);
        Band(titleScreen, 14f, 72f, 0.35f);
        Line(titleScreen, "PENDULUM", 18f, 0f, 90f, 72f, true, Pickup.OrbColor);
        Line(titleScreen, "You are the thruster. Collect the stars, survive the swarm.", 18f, 80f, 30f, 18f, false, Dim);
        Line(titleScreen, "Drag from the thruster  /  WASD  /  Arrow keys   —  fly", 40f, 0f, 30f, 18f, false, Color.white);
        Line(titleScreen, "Blue orbs add orbiting balls (shells of 2, then 8)  •  each ball blocks one enemy", 40f, 30f, 30f, 18f, false, Color.white);
        Line(titleScreen, "Enemies that reach the thruster cost a heart  •  coins buy upgrades after each level", 40f, 60f, 30f, 18f, false, Dim);
        Line(titleScreen, "Esc / P  —  pause", 40f, 90f, 30f, 18f, false, Dim);
        titleStart = Line(titleScreen, "", 70f, 0f, 40f, 24f, true, Color.white);
        titleRestart = Line(titleScreen, "Press 1 to start over from Level 1 (resets upgrades)", 70f, 40f, 30f, 18f, false, Dim);
    }

    void BuildIntro()
    {
        introScreen = Layer(root);
        introLevel = Line(introScreen, "", 24f, 0f, 90f, 72f, true, Color.white);
        introStars = Line(introScreen, "", 24f, 80f, 40f, 24f, true, Pickup.Gold);
    }

    void BuildPause()
    {
        pauseScreen = Layer(root);
        pauseScreen.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
        Line(pauseScreen, "PAUSED", 38f, 0f, 90f, 72f, true, Color.white);
        Line(pauseScreen, "Esc / Space to resume   •   R to restart level", 38f, 90f, 30f, 18f, false, Dim);
    }

    void BuildComplete()
    {
        completeScreen = Layer(root);
        completeBand = Band(completeScreen, 30f, 30f, 0.35f);
        completeTitle = Line(completeScreen, "", 33f, 0f, 90f, 72f, true, Pickup.Gold);
        completeNext = Line(completeScreen, "", 33f, 90f, 40f, 24f, true, Color.white);
    }

    void BuildGameOver()
    {
        gameOverScreen = Layer(root);
        gameOverBand = Band(gameOverScreen, 25f, 50f, 0.45f);
        gameOverTitle = Line(gameOverScreen, "GAME OVER", 29f, 0f, 90f, 72f, true, new Color(1f, 0.35f, 0.4f));
        gameOverInfo = Line(gameOverScreen, "", 29f, 90f, 40f, 24f, true, Color.white);
        gameOverRetry = Line(gameOverScreen, "Press SPACE, R or Click to retry the level", 62f, 0f, 40f, 24f, true, Color.white);
    }

    void BuildUpgrade()
    {
        upgradeScreen = Layer(root);
        upgradeScreen.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
        Line(upgradeScreen, "CHOOSE AN UPGRADE", 14f, 0f, 60f, 34f, true, Color.white);

        var counter = Row(upgradeScreen);
        counter.style.position = Position.Absolute;
        counter.style.left = counter.style.right = 0;
        counter.style.top = Length.Percent(14);
        counter.style.marginTop = 60;
        counter.style.height = 30;
        counter.style.justifyContent = Justify.Center;
        upgradeCounterIcon = Icon(counter, ProcSprites.Coin, 22, Pickup.CoinColor);
        upgradeCounter = Text(counter, "", 18, false, Pickup.CoinColor, TextAnchor.MiddleLeft);
        upgradeCounter.style.marginLeft = 6;

        var column = Box(upgradeScreen);
        column.style.position = Position.Absolute;
        column.style.left = column.style.right = 0;
        column.style.top = Length.Percent(36);
        column.style.alignItems = Align.Center;

        cardRow = Row(column);
        cardRow.style.alignItems = Align.Stretch;
        cardRow.style.justifyContent = Justify.Center;
        cardRow.style.paddingLeft = cardRow.style.paddingRight = 20;
        cardRow.style.alignSelf = Align.Stretch;

        upgradeHint = Text(column, "", 18, false, Dim);
        upgradeHint.style.marginTop = 24;
    }

    // ----------------------------------------------------------------- cards

    void RebuildCards()
    {
        builtOffer = gm.OfferVersion;
        cardRow.Clear();
        cards.Clear();

        var offer = gm.Offer;
        revealPending = false;
        if (gm.MysteryIndex >= 0)
        {
            Sfx.PlaySuspense(MysteryReveal, 0.9f);   // lasts exactly until the box opens
            revealPending = true;
        }
        for (int i = 0; i < offer.Count; i++)
            cards.Add(i == gm.MysteryIndex ? MakeMysteryCard(i, offer[i])
                    : GameManager.IsRare(offer[i]) ? MakeRareCard(i, offer[i])
                    : MakeCard(i, offer[i]));

        string keys = "";
        for (int i = 0; i < offer.Count; i++) keys += (i > 0 ? " / " : "") + (i + 1);
        upgradeHint.text = "Click a card or press " + keys;
    }

    /// <summary>Seconds until a Character box opens (shared by the animation and the sounds).</summary>
    float MysteryReveal => Mathf.Max(0.5f, gm.MysteryRevealTime);

    Card NewCardShell(int index)
    {
        var card = new Card();
        var e = new VisualElement { pickingMode = PickingMode.Position };
        e.style.width = 220;
        e.style.minHeight = 230;
        e.style.flexShrink = 1;
        e.style.marginLeft = e.style.marginRight = 8;
        e.style.alignItems = Align.Center;
        e.RegisterCallback<PointerEnterEvent>(_ => card.hover = true);
        e.RegisterCallback<PointerLeaveEvent>(_ => card.hover = false);
        e.RegisterCallback<PointerDownEvent>(ev =>
        {
            if (ev.button == 0) gm.ChooseUpgrade(index);   // GameManager ignores it while sealed / too early
        });
        cardRow.Add(e);
        card.root = e;
        return card;
    }

    Card MakeCard(int index, GameManager.UpgradeType u)
    {
        Card card = NewCardShell(index);
        card.color = GameManager.UpgradeColor(u);
        VisualElement e = card.root;
        e.style.paddingTop = 16;
        e.style.paddingLeft = e.style.paddingRight = 10;
        e.style.paddingBottom = 6;
        Border(e, 3f, Color.clear);
        e.style.borderTopWidth = 6;
        e.style.borderTopColor = card.color;

        Icon(e, IconFor(u), 48, card.color);

        var title = Text(e, GameManager.UpgradeTitle(u), 20, true, card.color, TextAnchor.MiddleCenter, true);
        title.style.marginTop = 10;
        title.style.minHeight = 34;
        var desc = Text(e, gm.UpgradeDescription(u), 16, false, Color.white, TextAnchor.UpperCenter, true);
        desc.style.marginTop = 4;
        desc.style.flexGrow = 1;
        Text(e, "[" + (index + 1) + "]", 18, false, Dim).style.height = 24;
        return card;
    }

    static Sprite IconFor(GameManager.UpgradeType u)
    {
        switch (u)
        {
            case GameManager.UpgradeType.Heal:
            case GameManager.UpgradeType.MaxHealth:
            case GameManager.UpgradeType.Regeneration: return ProcSprites.Heart;
            case GameManager.UpgradeType.BallArmor:
            case GameManager.UpgradeType.BallSize: return ProcSprites.Ball;
            case GameManager.UpgradeType.PickupRange: return ProcSprites.Star;
            case GameManager.UpgradeType.Torque:
            case GameManager.UpgradeType.Repel:
            case GameManager.UpgradeType.TurretRange: return ProcSprites.Ring;
            case GameManager.UpgradeType.FireRate: return ProcSprites.Dart;
            default: return ProcSprites.Arrow;
        }
    }

    /// <summary>
    /// Rare card: its own tier, between Normal cards and Characters. Prismatic border that
    /// cycles through colors, a "RARE" ribbon, and a light sweep across the face.
    /// </summary>
    Card MakeRareCard(int index, GameManager.UpgradeType u)
    {
        Card card = NewCardShell(index);
        card.rare = true;
        card.color = GameManager.UpgradeColor(u);
        VisualElement e = card.root;
        e.style.overflow = Overflow.Hidden;
        e.style.paddingLeft = e.style.paddingRight = 10;
        e.style.paddingBottom = 6;
        e.style.backgroundColor = new Color(0.07f, 0.05f, 0.13f, 0.96f);
        Border(e, 3f, Color.white);
        e.style.borderTopWidth = 0;
        e.style.borderTopLeftRadius = e.style.borderTopRightRadius = 10;
        e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = 10;

        // Light sweep (drawn under the content).
        card.shimmer = Box(e);
        card.shimmer.style.position = Position.Absolute;
        card.shimmer.style.top = -40;
        card.shimmer.style.bottom = -40;
        card.shimmer.style.width = 36;
        card.shimmer.style.rotate = new Rotate(20f);
        card.shimmer.style.backgroundColor = new Color(1f, 1f, 1f, 0.07f);

        // Ribbon across the top.
        card.ribbon = Box(e);
        card.ribbon.style.alignSelf = Align.Stretch;
        card.ribbon.style.marginLeft = card.ribbon.style.marginRight = -10;
        card.ribbon.style.height = 24;
        card.ribbon.style.justifyContent = Justify.Center;
        var tag = Text(card.ribbon, "RARE", 15, true, new Color(0.08f, 0.05f, 0.12f));
        tag.style.letterSpacing = 6;
        tag.style.textShadow = new TextShadow { color = Color.clear };

        var icon = Icon(e, IconFor(u), 48, card.color);
        icon.style.marginTop = 12;
        var title = Text(e, GameManager.UpgradeTitle(u), 20, true, card.color, TextAnchor.MiddleCenter, true);
        title.style.marginTop = 8;
        title.style.minHeight = 34;
        var desc = Text(e, gm.UpgradeDescription(u), 16, false, Color.white, TextAnchor.UpperCenter, true);
        desc.style.marginTop = 4;
        desc.style.flexGrow = 1;
        Text(e, "[" + (index + 1) + "]", 18, false, Dim).style.height = 24;
        return card;
    }

    /// <summary>The Character card: a sealed box that trembles and swells, then bursts open.</summary>
    Card MakeMysteryCard(int index, GameManager.UpgradeType u)
    {
        Card card = NewCardShell(index);
        card.mystery = true;
        card.color = GameManager.UpgradeColor(u);
        VisualElement e = card.root;
        Border(e, 4f, CardGold);

        // Inner thin frame.
        card.inner = Layer(e);
        card.inner.style.left = card.inner.style.right = card.inner.style.top = card.inner.style.bottom = 9;
        Border(card.inner, 1.5f, WithAlpha(CardGold, 0.45f));

        // Revealed contents (in the layout flow so the card fits the text; hidden until open).
        card.openPart = Box(e);
        card.openPart.style.alignItems = Align.Center;
        card.openPart.style.flexGrow = 1;
        card.openPart.style.paddingTop = 14;
        card.openPart.style.paddingLeft = card.openPart.style.paddingRight = 14;
        card.openPart.style.paddingBottom = 12;
        var tierTag = Text(card.openPart, "CHARACTER", 18, true, CardGold);
        tierTag.style.height = 24;
        tierTag.style.letterSpacing = 4;
        var title = Text(card.openPart, GameManager.UpgradeTitle(u), 20, true, card.color, TextAnchor.MiddleCenter, true);
        title.style.marginTop = 4;
        title.style.minHeight = 34;
        var desc = Text(card.openPart, gm.UpgradeDescription(u), 16, false, Color.white, TextAnchor.UpperCenter, true);
        desc.style.marginTop = 6;
        desc.style.flexGrow = 1;
        var cost = Row(card.openPart);
        cost.style.marginTop = 6;
        Icon(cost, ProcSprites.Coin, 20, Pickup.CoinColor);
        Icon(cost, ProcSprites.Coin, 20, Pickup.CoinColor).style.marginLeft = -2;
        Text(cost, "costs 2 coins", 18, false, Pickup.CoinColor, TextAnchor.MiddleLeft).style.marginLeft = 6;
        Text(card.openPart, "[" + (index + 1) + "]", 18, false, Dim).style.height = 24;

        // Sealed box: lid seam, "? ? ?" and a big pulsing "?".
        card.sealedPart = Layer(e);
        Text(card.sealedPart, "? ? ?", 18, false, WithAlpha(CardGold, 0.8f)).style.marginTop = 14;
        var seam = Box(card.sealedPart);
        seam.style.position = Position.Absolute;
        seam.style.left = seam.style.right = 0;
        seam.style.top = Length.Percent(30);
        seam.style.height = 2;
        seam.style.backgroundColor = WithAlpha(CardGold, 0.6f);
        card.bigQuestion = Text(card.sealedPart, "?", 90, true, CardGold);
        card.bigQuestion.style.position = Position.Absolute;
        card.bigQuestion.style.left = card.bigQuestion.style.right = 0;
        card.bigQuestion.style.top = Length.Percent(28);
        card.bigQuestion.style.height = Length.Percent(60);

        // White flash when it opens.
        card.flash = Layer(e);
        card.flash.style.backgroundColor = Color.white;
        card.flash.style.opacity = 0f;
        return card;
    }

    void UpdateCards()
    {
        if (builtOffer != gm.OfferVersion) RebuildCards();

        float T = gm.StateTime, R = MysteryReveal;
        if (revealPending && T >= R)
        {
            revealPending = false;
            Sfx.Play("reveal", 0.9f);
        }
        float ut = Time.unscaledTime;
        foreach (var card in cards)
        {
            VisualElement e = card.root;
            if (card.rare)
            {
                // Prismatic border + ribbon, gentle bob, light sweep every couple of seconds.
                Color prism = Color.HSVToRGB(Mathf.Repeat(ut * 0.22f, 1f), 0.45f, 1f);
                e.style.borderLeftColor = e.style.borderRightColor = e.style.borderBottomColor = prism;
                card.ribbon.style.backgroundColor = prism;
                e.style.backgroundColor = new Color(0.07f, 0.05f, 0.13f, card.hover ? 1f : 0.96f);
                float lift = card.hover ? -6f : -2f * (0.5f + 0.5f * Mathf.Sin(ut * 2.5f));
                e.style.translate = new Translate(0f, lift);
                float sweep = Mathf.Repeat(ut * 0.45f, 1f);   // 0..1, visible for the first part of the cycle
                card.shimmer.style.left = Mathf.Lerp(-60f, 300f, Mathf.Clamp01(sweep * 1.6f));
                continue;
            }
            if (!card.mystery)
            {
                e.style.backgroundColor = new Color(0.08f, 0.09f, 0.16f, card.hover ? 0.98f : 0.9f);
                Color side = card.hover ? card.color : Color.clear;
                e.style.borderLeftColor = e.style.borderRightColor = e.style.borderBottomColor = side;
                continue;
            }

            bool revealed = T >= R;
            // Still for the first quarter, then shake + grow until the reveal.
            float build = Mathf.Clamp01((T - R * 0.25f) / (R * 0.75f));
            float scale = revealed ? 1.1f + 0.06f * Mathf.Clamp01(1f - (T - R) / 0.25f) : 1f + 0.16f * build * build;
            float amp = revealed ? 0f : 9f * build * build;
            e.style.scale = new Scale(new Vector3(scale, scale, 1f));
            e.style.translate = new Translate(Mathf.Sin(ut * 61f) * amp, Mathf.Cos(ut * 47f) * amp * 0.6f);

            Color c = revealed ? card.color : CardGold;
            e.style.backgroundColor = revealed ? new Color(0.12f, 0.08f, 0.14f, 0.98f) : new Color(0.16f, 0.12f, 0.05f, 0.98f);
            e.style.borderLeftColor = e.style.borderRightColor = e.style.borderTopColor = e.style.borderBottomColor = c;
            Border(card.inner, 1.5f, WithAlpha(CardGold, card.hover && revealed ? 0.9f : 0.45f));

            card.openPart.style.visibility = revealed ? Visibility.Visible : Visibility.Hidden;
            Show(card.sealedPart, !revealed);
            if (!revealed)
            {
                float pulse = 0.6f + 0.4f * Mathf.Sin(ut * (4f + 14f * build));
                card.bigQuestion.style.color = WithAlpha(CardGold, pulse);
            }
            card.flash.style.opacity = revealed ? Mathf.Clamp01(1f - (T - R) / 0.35f) : 0f;
        }
    }

    // ---------------------------------------------------------------- update

    void LateUpdate()
    {
        if (root == null || gm == null) return;
        var st = gm.CurrentState;

        Show(hud, st != GameManager.State.Title);
        Show(titleScreen, st == GameManager.State.Title);
        Show(introScreen, st == GameManager.State.Playing && gm.LevelTime < 2.5f);
        Show(pauseScreen, st == GameManager.State.Paused);
        Show(completeScreen, st == GameManager.State.LevelComplete);
        Show(gameOverScreen, st == GameManager.State.GameOver);
        Show(upgradeScreen, st == GameManager.State.Upgrade);

        if (st != GameManager.State.Title) UpdateHud(st);

        switch (st)
        {
            case GameManager.State.Title:
                titleStart.text = gm.SavedLevel > 1
                    ? "Press SPACE or Click to continue  —  Level " + gm.SavedLevel
                    : "Press SPACE or Click to start";
                titleStart.style.color = new Color(1f, 1f, 1f, Blink);
                Show(titleRestart, gm.SavedLevel > 1);
                break;

            case GameManager.State.Playing:
                if (gm.LevelTime < 2.5f)
                {
                    float t = gm.LevelTime;
                    float a = Mathf.Clamp01(Mathf.Min(t / 0.3f, (2.5f - t) / 0.6f));
                    introLevel.text = "LEVEL " + gm.Level;
                    introStars.text = "Collect " + gm.StarsNeeded + " stars";
                    introLevel.style.color = new Color(1f, 1f, 1f, a);
                    introStars.style.color = WithAlpha(Pickup.Gold, a);
                }
                break;

            case GameManager.State.LevelComplete:
            {
                float a = Mathf.Clamp01(gm.StateTime / 0.3f);
                completeBand.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f * a);
                completeTitle.text = "LEVEL " + gm.Level + " COMPLETE";
                completeTitle.style.color = WithAlpha(Pickup.Gold, a);
                int coins = gm.LevelCoins;
                completeNext.text = coins > 0
                    ? coins + (coins == 1 ? " coin" : " coins") + " to spend on upgrades"
                    : "Next: Level " + (gm.Level + 1) + "  —  " + gm.NextLevelStars + " stars";
                completeNext.style.color = new Color(1f, 1f, 1f, a);
                break;
            }

            case GameManager.State.GameOver:
            {
                float a = Mathf.Clamp01(gm.StateTime / 0.5f);
                gameOverBand.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f * a);
                gameOverTitle.style.opacity = a;
                gameOverInfo.text = "Level " + gm.Level + "   •   " + gm.StarsCollected + " / " + gm.StarsNeeded + " stars";
                gameOverInfo.style.opacity = a;
                Show(gameOverRetry, gm.StateTime > 0.8f);
                gameOverRetry.style.color = new Color(1f, 1f, 1f, Blink);
                break;
            }

            case GameManager.State.Upgrade:
            {
                bool refund = gm.ChoiceIndex >= gm.CoinChoices && gm.RefundTotal > 0;
                Color rc = GameManager.UpgradeColor(GameManager.CharacterCard(gm.ActiveCharacter));
                upgradeCounter.text = refund
                    ? "Refund " + (gm.ChoiceIndex - gm.CoinChoices + 1) + " of " + gm.RefundTotal
                    : "Coin " + (gm.ChoiceIndex + 1) + " of " + gm.CoinChoices;
                upgradeCounter.style.color = refund ? rc : Pickup.CoinColor;
                upgradeCounterIcon.style.unityBackgroundImageTintColor = refund ? rc : Pickup.CoinColor;
                UpdateCards();
                break;
            }
        }
    }

    void UpdateHud(GameManager.State st)
    {
        if (shownLevel != gm.Level) { shownLevel = gm.Level; levelLabel.text = "LEVEL " + shownLevel; }
        if (shownStars != gm.StarsCollected || shownNeeded != gm.StarsNeeded)
        {
            shownStars = gm.StarsCollected;
            shownNeeded = gm.StarsNeeded;
            starsLabel.text = shownStars + " / " + shownNeeded;
        }
        if (shownCoins != gm.LevelCoins || shownCoinMax != gm.CoinsPerLevel)
        {
            shownCoins = gm.LevelCoins;
            shownCoinMax = gm.CoinsPerLevel;
            coinsLabel.text = shownCoins + " / " + shownCoinMax;
        }
        if (shownBest != gm.BestLevel) { shownBest = gm.BestLevel; bestLabel.text = "BEST  LV " + shownBest; }

        if (shownCharacter != gm.ActiveCharacter)
        {
            shownCharacter = gm.ActiveCharacter;
            GameManager.UpgradeType card = GameManager.CharacterCard(shownCharacter);
            Show(characterLabel, shownCharacter != GameManager.Character.None);
            characterLabel.text = GameManager.UpgradeTitle(card);
            characterLabel.style.color = GameManager.UpgradeColor(card);
        }

        PendulumController pc = gm.Pendulum;
        if (pc != null)
        {
            // Turrets (Spaceship) then balls per shell ("turrets 1/2   3/8").
            int sig = pc.ShellTotal * 7 + pc.TurretSlots * 1009 + pc.TurretCount * 31337;
            for (int i = 0; i < pc.ShellTotal; i++) sig = sig * 31 + pc.ShellCount(i) * 97 + pc.ShellCapacity(i);
            if (sig != shownShellSig)
            {
                shownShellSig = sig;
                Show(shellsRow, pc.ShellTotal > 0 || pc.TurretSlots > 0);
                string shells = pc.TurretSlots > 0 ? "turrets " + pc.TurretCount + "/" + pc.TurretSlots : "";
                for (int i = 0; i < pc.ShellTotal; i++)
                    shells += (shells.Length > 0 ? "   " : "") + pc.ShellCount(i) + "/" + pc.ShellCapacity(i);
                shellsLabel.text = shells;
            }

            Show(speedBox, st != GameManager.State.Upgrade);
            float spd = pc.Speed;
            int tenths = Mathf.RoundToInt(spd * 10f);
            if (tenths != shownSpeed)
            {
                shownSpeed = tenths;
                speedLabel.text = (tenths / 10f).ToString("0.0") + "  SPEED";
            }
            speedFill.style.width = Length.Percent(100f * Mathf.Clamp01(spd / Mathf.Max(0.01f, pc.MaxSpeed)));
        }

        // Hearts: rebuilt when max health changes, tinted every time health changes.
        int max = gm.MaxHealth;
        if (hearts.Count != max)
        {
            heartsRow.Clear();
            hearts.Clear();
            for (int i = 0; i < max; i++)
            {
                var h = Icon(heartsRow, ProcSprites.Heart, 28, Color.white);
                if (i > 0) h.style.marginLeft = 6;
                hearts.Add(h);
            }
            shownLives = -1;
        }
        if (shownLives != gm.Lives)
        {
            shownLives = gm.Lives;
            for (int i = 0; i < hearts.Count; i++)
                hearts[i].style.unityBackgroundImageTintColor = i < shownLives ? Pickup.HeartColor : new Color(1f, 1f, 1f, 0.15f);
        }
    }
}
