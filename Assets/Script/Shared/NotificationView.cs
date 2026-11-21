using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Alur: pesan masuk -> open -> hold (holdSeconds) -> close.
// Pesan baru kapan pun (saat opening, hold, atau closing): teks langsung diganti, bar tetap/kembali terbuka,
// hold dihitung ulang dari pesan terakhir. Close hanya terjadi kalau tidak ada pesan baru selama holdSeconds.
public class NotificationView : MonoBehaviour
{
    [Header("Animasi (entri di PanelAnimator)")]
    [SerializeField] PanelAnimator animator;
    [SerializeField] int openIndex = 0;
    [SerializeField] int closeIndex = 1;
    [Tooltip("Lama bar tetap terbuka SETELAH selesai open, sebelum mulai close.")]
    [SerializeField] float holdSeconds = 2.2f;

    [Header("Tampilan")]
    [SerializeField] Image background;
    [SerializeField] TMP_Text message;
    [SerializeField] Image icon;   // opsional
    [SerializeField] Color textColor = new Color(0.15f, 0.15f, 0.15f);
    [SerializeField] Color infoColor = new Color(0.76f, 0.86f, 1.00f);
    [SerializeField] Color successColor = new Color(0.72f, 0.92f, 0.76f);
    [SerializeField] Color warningColor = new Color(1.00f, 0.90f, 0.60f);
    [SerializeField] Color errorColor = new Color(0.98f, 0.74f, 0.74f);
    [SerializeField] Sprite infoIcon, successIcon, warningIcon, errorIcon;   // opsional

    bool visible;          // true dari open dipicu sampai close dipicu
    float openFinishAt;    // kapan animasi open yang terakhir dipicu selesai
    float hideAt;          // kapan close dipicu kalau tidak ada pesan baru
    Coroutine runner;

    void OnEnable()
    {
        // Bar tidak boleh menahan tap di area yang ditutupinya
        if (background) background.raycastTarget = false;
        if (message) message.raycastTarget = false;
        if (icon) icon.raycastTarget = false;

        Feedback.Raised += OnRaised;
        visible = false;
        runner = null;
        if (animator) animator.TriggerSingle(closeIndex);   // pastikan tertutup tiap kali page aktif
    }

    void OnDisable()
    {
        Feedback.Raised -= OnRaised;
        visible = false;
        runner = null;      // coroutine ikut berhenti saat nonaktif
    }

    void OnRaised(string text, FeedbackKind kind)
    {
        if (!isActiveAndEnabled || animator == null || string.IsNullOrEmpty(text)) return;

        Apply(text, kind);   // teks selalu langsung berganti

        if (!visible)
        {
            // Tertutup atau sedang menutup: buka (menutup yang berjalan otomatis dibatalkan, lanjut dari posisi sekarang)
            animator.TriggerSingle(openIndex);
            visible = true;
            openFinishAt = Time.unscaledTime + animator.DurationOf(openIndex);
        }
        // Sudah terbuka atau sedang membuka: tidak perlu memicu apa pun, cukup mulai ulang hold

        hideAt = Mathf.Max(Time.unscaledTime, openFinishAt) + holdSeconds;

        if (runner == null) runner = StartCoroutine(WaitThenClose());
    }

    IEnumerator WaitThenClose()
    {
        while (Time.unscaledTime < hideAt) yield return null;   // hideAt bisa diperpanjang pesan baru selama menunggu

        runner = null;
        visible = false;
        animator.TriggerSingle(closeIndex);
    }

    void Apply(string text, FeedbackKind kind)
    {
        Color bg; Sprite sp;
        switch (kind)
        {
            case FeedbackKind.Success: bg = successColor; sp = successIcon; break;
            case FeedbackKind.Warning: bg = warningColor; sp = warningIcon; break;
            case FeedbackKind.Error: bg = errorColor; sp = errorIcon; break;

            default: bg = infoColor; sp = infoIcon; break;
        }

        if (background) background.color = bg;
        if (message) { message.text = text; message.color = textColor; }
        if (icon) { icon.sprite = sp; icon.enabled = sp != null; }
    }
}