using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// The Index: every power-up and enemy, "?" until the player has met it (see Codex).
/// Power-ups show their card art and text; enemies also play a small looping demo of what
/// they do on the field (thruster with its orbiting orbs, the enemy coming at it).
/// Opened from the title and pause screens (button or I), closed with Esc / Back.
/// </summary>
public partial class GameUI
{
    enum Demo { None, Shard, Brute, Missile, Flash, Broken, Asteroid, Well }

    class Entry
    {
        public string id, name, desc, tag;
        public Sprite sprite;
        public Color color;
        public Demo demo;
        public bool Seen => Codex.Seen(id);
    }

    static readonly Color RockColor = new Color(0.66f, 0.6f, 0.54f);
    static readonly Color WellColor = new Color(0.6f, 0.35f, 1f);
    static readonly Color PodColor = new Color(0.9f, 0.82f, 0.62f);

    VisualElement indexScreen, indexGrid, detailIconHolder, previewBox;
    Label detailName, detailTag, detailDesc;
    VisualElement[] tabs = new VisualElement[2];
    Label[] tabCounts = new Label[2];
    readonly List<VisualElement> tiles = new List<VisualElement>();
    List<Entry> entries = new List<Entry>();
    bool indexOpen;
    int indexTab, selected, builtTab = -1;

    // Demo field.
    const float FieldW = 420f, FieldH = 230f;
    VisualElement pvPod, pvRing, pvEnemy, pvAim, pvPop, pvCore, pvCoreGlow, pvDisk;
    readonly VisualElement[] pvOrbs = new VisualElement[2];
    readonly VisualElement[] pvSwirl = new VisualElement[3];
    readonly VisualElement[] pvDust = new VisualElement[6];
    Demo demo;
    float demoTime, demoLastT;
    Vector2 simPos, simVel;
    float simTimer;
    int simPushes;
    bool simDead;

    // ----------------------------------------------------------------- data

