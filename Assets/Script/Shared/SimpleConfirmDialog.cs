using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimpleConfirmDialog : MonoBehaviour
{
    [SerializeField] GameObject root;      // objek popup yang di-SetActive
    [SerializeField] TMP_Text description;
    [SerializeField] Button yesButton;
    [SerializeField] Button noButton;

    [Header("Opsional: dipakai mode peringatan (Inform)")]
    [SerializeField] TMP_Text titleLabel;  // judul popup (teks tebal di atas deskripsi)
    [SerializeField] TMP_Text yesLabel;    // teks di dalam tombol YA

    Action onYes;
    string defaultTitle, defaultYes;

    void Awake()
    {
        if (titleLabel) defaultTitle = titleLabel.text;
        if (yesLabel) defaultYes = yesLabel.text;

        yesButton.onClick.AddListener(() => { Action a = onYes; onYes = null; root.SetActive(false); a?.Invoke(); });
        noButton.onClick.AddListener(() => { onYes = null; root.SetActive(false); });
    }

    // Konfirmasi: YA menjalankan aksi, TIDAK membatalkan (judul dan teks tombol kembali ke bawaan scene)
    public void Ask(string message, Action yes) => Show(message, yes, true, defaultTitle, defaultYes);

    // Peringatan: satu tombol saja yang menutup popup, tanpa aksi
    public void Inform(string title, string message, string okLabel = "MENGERTI") =>
        Show(message, null, false, title, okLabel);

    void Show(string message, Action yes, bool showNo, string title, string yesText)
    {
        onYes = yes;
        description.text = message;
        if (titleLabel && title != null) titleLabel.text = title;
        if (yesLabel && yesText != null) yesLabel.text = yesText;
        noButton.gameObject.SetActive(showNo);
        root.SetActive(true);
    }
}