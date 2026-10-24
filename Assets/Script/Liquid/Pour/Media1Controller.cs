using TMPro;
using UnityEngine;

// Controller halaman Media 1: konfigurasi (ENTER), popup, prediksi gelas akhir (CEK), ulangi. Satu instance per halaman.
public class Media1Controller : MonoBehaviour
{
    [SerializeField] Media1Limits limits = new Media1Limits();
    [SerializeField] Media1VisualBinder binder;
    [SerializeField] SimpleConfirmDialog confirmDialog;

    [Header("Panel konfigurasi")]
    [SerializeField] UIStepper denominatorStepper;        // Partial per Satuan
    [SerializeField] UIStepper waterStepper;              // pembilang gelas A (air)
    [SerializeField] UIStepper syrupStepper;              // pembilang gelas B (sirup)

    [Header("Prediksi")]
    [SerializeField] UIStepper predictStepper;

    [Header("Informasi larutan di wadah (opsional)")]
    [SerializeField] TMP_Text infoSyrup, infoWater, infoTotal;

    [Header("Objek dunia milik aktivitas ini")]
    [SerializeField] GameObject[] worldRoots;             // induk semua objek dunia Media 1 (awalnya NONAKTIF di scene)

    Media1Session session;
    public Media1Session Session => session ??= new Media1Session(limits);

    void OnEnable()
    {
        if (binder == null || denominatorStepper == null || waterStepper == null || syrupStepper == null || predictStepper == null)
        {
            Debug.LogError($"[{name}] Media1Controller: Binder dan keempat stepper harus diisi.", this);
            return;
        }

        SetWorldRoots(true);
        binder.Bind(Session);
        Session.Changed += RefreshInfo;

        denominatorStepper.SetRange(limits.MinDenominator, limits.MaxDenominator);
        predictStepper.SetRange(1, limits.MaxPredictCups);
        denominatorStepper.Changed += OnDenominatorChanged;
        OnDenominatorChanged(denominatorStepper.Value);
        RefreshInfo();
    }

    void OnDisable()
    {
        if (denominatorStepper) denominatorStepper.Changed -= OnDenominatorChanged;
        if (session != null) session.Changed -= RefreshInfo;
        if (gameObject.scene.isLoaded) SetWorldRoots(false);
    }

    void SetWorldRoots(bool on)
    {
        if (worldRoots == null) return;
        foreach (GameObject r in worldRoots)
            if (r && r.activeSelf != on) r.SetActive(on);
    }

    // pembilang tiap gelas: 1 sampai penyebut
    void OnDenominatorChanged(int p)
    {
        waterStepper.SetRange(limits.MinNumerator, p);
        syrupStepper.SetRange(limits.MinNumerator, p);
    }

    void ConfirmOrRun(bool needsConfirm, string message, System.Action action)
    {
        if (needsConfirm && confirmDialog != null) confirmDialog.Ask(message, action);
        else action();
    }

    void Apply(int p, int water, int syrup)
    {
        ConfigError err = Session.ApplyConfig(p, water, syrup);
        if (err != ConfigError.None) Feedback.Show(Media1Messages.For(err, limits), FeedbackKind.Warning);
    }

    // onClick tombol ENTER
    public void OnEnterPressed()
    {
        int p = denominatorStepper.Value, w = waterStepper.Value, s = syrupStepper.Value;
        ConfigError err = Session.Validate(p, w, s);
        if (err != ConfigError.None) { Feedback.Show(Media1Messages.For(err, limits), FeedbackKind.Warning); return; }

        ConfirmOrRun(Session.NeedsConfirmBeforeReconfigure, Media1Messages.DataExists, () => Apply(p, w, s));
    }

    // onClick tombol Ulangi: pasang ulang konfigurasi yang sama (menghapus kemajuan)
    public void OnRepeatPressed()
    {
        if (!Session.IsConfigured) { Feedback.Show(Media1Messages.For(PourError.NotConfigured), FeedbackKind.Warning); return; }

        Media1Config c = Session.Config.Value;
        ConfirmOrRun(true, Media1Messages.RepeatWarning, () => Apply(c.denominator, c.waterSections, c.syrupSections));
    }

    // onClick tombol CEK
    public void OnCheckPressed()
    {
        PredictResult r = Session.TryPredict(predictStepper.Value);
        if (r.error != PredictError.None) { Feedback.Show(Media1Messages.For(r.error), FeedbackKind.Warning); return; }

        if (r.correct) Feedback.Show(Media1Messages.PredictCorrect, FeedbackKind.Success);
        else Feedback.Show(Media1Messages.PredictWrong, FeedbackKind.Warning);
    }

    void RefreshInfo()
    {
        if (session == null) return;
        Mixture m = session.W.Content;
        if (infoSyrup) infoSyrup.text = Sections(m.syrup);
        if (infoWater) infoWater.text = Sections(m.water);
        if (infoTotal) infoTotal.text = Sections(m.Total);
    }

    // 3 -> "3", 3/2 -> "1 1/2"
    static string Sections(Fraction f)
    {
        Fraction r = f.Reduce();
        long whole = r.numerator / r.denominator, rem = r.numerator % r.denominator;
        if (rem == 0) return whole.ToString();
        return whole == 0 ? $"{rem}/{r.denominator}" : $"{whole} {rem}/{r.denominator}";
    }
}