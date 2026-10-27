using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Satu baris percobaan di daftar Media 3: nomor, jumlah air dan sirup, kotak warna. Tap baris untuk memilih, lalu tombol hapus muncul.
public class TrialEntryRow : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] TMP_Text numberText;        // "Percobaan 1"
    [SerializeField] TMP_Text waterText;
    [SerializeField] TMP_Text syrupText;
    [SerializeField] Image colorSwatch;
    [SerializeField] GameObject highlight;       // opsional: tanda baris terpilih (nonaktif di prefab)
    [SerializeField] Button deleteButton;        // nonaktif di prefab, anak terakhir root

    int index;
    bool initialized;
    Action<TrialEntryRow> onTap;
    Action<int> onDelete;

    public int Index => index;

    public void Setup(int index, Media3Entry entry, Color color, Action<TrialEntryRow> onTap, Action<int> onDelete)
    {
        if (!initialized)
        {
            initialized = true;
            if (deleteButton) deleteButton.onClick.AddListener(() => this.onDelete?.Invoke(this.index));
        }

        this.index = index;
        this.onTap = onTap;
        this.onDelete = onDelete;

        if (numberText) numberText.text = $"{index + 1}";
        if (waterText) waterText.text = entry.waterSections.ToString();
        if (syrupText) syrupText.text = entry.syrupSections.ToString();
        if (colorSwatch) colorSwatch.color = color;
        SetSelected(false);
    }

    public void OnPointerClick(PointerEventData eventData) => onTap?.Invoke(this);

    public void SetSelected(bool on)
    {
        if (deleteButton) deleteButton.gameObject.SetActive(on);
        if (highlight) highlight.SetActive(on);
    }
}