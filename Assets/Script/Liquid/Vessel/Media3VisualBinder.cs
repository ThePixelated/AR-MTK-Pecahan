using UnityEngine;

// Menghubungkan Media3Session (data) ke LiquidVessel (tampilan). Gelas dan wadah selalu penuh, tanpa garis partisi;
// yang bergerak hanya warna wadah (dari warna air ke warna sirup menurut rasio).
// Bagian "Uji" untuk mencoba tanpa UI; di controller Media 3 session dibuat dan di-Bind oleh controller.
public class Media3VisualBinder : LiquidBinderBase
{
    [SerializeField] LiquidStyle style;
    [SerializeField] LiquidVessel vesselSyrup, vesselWater, vesselWadah;
    [SerializeField] float colorSpeed = 2f;      // warna wadah mengikuti rasio (rasio sirup per detik)

    [Header("Uji (sementara)")]
    [SerializeField] bool createTestSession = true;

    Media3Session session;
    float shownRatio;
    bool ratioPlaced;
    float pourWindowEnd = -1f;

    public Media3Session Session => session;
    public override IPourSession PourSession => session;

    // Selama jendela ini, warna wadah bergerak merata sampai tepat selesai di akhir durasi tuang
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
        ratioPlaced = false;       // wadah langsung memakai warna yang benar saat halaman dibuka
    }

    void Update()
    {
        if (session == null) return;

        float target = session.SyrupRatio;
        if (!ratioPlaced) { shownRatio = target; ratioPlaced = true; }

        float remaining = pourWindowEnd - Time.time;
        float step = remaining > 0.02f ? Mathf.Abs(target - shownRatio) / remaining : colorSpeed;
        shownRatio = Mathf.MoveTowards(shownRatio, target, step * Time.deltaTime);

        Drive(vesselWater, 0f);          // air bening
        Drive(vesselSyrup, 1f);          // sirup pekat
        Drive(vesselWadah, shownRatio);  // campuran
    }

    void Drive(LiquidVessel v, float ratio)
    {
        if (!v) return;
        v.ScaleVisible = false;          // tanpa garis partisi
        v.Fill = 1f;                     // selalu penuh
        if (style != null) v.LiquidColor = style.ColorForRatio(ratio);
    }

    // ---- menu uji (klik kanan komponen saat Play) ----
    void EnsureSession() { if (session == null) Bind(new Media3Session()); }

    [ContextMenu("Uji: air +1")] void TestWater() { EnsureSession(); Debug.Log($"[Media3Uji] {session.TryPour(session.Water, session.W)}", this); }
    [ContextMenu("Uji: sirup +1")] void TestSyrup() { EnsureSession(); Debug.Log($"[Media3Uji] {session.TryPour(session.Syrup, session.W)}", this); }
    [ContextMenu("Uji: Gabungkan")] void TestMerge() { EnsureSession(); Debug.Log($"[Media3Uji] {session.TryMerge()}", this); }
    [ContextMenu("Uji: Input")] void TestInput() { EnsureSession(); Debug.Log($"[Media3Uji] {session.TryInput()}, entri = {session.Entries.Count}", this); }
    [ContextMenu("Uji: reset semua")] void TestReset() { EnsureSession(); session.ResetAll(); }
}