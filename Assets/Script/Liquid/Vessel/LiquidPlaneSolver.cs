using UnityEngine;

// Mencari bidang permukaan cairan (selalu horizontal di dunia) untuk volume tertentu di wadah yang bisa miring.
// Rongga diwakili irisan datar (ys, rs) dengan tebal dy. Koordinat x,z relatif terhadap sumbu; y mutlak.
// u = arah "atas dunia" dalam ruang lokal mesh (satuan). Bidang: u . p = d.
public static class LiquidPlaneSolver
{
    public static float SolveOffset(float[] ys, float[] rs, float dy, Vector3 u, float targetVolume)
    {
        float rMax = 0f;
        for (int i = 0; i < rs.Length; i++) rMax = Mathf.Max(rMax, rs[i]);

        float hh = Mathf.Sqrt(u.x * u.x + u.z * u.z);
        float a = u.y * (ys[0] - 0.5f * dy);
        float b = u.y * (ys[ys.Length - 1] + 0.5f * dy);
        float lo = Mathf.Min(a, b) - hh * rMax - 1e-6f;
        float hi = Mathf.Max(a, b) + hh * rMax + 1e-6f;

        for (int it = 0; it < 28; it++)
        {
            float mid = 0.5f * (lo + hi);
            if (Volume(ys, rs, dy, u, hh, mid) < targetVolume) lo = mid; else hi = mid;
        }
        return 0.5f * (lo + hi);
    }

    static float Volume(float[] ys, float[] rs, float dy, Vector3 u, float hh, float d)
    {
        float sum = 0f;
        for (int i = 0; i < ys.Length; i++)
        {
            float r = rs[i];
            if (r <= 0f) continue;

            float area;
            if (hh < 1e-4f)
            {
                // bidang datar: irisan parsial supaya level naik mulus
                float level = d / u.y;
                float bottom = ys[i] - 0.5f * dy;
                float frac = u.y > 0f ? (level - bottom) / dy : (bottom + dy - level) / dy;
                area = Mathf.Clamp01(frac) * Mathf.PI * r * r;
            }
            else
            {
                float s = (d - u.y * ys[i]) / hh;     // posisi garis potong pada irisan, searah komponen datar u
                area = CircleAreaBelow(r, s);
            }
            sum += area * dy;
        }
        return sum;
    }

    // Luas bagian lingkaran berjari-jari r di sisi t <= s
    static float CircleAreaBelow(float r, float s)
    {
        if (s >= r) return Mathf.PI * r * r;
        if (s <= -r) return 0f;
        return r * r * Mathf.Acos(-s / r) + s * Mathf.Sqrt(r * r - s * s);
    }
}