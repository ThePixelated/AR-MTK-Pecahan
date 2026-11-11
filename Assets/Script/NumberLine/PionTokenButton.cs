using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Tombol tokoh di sisi kiri (prefab, di-instantiate otomatis oleh MultiPionPresenter ke grid).
// Tap = pilih (garis ganti partisi); drag ke sisi kanan = hewan menempel ke garis.
public class PionTokenButton : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] TMP_Text label;             // teks tokoh dari soal (mis. "Rahmi")
    [SerializeField] Image icon;                 // opsional: ikon hewan pion ini
    [SerializeField] GameObject selectedMark;    // opsional: penanda terpilih
    [SerializeField] GameObject placedMark;      // opsional: penanda "sudah di garis"

    public int Index { get; private set; }
    public RectTransform Rect => (RectTransform)transform;
    public string Label => label != null ? label.text : "";

    public event Action<PionTokenButton> Tapped;
    public event Action<PionTokenButton, PointerEventData> DragBegan;
    public event Action<PionTokenButton, PointerEventData> Dragged;
    public event Action<PionTokenButton, PointerEventData> DragEnded;

    public void Setup(int index, string text, Sprite sprite)
    {
        Index = index;
        if (label != null) label.text = text;
        if (icon != null && sprite != null)
        {
            icon.sprite = sprite;
            icon.preserveAspect = true;
        }
        SetSelected(false);
        SetPlaced(false);
    }

    public void SetSelected(bool value)
    {
        if (selectedMark != null && selectedMark.activeSelf != value) selectedMark.SetActive(value);
    }

    public void SetPlaced(bool value)
    {
        if (placedMark != null && placedMark.activeSelf != value) placedMark.SetActive(value);
    }

    // Salinan untuk ghost drag: tidak menerima input apa pun
    public void MakeInert()
    {
        enabled = false;
        var group = gameObject.GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.dragging) return;
        Tapped?.Invoke(this);
    }

    public void OnBeginDrag(PointerEventData e) => DragBegan?.Invoke(this, e);
    public void OnDrag(PointerEventData e) => Dragged?.Invoke(this, e);
    public void OnEndDrag(PointerEventData e) => DragEnded?.Invoke(this, e);
}
