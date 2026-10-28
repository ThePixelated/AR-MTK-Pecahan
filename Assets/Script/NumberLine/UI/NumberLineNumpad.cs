using System;
using UnityEngine;
using UnityEngine.UI;

// Numpad (prefab): 0-9, backspace, enter. Mengisi satu kotak dari satu NumberLineSoalInput; tidak tahu panel atau session.
public class NumberLineNumpad : MonoBehaviour
{
    [SerializeField] GameObject root;              // kosong = objek ini sendiri
    [SerializeField] Button[] digitButtons;        // indeks = angka (0..9)
    [SerializeField] Button backspaceButton;
    [SerializeField] Button enterButton;

    NumberLineSoalInput target;
    NumberLineCell cell;
    bool wired;

    public event Action<int> Overflowed;           // angka melebihi batas; argumen = batas
    public event Action Entered;                   // Enter ditekan (numpad lalu ditutup)
    public event Action Closed;

    GameObject Root => root != null ? root : gameObject;
    public bool IsOpen => Root.activeSelf;

    void Awake() => Wire();

    void Wire()
    {
        if (wired) return;
        wired = true;

        for (int i = 0; i < digitButtons.Length; i++)
        {
            int digit = i;
            digitButtons[i].onClick.AddListener(() => OnDigit(digit));
        }
        if (backspaceButton) backspaceButton.onClick.AddListener(OnBackspace);
        if (enterButton) enterButton.onClick.AddListener(OnEnter);
    }

    // Buka (atau pindahkan fokus) ke satu kotak
    public void Open(NumberLineSoalInput input, NumberLineCell targetCell)
    {
        Wire();
        target = input;
        cell = targetCell;
        if (!Root.activeSelf) Root.SetActive(true);
    }

    public void Close()
    {
        if (!Root.activeSelf) return;
        target = null;
        Root.SetActive(false);
        Closed?.Invoke();
    }

    void OnDigit(int digit)
    {
        if (target == null) return;
        if (target.TypeDigit(cell, digit) == NumberLineEditResult.Overflow) Overflowed?.Invoke(target.MaxCell);
    }

    void OnBackspace() => target?.Backspace(cell);

    void OnEnter()
    {
        Entered?.Invoke();
        Close();
    }
}