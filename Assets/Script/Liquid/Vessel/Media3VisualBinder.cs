using UnityEngine;

// Menghubungkan Media3Session (data) ke LiquidVessel (tampilan), tanpa garis partisi:
// gelas air dan gelas sirup penuh atau kosong (kosong setelah dituang, penuh lagi saat Isi), wadah naik sesuai total tuangan / kapasitas,
// dan warna wadah mengikuti rasio sirup lewat pengaturan kontras.
// Bagian "Uji" untuk mencoba tanpa UI; di controller Media 3 session dibuat dan di-Bind oleh controller.
public class Media3VisualBinder : LiquidBinderBase
{
    [SerializeField] LiquidStyle style;
    [SerializeField] LiquidVessel vesselSyrup, vesselWater, vesselWadah;
    [SerializeField] Media3ColorSettings colors = new Media3ColorSettings();

    [Header("Kecepatan")]
    [SerializeField] float cupRefillSpeed = 1.5f;      // gelas terisi lagi (rasio per detik)
    [SerializeField] float wadahSpeed = 1f;            // tinggi wadah mengikuti data di luar tuangan (rasio per detik)
    [SerializeField] float colorSpeed = 2f;            // warna wadah mengikuti rasio (rasio sirup per detik)

    [Header("Uji (sementara)")]
    [SerializeField] bool createTestSession = true;

    Media3Session session;
    float shownWater, shownSyrup, shownWadah, shownRatio;
    bool placed;
    float pourWindowEnd = -1f;

    public Media3Session Session => session;
    public Media3ColorSettings Colors => colors;
    public override IPourSession PourSession => session;

    // Selama jendela ini, level dan warna bergerak merata sampai tepat selesai di akhir durasi tuang
    public override void BeginPourWindow(float seconds) => pourWindowEnd = Time.time + seconds;

    public override bool TryGetContainer(LiquidVessel v, out LiquidContainer c)
    {
        c = null;
        if (session == null || v == null) return false;
        if (v == vesselSyrup) c = session.Syrup;
        else if (v == vesselWater) c = session.Water;
        else if (v == vesselWadah) c = session.W;
        return c != null;
    }

    void Start()
    {
        if (createTestSession && session == null) Bind(new Media3Session());
    }

    public void Bind(Media3Session s)
    {
        session = s;
        placed = false;       // saat halaman dibuka, tampilan langsung mengikuti data tanpa animasi
    }

    static float Move(float current, float target, float remaining, float speed)
    {
        float step = remaining > 0.02f ? Mathf.Abs(target - current) / remaining : speed;
        return Mathf.MoveTowards(current, target, step * Time.deltaTime);
    }

    void Update()
    {
        if (session == null) return;

        float waterTarget = session.WaterCupFull ? 1f : 0f;
        float syrupTarget = session.SyrupCupFull ? 1f : 0f;
        float wadahTarget = session.FillRatio;
        // wadah kosong mempertahankan warna terakhir sampai levelnya habis
        float ratioTarget = session.TotalCount > 0 ? session.SyrupRatio : shownRatio;

        if (!placed)
        {
            shownWater = waterTarget; shownSyrup = syrupTarget; shownWadah = wadahTarget; shownRatio = session.SyrupRatio;
            placed = true;
        }

        float remaining = pourWindowEnd - Time.time;
        shownWater = Move(shownWater, waterTarget, remaining, cupRefillSpeed);
        shownSyrup = Move(shownSyrup, syrupTarget, remaining, cupRefillSpeed);
        shownWadah = Move(shownWadah, wadahTarget, remaining, wadahSpeed);
        shownRatio = Move(shownRatio, ratioTarget, remaining, colorSpeed);

        Drive(vesselWater, shownWater, 0f);          // air bening
        Drive(vesselSyrup, shownSyrup, 1f);          // sirup pekat
        Drive(vesselWadah, shownWadah, shownRatio);  // campuran
    }

    void Drive(LiquidVessel v, float fill, float ratio)
    {
        if (!v) return;
        v.ScaleVisible = false;          // tanpa garis partisi
        v.Fill = fill;
        v.LiquidColor = colors != null ? colors.Evaluate(style, ratio) : (style != null ? style.ColorForRatio(ratio) : Color.white);
    }

    // ---- menu uji (klik kanan komponen saat Play) ----
    void EnsureSession() { if (session == null) Bind(new Media3Session()); }

    [ContextMenu("Uji: tuang air")] void TestWater() { EnsureSession(); Debug.Log($"[Media3Uji] {session.TryPour(session.Water, session.W)}", this); }
    [ContextMenu("Uji: tuang sirup")] void TestSyrup() { EnsureSession(); Debug.Log($"[Media3Uji] {session.TryPour(session.Syrup, session.W)}", this); }
    [ContextMenu("Uji: isi gelas air")] void TestRefillWater() { EnsureSession(); Debug.Log($"[Media3Uji] {session.TryRefill(LiquidKind.Water)}", this); }
    [ContextMenu("Uji: isi gelas sirup")] void TestRefillSyrup() { EnsureSession(); Debug.Log($"[Media3Uji] {session.TryRefill(LiquidKind.Syrup)}", this); }
    [ContextMenu("Uji: Gabungkan")] void TestMerge() { EnsureSession(); Debug.Log($"[Media3Uji] {session.TryMerge()}", this); }
    [ContextMenu("Uji: Input")] void TestInput() { EnsureSession(); Debug.Log($"[Media3Uji] {session.TryInput()}, entri = {session.Entries.Count}", this); }
    [ContextMenu("Uji: reset semua")] void TestReset() { EnsureSession(); session.ResetAll(); }
}