using UnityEngine;

public enum LineStyle { Solid = 0, Dashed = 1, Texture = 2 }

[CreateAssetMenu(menuName = "AR Pecahan/Dpz Style")]
public class DpzStyle : ScriptableObject
{
    [Header("Geometri (meter)")]
    public float wholeLength = 0.10f;    // panjang satu utuh (W)
    public float thickness = 0.015f;   // tebal balok = ukuran baris
    public float height = 0.012f;
    public float cellGap = 0.0005f;  // celah dasar antar satuan (g): konstan supaya 1/2 dan 2/4 sama panjang
    public float inset = 0.0012f;  // kemiringan sisi trapesium (s), otomatis dibatasi 30% panjang sel
    public float rowGap = 0.005f;
    public float innerPadding = 0.005f;
    public float outerMargin = 0.015f;
    public float baseLift = 0.001f;   // tinggi dasar balok dan garis lantai dari bidang dpz (di atas area visual lu)
    [Min(1)] public int minRows = 1;      // area tidak pernah lebih kecil dari ini

    [Header("Garis lantai")]
    public LineStyle lineStyle = LineStyle.Solid;
    public float lineWidth = 0.0006f;
    public float dashLength = 0.004f;
    public float dashGap = 0.003f;
    public Texture2D lineTexture;                    // untuk LineStyle.Texture (Wrap Mode: Repeat)
    public float textureTile = 0.01f;                // panjang satu pengulangan texture (meter)
    public bool forceLineColor;                     // true = semua garis satu warna, abaikan warna balok
    public Color forcedLineColor = Color.white;
    [Range(0f, 1f)] public float emptyCellAlpha = 0.08f;

    [Header("Animasi dan highlight")]
    public float animationSpeed = 0f;                // meter/detik, 0 = statis
    public bool animateOnlyWhenHighlighted = true;
    [Range(0f, 1f)] public float highlightBrighten = 0.5f;   // seberapa terang garis saat highlight
}