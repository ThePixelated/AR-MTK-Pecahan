using TMPro;
using UnityEngine;

// Controller halaman Media 3: stepper Atur Campuran (berlaku langsung), tombol Input, Reset All, dan popup. Satu instance per halaman.
public class Media3Controller : MonoBehaviour
{
    [SerializeField] Media3Limits limits = new Media3Limits();
    [SerializeField] Media3VisualBinder binder;
    [SerializeField] PourController pourController;          // opsional: dipakai untuk menolak Input saat tuangan masih berjalan
    [SerializeField] SimpleConfirmDialog confirmDialog;      // popup konfirmasi ubah campuran
    [SerializeField] SimpleConfirmDialog resetAllDialog;     // kosong = pakai confirmDialog

    [Header("Panel Atur Campuran")]
    [SerializeField] UIStepper syrupStepper;
    [SerializeField] UIStepper waterStepper;

    [Header("Informasi (opsional)")]
    [SerializeField] TMP_Text infoSyrup;                     // sirup yang sudah di wadah
    [SerializeField] TMP_Text infoWater;                     // air yang sudah di wadah
    [SerializeField] TMP_Text trialCounter;                  // "2/4"

    [Header("Objek dunia milik aktivitas ini")]
    [SerializeField] GameObject[] worldRoots;                // induk semua objek dunia Media 3 (awalnya NONAKTIF di scene)

    Media3Session session;
    public Media3Session Session => session ??= new Media3Session(limits);

    bool suppress;      // true saat stepper diubah dari kode, supaya tidak dianggap input pengguna

    void OnEnable()
    {
        if (binder == null || syrupStepper == null || waterStepper == null)
        {
            Debug.LogError($"[{name}] Media3Controller: Binder dan kedua stepper harus diisi.", this);
            return;
        }

        SetWorldRoots(true);
        binder.Bind(Session);
        Session.Changed += RefreshInfo;

        syrupStepper.SetRange(limits.MinPerCup, limits.MaxPerCup);
        waterStepper.SetRange(limits.MinPerCup, limits.MaxPerCup);
        syrupStepper.Changed += OnStepperChanged;
        waterStepper.Changed += OnStepperChanged;

        if (Session.IsConfigured) ShowConfigOnSteppers();      // kembali ke halaman ini: stepper mengikuti keadaan terakhir
        else Apply(syrupStepper.Value, waterStepper.Value);    // pertama kali: gelas langsung terisi sesuai stepper
        RefreshInfo();
    }

    void OnDisable()
    {
        if (syrupStepper) syrupStepper.Changed -= OnStepperChanged;
        if (waterStepper) waterStepper.Changed -= OnStepperChanged;
        if (session != null) session.Changed -= RefreshInfo;
        if (gameObject.scene.isLoaded) SetWorldRoots(false);
    }

    void SetWorldRoots(bool on)
    {
        if (worldRoots == null) return;
        foreach (GameObject r in worldRoots)
            if (r && r.activeSelf != on) r.SetActive(on);
    }

    // ---------------- Atur Campuran ----------------
    void OnStepperChanged(int _)
    {
        if (suppress) return;
        int s = syrupStepper.Value, w = waterStepper.Value;

        // sudah ada yang dituang: tanya dulu; TIDAK mengembalikan stepper ke konfigurasi yang berlaku
        if (Session.NeedsConfirmBeforeReconfigure && confirmDialog != null)
        {
            confirmDialog.Ask(Media3Messages.ReconfigureWarning, () => Apply(s, w), ShowConfigOnSteppers);
            return;
        }
        Apply(s, w);
    }

    void Apply(int syrup, int water)
    {
        Media3ConfigError err = Session.ApplyConfig(syrup, water);
        if (err != Media3ConfigError.None) Feedback.Show(Media3Messages.For(err, limits), FeedbackKind.Warning);
    }

    void ShowConfigOnSteppers()
    {
        if (!Session.IsConfigured) return;
        Media3Config c = Session.Config.Value;
        suppress = true;
        syrupStepper.Set(c.syrupSections);
        waterStepper.Set(c.waterSections);
        suppress = false;
    }

    // ---------------- tombol ----------------
    // onClick tombol Input
    public void OnInputPressed()
    {
        if (pourController != null && pourController.IsBusy)
        {
            Feedback.Show(Media3Messages.PourBusy, FeedbackKind.Info);
            return;
        }

        Media3InputResult r = Session.TryInput();
        switch (r)
        {
            case Media3InputResult.Added:
                int count = Session.Entries.Count;
                Feedback.Show(Media3Messages.EntryAdded(count, Session.Entries[count - 1], Session.TrialsFull), FeedbackKind.Success);
                break;

            case Media3InputResult.DuplicateEntry:
                Feedback.Show(Media3Messages.Duplicate(Session.LastDuplicateIndex + 1), FeedbackKind.Warning);
                break;

            default:
                string msg = Media3Messages.For(r, limits);
                if (!string.IsNullOrEmpty(msg)) Feedback.Show(msg, FeedbackKind.Warning);
                break;
        }
    }

    // onClick tombol Reset All
    public void OnResetAllPressed()
    {
        if (Session.Entries.Count == 0 && !Session.NeedsConfirmBeforeReconfigure)
        {
            Feedback.Show(Media3Messages.NothingToReset, FeedbackKind.Info);
            return;
        }

        SimpleConfirmDialog dialog = resetAllDialog ? resetAllDialog : confirmDialog;
        if (dialog != null) dialog.Ask(Media3Messages.ResetAllWarning, ResetAll);
        else ResetAll();
    }

    void ResetAll()
    {
        Session.ResetAll();      // entri dihapus, gelas diisi ulang sesuai konfigurasi aktif, wadah kosong
        Feedback.Show(Media3Messages.AllReset, FeedbackKind.Success);
    }

    // ---------------- informasi ----------------
    void RefreshInfo()
    {
        if (session == null) return;
        Mixture m = session.W.Content;
        if (infoSyrup) infoSyrup.text = Whole(m.syrup).ToString();
        if (infoWater) infoWater.text = Whole(m.water).ToString();
        if (trialCounter) trialCounter.text = $"{session.Entries.Count}/{limits.MaxTrials}";
    }

    static long Whole(Fraction f) => f.denominator == 0 ? 0 : f.numerator / f.denominator;
}