using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the player has discovered (shown in the Index). Kept across runs and launches.
/// Upgrades count as seen once offered on a card (a Character box once it opens);
/// enemies and hazards once they appear.
/// </summary>
public static class Codex
{
    const string Prefix = "Seen_";
    static readonly Dictionary<string, bool> cache = new Dictionary<string, bool>();

    public static string Upgrade(GameManager.UpgradeType u) => "upg_" + u;
    public static string Enemy(EnemyType t) => "enemy_" + t;
    public static string Hazard(HazardType t) => "hazard_" + t;

    public static bool Seen(string id)
    {
        if (!cache.TryGetValue(id, out bool seen))
        {
            seen = PlayerPrefs.GetInt(Prefix + id, 0) == 1;
            cache[id] = seen;
        }
        return seen;
    }

    public static void See(string id)
    {
        if (Seen(id)) return;
        cache[id] = true;
        PlayerPrefs.SetInt(Prefix + id, 1);
        PlayerPrefs.Save();
    }
}
