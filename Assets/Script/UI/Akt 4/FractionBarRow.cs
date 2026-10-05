using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FractionBarRow : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] TMP_Text label;
    [SerializeField] Image bar;            // BarImg
    [SerializeField] Image highlight;      // opsional: overlay hover merge (raycastTarget OFF)
    [SerializeField] Button deleteButton;  // di sisi kanan row, awalnya nonaktif

    public Fraction Piece { get; private set; }
    public int Index { get; private set; }

    public void Setup(Fraction piece, int index, FractionPalette palette, Action<int> deleteRequested)
    {
        Piece = piece; Index = index;

        if (label) label.text = FractionFormat.Label(piece);
        if (bar) bar.color = palette.Get(piece.denominator);

        SetMergeHighlight(false);
        ShowDelete(false);

        if (deleteButton)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => { RowSelection.Clear(); deleteRequested?.Invoke(Index); });
        }
    }

    public bool ContainsScreenPoint(Vector2 p, Camera cam) =>
        RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, p, cam);

    public void SetMergeHighlight(bool on) { if (highlight) highlight.enabled = on; }

    // Polish nanti: expand kanan ke kiri pakai RectTween
    public void ShowDelete(bool on) { if (deleteButton) deleteButton.gameObject.SetActive(on); }

    public void OnPointerClick(PointerEventData e) => RowSelection.Select(this);

    void OnValidate()
    {
        if (!label || !bar) Debug.LogWarning($"[{name}] FractionBarRow: label/bar belum diisi", this);
        if (!deleteButton) Debug.LogWarning($"[{name}] FractionBarRow: deleteButton belum diisi, tombol hapus tidak akan jalan", this);
    }
}