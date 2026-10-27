using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Daftar percobaan Media 3: baris dibuat dan dipakai ulang (pool) mengikuti Media3Session.Entries.
// Tap baris = pilih, tap lagi atau tap di luar = tutup, tombol hapus = RemoveEntry.
public class TrialListView : MonoBehaviour
{
    [SerializeField] TrialEntryRow rowPrefab;
    [SerializeField] RectTransform listRoot;     // induk baris (beri VerticalLayoutGroup)
    [SerializeField] LiquidStyle style;          // untuk warna kotak (rasio sirup terhadap total)
    [SerializeField] GameObject emptyHint;       // opsional: tampil saat belum ada percobaan

    static readonly List<RaycastResult> hits = new List<RaycastResult>();
    readonly List<TrialEntryRow> rows = new List<TrialEntryRow>();

    Media3Session session;
    TrialEntryRow selected;

    // Dipanggil Media3Controller saat halaman aktif
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
        ClearSelection();
    }

    void OnDisable() => ClearSelection();

    void Rebuild()
    {
        if (session == null || rowPrefab == null || listRoot == null) return;
        ClearSelection();

        int n = session.Entries.Count;
        while (rows.Count < n) rows.Add(Instantiate(rowPrefab, listRoot));

        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < n;
            rows[i].gameObject.SetActive(used);
            if (!used) continue;

            Media3Entry e = session.Entries[i];
            rows[i].Setup(i, e, ColorOf(e), OnRowTapped, OnDelete);
        }

        if (emptyHint) emptyHint.SetActive(n == 0);
    }

    Color ColorOf(Media3Entry e) => style == null ? Color.white : style.ColorForRatio(e.SyrupRatio);

    //Color ColorOf(Media3Entry e)
    //{
    //    if (style == null) return Color.white;
    //    Fraction c = e.Concentration;
    //    float ratio = c.denominator == 0 ? 0f : (float)c.numerator / c.denominator;
    //    return style.ColorForRatio(ratio);
    //}

    void OnRowTapped(TrialEntryRow row)
    {
        if (selected == row) { ClearSelection(); return; }     // tap lagi di baris yang sama = tutup
        ClearSelection();
        selected = row;
        row.SetSelected(true);
    }

    void OnDelete(int index)
    {
        ClearSelection();
        if (session != null && session.RemoveEntry(index))
            Feedback.Show(Media3Messages.EntryRemoved(index + 1), FeedbackKind.Info);
    }

    void ClearSelection()
    {
        if (selected) selected.SetSelected(false);
        selected = null;
    }

    // Tap di luar baris terpilih menutup tombol hapus; diputuskan dari objek UI paling atas di bawah jari
    void Update()
    {
        if (selected == null || EventSystem.current == null) return;
        Pointer p = Pointer.current;
        if (p == null || !p.press.wasPressedThisFrame) return;

        var data = new PointerEventData(EventSystem.current) { position = p.position.ReadValue() };
        hits.Clear();
        EventSystem.current.RaycastAll(data, hits);      // urut dari paling atas

        bool overRow = hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(selected.transform);
        if (!overRow) ClearSelection();
    }
}