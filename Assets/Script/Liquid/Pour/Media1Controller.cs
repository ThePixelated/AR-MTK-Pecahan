using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Controller halaman Media 1: partisi realtime, tarik air dan sirup ke gelas, Gabungkan, prediksi gelas akhir (CEK), Ulangi.
// Satu instance per halaman.
public class Media1Controller : MonoBehaviour
{
    [SerializeField] Media1Limits limits = new Media1Limits();
    [SerializeField] Media1VisualBinder binder;
    [SerializeField] PourController pourController;
    [SerializeField] LiquidDragController dragController;
    [SerializeField] SimpleConfirmDialog confirmDialog;

    [Header("Gelas")]
    [SerializeField] VesselActor actorA;                  // gelas air
    [SerializeField] VesselActor actorB;                  // gelas sirup
    [SerializeField] VesselActor actorW;                  // wadah gabungan

    [Header("Panel")]
    [SerializeField] UIStepper denominatorStepper;        // Partial per Satuan: berlaku langsung
    [SerializeField] UIStepper predictStepper;

    [Header("Gabungkan")]
    [SerializeField] bool sequentialMerge = true;         // true: gelas A lalu B; false: bersamaan
    [SerializeField] float mergeSecondsPerSection = 0.5f; // lama menuang per bagian yang ada di gelas
    [SerializeField] float mergeMinSeconds = 1.2f;

    [Header("Informasi larutan di wadah (opsional)")]
    [SerializeField] TMP_Text infoSyrup, infoWater, infoTotal;

    [Header("Informasi Hasil (opsional)")]
    [SerializeField] TMP_Text resultNumerator;     // Liquid1ValTxt
    [SerializeField] TMP_Text resultDenominator;   // Liquid2ValTxt
    [SerializeField] TMP_Text totalValueText;      // TotalLiquidValTxt
    [SerializeField] TMP_Text compareSymbolText;   // TextSimbol
    [SerializeField] TMP_Text compareLimitText;    // TextBatasNilai

    [Header("Objek dunia milik aktivitas ini")]
    [SerializeField] GameObject[] worldRoots;             // induk semua objek dunia Media 1 (awalnya NONAKTIF di scene)

    Media1Session session;
    public Media1Session Session => session ??= new Media1Session(limits);

    bool suppress;

    void OnEnable()
    {
        if (binder == null || denominatorStepper == null || predictStepper == null)
        {
            Debug.LogError($"[{name}] Media1Controller: Binder, stepper partisi, dan stepper prediksi harus diisi.", this);
            return;
        }

        SetWorldRoots(true);
        binder.Bind(Session);
        Session.Changed += RefreshInfo;

        denominatorStepper.SetRange(limits.MinDenominator, limits.MaxDenominator);
        predictStepper.SetRange(1, limits.MaxPredictCups);

        if (!Session.IsConfigured) Session.SetPartition(denominatorStepper.Value);     // pertama kali: gelas kosong langsung berpartisi
        else ShowPartition();                                                          // kembali ke halaman ini: stepper mengikuti keadaan terakhir
        denominatorStepper.Changed += OnPartitionChanged;

        if (dragController != null)
        {
            dragController.IsValidTarget = IsValidTarget;
            dragController.BottleColor = binder.PureColor;
            dragController.Dropped += OnLiquidDropped;
        }
        RefreshInfo();
    }

    void OnDisable()
    {
        if (denominatorStepper) denominatorStepper.Changed -= OnPartitionChanged;
        if (dragController != null) dragController.Dropped -= OnLiquidDropped;
        if (session != null) session.Changed -= RefreshInfo;
        if (gameObject.scene.isLoaded) SetWorldRoots(false);
    }

    void SetWorldRoots(bool on)
    {
        if (worldRoots == null) return;
        foreach (GameObject r in worldRoots)
            if (r && r.activeSelf != on) r.SetActive(on);
    }

