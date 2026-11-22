using UnityEditor;
using UnityEngine;

/// <summary>Editor-only shortcuts for testing (menu: Pendulum > Dev). Not part of the game build.</summary>
public static class PendulumDevTools
{
    [MenuItem("Pendulum/Dev/Complete Index")]
    public static void CompleteIndex()
    {
        int n = 0;
        foreach (GameManager.UpgradeType u in System.Enum.GetValues(typeof(GameManager.UpgradeType))) { Codex.See(Codex.Upgrade(u)); n++; }
        foreach (EnemyType t in System.Enum.GetValues(typeof(EnemyType))) { Codex.See(Codex.Enemy(t)); n++; }
        foreach (HazardType t in System.Enum.GetValues(typeof(HazardType))) { Codex.See(Codex.Hazard(t)); n++; }
        foreach (var k in new[] { PickupKind.Star, PickupKind.Orb, PickupKind.Coin, PickupKind.Neutrino, PickupKind.Heart })
        { Codex.See(Codex.Particle(k)); n++; }
        PlayerPrefs.Save();
        Debug.Log("Pendulum dev: Index complete (" + n + " entries seen).");
    }

    [MenuItem("Pendulum/Dev/Reset Lab + Data To Buy Everything")]
    public static void ResetAndFundLab()
    {
        int total = Lab.TotalCost;
        Lab.DevReset(total);
        Debug.Log("Pendulum dev: Lab reset; bank set to " + total + " data (exactly enough to research everything).");
    }

    [MenuItem("Pendulum/Dev/Complete Index + Reset And Fund Lab")]
    public static void Both()
    {
        CompleteIndex();
        ResetAndFundLab();
    }
}
