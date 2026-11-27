using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sound effects: synthesized ones, replaced by recordings in Assets/Resources/Sfx where there
/// are some (see Files), plus the enemy-approaching set in Resources/Sfx/Approach.
/// </summary>
public static class Sfx
{
    static AudioSource src;
    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

    public static void Init(GameObject host)
    {
        src = host.GetComponent<AudioSource>();
        if (src == null) src = host.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        InitVoices(host);

        clips.Clear();
        clips["warn"]  = Synth("warn", 0.09f, t => 880f + t * 2500f, 0f, 25f, 0.22f);
        clips["dodge"] = Synth("dodge", 0.05f, t => 1500f, 0f, 60f, 0.10f);
        clips["hit"]   = Synth("hit", 0.35f, t => 220f * Mathf.Exp(-t * 6f), 0.5f, 9f, 0.75f);
        clips["over"]  = Synth("over", 0.9f, t => 330f * Mathf.Exp(-t * 1.5f), 0.12f, 3f, 0.6f);
        clips["start"] = Synth("start", 0.25f, t => 440f + t * 1600f, 0f, 8f, 0.35f);
        clips["kick"]  = Synth("kick", 0.08f, t => 300f + t * 1500f, 0.35f, 30f, 0.25f);
        clips["snap"]  = Synth("snap", 0.06f, t => 160f, 0.7f, 45f, 0.35f);
        // Orbitals full: a low, square-ish double buzz ("nope").
        clips["full"]  = Synth("full", 0.2f, t => t < 0.09f ? 220f : 165f, 0.05f, 6f, 0.4f, 0.8f);
        // Orb recycled: a quick falling chirp.
        clips["recycle"] = Synth("recycle", 0.14f, t => 1400f - t * 5000f, 0f, 14f, 0.28f);
        // Broken Thruster push: a rough, coughing low burst.
        clips["sputter"] = Synth("sputter", 0.22f, t => 70f + 50f * Mathf.Abs(Mathf.Sin(t * 120f)), 0.6f, 11f, 0.55f, 0.6f);
        clips["star"]  = Synth("star", 0.35f, t => t < 0.08f ? 988f : 1480f, 0f, 7f, 0.4f);
        clips["orb"]   = Synth("orb", 0.22f, t => 600f + t * 3000f, 0f, 9f, 0.35f);
        clips["coin"]  = Synth("coin", 0.25f, t => t < 0.06f ? 1320f : 1760f, 0f, 10f, 0.3f);
        clips["lock"]   = Synth("lock", 0.05f, t => 1900f, 0f, 30f, 0.18f);
        clips["charge"] = Synth("charge", 1.3f, t => 250f + t * 900f, 0.25f, 0.6f, 0.18f);
        clips["zap"]    = Synth("zap", 0.3f, t => 2400f * Mathf.Exp(-t * 8f) + 200f, 0.6f, 9f, 0.55f);
        clips["pop"]   = Synth("pop", 0.15f, t => 500f * Mathf.Exp(-t * 12f), 0.4f, 18f, 0.5f);
        clips["repel"] = Synth("repel", 0.4f, t => 140f + 260f * Mathf.Exp(-t * 10f), 0.45f, 7f, 0.55f);
        clips["boost"] = Synth("boost", 0.45f, t => 220f + t * 2200f, 0.3f, 4.5f, 0.4f);
        // Spaceship turret: a short, buzzy laser chirp.
        clips["shoot"] = Synth("shoot", 0.11f, t => 1750f * Mathf.Exp(-t * 18f) + 420f, 0.06f, 20f, 0.32f, 0.5f);
        clips["reveal"] = Reveal();
        clips["neutrino"] = Neutrino();
        // Core meltdown: a low, rising, trembling rumble; then the blast.
        clips["meltdown"] = Synth("meltdown", 1.2f, t => 55f + 170f * t * t + 25f * Mathf.Sin(t * 70f), 0.45f, 0.4f, 0.5f, 0.3f);
        clips["explode"] = Synth("explode", 1.5f, t => 95f * Mathf.Exp(-t * 3f) + 28f, 0.75f, 3.2f, 0.95f);
        clips["level"] = Synth("level", 0.9f, t => t < 0.15f ? 523f : t < 0.3f ? 659f : t < 0.45f ? 784f : 1046f, 0f, 2.5f, 0.45f);

        // Redesigned with the toolkit below (the originals stay as "name#old", for the
        // Pendulum > Sound Preview window).
        Redesign("orb", Orb());
        Redesign("hit", Hit());
        clips["positronium"] = Positronium();   // used to be "charge", which outlasted the spiral
        clips["annihilate"] = Annihilate();      // used to be "zap"
        clips["heal"] = Heal();                  // these five used to be "orb" too
        clips["pick"] = Pick();
        clips["neutrinoLand"] = NeutrinoLand();
        clips["uiOpen"] = UiClick("uiOpen", false);
        clips["research"] = UiClick("research", true);
        Redesign("coin", Coin());
        Redesign("star", Star());
        Redesign("recycle", Recycle());
        Redesign("full", Full());
        Redesign("pop", Pop());
        Redesign("snap", Snap());
        Redesign("zap", Zap());
        Redesign("boost", Boost());
        Redesign("repel", Repel());
        Redesign("charge", Charge());
        Redesign("shoot", Shoot());
        Redesign("sputter", Sputter());
        Redesign("explode", Explode());
        Redesign("meltdown", Meltdown());
        Redesign("lock", Lock());
        clips["tick"] = Tick();          // these three used to be "lock" too
        clips["block"] = Block();
        clips["conduct"] = Conduct();
        Redesign("warn", Warn());
        Redesign("start", StartSound());
        Redesign("level", Level());
        Redesign("reveal", RevealSound());

        // Moments that used to share a synthesized sound; each gets its own key now, starting
        // from the old sound in case its recording is missing.
        clips["levelStart"] = clips["start"];
        clips["levelComplete"] = clips["level"];
        clips["energy"] = clips["level"];
        clips["gammaRain"] = clips["charge"];
        clips["secondWind"] = clips["meltdown"];

        // Recorded sounds (Assets/Resources/Sfx) replace the synthesized ones.
        foreach (var (key, file) in Files)
        {
            var clip = Resources.Load<AudioClip>("Sfx/" + file);
            if (clip == null) { Debug.LogWarning("Sfx: missing Resources/Sfx/" + file + ", using the synthesized sound."); continue; }
            Tuning tuning = System.Array.Find(Tunings, x => x.key == key);
            AudioClip tuned = tuning != null ? Tune(clip, tuning) : null;
            if (tuning != null && tuned == null) Debug.LogWarning("Sfx: couldn't read Resources/Sfx/" + file + " to tune it; using it as it is.");
            if (tuned == null) { clips[key] = clip; continue; }
            if (key == "explode") clips["explode#synth"] = clips["explode"];   // the designed one it replaced
            clips[key + "#raw"] = clip;
            clips[key] = tuned;
        }
        LoadApproach();
    }

    /// <summary>Sound key -> recording in Resources/Sfx.</summary>
    static readonly (string key, string file)[] Files =
    {
        ("over", "game_over_sfx"),
        ("levelStart", "start_sfx"),
        ("levelComplete", "level_sfx"),
        ("energy", "energy_level_completed_sfx"),
        ("gammaRain", "gamma_rain_sfx"),
        ("neutrino", "neutrino_earned_sfx"),
        ("secondWind", "second_wind_charging_sfx"),
        ("explode", "Explosion"),
    };

    /// <summary>Length in seconds of a sound (0 if there is none).</summary>
    public static float Length(string name) =>
        clips.TryGetValue(name, out AudioClip c) && c != null ? c.length : 0f;

    // ------------------------------------------------------------ approach

    /// <summary>The enemy-approaching sounds (Resources/Sfx/Approach), lowest pitch first.</summary>
    static AudioClip[] approach = new AudioClip[0];