    // ---------------- partisi (realtime) ----------------
    void ShowPartition()
    {
        suppress = true;
        denominatorStepper.Set(Session.Denominator);
        suppress = false;
    }

    void OnPartitionChanged(int p)
    {
        if (suppress || p == Session.Denominator) return;

        // sudah ada isi: tanya dulu; TIDAK mengembalikan stepper ke partisi yang berlaku
        if (Session.HasContent && confirmDialog != null)
        {
            confirmDialog.Ask(Media1Texts.PartitionResetWarning, () => ApplyPartition(p), ShowPartition);
            return;
        }
        ApplyPartition(p);
    }

    void ApplyPartition(int p)
    {
        ConfigError err = Session.SetPartition(p);
        if (err != ConfigError.None) Feedback.Show(Media1Messages.For(err, limits), FeedbackKind.Warning);
        ShowPartition();
    }

    // ---------------- tarik air dan sirup ----------------
    VesselActor Expected(LiquidKind kind) => kind == LiquidKind.Water ? actorA : actorB;

    bool IsValidTarget(LiquidKind kind, VesselActor target) => target != null && target == Expected(kind);

    void OnLiquidDropped(LiquidDrop drop)
    {
        if (drop.target == null)
        {
            dragController.DismissBottle(drop.bottle);
            Feedback.Show(Media1Texts.DropOnCup, FeedbackKind.Info);
            return;
        }
        if (drop.target != Expected(drop.kind))
        {
            dragController.DismissBottle(drop.bottle);
            Feedback.Show(drop.kind == LiquidKind.Water ? Media1Texts.WaterOnlyA : Media1Texts.SyrupOnlyB, FeedbackKind.Warning);
            return;
        }
        if (pourController != null && pourController.IsBusy)
        {
            dragController.DismissBottle(drop.bottle);
            Feedback.Show(Media1Texts.PourBusy, FeedbackKind.Info);
            return;
        }

        PourError err = Session.CheckAdd(drop.kind);
        if (err != PourError.None)
        {
            dragController.DismissBottle(drop.bottle);
            Feedback.Show(err == PourError.TargetFull ? Media1Texts.CupFull(drop.kind, Session.Denominator) : Media1Messages.For(err), FeedbackKind.Warning);
            return;
        }

        // wadah atau gelas akhir sudah terisi: konfirmasi, lalu semuanya direset dan tarikan tetap dijalankan
        if (Session.NeedsConfirmBeforeAdd && confirmDialog != null)
        {
            confirmDialog.Ask(Media1Texts.AddResetWarning,
                () => { Session.ResetContent(); StartAdd(drop); },
                () => dragController.DismissBottle(drop.bottle));
            return;
        }
        StartAdd(drop);
    }

    // Botol berpindah ke gelas, menuang (data +1 bagian saat aliran mulai), lalu hilang
    void StartAdd(LiquidDrop drop)
    {
        if (pourController == null || drop.bottle == null || drop.bottle.Vessel == null)
        {
            dragController.DismissBottle(drop.bottle);
            Session.TryAdd(drop.kind);       // tanpa animasi kalau belum dipasang
            return;
        }

        LiquidKind kind = drop.kind;
        binder.SetBottle(drop.bottle.Vessel, binder.PureColor(kind));

        var job = new PourJob
        {
            source = drop.bottle,
            target = Expected(kind),
            commit = () => Session.TryAdd(kind),
            disposeSource = true,
            onDone = binder.ClearBottle
        };

        if (!pourController.StartJobs(new[] { job }, false))
        {
            binder.ClearBottle();
            dragController.DismissBottle(drop.bottle);
            Feedback.Show(Media1Texts.PourBusy, FeedbackKind.Info);
        }
    }