    List<Entry> BuildEntries(int tab)
    {
        var list = new List<Entry>();
        if (tab == 0)
        {
            foreach (GameManager.UpgradeType u in System.Enum.GetValues(typeof(GameManager.UpgradeType)))
            {
                list.Add(new Entry
                {
                    id = Codex.Upgrade(u),
                    name = GameManager.UpgradeTitle(u),
                    desc = gm.IndexDescription(u),
                    tag = GameManager.IsCharacter(u) ? Loc.T("CHARACTER", "KARAKTER")
                        : GameManager.IsRare(u) ? Loc.T("RARE", "NADİR") : "",
                    sprite = IconFor(u),
                    color = GameManager.UpgradeColor(u),
                });
            }
            return list;
        }

        var sp = FindFirstObjectByType<EnemySpawner>();
        Color shard = sp != null ? sp.shardColor : new Color(1f, 0.45f, 0.25f);
        Color brute = sp != null ? sp.bruteColor : new Color(0.85f, 0.25f, 0.6f);
        Color missile = sp != null ? sp.missileColor : new Color(0.55f, 1f, 0.35f);
        Color flash = sp != null ? sp.flashColor : new Color(1f, 0.95f, 0.3f);
        Color broken = sp != null ? sp.brokenColor : new Color(0.7f, 0.55f, 1f);

        list.Add(new Entry { id = Codex.Enemy(EnemyType.Shard), sprite = ProcSprites.Shard, color = shard, demo = Demo.Shard,
            name = Loc.T("FREE RADICAL", "SERBEST RADİKAL"),
            desc = Loc.T("A reactive fragment with one unpaired electron. Flies straight at you, sometimes in cosmic ray showers. It bonds with the first electron it touches and carries it off.",
                         "Eşleşmemiş tek elektronlu, tepkin bir parça. Sana dümdüz uçar, bazen kozmik ışın sağanağıyla gelir. Dokunduğu ilk elektronla bağ kurup onu alıp götürür.") });
        list.Add(new Entry { id = Codex.Enemy(EnemyType.Brute), sprite = ProcSprites.Alpha, color = brute, demo = Demo.Brute,
            name = Loc.T("ALPHA PARTICLE", "ALFA PARÇACIĞI"),
            desc = Loc.T("Two protons, two neutrons: heavy and slow, and it shoves you twice as hard. It snatches an electron and keeps coming; with a second one it becomes helium and leaves.",
                         "İki proton, iki nötron: ağır ve yavaş, seni iki kat sert iter. Bir elektron kapar ve gelmeye devam eder; ikincisini de alınca helyuma dönüşüp gider.") });
        list.Add(new Entry { id = Codex.Enemy(EnemyType.Missile), sprite = ProcSprites.Positron, color = missile, demo = Demo.Missile,
            name = Loc.T("POSITRON", "POZİTRON"),
            desc = Loc.T("Antimatter. Its opposite charge pulls it after you forever. When it catches an electron the two circle each other as positronium, then annihilate in a flash. A Repel blast breaks its pull.",
                         "Antimadde. Zıt yükü onu peşinden hiç ayırmaz. Bir elektron yakalarsa ikisi pozitronyum olarak birbirinin etrafında döner, sonra bir parlamayla yok olurlar. İtme patlaması çekimini koparır.") });
        list.Add(new Entry { id = Codex.Enemy(EnemyType.Flash), sprite = ProcSprites.Gamma, color = flash, demo = Demo.Flash,
            name = Loc.T("GAMMA RAY", "GAMA IŞINI"),
            desc = Loc.T("Appears at the edge and aims. When the line locks, it strikes once at light speed, piercing up to 3 targets and knocking electrons out. Sometimes they come as a Gamma Rain. Move!",
                         "Kenarda belirip nişan alır. Çizgi sabitlenince ışık hızında bir kez vurur; 3 hedefe kadar delip geçer ve elektronları koparır. Bazen Gama Yağmuru olarak gelirler. Kaç!") });
        list.Add(new Entry { id = Codex.Enemy(EnemyType.Broken), sprite = ProcSprites.Isotope, color = broken, demo = Demo.Broken,
            name = Loc.T("UNSTABLE ISOTOPE", "KARARSIZ İZOTOP"),
            desc = Loc.T("Each decay flings it at you, weaker every time, until it turns stable and drifts. Its first hit shakes an electron loose; its second takes one with it.",
                         "Her bozunma onu sana fırlatır, her seferinde daha zayıf; sonunda kararlı hâle gelip sürüklenir. İlk vuruşu bir elektronu koparır, ikincisi birini alıp götürür.") });
        list.Add(new Entry { id = Codex.Hazard(HazardType.Asteroid), sprite = ProcSprites.Asteroid(0), color = RockColor, demo = Demo.Asteroid,
            name = Loc.T("DUST GRAIN", "TOZ TANESİ"),
            desc = Loc.T("To an atom, a mountain. Unbreakable: crushes enemies, chips your electrons and bounces you away.",
                         "Bir atom için koca bir dağ. Kırılmaz: düşmanları ezer, elektronlarını hırpalar ve seni savurur.") });
        list.Add(new Entry { id = Codex.Hazard(HazardType.GravityWell), sprite = ProcSprites.Ring, color = WellColor, demo = Demo.Well,
            name = Loc.T("BLACK HOLE", "KARA DELİK"),
            desc = Loc.T("Pulls everything in, harder near the core. The core destroys electrons.",
                         "Her şeyi içine çeker, merkeze yaklaştıkça daha güçlü. Merkezi elektronları yok eder.") });
        return list;
    }

    // ---------------------------------------------------------------- build

