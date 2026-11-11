using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum PionMark { None, Correct, Wrong }

// Pengaturan tampilan dan umpan balik pion MultiPion (warna diatur di Inspector MultiPionPresenter)
[Serializable]
public class PionFeedbackStyle
{
    [Header("Warna outline")]
    public Color highlightColor = new Color(1f, 0.85f, 0.2f, 1f);
    public Color correctColor = new Color(0.2f, 0.75f, 0.3f, 1f);
    public Color wrongColor = new Color(0.9f, 0.3f, 0.25f, 1f);

    [Header("Outline")]
    public float outlineWidth = 4f;          // tebal outline benar atau salah (tetap)
    public float pulseMinWidth = 3f;         // highlight: tebal terkecil
    public float pulseMaxWidth = 9f;         // highlight: tebal terbesar
    public float pulseSpeed = 6f;            // kecepatan denyut (radian per detik)

    [Header("Highlight (tombol tokoh yang pionnya sudah di garis ditap atau di-drag)")]
    public float highlightSeconds = 2f;
    public bool highlightOutline = true;     // outline berdenyut
    public bool highlightJump = true;        // pion melompat kecil
    public float jumpHeight = 24f;
    public float jumpSeconds = 0.3f;         // lama satu lompatan
    public int jumpCount = 2;                // jumlah lompatan di awal highlight

    [Header("Lain-lain")]
    public float selectedScale = 1.1f;       // pion yang sedang dipilih sedikit lebih besar
    [Range(0f, 1f)] public float fadedAlpha = 0.4f;   // pion di-drag keluar area: akan dikembalikan ke tombolnya saat dilepas
}

// Satu pion hewan di garis bilangan (soal MultiPion). Posisi diatur MultiPionPresenter;
// di sini: gambar, outline (script), lompat kecil, dan penerusan input.
[RequireComponent(typeof(Image))]
public class PionView : MonoBehaviour, IPointerClickHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int Index { get; private set; }
    public RectTransform Rect => (RectTransform)transform;
    public float JumpOffset { get; private set; }          // tambahan y dari lompatan highlight
    public PionMark Mark => mark;

    public event Action<PionView> Tapped;
    public event Action<PionView, PointerEventData> DragBegan;
    public event Action<PionView, PointerEventData> Dragged;
    public event Action<PionView, PointerEventData> DragEnded;

    Image image;
    Outline outline;
    PionFeedbackStyle style = new PionFeedbackStyle();
    PionMark mark;
    float highlightLeft, highlightTotal;
    bool selected, faded, useDragThreshold = true;

    void Awake() => EnsureComponents();

    void EnsureComponents()
    {
        if (image == null) image = GetComponent<Image>();
        if (outline == null)
        {
            outline = GetComponent<Outline>();
            if (outline == null) outline = gameObject.AddComponent<Outline>();
            outline.useGraphicAlpha = false;
            outline.enabled = false;
        }
    }

    public void Setup(int index, Sprite sprite, PionFeedbackStyle feedbackStyle, bool dragThreshold)
    {
        EnsureComponents();
        Index = index;
        style = feedbackStyle ?? new PionFeedbackStyle();
        useDragThreshold = dragThreshold;

        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = true;
        ResetFeedback();
    }

    // ---------------- umpan balik ----------------
    public void SetMark(PionMark value) => mark = value;
    public void SetSelected(bool value) => selected = value;
    public void SetFaded(bool value) => faded = value;

    public void Highlight()
    {
        highlightTotal = Mathf.Max(0.1f, style.highlightSeconds);
        highlightLeft = highlightTotal;
    }

    public void ResetFeedback()
    {
        mark = PionMark.None;
        highlightLeft = 0f;
        faded = false;
        selected = false;
        JumpOffset = 0f;
        if (outline != null) outline.enabled = false;
    }

    void Update()
    {
        JumpOffset = 0f;
        bool highlighting = highlightLeft > 0f;
        if (highlighting)
        {
            highlightLeft -= Time.unscaledDeltaTime;
            highlighting = highlightLeft > 0f;

            float t = highlightTotal - Mathf.Max(0f, highlightLeft);      // detik sejak highlight mulai
            if (highlighting && style.highlightJump && style.jumpCount > 0 && style.jumpSeconds > 0.01f && t < style.jumpCount * style.jumpSeconds)
            {
                float u = (t % style.jumpSeconds) / style.jumpSeconds;
                JumpOffset = style.jumpHeight * 4f * u * (1f - u);        // lengkung kecil, sama seperti lompatan pion Media 5
            }
        }

        ApplyOutline(highlighting);
        ApplyLook();
    }

    void ApplyOutline(bool highlighting)
    {
        if (highlighting && style.highlightOutline)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * style.pulseSpeed);
            SetOutline(style.highlightColor, Mathf.Lerp(style.pulseMinWidth, style.pulseMaxWidth, k));
        }
        else if (mark == PionMark.Correct) SetOutline(style.correctColor, style.outlineWidth);
        else if (mark == PionMark.Wrong) SetOutline(style.wrongColor, style.outlineWidth);
        else if (outline.enabled) outline.enabled = false;
    }

    void SetOutline(Color color, float width)
    {
        if (!outline.enabled) outline.enabled = true;
        outline.effectColor = color;
        outline.effectDistance = new Vector2(width, width);
    }

    void ApplyLook()
    {
        float scale = selected ? style.selectedScale : 1f;
        if (!Mathf.Approximately(Rect.localScale.x, scale)) Rect.localScale = new Vector3(scale, scale, 1f);

        Color c = image.color;
        float alpha = faded ? style.fadedAlpha : 1f;
        if (!Mathf.Approximately(c.a, alpha))
        {
            c.a = alpha;
            image.color = c;
        }
    }

    // ---------------- input ----------------
    public void OnPointerClick(PointerEventData e)
    {
        if (e.dragging) return;
        Tapped?.Invoke(this);
    }

    public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = useDragThreshold;
    public void OnBeginDrag(PointerEventData e) => DragBegan?.Invoke(this, e);
    public void OnDrag(PointerEventData e) => Dragged?.Invoke(this, e);
    public void OnEndDrag(PointerEventData e) => DragEnded?.Invoke(this, e);
}
