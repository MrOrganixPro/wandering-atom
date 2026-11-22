using UnityEngine;

/// <summary>What the Lab can research, grouped by branch (see Lab.Parents for the tree's shape).</summary>
public enum LabPerk
{
    // Atom
    Stability, StartOrbital, StartNeutrino, SecondWind,
    // Field
    Research, ExtraElectrons, StabilityPickup, NeutrinoPickup,
    // Discoveries
    RareChance, CharacterChance, UnlockHydrogen, UnlockArgon, UnlockRadium,
    // Discoveries: two upgrades per character, growing out of its unlock
    IonQuanta, IonNeutrino, NobleDash, NobleBlast, RadiantSides, RadiantWaves,
    // Discoveries: the later characters (their own upgrades are still to come). Saved by name: only append.
    UnlockConductive, UnlockHalogen, UnlockAlkali,
    // The Excited Atom (it took over NobleDash / NobleBlast, which keep their saved names),
    // and the Noble Atom's own two upgrades.
    UnlockExcited, NobleLinger, NobleProclaim,
}

/// <summary>
/// The Lab: upgrades that carry over between runs ("experiments"), bought with Research Data.
/// A run pays (highest atomic number reached with real electrons + 2 x the level it ended on),
/// times the Research bonus. Everything is saved in PlayerPrefs. Purchases are permanent.
/// </summary>
public static class Lab
{
    const string DataKey = "Pendulum_LabData";
    const string RecordKey = "Pendulum_LabRecord";
    const string LevelPrefix = "Pendulum_Lab_";

    public static readonly Color DataColor = new Color(0.45f, 1f, 0.72f);

    // Cost of each level ("fast": the whole Lab in roughly 20 runs).
    static readonly int[][] Costs =
    {
        new[] { 10, 20, 35 },          // Stability
        new[] { 20, 35, 50, 70 },      // StartOrbital
        new[] { 10, 15, 25 },          // StartNeutrino
        new[] { 40 },                  // SecondWind
        new[] { 8, 12, 20, 30, 40 },   // Research
        new[] { 10, 20, 30 },          // ExtraElectrons
        new[] { 8, 15, 25, 35 },       // StabilityPickup
        new[] { 10, 20, 30 },          // NeutrinoPickup
        new[] { 6, 10, 15, 20, 25 },   // RareChance
        new[] { 8, 12, 18, 24, 30 },   // CharacterChance
        new[] { 25 },                  // UnlockHydrogen
        new[] { 25 },                  // UnlockArgon
        new[] { 25 },                  // UnlockRadium
        new[] { 30 },                  // IonQuanta
        new[] { 30 },                  // IonNeutrino
        new[] { 30 },                  // NobleDash
        new[] { 30 },                  // NobleBlast
        new[] { 30 },                  // RadiantSides
        new[] { 30 },                  // RadiantWaves
        new[] { 25 },                  // UnlockConductive
        new[] { 25 },                  // UnlockHalogen
        new[] { 25 },                  // UnlockAlkali
        new[] { 25 },                  // UnlockExcited
        new[] { 30 },                  // NobleLinger
        new[] { 30 },                  // NobleProclaim
    };

    // ---- effect sizes (per level)
    public const float ResearchPerLevel = 0.10f;          // +10% data
    public const float StabilityPickupPerLevel = 0.20f;   // chance per level of a stability point when a level starts hurt
    public const float NeutrinoPickupPerLevel = 0.05f;    // neutrino pickup chance per level (5% base, up to 20%)
    public const float RareChancePerLevel = 0.01f;        // 5% base, up to 10%
    public const float CharacterChancePerLevel = 0.01f;   // 5% base, up to 10%

    // ------------------------------------------------------------- bank