    void BuildIndex()
    {
        builtTab = -1;
        tiles.Clear();
        indexScreen = Layer(root);
        indexScreen.pickingMode = PickingMode.Position;   // blocks the menu underneath
        indexScreen.style.backgroundColor = new Color(0.01f, 0.01f, 0.04f, 0.94f);

        var head = Column(indexScreen, 4f);
        var title = Text(head, Loc.T("INDEX", "DİZİN"), 44, true, Pickup.Gold);
        title.style.letterSpacing = 8;
        var tabRow = Row(head);
        tabRow.style.marginTop = 10;
        MakeTab(tabRow, 0, ProcSprites.Quanta, Loc.T("POWER-UPS", "GÜÇLENDİRMELER"));
        MakeTab(tabRow, 1, ProcSprites.Positron, Loc.T("ENEMIES", "DÜŞMANLAR"));

        var main = Row(indexScreen);
        main.style.position = Position.Absolute;
        main.style.left = main.style.right = 40;
        main.style.top = Length.Percent(24);
        main.style.bottom = Length.Percent(14);
        main.style.alignItems = Align.FlexStart;
        main.style.justifyContent = Justify.Center;

        indexGrid = Row(main);
        indexGrid.style.flexWrap = Wrap.Wrap;
        indexGrid.style.width = 520;
        indexGrid.style.alignContent = Align.FlexStart;
        indexGrid.style.justifyContent = Justify.FlexStart;

        var detail = Box(main);
        detail.style.width = 470;
        detail.style.marginLeft = 30;
        detail.style.alignItems = Align.Center;
        detail.style.paddingTop = 14;
        detail.style.paddingBottom = 18;
        detail.style.paddingLeft = detail.style.paddingRight = 20;
        detail.style.backgroundColor = WithAlpha(Panel, 0.8f);
        Radius(detail, 14);
        Border(detail, 1.5f, WithAlpha(Color.white, 0.12f));

        detailTag = Text(detail, "", 14, true, CardGold);
        detailTag.style.letterSpacing = 4;
        detailTag.style.height = 18;
        detailIconHolder = Box(detail);
        detailIconHolder.style.height = 96;
        detailIconHolder.style.justifyContent = Justify.Center;
        detailIconHolder.style.alignItems = Align.Center;
        BuildPreview(detail);
        detailName = Text(detail, "", 30, true, Color.white, TextAnchor.MiddleCenter, true);
        detailName.style.marginTop = 10;
        detailDesc = Text(detail, "", 18, false, new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperCenter, true);
        detailDesc.style.marginTop = 8;
        detailDesc.style.maxWidth = 420;

        var bottom = Column(indexScreen, 87f);
        Button(bottom, null, Loc.T("BACK", "GERİ"), "ESC", Dim, false, CloseIndex);

        Show(indexScreen, false);
    }

    void MakeTab(VisualElement parent, int index, Sprite icon, string label)
    {
        var e = new VisualElement { pickingMode = PickingMode.Position };
        e.style.flexDirection = FlexDirection.Row;
        e.style.alignItems = Align.Center;
        e.style.height = 42;
        e.style.paddingLeft = e.style.paddingRight = 16;
        e.style.marginLeft = e.style.marginRight = 6;
        Radius(e, 10);
        Icon(e, icon, 20, Color.white).style.marginRight = 8;
        Text(e, label, 18, true, Color.white);
        tabCounts[index] = Text(e, "", 15, true, Dim);
        tabCounts[index].style.marginLeft = 10;
        e.RegisterCallback<PointerDownEvent>(ev => { if (ev.button == 0) SetTab(index); });
        parent.Add(e);
        tabs[index] = e;
    }

    VisualElement Abs(VisualElement parent, Sprite sprite, Color c)
    {
        var e = Icon(parent, sprite, 10, c);
        e.style.position = Position.Absolute;
        return e;
    }

