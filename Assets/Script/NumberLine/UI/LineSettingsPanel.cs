using System;
using UnityEngine;
using UnityEngine.UI;

// Panel atur garis bilangan (prefab): tombol panjang 1-5 dan stepper partisi. Tidak tahu session; controller yang menghubungkan.
public class LineSettingsPanel : MonoBehaviour
{
    [SerializeField] Button[] lengthButtons;             // GarisVal1Btn..GarisVal5Btn, indeks 0 = panjang 1
    [SerializeField] GameObject[] lengthSelectedMarks;   // opsional, sejajar dengan lengthButtons
    [SerializeField] UIStepper partisiStepper;
    [SerializeField] GameObject lockOverlay;             // opsional: Image yang memblokir raycast saat panel dikunci

    public event Action<int> LengthChosen;
    public event Action<int> PartisiChanged;

    bool suppress;

    void Awake()
    {
        for (int i = 0; i < lengthButtons.Length; i++)
        {
            int value = i + 1;
            lengthButtons[i].onClick.AddListener(() => LengthChosen?.Invoke(value));
        }
        partisiStepper.Changed += OnStepperChanged;
        if (lockOverlay) lockOverlay.SetActive(false);
    }

    void OnDestroy()
    {
        if (partisiStepper) partisiStepper.Changed -= OnStepperChanged;
    }

    void OnStepperChanged(int value)
    {
        if (!suppress) PartisiChanged?.Invoke(value);
    }

    // Rentang dari limits: stepper partisi dan tombol panjang yang boleh tampil
    public void Configure(NumberLineLimits limits)
    {
        suppress = true;
        partisiStepper.SetRange(limits.MinPartisi, limits.MaxPartisi);
        suppress = false;

        for (int i = 0; i < lengthButtons.Length; i++)
        {
            int value = i + 1;
            lengthButtons[i].gameObject.SetActive(value >= limits.MinLength && value <= limits.MaxLength);
        }
    }

    // Tampilkan konfigurasi yang berlaku tanpa memicu event
    public void Show(NumberLineConfig config)
    {
        suppress = true;
        partisiStepper.Set(config.partisi);
        suppress = false;

        if (lengthSelectedMarks == null) return;
        for (int i = 0; i < lengthSelectedMarks.Length; i++)
            if (lengthSelectedMarks[i]) lengthSelectedMarks[i].SetActive(i + 1 == config.length);
    }

    public void SetLocked(bool locked)
    {
        if (lockOverlay) lockOverlay.SetActive(locked);
    }
}