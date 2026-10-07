using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NotificationView : MonoBehaviour
{
    [Header("Animasi (entri di PanelAnimator)")]
    [SerializeField] PanelAnimator animator;
    [SerializeField] int openIndex = 0;
    [SerializeField] int closeIndex = 1;
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

    bool isOpen;
    Coroutine hold;

    void OnEnable()
    {
        // Bar tidak boleh menahan tap di area yang ditutupinya
        if (background) background.raycastTarget = false;
        if (message) message.raycastTarget = false;
        if (icon) icon.raycastTarget = false;

        Feedback.Raised += OnRaised;
        isOpen = false;
        if (animator) animator.Trigger(closeIndex);   // pastikan tertutup tiap kali page aktif
    }

    void OnDisable()
    {
        Feedback.Raised -= OnRaised;
        hold = null;      // coroutine ikut berhenti saat nonaktif
        isOpen = false;
    }

    void OnRaised(string text, FeedbackKind kind)
    {
        if (!isActiveAndEnabled || animator == null || string.IsNullOrEmpty(text)) return;

        Apply(text, kind);

        if (!isOpen) { isOpen = true; animator.Trigger(openIndex); }   // sedang terbuka: cukup ganti teks

        if (hold != null) StopCoroutine(hold);
        hold = StartCoroutine(HoldThenClose());                        // timer mulai ulang tiap pesan baru
    }

    IEnumerator HoldThenClose()
    {
        yield return new WaitForSecondsRealtime(holdSeconds);
        hold = null;
        isOpen = false;
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