    public static int Data
    {
        get => Mathf.Max(0, PlayerPrefs.GetInt(DataKey, 0));
        private set { PlayerPrefs.SetInt(DataKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    /// <summary>Most Research Data a single run has been worth.</summary>
    public static int Record => PlayerPrefs.GetInt(RecordKey, 0);

    public static void AddData(int amount)
    {
        if (amount > 0) Data += amount;
    }

    /// <summary>Records a run's worth; true if it beat the old record.</summary>
    public static bool SubmitRun(int value)
    {
        if (value <= Record) return false;
        PlayerPrefs.SetInt(RecordKey, value);
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>What a run is worth: highest atomic number + 2 x level, with the Research bonus.</summary>
    public static int RunValue(int atomicNumber, int level) =>
        Mathf.RoundToInt((Mathf.Max(0, atomicNumber) + 2 * Mathf.Max(0, level)) * ResearchMultiplier);

    public static float ResearchMultiplier => 1f + ResearchPerLevel * Level(LabPerk.Research);

    // ------------------------------------------------------------- perks

    public static int MaxLevel(LabPerk p) => Costs[(int)p].Length;
    public static int Level(LabPerk p) => Mathf.Clamp(PlayerPrefs.GetInt(LevelPrefix + p, 0), 0, MaxLevel(p));
    public static bool Maxed(LabPerk p) => Level(p) >= MaxLevel(p);
    public static int NextCost(LabPerk p) => Maxed(p) ? 0 : Costs[(int)p][Level(p)];

    public static int Branch(LabPerk p) => p <= LabPerk.SecondWind ? 0 : p <= LabPerk.NeutrinoPickup ? 1 : 2;

    // The tree forks: Atom and Field split after their root and merge again into a keystone
    // that needs both paths; Discoveries has two roots, and the characters fan out of Mutations.
    static readonly LabPerk[] NoParents = new LabPerk[0];
    static readonly LabPerk[] FromStability = { LabPerk.Stability };
    static readonly LabPerk[] AtomPaths = { LabPerk.StartOrbital, LabPerk.StartNeutrino };
    static readonly LabPerk[] FromResearch = { LabPerk.Research };
    static readonly LabPerk[] FieldPaths = { LabPerk.StabilityPickup, LabPerk.NeutrinoPickup };
    static readonly LabPerk[] FromMutations = { LabPerk.CharacterChance };
    static readonly LabPerk[] FromIon = { LabPerk.UnlockHydrogen };
    static readonly LabPerk[] FromNoble = { LabPerk.UnlockArgon };
    static readonly LabPerk[] FromExcited = { LabPerk.UnlockExcited };
    static readonly LabPerk[] FromRadiant = { LabPerk.UnlockRadium };

    /// <summary>The nodes it grows from; it opens once every one of them has a level. Empty for roots.</summary>
    public static LabPerk[] Parents(LabPerk p)
    {
        switch (p)
        {
            case LabPerk.StartOrbital:
            case LabPerk.StartNeutrino: return FromStability;
            case LabPerk.SecondWind: return AtomPaths;
            case LabPerk.StabilityPickup:
            case LabPerk.NeutrinoPickup: return FromResearch;
            case LabPerk.ExtraElectrons: return FieldPaths;
            case LabPerk.UnlockHydrogen:
            case LabPerk.UnlockArgon:
            case LabPerk.UnlockRadium:
            case LabPerk.UnlockConductive:
            case LabPerk.UnlockHalogen:
            case LabPerk.UnlockAlkali:
            case LabPerk.UnlockExcited: return FromMutations;
            case LabPerk.IonQuanta:
            case LabPerk.IonNeutrino: return FromIon;
            case LabPerk.NobleDash:
            case LabPerk.NobleBlast: return FromExcited;
            case LabPerk.NobleLinger:
            case LabPerk.NobleProclaim: return FromNoble;
            case LabPerk.RadiantSides:
            case LabPerk.RadiantWaves: return FromRadiant;
            default: return NoParents;   // Stability, Research, RareChance, CharacterChance
        }
    }

    public static bool IsOpen(LabPerk p)
    {
        foreach (LabPerk parent in Parents(p))
            if (Level(parent) < 1) return false;
        return true;
    }

    /// <summary>The character a perk unlocks (None for the others).</summary>
    public static GameManager.Character CharacterOf(LabPerk p) =>
        p == LabPerk.UnlockHydrogen ? GameManager.Character.Challenger
        : p == LabPerk.UnlockArgon ? GameManager.Character.Sluggish
        : p == LabPerk.UnlockRadium ? GameManager.Character.Spaceship
        : p == LabPerk.UnlockConductive ? GameManager.Character.Conductive
        : p == LabPerk.UnlockHalogen ? GameManager.Character.Halogen
        : p == LabPerk.UnlockAlkali ? GameManager.Character.Alkali
        : p == LabPerk.UnlockExcited ? GameManager.Character.Excited
        : GameManager.Character.None;

    /// <summary>Every character the Lab can unlock, in the order the chooser shows them.</summary>
    public static readonly GameManager.Character[] Characters =
    {
        GameManager.Character.Challenger, GameManager.Character.Sluggish, GameManager.Character.Spaceship,
        GameManager.Character.Excited, GameManager.Character.Conductive, GameManager.Character.Halogen,
        GameManager.Character.Alkali,
    };

    /// <summary>A character can only be researched once it has been met in a run (it's in the Index).</summary>
    public static bool Discovered(LabPerk p)
    {
        GameManager.Character c = CharacterOf(p);
        return c == GameManager.Character.None || Codex.Seen(Codex.Upgrade(GameManager.CharacterCard(c)));
    }

    public static bool CanBuy(LabPerk p) => IsOpen(p) && Discovered(p) && !Maxed(p) && Data >= NextCost(p);

    public static bool Buy(LabPerk p)
    {
        if (!CanBuy(p)) return false;
        int cost = NextCost(p);
        Data -= cost;
        PlayerPrefs.SetInt(LevelPrefix + p, Level(p) + 1);
        PlayerPrefs.Save();
        return true;
    }

    // ------------------------------------------------------------- characters

    public static bool CharacterUnlocked(GameManager.Character c)
    {
        switch (c)
        {
            case GameManager.Character.Challenger: return Level(LabPerk.UnlockHydrogen) > 0;
            case GameManager.Character.Sluggish: return Level(LabPerk.UnlockArgon) > 0;
            case GameManager.Character.Spaceship: return Level(LabPerk.UnlockRadium) > 0;
            case GameManager.Character.Conductive: return Level(LabPerk.UnlockConductive) > 0;
            case GameManager.Character.Halogen: return Level(LabPerk.UnlockHalogen) > 0;
            case GameManager.Character.Alkali: return Level(LabPerk.UnlockAlkali) > 0;
            case GameManager.Character.Excited: return Level(LabPerk.UnlockExcited) > 0;
            default: return true;
        }
    }

    public static bool Has(LabPerk p) => Level(p) > 0;

    // ------------------------------------------------------------- Excited Atom ability

    const string NobleEquipKey = "Pendulum_Lab_NobleEquip";

    /// <summary>
    /// The Excited Atom brings one ability: the equipped one if researched, otherwise whichever
    /// is researched. Null when neither is.
    /// </summary>
    public static LabPerk? ExcitedAbility
    {
        get
        {
            var eq = (LabPerk)PlayerPrefs.GetInt(NobleEquipKey, (int)LabPerk.NobleDash);
            if ((eq == LabPerk.NobleDash || eq == LabPerk.NobleBlast) && Has(eq)) return eq;
            if (Has(LabPerk.NobleDash)) return LabPerk.NobleDash;
            if (Has(LabPerk.NobleBlast)) return LabPerk.NobleBlast;
            return null;
        }
    }

    public static bool IsExcitedAbility(LabPerk p) => p == LabPerk.NobleDash || p == LabPerk.NobleBlast;

    /// <summary>Makes a researched ability the one the Excited Atom brings.</summary>
    public static bool Equip(LabPerk p)
    {
        if (!IsExcitedAbility(p) || !Has(p) || ExcitedAbility == p) return false;
        PlayerPrefs.SetInt(NobleEquipKey, (int)p);
        PlayerPrefs.Save();
        return true;
    }

    public static bool AnyCharacterUnlocked
    {
        get
        {
            foreach (var c in Characters)
                if (CharacterUnlocked(c)) return true;
            return false;
        }
    }
}
