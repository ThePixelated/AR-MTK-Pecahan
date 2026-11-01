using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum PionMoveMode
{
    Smooth,    // meluncur halus ke tick (Media 2)
    Hop        // melompat dengan lengkung kecil (Media 5)
}

// Garis bilangan UI (Media 2 dan 5). Pasang di objek HorizontalBar (main line, anchor stretch horizontal).
// Hanya tampilan: konfigurasi dan posisi pion diberikan dari luar, drag pion dilaporkan lewat event.
[RequireComponent(typeof(RectTransform))]
public class NumberLineView : MonoBehaviour
{
    [Header("Prefab tick (anchor TITIK, bukan stretch)")]
    [SerializeField] RectTransform wholeTickPrefab;       // lebar ~9; boleh punya anak TMP_Text untuk angka 0,1,2...
    [SerializeField] RectTransform fractionTickPrefab;    // lebar ~7

    [Header("Pion")]
    [SerializeField] RectTransform pion;                  // Image pion; boleh di mana saja di hierarki UI
    [SerializeField] PionMoveMode moveMode = PionMoveMode.Smooth;
    [SerializeField] float pionSmoothing = 30f;           // mode Smooth: 0 = langsung loncat ke tick
    [SerializeField] float hopDuration = 0.18f;           // mode Hop: satu lompatan (1 tick)
    [SerializeField] float longHopDuration = 0.4f;        // mode Hop: perpindahan lebih dari 1 tick (mis. Coba lagi)
    [SerializeField] float hopHeight = 40f;               // mode Hop: tinggi lengkung

    [Header("Jejak lompatan (opsional)")]
    [SerializeField] bool showTrail;                      // mati secara default
    [SerializeField] RectTransform trailArcPrefab;        // opsional: prefab Image busur; kosong = busur bawaan
    [SerializeField] float trailHeight = 36f;
    [SerializeField] float trailMinHeight = 8f;
    [SerializeField] float trailYOffset = 4f;
    [SerializeField] Color trailForwardColor = new Color(0.2f, 0.7f, 0.3f, 0.9f);
    [SerializeField] Color trailBackwardColor = new Color(0.9f, 0.35f, 0.3f, 0.9f);
    [SerializeField] int maxTrailArcs = 60;
    [SerializeField] float trailThickness = 4f;           // tebal garis busur (piksel); hanya untuk busur bawaan
    [SerializeField] bool trailDashed;                    // busur putus-putus
    [SerializeField] float trailDashLength = 10f;
    [SerializeField] float trailGapLength = 6f;

    [Header("Penanda bersurat (Level 2: A, B, C ...)")]
    [SerializeField] RectTransform markerPrefab;       // RectTransform dengan TMP_Text anak; kosong = penanda bawaan
    [SerializeField] float markerY = 12f;              // tinggi penanda bawaan di atas garis
    [SerializeField] float markerFontSize = 40f;       // hanya penanda bawaan
    [SerializeField] Color markerColor = Color.white;  // hanya penanda bawaan

    readonly List<RectTransform> markerPool = new List<RectTransform>();
    readonly List<TMP_Text> markerTexts = new List<TMP_Text>();
    readonly List<int> markerTicks = new List<int>();
    readonly List<string> markerNames = new List<string>();
    bool markersDirty;

    class Tick
    {
        public RectTransform rt;
        public TMP_Text label;
    }

    struct Arc
    {
        public int from, to;
    }

    RectTransform mainLine;
    readonly List<Tick> wholePool = new List<Tick>();
    readonly List<Tick> fractionPool = new List<Tick>();
    readonly List<Arc> trail = new List<Arc>();
    readonly List<Graphic> arcPool = new List<Graphic>();

    NumberLineConfig config = new NumberLineConfig(1, 5);
    int pionTick;
    bool structureDirty = true;
    bool layoutDirty = true;
    bool arcsDirty;
    bool lastShowTrail;