    /// <summary>Plays the approach sound for 'pace' (0 = slowest spawning, lowest pitch; 1 = fastest, highest).</summary>
    public static void PlayApproach(float pace, float volume = 1f)
    {
        if (src == null) return;
        if (approach.Length == 0) { Play("warn", volume); return; }
        int i = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(pace) * approach.Length), 0, approach.Length - 1);
        src.PlayOneShot(approach[i], volume);
    }

    /// <summary>Loads the approach sounds and sorts them by their measured pitch.</summary>
    static void LoadApproach()
    {
        var loaded = Resources.LoadAll<AudioClip>("Sfx/Approach");
        var list = new List<(AudioClip clip, float hz)>();
        foreach (var c in loaded) if (c != null) list.Add((c, EstimatePitch(c)));
        // Unmeasurable ones (0 Hz) keep their file order, after sorting by name.
        list.Sort((a, b) => string.CompareOrdinal(a.clip.name, b.clip.name));
        if (list.TrueForAll(x => x.hz > 0f)) list.Sort((a, b) => a.hz.CompareTo(b.hz));
        approach = list.ConvertAll(x => x.clip).ToArray();

        var order = new System.Text.StringBuilder("Sfx: approach sounds, low to high:");
        foreach (var x in list) order.Append($" {x.clip.name} ({x.hz:0} Hz)");
        Debug.Log(order.ToString());
    }

    /// <summary>
    /// Fundamental frequency of the loudest part of a clip (normalized autocorrelation over a
    /// short window), or 0 if it can't be read or has no clear pitch.
    /// </summary>
    static float EstimatePitch(AudioClip clip)
    {
        float[] mono = MonoData(clip);
        int rate = clip.frequency;
        if (mono == null || mono.Length < PitchWin * 2) return 0f;

        // Loudest window.
        int best = 0;
        float bestE = -1f;
        for (int s = 0; s + PitchWin * 2 <= mono.Length; s += 512)
        {
            float e = 0f;
            for (int i = 0; i < PitchWin; i++) e += mono[s + i] * mono[s + i];
            if (e > bestE) { bestE = e; best = s; }
        }
        if (bestE <= 1e-6f) return 0f;
        float hz = PitchAt(mono, best, rate, out float clarity);
        return clarity >= 0.5f ? hz : 0f;   // below that: noise, no clear pitch
    }

    const int PitchWin = 2048;

    /// <summary>
    /// Pitch of the window starting at 'start' (needs a window plus the longest lag after it),
    /// with how clearly periodic it is (0..1). 0 Hz if there is none between 50 Hz and 2 kHz.
    /// </summary>
    static float PitchAt(float[] mono, int start, int rate, out float clarity)
    {
        clarity = 0f;
        int minLag = Mathf.Max(2, rate / 2000), maxLag = Mathf.Min(PitchWin, rate / 50);
        if (start + PitchWin + maxLag >= mono.Length) return 0f;
        var nsdf = new float[maxLag + 1];
        float top = 0f;
        for (int lag = minLag; lag <= maxLag; lag++)
        {
            float ac = 0f, m = 0f;
            for (int i = 0; i < PitchWin; i++)
            {
                float a = mono[start + i], b = mono[start + i + lag];
                ac += a * b;
                m += a * a + b * b;
            }
            nsdf[lag] = m > 0f ? 2f * ac / m : 0f;
            top = Mathf.Max(top, nsdf[lag]);
        }
        clarity = top;
        if (top <= 0f) return 0f;

        // The first peak close to the best one (avoids picking a lower octave).
        for (int lag = minLag + 1; lag < maxLag; lag++)
        {
            float v = nsdf[lag];
            if (v >= 0.9f * top && v >= nsdf[lag - 1] && v >= nsdf[lag + 1])
            {
                float a = nsdf[lag - 1], b = v, c = nsdf[lag + 1];
                float d = a - 2f * b + c;
                float shift = Mathf.Abs(d) > 1e-6f ? 0.5f * (a - c) / d : 0f;   // parabolic refinement
                return rate / (lag + shift);
            }
        }
        return 0f;
    }

    static float[] MonoData(AudioClip clip)
    {
        if (clip.loadState != AudioDataLoadState.Loaded && !clip.LoadAudioData()) return null;
        int ch = Mathf.Max(1, clip.channels);
        var raw = new float[clip.samples * ch];
        if (!clip.GetData(raw, 0)) return null;
        if (ch == 1) return raw;
        var mono = new float[clip.samples];
        for (int i = 0; i < mono.Length; i++)
        {
            float s = 0f;
            for (int c = 0; c < ch; c++) s += raw[i * ch + c];
            mono[i] = s / ch;
        }
        return mono;
    }

    // ------------------------------------------------------- recordings

    /// <summary>How a recording is fitted to the designed sounds (see Tune).</summary>
#pragma warning disable 0649   // some options are only there for recordings that need them
    class Tuning
    {
        public string key;
        public float peak = 0.5f;        // loudness it's normalized to (the designed sounds use 0.3..0.9)
        public float highCut = 20000f;   // Hz: rounds off a harsh top end
        public float room = 0.3f;        // how much of the shared room
        public float maxLength = 60f;    // seconds; longer ones fade out over their last half second
        public bool toKey;               // shift its notes into D minor (the music's key)
        public float pitch;              // extra shift, in semitones
        public float sub;                // a deep punch under the hit (120 to 35 Hz), 0..1
        public float thump;              // a body blow under the hit (180 to 60 Hz, quicker), 0..1
        public float crack;              // a bright crack at the very start, 0..1
    }
