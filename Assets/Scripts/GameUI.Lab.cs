using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// The Lab: a research tree of upgrades that carry over between runs, bought with Research
/// Data. Three branches (Atom, Field, Discoveries); in each, a node opens once the one above
/// has a level. Hover a node for details, click to research it. Also here: the character
/// chooser on PLAY (once a character is researched) and the Research Data payout on game over.
/// </summary>
public partial class GameUI
{
    class LabNode
    {
        public LabPerk perk;
        public VisualElement root, ring, iconHolder, connector;
        public Label name, question;
        public Pips pips;
        public float pop;
    }

    VisualElement labScreen;
    Label labBank, labDetailTitle, labDetailLevel, labDetailBody, labDetailStatus;
    VisualElement labDetail;
    readonly List<LabNode> labNodes = new List<LabNode>();
    bool labOpen;
    int labHover = -1;
    float labBankPop;

    VisualElement chooserScreen, chooserRow;
    bool chooserOpen;
    int chooserOpenedFrame;

    // Game over payout
    VisualElement payoutBox, payoutTotalRow;
    Label payoutElement, payoutElementGain, payoutLevel, payoutLevelGain, payoutBonus, payoutTotal, payoutNote, payoutBank;
    int payoutShownVersion = -1, payoutTicked;

    static readonly string[] BranchNamesEn = { "ATOM", "FIELD", "DISCOVERIES" };
    static readonly string[] BranchNamesTr = { "ATOM", "ALAN", "KEŞİFLER" };

    public static void OpenChooser()
    {
        if (instance != null) instance.ShowChooser();
    }

    bool AnyOverlay => indexOpen || labOpen || chooserOpen;

    // ---------------------------------------------------------------- texts

    static string PerkName(LabPerk p)
    {
        switch (p)
        {
            case LabPerk.Stability: return Loc.T("REINFORCED CORE", "GÜÇLÜ ÇEKİRDEK");
            case LabPerk.StartOrbital: return Loc.T("PRIMED ORBITAL", "HAZIR ORBİTAL");
            case LabPerk.StartNeutrino: return Loc.T("NEUTRINO STOCK", "NÖTRİNO STOKU");
            case LabPerk.SecondWind: return Loc.T("SECOND WIND", "İKİNCİ NEFES");
            case LabPerk.Research: return Loc.T("DATA ANALYSIS", "VERİ ANALİZİ");
            case LabPerk.ExtraElectrons: return Loc.T("ELECTRON SOURCE", "ELEKTRON KAYNAĞI");
            case LabPerk.StabilityPickup: return Loc.T("STABILIZERS", "DENGELEYİCİLER");
            case LabPerk.NeutrinoPickup: return Loc.T("NEUTRINO DETECTOR", "NÖTRİNO DEDEKTÖRÜ");
            case LabPerk.RareChance: return Loc.T("RARE FINDS", "NADİR BULGULAR");
            case LabPerk.CharacterChance: return Loc.T("MUTATIONS", "DÖNÜŞÜMLER");
            default: return GameManager.UpgradeTitle(GameManager.CharacterCard(Lab.CharacterOf(p)));
        }
    }

