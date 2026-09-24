using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Controller animasi panel HUD Graph + button toggle-nya.
// Semua gerakan pakai anchoredPosition, jadi aman buat berbagai ukuran layar.
public class GraphPanelToggle : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform panel;        // ViewPortPanel (HUD)
    [SerializeField] private RectTransform button;       // RectTransform button toggle
    [SerializeField] private Button toggleButton;        // Button component di button toggle
    [SerializeField] private Button panelTapButton;      // (opsional) Button di background panel buat deteksi tap -> peek

    [Header("Panel positions (anchoredPosition)")]
    [SerializeField] private Vector2 panelShownPos;      // panel in-frame
    [SerializeField] private Vector2 panelHiddenPos;     // panel geser kanan, keluar frame

    [Header("Button positions (anchoredPosition)")]
    [SerializeField] private Vector2 buttonBehindPos;    // nyempil di belakang panel
    [SerializeField] private Vector2 buttonShownPos;     // keluar, in-frame (peek & locked)

    [Header("Timing (detik)")]
    [SerializeField] private float buttonTuckDur = 0.2f;   // ease-in ke belakang
    [SerializeField] private float buttonRevealDur = 0.5f; // ease-out bounce keluar
    [SerializeField] private float panelSlideDur = 0.35f;
    [SerializeField] private float peekHoldTime = 1.0f;    // lama button nongol pas panel di-tap

    [Header("State awal")]
    [SerializeField] private bool startShown = true;

    [Header("SFX")]
    [SerializeField] private AudioClip panelSlideSfx;
    [SerializeField, Range(0f, 1f)] private float panelSlideVolume = 0.8f;

    private bool isShown;
    private bool isAnimating;
    private Coroutine peekRoutine;

    private void Awake()
    {
        if (toggleButton) toggleButton.onClick.AddListener(OnTogglePressed);
        if (panelTapButton) panelTapButton.onClick.AddListener(OnPanelTapped);

        isShown = startShown;
        panel.anchoredPosition = isShown ? panelShownPos : panelHiddenPos;
        button.anchoredPosition = isShown ? buttonBehindPos : buttonShownPos;
    }

    // ---- Input handlers ----

    private void OnTogglePressed()
    {
        if (isAnimating) return;
        StopPeek();
        StartCoroutine(isShown ? HideSequence() : ShowSequence());
    }

    private void OnPanelTapped()
    {
        // peek cuma pas panel lagi tampil & gak lagi animasi toggle
        if (isAnimating || !isShown) return;
        if (peekRoutine != null) StopCoroutine(peekRoutine);
        peekRoutine = StartCoroutine(PeekHint());
    }

    // ---- Sequences ----

    private IEnumerator HideSequence()
    {
        isAnimating = true;
        // 1) button ease-in ke belakang panel
        yield return AnimatePos(button, buttonBehindPos, buttonTuckDur, EaseInCubic);
        // 2) panel geser kanan keluar frame
        //if (panelSlideSfx != null) SfxPlayer.Play2D(panelSlideSfx, panelSlideVolume);
        yield return AnimatePos(panel, panelHiddenPos, panelSlideDur, EaseInCubic);
        // 3) button ease-out bounce keluar, in-frame (locked)
        yield return AnimatePos(button, buttonShownPos, buttonRevealDur, EaseOutBounce);
        isShown = false;
        isAnimating = false;
    }

    private IEnumerator ShowSequence()
    {
        isAnimating = true;
        // 1) button ease-in ke belakang panel (ikut keluar frame)
        yield return AnimatePos(button, buttonBehindPos, buttonTuckDur, EaseInCubic);
        // 2) panel ease-out bounce masuk frame (button tetap di belakang)
        //if (panelSlideSfx != null) SfxPlayer.Play2D(panelSlideSfx, panelSlideVolume);
        yield return AnimatePos(panel, panelShownPos, panelSlideDur, EaseOutBounce);
        isShown = true;
        isAnimating = false;
    }

    private IEnumerator PeekHint()
    {
        yield return AnimatePos(button, buttonShownPos, buttonRevealDur, EaseOutBounce);
        yield return new WaitForSecondsRealtime(peekHoldTime);
        yield return AnimatePos(button, buttonBehindPos, buttonTuckDur, EaseInCubic);
        peekRoutine = null;
    }

    private void StopPeek()
    {
        if (peekRoutine != null) { StopCoroutine(peekRoutine); peekRoutine = null; }
    }

    // ---- Core tween ----

    private IEnumerator AnimatePos(RectTransform rt, Vector2 to, float dur, System.Func<float, float> ease)
    {
        Vector2 from = rt.anchoredPosition;
        if (dur <= 0f) { rt.anchoredPosition = to; yield break; }

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime; // unscaled -> aman walau Time.timeScale diubah
            rt.anchoredPosition = Vector2.LerpUnclamped(from, to, ease(Mathf.Clamp01(t / dur)));
            yield return null;
        }
        rt.anchoredPosition = to;
    }

    // ---- Easing ----

    private static float EaseInCubic(float t) => t * t * t;

    private static float EaseOutBounce(float t)
    {
        const float n1 = 7.5625f, d1 = 2.75f;
        if (t < 1f / d1) return n1 * t * t;
        if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
        if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
        t -= 2.625f / d1; return n1 * t * t + 0.984375f;
    }

    // ---- Helper isi posisi dari editor (klik kanan komponen) ----
    // Taruh objek di posisi yang lu mau, lalu klik kanan komponen -> pilih capture-nya.
    [ContextMenu("Capture: Panel Shown")] private void CapPanelShown() => panelShownPos = panel.anchoredPosition;
    [ContextMenu("Capture: Panel Hidden")] private void CapPanelHidden() => panelHiddenPos = panel.anchoredPosition;
    [ContextMenu("Capture: Button Behind")] private void CapBtnBehind() => buttonBehindPos = button.anchoredPosition;
    [ContextMenu("Capture: Button Shown")] private void CapBtnShown() => buttonShownPos = button.anchoredPosition;
}