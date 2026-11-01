using UnityEngine;

public enum LineStyle { Solid = 0, Dashed = 1, Texture = 2 }
public enum GhostScope { Off = 0, InnerAreaOnly = 1, AllAreas = 2 }

[System.Serializable]
public class DpzAreaStyle
{
    [Header("Tampil")]
    public bool visible = true;                       // toggle area ini

    [Header("Animasi")]
    public bool flash = false;                        // kedip: opacity naik turun
    public float flashSpeed = 1.2f;                   // Hz
    [Range(0f, 1f)] public float flashDepth = 0.6f;   // seberapa dalam opacity turun
    public bool lineAnimated = false;                 // garis arsir bergerak (butuh properti _HatchPhase di shader DpzArea)
    public float lineAnimSpeed = 0.01f;               // meter per detik

    [Header("Garis tepi")]
    public Color borderColor = new Color(1f, 1f, 1f, 0.9f);
    public float borderThickness = 0.001f;            // meter

    [Header("Isian (alpha = opacity)")]
    public Color fillColor = new Color(1f, 1f, 1f, 0.06f);

    [Header("Arsir (garis miring)")]
    public bool hatch = true;
    public Color hatchColor = new Color(1f, 1f, 1f, 0.25f);
    [Range(0f, 180f)] public float hatchAngle = 45f;
    public float hatchSpacing = 0.006f;               // jarak antar garis (meter)
    public float hatchWidth = 0.0006f;                // tebal garis (meter)
    public bool hatchDashed = false;
    public float hatchDashLength = 0.004f;
    public float hatchDashGap = 0.003f;

    [Header("Judul (di luar area, relatif dari pojok anchor)")]
    public string title = "";
    public Color titleColor = Color.white;
    public float titleSizeMM = 6f;                    // tinggi huruf dalam milimeter
    public Vector2 titleOffset = new Vector2(0f, 0.003f);   // meter: x ke kanan, y menjauhi area
}

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
    public float baseLift = 0.001f;   // tinggi dasar balok dan garis lantai dari bidang dpz (di atas area visual)

    [Header("Baris area")]
    public float reflowSpeed = 14f;            // kecepatan baris meluncur naik saat ada blok hilang; 0 = langsung loncat
    [Min(1)] public int minRows = 1;           // lantai ukuran area (baris)
    [Min(0)] public int minEmptyRows = 1;      // selalu sisakan sekian baris kosong setelah blok terakhir (dibatasi MaxDpzBlocks)
    public bool areaPredictsOnHover = false;   // true: area mengembang/menyusut sesuai prediksi blok baru saat hover

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
    [Range(0f, 1f)] public float previewFilledAlpha = 0.3f;   // isian datar sel pada preview; set 0 kalau sudah pakai ghost satuan

    [Header("Animasi dan highlight")]
    public float animationSpeed = 0f;                // meter/detik, 0 = statis
    public bool animateOnlyWhenHighlighted = true;
    public bool highlightCells = false;   // false: highlight hanya di garis border; true: sel balok ikut terang
    [Range(0f, 1f)] public float highlightBrighten = 0.5f;

    [Header("Ghost satuan: prediksi potongan (semi-transparan)")]
    public GhostScope ghostScope = GhostScope.InnerAreaOnly;   // Off = mati; InnerAreaOnly; AllAreas (termasuk zona luar)
    [Range(0f, 1f)] public float ghostAlpha = 0.45f;
    public bool ghostForceColor;                    // true = ghost satu warna, abaikan warna penyebut
    public Color ghostColor = Color.white;
    public float ghostPulseSpeed = 0f;               // denyut transparansi (Hz), 0 = tanpa denyut
    [Range(0f, 1f)] public float ghostPulseAmount = 0.5f;

    [Header("Ghost yang dipegang (3D)")]
    [Range(0f, 1f)] public float heldGhostAlpha = 0.85f;
    public float heldGhostLift = 0.01f;              // meter di atas bidang dpz

    [Header("Area dua bagian: atas (balok dari Kotak) dan bawah (tebakan sisa)")]
    public float sectionPadding = 0.003f;             // jarak isi ke tepi tiap bagian (meter)
    [Min(0)] public int sectionGapRows = 1;           // baris kosong di antara bagian atas dan bawah

    [Header("Balok carry (utuh otomatis dari jumlah pecahan)")]
    public bool carryUseSourceColors = true;                           // true: tiap sel berwarna pecahan asalnya; false: seluruh balok memakai Carry Color
    public Color carryColor = new Color(1f, 0.85f, 0.3f, 1f);          // warna kuning bawaan
    public bool carryTranslucent = false;                              // true: digambar semi-transparan
    [Range(0f, 1f)] public float carryOpacity = 0.6f;                  // opacity (hanya saat Translucent)
    public bool carryPulse = false;                                    // opacity berdenyut (hanya saat Translucent)
    public float carryPulseSpeed = 1.2f;                               // Hz
    [Range(0f, 1f)] public float carryPulseDepth = 0.5f;
    public bool carryHighlight = true;                                 // garis lantai disorot
    [Range(0f, 1f)] public float carryHighlightAmount = 1f;
    public bool carryLineAnimated = false;                             // garis lantai bergerak
    public float carryLineAnimSpeed = 0.02f;                           // meter per detik
    public Color subtractColor = new Color(0.9f, 0.35f, 0.3f, 1f);   // tint balok yang dikurangkan (Media 6)
    [Range(0f, 1f)] public float subtractBlend = 0.5f;
    //public bool showCarryBlock = true;

    [Header("Area (shader DpzArea): konfigurasi terpisah luar dan dalam")]
    public DpzAreaStyle outerArea = new DpzAreaStyle
    {
        title = "Zona luar",
        borderColor = new Color(0.45f, 0.8f, 1f, 0.9f),
        borderThickness = 0.0008f,
        fillColor = new Color(0.45f, 0.8f, 1f, 0.05f),
        hatchColor = new Color(0.45f, 0.8f, 1f, 0.18f),
        hatchDashed = true,
    };
    public DpzAreaStyle innerArea = new DpzAreaStyle
    {
        title = "Area balok",
        borderColor = new Color(1f, 1f, 1f, 0.9f),
        borderThickness = 0.001f,
        fillColor = new Color(1f, 1f, 1f, 0.10f),
        hatchColor = new Color(1f, 1f, 1f, 0.22f),
    };

    public DpzAreaStyle upperArea = new DpzAreaStyle
    {
        title = "",
        borderColor = new Color(1f, 1f, 1f, 0.5f),
        borderThickness = 0.0006f,
        fillColor = new Color(1f, 1f, 1f, 0.04f),
        hatch = false,
    };
    public DpzAreaStyle lowerArea = new DpzAreaStyle
    {
        title = "",
        borderColor = new Color(1f, 0.85f, 0.3f, 0.9f),
        borderThickness = 0.0008f,
        fillColor = new Color(1f, 0.85f, 0.3f, 0.06f),
        hatchColor = new Color(1f, 0.85f, 0.3f, 0.2f),
        hatchDashed = true,
    };
}