    // status gerak pion
    bool placed;            // false = tempatkan pion langsung di tick (tanpa animasi) pada frame berikutnya
    bool hopping;
    int hopTick;            // tick yang sedang dituju oleh lompatan
    float hopT, hopDurationNow, hopFromX, restY;

    //static Sprite defaultArcSprite;

    // Pion yang sedang diatur oleh sebuah view; dua view aktif untuk satu pion akan saling berebut posisinya
    static readonly Dictionary<RectTransform, NumberLineView> pionOwners = new Dictionary<RectTransform, NumberLineView>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => pionOwners.Clear();

    // Dilaporkan saat pion di-drag: tick terdekat dari posisi pointer
    public event Action<int> PionDragged;

    public bool DragEnabled { get; set; } = true;
    public NumberLineConfig Config => config;

    public bool ShowTrail
    {
        get => showTrail;
        set => showTrail = value;
    }

    void Awake() => mainLine = (RectTransform)transform;

    void OnEnable()
    {
        structureDirty = true;
        markersDirty = true;
        layoutDirty = true;
        arcsDirty = true;
        placed = false;

        if (pion != null)
        {
            if (pionOwners.TryGetValue(pion, out NumberLineView other) && other != null && other != this && other.isActiveAndEnabled)
                Debug.LogWarning($"[{name}] Pion '{pion.name}' juga diatur oleh NumberLineView di '{other.name}'. Dua view untuk satu pion akan saling berebut posisinya.", this);
            pionOwners[pion] = this;
        }
    }

    void OnDisable()
    {
        if (pion != null && pionOwners.TryGetValue(pion, out NumberLineView owner) && owner == this)
            pionOwners.Remove(pion);
    }

    void OnValidate() => arcsDirty = true;

    // Terpanggil saat lebar main line berubah (resolusi, rotasi layar, anchor stretch)
    void OnRectTransformDimensionsChange() => layoutDirty = true;

    // ---------------- API ----------------
    // Konfigurasi baru membuang jejak, karena arti tick-nya berubah
    public void SetConfig(NumberLineConfig newConfig)
    {
        if (newConfig.length == config.length && newConfig.partisi == config.partisi && !structureDirty) return;
        config = newConfig;
        structureDirty = true;
        trail.Clear();
        markerTicks.Clear();
        markerNames.Clear();
        markersDirty = true;
        arcsDirty = true;
        if (pionTick > config.MaxTick) pionTick = config.MaxTick;
    }

    // instant = true: pion langsung di tick tanpa animasi (soal baru, Reset, ganti garis)
    public void SetPionTick(int tick, bool instant = false)
    {
        pionTick = tick < 0 ? 0 : tick > config.MaxTick ? config.MaxTick : tick;
        if (instant) placed = false;
    }

    public void SetPionVisible(bool visible)
    {
        if (pion && pion.gameObject.activeSelf != visible) pion.gameObject.SetActive(visible);
    }

    // Satu busur jejak dari tick ke tick (hijau = maju, merah = mundur); selalu dicatat, tampil bila ShowTrail aktif
    public void AddTrailArc(int fromTick, int toTick)
    {
        if (fromTick == toTick) return;
        trail.Add(new Arc { from = fromTick, to = toTick });
        while (trail.Count > Mathf.Max(1, maxTrailArcs)) trail.RemoveAt(0);
        arcsDirty = true;
    }

    public void ClearTrail()
    {
        if (trail.Count == 0) return;
        trail.Clear();
        arcsDirty = true;
    }

    // ---------------- geometri ----------------
    static float WidthOf(RectTransform rt) => rt == null ? 0f : (rt.sizeDelta.x > 0f ? rt.sizeDelta.x : rt.rect.width);

    float WholeWidth => WidthOf(wholeTickPrefab);

    // Jarak antara pusat tick pertama dan pusat tick terakhir
    float Span => Mathf.Max(0f, mainLine.rect.width - WholeWidth);

    // Jarak pusat tick dari tepi kiri main line
    float OffsetFromLeft(int tick) => WholeWidth * 0.5f + config.TickToNormalized(tick) * Span;

