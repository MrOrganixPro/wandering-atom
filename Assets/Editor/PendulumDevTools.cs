using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only shortcuts for testing (menu: Pendulum > Dev). Not part of the game build.
/// Every action saves at once and logs what it did to the Console. Menus opened in the game
/// (Index, Lab) show the change the next time they are opened.
/// </summary>
public static class PendulumDevTools
{
    const string Root = "Pendulum/Dev/";
    const string IndexMenu = Root + "Index/";
    const string LabMenu = Root + "Lab/";
    const string DataMenu = Root + "Research Data/";
    const string TutorialMenu = Root + "Tutorials/";
    const string SaveMenu = Root + "Save/";

    // ================================================================ Index

    static IEnumerable<string> PowerUpIds()
    {
        foreach (GameManager.UpgradeType u in System.Enum.GetValues(typeof(GameManager.UpgradeType))) yield return Codex.Upgrade(u);
    }

    static IEnumerable<string> EnemyIds()
    {
        foreach (EnemyType t in System.Enum.GetValues(typeof(EnemyType))) yield return Codex.Enemy(t);
        foreach (HazardType t in System.Enum.GetValues(typeof(HazardType))) yield return Codex.Hazard(t);
    }

    static readonly PickupKind[] Particles = { PickupKind.Star, PickupKind.Orb, PickupKind.Coin, PickupKind.Neutrino, PickupKind.Heart };

    static IEnumerable<string> ParticleIds()
    {
        foreach (var k in Particles) yield return Codex.Particle(k);
    }

    static IEnumerable<string> AllIds()
    {
        foreach (var id in PowerUpIds()) yield return id;
        foreach (var id in EnemyIds()) yield return id;
        foreach (var id in ParticleIds()) yield return id;
    }

    static void SeeAll(IEnumerable<string> ids, string what)
    {
        int n = 0;
        foreach (var id in ids) { Codex.See(id); n++; }
        Debug.Log("Pendulum dev: Index " + what + " complete (" + n + " entries).");
    }

    static void ForgetAll(IEnumerable<string> ids, string what)
    {
        int n = 0;
        foreach (var id in ids) { Codex.Forget(id); n++; }
        Debug.Log("Pendulum dev: Index " + what + " reset (" + n + " entries back to \"?\").");
    }

    [MenuItem(IndexMenu + "Complete All", false, 0)] static void IndexAll() => SeeAll(AllIds(), "(everything)");
    [MenuItem(IndexMenu + "Complete Power-ups", false, 11)] static void IndexPowerUps() => SeeAll(PowerUpIds(), "power-ups");
    [MenuItem(IndexMenu + "Complete Enemies + Hazards", false, 12)] static void IndexEnemies() => SeeAll(EnemyIds(), "enemies");
    [MenuItem(IndexMenu + "Complete Particles", false, 13)] static void IndexParticles() => SeeAll(ParticleIds(), "particles");
    [MenuItem(IndexMenu + "Reset All", false, 30)] static void IndexReset() => ForgetAll(AllIds(), "(everything)");

    // ================================================================ Lab

    static LabPerk[] AllPerks => (LabPerk[])System.Enum.GetValues(typeof(LabPerk));

    [MenuItem(LabMenu + "Reset Research (keep data)", false, 0)]
    static void LabReset()
    {
        Lab.DevResetResearch();
        Debug.Log("Pendulum dev: every Lab perk forgotten. Data kept: " + Lab.Data + ".");
    }

    [MenuItem(LabMenu + "Research Everything (free)", false, 1)]
    static void LabMaxAll()
    {
        foreach (var p in AllPerks) Lab.DevSetLevel(p, Lab.MaxLevel(p));
        Debug.Log("Pendulum dev: every Lab perk maxed (no data spent).");
    }

