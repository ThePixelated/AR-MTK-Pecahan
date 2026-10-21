using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tap balok di dpz: panel (satu canvas) muncul tepat di atas balok itu, selebar track penuh, berisi label nilai dan tombol -1 / hapus.
// Pindah antar balok: tutup di posisi lama, teleport, buka di posisi baru. Tap terakhir menang; animasi bisa diinterupsi.
public class DpzBlockActionPanel : MonoBehaviour
{
    [SerializeField] Activity4Controller controller;
    [SerializeField] DpzView dpzView;
    [SerializeField] DpzBlockDragHandler dragHandler;
    [SerializeField] RectTransform panelRect;         // RectTransform root Canvas panel (anak DpzRoot), awalnya NONAKTIF
    [SerializeField] TMP_Text valueLabel;
    [SerializeField] Button minusButton;              // -1 satuan
    [SerializeField] Button deleteButton;             // hapus blok

    [Header("Posisi dan ukuran")]
    [SerializeField] float liftAboveBlock = 0.002f;   // meter di atas tinggi sel
    [SerializeField] float extraHeight = 0f;          // tambahan tinggi panel (meter) di atas tebal baris

    [Header("Animasi (skala X)")]
    [SerializeField] float animDuration = 0.15f;      // detik, buka dan tutup
    [SerializeField] float openScaleX = 0.001f;       // skala X saat terbuka (1 satuan canvas = 1 mm)
    [SerializeField] float scaleYZ = 0.001f;

    const float MinScaleX = 0.000001f;

    FractionSession session;
    bool bound;
    int shownIndex = -1;       // balok tempat panel sedang diletakkan
    float openness;            // 0 tertutup, 1 terbuka penuh
    long lastN = -1, lastD = -1;

    void OnEnable()
    {
        if (controller == null || dpzView == null || dragHandler == null || panelRect == null
            || valueLabel == null || minusButton == null || deleteButton == null)
        {
            Debug.LogError($"[{name}] DpzBlockActionPanel: ada field Inspector yang kosong " +
                           "(Controller, Dpz View, Drag Handler, Panel Rect, Value Label, Minus Button, Delete Button)", this);
            return;
        }

        session = controller.Session;
        session.Dpz.Changed += OnDpzChanged;
        dragHandler.BlockTapped += OnBlockTapped;
        dragHandler.BackgroundTapped += OnBackgroundTapped;
        minusButton.onClick.AddListener(OnMinus);
        deleteButton.onClick.AddListener(OnDelete);
        bound = true;

        shownIndex = -1;
        openness = 0f;
        lastN = lastD = -1;
        panelRect.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        if (!bound) return;
        session.Dpz.Changed -= OnDpzChanged;
        dragHandler.BlockTapped -= OnBlockTapped;
        dragHandler.BackgroundTapped -= OnBackgroundTapped;
        minusButton.onClick.RemoveListener(OnMinus);
        deleteButton.onClick.RemoveListener(OnDelete);
        bound = false;

        panelRect.gameObject.SetActive(false);
        openness = 0f;
        shownIndex = -1;
    }

    void OnDpzChanged() => lastN = lastD = -1;   // isi blok bisa berubah (-1 satuan, merge): label dihitung ulang di Update

    void Update()
    {
        if (!bound) return;

        int want = dpzView.SelectedBlock;
        if (want >= session.Dpz.Count) want = -1;

        float target;
        if (shownIndex == want)
        {
            target = want >= 0 ? 1f : 0f;                // tidak pindah: buka (atau tetap tertutup)
        }
        else
        {
            target = 0f;                                 // pindah atau tutup: tutup dulu di posisi lama
            if (openness <= 0f)                          // sudah tertutup: teleport ke balok tujuan
            {
                shownIndex = want;
                if (want >= 0) Place(want);
                lastN = lastD = -1;
                target = want >= 0 ? 1f : 0f;
            }
        }

        if (shownIndex >= 0 && shownIndex == want) UpdateLabel();

        openness = Mathf.MoveTowards(openness, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, animDuration));
        Apply();
    }

    // Panel rebah selebar track penuh (Whole Length) dan setebal baris, di atas balok. Rotasi diatur manual di Inspector.
    void Place(int index)
    {
        Vector2 foot = dpzView.BlockFootprint;
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(foot.x * 1000f, (foot.y + extraHeight) * 1000f);   // 1 satuan canvas = 1 mm
        panelRect.localPosition = dpzView.BlockTopCenterLocal(index, liftAboveBlock);
    }

    void UpdateLabel()
    {
        if (!dpzView.TryGetBlockPiece(shownIndex, out Fraction p)) return;
        if (p.numerator == lastN && p.denominator == lastD) return;

        lastN = p.numerator;
        lastD = p.denominator;
        valueLabel.text = FractionFormat.Label(p);
    }

    void Apply()
    {
        bool visible = openness > 0f;
        if (panelRect.gameObject.activeSelf != visible) panelRect.gameObject.SetActive(visible);
        if (!visible) return;

        float k = openness * openness * (3f - 2f * openness);   // smoothstep
        panelRect.localScale = new Vector3(Mathf.Max(openScaleX * k, MinScaleX), scaleYZ, scaleYZ);

        bool interactive = shownIndex >= 0 && shownIndex == dpzView.SelectedBlock && openness > 0.7f;
        minusButton.interactable = interactive;     // tidak bisa ditekan saat menutup atau pindah
        deleteButton.interactable = interactive;
    }

    bool CanAct() =>
        shownIndex >= 0 && shownIndex < session.Dpz.Count && shownIndex == dpzView.SelectedBlock && openness > 0.7f;

    // Tap balok yang sudah terpilih: tetap terbuka (tidak toggle). Tap balok lain: pindah.
    void OnBlockTapped(int index)
    {
        if (index != dpzView.SelectedBlock) dpzView.SelectBlock(index);
    }

    void OnBackgroundTapped() => dpzView.ClearSelection();

    void OnMinus() { if (CanAct()) session.RemoveUnitFromDpz(shownIndex); }   // satuan habis = blok hilang, pilihan otomatis dicabut
    void OnDelete() { if (CanAct()) session.RemoveFromDpz(shownIndex); }
}