    static string PerkBody(LabPerk p)
    {
        switch (p)
        {
            case LabPerk.Stability: return Loc.T("Every run starts with +1 max stability per level.", "Her oyun, seviye başına +1 azami kararlılıkla başlar.");
            case LabPerk.StartOrbital: return Loc.T("Every run starts with a New Orbital (+2 electron slots) per level.", "Her oyun, seviye başına bir Yeni Orbitalle (+2 elektron yuvası) başlar.");
            case LabPerk.StartNeutrino: return Loc.T("Every run starts with +1 neutrino per level.", "Her oyun, seviye başına +1 nötrinoyla başlar.");
            case LabPerk.SecondWind: return Loc.T("Once per run, a hit that would destroy your core leaves it at 1 stability instead, with a shockwave and 2 seconds of safety.",
                                                  "Oyun başına bir kez, çekirdeğini yok edecek bir vuruş onu 1 kararlılıkta bırakır; bir şok dalgası ve 2 saniyelik koruma da gelir.");
            case LabPerk.Research: return Loc.T("+10% Research Data from every run, per level.", "Seviye başına her oyundan %10 fazla Araştırma Verisi.");
            case LabPerk.ExtraElectrons: return Loc.T("+1 electron placed on the map every level, per level.", "Seviye başına her bölümde haritaya +1 elektron yerleşir.");
            case LabPerk.StabilityPickup: return Loc.T("+8% chance per level that a level holds a stability pickup (for when you're hurt).", "Seviye başına bir bölümde kararlılık bulunma şansı %8 artar (hasarlıyken çıkar).");
            case LabPerk.NeutrinoPickup: return Loc.T("A rare, pale neutrino can drift through a level (4% at first); +6% chance per level. It passes through everything: catch it.",
                                                      "Bir bölümde nadiren soluk bir nötrino süzülebilir (başta %4); seviye başına şans %6 artar. Her şeyin içinden geçer: yakala onu.");
            case LabPerk.RareChance: return Loc.T("Rare upgrades are offered 5% of the time; +1% per level.", "Nadir geliştirmeler %5 ihtimalle çıkar; seviye başına %1 artar.");
            case LabPerk.CharacterChance: return Loc.T("A character box appears 5% of the time; +1% per level.", "Karakter kutusu %5 ihtimalle çıkar; seviye başına %1 artar.");
            default:
                return Loc.T("Choose to start any run as " + PerkName(p) + ". (It can still turn up in a character box as usual.)",
                             "Dilediğin oyuna " + PerkName(p) + " olarak başlayabilirsin. (Karakter kutusunda yine de çıkabilir.)");
        }
    }

    static Sprite PerkIcon(LabPerk p)
    {
        switch (p)
        {
            case LabPerk.Stability:
            case LabPerk.StabilityPickup: return ProcSprites.Stability;
            case LabPerk.StartOrbital:
            case LabPerk.ExtraElectrons: return ProcSprites.Electron;
            case LabPerk.StartNeutrino:
            case LabPerk.NeutrinoPickup: return ProcSprites.Neutrino;
            case LabPerk.SecondWind: return ProcSprites.Ring;
            case LabPerk.Research: return ProcSprites.DataChip;
            case LabPerk.RareChance: return ProcSprites.Star;
            case LabPerk.CharacterChance: return ProcSprites.Quanta;
            default: return IconFor(GameManager.CharacterCard(Lab.CharacterOf(p)));
        }
    }

    static Color PerkColor(LabPerk p)
    {
        switch (p)
        {
            case LabPerk.Stability:
            case LabPerk.StabilityPickup: return Pickup.HeartColor;
            case LabPerk.StartOrbital:
            case LabPerk.ExtraElectrons: return Pickup.OrbColor;
            case LabPerk.StartNeutrino:
            case LabPerk.NeutrinoPickup: return Pickup.NeutrinoColor;
            case LabPerk.SecondWind: return Color.white;
            case LabPerk.Research: return Lab.DataColor;
            case LabPerk.RareChance: return new Color(0.85f, 0.65f, 1f);
            case LabPerk.CharacterChance: return CardGold;
            default: return GameManager.UpgradeColor(GameManager.CharacterCard(Lab.CharacterOf(p)));
        }
    }

    // ---------------------------------------------------------------- build

