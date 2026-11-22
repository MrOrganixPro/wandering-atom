using UnityEngine;

/// <summary>
/// Generative ambient score, synthesized live (no audio files, never loops the same way).
///
/// The mood: an atom lost in space, drifting with nowhere to go.
///  - Pad:    slow, breathing chords in D minor that wander from one to another at random
///            (Dm9, Bbmaj7, Gm9, Asus, Fmaj7, C/E), crossfading over several seconds.
///  - Drone:  a low D that never leaves, like the distant pull of something long gone.
///  - Bells:  sparse, glassy notes that random-walk the scale, lean downward like sighs,
///            and trail off into long silences.
///  - Space:  faint filtered "solar wind", a ping-pong echo and a big reverb wash.
///  - Drift:  the whole thing slowly wavers in pitch, like an old tape floating loose.
///
/// The Lab has its own piece, crossfaded in while its screen is open: a calm, curious
/// F-lydian arpeggio of soft plucks (100 bpm) over a warm bass note each bar, slow chord
/// swells and the odd high "computer" blip, all through the same echo and reverb.
///
/// Rendered on the audio thread (OnAudioFilterRead), so it only uses System math and its own
/// System.Random. Muffled while paused. Created by GameManager.
/// </summary>
public class Music : MonoBehaviour
{
    /// <summary>0..1, set by GameManager every frame.</summary>
    public float volume = 0.4f;
    /// <summary>Muffle and soften (pause menu).</summary>
    public bool muffled;
    /// <summary>Play the Lab's piece instead of the ambient score.</summary>
    public bool lab;

    public static Music Create(Transform parent)
    {
        var go = new GameObject("Music");
        go.transform.SetParent(parent, false);
        var src = go.AddComponent<AudioSource>();   // must come before the filter
        src.playOnAwake = false;
        src.loop = true;
        src.spatialBlend = 0f;
        src.priority = 0;
        return go.AddComponent<Music>();
    }

    // ------------------------------------------------------------- music data

    // Chords as MIDI notes (low to high). All sit over the D drone.
    static readonly int[][] Chords =
    {
        new[] { 50, 57, 65, 69, 76 },   // Dm9        D A F A E
        new[] { 46, 53, 62, 65, 69 },   // Bbmaj7     Bb F D F A
        new[] { 43, 50, 58, 62, 69 },   // Gm9        G D Bb D A
        new[] { 45, 52, 62, 64, 69 },   // Asus4      A E D E A
        new[] { 41, 48, 57, 64, 69 },   // Fmaj7      F C A E A
        new[] { 40, 48, 55, 62, 67 },   // C/E add9   E C G D G
    };

    // D natural minor, A3 .. C6: where the melody wanders.
    static readonly int[] Scale = BuildScale(57, 84);

    static int[] BuildScale(int lo, int hi)
    {
        int[] steps = { 0, 2, 3, 5, 7, 8, 10 };   // from D
        var list = new System.Collections.Generic.List<int>();
        for (int m = lo; m <= hi; m++)
        {
            int pc = ((m - 62) % 12 + 12) % 12;
            if (System.Array.IndexOf(steps, pc) >= 0) list.Add(m);
        }
        return list.ToArray();
    }

    // The Lab: four bars in F lydian, one chord each (low to high).
    static readonly int[][] LabChords =
    {
        new[] { 53, 60, 64, 69, 71 },   // Fmaj7#11   F C E A B
        new[] { 55, 62, 67, 69, 76 },   // G6/9       G D G A E
        new[] { 52, 59, 62, 67, 71 },   // Em7        E B D G B
        new[] { 57, 60, 64, 67, 71 },   // Am9        A C E G B
    };
    // Which chord tone each eighth note of a bar plays (with +12 = an octave up).
    static readonly int[] LabPattern = { 1, 3, 2, 4, 3, 2 + 12, 4, 3 };
    const float LabStep = 0.3f;   // eighth notes at 100 bpm

    static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

    // ----------------------------------------------------------------- tables

