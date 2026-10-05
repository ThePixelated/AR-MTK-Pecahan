using UnityEngine;
using UnityEngine.InputSystem;

public class TapAwayDismiss : MonoBehaviour
{
    [SerializeField] Camera uiCamera;   // kosongkan untuk Canvas Overlay

    void Update()
    {
        Pointer p = Pointer.current;
        if (p == null || !p.press.wasPressedThisFrame) return;

        FractionBarRow row = RowSelection.Current;
        if (!row) return;

        // Tap di dalam row terpilih (termasuk tombol hapusnya) diurus row sendiri
        if (!row.ContainsScreenPoint(p.position.ReadValue(), uiCamera)) RowSelection.Clear();
    }
}