    void BuildPreview(VisualElement parent)
    {
        previewBox = Box(parent);
        previewBox.style.width = FieldW;
        previewBox.style.height = FieldH;
        previewBox.style.overflow = Overflow.Hidden;
        previewBox.style.backgroundColor = new Color(0.03f, 0.04f, 0.09f, 1f);
        previewBox.style.backgroundImage = new StyleBackground(ProcSprites.GridTile);
        previewBox.style.unityBackgroundImageTintColor = new Color(0.55f, 0.75f, 1f, 0.08f);
        previewBox.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
        previewBox.style.backgroundSize = new BackgroundSize(32, 32);
        Radius(previewBox, 10);
        Border(previewBox, 1.5f, WithAlpha(Color.white, 0.12f));

        for (int i = 0; i < pvSwirl.Length; i++) pvSwirl[i] = Abs(previewBox, ProcSprites.Ring, Color.clear);
        pvCoreGlow = Abs(previewBox, ProcSprites.Glow, WithAlpha(WellColor, 0.6f));
        pvDisk = Abs(previewBox, ProcSprites.Ring, new Color(1f, 0.75f, 0.45f, 0.7f));
        pvCore = Abs(previewBox, ProcSprites.Ball, Color.black);
        for (int i = 0; i < pvDust.Length; i++) pvDust[i] = Abs(previewBox, ProcSprites.Ball, new Color(0.8f, 0.85f, 1f, 0.7f));
        pvRing = Abs(previewBox, ProcSprites.Ring, new Color(1f, 1f, 1f, 0.12f));
        for (int i = 0; i < pvOrbs.Length; i++) pvOrbs[i] = Abs(previewBox, ProcSprites.Electron, Pickup.OrbColor);
        pvPod = Abs(previewBox, ProcSprites.Nucleus, PodColor);
        pvAim = Box(previewBox);
        pvAim.style.position = Position.Absolute;
        pvAim.style.height = 2;
        pvAim.style.width = 600;
        pvAim.style.transformOrigin = new TransformOrigin(0, Length.Percent(50));
        pvEnemy = Abs(previewBox, ProcSprites.Shard, Color.white);
        pvPop = Abs(previewBox, ProcSprites.Ring, Color.clear);
    }

    // -------------------------------------------------------------- control

    void OpenIndex()
    {
        if (indexOpen) return;
        indexOpen = true;
        builtTab = -1;
        Sfx.Play("orb", 0.5f);
    }

    void CloseIndex()
    {
        if (!indexOpen) return;
        indexOpen = false;
        if (gm != null) gm.OverlayOpen = false;
    }

    void SetTab(int t)
    {
        if (t == indexTab && builtTab == t) return;
        indexTab = t;
        selected = 0;
        builtTab = -1;
    }

    void RebuildGrid()
    {
        builtTab = indexTab;
        indexGrid.Clear();
        tiles.Clear();
        entries = BuildEntries(indexTab);
        for (int i = 0; i < entries.Count; i++)
        {
            int index = i;
            Entry en = entries[i];
            var tile = new VisualElement { pickingMode = PickingMode.Position };
            tile.style.width = tile.style.height = 64;
            tile.style.marginRight = tile.style.marginBottom = 10;
            tile.style.alignItems = Align.Center;
            tile.style.justifyContent = Justify.Center;
            Radius(tile, 10);
            if (en.Seen) Icon(tile, en.sprite, 40, en.color);
            else Text(tile, "?", 30, true, WithAlpha(Color.white, 0.3f));
            tile.RegisterCallback<PointerDownEvent>(ev => { if (ev.button == 0) Select(index); });
            tile.RegisterCallback<PointerEnterEvent>(_ => Select(index));
            indexGrid.Add(tile);
            tiles.Add(tile);
        }
        for (int t = 0; t < 2; t++)
        {
            int seen = 0, total = 0;
            foreach (var en in BuildEntries(t)) { total++; if (en.Seen) seen++; }
            tabCounts[t].text = seen + "/" + total;
            bool on = t == indexTab;
            tabs[t].style.backgroundColor = on ? WithAlpha(Pickup.Gold, 0.25f) : WithAlpha(Panel, 0.6f);
            Border(tabs[t], 2f, on ? Pickup.Gold : WithAlpha(Color.white, 0.2f));
        }
        selected = Mathf.Clamp(selected, 0, entries.Count - 1);
        Select(selected, true);
    }