    const int TableSize = 4096;
    static float[] sineTable, padTable;

    static void BuildTables()
    {
        if (sineTable != null) return;
        sineTable = new float[TableSize + 1];
        padTable = new float[TableSize + 1];
        for (int i = 0; i <= TableSize; i++)
        {
            double x = 2.0 * System.Math.PI * i / TableSize;
            sineTable[i] = (float)System.Math.Sin(x);
            // Soft, slightly hollow tone: a few quiet harmonics on a sine.
            padTable[i] = (float)((System.Math.Sin(x) + 0.28 * System.Math.Sin(2 * x)
                                   + 0.12 * System.Math.Sin(3 * x) + 0.04 * System.Math.Sin(5 * x)) / 1.3);
        }
    }

    /// <summary>Table lookup; phase in cycles, kept in [0, 1).</summary>
    static float Tab(float[] t, double phase)
    {
        double p = phase * TableSize;
        int i = (int)p;
        float f = (float)(p - i);
        return t[i] + (t[i + 1] - t[i]) * f;
    }

    static double Wrap(double p) => p >= 1.0 ? p - System.Math.Floor(p) : p;

    // ----------------------------------------------------------------- voices

    class PadVoice
    {
        public double phaseA, phaseB;
        public float freq, pan, level, lfoRate, lfoPhase, gain;
    }

    class Bell
    {
        public bool active;
        public double p1, p2, p3, p4;
        public float freq, amp, decay, age, pan;
    }

    PadVoice[] cur, prev;
    int chordIndex = -1;
    float chordTimer, chordFade = 1f, curGain = 1f, prevGain;

    readonly Bell[] bells = new Bell[10];
    int scaleIdx;
    float noteTimer = 3f;
    int phraseLeft;
    bool sighNext;

    double dronePhase1, dronePhase2, dronePhase3;

    // The Lab's piece
    readonly Bell[] plucks = new Bell[16];
    float labMix, labTimer;
    int labStep;
    volatile bool targetLab;

    // ------------------------------------------------------------- effects

    float[] echoL, echoR;
    int echoPosL, echoPosR;
    float echoDampL, echoDampR;

    float[][] combs;
    int[] combPos;
    float[] combDamp;
    float[][] allpassL, allpassR;
    int[] apPosL, apPosR;

    // ---------------------------------------------------------------- state

    System.Random rng;
    int sr = 48000;
    bool ready;
    double time;
    int controlCounter;
    float drift = 1f, droneBreath = 1f, windAmp, windCoef = 0.05f;
    float windL, windL2, windR, windR2;
    float gain, lpCoef = 1f, lpL, lpR;
    volatile float targetGain;
    volatile bool targetMuffled;

    void Start()
    {
        BuildTables();
        sr = AudioSettings.outputSampleRate;
        rng = new System.Random();
        for (int i = 0; i < bells.Length; i++) bells[i] = new Bell();
        for (int i = 0; i < plucks.Length; i++) plucks[i] = new Bell();

        echoL = new float[(int)(0.62f * sr)];
        echoR = new float[(int)(0.91f * sr)];

        float scale = sr / 44100f;
        int[] combLens = { 1557, 1617, 1491, 1422 };
        combs = new float[combLens.Length][];
        combPos = new int[combLens.Length];
        combDamp = new float[combLens.Length];
        for (int i = 0; i < combLens.Length; i++) combs[i] = new float[(int)(combLens[i] * 1.6f * scale)];
        int[] apL = { 556, 225 }, apR = { 579, 248 };
        allpassL = new float[2][];
        allpassR = new float[2][];
        apPosL = new int[2];
        apPosR = new int[2];
        for (int i = 0; i < 2; i++)
        {
            allpassL[i] = new float[(int)(apL[i] * scale)];
            allpassR[i] = new float[(int)(apR[i] * scale)];
        }

        NextChord();
        prev = null;
        chordFade = 0f;   // the first chord fades in too
        scaleIdx = Scale.Length / 2;

        // Any playing clip makes Unity run the filter; this one is just silence.
        var src = GetComponent<AudioSource>();
        var silence = AudioClip.Create("MusicCarrier", sr, 1, sr, false);
        silence.SetData(new float[sr], 0);
        src.clip = silence;
        src.Play();
        ready = true;
    }

