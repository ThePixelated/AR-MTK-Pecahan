using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimpleConfirmDialog : MonoBehaviour
{
    [SerializeField] GameObject root;      // objek popup yang di-SetActive; kosong = objek ini sendiri
    [SerializeField] TMP_Text description; // isi popup
    [SerializeField] Button yesButton;
    [SerializeField] Button noButton;

    [Header("Opsional: dipakai mode peringatan (Inform)")]
    [SerializeField] TMP_Text titleLabel;  // judul popup (teks tebal di atas deskripsi)
    [SerializeField] TMP_Text yesLabel;    // teks di dalam tombol YA

    Action onYes, onNo;
    string defaultTitle, defaultYes;
    bool ready;
    bool broken;

    GameObject Root => root != null ? root : gameObject;

    void Awake() => Prepare();

    // Idempotent: dipanggil dari Awake dan dari Show, jadi aman walau Awake belum jalan
    bool Prepare()
    {
        if (ready) return true;
        if (broken) return false;

        if (description == null) description = FindMessageText();

        if (yesButton == null || noButton == null || description == null)
        {
            broken = true;
            Debug.LogError($"[{name}] SimpleConfirmDialog: field belum lengkap " +
                           $"(Yes Button={(yesButton ? "ok" : "KOSONG")}, No Button={(noButton ? "ok" : "KOSONG")}, " +
                           $"Description={(description ? "ok" : "KOSONG")}). Popup tidak ditampilkan; aksinya dijalankan langsung.", this);
            return false;
        }

        if (titleLabel) defaultTitle = titleLabel.text;
        if (yesLabel) defaultYes = yesLabel.text;

        yesButton.onClick.AddListener(OnYes);
        noButton.onClick.AddListener(OnNo);
        ready = true;
        return true;
    }

    // Cadangan kalau Description belum diisi: teks pertama di dalam dialog yang bukan judul dan bukan teks tombol
    TMP_Text FindMessageText()
    {
        foreach (TMP_Text t in GetComponentsInChildren<TMP_Text>(true))
        {
            if (t == titleLabel || t == yesLabel) continue;
            if (yesButton && t.transform.IsChildOf(yesButton.transform)) continue;
            if (noButton && t.transform.IsChildOf(noButton.transform)) continue;

            Debug.LogWarning($"[{name}] SimpleConfirmDialog: 'Description' belum diisi, memakai '{t.name}'. Isi manual di Inspector.", this);
            return t;
        }
        return null;
    }

    void OnYes()
    {
        Action a = onYes;
        onYes = null; onNo = null;
        Root.SetActive(false);
        a?.Invoke();
    }

    void OnNo()
    {
        Action n = onNo;
        onYes = null; onNo = null;
        Root.SetActive(false);
        n?.Invoke();
    }

    // Konfirmasi: YA menjalankan aksi, TIDAK membatalkan (dan menjalankan 'no' kalau diberikan, mis. untuk mengembalikan stepper)
    public void Ask(string message, Action yes, Action no = null) => Show(message, yes, no, true, defaultTitle, defaultYes);

    // Peringatan: satu tombol saja yang menutup popup, tanpa aksi
    public void Inform(string title, string message, string okLabel = "MENGERTI") =>
        Show(message, null, null, false, title, okLabel);

    void Show(string message, Action yes, Action no, bool showNo, string title, string yesText)
    {
        if (!Prepare())
        {
            yes?.Invoke();     // dialog rusak: sama seperti saat dialog tidak dipasang
            return;
        }

        onYes = yes;
        onNo = no;
        description.text = message;
        if (titleLabel && title != null) titleLabel.text = title;
        if (yesLabel && yesText != null) yesLabel.text = yesText;
        noButton.gameObject.SetActive(showNo);
        Root.SetActive(true);
    }
}