using System;
using UnityEngine;

// Pengaturan kontras warna campuran Media 3. Dipakai bersama oleh wadah kerja (Media3VisualBinder) dan wadah perbandingan (TrialGridView),
// supaya rasio yang sama selalu menghasilkan warna yang sama di kedua tempat.
[Serializable]
public class Media3ColorSettings
{
    [Tooltip("Kontras warna: 1 = warna asli dari LiquidStyle. Makin besar, campuran encer makin jelas bedanya dari air dan antar rasio makin terpisah.")]
    [Range(1f, 3f)] public float contrast = 1.8f;

    [Tooltip("Pengali saturasi warna akhir (1 = tanpa perubahan). Menambah beda antar campuran.")]
    [Range(0.5f, 2f)] public float saturationBoost = 1.2f;

    [Tooltip("Pengali kecerahan warna akhir (1 = tanpa perubahan).")]
    [Range(0.5f, 1.5f)] public float brightness = 1f;

    // rasio sirup 0..1 -> warna. Rasio 0 tetap warna air dan rasio 1 tetap warna sirup; yang di antaranya digeser oleh kurva kontras.
    public Color Evaluate(LiquidStyle style, float syrupRatio)
    {
        if (style == null) return Color.white;

        float exponent = 1f / Mathf.Max(1f, contrast);
        float shaped = Mathf.Pow(Mathf.Clamp01(syrupRatio), exponent);
        Color c = style.ColorForRatio(shaped);

        Color.RGBToHSV(c, out float h, out float s, out float v);
        Color o = Color.HSVToRGB(h, Mathf.Clamp01(s * saturationBoost), Mathf.Clamp01(v * brightness));
        o.a = c.a;
        return o;
    }
}