    void BuildLab()
    {
        labScreen = Layer(root);
        labScreen.style.backgroundColor = new Color(0f, 0.01f, 0.03f, 0.95f);

        var head = Column(labScreen, 4f);
        var title = Text(head, Loc.T("THE LAB", "LABORATUVAR"), 50, true, Lab.DataColor);
        title.style.letterSpacing = 8;
        Text(head, Loc.T("Every experiment leaves data behind. Research here lasts forever.",
                         "Her deney geride veri bırakır. Burada yapılan araştırma sonsuza dek kalır."), 16, false, Dim)
            .style.marginTop = 2;

        // Top-right: the bank.
        var bank = Row(labScreen);
        bank.style.position = Position.Absolute;
        bank.style.right = 28;
        bank.style.top = 22;
        bank.style.alignItems = Align.Center;
        bank.style.paddingLeft = bank.style.paddingRight = 14;
        bank.style.paddingTop = bank.style.paddingBottom = 6;
        bank.style.backgroundColor = WithAlpha(Panel, 0.9f);
        Border(bank, 2f, WithAlpha(Lab.DataColor, 0.7f));
        Radius(bank, 10);
        Icon(bank, ProcSprites.DataChip, 30, Lab.DataColor);
        labBank = Text(bank, "0", 28, true, Lab.DataColor);
        labBank.style.marginLeft = 8;

        // The tree: three columns.
        var tree = Row(labScreen);
        tree.style.position = Position.Absolute;
        tree.style.left = tree.style.right = 0;
        tree.style.top = Length.Percent(17);
        tree.style.justifyContent = Justify.Center;
        tree.style.alignItems = Align.FlexStart;
        labNodes.Clear();
        for (int b = 0; b < 3; b++)
        {
            var col = Box(tree);
            col.style.width = 290;
            col.style.alignItems = Align.Center;
            col.style.marginLeft = col.style.marginRight = 10;
            var bh = Text(col, Loc.T(BranchNamesEn[b], BranchNamesTr[b]), 18, true, Dim);
            bh.style.letterSpacing = 6;
            bh.style.marginBottom = 8;

            VisualElement unlockRow = null;
            foreach (LabPerk p in System.Enum.GetValues(typeof(LabPerk)))
            {
                if (Lab.Branch(p) != b) continue;
                bool unlock = Lab.CharacterOf(p) != GameManager.Character.None;
                if (unlock && unlockRow == null)
                {
                    var link = Box(col);   // one link down to the row of character unlocks
                    link.style.width = 3;
                    link.style.height = 16;
                    link.style.backgroundColor = WithAlpha(Color.white, 0.15f);
                    unlockRow = Row(col);
                    unlockRow.style.justifyContent = Justify.Center;
                    unlockRow.style.alignItems = Align.FlexStart;
                }
                labNodes.Add(BuildLabNode(unlock ? unlockRow : col, p, unlock));
            }
        }

        // Bottom: details of the hovered node.
        labDetail = Box(labScreen);
        labDetail.style.position = Position.Absolute;
        labDetail.style.left = Length.Percent(50);
        labDetail.style.top = Length.Percent(73);
        labDetail.style.width = 620;
        labDetail.style.marginLeft = -310;
        labDetail.style.paddingLeft = labDetail.style.paddingRight = 20;
        labDetail.style.paddingTop = labDetail.style.paddingBottom = 10;
        labDetail.style.backgroundColor = WithAlpha(Panel, 0.9f);
        labDetail.style.alignItems = Align.Center;
        Radius(labDetail, 12);
        labDetailTitle = Text(labDetail, "", 22, true, Color.white);
        labDetailTitle.style.letterSpacing = 2;
        labDetailLevel = Text(labDetail, "", 14, true, Dim);
        labDetailLevel.style.letterSpacing = 3;
        labDetailBody = Text(labDetail, "", 16, false, WithAlpha(Color.white, 0.9f), TextAnchor.MiddleCenter, true);
        labDetailBody.style.marginTop = 4;
        labDetailBody.style.maxWidth = 580;
        labDetailStatus = Text(labDetail, "", 16, true, Lab.DataColor);
        labDetailStatus.style.marginTop = 6;

        var bottom = Column(labScreen, 90f);
        Button(bottom, null, Loc.T("BACK", "GERİ"), "ESC", Dim, false, CloseLab);

        Show(labScreen, false);
    }

