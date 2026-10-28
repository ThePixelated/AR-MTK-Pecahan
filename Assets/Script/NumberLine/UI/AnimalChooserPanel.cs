using System;
using UnityEngine;
using UnityEngine.UI;

// Panel pilih pion (prefab). Mengumpulkan semua AnimalButton di bawahnya, jadi cocok dengan scroll view.
public class AnimalChooserPanel : MonoBehaviour
{
    [SerializeField] Image pionImage;            // Image pion pada garis bilangan
    [SerializeField] GameObject lockOverlay;     // opsional: Image pemblokir raycast saat panel dikunci
    [SerializeField] int defaultIndex = 0;

    AnimalButton[] buttons = Array.Empty<AnimalButton>();
    AnimalButton selected;

    public event Action<Sprite> Selected;

    void Awake()
    {
        Rescan();
        if (lockOverlay) lockOverlay.SetActive(false);
    }

    // Panggil lagi kalau tombol ditambahkan lewat kode (mis. scroll view dengan isi dinamis)
    public void Rescan()
    {
        buttons = GetComponentsInChildren<AnimalButton>(true);
        foreach (AnimalButton b in buttons)
        {
            AnimalButton captured = b;
            captured.Button.onClick.RemoveAllListeners();
            captured.Button.onClick.AddListener(() => Select(captured));
            captured.SetSelected(false);
        }
        selected = null;
        if (buttons.Length > 0) Select(buttons[Mathf.Clamp(defaultIndex, 0, buttons.Length - 1)]);
    }

    public void Select(AnimalButton button)
    {
        if (button == null) return;
        if (selected != null) selected.SetSelected(false);
        selected = button;
        selected.SetSelected(true);

        Sprite sprite = button.PionSprite;
        if (pionImage != null && sprite != null) pionImage.sprite = sprite;
        Selected?.Invoke(sprite);
    }

    public void SetLocked(bool locked)
    {
        if (lockOverlay) lockOverlay.SetActive(locked);
    }
}