    void Update()
    {
        targetGain = Mathf.Clamp01(volume);
        targetMuffled = muffled;
        targetLab = lab;
    }

    // --------------------------------------------------------------- control

    float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    void NextChord()
    {
        int next;
        do next = rng.Next(Chords.Length); while (next == chordIndex);
        chordIndex = next;
        prev = cur;
        var notes = Chords[chordIndex];
        cur = new PadVoice[notes.Length];
        for (int i = 0; i < notes.Length; i++)
        {
            cur[i] = new PadVoice
            {
                freq = Hz(notes[i]),
                pan = (i % 2 == 0 ? -1f : 1f) * Rand(0.2f, 0.7f),
                level = notes[i] < 55 ? 1f : notes[i] < 66 ? 0.75f : 0.5f,
                lfoRate = Rand(0.04f, 0.13f),
                lfoPhase = Rand(0f, 6.28f),
                phaseA = rng.NextDouble(),
                phaseB = rng.NextDouble(),
            };
        }
        chordFade = 0f;
        chordTimer = Rand(11f, 18f);
    }

    bool InChord(int midi)
    {
        int pc = ((midi % 12) + 12) % 12;
        foreach (int n in Chords[chordIndex]) if (n % 12 == pc) return true;
        return false;
    }

    int NearestChordTone(int idx)
    {
        for (int d = 0; d < Scale.Length; d++)
        {
            if (idx - d >= 0 && InChord(Scale[idx - d])) return idx - d;     // prefer falling
            if (idx + d < Scale.Length && InChord(Scale[idx + d])) return idx + d;
        }
        return idx;
    }

    void PlayNote()
    {
        bool phraseStart = phraseLeft <= 0;
        if (phraseStart) phraseLeft = rng.Next(1, 5);

        // Random walk that leans downward; occasional leaps; sighs step down.
        int step;
        if (sighNext) { step = -1; sighNext = false; }
        else
        {
            double r = rng.NextDouble();
            step = r < 0.18 ? -2 : r < 0.52 ? -1 : r < 0.6 ? 0 : r < 0.83 ? 1 : r < 0.92 ? 2 : (rng.Next(2) == 0 ? -4 : 3);
        }
        scaleIdx += step;
        if (scaleIdx < 0) scaleIdx = -scaleIdx;
        if (scaleIdx >= Scale.Length) scaleIdx = 2 * (Scale.Length - 1) - scaleIdx;
        scaleIdx = Mathf.Clamp(scaleIdx, 0, Scale.Length - 1);
        if (phraseStart || phraseLeft == 1) scaleIdx = NearestChordTone(scaleIdx);   // land on something

        float amp = Rand(0.35f, 0.7f) * (phraseStart ? 1f : 0.85f);
        float pan = Rand(-0.7f, 0.7f);
        Ring(Hz(Scale[scaleIdx]), amp, Rand(0.6f, 1.2f), pan);
        if (rng.NextDouble() < 0.15) Ring(Hz(Scale[scaleIdx] + 12), amp * 0.25f, 1.4f, -pan);   // glassy ghost

        phraseLeft--;
        if (phraseLeft > 0)
        {
            if (rng.NextDouble() < 0.25) { sighNext = true; noteTimer = Rand(0.4f, 0.55f); }
            else noteTimer = Rand(0.55f, 1.3f);
        }
        else noteTimer = Rand(4f, 12f);   // long silences: nobody's listening
    }

