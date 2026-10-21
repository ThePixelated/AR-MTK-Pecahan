using UnityEngine;

[CreateAssetMenu(menuName = "AR Pecahan/Liquid Style")]
public class LiquidStyle : ScriptableObject
{
    [Header("Warna cairan (bisa direvisi klien)")]
    public Color waterColor = new Color(0.92f, 0.97f, 1f, 1f);          // tingkat 0 = air bening
    public Gradient dilutionGradient = DefaultGradient();                // tingkat 1..L dipetakan ke t = tingkat / L (L = jumlah tingkat)

    [Header("Garis penyebut")]
    public bool showInnerLines = true;                                   // di sisi dalam kaca
    public bool showOuterLines = false;                                  // di cangkang luar kaca
    public Color lineColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    [Range(0.001f, 0.03f)] public float lineWidth = 0.006f;              // tebal garis, pecahan dari kedalaman rongga
    public bool topLine = false;                                         // garis tambahan di batas penuh

    public Color ColorFor(int level, int levels)
    {
        if (level <= 0 || levels <= 0) return waterColor;
        return dilutionGradient.Evaluate(Mathf.Clamp01(level / (float)levels));
    }

    static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.78f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }
}