    LabNode BuildLabNode(VisualElement parent, LabPerk p, bool small)
    {
        var n = new LabNode { perk = p };
        float size = small ? 54f : 64f;
        var holder = Box(parent);
        holder.style.alignItems = Align.Center;
        holder.style.width = small ? 92 : 240;

        if (!small && Lab.Parent(p) != null)
        {
            n.connector = Box(holder);
            n.connector.style.width = 3;
            n.connector.style.height = 14;
        }

        n.root = new VisualElement { pickingMode = PickingMode.Position };
        n.root.style.width = n.root.style.height = size;
        n.root.style.justifyContent = Justify.Center;
        n.root.style.alignItems = Align.Center;
        Radius(n.root, size * 0.5f);
        holder.Add(n.root);
        n.iconHolder = Box(n.root);
        n.iconHolder.style.justifyContent = Justify.Center;
        n.iconHolder.style.alignItems = Align.Center;
        Icon(n.iconHolder, PerkIcon(p), size * 0.52f, PerkColor(p));
        n.question = Text(n.root, "?", size * 0.5f, true, WithAlpha(Color.white, 0.4f));
        n.question.style.position = Position.Absolute;
        n.ring = n.root;

        n.name = Text(holder, PerkName(p), small ? 11 : 13, true, Color.white, TextAnchor.MiddleCenter, true);
        n.name.style.marginTop = 4;
        n.name.style.maxWidth = small ? 90 : 230;
        n.name.style.letterSpacing = 1;
        n.pips = MakePips(holder, ProcSprites.Ball, 8, 3);
        n.pips.row.style.marginTop = 2;
        n.pips.row.style.marginBottom = 2;

        int index = labNodes.Count;
        n.root.RegisterCallback<PointerEnterEvent>(_ => labHover = index);
        n.root.RegisterCallback<PointerLeaveEvent>(_ => { if (labHover == index) labHover = -1; });
        n.root.RegisterCallback<PointerDownEvent>(ev => { if (ev.button == 0) TryResearch(n); });
        return n;
    }

    void TryResearch(LabNode n)
    {
        if (Lab.Buy(n.perk))
        {
            n.pop = 1f;
            labBankPop = 1f;
            Sfx.Play(Lab.Maxed(n.perk) ? "level" : "coin", 0.9f);
            if (Lab.CharacterOf(n.perk) != GameManager.Character.None) Sfx.Play("reveal", 0.6f);
        }
        else Sfx.Play("full", 0.5f);
    }

    // ------------------------------------------------------------- open / close

    void OpenLab()
    {
        if (labOpen || indexOpen || chooserOpen) return;
        labOpen = true;
        labHover = -1;
        Sfx.Play("orb", 0.5f);
    }

    void CloseLab()
    {
        if (!labOpen) return;
        labOpen = false;
    }

    // ---------------------------------------------------------------- update