    void Select(int i, bool force = false)
    {
        if (i < 0 || i >= entries.Count || (i == selected && !force)) return;
        selected = i;
        for (int k = 0; k < tiles.Count; k++)
        {
            Entry en = entries[k];
            bool on = k == selected;
            tiles[k].style.backgroundColor = en.Seen ? WithAlpha(en.color, on ? 0.28f : 0.1f) : WithAlpha(Panel, on ? 1f : 0.7f);
            Border(tiles[k], on ? 3f : 1.5f, on ? Color.white : WithAlpha(en.Seen ? en.color : Color.white, 0.25f));
        }

        Entry e = entries[selected];
        detailIconHolder.Clear();
        bool seen = e.Seen;
        bool showDemo = seen && e.demo != Demo.None;
        Show(previewBox, showDemo);
        Show(detailIconHolder, !showDemo);
        if (seen) { if (!showDemo) Icon(detailIconHolder, e.sprite, 88, e.color); }
        else Text(detailIconHolder, "?", 80, true, WithAlpha(Color.white, 0.25f));
        detailTag.text = seen ? e.tag : "";
        detailName.text = seen ? e.name : "???";
        detailName.style.color = seen ? e.color : WithAlpha(Color.white, 0.4f);
        detailDesc.text = seen ? e.desc : Loc.T("Not discovered yet", "Henüz keşfedilmedi");
        StartDemo(showDemo ? e.demo : Demo.None, e);
    }

    void UpdateIndex(GameManager.State st)
    {
        bool allowed = st == GameManager.State.Title || st == GameManager.State.Paused;
        if (!allowed && indexOpen) CloseIndex();

        var kb = Keyboard.current;
        if (allowed && !indexOpen && kb != null && kb.iKey.wasPressedThisFrame) OpenIndex();
        else if (indexOpen && kb != null)
        {
            if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame) CloseIndex();
            else if (kb.tabKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) SetTab(1 - indexTab);
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Select(Mathf.Min(selected + 1, entries.Count - 1));
            else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Select(Mathf.Max(selected - 1, 0));
            else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Select(Mathf.Min(selected + 7, entries.Count - 1));
            else if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Select(Mathf.Max(selected - 7, 0));
        }

