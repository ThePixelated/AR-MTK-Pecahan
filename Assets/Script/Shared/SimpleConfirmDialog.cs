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

    Action onYes, onNo;
    string defaultTitle, defaultYes;

    void Awake()
    {
        if (titleLabel) defaultTitle = titleLabel.text;
        if (yesLabel) defaultYes = yesLabel.text;

        yesButton.onClick.AddListener(() =>
        {
            Action a = onYes;
            onYes = null; onNo = null;
            root.SetActive(false);
            a?.Invoke();
        });
        noButton.onClick.AddListener(() =>
        {
            Action n = onNo;
            onYes = null; onNo = null;
            root.SetActive(false);
            n?.Invoke();
        });
    }

    // Konfirmasi: YA menjalankan aksi, TIDAK membatalkan (dan menjalankan 'no' kalau diberikan, mis. untuk mengembalikan stepper)
    public void Ask(string message, Action yes, Action no = null) => Show(message, yes, no, true, defaultTitle, defaultYes);

    // Peringatan: satu tombol saja yang menutup popup, tanpa aksi
    public void Inform(string title, string message, string okLabel = "MENGERTI") =>
        Show(message, null, null, false, title, okLabel);

    void Show(string message, Action yes, Action no, bool showNo, string title, string yesText)
    {
        onYes = yes;
        onNo = no;
        description.text = message;
        if (titleLabel && title != null) titleLabel.text = title;
        if (yesLabel && yesText != null) yesLabel.text = yesText;
        noButton.gameObject.SetActive(showNo);
        root.SetActive(true);
    }
}