    void UpdateLab(GameManager.State st)
    {
        if (labScreen == null) return;
        if (labOpen && st != GameManager.State.Title && st != GameManager.State.GameOver) labOpen = false;
        if (chooserOpen && st != GameManager.State.Title && st != GameManager.State.GameOver) HideChooser();
        Show(labScreen, labOpen);
        UpdateChooser();
        var lk = Keyboard.current;
        if (!AnyOverlay && lk != null && lk.lKey.wasPressedThisFrame
            && (st == GameManager.State.Title || (st == GameManager.State.GameOver && gm.StateTime > 0.8f)))
        {
            OpenLab();
            return;
        }
        if (!labOpen) return;

        var kb = Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)) { CloseLab(); return; }

        float dt = Time.unscaledDeltaTime, ut = Time.unscaledTime;
        labBankPop = Mathf.MoveTowards(labBankPop, 0f, dt / 0.35f);
        labBank.text = Lab.Data.ToString();
        float bs = 1f + 0.25f * labBankPop * labBankPop;
        labBank.style.scale = new Scale(new Vector3(bs, bs, 1f));

        for (int i = 0; i < labNodes.Count; i++)
        {
            LabNode n = labNodes[i];
            int lvl = Lab.Level(n.perk), max = Lab.MaxLevel(n.perk);
            bool open = Lab.IsOpen(n.perk), disc = Lab.Discovered(n.perk);
            bool can = Lab.CanBuy(n.perk);
            Color c = PerkColor(n.perk);
            bool hover = labHover == i;

            n.pop = Mathf.MoveTowards(n.pop, 0f, dt / 0.4f);
            n.pips.Set(max, lvl, c, WithAlpha(Color.white, 0.18f));
            Show(n.iconHolder, open && disc);
            Show(n.question, !(open && disc));
            n.iconHolder.style.opacity = lvl > 0 ? 1f : 0.55f;

            Color border, bg;
            if (!open || !disc) { border = WithAlpha(Color.white, 0.18f); bg = WithAlpha(Panel, 0.7f); }
            else if (lvl >= max) { border = c; bg = WithAlpha(c, 0.3f); }
            else if (can) { border = WithAlpha(c, 0.6f + 0.4f * Mathf.Sin(ut * 4f)); bg = WithAlpha(c, lvl > 0 ? 0.22f : 0.1f); }
            else { border = WithAlpha(c, lvl > 0 ? 0.7f : 0.3f); bg = WithAlpha(c, lvl > 0 ? 0.18f : 0.05f); }
            Border(n.root, hover ? 3f : 2f, hover ? Color.Lerp(border, Color.white, 0.4f) : border);
            n.root.style.backgroundColor = bg;
            float s = (hover ? 1.08f : 1f) * (1f + 0.3f * n.pop * n.pop);
            n.root.style.scale = new Scale(new Vector3(s, s, 1f));
            n.name.style.color = open && disc ? (lvl > 0 ? c : WithAlpha(Color.white, 0.75f)) : WithAlpha(Color.white, 0.3f);
            if (n.connector != null) n.connector.style.backgroundColor = open ? WithAlpha(c, 0.55f) : WithAlpha(Color.white, 0.12f);
        }
        UpdateLabDetail();
    }

    void UpdateLabDetail()
    {
        Show(labDetail, true);
        if (labHover < 0 || labHover >= labNodes.Count)
        {
            Border(labDetail, 1.5f, WithAlpha(Color.white, 0.12f));
            labDetailTitle.text = Loc.T("RESEARCH", "ARAŞTIRMA");
            labDetailTitle.style.color = Lab.DataColor;
            labDetailLevel.text = "";
            labDetailBody.text = Loc.T("Each run earns Research Data: the highest element you reached, plus twice the level the run ended on. Hover a node to see what it does; click to research it.",
                                       "Her oyun Araştırma Verisi kazandırır: ulaştığın en yüksek element, artı oyunun bittiği seviyenin iki katı. Ne işe yaradığını görmek için bir düğümün üzerine gel; araştırmak için tıkla.");
            labDetailStatus.text = Lab.Record > 0 ? Loc.T("Best run: ", "En iyi oyun: ") + Lab.Record : "";
            labDetailStatus.style.color = Dim;
            return;
        }
        LabPerk p = labNodes[labHover].perk;
        Color c = PerkColor(p);
        int lvl = Lab.Level(p), max = Lab.MaxLevel(p);
        Border(labDetail, 2f, WithAlpha(c, 0.8f));
        bool disc = Lab.Discovered(p), open = Lab.IsOpen(p);
        labDetailTitle.text = disc ? PerkName(p) : "???";
        labDetailTitle.style.color = disc ? c : Dim;
        labDetailLevel.text = max > 1 ? Loc.T("LEVEL ", "SEVİYE ") + lvl + " / " + max : (lvl > 0 ? Loc.T("RESEARCHED", "ARAŞTIRILDI") : "");
        labDetailBody.text = disc ? PerkBody(p)
            : Loc.T("A character you haven't met yet. Find it in a character box during a run, then research it here.",
                    "Henüz karşılaşmadığın bir karakter. Önce bir oyunda karakter kutusundan çıkar, sonra burada araştır.");

        string status;
        Color sc = Lab.DataColor;
        if (lvl >= max) { status = Loc.T("FULLY RESEARCHED", "TAMAMEN ARAŞTIRILDI"); sc = c; }
        else if (!open) { status = Loc.T("Needs: ", "Gerekli: ") + PerkName(Lab.Parent(p).Value); sc = Dim; }
        else if (!disc) { status = Loc.T("Not discovered yet", "Henüz keşfedilmedi"); sc = Dim; }
        else
        {
            int cost = Lab.NextCost(p);
            bool can = Lab.Data >= cost;
            status = Loc.T("Cost: ", "Bedel: ") + cost + "  ·  " + (can ? Loc.T("click to research", "araştırmak için tıkla")
                                                                         : Loc.T("not enough data", "veri yetersiz"));
            sc = can ? Lab.DataColor : Danger;
        }
        labDetailStatus.text = status;
        labDetailStatus.style.color = sc;
    }

    // ------------------------------------------------------------- chooser

    void BuildChooser()
    {
        chooserScreen = Layer(root);
        chooserScreen.style.backgroundColor = new Color(0f, 0f, 0.02f, 0.88f);
        var col = Column(chooserScreen, 24f);
        var t = Text(col, Loc.T("START AS", "NE OLARAK BAŞLA"), 40, true, Color.white);
        t.style.letterSpacing = 6;
        Text(col, Loc.T("Characters you researched in the Lab", "Laboratuvarda araştırdığın karakterler"), 16, false, Dim).style.marginTop = 4;
        chooserRow = Row(col);
        chooserRow.style.marginTop = 30;
        chooserRow.style.justifyContent = Justify.Center;
        var bottom = Column(chooserScreen, 80f);
        Button(bottom, null, Loc.T("BACK", "GERİ"), "ESC", Dim, false, HideChooser);
        Show(chooserScreen, false);
    }

    void ShowChooser()
    {
        if (chooserOpen || labOpen || indexOpen) return;
        chooserOpen = true;
        chooserOpenedFrame = Time.frameCount;   // the key that opened it must not also pick
        chooserRow.Clear();
        AddChooserCard(GameManager.Character.None);
        foreach (GameManager.Character c in new[] { GameManager.Character.Challenger, GameManager.Character.Sluggish, GameManager.Character.Spaceship })
            if (Lab.CharacterUnlocked(c)) AddChooserCard(c);
        Sfx.Play("orb", 0.5f);
    }

    void HideChooser()
    {
        chooserOpen = false;
    }

    void AddChooserCard(GameManager.Character c)
    {
        bool normal = c == GameManager.Character.None;
        GameManager.UpgradeType card = GameManager.CharacterCard(c);
        Color col = normal ? Pickup.OrbColor : GameManager.UpgradeColor(card);
        var e = new VisualElement { pickingMode = PickingMode.Position };
        e.style.width = 200;
        e.style.height = 230;
        e.style.marginLeft = e.style.marginRight = 10;
        e.style.alignItems = Align.Center;
        e.style.justifyContent = Justify.Center;
        e.style.backgroundColor = WithAlpha(Panel, 0.9f);
        Radius(e, 14);
        Border(e, 2f, WithAlpha(col, 0.6f));
        Icon(e, normal ? ProcSprites.Electron : IconFor(card), 70, col);
        var name = Text(e, normal ? Loc.T("ATOM", "ATOM") : GameManager.UpgradeTitle(card), 20, true, col, TextAnchor.MiddleCenter, true);
        name.style.marginTop = 12;
        name.style.maxWidth = 180;
        var sub = Text(e, normal ? Loc.T("The usual start", "Her zamanki başlangıç") : Loc.T("Researched", "Araştırıldı"), 14, false, Dim);
        sub.style.marginTop = 4;
        if (normal) Keycap(e, Loc.T("SPACE", "BOŞLUK"), 22).style.marginTop = 10;
        e.RegisterCallback<PointerEnterEvent>(_ => { Border(e, 3f, col); e.style.scale = new Scale(new Vector3(1.05f, 1.05f, 1f)); });
        e.RegisterCallback<PointerLeaveEvent>(_ => { Border(e, 2f, WithAlpha(col, 0.6f)); e.style.scale = new Scale(Vector3.one); });
        e.RegisterCallback<PointerDownEvent>(ev => { if (ev.button == 0) ChooseStart(c); });
        chooserRow.Add(e);
    }

    void ChooseStart(GameManager.Character c)
    {
        HideChooser();
        gm.OverlayOpen = false;
        gm.StartRun(c);
    }

    void UpdateChooser()
    {
        if (chooserScreen == null) return;
        Show(chooserScreen, chooserOpen);
        if (!chooserOpen) return;
        var kb = Keyboard.current;
        if (kb == null || Time.frameCount == chooserOpenedFrame) return;
        if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) HideChooser();
        else if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) ChooseStart(GameManager.Character.None);
    }

    // ------------------------------------------------------- game over payout

    void BuildPayout(VisualElement parent)
    {
        payoutBox = Box(parent);
        payoutBox.style.marginTop = 14;
        payoutBox.style.alignItems = Align.Center;
        payoutBox.style.paddingLeft = payoutBox.style.paddingRight = 22;
        payoutBox.style.paddingTop = payoutBox.style.paddingBottom = 8;
        payoutBox.style.backgroundColor = WithAlpha(Panel, 0.75f);
        Border(payoutBox, 1.5f, WithAlpha(Lab.DataColor, 0.45f));
        Radius(payoutBox, 12);

        var lines = Row(payoutBox);
        lines.style.alignItems = Align.Center;
        var el = Row(lines);
        payoutElement = Text(el, "", 15, true, Dim);
        payoutElementGain = Text(el, "", 17, true, Lab.DataColor);
        payoutElementGain.style.marginLeft = 8;
        var lv = Row(lines);
        lv.style.marginLeft = 26;
        payoutLevel = Text(lv, "", 15, true, Dim);
        payoutLevelGain = Text(lv, "", 17, true, Lab.DataColor);
        payoutLevelGain.style.marginLeft = 8;
        payoutBonus = Text(lines, "", 15, true, Lab.DataColor);
        payoutBonus.style.marginLeft = 26;

        payoutTotalRow = Row(payoutBox);
        payoutTotalRow.style.alignItems = Align.Center;
        payoutTotalRow.style.marginTop = 4;
        Icon(payoutTotalRow, ProcSprites.DataChip, 30, Lab.DataColor);
        payoutTotal = Text(payoutTotalRow, "", 30, true, Lab.DataColor);
        payoutTotal.style.marginLeft = 8;
        var word = Text(payoutTotalRow, Loc.T("RESEARCH DATA", "ARAŞTIRMA VERİSİ"), 15, true, Lab.DataColor);
        word.style.marginLeft = 10;
        word.style.letterSpacing = 3;

        payoutNote = Text(payoutBox, "", 14, true, Pickup.Gold);
        payoutNote.style.letterSpacing = 2;
        payoutBank = Text(payoutBox, "", 14, false, Dim);
        payoutBank.style.marginTop = 2;
    }

    void UpdatePayout()
    {
        if (payoutBox == null) return;
        GameManager.Payout p = gm.LastPayout;
        if (payoutShownVersion != gm.PayoutVersion) { payoutShownVersion = gm.PayoutVersion; payoutTicked = 0; }
        float t = gm.StateTime;

        int z = p.atomicNumber;
        string el = z > 0 ? Elements.Name(z) + " (" + z + ")" : "—";
        payoutElement.text = Loc.T("HIGHEST ELEMENT  ", "EN YÜKSEK ELEMENT  ") + el;
        payoutElementGain.text = "+" + z;
        payoutLevel.text = Loc.T("LEVEL ", "SEVİYE ") + p.level + "  ×2";
        payoutLevelGain.text = "+" + 2 * p.level;
        payoutBonus.text = p.bonus > 0.001f ? Loc.T("ANALYSIS ", "ANALİZ ") + Loc.Pct(p.bonus) : "";
        payoutElement.parent.style.opacity = Mathf.Clamp01((t - 0.9f) / 0.25f);
        payoutLevel.parent.style.opacity = Mathf.Clamp01((t - 1.25f) / 0.25f);
        payoutBonus.style.opacity = Mathf.Clamp01((t - 1.6f) / 0.25f);

        // The total counts up into the bank.
        float k = Mathf.Clamp01((t - 1.8f) / 0.9f);
        int shown = Mathf.RoundToInt(p.gain * (1f - (1f - k) * (1f - k)));
        payoutTotalRow.style.opacity = t >= 1.8f ? 1f : 0f;
        payoutTotal.text = "+" + shown;
        if (shown > payoutTicked && t >= 1.8f)
        {
            if (shown / 3 > payoutTicked / 3 || shown == p.gain) Sfx.Play("lock", 0.5f);
            payoutTicked = shown;
            if (shown == p.gain) Sfx.Play("coin", 0.9f);
        }
        float pop = k >= 1f ? 1f + 0.25f * Mathf.Clamp01(1f - (t - 2.7f) / 0.3f) : 1f;
        payoutTotal.style.scale = new Scale(new Vector3(pop, pop, 1f));

        bool done = t >= 2.75f;
        string note = "";
        if (done && p.record) note = Loc.T("NEW RECORD", "YENİ REKOR");
        else if (done && p.gain < p.value) note = Loc.T("Already recorded this run: ", "Bu oyundan zaten kaydedilen: ") + (p.value - p.gain);
        payoutNote.text = note;
        payoutNote.style.color = p.record ? Pickup.Gold : Dim;
        payoutBank.text = done ? Loc.T("In the Lab: ", "Laboratuvarda: ") + p.bank : "";
    }
}
