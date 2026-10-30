using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class KotakDropZone : MonoBehaviour, IFractionDropZone
{
    [SerializeField] bool isKotakA = true;
    [SerializeField] Activity4Controller controller;
    [SerializeField] RectTransform zoneRect;      // KotakAPanel / KotakBPanel
    [SerializeField] ScrollRect scroll;
    [SerializeField] RectTransform content;       // HANYA berisi row; sisa anak lama dibuang sekali saat Bind pertama
    [SerializeField] FractionBarRow rowPrefab;
    [SerializeField, Range(0.05f, 0.4f)] float edgeFraction = 0.15f;
    [SerializeField] float scrollSpeed = 900f;

    const string ChangeKotakWarning = "Mengubah kotak akan menghapus susunan di area susun. Lanjut?";

    FractionSession session;
    KotakContents contents;
    bool bound;
    bool purged;
    readonly List<FractionBarRow> rows = new List<FractionBarRow>();
    int hoverRow = -1;
    int lastCount;

    void OnEnable() { DropZoneRegistry.Register(this); Bind(); }
    void OnDisable() { DropZoneRegistry.Unregister(this); Unbind(); RowSelection.Clear(); hoverRow = -1; }

    void Bind()
    {
        if (bound) return;
        if (controller == null) { Debug.LogError($"[{name}] KotakDropZone: controller belum diisi", this); return; }

        session = controller.Session;
        contents = isKotakA ? session.A : session.B;
        contents.Changed += Refresh;
        bound = true;

        if (!purged) { PurgeContent(); purged = true; }   // buang row dummy dari scene, sekali saja
        lastCount = contents.Count;                       // sinkron ulang tanpa lompatan scroll
        Refresh();
    }

    void Unbind()
    {
        if (!bound) return;
        contents.Changed -= Refresh;
        bound = false;
    }

    void PurgeContent()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject c = content.GetChild(i).gameObject;
            c.SetActive(false);
            Destroy(c);
        }
        rows.Clear();
    }

    // ---------- tampilan: reconcile data -> row (row dipakai ulang) ----------
    void Refresh()
    {
        RowSelection.Clear();
        hoverRow = -1;

        while (rows.Count > contents.Count)                // row berlebih (setelah hapus atau reset)
        {
            FractionBarRow extra = rows[rows.Count - 1];
            rows.RemoveAt(rows.Count - 1);
            if (extra) { extra.gameObject.SetActive(false); Destroy(extra.gameObject); }
        }

        for (int i = 0; i < contents.Count; i++)
        {
            if (i >= rows.Count) rows.Add(null);
            if (!rows[i]) rows[i] = Instantiate(rowPrefab, content);   // masuk list SEBELUM Setup

            if (rows[i].transform.GetSiblingIndex() != i) rows[i].transform.SetSiblingIndex(i);
            rows[i].Setup(contents.Pieces[i], i, controller.Palette, OnRowDeleteRequested, isKotakA, OnRowLiftRequested);
        }

        bool appended = contents.Count > lastCount;
        lastCount = contents.Count;
        if (appended)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition = 0f;        // tampilkan row terbaru (paling bawah)
        }
    }

    // Row diangkat ke area: alasan penolakan (soal belum dibentuk, sudah ada di area, area penuh) ditampilkan di sini
    bool OnRowLiftRequested(int index)
    {
        RejectReason why = session.PreviewMoveToArea(isKotakA, index);
        if (why == RejectReason.None) return true;

        string msg = Messages.For(why);
        if (!string.IsNullOrEmpty(msg)) Feedback.Show(msg, FeedbackKind.Warning);
        return false;
    }

    void OnRowDeleteRequested(int index)
    {
        if (session.IsKotakLocked(isKotakA))
        {
            Feedback.Show(Messages.For(RejectReason.KotakTerkunci), FeedbackKind.Warning);
            return;
        }
        controller.ConfirmOrRun(session.KotakEditClearsDpz, ChangeKotakWarning,
            () => session.RemoveFromKotak(isKotakA, index));
    }

    public void OnResetPressed()    // onClick ResetBtn milik Kotak ini
    {
        if (!bound || contents.IsEmpty) return;
        if (session.IsKotakLocked(isKotakA))
        {
            Feedback.Show(Messages.For(RejectReason.KotakTerkunci), FeedbackKind.Warning);
            return;
        }
        controller.ConfirmOrRun(session.KotakEditClearsDpz, ChangeKotakWarning,
            () => session.ClearKotak(isKotakA));
    }

    // ---------- IFractionDropZone ----------
    public bool Contains(Vector2 p, Camera cam) => RectTransformUtility.RectangleContainsScreenPoint(zoneRect, p, cam);

    public void Hover(in DragPayload payload, Vector2 p, Camera cam, float dt)
    {
        if (payload.origin != DragOrigin.Bank) { HoverExit(); return; }   // blok dpz yang diangkat tidak memicu highlight Kotak

        // merge otomatis: yang menyala selalu row yang akan menerima, di mana pun jari di atas Kotak ini
        int target = session.FindKotakMergeTarget(isKotakA, payload.piece);
        if (target != hoverRow)
        {
            SetHighlight(hoverRow, false);
            hoverRow = target;
            SetHighlight(hoverRow, true);
        }
        AutoScroll(p, cam, dt);
    }

    public void HoverExit() { SetHighlight(hoverRow, false); hoverRow = -1; }

    public RejectReason Accept(in DragPayload payload, Vector2 p, Camera cam)
    {
        if (payload.origin != DragOrigin.Bank) return RejectReason.None;   // asal lain diurus di step 5

        Fraction piece = payload.piece;      // salin dulu: parameter `in` tidak boleh dipakai di lambda

        RejectReason why = session.PreviewDropToKotak(isKotakA, piece);
        if (why != RejectReason.None) return why;

        controller.ConfirmOrRun(session.KotakEditClearsDpz, ChangeKotakWarning,
            () => session.TryDropToKotak(isKotakA, piece));
        return RejectReason.None;
    }

    // ---------- helper ----------
    //int RowAt(Vector2 p, Camera cam)
    //{
    //    RectTransform vp = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
    //    if (!RectTransformUtility.RectangleContainsScreenPoint(vp, p, cam)) return -1;

    //    for (int i = 0; i < rows.Count; i++)
    //        if (rows[i] && rows[i].ContainsScreenPoint(p, cam)) return i;
    //    return -1;
    //}

    void SetHighlight(int i, bool on) { if (i >= 0 && i < rows.Count && rows[i]) rows[i].SetMergeHighlight(on); }

    void AutoScroll(Vector2 p, Camera cam, float dt)
    {
        RectTransform vp = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(vp, p, cam, out Vector2 local)) return;

        float scrollable = content.rect.height - vp.rect.height;
        if (scrollable <= 1f) return;

        float t = (local.y - vp.rect.yMin) / vp.rect.height;   // 0 = bawah viewport, 1 = atas
        float dir = t > 1f - edgeFraction ? 1f : (t < edgeFraction ? -1f : 0f);
        if (dir == 0f) return;

        scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + dir * scrollSpeed * dt / scrollable);
    }
}