        gm.OverlayOpen = indexOpen;
        Show(indexScreen, indexOpen);
        if (titleScreen != null) Show(titleScreen, st == GameManager.State.Title && !indexOpen);
        if (pauseScreen != null) Show(pauseScreen, st == GameManager.State.Paused && !indexOpen);
        if (!indexOpen) return;
        if (builtTab != indexTab) RebuildGrid();
        UpdateDemo(Time.unscaledDeltaTime);
    }

    // ----------------------------------------------------------------- demo

    static void Place(VisualElement e, Vector2 p, float size, float rotDeg = 0f, float alpha = 1f)
    {
        e.style.left = p.x - size * 0.5f;
        e.style.top = p.y - size * 0.5f;
        e.style.width = e.style.height = size;
        e.style.rotate = new Rotate(rotDeg);
        e.style.opacity = alpha;
    }

    static float Deg(Vector2 d) => Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

    Color demoColor;

    void StartDemo(Demo d, Entry e)
    {
        demo = d;
        demoTime = 0f;
        demoLastT = 0f;
        if (d == Demo.None) return;
        demoColor = e.color;
        pvEnemy.style.backgroundImage = new StyleBackground(e.sprite);
        pvEnemy.style.unityBackgroundImageTintColor = e.color;
        bool well = d == Demo.Well;
        foreach (var s in pvSwirl) Show(s, well);
        foreach (var s in pvDust) Show(s, well);
        Show(pvCore, well);
        Show(pvCoreGlow, well);
        Show(pvDisk, well);
        Show(pvEnemy, !well);
        Show(pvAim, d == Demo.Flash);
        ResetSim();
    }

    void ResetSim()
    {
        simDead = false;
        simPushes = 0;
        simTimer = 0.2f;
        if (demo == Demo.Missile) { simPos = new Vector2(-10f, 20f); simVel = new Vector2(1f, 0.25f).normalized; }
        else if (demo == Demo.Broken) { simPos = new Vector2(FieldW + 10f, 26f); simVel = Vector2.zero; }
    }

    /// <summary>The demo thruster with its two orbiting orbs.</summary>
    void PlacePod(Vector2 p)
    {
        Place(pvPod, p, 22f);
        Place(pvRing, p, 64f);
        for (int i = 0; i < pvOrbs.Length; i++)
        {
            float a = demoTime * 1.8f + i * Mathf.PI;
            Place(pvOrbs[i], p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 30f, 12f);
        }
    }

    void Pop(Vector2 at, float age, Color c)
    {
        if (age < 0f || age > 0.4f) { pvPop.style.opacity = 0f; return; }
        float k = age / 0.4f;
        Place(pvPop, at, 20f + 50f * k, 0f, 1f - k);
        pvPop.style.unityBackgroundImageTintColor = c;
    }

    void UpdateDemo(float dt)
    {
        if (demo == Demo.None) return;
        demoTime += dt;
        Vector2 pod = new Vector2(FieldW * 0.62f, FieldH * 0.5f);
        const float contact = 36f;
        pvPop.style.opacity = 0f;

        switch (demo)
        {
            case Demo.Shard:
            {
                float t = demoTime % 2.4f;
                Vector2 start = new Vector2(-15f, 50f);
                Vector2 dir = (pod - start).normalized;
                float reach = (pod - start).magnitude - contact;
                float d = 170f * t;
                PlacePod(pod);
                if (d < reach) Place(pvEnemy, start + dir * d, 20f, Deg(dir));
                else { pvEnemy.style.opacity = 0f; Pop(start + dir * reach, (d - reach) / 170f, demoColor); }
                break;
            }
            case Demo.Brute:
            {
                float t = demoTime % 4.6f;
                Vector2 start = new Vector2(-40f, FieldH - 30f);
                Vector2 dir = (pod - start).normalized;
                float reach = (pod - start).magnitude - contact - 12f;
                float t1 = reach / 60f;
                float d;
                bool flashWhite = false, gone = false;
                if (t < t1) d = 60f * t;
                else if (t < t1 + 0.7f) { d = reach - 34f * Mathf.Sin(Mathf.PI * (t - t1) / 0.7f); flashWhite = t < t1 + 0.15f; }
                else { d = reach; gone = true; }
                PlacePod(pod);
                if (!gone)
                {
                    Place(pvEnemy, start + dir * d, 50f, demoTime * 40f);
                    Color c = t > t1 ? Color.Lerp(demoColor, Color.black, 0.35f) : demoColor;
                    pvEnemy.style.unityBackgroundImageTintColor = flashWhite ? Color.white : c;
                }
                else { pvEnemy.style.opacity = 0f; Pop(start + dir * (reach + 12f), t - t1 - 0.7f, demoColor); }
                break;
            }
            case Demo.Missile:
            {
                float t = demoTime % 6f;
                if (t < demoLastT) ResetSim();
                Vector2 p = pod + new Vector2(Mathf.Cos(demoTime * 0.9f) * 50f, Mathf.Sin(demoTime * 0.9f) * 35f) - new Vector2(30f, 0f);
                PlacePod(p);
                if (!simDead)
                {
                    Vector2 want = (p - simPos).normalized;
                    float maxTurn = 2.4f * dt;
                    float ang = Vector2.SignedAngle(simVel, want) * Mathf.Deg2Rad;
                    ang = Mathf.Clamp(ang, -maxTurn, maxTurn);
                    simVel = new Vector2(simVel.x * Mathf.Cos(ang) - simVel.y * Mathf.Sin(ang),
                                         simVel.x * Mathf.Sin(ang) + simVel.y * Mathf.Cos(ang)).normalized;
                    simPos += simVel * 125f * dt;
                    if ((simPos - p).magnitude < contact) { simDead = true; simTimer = 0f; }
                    Place(pvEnemy, simPos, 26f, Deg(simVel));
                    pvEnemy.style.unityBackgroundImageTintColor = Color.Lerp(demoColor, Color.white, Mathf.PingPong(demoTime * 6f, 1f) * 0.4f);
                }
                else { simTimer += dt; pvEnemy.style.opacity = 0f; Pop(simPos, simTimer, demoColor); }
                break;
            }
            case Demo.Flash:
            {
                float t = demoTime % 3f;
                Vector2 start = new Vector2(16f, 40f);
                Vector2 dir = (pod - start).normalized;
                float reach = (pod - start).magnitude - contact;
                PlacePod(pod);
                pvAim.style.left = start.x;
                pvAim.style.top = start.y - 1f;
                pvAim.style.rotate = new Rotate(Deg(dir));
                if (t < 1.4f)
                {
                    float a = t < 1f ? 0.15f + 0.25f * Mathf.PingPong(t * 6f, 1f) : 0.8f;   // aiming, then locked
                    pvAim.style.backgroundColor = WithAlpha(demoColor, a);
                    pvAim.style.height = t < 1f ? 2f : 3f;
                    Place(pvEnemy, start, 22f, Deg(dir));
                }
                else
                {
                    pvAim.style.backgroundColor = Color.clear;
                    float d = (t - 1.4f) * 900f;
                    if (d < reach) Place(pvEnemy, start + dir * d, 22f, Deg(dir));
                    else { pvEnemy.style.opacity = 0f; Pop(start + dir * reach, (d - reach) / 900f, demoColor); }
                }
                break;
            }
            case Demo.Broken:
            {
                float t = demoTime % 6.5f;
                if (t < demoLastT) ResetSim();
                PlacePod(pod);
                if (!simDead)
                {
                    bool engineOn = t < 4.5f;
                    if (engineOn)
                    {
                        simTimer -= dt;
                        if (simTimer <= 0f)
                        {
                            simVel = (pod - simPos).normalized * 165f * Mathf.Pow(0.8f, simPushes);
                            simPushes++;
                            simTimer = 1f;
                        }
                    }
                    simVel *= Mathf.Exp(-2.2f * dt);
                    simPos += simVel * dt;
                    if ((simPos - pod).magnitude < contact) { simDead = true; simTimer = 0f; }
                    float kick = engineOn ? Mathf.Clamp01(1f - (1f - simTimer) / 0.15f) : 0f;
                    Vector2 face = (pod - simPos).normalized;
                    Place(pvEnemy, simPos, 30f * (1f + 0.2f * kick), Deg(face));
                    pvEnemy.style.unityBackgroundImageTintColor = engineOn ? Color.Lerp(demoColor, Color.white, kick * 0.6f)
                                                                           : Color.Lerp(demoColor, Color.gray, 0.6f);
                }
                else { simTimer += dt; pvEnemy.style.opacity = 0f; Pop(simPos, simTimer, demoColor); }
                break;
            }
            case Demo.Asteroid:
            {
                float t = demoTime % 8f;
                Vector2 a = new Vector2(-70f, 30f), b = new Vector2(FieldW + 70f, 110f);
                PlacePod(pod + new Vector2(0f, 40f));
                Place(pvEnemy, Vector2.Lerp(a, b, t / 8f), 110f, demoTime * 12f);
                break;
            }
            case Demo.Well:
            {
                Vector2 c = new Vector2(FieldW * 0.33f, FieldH * 0.5f);
                PlacePod(new Vector2(FieldW * 0.8f, FieldH * 0.5f));
                for (int i = 0; i < pvSwirl.Length; i++)
                {
                    float k = Mathf.Repeat(demoTime * 0.35f + i / (float)pvSwirl.Length, 1f);
                    Place(pvSwirl[i], c, Mathf.Lerp(190f, 24f, k * k));
                    pvSwirl[i].style.unityBackgroundImageTintColor = WithAlpha(WellColor, 0.05f + 0.8f * k * (1f - k));
                }
                Place(pvCoreGlow, c, 60f, 0f, 0.6f + 0.25f * Mathf.Sin(demoTime * 5f));
                Place(pvCore, c, 22f);
                Place(pvDisk, c, 70f, -15f);
                pvDisk.style.scale = new Scale(new Vector3(1f, 0.28f, 1f));
                for (int i = 0; i < pvDust.Length; i++)
                {
                    float u = Mathf.Repeat(demoTime * 0.3f + i / (float)pvDust.Length, 1f);
                    float r = 95f * (1f - u * u);   // falls in faster and faster
                    float ang = i * 1.7f + u * 7f;
                    Place(pvDust[i], c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r, 7f, 0f, Mathf.Min(1f, (1f - u) * 4f));
                }
                break;
            }
        }
        demoLastT = demoTime % (demo == Demo.Missile ? 6f : 6.5f);
    }
}
