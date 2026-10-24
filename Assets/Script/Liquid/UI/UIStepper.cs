using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Stepper "- n +": nilai bulat dalam rentang min..max.
public class UIStepper : MonoBehaviour
{
    [SerializeField] TMP_Text valueText;
    [SerializeField] Button minusButton, plusButton;
    [SerializeField] int min = 1, max = 10, value = 1;

    public event Action<int> Changed;
    public int Value => value;

    void OnEnable()
    {
        if (valueText == null || minusButton == null || plusButton == null)
        {
            Debug.LogError($"[{name}] UIStepper: 'Value Text', 'Minus Button', dan 'Plus Button' harus diisi.", this);
            return;
        }
        minusButton.onClick.AddListener(Minus);
        plusButton.onClick.AddListener(Plus);
        Refresh();
    }

    void OnDisable()
    {
        if (minusButton) minusButton.onClick.RemoveListener(Minus);
        if (plusButton) plusButton.onClick.RemoveListener(Plus);
    }

    void Minus() => Set(value - 1);
    void Plus() => Set(value + 1);

    public void SetRange(int newMin, int newMax)
    {
        min = newMin;
        max = Mathf.Max(newMin, newMax);
        Set(value);
        Refresh();
    }

    public void Set(int v)
    {
        int clamped = Mathf.Clamp(v, min, max);
        bool changed = clamped != value;
        value = clamped;
        Refresh();
        if (changed) Changed?.Invoke(value);
    }

    void Refresh()
    {
        if (valueText == null || minusButton == null || plusButton == null) return;
        valueText.text = value.ToString();
        minusButton.interactable = value > min;
        plusButton.interactable = value < max;
    }
}