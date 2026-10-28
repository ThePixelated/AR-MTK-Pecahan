using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

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
    [SerializeField] float pionSmoothing = 30f;           // 0 = langsung loncat ke tick

    class Tick
    {
        public RectTransform rt;
        public TMP_Text label;
    }

    RectTransform mainLine;
    readonly List<Tick> wholePool = new List<Tick>();
    readonly List<Tick> fractionPool = new List<Tick>();

    NumberLineConfig config = new NumberLineConfig(1, 5);
    int pionTick;
    bool structureDirty = true;
    bool layoutDirty = true;

    // Dilaporkan saat pion di-drag: tick terdekat dari posisi pointer
    public event Action<int> PionDragged;

    public bool DragEnabled { get; set; } = true;
    public NumberLineConfig Config => config;

    void Awake() => mainLine = (RectTransform)transform;

    void OnEnable()
    {
        structureDirty = true;
        layoutDirty = true;
    }

    // Terpanggil saat lebar main line berubah (resolusi, rotasi layar, anchor stretch)
    void OnRectTransformDimensionsChange() => layoutDirty = true;

    // ---------------- API ----------------
    public void SetConfig(NumberLineConfig newConfig)
    {
        if (newConfig.length == config.length && newConfig.partisi == config.partisi && !structureDirty) return;
        config = newConfig;
        structureDirty = true;
        if (pionTick > config.MaxTick) pionTick = config.MaxTick;
    }

    public void SetPionTick(int tick) => pionTick = tick < 0 ? 0 : tick > config.MaxTick ? config.MaxTick : tick;

    public void SetPionVisible(bool visible)
    {
        if (pion && pion.gameObject.activeSelf != visible) pion.gameObject.SetActive(visible);
    }

    // ---------------- geometri ----------------
    static float WidthOf(RectTransform rt) => rt == null ? 0f : (rt.sizeDelta.x > 0f ? rt.sizeDelta.x : rt.rect.width);

    float WholeWidth => WidthOf(wholeTickPrefab);

    // Jarak antara pusat tick pertama dan pusat tick terakhir
    float Span => Mathf.Max(0f, mainLine.rect.width - WholeWidth);

    // Jarak pusat tick dari tepi kiri main line
    float OffsetFromLeft(int tick) => WholeWidth * 0.5f + config.TickToNormalized(tick) * Span;

    bool LayoutReady => mainLine.rect.width > WholeWidth && WholeWidth > 0f;

    // ---------------- tick ----------------
    void LateUpdate()
    {
        if (wholeTickPrefab == null || fractionTickPrefab == null) return;

        if (structureDirty)
        {
            RebuildStructure();
            structureDirty = false;
            layoutDirty = true;
        }

        if (layoutDirty && LayoutReady)
        {
            PositionTicks();
            layoutDirty = false;
        }

        UpdatePion();
    }

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

        // tick baru ditambahkan sebagai anak terakhir; pion harus tetap tergambar di atasnya
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

    // ---------------- pion ----------------
    void UpdatePion()
    {
        if (pion == null || !pion.gameObject.activeInHierarchy || !LayoutReady) return;

        // pusat tick di ruang main line -> dunia -> ruang lokal parent pion (tidak bergantung pada anchor)
        Vector3 world = mainLine.TransformPoint(new Vector3(mainLine.rect.xMin + OffsetFromLeft(pionTick), 0f, 0f));
        Vector3 target = pion.parent.InverseTransformPoint(world);

        Vector3 lp = pion.localPosition;
        float x = pionSmoothing <= 0f
            ? target.x
            : Mathf.Lerp(lp.x, target.x, 1f - Mathf.Exp(-pionSmoothing * Time.unscaledDeltaTime));
        if (Mathf.Abs(x - target.x) < 0.05f) x = target.x;

        pion.localPosition = new Vector3(x, lp.y, lp.z);
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
}