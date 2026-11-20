using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NotificationView : MonoBehaviour
{
    [Header("Animasi (entri di PanelAnimator)")]
    [SerializeField] PanelAnimator animator;
    [SerializeField] int openIndex = 0;
    [SerializeField] int closeIndex = 1;

    [Header("Waktu baca")]
    [Tooltip("Waktu minimal pesan terlihat, dihitung SETELAH bar selesai terbuka (durasi entri Open tidak memotongnya).")]
    [SerializeField] float holdSeconds = 2.2f;
    [Tooltip("Tambahan waktu per karakter, supaya pesan panjang tidak hilang sebelum selesai dibaca.")]
    [SerializeField] float secondsPerCharacter = 0.04f;
    [SerializeField] float maxHoldSeconds = 8f;
    [Tooltip("Pesan yang datang saat pesan lain masih tampil diantre, bukan langsung menimpa. Yang paling lama dibuang bila penuh.")]
    [SerializeField] int maxQueued = 3;

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

    struct Item
    {
        public string text;
        public FeedbackKind kind;
    }

    readonly Queue<Item> queue = new Queue<Item>();
    Coroutine runner;
    string currentText;
    float hideAt;

    void OnEnable()
    {
        // Bar tidak boleh menahan tap di area yang ditutupinya
        if (background) background.raycastTarget = false;
        if (message) message.raycastTarget = false;
        if (icon) icon.raycastTarget = false;

        Feedback.Raised += OnRaised;
        if (animator) animator.Trigger(closeIndex);   // pastikan tertutup tiap kali page aktif
    }

    void OnDisable()
    {
        Feedback.Raised -= OnRaised;
        queue.Clear();
        runner = null;        // coroutine ikut berhenti saat nonaktif
        currentText = null;
    }

    void OnRaised(string text, FeedbackKind kind)
    {
        if (!isActiveAndEnabled || animator == null || string.IsNullOrEmpty(text)) return;

        // Pesan yang sama dengan yang sedang tampil: cukup perpanjang waktunya, jangan diulang
        if (runner != null && text == currentText)
        {
            hideAt = Mathf.Max(hideAt, Time.unscaledTime + HoldFor(text));
            return;
        }

        // Pesan yang sama dengan antrean paling belakang: abaikan
        if (queue.Count > 0)
        {
            Item last = default;
            foreach (Item it in queue) last = it;
            if (last.text == text) return;
        }

        while (queue.Count >= Mathf.Max(1, maxQueued)) queue.Dequeue();
        queue.Enqueue(new Item { text = text, kind = kind });

        if (runner == null) runner = StartCoroutine(Run());
    }

    float HoldFor(string text) =>
        Mathf.Max(holdSeconds, Mathf.Min(maxHoldSeconds, holdSeconds + text.Length * secondsPerCharacter));

    IEnumerator Run()
    {
        while (queue.Count > 0)
        {
            Item it = queue.Dequeue();
            currentText = it.text;
            Apply(it.text, it.kind);

            // Selalu minta buka. Kalau sudah terbuka, tween berjalan ke posisi yang sama (tidak terlihat).
            // Kalau sedang menutup, penutupan dibatalkan dan bar naik lagi.
            animator.Trigger(openIndex);

            // Waktu baca baru mulai setelah bar selesai terbuka
            hideAt = Time.unscaledTime + animator.DurationOf(openIndex) + HoldFor(it.text);
            while (Time.unscaledTime < hideAt) yield return null;
        }

        currentText = null;
        runner = null;
        animator.Trigger(closeIndex);
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