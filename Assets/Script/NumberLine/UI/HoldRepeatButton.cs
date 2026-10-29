using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Tombol tekan-dan-tahan: satu langkah saat ditekan, lalu berulang selama ditahan.
// Komponen Button di objek yang sama hanya untuk tampilan (warna tekan dan disabled); JANGAN isi onClick-nya.
public class HoldRepeatButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] float initialDelay = 0.4f;       // jeda sebelum mulai mengulang
    [SerializeField] float repeatInterval = 0.18f;    // jarak antar langkah saat ditahan (samakan dengan durasi hop)

    public event Action Step;

    Button button;
    bool holding;
    float nextStepTime;

    public bool Interactable
    {
        get => button == null || button.IsInteractable();
        set
        {
            if (button) button.interactable = value;
            if (!value) holding = false;
        }
    }

    void Awake() => button = GetComponent<Button>();

    void OnDisable() => holding = false;

    public void OnPointerDown(PointerEventData e)
    {
        if (!Interactable) return;
        holding = true;
        nextStepTime = Time.unscaledTime + initialDelay;
        Step?.Invoke();
    }

    public void OnPointerUp(PointerEventData e) => holding = false;
    public void OnPointerExit(PointerEventData e) => holding = false;

    void Update()
    {
        if (!holding) return;
        if (!Interactable) { holding = false; return; }

        if (Time.unscaledTime >= nextStepTime)
        {
            nextStepTime = Time.unscaledTime + repeatInterval;
            Step?.Invoke();
        }
    }
}