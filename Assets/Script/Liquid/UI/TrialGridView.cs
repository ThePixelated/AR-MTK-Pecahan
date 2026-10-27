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

    [Header("Tata letak (ruang lokal objek ini, bidang XZ)")]
    [SerializeField, Min(1)] int columns = 2;
    [SerializeField] Vector2 spacing = new Vector2(0.12f, 0.16f);      // jarak antar kolom (x) dan antar baris (z), meter
    [SerializeField] float itemScale = 1f;                              // skala satu item (wadah dan label)
    [SerializeField] Vector3 labelOffset = new Vector3(0f, 0.001f, -0.07f);   // posisi label relatif wadah (depan = -Z)

    [Header("Label dan animasi")]
    [SerializeField] string waterSpriteName = "air";                    // nama sprite di TMP Sprite Asset
    [SerializeField] string syrupSpriteName = "sirup";
    [SerializeField] float fillSpeed = 1.2f;                            // level naik saat wadah baru muncul (rasio per detik)

    class Item
    {
        public GameObject root;
        public LiquidVessel vessel;
        public TMP_Text label;
        public float fill, target;
        public bool scaleConfigured;
    }

    readonly List<Item> items = new List<Item>();
    Media3Session session;
    bool dirty = true;

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
        item.label = label.GetComponentInChildren<TMP_Text>(true);
        return item;
    }

    void Rebuild()
    {
        if (session == null || vesselPrefab == null || labelPrefab == null) return;
        if (!isActiveAndEnabled) { dirty = true; return; }     // ditunda sampai grid ditampilkan
        dirty = false;

        int n = session.Entries.Count;
        int max = session.Limits.MaxPerCup;
        while (items.Count < n) items.Add(CreateItem());

        for (int i = 0; i < items.Count; i++)
        {
            Item it = items[i];
            bool used = i < n;
            bool wasActive = it.root.activeSelf;
            it.root.SetActive(used);
            if (!used) continue;

            Media3Entry e = session.Entries[i];
            if (!it.scaleConfigured)
            {
                it.vessel.ConfigureScale(2 * max, 1);      // sama dengan wadah kerja: kapasitas 2 x MaxPerCup, garis tiap 1 bagian
                it.scaleConfigured = true;
            }

            it.target = Mathf.Clamp01(e.TotalSections / (float)(2 * max));
            it.fill = wasActive ? it.target : 0f;          // wadah baru muncul naik dari kosong; yang sudah ada langsung mengikuti data
            it.vessel.Fill = it.fill;
            it.vessel.LiquidColor = style == null ? Color.white : style.ColorForRatio(e.SyrupRatio);

            if (it.label)
                it.label.text = $"Wadah percobaan {i + 1}\n<sprite name=\"{waterSpriteName}\"> {e.waterSections} : <sprite name=\"{syrupSpriteName}\"> {e.syrupSections}";

            it.root.transform.localPosition = SlotPosition(i, n);
        }
    }

    // Susunan grid terpusat; baris pertama paling jauh dari penonton (+Z), baris terakhir paling dekat
    Vector3 SlotPosition(int i, int n)
    {
        int rowCount = Mathf.CeilToInt(n / (float)columns);
        int row = i / columns, col = i % columns;
        int colsInRow = Mathf.Min(columns, n - row * columns);

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