    // ---------------- tombol ----------------
    // onClick tombol Gabungkan: seluruh isi gelas A lalu gelas B pindah ke wadah; lama animasi sebanding volume
    public void OnMergePressed()
    {
        if (pourController != null && pourController.IsBusy)
        {
            Feedback.Show(Media1Texts.PourBusy, FeedbackKind.Info);
            return;
        }

        var jobs = new List<PourJob>();
        AddMergeJob(jobs, Session.A, actorA);
        AddMergeJob(jobs, Session.B, actorB);

        if (jobs.Count == 0)
        {
            if (Session.A.IsEmpty && Session.B.IsEmpty) Feedback.Show(Media1Texts.NothingToMerge, FeedbackKind.Info);
            return;
        }

        if (pourController == null || actorW == null)
        {
            Session.TryPourAll(Session.A);      // tanpa animasi kalau gelas belum dipasang
            Session.TryPourAll(Session.B);
            return;
        }
        pourController.StartJobs(jobs, sequentialMerge);
    }

    void AddMergeJob(List<PourJob> jobs, LiquidContainer cup, VesselActor actor)
    {
        if (cup.IsEmpty || actor == null) return;

        PourError err = Session.CheckPourAll(cup);
        if (err != PourError.None)
        {
            Feedback.Show(Media1Messages.For(err), FeedbackKind.Warning);
            return;
        }

        float seconds = Mathf.Max(mergeMinSeconds, cup.VolumeSections * mergeSecondsPerSection);
        jobs.Add(new PourJob { source = actor, target = actorW, seconds = seconds, commit = () => Session.TryPourAll(cup) });
    }

    // onClick tombol Ulangi: kosongkan semuanya, partisi tetap
    public void OnRepeatPressed()
    {
        if (!Session.HasContent)
        {
            Feedback.Show(Media1Texts.NothingToReset, FeedbackKind.Info);
            return;
        }

        if (confirmDialog != null) confirmDialog.Ask(Media1Messages.RepeatWarning, Session.ResetContent);
        else Session.ResetContent();
    }

    // onClick tombol CEK
    public void OnCheckPressed()
    {
        if (Session.TotalLiquid == 0)
        {
            Feedback.Show(Media1Texts.AddFirst, FeedbackKind.Info);
            return;
        }

        PredictResult r = Session.TryPredict(predictStepper.Value);
        if (r.error != PredictError.None) { Feedback.Show(Media1Messages.For(r.error), FeedbackKind.Warning); return; }

        if (r.correct) Feedback.Show(Media1Messages.PredictCorrect, FeedbackKind.Success);
        else Feedback.Show(Media1Messages.PredictWrong, FeedbackKind.Warning);
    }

    // ---------------- informasi ----------------
    void RefreshInfo()
    {
        if (session == null) return;
        Mixture m = session.W.Content;
        if (infoSyrup) infoSyrup.text = Sections(m.syrup);
        if (infoWater) infoWater.text = Sections(m.water);
        if (infoTotal) infoTotal.text = Sections(m.Total);

        RefreshResult();
    }

    // Isi wadah dalam satuan gelas (total bagian / penyebut), dibandingkan dengan 1 gelas penuh
    void RefreshResult()
    {
        if (session == null) return;

        if (!session.IsConfigured)
        {
            SetResult("0", "-", "0", "<");
            return;
        }

        int p = session.Denominator;
        Fraction t = session.W.Content.Total.Reduce();   // dalam bagian
        long num = t.numerator, den = t.denominator * p;

        long whole = num / den, rem = num % den;
        string mixed = rem == 0 ? whole.ToString()
                     : whole == 0 ? $"{rem}/{den}"
                     : $"{whole} {rem}/{den}";
        string symbol = num > den ? ">" : num < den ? "<" : "=";

        SetResult(num.ToString(), den.ToString(), mixed, symbol);
    }

    void SetResult(string numerator, string denominator, string total, string symbol)
    {
        if (resultNumerator) resultNumerator.text = numerator;
        if (resultDenominator) resultDenominator.text = denominator;
        if (totalValueText) totalValueText.text = total;
        if (compareSymbolText) compareSymbolText.text = symbol;
        if (compareLimitText) compareLimitText.text = "1";
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