#pragma warning restore 0649

    static readonly Tuning[] Tunings =
    {
        // The arpeggios: in key, a little softer on top, in the room.
        new Tuning { key = "levelStart", peak = 0.4f, highCut = 10000f, room = 0.3f, toKey = true },
        new Tuning { key = "levelComplete", peak = 0.5f, highCut = 10000f, room = 0.35f, toKey = true },
        new Tuning { key = "neutrino", peak = 0.45f, highCut = 12000f, room = 0.4f, toKey = true },
        // Game over: in key, a touch softer on top and in the room.
        new Tuning { key = "over", peak = 0.6f, highCut = 9000f, room = 0.25f, toKey = true },
        new Tuning { key = "explode", peak = 0.9f, highCut = 6500f, room = 0.3f, maxLength = 2.2f, sub = 0.35f },
    };

    /// <summary>
    /// Fits a recording to the designed sounds: starts it on its first sound (no silence before
    /// it), shifts it into D minor if asked (and/or by 'pitch'), rounds off the highs, layers
    /// synthesized punch under the hit, puts it in the shared room, caps its length and
    /// normalizes it. Null if the clip can't be read.
    /// </summary>
    static AudioClip Tune(AudioClip raw, Tuning tu)
    {
        if (raw.loadState != AudioDataLoadState.Loaded && !raw.LoadAudioData()) return null;
        int ch = Mathf.Max(1, raw.channels), rate = raw.frequency;
        var src = new float[raw.samples * ch];
        if (!raw.GetData(src, 0)) return null;

        float peak = 0f;
        foreach (float x in src) peak = Mathf.Max(peak, Mathf.Abs(x));
        if (peak <= 1e-5f) return null;
        int start = 0;   // first frame louder than 3% of the peak, minus 5 ms
        while (start < raw.samples && Mathf.Abs(src[start * ch]) < peak * 0.03f && Mathf.Abs(src[start * ch + ch - 1]) < peak * 0.03f) start++;
        start = Mathf.Max(0, start - rate / 200);

        float keyCents = 0f;
        string notes = "";
        if (tu.toKey) keyCents = KeyShift(src, ch, start, rate, out notes);
        float cents = keyCents + tu.pitch * 100f;
        float ratio = Mathf.Pow(2f, cents / 1200f);   // > 1: higher (and shorter)

        int left = (int)((raw.samples - start - 1) / ratio);
        int body = Mathf.Max(1, Mathf.Min(left, (int)(tu.maxLength * rate)));
        bool capped = body < left;
        int fade = capped ? Mathf.Min(body, rate / 2) : Mathf.Min(body, rate / 50);
        int total = body + rate / 2;   // room tail
        bool cut = tu.highCut < 19000f;
        var lpL = new Svf { rate = rate };
        var lpR = new Svf { rate = rate };
        var crackF = new Svf { rate = rate };
        var rng = new System.Random(113);
        var room = new Room();
        var data = new float[total * 2];
        float max = 1e-6f;
        for (int i = 0; i < total; i++)
        {
            float l = 0f, r = 0f;
            if (i < body)
            {
                float pos = start + i * ratio;
                int j = (int)pos;
                float f = pos - j;
                int a = j * ch, b = Mathf.Min(j + 1, raw.samples - 1) * ch;
                l = src[a] + (src[b] - src[a]) * f;
                r = src[a + ch - 1] + (src[b + ch - 1] - src[a + ch - 1]) * f;
                if (cut)
                {
                    lpL.Run(l, tu.highCut, 0.7f);
                    lpR.Run(r, tu.highCut, 0.7f);
                    l = lpL.Low;
                    r = lpR.Low;
                }

                float t = i / (float)rate, layer = 0f;
                if (tu.sub > 0f) layer += Mathf.Sin(Tau * Glide(t, 120f, 35f, 5f)) * Env(t, 0.002f, 3.2f) * tu.sub;
                if (tu.thump > 0f) layer += Mathf.Sin(Tau * Glide(t, 180f, 60f, 25f)) * Env(t, 0.001f, 14f) * tu.thump;
                if (tu.crack > 0f)
                {
                    crackF.Run(Noise(rng), 3500f, 0.7f);
                    layer += crackF.High * Env(t, 0.0005f, 90f) * tu.crack * 1.5f;
                }
                float e = i >= body - fade ? (body - i) / (float)fade : 1f;
                e *= e;
                l = (l + layer * peak) * e;
                r = (r + layer * peak) * e;
            }
            room.Process((l + r) * 0.5f, out float rl, out float rr);
            float L = l + rl * tu.room, R = r + rr * tu.room;
            data[2 * i] = L;
            data[2 * i + 1] = R;
            max = Mathf.Max(max, Mathf.Max(Mathf.Abs(L), Mathf.Abs(R)));
        }
        float g = tu.peak / max;
        int tailFade = rate / 20;
        for (int i = 0; i < total; i++)
        {
            float e = i >= total - tailFade ? (total - i) / (float)tailFade : 1f;
            data[2 * i] *= g * e;
            data[2 * i + 1] *= g * e;
        }
        var clip = AudioClip.Create(tu.key, total, 2, rate, false);
        clip.SetData(data, 0);
        string keyNote = tu.toKey ? ". Notes found: " + (notes.Length > 0 ? notes : "none, so not shifted into key") : "";
        Debug.Log($"Sfx: tuned {tu.key} ({raw.length:0.00} s, {ch} ch, {rate} Hz): trimmed {start / (float)rate:0.000} s of lead-in, "
                + $"shifted {cents:0} cents, kept {body / (float)rate:0.00} s{(capped ? " (capped)" : "")}{keyNote}.");
        return clip;
    }

    static readonly int[] DMinor = { 2, 4, 5, 7, 9, 10, 0 };   // pitch classes: D E F G A Bb C
    static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "Bb", "B" };

    /// <summary>
    /// How many cents to shift a recording so its notes land in D minor: finds the pitch of its
    /// loud, clearly tonal moments, then picks the shift (within 2.5 semitones, smaller preferred)
    /// that puts them closest to D minor notes, weighted by loudness. 0 if it has no clear notes.
    /// </summary>
    static float KeyShift(float[] src, int ch, int start, int rate, out string notes)
    {
        notes = "";
        int frames = src.Length / ch;
        var mono = new float[Mathf.Max(0, frames - start)];
        for (int i = 0; i < mono.Length; i++)
        {
            float v = 0f;
            for (int c = 0; c < ch; c++) v += src[(start + i) * ch + c];
            mono[i] = v / ch;
        }

        // Only the first few seconds, and only the loudest moments of them: each pitch reading
        // costs a few million operations, and this runs while the game starts.
        const float Seconds = 6f;
        const int MaxReadings = 40;
        int scanEnd = Mathf.Min(mono.Length, (int)(Seconds * rate));
        var energy = new List<(int at, float e)>();
        float loudest = 0f;
        for (int s = 0; s + PitchWin * 2 < scanEnd; s += PitchWin)
        {
            float e = 0f;
            for (int i = 0; i < PitchWin; i++) e += mono[s + i] * mono[s + i];
            energy.Add((s, e));
            loudest = Mathf.Max(loudest, e);
        }
        energy.Sort((x, y) => y.e.CompareTo(x.e));
        if (energy.Count > MaxReadings) energy.RemoveRange(MaxReadings, energy.Count - MaxReadings);
        energy.Sort((x, y) => x.at.CompareTo(y.at));   // back in time order, for the note list

        var found = new List<(float midi, float weight)>();
        var named = new System.Text.StringBuilder();
        int lastNote = int.MinValue;
        foreach (var (at, e) in energy)
        {
            if (e < loudest * 0.08f) continue;
            float hz = PitchAt(mono, at, rate, out float clarity);
            if (hz <= 0f || clarity < 0.8f) continue;
            float midi = 69f + 12f * Mathf.Log(hz / 440f, 2f);
            found.Add((midi, Mathf.Sqrt(e) * clarity));
            int rounded = Mathf.RoundToInt(midi);
            if (rounded != lastNote && named.Length < 120)
            {
                named.Append(named.Length > 0 ? " " : "").Append(NoteNames[((rounded % 12) + 12) % 12]).Append(rounded / 12 - 1);
                lastNote = rounded;
            }
        }
        if (found.Count < 2) return 0f;
        notes = named.ToString();

        float bestShift = 0f, bestCost = float.MaxValue;
        for (int cents = -250; cents <= 250; cents += 5)
        {
            float cost = 0f, total = 0f;
            foreach (var (midi, w) in found)
            {
                float m = midi + cents / 100f, nearest = float.MaxValue;
                foreach (int pc in DMinor)
                    nearest = Mathf.Min(nearest, Mathf.Abs(Mathf.Repeat(m - pc + 6f, 12f) - 6f));   // semitones to that note
                cost += w * nearest * nearest;
                total += w;
            }
            cost = cost / Mathf.Max(1e-6f, total) + Mathf.Abs(cents) * 0.0004f;   // prefer the smallest shift that works
            if (cost < bestCost) { bestCost = cost; bestShift = cents; }
        }
        return bestShift;
    }

    // --------------------------------------------------------------- hints

    /// <summary>
    /// A short taste of a sound: its first 'seconds' (faded out), played 'repeats' times
    /// 'gap' seconds apart. Used to announce an enemy before it arrives.
    /// </summary>
    public static void PlayHint(string name, float seconds, float volume = 1f, int repeats = 1, float gap = 0.12f)
    {
        if (src == null) return;
        string key = $"{name}#hint{Mathf.RoundToInt(seconds * 1000f)}x{repeats}";
        if (!clips.TryGetValue(key, out AudioClip c))
        {
            c = clips.TryGetValue(name, out AudioClip full) && full != null ? Hint(key, full, seconds, repeats, gap) : null;
            clips[key] = c;
        }
        if (c != null) src.PlayOneShot(c, volume);
        else Play(name, volume);
    }

    static AudioClip Hint(string key, AudioClip full, float seconds, int repeats, float gap)
    {
        float[] mono = MonoData(full);
        if (mono == null) return null;
        int rate = full.frequency;
        int len = Mathf.Clamp((int)(seconds * rate), 1, mono.Length);
        int step = len + (int)(gap * rate);
        int fade = Mathf.Max(1, Mathf.Min(len / 3, rate / 50));
        repeats = Mathf.Max(1, repeats);
        var data = new float[step * (repeats - 1) + len];
        for (int r = 0; r < repeats; r++)
            for (int i = 0; i < len; i++)
                data[r * step + i] = mono[i] * Mathf.Clamp01((len - i) / (float)fade);
        var clip = AudioClip.Create(key, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    public static void Play(string name, float volume = 1f)
    {
        if (src == null) return;
        if (!clips.TryGetValue(name, out AudioClip c) || c == null) return;
        if (Vary.TryGetValue(name, out float spread)) PlayVaried(c, volume, spread);
        else src.PlayOneShot(c, volume);
    }

    /// <summary>Every sound name (for the preview window), without the cached hints and build-ups.</summary>
    public static IEnumerable<string> Names
    {
        get
        {
            foreach (var k in clips.Keys)
                if (!k.Contains("#hint") && !k.StartsWith("suspense")) yield return k;
        }
    }

    public static bool Ready => src != null;

    static void Redesign(string name, AudioClip clip)
    {
        if (clips.TryGetValue(name, out AudioClip old)) clips[name + "#old"] = old;
        clips[name] = clip;
    }

    // ----------------------------------------------------------- variation

    /// <summary>
    /// Sounds that change slightly every time they play (pitch spread, as a fraction), so the
    /// frequent ones don't sound like the same sample over and over.
    /// </summary>
    static readonly Dictionary<string, float> Vary = new Dictionary<string, float>
    {
        { "orb", 0.02f }, { "hit", 0.02f },
        { "heal", 0.015f }, { "pick", 0.015f }, { "neutrinoLand", 0.01f }, { "uiOpen", 0.03f }, { "research", 0.02f }, { "coin", 0.012f }, { "star", 0.012f }, { "recycle", 0.05f }, { "full", 0.02f },
        { "pop", 0.06f }, { "snap", 0.08f }, { "zap", 0.04f }, { "boost", 0.03f }, { "repel", 0.03f },
        { "shoot", 0.05f }, { "sputter", 0.06f }, { "explode", 0.03f },
        { "tick", 0.06f }, { "block", 0.03f }, { "conduct", 0.03f },
    };

    static AudioSource[] voices = new AudioSource[0];
    static int nextVoice;

    /// <summary>Pitch changes need their own source (PlayOneShot shares one pitch).</summary>
    static void InitVoices(GameObject host)
    {
        Transform t = host.transform.Find("SfxVoices");
        if (t == null)
        {
            var go = new GameObject("SfxVoices");
            go.hideFlags = host.hideFlags;
            go.transform.SetParent(host.transform, false);
            t = go.transform;
        }
        var list = new List<AudioSource>(t.GetComponents<AudioSource>());
        while (list.Count < 8) list.Add(t.gameObject.AddComponent<AudioSource>());
        foreach (var v in list) { v.playOnAwake = false; v.spatialBlend = 0f; v.loop = false; }
        voices = list.ToArray();
    }

    static void PlayVaried(AudioClip c, float volume, float spread)
    {
        AudioSource v = null;
        for (int i = 0; i < voices.Length && v == null; i++)
        {
            var cand = voices[(nextVoice + i) % voices.Length];
            if (cand != null && !cand.isPlaying) v = cand;
        }
        if (v == null && voices.Length > 0) v = voices[nextVoice % voices.Length];   // all busy: take the oldest
        nextVoice = (nextVoice + 1) % Mathf.Max(1, voices.Length);
        if (v == null) { src.PlayOneShot(c, volume); return; }
        v.clip = c;
        v.pitch = 1f + Random.Range(-spread, spread);
        v.volume = volume * Random.Range(0.9f, 1f);
        v.Play();
    }

    /// <summary>
    /// Character mystery box build-up, exactly 'dur' seconds long so it lands on the reveal:
    /// a low hum for the first quarter, then a rising, ever faster trembling tone over a rumble.
    /// </summary>
    public static void PlaySuspense(float dur, float volume = 1f)
    {
        if (src == null) return;
        string key = "suspense" + Mathf.RoundToInt(dur * 100f);
        if (!clips.TryGetValue(key, out AudioClip c) || c == null)
        {
            c = Suspense(key, Mathf.Max(0.3f, dur));
            clips[key] = c;
        }
        src.PlayOneShot(c, volume);
    }

    static AudioClip Suspense(string name, float dur)
    {
        float tonePhase = 0f, tonePhase2 = 0f, humPhase = 0f, tremPhase = 0f;
        var rumbleF = new Svf();
        var rng = new System.Random(7);
        return Design(name, dur, 0.35f, 0.3f, 0.5f, t =>
        {
            float k = Mathf.Clamp01(t / dur);
            float build = Mathf.Clamp01((k - 0.25f) / 0.75f);          // matches the card's shake
            float b2 = build * build;

            humPhase += 2f * Mathf.PI * (Note(38) * (1f + 0.18f * build)) / Rate;   // D2, drifting up
            float hum = Mathf.Sin(humPhase) * 0.6f + Mathf.Sin(humPhase * 2f) * 0.2f;

            rumbleF.Run(Noise(rng), 120f + 500f * build, 0.9f);          // low rumble, opening up
            float rumble = rumbleF.Low * 2.2f * (0.3f + 0.7f * build);

            float f = Note(55) * Mathf.Pow(2f, 2.2f * b2);              // G3, up ~2 octaves
            tonePhase += 2f * Mathf.PI * f / Rate;
            tonePhase2 += 2f * Mathf.PI * f * 1.006f / Rate;            // detuned twin: a slow shimmer
            tremPhase += 2f * Mathf.PI * (4f + 14f * build) / Rate;      // tremble speeds up
            float trem = 0.5f + 0.5f * Mathf.Sin(tremPhase);
            float tone = (Mathf.Sin(tonePhase + 0.5f * b2 * Mathf.Sin(tonePhase * 2f)) + 0.7f * Mathf.Sin(tonePhase2)
                          + 0.25f * Mathf.Sin(tonePhase * 1.5f)) * 0.6f * trem * b2;

            float env = 0.25f + 0.75f * b2;
            return (hum * 0.35f + rumble * 0.4f + tone * 0.5f) * env * 0.55f;
        });
    }

    /// <summary>
    /// Neutrino earned: two struck glass tones (inharmonic partials, like a wine glass or a glass
    /// bell) ringing a fifth apart with a slow shimmer, over a breath of airy noise.
    /// </summary>
    static AudioClip Neutrino()
    {
        float[] ratios = { 1f, 2.32f, 4.25f, 6.63f };   // glass-like, not whole-number harmonics
        float[] amps = { 1f, 0.45f, 0.22f, 0.1f };
        float[] decays = { 2.2f, 3.6f, 6f, 9f };
        float[] roots = { 1318.5f, 1975.5f };         // E6, then B6
        float[] starts = { 0f, 0.11f };
        float[,] ph = new float[2, 4];
        float lp = 0f, hp = 0f;
        var rng = new System.Random(23);
        return Build("neutrino", 1.8f, t =>
        {
            float v = 0f;
            for (int n = 0; n < 2; n++)
            {
                float tt = t - starts[n];
                if (tt < 0f) continue;
                float attack = Mathf.Clamp01(tt * 900f);
                for (int i = 0; i < ratios.Length; i++)
                {
                    float shimmer = 1f + 0.0025f * Mathf.Sin(tt * (5.5f + i * 1.7f));
                    ph[n, i] += 2f * Mathf.PI * roots[n] * ratios[i] * shimmer / Rate;
                    v += Mathf.Sin(ph[n, i]) * amps[i] * Mathf.Exp(-tt * decays[i]) * attack * (n == 0 ? 1f : 0.8f);
                }
            }
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * 0.5f;
            hp = noise - lp;                                         // thin, airy
            float air = hp * Mathf.Exp(-t * 5f) * 0.12f;
            return v * 0.16f + air;
        });
    }

    /// <summary>Character reveal: a soft boom, a whoosh and a bright rising chord.</summary>
    static AudioClip Reveal()
    {
        float boomPhase = 0f, lp = 0f;
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f };
        float[] phases = new float[notes.Length];
        var rng = new System.Random(11);
        return Build("reveal", 1.4f, t =>
        {
            boomPhase += 2f * Mathf.PI * (60f + 90f * Mathf.Exp(-t * 18f)) / Rate;
            float boom = Mathf.Sin(boomPhase) * Mathf.Exp(-t * 7f);

            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * 0.35f;
            float whoosh = lp * Mathf.Exp(-t * 9f);

            float chord = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float start = i * 0.045f;                               // quick upward arpeggio
                float vib = 1f + 0.004f * Mathf.Sin(t * 30f + i);
                phases[i] += 2f * Mathf.PI * notes[i] * vib / Rate;
                if (t < start) continue;
                float e = Mathf.Exp(-(t - start) * 2.6f) * Mathf.Clamp01((t - start) * 200f);
                chord += Mathf.Sin(phases[i]) * e;
            }
            return boom * 0.55f + whoosh * 0.35f + chord * 0.16f;
        });
    }

    // ============================================================ designs
    //
    // Tuned to D minor (the music's key): D E F G A Bb C. Each is rendered dry, then given the
    // same short stereo room so they sit in the music's space (see Design, Room).

    /// <summary>
    /// Something gained (an electron, stability, a menu opening): the old rising chirp, made
    /// rounder. One tone sweeping up an octave, D5 to D6, fast at first and settling on the D
    /// (instead of a straight-line sweep that never lands), with a soft overtone and the room.
    /// </summary>
    static AudioClip Orb() => Design("orb", 0.22f, 0.4f, 0.25f, 0.38f, t =>
    {
        float p = Tau * Glide(t, Note(74), Note(86), 22f);
        float tone = Mathf.Sin(p) + 0.18f * Mathf.Sin(2f * p) * Mathf.Exp(-t * 14f);
        return tone * Env(t, 0.004f, 9f);
    });

    /// <summary>
    /// Stability restored (a heart, Regeneration, the Health Recycler): a warm breath in. A soft
    /// F major chord (F4 A4 C5, each a slightly detuned pair) swelling up, air rising through
    /// it, and a small glass ping (F6) blooming on top as it settles. The opposite of the hit.
    /// </summary>
    static AudioClip Heal()
    {
        var air = new Svf();
        var rng = new System.Random(131);
        int[] chord = { 65, 69, 72 };
        return Design("heal", 0.6f, 0.75f, 0.4f, 0.42f, t =>
        {
            float swell = Mathf.Clamp01(t / 0.12f);
            swell = swell * swell * (3f - 2f * swell) * Mathf.Exp(-Mathf.Max(0f, t - 0.12f) * 4.5f);
            float pad = 0f;
            for (int i = 0; i < chord.Length; i++)
            {
                float f = Note(chord[i]);
                pad += Mathf.Sin(Tau * f * t) + 0.7f * Mathf.Sin(Tau * f * 1.004f * t + i);
            }
            air.Run(Noise(rng), 900f + 3500f * Mathf.Min(1f, t * 3f), 1.6f);
            float breath = air.Band * swell * 0.6f;
            return pad * swell * 0.18f + breath + Glass(t - 0.1f, Note(89), 5f) * 0.45f;
        });
    }

    /// <summary>
    /// An upgrade card chosen: a confident strum of three plucked notes (A4 D5 F5, 15 ms apart)
    /// with a short lift of air under it.
    /// </summary>
    static AudioClip Pick()
    {
        var air = new Svf();
        var rng = new System.Random(137);
        return Design("pick", 0.4f, 0.5f, 0.3f, 0.42f, t =>
        {
            float strum = Pluck(t, Note(69), 7f) * 0.8f + Pluck(t - 0.015f, Note(74), 6.5f) * 0.8f
                        + Pluck(t - 0.03f, Note(77), 6f);
            air.Run(Noise(rng), 700f + 2500f * Mathf.Min(1f, t * 7f), 1.8f);
            return strum * 0.5f + air.Band * Env(t, 0.03f, 12f) * 0.7f;
        });
    }

    /// <summary>
    /// A neutrino arriving on a locked petal: a short swell of air drawing in, landing in a
    /// glass tap (D6 and A6 together) with a soft low D4 under it, like something settling in.
    /// </summary>
    static AudioClip NeutrinoLand()
    {
        var air = new Svf();
        var rng = new System.Random(139);
        const float Land = 0.09f;
        return Design("neutrinoLand", 0.55f, 0.7f, 0.4f, 0.42f, t =>
        {
            float rise = t < Land ? Mathf.Pow(t / Land, 2f) : Mathf.Exp(-(t - Land) * 40f);
            air.Run(Noise(rng), 1500f + 4000f * Mathf.Min(1f, t / Land), 2f);
            float tap = Glass(t - Land, Note(86), 6f) + Glass(t - Land, Note(93), 7f) * 0.6f;
            float low = Mathf.Sin(Tau * Note(62) * t) * Env(t - Land, 0.003f, 10f) * 0.4f;
            return air.Band * rise * 0.6f + tap * 0.6f + low;
        });
    }

    /// <summary>
    /// A menu opening (Index, Lab, character chooser): a soft two-part click, press then a
    /// lighter release 25 ms later, each a tiny filtered tick with a wooden "tock" in it.
    /// 'confirm' (buying research) adds a small A5 ping after it: the same click, but it took.
    /// </summary>
    static AudioClip UiClick(string name, bool confirm)
    {
        var tick = new Svf();
        var rng = new System.Random(149);
        return Design(name, confirm ? 0.3f : 0.09f, confirm ? 0.45f : 0.15f, confirm ? 0.25f : 0.12f, confirm ? 0.36f : 0.32f, t =>
        {
            tick.Run(Noise(rng), 3200f, 1.4f);
            float click = tick.Band * (Env(t, 0.0004f, 220f) + Env(t - 0.025f, 0.0004f, 300f) * 0.5f) * 1.6f;
            float tock = Mathf.Sin(Tau * 1150f * t) * Env(t, 0.0008f, 90f) * 0.5f
                       + Mathf.Sin(Tau * 1500f * t) * Env(t - 0.025f, 0.0008f, 120f) * 0.25f;
            float ping = confirm ? Ping(t - 0.035f, Note(81), 12f) * 0.45f : 0f;
            return click + tock + ping;
        });
    }

    /// <summary>
    /// Losing stability: the first version (a tone dropping from 220 Hz, half noise, 0.35 s),
    /// kept as it was but rounded: the noise filtered so it thuds instead of hissing, and a
    /// touch of the room.
    /// </summary>
    static AudioClip Hit()
    {
        var body = new Svf();
        var rng = new System.Random(127);
        return Design("hit", 0.35f, 0.3f, 0.2f, 0.75f, t =>
        {
            float tone = Mathf.Sin(Tau * 220f * (1f - Mathf.Exp(-t * 6f)) / 6f);   // 220 Hz falling, as before
            body.Run(Noise(rng), 2500f - 1500f * Mathf.Min(1f, t * 4f), 0.7f);
            return (tone * 0.5f + body.Low * 0.9f) * Env(t, 0.0025f, 9f);
        });
    }

    /// <summary>
    /// A positron and an electron spiralling into each other (exactly EnemyMover.PositroniumTime
    /// long, ending as they annihilate): a tone winding up an octave, warbling faster and faster
    /// like something spinning, cut off at the end for the flash.
    /// </summary>
    static AudioClip Positronium()
    {
        float dur = EnemyMover.PositroniumTime, p = 0f, spin = 0f;
        return Design("positronium", dur, 0.08f, 0.15f, 0.35f, t =>
        {
            float k = Mathf.Clamp01(t / dur);
            p += Note(74) * Mathf.Pow(2f, k * k) / Rate;          // D5 up to D6, speeding up
            spin += (6f + 40f * k * k) / Rate;                     // the spiral tightening
            float warble = 0.55f + 0.45f * Mathf.Sin(Tau * spin);
            float tone = Mathf.Sin(Tau * p + 0.6f * Mathf.Sin(Tau * p * 2f)) * warble;
            return tone * (0.3f + 0.7f * k) * Mathf.Clamp01(t * 60f);
        });
    }

    /// <summary>
    /// Annihilation: a white flash of a sound (a sharp burst of bright noise) with two high
    /// glass tings for the gamma photons flying apart. Short, so it's over with the flash.
    /// </summary>
    static AudioClip Annihilate()
    {
        var flash = new Svf();
        var rng = new System.Random(151);
        return Design("annihilate", 0.3f, 0.2f, 0.2f, 0.5f, t =>
        {
            flash.Run(Noise(rng), 6000f - 4000f * Mathf.Min(1f, t * 15f), 0.9f);
            float burst = flash.Low * Env(t, 0.0005f, 30f) * 1.4f;
            float photons = Glass(t, Note(98), 14f) + Glass(t - 0.02f, Note(105), 16f) * 0.7f;
            float thump = Mathf.Sin(Tau * Glide(t, 300f, 90f, 30f)) * Env(t, 0.001f, 25f) * 0.5f;
            return burst + photons * 0.5f + thump;
        });
    }

    /// <summary>
    /// A quantum: two struck glass bells a fifth apart (D6, A6), brighter than the orb, with
    /// a glint of high noise on the second.
    /// </summary>
    static AudioClip Coin()
    {
        var air = new Svf();
        var rng = new System.Random(31);
        return Design("coin", 0.45f, 0.6f, 0.3f, 0.4f, t =>
        {
            float v = Glass(t, Note(86), 9f) * 0.7f + Glass(t - 0.07f, Note(93), 6.5f);
            air.Run(Noise(rng), 9000f, 0.8f);
            return v + air.Band * Env(t - 0.07f, 0.002f, 40f) * 0.35f;
        });
    }

    /// <summary>
    /// A photon (the level goal): a quick rising chime arpeggio, F6 A6 D7, over a soft D5 body
    /// and a breath of shimmer. The biggest of the pickups.
    /// </summary>
    static AudioClip Star()
    {
        var shimmer = new Svf();
        var rng = new System.Random(37);
        return Design("star", 0.6f, 0.8f, 0.4f, 0.45f, t =>
        {
            float v = Glass(t, Note(89), 7f) * 0.6f + Glass(t - 0.035f, Note(93), 6f) * 0.7f
                    + Glass(t - 0.07f, Note(98), 5f) * 0.8f;
            float body = Mathf.Sin(Tau * Note(74) * t) * Env(t, 0.004f, 7f) * 0.35f;
            shimmer.Run(Noise(rng), 7000f + 2000f * Mathf.Sin(t * 30f), 1.5f);
            float sh = shimmer.Band * Env(t, 0.03f, 6f) * 0.3f;
            return v + body + sh;
        });
    }

    /// <summary>
    /// An orb recycled: a soft falling swish (noise swept down from 3 kHz) with a faint
    /// glide from D6 down to A5, like something dissolving.
    /// </summary>
    static AudioClip Recycle()
    {
        var swish = new Svf();
        var rng = new System.Random(41);
        return Design("recycle", 0.24f, 0.4f, 0.25f, 0.32f, t =>
        {
            float hz = 600f + 2400f * Mathf.Exp(-t * 14f);
            swish.Run(Noise(rng), hz, 3f);
            float glide = Mathf.Sin(Tau * Glide(t, Note(86), Note(81), 18f)) * 0.35f;
            return (swish.Band * 1.4f + glide) * Env(t, 0.008f, 13f);
        });
    }

    /// <summary>
    /// Not possible (full, not ready, can't afford): two muted low notes falling a third,
    /// A3 then F3, rounded and slightly thick, instead of a harsh buzz.
    /// </summary>
    static AudioClip Full()
    {
        var click = new Svf();
        var rng = new System.Random(43);
        return Design("full", 0.3f, 0.3f, 0.15f, 0.4f, t =>
        {
            float v = Muted(t, Note(57), 24f) + Muted(t - 0.1f, Note(53), 18f) * 0.9f;
            click.Run(Noise(rng), 1800f, 0.7f);
            return v + click.Low * Env(t, 0.001f, 300f) * 0.5f;
        });
    }

    /// <summary>
    /// Something destroyed (an enemy, an electron lost, a pickup bursting): a round little pop,
    /// a tone that drops an octave fast (D5 to D4) inside a puff of filtered noise.
    /// </summary>
    static AudioClip Pop()
    {
        var puff = new Svf();
        var rng = new System.Random(47);
        return Design("pop", 0.16f, 0.35f, 0.25f, 0.45f, t =>
        {
            float tone = Mathf.Sin(Tau * Glide(t, Note(74), Note(62), 35f)) * Env(t, 0.002f, 22f);
            puff.Run(Noise(rng), 1400f - 900f * Mathf.Min(1f, t * 12f), 1.2f);
            return tone * 0.8f + puff.Band * Env(t, 0.001f, 45f) * 0.9f;
        });
    }

    /// <summary>
    /// A bond breaking: a crisp crack (bright noise, a couple of milliseconds) with a tiny
    /// resonant ring after it and a dull tick underneath.
    /// </summary>
    static AudioClip Snap()
    {
        var crack = new Svf();
        var ring = new Svf();
        var rng = new System.Random(53);
        return Design("snap", 0.1f, 0.3f, 0.2f, 0.4f, t =>
        {
            float n = Noise(rng);
            crack.Run(n, 4500f, 0.7f);
            ring.Run(n * Env(t, 0.0005f, 400f), 3150f, 14f);   // excited by the crack, then rings on its own
            float tick = Mathf.Sin(Tau * Glide(t, 420f, 160f, 80f)) * Env(t, 0.001f, 70f);
            return crack.High * Env(t, 0.0005f, 140f) + ring.Band * 2.5f * Env(t, 0f, 35f) + tick * 0.5f;
        });
    }

    /// <summary>
    /// Electricity: a gritty tone falling fast (FM for the buzz) wrapped in crackling arcs
    /// (noise that flickers on and off every few milliseconds).
    /// </summary>
    static AudioClip Zap()
    {
        var arc = new Svf();
        var rng = new System.Random(59);
        float gate = 1f;
        int gateLeft = 0;
        return Design("zap", 0.32f, 0.4f, 0.25f, 0.5f, t =>
        {
            float p = Tau * Glide(t, 2200f, 170f, 9f);
            float buzz = Mathf.Sin(p + 2.2f * Mathf.Sin(p * 3.7f)) * Env(t, 0.001f, 9f);
            if (--gateLeft <= 0)
            {
                gateLeft = Rate / 1000 * (2 + (int)(rng.NextDouble() * 4));   // 2..5 ms chunks
                gate = rng.NextDouble() < 0.6 ? 1f : 0.15f;
            }
            arc.Run(Noise(rng), 3200f - 1800f * Mathf.Min(1f, t * 4f), 1.8f);
            return buzz * 0.55f + arc.Band * gate * Env(t, 0.001f, 7f) * 1.3f;
        });
    }

    /// <summary>
    /// A burst of speed: air rushing upward (noise swept from 400 Hz to 3.5 kHz) over a soft
    /// rising D4 to A4 underneath.
    /// </summary>
    static AudioClip Boost()
    {
        var air = new Svf();
        var rng = new System.Random(61);
        return Design("boost", 0.45f, 0.45f, 0.3f, 0.42f, t =>
        {
            float sweep = 1f - Mathf.Exp(-t * 7f);
            air.Run(Noise(rng), 400f + 3100f * sweep, 2.2f);
            float rise = Mathf.Sin(Tau * Glide(t, Note(62), Note(69), 8f)) * 0.25f;
            return (air.Band * 1.6f + rise) * Env(t, 0.06f, 5.5f);
        });
    }

    /// <summary>
    /// A shockwave pushing outward: a deep thump (sine dropping 160 to 45 Hz) and a whump of
    /// low noise whose filter closes as the wave spreads.
    /// </summary>
    static AudioClip Repel()
    {
        var whump = new Svf();
        var rng = new System.Random(67);
        return Design("repel", 0.55f, 0.6f, 0.35f, 0.55f, t =>
        {
            float thump = Mathf.Sin(Tau * Glide(t, 160f, 45f, 12f)) * Env(t, 0.003f, 6.5f);
            whump.Run(Noise(rng), 200f + 1800f * Mathf.Exp(-t * 9f), 0.9f);
            return thump + whump.Low * Env(t, 0.004f, 5f) * 1.2f;
        });
    }

    /// <summary>
    /// Energy building up (a gamma ray aiming, an ability armed, overcharge...): a detuned,
    /// slightly gritty tone climbing two octaves (D3 to D5) on a curve, trembling faster and
    /// faster, over a hiss that rises with it. 1.3 s, loudest at the end.
    /// </summary>
    static AudioClip Charge()
    {
        var hiss = new Svf();
        var rng = new System.Random(71);
        float pa = 0f, pb = 0f, trem = 0f;
        return Design("charge", 1.3f, 0.45f, 0.3f, 0.42f, t =>
        {
            float k = Mathf.Clamp01(t / 1.3f);
            float f = Note(50) * Mathf.Pow(4f, k * k * (3f - 2f * k));   // eased, two octaves up
            pa += f / Rate;
            pb += f * 1.007f / Rate;
            trem += (5f + 25f * k) / Rate;
            float tone = Mathf.Sin(Tau * pa + 0.8f * Mathf.Sin(Tau * pa * 2f)) + Mathf.Sin(Tau * pb);
            float tr = 0.65f + 0.35f * Mathf.Sin(Tau * trem);
            hiss.Run(Noise(rng), 800f + 4000f * k, 1.5f);
            float swell = 0.15f + 0.85f * k * k;
            return (tone * 0.4f * tr + hiss.Band * 0.6f * k) * swell * Mathf.Clamp01(t * 30f);
        });
    }

    /// <summary>The atom's turret: a short, soft "pew", a tone diving 1.8 kHz to 500 Hz with a click in front.</summary>
    static AudioClip Shoot()
    {
        var click = new Svf();
        var rng = new System.Random(73);
        return Design("shoot", 0.12f, 0.3f, 0.2f, 0.32f, t =>
        {
            float p = Tau * Glide(t, 1800f, 500f, 30f);
            float pew = Mathf.Sin(p + 0.9f * Mathf.Exp(-t * 40f) * Mathf.Sin(p * 2f)) * Env(t, 0.001f, 26f);
            click.Run(Noise(rng), 5000f, 0.8f);
            return pew + click.Band * Env(t, 0.0005f, 300f) * 0.6f;
        });
    }

    /// <summary>
    /// The unstable isotope's push: an engine coughing, low filtered noise chugging unevenly
    /// (about 30 times a second) over a thump that drops 95 to 50 Hz.
    /// </summary>
    static AudioClip Sputter()
    {
        var body = new Svf();
        var rng = new System.Random(79);
        float chug = 0f, chugRate = 30f;
        return Design("sputter", 0.24f, 0.3f, 0.2f, 0.5f, t =>
        {
            chug += chugRate / Rate;
            if (chug >= 1f) { chug -= 1f; chugRate = 24f + (float)rng.NextDouble() * 14f; }
            float pulse = Mathf.Pow(1f - chug, 3f);   // a burst at the start of every cycle
            body.Run(Noise(rng), 700f - 300f * Mathf.Min(1f, t * 5f), 1.1f);
            float thump = Mathf.Sin(Tau * Glide(t, 95f, 50f, 14f)) * Env(t, 0.002f, 12f);
            return (body.Low * 1.6f * (0.25f + 0.75f * pulse) + thump * 0.7f) * Env(t, 0.004f, 10f);
        });
    }

    /// <summary>
    /// A big blast: a sub punch (120 to 35 Hz), a roar of noise whose filter closes from
    /// 3 kHz down to 150 Hz, and debris crackling as it fades, in a slightly bigger room.
    /// </summary>
    static AudioClip Explode()
    {
        var roar = new Svf();
        var debris = new Svf();
        var rng = new System.Random(83);
        float spark = 0f;
        return Design("explode", 1.6f, 0.9f, 0.4f, 0.9f, t =>
        {
            float sub = Mathf.Sin(Tau * Glide(t, 120f, 35f, 5f)) * Env(t, 0.002f, 3.2f);
            roar.Run(Noise(rng), 150f + 2850f * Mathf.Exp(-t * 4f), 0.8f);
            if (rng.NextDouble() < 0.0012) spark = 1f;   // a crackle now and then
            spark *= 0.993f;
            debris.Run(Noise(rng) * spark, 2500f, 3f);
            return sub * 0.9f + roar.Low * Env(t, 0.003f, 3f) * 1.4f + debris.Band * Env(t - 0.1f, 0.05f, 2.5f) * 1.5f;
        });
    }

    /// <summary>
    /// The core coming apart (1.2 s, then the explosion): a rumble whose filter opens, a tone
    /// rising 55 to 220 Hz with a tremble that speeds up from 6 to 30 a second, and a thin
    /// whine climbing above it all. It stops dead, for the blast to take over.
    /// </summary>
    static AudioClip Meltdown()
    {
        var rumble = new Svf();
        var rng = new System.Random(89);
        float p = 0f, w = 0f, trem = 0f;
        return Design("meltdown", 1.2f, 0.25f, 0.25f, 0.55f, t =>
        {
            float k = Mathf.Clamp01(t / 1.2f);
            p += (55f * Mathf.Pow(4f, k * k)) / Rate;
            w += (300f + 900f * k * k) / Rate;
            trem += (6f + 24f * k) / Rate;
            rumble.Run(Noise(rng), 150f + 1050f * k, 0.9f);
            float tr = 0.5f + 0.5f * Mathf.Sin(Tau * trem);
            float tone = (Mathf.Sin(Tau * p) + 0.4f * Mathf.Sin(Tau * p * 2.01f)) * tr;
            float whine = Mathf.Sin(Tau * w) * 0.12f * k * k;
            return (rumble.Low * 1.5f + tone * 0.6f + whine) * (0.3f + 0.7f * k) * Mathf.Clamp01(t * 20f);
        });
    }

    /// <summary>
    /// A positron homing in (and its spawn hint, and a pair leaving): a short electronic beep
    /// on A6, rounded square, the same every time so it reads as a radar.
    /// </summary>
    static AudioClip Lock() => Design("lock", 0.06f, 0.15f, 0.12f, 0.3f, t => Muted(t, Note(93), 45f));

    /// <summary>
    /// The interface tick (typewriter, hovering, counting, aiming, a goal reaching the HUD,
    /// a Halogen grab): a tiny soft click with a faint D6 in it.
    /// </summary>
    static AudioClip Tick()
    {
        var click = new Svf();
        var rng = new System.Random(97);
        return Design("tick", 0.04f, 0.12f, 0.1f, 0.3f, t =>
        {
            click.Run(Noise(rng), 2600f, 2f);
            return click.Band * 2f * Env(t, 0.0005f, 250f) + Mathf.Sin(Tau * Note(86) * t) * Env(t, 0.001f, 80f) * 0.4f;
        });
    }

    /// <summary>Noble Atom shrugging off a hit: a bright gold clink, two small bells (D7 over A6).</summary>
    static AudioClip Block() => Design("block", 0.28f, 0.45f, 0.3f, 0.4f, t =>
        Glass(t, Note(98), 14f) + Glass(t, Note(93), 18f) * 0.6f);

    /// <summary>
    /// Conductive charging up a step: a quick electric blip gliding up (900 Hz to 1.8 kHz),
    /// buzzing (strong FM, a little rough) and spitting a few sparks: the zap's little cousin.
    /// </summary>
    static AudioClip Conduct()
    {
        var fizz = new Svf();
        var rng = new System.Random(101);
        float gate = 1f;
        int gateLeft = 0;
        return Design("conduct", 0.11f, 0.22f, 0.15f, 0.32f, t =>
        {
            float p = Tau * Glide(t, 900f, 1800f, 45f);
            float buzz = Mathf.Sin(p + 2.4f * Mathf.Sin(p * 3.7f));      // the zap's gritty FM
            buzz = Mathf.Clamp(buzz * 1.6f, -1f, 1f);                     // driven a little: rougher edge
            if (--gateLeft <= 0)
            {
                gateLeft = Rate / 1000 * (1 + (int)(rng.NextDouble() * 3));   // 1..3 ms sparks
                gate = rng.NextDouble() < 0.55 ? 1f : 0.1f;
            }
            fizz.Run(Noise(rng), 3800f, 1.6f);
            return buzz * Env(t, 0.001f, 30f) * 0.75f + fizz.Band * gate * Env(t, 0.0005f, 28f) * 1.3f;
        });
    }

    /// <summary>A warning beep (banners without a sound of their own): a rounded A5 that tilts up a little.</summary>
    static AudioClip Warn() => Design("warn", 0.11f, 0.2f, 0.15f, 0.3f, t =>
    {
        if (t < 0f) return 0f;
        float p = Tau * Glide(t, Note(81) * 0.97f, Note(81), 40f);
        return (Mathf.Sin(p) + 0.25f * Mathf.Sin(3f * p) + 0.08f * Mathf.Sin(5f * p)) * Env(t, 0.003f, 22f);
    });

    /// <summary>Back to it (a lesson closed): a short rush of air upward into a soft A5 ping.</summary>
    static AudioClip StartSound()
    {
        var air = new Svf();
        var rng = new System.Random(103);
        return Design("start", 0.3f, 0.45f, 0.3f, 0.35f, t =>
        {
            air.Run(Noise(rng), 600f + 2400f * Mathf.Min(1f, t * 8f), 2f);
            return air.Band * Env(t, 0.05f, 14f) * 1.2f + Ping(t - 0.08f, Note(81), 10f) * 0.8f;
        });
    }

    /// <summary>
    /// A milestone (a character picked, Lab research maxed, the Noble wave): a rising glass
    /// fanfare D6 F6 A6 D7, the last one ringing, over a warm D4 + A4 swell.
    /// </summary>
    static AudioClip Level() => Design("level", 1.0f, 0.9f, 0.4f, 0.5f, t =>
    {
        float v = Glass(t, Note(86), 7f) * 0.6f + Glass(t - 0.09f, Note(89), 6.5f) * 0.65f
                + Glass(t - 0.18f, Note(93), 6f) * 0.7f + Glass(t - 0.3f, Note(98), 3f);
        float swell = (Mathf.Sin(Tau * Note(62) * t) + 0.6f * Mathf.Sin(Tau * Note(69) * t))
                    * Mathf.Clamp01(t / 0.25f) * Mathf.Exp(-t * 3f) * 0.25f;
        return v + swell;
    });

    /// <summary>
    /// A character revealed: a soft low boom, a whoosh rising out of it, and a bright glass
    /// chord blooming upward (D5 F5 A5 D6 F6) with shimmer on top. The biggest designed sound.
    /// </summary>
    static AudioClip RevealSound()
    {
        var whoosh = new Svf();
        var shimmer = new Svf();
        var rng = new System.Random(107);
        int[] chord = { 74, 77, 81, 86, 89 };
        return Design("reveal", 1.5f, 1.0f, 0.45f, 0.6f, t =>
        {
            float boom = Mathf.Sin(Tau * Glide(t, 110f, 40f, 14f)) * Env(t, 0.002f, 6f);
            whoosh.Run(Noise(rng), 500f + 4500f * (1f - Mathf.Exp(-t * 6f)), 1.4f);
            float wh = whoosh.Band * Env(t, 0.04f, 5f) * 1.1f;
            float c = 0f;
            for (int i = 0; i < chord.Length; i++) c += Glass(t - i * 0.045f, Note(chord[i]), 2.6f) * (0.6f + 0.1f * i);
            shimmer.Run(Noise(rng), 8000f, 1.5f);
            float sh = shimmer.Band * Env(t - 0.15f, 0.2f, 2.5f) * 0.35f;
            return boom * 0.7f + wh + c * 0.45f + sh;
        });
    }

    // ============================================================ toolkit

    const float Tau = 2f * Mathf.PI;

    static float Note(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

    static float Noise(System.Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);

    /// <summary>Envelope: rises over 'attack' seconds, then decays exponentially (0 before t = 0).</summary>
    static float Env(float t, float attack, float decay) =>
        t < 0f ? 0f : Mathf.Clamp01(t / Mathf.Max(1e-4f, attack)) * Mathf.Exp(-t * decay);

    /// <summary>Phase (in cycles) of a tone gliding from f0 to f1 on an exponential curve.</summary>
    static float Glide(float t, float f0, float f1, float speed) =>
        f1 * t + (f0 - f1) * (1f - Mathf.Exp(-t * speed)) / speed;

    /// <summary>
    /// A soft glassy note that slides up into pitch over its first few milliseconds, with a
    /// little FM brightness at the start that fades.
    /// </summary>
    static float Ping(float t, float f, float decay)
    {
        if (t < 0f) return 0f;
        float p = Tau * Glide(t, f * 0.88f, f, 60f);
        float bright = 0.7f * Mathf.Exp(-t * 30f);
        float s = Mathf.Sin(p + bright * Mathf.Sin(2f * p)) + 0.12f * Mathf.Sin(3.01f * p) * Mathf.Exp(-t * decay);
        return s * Env(t, 0.003f, decay);
    }

    static readonly float[] GlassRatio = { 1f, 2.76f, 5.4f, 8.93f };   // inharmonic, like glass or a small bell
    static readonly float[] GlassAmp = { 1f, 0.45f, 0.22f, 0.1f };
    static readonly float[] GlassDecay = { 1f, 1.8f, 3f, 4.5f };

    /// <summary>A struck glass bell: inharmonic partials, the high ones dying first.</summary>
    static float Glass(float t, float f, float decay)
    {
        if (t < 0f) return 0f;
        float v = 0f;
        for (int i = 0; i < GlassRatio.Length; i++)
        {
            float hz = f * GlassRatio[i];
            if (hz > Rate * 0.45f) break;
            v += Mathf.Sin(Tau * hz * t) * GlassAmp[i] * Mathf.Exp(-t * decay * GlassDecay[i]);
        }
        return v * Mathf.Clamp01(t * 800f) * 0.6f;
    }

    /// <summary>A plucked string-ish note: harmonics falling off in level, the high ones dying first.</summary>
    static float Pluck(float t, float f, float decay)
    {
        if (t < 0f) return 0f;
        float v = 0f;
        for (int k = 1; k <= 6; k++)
        {
            if (f * k > Rate * 0.45f) break;
            v += Mathf.Sin(Tau * f * k * t) / k * Mathf.Exp(-t * decay * (0.6f + 0.4f * k));
        }
        return v * Mathf.Clamp01(t * 1500f);
    }

    /// <summary>A rounded, muted low note (a few odd harmonics, slightly detuned pair).</summary>
    static float Muted(float t, float f, float decay)
    {
        if (t < 0f) return 0f;
        float v = 0f;
        for (int d = 0; d < 2; d++)
        {
            float p = Tau * f * (d == 0 ? 1f : 1.006f) * t;
            v += Mathf.Sin(p) + 0.25f * Mathf.Sin(3f * p) + 0.08f * Mathf.Sin(5f * p);
        }
        return v * 0.5f * Env(t, 0.004f, decay);
    }

    /// <summary>State-variable filter (low, band and high pass at once).</summary>
    class Svf
    {
        float ic1, ic2;
        public float Low, Band, High;
        public int rate = Rate;

        public void Run(float x, float hz, float q)
        {
            float g = Mathf.Tan(Mathf.PI * Mathf.Clamp(hz, 20f, rate * 0.45f) / rate);
            float k = 1f / Mathf.Max(0.1f, q);
            float a1 = 1f / (1f + g * (g + k)), a2 = g * a1, a3 = g * a2;
            float v3 = x - ic2;
            float v1 = a1 * ic1 + a2 * v3;
            float v2 = ic2 + a2 * ic1 + a3 * v3;
            ic1 = 2f * v1 - ic1;
            ic2 = 2f * v2 - ic2;
            Low = v2;
            Band = v1;
            High = x - k * v1 - v2;
        }
    }

    /// <summary>
    /// A small stereo room (4 damped combs + 2 allpasses per side, slightly different lengths
    /// left and right), about 0.8 s long: the shared space every designed sound is put in.
    /// </summary>
    class Room
    {
        const float Feedback = 0.78f, Damp = 0.35f;
        readonly float[][] comb = new float[8][];
        readonly int[] combPos = new int[8];
        readonly float[] combLp = new float[8];
        readonly float[][] ap = new float[4][];
        readonly int[] apPos = new int[4];

        public Room()
        {
            int[] combLen = { 781, 832, 894, 949 };
            int[] apLen = { 389, 309 };
            for (int s = 0; s < 2; s++)
            {
                for (int i = 0; i < 4; i++) comb[s * 4 + i] = new float[combLen[i] + s * 17];
                for (int i = 0; i < 2; i++) ap[s * 2 + i] = new float[apLen[i] + s * 17];
            }
        }

        public void Process(float x, out float l, out float r)
        {
            l = Side(0, x);
            r = Side(1, x);
        }

        float Side(int s, float x)
        {
            float input = x * 0.25f, sum = 0f;
            for (int i = 0; i < 4; i++)
            {
                int c = s * 4 + i;
                float[] buf = comb[c];
                float o = buf[combPos[c]];
                combLp[c] += (o - combLp[c]) * (1f - Damp);
                buf[combPos[c]] = input + combLp[c] * Feedback;
                if (++combPos[c] >= buf.Length) combPos[c] = 0;
                sum += o;
            }
            for (int i = 0; i < 2; i++)
            {
                int a = s * 2 + i;
                float[] buf = ap[a];
                float d = buf[apPos[a]];
                buf[apPos[a]] = sum + d * 0.5f;
                sum = d - sum * 0.5f;
                if (++apPos[a] >= buf.Length) apPos[a] = 0;
            }
            return sum;
        }
    }

    /// <summary>
    /// Renders 'sample' for 'dur' seconds (dry, mono), then adds 'tail' seconds of the shared
    /// room at 'wet', and normalizes the stereo result to a peak of 'peak'.
    /// </summary>
    static AudioClip Design(string name, float dur, float tail, float wet, float peak, System.Func<float, float> sample)
    {
        int n = Mathf.Max(1, (int)(dur * Rate)), total = n + (int)(tail * Rate);
        var room = new Room();
        var data = new float[total * 2];
        float max = 1e-6f;
        for (int i = 0; i < total; i++)
        {
            float dry = 0f;
            if (i < n)
            {
                float t = i / (float)Rate;
                dry = sample(t) * Mathf.Clamp01(t * 400f) * Mathf.Clamp01((dur - t) * 200f);
            }
            room.Process(dry, out float l, out float r);
            float L = dry + l * wet, R = dry + r * wet;
            data[2 * i] = L;
            data[2 * i + 1] = R;
            max = Mathf.Max(max, Mathf.Max(Mathf.Abs(L), Mathf.Abs(R)));
        }
        float g = peak / max;
        int fade = Mathf.Min(total, Rate / 20);   // the room's last moments fade to silence
        for (int i = 0; i < total; i++)
        {
            float e = i >= total - fade ? (total - i) / (float)fade : 1f;
            data[2 * i] *= g * e;
            data[2 * i + 1] *= g * e;
        }
        var clip = AudioClip.Create(name, total, 2, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    const int Rate = 44100;

    /// <summary>Renders sample(t) for 'dur' seconds, with tiny fades at both ends.</summary>
    static AudioClip Build(string name, float dur, System.Func<float, float> sample)
    {
        int n = Mathf.Max(1, (int)(dur * Rate));
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float edge = Mathf.Clamp01(t * 400f) * Mathf.Clamp01((dur - t) * 60f);
            data[i] = Mathf.Clamp(sample(t) * edge, -1f, 1f);
        }
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <param name="square">0 = pure sine, 1 = square wave (buzzier).</param>
    static AudioClip Synth(string name, float dur, System.Func<float, float> freq, float noiseMix, float decay, float vol, float square = 0f)
    {
        const int rate = 44100;
        int n = Mathf.Max(1, (int)(dur * rate));
        var data = new float[n];
        var rng = new System.Random(name.GetHashCode());
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            phase += 2f * Mathf.PI * freq(t) / rate;
            float sine = Mathf.Sin(phase);
            float tone = Mathf.Lerp(sine, Mathf.Sign(sine) * 0.7f, square);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float s = tone * (1f - noiseMix) + noise * noiseMix;
            float env = Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 400f) * Mathf.Clamp01((dur - t) * 60f);
            data[i] = s * env * vol;
        }
        var clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
