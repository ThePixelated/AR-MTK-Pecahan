using UnityEngine;
using UnityEngine.UI;

// Satu pilihan pion. Pasang di tiap HewanNBtn; tanda terpilih berupa anak yang dinyalakan atau dimatikan.
[RequireComponent(typeof(Button))]
public class AnimalButton : MonoBehaviour
{
    [SerializeField] Sprite pionSprite;          // kosong = pakai sprite dari Image ikon
    [SerializeField] Image icon;                 // opsional: Image ikon tombol ini
    [SerializeField] GameObject selectedMark;    // anak penanda terpilih (nonaktif di prefab)

    public Sprite PionSprite => pionSprite != null ? pionSprite : (icon != null ? icon.sprite : null);
    public Button Button => GetComponent<Button>();

    public void SetSelected(bool selected)
    {
        if (selectedMark) selectedMark.SetActive(selected);
    }
}