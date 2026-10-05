using UnityEngine;

[CreateAssetMenu(menuName = "AR Pecahan/Fraction Palette")]
public class FractionPalette : ScriptableObject
{
    public Color wholeColor = new Color(0.25f, 0.45f, 0.85f);
    public Color[] colors = new Color[GameRules.MaxDenominator + 1];   // index = penyebut (2..25)

    public Color Get(long denominator)
    {
        if (denominator <= 1) return wholeColor;
        return denominator < colors.Length ? colors[(int)denominator] : Color.gray;
    }

    void OnValidate() { if (colors == null || colors.Length != GameRules.MaxDenominator + 1) Generate(); }

    [ContextMenu("Generate default colors")]
    void Generate()
    {
        colors = new Color[GameRules.MaxDenominator + 1];
        for (int d = 2; d <= GameRules.MaxDenominator; d++)
        {
            float hue = ((d - 2) * 0.381966f) % 1f;        // sebaran golden ratio
            float val = (d % 2 == 0) ? 0.95f : 0.75f;      // selang-seling terang/gelap biar tetangga beda
            colors[d] = Color.HSVToRGB(hue, 0.6f, val);
        }
    }
}