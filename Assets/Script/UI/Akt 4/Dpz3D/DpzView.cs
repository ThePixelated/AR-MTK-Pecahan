using System.Collections.Generic;
using UnityEngine;

public class DpzView : MonoBehaviour
{
    [SerializeField] Activity4Controller controller;
    [SerializeField] DpzStyle style;
    [SerializeField] UnitVisualSet visuals;
    [SerializeField] Material decalMaterial;           // material dengan shader ARPecahan/TrackDecal

    [Header("Area (objek visual milik lu, harus anak dari objek ini)")]
    [SerializeField] Transform outerArea;              // zona luar (catch-all)
    [SerializeField] Transform innerArea;              // area balok
    [SerializeField] float areaMeshUnit = 1f;          // ukuran mesh bawaan di X dan Z (Cube/Quad = 1, Plane bawaan Unity = 10)
    [SerializeField] float areaScaleY = 0.0004f;       // ketebalan area kalau pakai Cube
    [SerializeField] float areaSmoothing = 12f;        // makin besar makin cepat area mengikuti
    [SerializeField] bool growTowardPositiveZ = false; // default: baris baru ke arah -Z

    // Sumbu lokal: baris memanjang ke +X, baris berikutnya ke -Z (atau +Z). Origin = sudut kiri-atas area dalam.
    FractionSession session;
    bool bound;
    readonly List<DpzBlockView> blocks = new List<DpzBlockView>();
    int highlightedBlock = -1;
    Vector2 innerSize, targetInnerSize;               // (lebar X, kedalaman Z)

    float GrowSign => growTowardPositiveZ ? 1f : -1f;

    public int BlockCount => blocks.Count;

    void OnEnable() => Bind();
    void OnDisable() => Unbind();

    void Bind()
    {
        if (bound) return;
        if (controller == null || style == null || visuals == null || decalMaterial == null)
        {
            Debug.LogError($"[{name}] DpzView: controller/style/visuals/decalMaterial belum lengkap", this);
            return;
        }

        session = controller.Session;
        session.Dpz.Changed += Refresh;
        bound = true;

        Refresh();
        innerSize = targetInnerSize;   // tampil langsung dengan ukuran yang benar, tanpa animasi awal
        ApplyAreas();
    }

    void Unbind()
    {
        if (!bound) return;
        session.Dpz.Changed -= Refresh;
        bound = false;
    }

    // Data -> tampilan. Blok dipakai ulang; hanya tambah atau kurangi di ujung.
    void Refresh()
    {
        var pieces = session.Dpz.Pieces;
        int count = pieces.Count;

        while (blocks.Count > count)
        {
            DpzBlockView extra = blocks[blocks.Count - 1];
            blocks.RemoveAt(blocks.Count - 1);
            if (extra) { extra.gameObject.SetActive(false); Destroy(extra.gameObject); }
        }

        for (int i = 0; i < count; i++)
        {
            if (i >= blocks.Count) blocks.Add(null);
            if (!blocks[i]) blocks[i] = CreateBlock();   // masuk list SEBELUM Show

            Fraction p = pieces[i];
            blocks[i].Show(p, i, controller.Palette.Get(p.denominator), BlockOrigin(i));
            blocks[i].SetHighlight(i == highlightedBlock ? 1f : 0f);
        }

        if (highlightedBlock >= count) highlightedBlock = -1;
        targetInnerSize = ComputeInnerSize(Mathf.Max(style.minRows, count));
    }

    DpzBlockView CreateBlock()
    {
        var go = new GameObject("DpzBlock");
        go.transform.SetParent(transform, false);
        var b = go.AddComponent<DpzBlockView>();
        b.Init(style, visuals, decalMaterial);
        return b;
    }

    Vector2 ComputeInnerSize(int rows)
    {
        float w = style.wholeLength + 2f * style.innerPadding;
        float d = rows * style.thickness + (rows - 1) * style.rowGap + 2f * style.innerPadding;
        return new Vector2(w, d);
    }

    // Posisi lokal origin blok ke-i. Blok memanjang ke -Z dari origin-nya, jadi arah +Z digeser setebal blok.
    Vector3 BlockOrigin(int i)
    {
        float pitch = style.thickness + style.rowGap;
        float z = GrowSign < 0f
            ? -(style.innerPadding + i * pitch)
            : style.innerPadding + i * pitch + style.thickness;
        return new Vector3(style.innerPadding, 0f, z);
    }

    void Update()
    {
        if (!bound) return;
        if ((innerSize - targetInnerSize).sqrMagnitude < 1e-12f) return;

        float k = 1f - Mathf.Exp(-areaSmoothing * Time.unscaledDeltaTime);
        innerSize = Vector2.Lerp(innerSize, targetInnerSize, k);
        if ((innerSize - targetInnerSize).sqrMagnitude < 1e-10f) innerSize = targetInnerSize;
        ApplyAreas();
    }

    // Sudut kiri-atas kedua area tetap; hanya sisi kanan dan bawah yang bergerak (pusat bergeser, ukuran bertambah)
    void ApplyAreas()
    {
        Vector2 outer = innerSize + Vector2.one * (2f * style.outerMargin);
        float cx = innerSize.x * 0.5f;
        float cz = GrowSign * innerSize.y * 0.5f;

        PlaceArea(innerArea, innerSize, cx, cz);
        PlaceArea(outerArea, outer, cx, cz);
    }

    void PlaceArea(Transform area, Vector2 size, float cx, float cz)
    {
        if (!area) return;
        area.localPosition = new Vector3(cx, area.localPosition.y, cz);
        area.localScale = new Vector3(size.x / areaMeshUnit, areaScaleY, size.y / areaMeshUnit);
    }

    // Dipakai debug zone (5b-1) dan zona dunia (5b-2)
    public void SetHighlightedBlock(int index)
    {
        if (index == highlightedBlock) return;

        if (highlightedBlock >= 0 && highlightedBlock < blocks.Count && blocks[highlightedBlock])
            blocks[highlightedBlock].SetHighlight(0f);

        highlightedBlock = index;
        if (index >= 0 && index < blocks.Count && blocks[index]) blocks[index].SetHighlight(1f);
    }

    [ContextMenu("Debug: Refresh Visuals")]   // klik setelah mengubah DpzStyle saat Play
    void DebugRefreshVisuals()
    {
        foreach (var b in blocks) if (b) b.InvalidateVisual();
        if (bound) Refresh();
    }
}