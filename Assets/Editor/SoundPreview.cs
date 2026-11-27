using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pendulum > Sound Preview: every sound effect with a Play button, and the original version
/// next to each redesigned one, so they can be compared without playing the game.
/// Works in Edit mode (its own hidden audio host) and in Play mode (the game's).
/// </summary>
public class SoundPreview : EditorWindow
{
    static GameObject host;
    Vector2 scroll;
    string filter = "";

    [MenuItem("Pendulum/Sound Preview")]
    static void Open() => GetWindow<SoundPreview>("Sound Preview");

    void OnEnable() => EditorApplication.playModeStateChanged += OnPlayMode;

    void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayMode;
        DestroyHost();
    }

    static void OnPlayMode(PlayModeStateChange change)
    {
        // The game sets up its own sounds in Play mode; ours is rebuilt when needed.
        if (change == PlayModeStateChange.ExitingEditMode || change == PlayModeStateChange.EnteredEditMode) DestroyHost();
    }

    static void DestroyHost()
    {
        if (host != null) DestroyImmediate(host);
        host = null;
    }

    static void EnsureReady(bool rebuild = false)
    {
        if (Application.isPlaying) return;   // the game's GameManager owns Sfx then
        if (rebuild) DestroyHost();
        if (host != null && Sfx.Ready) return;
        DestroyHost();
        host = new GameObject("SoundPreviewHost") { hideFlags = HideFlags.HideAndDontSave };
        Sfx.Init(host);
    }

    void OnGUI()
    {
        EnsureReady();
        EditorGUILayout.BeginHorizontal();
        filter = EditorGUILayout.TextField(filter, EditorStyles.toolbarSearchField);
        if (!Application.isPlaying && GUILayout.Button(new GUIContent("Rebuild", "Re-render every sound (after editing Sfx.cs and recompiling this happens on its own)."), GUILayout.Width(70)))
            EnsureReady(true);
        EditorGUILayout.EndHorizontal();
        if (!Sfx.Ready)
        {
            EditorGUILayout.HelpBox("Sounds are not set up yet.", MessageType.Info);
            return;
        }

        var all = new SortedSet<string>(Sfx.Names);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (string name in all)
        {
            if (name.Contains("#")) continue;   // a version of another sound: shown on its row
            if (filter.Length > 0 && name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            var versions = new List<string>();
            foreach (string other in all)
                if (other.StartsWith(name + "#")) versions.Add(other.Substring(name.Length + 1));
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(versions.Count > 0 ? name + "  (new)" : name, GUILayout.Width(170));
            GUILayout.Label(Sfx.Length(name).ToString("0.00") + " s", EditorStyles.miniLabel, GUILayout.Width(50));
            if (GUILayout.Button("Play", GUILayout.Width(60))) Sfx.Play(name);
            foreach (string v in versions)
                if (GUILayout.Button(char.ToUpper(v[0]) + v.Substring(1), GUILayout.Width(55))) Sfx.Play(name + "#" + v);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.HelpBox("\"Old\" plays the original synthesized version; \"Raw\" an untouched recording, \"Synth\" the designed sound it replaced. Redesigned sounds vary slightly in pitch on every play, as in the game.", MessageType.None);
    }
}
