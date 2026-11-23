using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the player has discovered (shown in the Index). Kept across runs and launches.
/// Upgrades count as seen once offered on a card (a Character box once it opens);
/// enemies and hazards once they appear; particles (photons, electrons...) once collected.
/// </summary>
public static class Codex
{
    const string Prefix = "Seen_";
    static readonly Dictionary<string, bool> cache = new Dictionary<string, bool>();

    public static string Upgrade(GameManager.UpgradeType u) => "upg_" + u;
    public static string Enemy(EnemyType t) => "enemy_" + t;
    public static string Hazard(HazardType t) => "hazard_" + t;
    public static string Particle(PickupKind k) => "particle_" + k;

    public static bool Seen(string id)
    {
        if (!cache.TryGetValue(id, out bool seen))
        {
            seen = PlayerPrefs.GetInt(Prefix + id, 0) == 1;
            cache[id] = seen;
        }
        return seen;
    }

    /// <summary>Dev: marks an entry as never seen again.</summary>
    public static void Forget(string id)
    {
        cache[id] = false;
        PlayerPrefs.DeleteKey(Prefix + id);
        PlayerPrefs.Save();
    }

    /// <summary>Dev: drops the in-memory cache (after the saved data was wiped from outside).</summary>
    public static void ClearCache() => cache.Clear();

    public static void See(string id)
    {
        if (Seen(id)) return;
        cache[id] = true;
        PlayerPrefs.SetInt(Prefix + id, 1);
        PlayerPrefs.Save();
    }
}
