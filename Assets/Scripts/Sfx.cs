using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple synthesized sound effects (no audio files needed).
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
        // Core meltdown: a low, rising, trembling rumble; then the blast.
        clips["meltdown"] = Synth("meltdown", 1.2f, t => 55f + 170f * t * t + 25f * Mathf.Sin(t * 70f), 0.45f, 0.4f, 0.5f, 0.3f);
        clips["explode"] = Synth("explode", 1.5f, t => 95f * Mathf.Exp(-t * 3f) + 28f, 0.75f, 3.2f, 0.95f);
        clips["level"] = Synth("level", 0.9f, t => t < 0.15f ? 523f : t < 0.3f ? 659f : t < 0.45f ? 784f : 1046f, 0f, 2.5f, 0.45f);
    }

    public static void Play(string name, float volume = 1f)
    {
        if (src == null) return;
        if (clips.TryGetValue(name, out AudioClip c) && c != null) src.PlayOneShot(c, volume);
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
        float tonePhase = 0f, humPhase = 0f, tremPhase = 0f, lp = 0f;
        var rng = new System.Random(7);
        return Build(name, dur, t =>
        {
            float k = Mathf.Clamp01(t / dur);
            float build = Mathf.Clamp01((k - 0.25f) / 0.75f);          // matches the card's shake
            float b2 = build * build;

            humPhase += 2f * Mathf.PI * (55f + 10f * build) / Rate;
            float hum = Mathf.Sin(humPhase) * 0.6f + Mathf.Sin(humPhase * 2f) * 0.2f;

            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * 0.04f;                                 // low rumble
            float rumble = lp * 3f * (0.3f + 0.7f * build);

            tonePhase += 2f * Mathf.PI * (190f * Mathf.Pow(2f, 2.2f * b2)) / Rate;   // up ~2 octaves
            tremPhase += 2f * Mathf.PI * (4f + 14f * build) / Rate;      // tremble speeds up
            float trem = 0.5f + 0.5f * Mathf.Sin(tremPhase);
            float tone = (Mathf.Sin(tonePhase) + 0.3f * Mathf.Sin(tonePhase * 1.5f)) * trem * b2;

            float env = 0.25f + 0.75f * b2;
            return (hum * 0.35f + rumble * 0.4f + tone * 0.5f) * env * 0.55f;
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