    void Ring(float freq, float amp, float decay, float pan)
    {
        Bell b = null;
        foreach (var x in bells) if (!x.active) { b = x; break; }
        if (b == null)
        {
            b = bells[0];   // steal the oldest-sounding (quietest) one
            foreach (var x in bells) if (x.age > b.age) b = x;
        }
        b.active = true;
        b.freq = freq;
        b.amp = amp;
        b.decay = decay;
        b.age = 0f;
        b.pan = pan;
        b.p1 = b.p2 = b.p3 = b.p4 = 0.0;
    }

    /// <summary>Slow things: chord changes, LFOs, melody timing, wind. Runs every 32 samples.</summary>
    void Control(float dt)
    {
        float t = (float)time;
        drift = 1f + 0.0025f * Mathf.Sin(t * 2f * Mathf.PI * 0.031f) + 0.0015f * Mathf.Sin(t * 2f * Mathf.PI * 0.083f + 1.3f);
        droneBreath = 0.7f + 0.3f * Mathf.Sin(t * 2f * Mathf.PI * 0.045f);
        windAmp = 0.018f * (0.4f + 0.6f * (0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI * 0.021f + 2f)));
        float windHz = 250f + 450f * (0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI * 0.017f));
        windCoef = 1f - Mathf.Exp(-2f * Mathf.PI * windHz / sr);

        chordTimer -= dt;
        if (chordTimer <= 0f) NextChord();
        chordFade = Mathf.Min(1f, chordFade + dt / 6f);
        curGain = chordFade * chordFade * (3f - 2f * chordFade);
        prevGain = prev != null ? 1f - curGain : 0f;
        if (prev != null && chordFade >= 1f) prev = null;

        UpdateLfo(cur, dt);
        UpdateLfo(prev, dt);

        noteTimer -= dt;
        if (noteTimer <= 0f) PlayNote();

        // The Lab's piece fades in over ~2 s (and out over ~3 s); its sequencer only runs while audible.
        labMix = Mathf.MoveTowards(labMix, targetLab ? 1f : 0f, dt / (targetLab ? 2f : 3f));
        if (labMix > 0f)
        {
            labTimer -= dt;
            if (labTimer <= 0f) { labTimer += LabStep; LabNote(); }
        }
        else { labTimer = 0f; labStep = 0; }
    }

    /// <summary>One eighth note of the Lab's piece.</summary>
    void LabNote()
    {
        int beat = labStep % 8;
        int[] chord = LabChords[(labStep / 8) % LabChords.Length];
        int pick = LabPattern[beat];
        int midi = chord[pick % 12] + (pick >= 12 ? 12 : 0);
        if (beat == 0)
        {
            Pluck(Hz(chord[0] - 12), 0.75f, 2.2f, 0f);          // warm bass
            Pluck(Hz(chord[2]), 0.22f, 0.45f, -0.4f);            // slow chord swell
            Pluck(Hz(chord[4]), 0.18f, 0.45f, 0.4f);
        }
        if (beat == 0 || rng.NextDouble() < 0.88)                // the odd rest keeps it alive
            Pluck(Hz(midi), (beat == 0 ? 0.55f : 0.38f) * Rand(0.85f, 1.1f), 5.5f, beat % 2 == 0 ? -0.45f : 0.45f);
        if (rng.NextDouble() < 0.07)                            // a high "computer" blip
            Pluck(Hz(chord[rng.Next(1, chord.Length)] + 24), 0.16f, 9f, Rand(-0.8f, 0.8f));
        labStep++;
    }

    void Pluck(float freq, float amp, float decay, float pan)
    {
        Bell b = null;
        foreach (var x in plucks) if (!x.active) { b = x; break; }
        if (b == null)
        {
            b = plucks[0];
            foreach (var x in plucks) if (x.age > b.age) b = x;
        }
        b.active = true;
        b.freq = freq;
        b.amp = amp;
        b.decay = decay;
        b.age = 0f;
        b.pan = pan;
        b.p1 = b.p2 = b.p3 = b.p4 = 0.0;
    }

    static void UpdateLfo(PadVoice[] voices, float dt)
    {
        if (voices == null) return;
        foreach (var v in voices)
        {
            v.lfoPhase += dt * v.lfoRate * 2f * Mathf.PI;
            v.gain = v.level * (0.55f + 0.45f * Mathf.Sin(v.lfoPhase));
        }
    }

    // ----------------------------------------------------------------- audio

    void Pad(PadVoice[] voices, float g, ref float l, ref float r)
    {
        if (voices == null || g <= 0f) return;
        double inv = 1.0 / sr;
        foreach (var v in voices)
        {
            double f = v.freq * drift * inv;
            v.phaseA = Wrap(v.phaseA + f);
            v.phaseB = Wrap(v.phaseB + f * 1.0035);   // ~6 cents apart: slow beating
            float s = (Tab(padTable, v.phaseA) + Tab(padTable, v.phaseB)) * v.gain * g * 0.045f;
            l += s * (0.5f - 0.4f * v.pan);
            r += s * (0.5f + 0.4f * v.pan);
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!ready) return;
        int frames = data.Length / channels;
        float dt = 1f / sr;
        double inv = 1.0 / sr;
        float goalGain = targetGain * (targetMuffled ? 0.55f : 1f);
        float goalLp = targetMuffled ? 1f - Mathf.Exp(-2f * Mathf.PI * 600f / sr) : 1f;

        for (int n = 0; n < frames; n++)
        {
            if ((controlCounter++ & 31) == 0) Control(32f / sr);
            time += inv;

            // ---- pad
            float padL = 0f, padR = 0f;
            Pad(cur, curGain, ref padL, ref padR);
            Pad(prev, prevGain, ref padL, ref padR);

            // ---- drone: D2 + A2, and a whisper of D3
            dronePhase1 = Wrap(dronePhase1 + 73.42 * drift * inv);
            dronePhase2 = Wrap(dronePhase2 + 110.0 * drift * inv);
            dronePhase3 = Wrap(dronePhase3 + 146.83 * drift * 1.002 * inv);
            float drone = (Tab(padTable, dronePhase1) * 0.07f + Tab(padTable, dronePhase2) * 0.03f
                           + Tab(sineTable, dronePhase3) * 0.015f) * droneBreath;

            // ---- bells
            float bellL = 0f, bellR = 0f;
            foreach (var b in bells)
            {
                if (!b.active) continue;
                b.age += dt;
                float env = Mathf.Min(1f, b.age / 0.012f) * Mathf.Exp(-b.age * b.decay);
                if (env < 0.0004f && b.age > 0.1f) { b.active = false; continue; }
                double f = b.freq * drift * inv;
                b.p1 = Wrap(b.p1 + f);
                b.p2 = Wrap(b.p2 + f * 2.0);
                b.p3 = Wrap(b.p3 + f * 3.01);
                b.p4 = Wrap(b.p4 + f * 4.17);
                float bright = Mathf.Exp(-b.age * b.decay * 2f);
                float s = Tab(sineTable, b.p1) + (0.22f * Tab(sineTable, b.p2) + 0.08f * Tab(sineTable, b.p3)
                          + 0.05f * Tab(sineTable, b.p4)) * bright;
                s *= env * b.amp * 0.12f;
                bellL += s * (0.5f - 0.4f * b.pan);
                bellR += s * (0.5f + 0.4f * b.pan);
            }

            // ---- solar wind: two independent filtered noises
            float nl = (float)(rng.NextDouble() * 2.0 - 1.0), nr = (float)(rng.NextDouble() * 2.0 - 1.0);
            windL += (nl - windL) * windCoef; windL2 += (windL - windL2) * windCoef;
            windR += (nr - windR) * windCoef; windR2 += (windR - windR2) * windCoef;
            float wL = windL2 * windAmp * 3f, wR = windR2 * windAmp * 3f;

            // ---- the Lab: crossfade the ambient score out and the plucks in
            float labK = labMix * labMix * (3f - 2f * labMix);
            float amb = 1f - labK;
            padL *= amb; padR *= amb; drone *= amb;
            bellL *= amb; bellR *= amb; wL *= amb; wR *= amb;
            if (labK > 0f)
            {
                foreach (var b in plucks)
                {
                    if (!b.active) continue;
                    b.age += dt;
                    float env = Mathf.Min(1f, b.age / 0.004f) * Mathf.Exp(-b.age * b.decay);
                    if (env < 0.0004f && b.age > 0.05f) { b.active = false; continue; }
                    double f = b.freq * inv;
                    b.p1 = Wrap(b.p1 + f);
                    b.p2 = Wrap(b.p2 + f * 2.0);
                    b.p3 = Wrap(b.p3 + f * 3.0);
                    b.p4 = Wrap(b.p4 + f * 0.5);
                    float bright = Mathf.Exp(-b.age * b.decay * 2.5f);
                    float s = Tab(sineTable, b.p1) + (0.35f * Tab(sineTable, b.p2) + 0.12f * Tab(sineTable, b.p3)) * bright
                              + 0.15f * Tab(sineTable, b.p4);
                    s *= env * b.amp * 0.1f * labK;
                    bellL += s * (0.5f - 0.4f * b.pan);   // through the same echo and reverb
                    bellR += s * (0.5f + 0.4f * b.pan);
                }
            }

            // ---- ping-pong echo (mostly the bells)
            float eOutL = echoL[echoPosL], eOutR = echoR[echoPosR];
            echoDampL += (eOutL - echoDampL) * 0.3f;
            echoDampR += (eOutR - echoDampR) * 0.3f;
            echoL[echoPosL] = bellL * 0.8f + padL * 0.08f + echoDampR * 0.52f;
            echoR[echoPosR] = bellR * 0.8f + padR * 0.08f + echoDampL * 0.52f;
            if (++echoPosL >= echoL.Length) echoPosL = 0;
            if (++echoPosR >= echoR.Length) echoPosR = 0;

            // ---- reverb wash (4 damped combs -> 2 allpasses per side)
            float revIn = ((padL + padR) * 0.45f + (bellL + bellR) * 0.6f + drone * 0.25f + (eOutL + eOutR) * 0.25f) * 0.25f;
            float comb = 0f;
            for (int c = 0; c < combs.Length; c++)
            {
                float[] buf = combs[c];
                float o = buf[combPos[c]];
                combDamp[c] += (o - combDamp[c]) * 0.55f;
                buf[combPos[c]] = revIn + combDamp[c] * 0.88f;
                if (++combPos[c] >= buf.Length) combPos[c] = 0;
                comb += o;
            }
            float revL = comb, revR = comb;
            for (int a = 0; a < 2; a++)
            {
                revL = Allpass(allpassL[a], ref apPosL[a], revL);
                revR = Allpass(allpassR[a], ref apPosR[a], revR);
            }

            // ---- mix
            float L = padL + bellL + drone + wL + eOutL * 0.5f + revL * 0.32f;
            float R = padR + bellR + drone + wR + eOutR * 0.5f + revR * 0.32f;

            gain += (goalGain * Mathf.Min(1f, (float)time / 5f) - gain) * 0.00008f;   // slow fades
            lpCoef += (goalLp - lpCoef) * 0.0002f;
            lpL += (L - lpL) * lpCoef;
            lpR += (R - lpR) * lpCoef;
            L = lpL * gain * 1.6f;
            R = lpR * gain * 1.6f;
            L /= 1f + Mathf.Abs(L);   // gentle soft clip
            R /= 1f + Mathf.Abs(R);

            int i = n * channels;
            if (channels == 1) data[i] = (L + R) * 0.5f;
            else
            {
                data[i] = L;
                data[i + 1] = R;
                for (int c = 2; c < channels; c++) data[i + c] = (L + R) * 0.5f;
            }
        }
    }

    static float Allpass(float[] buf, ref int pos, float x)
    {
        float d = buf[pos];
        float y = d - x * 0.5f;
        buf[pos] = x + d * 0.5f;
        if (++pos >= buf.Length) pos = 0;
        return y;
    }
}
