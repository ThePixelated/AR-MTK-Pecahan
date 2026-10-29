using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Numpad (prefab): 0-9, backspace, enter. Mengisi satu kotak dari satu NumberLineSoalInput; tidak tahu panel atau session.
// Tidak pernah memanggil SetActive: buka/tutup secara visual lewat UnityEvent (mis. PanelAnimator).
public class NumberLineNumpad : MonoBehaviour
{
    [SerializeField] Button[] digitButtons;        // indeks = angka (0..9)
    [SerializeField] Button backspaceButton;
    [SerializeField] Button enterButton;           // mengunci nilai lalu menutup; jangan disambung manual ke animasi
    [SerializeField] Button[] closeButtons;        // Begone, BottomPadBtn: menutup tanpa Enter (hanya status, tanpa SetActive)

    [Header("Transisi: sambungkan ke PanelAnimator")]
    [SerializeField] UnityEvent onOpened;          // numpad dibuka: jalankan animasi buka
    [SerializeField] UnityEvent onClosed;          // numpad ditutup (Enter, Begone, BottomPad, CEK, Reset): jalankan animasi tutup
    [SerializeField] UnityEvent onClosedImmediate; // ditutup karena halaman ditinggalkan: langsung ke pose tertutup tanpa transisi

    [Header("Perilaku")]
    [SerializeField] bool visibleWhenPageGone;     // true = tetap terbuka saat halaman ditinggalkan lalu dikunjungi lagi

    NumberLineSoalInput target;
    NumberLineCell cell;
    bool wired;

    public event Action<int> Overflowed;           // angka melebihi batas; argumen = batas
    public event Action Entered;                   // Enter ditekan (numpad lalu ditutup)
    public event Action Closed;                    // setiap penutupan, apa pun sebabnya

    public bool IsOpen { get; private set; }
    public NumberLineCell FocusedCell => cell;

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

        if (closeButtons != null)
            foreach (Button b in closeButtons)
                if (b) b.onClick.AddListener(Close);
    }

    // Buka, atau pindahkan fokus ke kotak lain kalau sudah terbuka (onOpened hanya dikirim saat baru dibuka)
    public void Open(NumberLineSoalInput input, NumberLineCell targetCell)
    {
        Wire();
        target = input;
        cell = targetCell;

        if (IsOpen) return;
        IsOpen = true;
        onOpened?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        target = null;
        onClosed?.Invoke();
        Closed?.Invoke();
    }

    // Dipanggil controller saat halaman ditinggalkan: menutup langsung tanpa transisi, kecuali visibleWhenPageGone
    public void PageGone()
    {
        if (!IsOpen || visibleWhenPageGone) return;
        IsOpen = false;
        target = null;
        onClosedImmediate?.Invoke();
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
        if (!IsOpen) return;
        Entered?.Invoke();
        Close();
    }
}