using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FractionBarRow : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] TMP_Text label;
    [SerializeField] FractionBarView barView;   // komponen di BarPivot
    [SerializeField] Image highlight;           // overlay hover merge
    [SerializeField] Button deleteButton;       // anak TERAKHIR root, awalnya nonaktif

    public Fraction Piece { get; private set; }
    public int Index { get; private set; }

    Action<int> deleteRequested;
    bool wired;
    bool hasPiece;

    public void Setup(Fraction piece, int index, FractionPalette palette, Action<int> onDelete)
    {
        Index = index;
        deleteRequested = onDelete;
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

    void OnValidate()
    {
        if (!label || !barView) Debug.LogWarning($"[{name}] FractionBarRow: label/barView belum diisi", this);
        if (!deleteButton) Debug.LogWarning($"[{name}] FractionBarRow: deleteButton belum diisi, tombol hapus tidak akan jalan", this);
    }
}