    [MenuItem(LabMenu + "Unlock Every Character (free)", false, 12)]
    static void LabCharacters()
    {
        foreach (var p in AllPerks)
            if (Lab.CharacterOf(p) != GameManager.Character.None)
            {
                Lab.DevSetLevel(p, 1);
                Codex.See(Codex.Upgrade(GameManager.CharacterCard(Lab.CharacterOf(p))));   // discovered too
            }
        Debug.Log("Pendulum dev: every character unlocked for the start chooser.");
    }

    [MenuItem(LabMenu + "Reset Research + Data To Buy Everything", false, 30)]
    static void LabResetAndFund()
    {
        Lab.DevResetResearch();
        Lab.DevSetData(Lab.TotalCost);
        Debug.Log("Pendulum dev: Lab reset; bank set to " + Lab.TotalCost + " data (exactly enough for everything).");
    }

    // ================================================================ Research Data

    static void AddData(int n)
    {
        Lab.DevSetData(Lab.Data + n);
        Debug.Log("Pendulum dev: +" + n + " data. Bank: " + Lab.Data + ".");
    }

    [MenuItem(DataMenu + "+50", false, 0)] static void Data50() => AddData(50);
    [MenuItem(DataMenu + "+250", false, 1)] static void Data250() => AddData(250);
    [MenuItem(DataMenu + "+1000", false, 2)] static void Data1000() => AddData(1000);

    [MenuItem(DataMenu + "Set To Buy What's Left", false, 13)]
    static void DataRemaining()
    {
        Lab.DevSetData(Lab.RemainingCost);
        Debug.Log("Pendulum dev: bank set to " + Lab.Data + " data (exactly what the unresearched perks cost).");
    }

    [MenuItem(DataMenu + "Clear (0)", false, 30)]
    static void DataClear()
    {
        Lab.DevSetData(0);
        Debug.Log("Pendulum dev: bank emptied.");
    }

    [MenuItem(DataMenu + "Clear Best-Run Record", false, 31)]
    static void DataRecord()
    {
        Lab.DevClearRecord();
        Debug.Log("Pendulum dev: best-run record cleared (the next run sets a new one).");
    }

    // ================================================================ Tutorials

    [MenuItem(TutorialMenu + "Show Story Again", false, 0)]
    static void TutorialStory()
    {
        PlayerPrefs.DeleteKey(GameManager.StoryKey);
        PlayerPrefs.Save();
        Debug.Log("Pendulum dev: the story plays on the next PLAY from the title.");
    }

    [MenuItem(TutorialMenu + "Show Lessons + First-Run Guide Again", false, 1)]
    static void TutorialLessons()
    {
        foreach (var k in GameManager.TutorialKeys) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();
        Debug.Log("Pendulum dev: energy level / noble gas / halogen / alkali lessons and the photon-electron-quantum guide will show again.");
    }

    // ================================================================ Save

    [MenuItem(SaveMenu + "Log Current State", false, 0)]
    static void SaveLog()
    {
        int seen = 0, total = 0;
        foreach (var id in AllIds()) { total++; if (Codex.Seen(id)) seen++; }
        int lvls = 0, maxLvls = 0;
        foreach (var p in AllPerks) { lvls += Lab.Level(p); maxLvls += Lab.MaxLevel(p); }
        var chars = new List<string>();
        foreach (var c in Lab.Characters) if (Lab.CharacterUnlocked(c)) chars.Add(c.ToString());
        Debug.Log("Pendulum dev: Index " + seen + "/" + total + " · Lab " + lvls + "/" + maxLvls + " levels · Data " + Lab.Data
                  + " (whole Lab costs " + Lab.TotalCost + ") · Record " + Lab.Record
                  + " · Characters: " + (chars.Count > 0 ? string.Join(", ", chars) : "none"));
    }

    [MenuItem(SaveMenu + "Wipe ALL Save Data...", false, 30)]
    static void SaveWipe()
    {
        if (!EditorUtility.DisplayDialog("Wipe all save data?",
                "Index, Lab, Research Data, best level, story, lessons, settings: everything this game saved is deleted. This can't be undone.",
                "Wipe", "Cancel")) return;
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Codex.ClearCache();
        Debug.Log("Pendulum dev: all save data wiped.");
    }
}