    bool LayoutReady => mainLine.rect.width > WholeWidth && WholeWidth > 0f;

    // ---------------- siklus ----------------
    void LateUpdate()
    {
        if (wholeTickPrefab == null || fractionTickPrefab == null) return;

        if (structureDirty)
        {
            RebuildStructure();
            structureDirty = false;
            layoutDirty = true;
        }

        bool relaid = false;
        if (layoutDirty && LayoutReady)
        {
            PositionTicks();
            layoutDirty = false;
            relaid = true;
        }

        if (showTrail != lastShowTrail)
        {
            lastShowTrail = showTrail;
            arcsDirty = true;
        }
        if ((arcsDirty || relaid) && LayoutReady)
        {
            PositionArcs();
            arcsDirty = false;
        }

        if ((markersDirty || relaid) && LayoutReady)
        {
            PositionMarkers();
            markersDirty = false;
        }

        UpdatePion();
    }

    // ---------------- tick ----------------
    void Ensure(List<Tick> pool, RectTransform prefab, int count)
    {
        while (pool.Count < count)
        {
            RectTransform rt = Instantiate(prefab, mainLine);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.gameObject.SetActive(true);
            pool.Add(new Tick { rt = rt, label = rt.GetComponentInChildren<TMP_Text>(true) });
        }
    }

    void RebuildStructure()
    {
        int wholeCount = config.length + 1;
        int fractionCount = config.length * (config.partisi - 1);

        Ensure(wholePool, wholeTickPrefab, wholeCount);
        Ensure(fractionPool, fractionTickPrefab, fractionCount);

        for (int i = 0; i < wholePool.Count; i++)
        {
            Tick t = wholePool[i];
            bool used = i < wholeCount;
            t.rt.gameObject.SetActive(used);
            if (used && t.label) t.label.text = i.ToString();
        }
        for (int i = 0; i < fractionPool.Count; i++)
            fractionPool[i].rt.gameObject.SetActive(i < fractionCount);

        KeepPionOnTop();
    }

    void KeepPionOnTop()
    {
        // tick dan busur baru ditambahkan sebagai anak terakhir; pion harus tetap tergambar di atasnya
        if (pion && pion.parent == mainLine) pion.SetAsLastSibling();
    }

    void PositionTicks()
    {
        Vector2 wholeSize = wholeTickPrefab.sizeDelta;
        Vector2 fractionSize = fractionTickPrefab.sizeDelta;
        float wholeY = wholeTickPrefab.anchoredPosition.y;
        float fractionY = fractionTickPrefab.anchoredPosition.y;

        int wi = 0, fi = 0;
        for (int k = 0; k <= config.MaxTick; k++)
        {
            bool whole = config.IsWholeTick(k);
            Tick t = whole ? wholePool[wi++] : fractionPool[fi++];
            Place(t.rt, whole ? wholeSize : fractionSize, OffsetFromLeft(k), whole ? wholeY : fractionY);
        }
    }

    // Anchor titik di tepi kiri-tengah main line, pivot di tengah tick
    static void Place(RectTransform rt, Vector2 size, float x, float y)
    {
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(x, y);
    }

