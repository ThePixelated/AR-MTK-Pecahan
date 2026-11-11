using System;
using System.Collections.Generic;
using UnityEngine;

public static class AudioMath
{
    public const float SilenceDb = -80f;

    public static float LinearToDb(float linear) => linear <= 0.0001f ? SilenceDb : 20f * Mathf.Log10(linear);
    public static float DbToLinear(float db) => db <= SilenceDb ? 0f : Mathf.Pow(10f, db / 20f);

    // baseValue dikali (1 +/- amount), acak seragam
    public static float Jitter(float baseValue, float amount, System.Random rng) =>
        amount <= 0f ? baseValue : baseValue * (1f + (float)(rng.NextDouble() * 2.0 - 1.0) * amount);
}

// Memilih klip acak berbobot tanpa pengulangan langsung (supaya tidak terdengar monoton)
public sealed class ClipPicker
{
    int last = -1;

    public int Pick(IReadOnlyList<float> weights, System.Random rng)
    {
        int n = weights.Count;
        if (n == 0) return -1;
        if (n == 1) { last = 0; return 0; }

        float total = 0f;
        for (int i = 0; i < n; i++) if (i != last) total += Mathf.Max(0f, weights[i]);
        if (total <= 0f) { last = (last + 1) % n; return last; }

        float r = (float)(rng.NextDouble() * total), acc = 0f;
        for (int i = 0; i < n; i++)
        {
            if (i == last) continue;
            acc += Mathf.Max(0f, weights[i]);
            if (r <= acc) { last = i; return i; }
        }

        for (int i = n - 1; i >= 0; i--)
            if (i != last && weights[i] > 0f) { last = i; return i; }
        last = (last + 1) % n;
        return last;
    }
}

// Jeda minimal antar pemicu satu cue
public sealed class CueGate
{
    float last = float.NegativeInfinity;

    public bool Ready(float now, float cooldown) => cooldown <= 0f || now - last >= cooldown;
    public void Mark(float now) => last = now;
}