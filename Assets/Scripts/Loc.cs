using UnityEngine;

public enum Language { English = 0, Turkish = 1 }

/// <summary>
/// Tiny localization: every player-facing string is written inline as Loc.T("English", "Türkçe").
/// The choice is remembered; on first launch it follows the system language.
/// Changing it fires Changed (GameUI rebuilds itself).
/// </summary>
public static class Loc
{
    const string PrefKey = "Language";
    static int current = -1;

    public static event System.Action Changed;

    public static Language Current
    {
        get
        {
            if (current < 0)
            {
                int saved = PlayerPrefs.GetInt(PrefKey, -1);
                current = saved >= 0 ? saved
                        : Application.systemLanguage == SystemLanguage.Turkish ? (int)Language.Turkish : (int)Language.English;
            }
            return (Language)current;
        }
        set
        {
            if (Current == value) return;
            current = (int)value;
            PlayerPrefs.SetInt(PrefKey, current);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    public static string T(string en, string tr) => Current == Language.Turkish ? tr : en;

    /// <summary>"+20%" in English, "+%20" in Turkish.</summary>
    public static string Pct(float fraction)
    {
        int p = Mathf.RoundToInt(fraction * 100f);
        return Current == Language.Turkish ? "+%" + p : "+" + p + "%";
    }
}