    // ---------------- jejak ----------------
    Graphic CreateArc()
    {
        Graphic arc;
        if (trailArcPrefab != null)
        {
            RectTransform instance = Instantiate(trailArcPrefab, mainLine);
            arc = instance.GetComponent<Graphic>();
            if (arc == null) arc = instance.gameObject.AddComponent<UIArc>();
            instance.gameObject.SetActive(true);
        }
        else
        {
            var go = new GameObject("TrailArc", typeof(RectTransform), typeof(UIArc));
            go.transform.SetParent(mainLine, false);
            arc = go.GetComponent<UIArc>();
        }

        arc.raycastTarget = false;
        RectTransform rt = arc.rectTransform;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);       // dasar busur di garis
        return arc;
    }

    void PositionArcs()
    {
        int count = showTrail ? trail.Count : 0;
        while (arcPool.Count < count) arcPool.Add(CreateArc());

        for (int i = 0; i < arcPool.Count; i++)
        {
            Graphic arc = arcPool[i];
            bool used = i < count;
            arc.gameObject.SetActive(used);
            if (!used) continue;

            Arc a = trail[i];
            float x1 = OffsetFromLeft(a.from), x2 = OffsetFromLeft(a.to);
            float width = Mathf.Abs(x2 - x1);
            float height = Mathf.Clamp(width * 0.5f, trailMinHeight, trailHeight);

            RectTransform rt = arc.rectTransform;
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2((x1 + x2) * 0.5f, trailYOffset);
            arc.color = a.to > a.from ? trailForwardColor : trailBackwardColor;

            if (arc is UIArc uiArc) uiArc.Configure(trailThickness, trailDashed, trailDashLength, trailGapLength);
        }

        KeepPionOnTop();
    }

    // Busur setengah elips putih (dipakai bila tidak ada prefab busur), dibuat sekali lewat kode
    //static Sprite DefaultArcSprite()
    //{
    //    if (defaultArcSprite != null) return defaultArcSprite;

    //    const int w = 128, h = 64;
    //    const float thickness = 5f;
    //    var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
    //    {
    //        wrapMode = TextureWrapMode.Clamp,
    //        filterMode = FilterMode.Bilinear
    //    };

    //    float a = w * 0.5f - thickness * 0.5f - 1f;     // jari-jari horizontal elips
    //    float b = h - thickness * 0.5f - 1f;            // jari-jari vertikal; pusat di dasar tengah
    //    var pixels = new Color32[w * h];
    //    for (int y = 0; y < h; y++)
    //    {
    //        for (int x = 0; x < w; x++)
    //        {
    //            float dx = (x + 0.5f - w * 0.5f) / a;
    //            float dy = (y + 0.5f) / b;
    //            float d = Mathf.Sqrt(dx * dx + dy * dy);
    //            float distance = Mathf.Abs(d - 1f) * (a + b) * 0.5f;      // perkiraan jarak piksel ke garis elips
    //            byte alpha = (byte)(Mathf.Clamp01(thickness * 0.5f + 0.5f - distance) * 255f);
    //            pixels[y * w + x] = new Color32(255, 255, 255, alpha);
    //        }
    //    }
    //    tex.SetPixels32(pixels);
    //    tex.Apply();

    //    defaultArcSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
    //    return defaultArcSprite;
    //}

    // ---------------- pion ----------------
    float PionTargetLocalX(int tick)
    {
        // pusat tick di ruang main line -> dunia -> ruang lokal parent pion (tidak bergantung pada anchor)
        Vector3 world = mainLine.TransformPoint(new Vector3(mainLine.rect.xMin + OffsetFromLeft(tick), 0f, 0f));
        return pion.parent.InverseTransformPoint(world).x;
    }

    void UpdatePion()
    {
        if (pion == null || !pion.gameObject.activeInHierarchy || !LayoutReady) return;

        float targetX = PionTargetLocalX(pionTick);
        Vector3 lp = pion.localPosition;

        if (!placed)
        {
            // penempatan awal atau instan: langsung di tick, tanpa animasi
            float y = hopping ? restY : lp.y;
            pion.localPosition = new Vector3(targetX, y, lp.z);
            placed = true;
            hopping = false;
            hopTick = pionTick;
            return;
        }

        if (moveMode == PionMoveMode.Hop)
        {
            UpdateHop(targetX, lp);
            return;
        }

        float x = pionSmoothing <= 0f
            ? targetX
            : Mathf.Lerp(lp.x, targetX, 1f - Mathf.Exp(-pionSmoothing * Time.unscaledDeltaTime));
        if (Mathf.Abs(x - targetX) < 0.05f) x = targetX;

        pion.localPosition = new Vector3(x, lp.y, lp.z);
    }

    void UpdateHop(float targetX, Vector3 lp)
    {
        if (pionTick != hopTick)
        {
            // tujuan baru (juga saat lompatan lain masih berjalan): mulai dari posisi pion sekarang
            if (!hopping) restY = lp.y;
            hopDurationNow = Mathf.Abs(pionTick - hopTick) > 1 ? longHopDuration : hopDuration;
            hopFromX = lp.x;
            hopT = 0f;
            hopping = true;
            hopTick = pionTick;
        }

        if (!hopping)
        {
            pion.localPosition = new Vector3(targetX, lp.y, lp.z);     // diam: ikuti perubahan tata letak
            return;
        }

        hopT += Time.unscaledDeltaTime / Mathf.Max(0.01f, hopDurationNow);
        float t = Mathf.Clamp01(hopT);
        float x = Mathf.Lerp(hopFromX, targetX, Mathf.SmoothStep(0f, 1f, t));
        float lift = hopHeight * 4f * t * (1f - t);
        pion.localPosition = new Vector3(x, restY + lift, lp.z);

        if (t >= 1f)
        {
            hopping = false;
            pion.localPosition = new Vector3(targetX, restY, lp.z);
        }
    }

    // Dipanggil NumberLinePionDrag: posisi pointer menjadi tick terdekat, pion pindah, event dikirim
    public void HandlePionDrag(PointerEventData e)
    {
        if (!DragEnabled || !LayoutReady) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mainLine, e.position, e.pressEventCamera, out Vector2 local)) return;

        float normalized = (local.x - mainLine.rect.xMin - WholeWidth * 0.5f) / Span;
        int tick = config.NearestTick(normalized);

        pionTick = tick;
        PionDragged?.Invoke(tick);
    }

    // Titik bersurat yang tidak bisa disentuh. tick di luar garis (< 0) disembunyikan. Panggil SETELAH SetConfig.
    public void SetMarkers(IReadOnlyList<int> ticks, IReadOnlyList<string> labels)
    {
        markerTicks.Clear();
        markerNames.Clear();
        if (ticks != null)
        {
            for (int i = 0; i < ticks.Count; i++)
            {
                markerTicks.Add(ticks[i]);
                markerNames.Add(labels != null && i < labels.Count ? labels[i] : NumberLineLabels.Letter(i));
            }
        }
        markersDirty = true;
    }

    public void ClearMarkers() => SetMarkers(null, null);

    void CreateMarker()
    {
        RectTransform rt;
        if (markerPrefab != null)
        {
            rt = Instantiate(markerPrefab, mainLine);
            rt.localScale = Vector3.one;
            rt.gameObject.SetActive(true);
        }
        else
        {
            var go = new GameObject("Marker", typeof(RectTransform));
            go.transform.SetParent(mainLine, false);
            rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(60f, 60f);
            rt.pivot = new Vector2(0.5f, 0f);

            var tgo = new GameObject("Text", typeof(RectTransform));
            tgo.transform.SetParent(rt, false);
            var trt = (RectTransform)tgo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            var text = tgo.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = markerFontSize;
            text.fontStyle = FontStyles.Bold;
            text.color = markerColor;
        }

        foreach (Graphic g in rt.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;     // tidak bisa disentuh

        markerPool.Add(rt);
        markerTexts.Add(rt.GetComponentInChildren<TMP_Text>(true));
    }

    void PositionMarkers()
    {
        int count = markerTicks.Count;
        while (markerPool.Count < count) CreateMarker();

        float y = markerPrefab != null ? markerPrefab.anchoredPosition.y : markerY;
        for (int i = 0; i < markerPool.Count; i++)
        {
            RectTransform rt = markerPool[i];
            bool used = i < count && markerTicks[i] >= 0 && markerTicks[i] <= config.MaxTick;
            if (rt.gameObject.activeSelf != used) rt.gameObject.SetActive(used);
            if (!used) continue;

            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(OffsetFromLeft(markerTicks[i]), y);
            if (markerTexts[i]) markerTexts[i].text = markerNames[i];
        }

        KeepPionOnTop();
    }
}