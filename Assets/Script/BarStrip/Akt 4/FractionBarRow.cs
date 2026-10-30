using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FractionBarRow : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] TMP_Text label;
    [SerializeField] FractionBarView barView;   // komponen di BarPivot
    [SerializeField] Image highlight;           // overlay hover merge
    [SerializeField] Button deleteButton;       // anak TERAKHIR root, awalnya nonaktif

    public Fraction Piece { get; private set; }
    public int Index { get; private set; }

    enum DragMode { None, Lift, Scroll }

    Action<int> deleteRequested;
    Func<int, bool> canLift;       // true = boleh diangkat ke area (zona yang menampilkan pesan penolakan)
    bool isKotakA = true;
    bool wired;
    bool hasPiece;
    DragMode dragMode;
    ScrollRect scroll;

    public void Setup(Fraction piece, int index, FractionPalette palette, Action<int> onDelete,
                      bool isKotakA = true, Func<int, bool> canLift = null)
    {
        Index = index;
        deleteRequested = onDelete;
        this.isKotakA = isKotakA;
        this.canLift = canLift;
        WireOnce();

        SetMergeHighlight(false);
        ShowDelete(false);

        bool same = hasPiece && piece.numerator == Piece.numerator && piece.denominator == Piece.denominator;
        Piece = piece;
        hasPiece = true;
        if (same) return;   // tampilan sudah benar: bar dan divider tidak dihitung ulang

        if (label) label.text = FractionFormat.Label(piece);
        if (barView) barView.Show(piece, palette.Get(piece.denominator));
    }

    void WireOnce()
    {
        if (wired) return;
        wired = true;

        // Cuma root (dan DeleteBtn) yang boleh menerima tap; teks dan highlight tidak boleh menutupi tombol
        if (label) label.raycastTarget = false;
        if (highlight) highlight.raycastTarget = false;

        if (!deleteButton) return;

        // RemoveAllListeners tidak menghapus listener dari Inspector, jadi yang tersisa dimatikan
        int persistent = deleteButton.onClick.GetPersistentEventCount();
        for (int i = 0; i < persistent; i++)
            deleteButton.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
        if (persistent > 0)
            Debug.LogWarning($"[{name}] DeleteBtn punya {persistent} onClick dari Inspector, dimatikan. Kosongkan di prefab.", this);

        deleteButton.onClick.AddListener(OnDeleteClicked);
    }

    void OnDeleteClicked()
    {
        RowSelection.Clear();
        deleteRequested?.Invoke(Index);
    }

    public bool ContainsScreenPoint(Vector2 p, Camera cam) =>
        RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, p, cam);

    public void SetMergeHighlight(bool on) { if (highlight) highlight.enabled = on; }

    // Polish nanti: expand kanan ke kiri pakai RectTween
    public void ShowDelete(bool on) { if (deleteButton) deleteButton.gameObject.SetActive(on); }

    public void OnPointerClick(PointerEventData e) => RowSelection.Select(this);

    // ---------- angkat ke area ----------
    // Gerak dominan vertikal = menggulir daftar; selain itu = mengangkat row ke area (pipeline drag yang sama dengan bank)
    public void OnBeginDrag(PointerEventData e)
    {
        dragMode = DragMode.None;
        RowSelection.Clear();
        scroll = GetComponentInParent<ScrollRect>();

        bool vertical = Mathf.Abs(e.delta.y) > Mathf.Abs(e.delta.x);
        if (vertical && scroll != null)
        {
            dragMode = DragMode.Scroll;
            ExecuteEvents.Execute(scroll.gameObject, e, ExecuteEvents.beginDragHandler);
            return;
        }

        FractionDragController drag = FractionDragController.Instance;
        if (drag == null) return;
        if (canLift != null && !canLift(Index)) return;      // ditolak: zona sudah menampilkan alasannya

        var payload = new DragPayload(Piece, isKotakA ? DragOrigin.KotakA : DragOrigin.KotakB, Index);
        if (drag.TryBegin(payload, e.position, e.pressEventCamera)) dragMode = DragMode.Lift;
    }

    public void OnDrag(PointerEventData e)
    {
        if (dragMode == DragMode.Scroll && scroll) ExecuteEvents.Execute(scroll.gameObject, e, ExecuteEvents.dragHandler);
        else if (dragMode == DragMode.Lift && FractionDragController.Instance) FractionDragController.Instance.Move(e.position);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (dragMode == DragMode.Scroll && scroll) ExecuteEvents.Execute(scroll.gameObject, e, ExecuteEvents.endDragHandler);
        else if (dragMode == DragMode.Lift && FractionDragController.Instance) FractionDragController.Instance.End(e.position);
        dragMode = DragMode.None;
    }

    void OnValidate()
    {
        if (!label || !barView) Debug.LogWarning($"[{name}] FractionBarRow: label/barView belum diisi", this);
        if (!deleteButton) Debug.LogWarning($"[{name}] FractionBarRow: deleteButton belum diisi, tombol hapus tidak akan jalan", this);
    }
}