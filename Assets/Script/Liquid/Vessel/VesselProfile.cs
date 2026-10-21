using System;
using UnityEngine;

[Serializable]
public struct ProfilePoint
{
    public float y;     // tinggi (ruang lokal mesh kaca)
    public float r;     // radius dalam rongga pada tinggi itu

    public ProfilePoint(float y, float r) { this.y = y; this.r = r; }
}

// Hasil kalibrasi satu model wadah (diisi tombol Auto-fit, boleh dikoreksi manual).
// Semua dalam ruang lokal mesh kaca; sumbu putar tegak di (axisXZ.x, *, axisXZ.y).
[Serializable]
public class VesselProfile
{
    public Vector2 axisXZ;
    public float floorY;               // lantai rongga di sumbu
    public float rimY;                 // bibir dalam (batas atas rongga)
    public float outerRadius;          // radius luar di tengah badan (info, untuk cangkang garis luar)
    public int segments = 48;          // jumlah sisi mesh kaca (koreksi apotema)
    public bool hasSpout;
    public Vector2 spoutDirXZ;         // arah cerat (lokal mesh kaca), satuan
    public float spoutReach;           // seberapa jauh cerat melewati radius luar
    public ProfilePoint[] samples = new ProfilePoint[0];   // dari lantai ke bibir, y naik

    public bool IsValid => samples != null && samples.Length >= 2 && rimY > floorY;
    public float Depth => rimY - floorY;

    public float RadiusAt(float y)
    {
        if (samples == null || samples.Length == 0) return 0f;
        if (y <= samples[0].y) return samples[0].r;
        for (int i = 1; i < samples.Length; i++)
        {
            if (y > samples[i].y) continue;
            float span = samples[i].y - samples[i - 1].y;
            float t = span > 1e-9f ? (y - samples[i - 1].y) / span : 0f;
            return Mathf.Lerp(samples[i - 1].r, samples[i].r, t);
        }
        return samples[samples.Length - 1].r;
    }
}