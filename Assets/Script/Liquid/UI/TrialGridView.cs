using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Grid wadah perbandingan Media 3: satu wadah visual (tanpa collider) dan satu label per entri.
// Pasang di objek "CompareRoot" (nonaktif saat mode entri). Pembangunan ulang ditunda sampai objek ini aktif.
public class TrialGridView : MonoBehaviour
{
    [SerializeField] LiquidVessel vesselPrefab;       // salinan wadah TANPA VesselActor
    [SerializeField] GameObject labelPrefab;          // world canvas berisi satu TMP_Text
    [SerializeField] LiquidStyle style;
    [SerializeField, Range(0.1f, 1f)] float maxFill = 1f;                // tinggi isi saat wadah terisi penuh (MaxTotalPours tuangan)

    [Header("Tata letak (ruang lokal objek ini, bidang XZ)")]
    [SerializeField] bool singleRow = true;                             // semua wadah sebaris (berjejer) supaya level dan warnanya mudah dibandingkan; mati = pakai Columns
    [SerializeField, Min(1)] int columns = 2;
    [SerializeField] Vector2 spacing = new Vector2(0.12f, 0.16f);      // jarak antar kolom (x) dan antar baris (z), meter
    [SerializeField] float itemScale = 1f;                              // skala satu item (wadah dan label)
    [SerializeField] Vector3 labelOffset = new Vector3(0f, 0.001f, -0.07f);   // posisi label relatif wadah (depan = -Z)
    [SerializeField] Vector3 labelEuler = new Vector3(60f, 0f, 0f);           // arah label tetap (derajat, relatif terhadap grid); tidak mengikuti kamera

    [Header("Label dan animasi")]
    [SerializeField] string waterSpriteName = "air";                    // nama sprite di TMP Sprite Asset
    [SerializeField] string syrupSpriteName = "sirup";
    [SerializeField] float fillSpeed = 1.2f;                            // level naik saat wadah baru muncul (rasio per detik)

    class Item
    {
        public GameObject root;
        public LiquidVessel vessel;
        public TMP_Text label;
        public TMP_Text water;      // teks per larutan di label: WaterTxt, SyrupTxt, TotalTxt
        public TMP_Text syrup;
        public TMP_Text total;
        public float fill, target;
    }

    readonly List<Item> items = new List<Item>();
    Media3Session session;
    Media3ColorSettings colors;
    bool dirty = true;

    // Pengaturan kontras yang sama dengan wadah kerja (dari Media3VisualBinder); panggil sebelum Bind
    public void SetColors(Media3ColorSettings settings)
    {
        colors = settings;
        if (session != null) Rebuild();
    }

    Color ColorOf(Media3Entry e) =>
        colors != null ? colors.Evaluate(style, e.SyrupRatio) : (style == null ? Color.white : style.ColorForRatio(e.SyrupRatio));

    public void Bind(Media3Session s)
    {
        Unbind();
        session = s;
        session.EntriesChanged += Rebuild;
        Rebuild();
    }

    public void Unbind()
    {
        if (session != null) session.EntriesChanged -= Rebuild;
        session = null;
    }

    void OnEnable()
    {
        if (session != null && dirty) Rebuild();
    }

    Item CreateItem()
    {
        var item = new Item { root = new GameObject("TrialItem") };
        item.root.transform.SetParent(transform, false);
        item.root.transform.localScale = Vector3.one * itemScale;
        item.root.SetActive(false);       // komponen anak baru hidup setelah root diaktifkan di Rebuild

        item.vessel = Instantiate(vesselPrefab, item.root.transform);
        item.vessel.transform.localPosition = Vector3.zero;
        item.vessel.transform.localRotation = Quaternion.identity;

        GameObject label = Instantiate(labelPrefab, item.root.transform);
        label.transform.localPosition = labelOffset;
        label.transform.localRotation = Quaternion.Euler(labelEuler);
        //item.label = label.GetComponentInChildren<TMP_Text>(true);      // diganti tiga teks terpisah di bawah
        item.water = FindText(label.transform, "WaterTxt");
        item.syrup = FindText(label.transform, "SyrupTxt");
        item.total = FindText(label.transform, "TotalTxt");
        return item;
    }

    // Cari teks anak berdasarkan nama (anak langsung dulu, lalu seluruh keturunan); kosong + peringatan kalau tidak ada
    TMP_Text FindText(Transform root, string childName)
    {
        Transform t = root.Find(childName);
        if (t == null)
        {
            foreach (Transform c in root.GetComponentsInChildren<Transform>(true))
                if (c.name == childName) { t = c; break; }
        }

        TMP_Text text = t != null ? t.GetComponent<TMP_Text>() : null;
        if (text == null) Debug.LogWarning($"[{name}] TrialGridView: label prefab tidak punya teks bernama '{childName}'.", this);
        return text;
    }

    void Rebuild()
    {
        if (session == null || vesselPrefab == null || labelPrefab == null) return;
        if (!isActiveAndEnabled) { dirty = true; return; }     // ditunda sampai grid ditampilkan
        dirty = false;

        int n = session.Entries.Count;
        while (items.Count < n) items.Add(CreateItem());

        for (int i = 0; i < items.Count; i++)
        {
            Item it = items[i];
            bool used = i < n;
            bool wasActive = it.root.activeSelf;
            it.root.SetActive(used);
            if (!used) continue;

            Media3Entry e = session.Entries[i];
            it.vessel.ScaleVisible = false;                              // tanpa garis partisi

            // tinggi isi = jumlah tuangan percobaan itu / kapasitas wadah, jadi percobaan berbeda terlihat berbeda levelnya
            it.target = Mathf.Clamp01(e.LevelRatio(session.Limits)) * maxFill;
            it.fill = wasActive ? it.target : 0f;          // wadah baru muncul naik dari kosong; yang sudah ada langsung mengikuti data
            it.vessel.Fill = it.fill;
            it.vessel.LiquidColor = ColorOf(e);

            if (it.water)
                it.water.text = $"{e.waterSections}";
            if (it.syrup)
                it.syrup.text = $"{e.syrupSections}";
            if (it.total)
                it.total.text = $"{e.waterSections + e.syrupSections}";

            it.root.transform.localPosition = SlotPosition(i, n);
        }
    }

    // Susunan grid terpusat; baris pertama paling jauh dari penonton (+Z), baris terakhir paling dekat
    Vector3 SlotPosition(int i, int n)
    {
        int cols = singleRow ? Mathf.Max(1, n) : columns;
        int rowCount = Mathf.CeilToInt(n / (float)cols);
        int row = i / cols, col = i % cols;
        int colsInRow = Mathf.Min(cols, n - row * cols);

        float x = (col - (colsInRow - 1) * 0.5f) * spacing.x;
        float z = ((rowCount - 1) * 0.5f - row) * spacing.y;
        return new Vector3(x, 0f, z);
    }

    void Update()
    {
        float step = fillSpeed * Time.deltaTime;
        for (int i = 0; i < items.Count; i++)
        {
            Item it = items[i];
            if (!it.root.activeSelf || Mathf.Approximately(it.fill, it.target)) continue;
            it.fill = Mathf.MoveTowards(it.fill, it.target, step);
            it.vessel.Fill = it.fill;
        }
    }
}