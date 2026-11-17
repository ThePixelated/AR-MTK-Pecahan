using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Controller halaman Media 1: partisi realtime, tarik air dan sirup ke gelas, Gabungkan, Tambah Gelas, Cek hasil (jawaban siswa), Ulangi.
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
    [SerializeField] UIStepper predictStepper;            // tebakan jumlah gelas hasil (- +)
    [SerializeField] Button addCupButton;                 // opsional: tombol Tambah Gelas nonaktif setelah gelas hasil muncul

    [Header("Ikon air dan sirup (HUD)")]
    [SerializeField] Button[] sourceButtons;              // dimatikan (interactable) selama ada tuangan berjalan, supaya tidak bisa di-drag lagi

    [Header("Panel hasil: jawaban siswa (Panel Soal prefab dengan Read Only dimatikan)")]
    [SerializeField] NumberLineSoalPanel resultPanel;
    [SerializeField] NumberLineNumpad numpad;

    [Header("Gabungkan")]
    [SerializeField] bool sequentialMerge = true;         // true: gelas A lalu B; false: bersamaan
    [SerializeField] float mergeSecondsPerSection = 0.5f; // lama menuang per bagian yang ada di gelas
    [SerializeField] float mergeMinSeconds = 1.2f;

    [Header("Informasi larutan: berapa kali dituang ke wadah, mis. 3/4")]
    [SerializeField] TMP_Text infoSyrup, infoWater;
    [SerializeField] bool infoMixedNumbers = false;       // false: selalu n/p (5/4 tetap 5/4); true: pecahan tak sejati ditulis campuran (1 1/4)

    [Header("Objek dunia milik aktivitas ini")]
    [SerializeField] GameObject[] worldRoots;             // induk semua objek dunia Media 1 (awalnya NONAKTIF di scene)

    Media1Session session;
    public Media1Session Session => session ??= new Media1Session(limits);

    readonly NumberLineSoalInput answerInput = new NumberLineSoalInput();     // jawaban siswa di panel hasil
    bool suppress;
    bool sourcesLocked;

    void OnEnable()
    {
        if (binder == null || denominatorStepper == null || predictStepper == null)
        {
            Debug.LogError($"[{name}] Media1Controller: Binder, stepper partisi, dan stepper tebakan gelas harus diisi.", this);
            return;
        }
        if (resultPanel == null || numpad == null)
            Debug.LogError($"[{name}] Media1Controller: 'Result Panel' dan 'Numpad' harus diisi supaya siswa bisa menjawab.", this);

        SetWorldRoots(true);
        binder.Bind(Session);
        Session.Changed += RefreshInfo;
        Session.Changed += OnSessionChanged;

        denominatorStepper.SetRange(limits.MinDenominator, limits.MaxDenominator);
        predictStepper.SetRange(1, limits.MaxPredictCups);

        if (!Session.IsConfigured) Session.SetPartition(denominatorStepper.Value);     // pertama kali: gelas kosong langsung berpartisi
        else ShowPartition();                                                          // kembali ke halaman ini: stepper mengikuti keadaan terakhir
        denominatorStepper.Changed += OnPartitionChanged;

        if (dragController != null)
        {
            dragController.IsValidTarget = IsValidTarget;
            dragController.BottleColor = binder.PureColor;
            dragController.CanBegin = CanStartDrag;          // tidak bisa menarik ikon baru selama ada tuangan berjalan
            dragController.Dropped += OnLiquidDropped;
        }
        sourcesLocked = false;
        SetSourcesInteractable(true);

        answerInput.Changed += OnAnswerEdited;
        if (resultPanel != null)
        {
            resultPanel.Bind(answerInput);
            resultPanel.CellTapped += OnAnswerCellTapped;
        }
        if (numpad != null)
        {
            numpad.Overflowed += OnOverflow;
            numpad.Entered += OnNumpadEntered;
            numpad.Closed += OnNumpadClosed;
        }

        RefreshInfo();
    }

    void OnDisable()
    {
        if (denominatorStepper) denominatorStepper.Changed -= OnPartitionChanged;
        if (dragController != null)
        {
            dragController.Dropped -= OnLiquidDropped;
            dragController.CanBegin = null;
        }
        SetSourcesInteractable(true);
        if (session != null)
        {
            session.Changed -= RefreshInfo;
            session.Changed -= OnSessionChanged;
        }

        answerInput.Changed -= OnAnswerEdited;
        if (resultPanel) resultPanel.CellTapped -= OnAnswerCellTapped;
        if (numpad)
        {
            numpad.Overflowed -= OnOverflow;
            numpad.Entered -= OnNumpadEntered;
            numpad.Closed -= OnNumpadClosed;
            numpad.PageGone();
        }

        if (gameObject.scene.isLoaded) SetWorldRoots(false);
    }

    // ---------------- ikon air dan sirup ----------------
    bool CanStartDrag() => !(pourController != null && pourController.IsBusy);

    void SetSourcesInteractable(bool on)
    {
        if (sourceButtons == null) return;
        foreach (Button b in sourceButtons)
            if (b) b.interactable = on;
    }

    // Selama ada tuangan berjalan (botol menuang, Gabungkan), ikon tidak bisa dipegang dan botol tidak ikut jari
    void Update()
    {
        bool busy = pourController != null && pourController.IsBusy;
        if (busy == sourcesLocked) return;

        sourcesLocked = busy;
        SetSourcesInteractable(!busy);
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

        // sudah ada cairan: tanya dulu; TIDAK mengembalikan stepper ke partisi yang berlaku
        if (Session.HasLiquid && confirmDialog != null)
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
        else ClearAnswer();
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

        // wadah atau gelas hasil sudah berisi cairan: konfirmasi, lalu semuanya direset dan tarikan tetap dijalankan
        if (Session.NeedsConfirmBeforeAdd && confirmDialog != null)
        {
            confirmDialog.Ask(Media1Texts.AddResetWarning,
                () => { Session.ResetContent(); ClearAnswer(); StartAdd(drop); },
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

    // onClick tombol Tambah Gelas: siswa harus menebak jumlah gelas hasil yang tepat (stepper - +).
    // Tebakan benar: semua gelas hasil muncul sekaligus; salah: tidak ada gelas yang muncul.
    public void OnAddCupPressed()
    {
        if (Session.TotalLiquid == 0)
        {
            Feedback.Show(Media1Texts.AddFirst, FeedbackKind.Info);
            return;
        }

        PredictResult r = Session.TryPredict(predictStepper.Value);
        if (r.error != PredictError.None)
        {
            Feedback.Show(Media1Messages.For(r.error), FeedbackKind.Warning);
            return;
        }

        if (r.correct) Feedback.Show(Media1Messages.PredictCorrect, FeedbackKind.Success);
        else Feedback.Show(Media1Messages.PredictWrong, FeedbackKind.Warning);
    }

    // onClick lama (CEK) yang masih tersambung tetap berfungsi: sama dengan Tambah Gelas
    public void OnCheckPressed() => OnAddCupPressed();

    // onClick tombol Ulangi: kosongkan semuanya (termasuk gelas hasil dan jawaban), partisi tetap
    public void OnRepeatPressed()
    {
        if (!Session.HasContent && answerInput.IsEmpty)
        {
            Feedback.Show(Media1Texts.NothingToReset, FeedbackKind.Info);
            return;
        }

        if (confirmDialog != null) confirmDialog.Ask(Media1Messages.RepeatWarning, ResetAndClear);
        else ResetAndClear();
    }

    void ResetAndClear()
    {
        Session.ResetContent();
        ClearAnswer();
    }

    // onClick tombol Cek hasil: cairan harus sudah dituang semuanya, lalu jawaban di panel hasil dinilai menurut nilai (1 3/4 sama dengan 7/4)
    public void OnCheckResultPressed()
    {
        if (numpad) numpad.Close();

        if (!Session.IsConfigured || Session.TotalLiquid == 0)
        {
            Feedback.Show(Media1AnswerTexts.NothingToCheck, FeedbackKind.Info);
            return;
        }
        if (!Session.AllPoured)
        {
            Feedback.Show(Media1AnswerTexts.NotAllPoured, FeedbackKind.Info);
            return;
        }

        NumberLineSoalStatus status = answerInput.Evaluate(out Fraction value);
        switch (status)
        {
            case NumberLineSoalStatus.Empty:
                Feedback.Show(Media1AnswerTexts.AnswerEmpty, FeedbackKind.Info);
                return;
            case NumberLineSoalStatus.Incomplete:
                Feedback.Show(Media1AnswerTexts.AnswerIncomplete, FeedbackKind.Info);
                return;
            case NumberLineSoalStatus.Impossible:
                answerInput.Clear();
                Feedback.Show(NumberLineMessages.SoalImpossible, FeedbackKind.Warning);
                return;
        }

        bool correct = Session.CheckAnswer(value) == Media1CheckOutcome.Correct;
        if (resultPanel) resultPanel.SetIndicator(correct ? NumberLineIndicator.Correct : NumberLineIndicator.Wrong);
        Feedback.Show(correct ? Media1AnswerTexts.AnswerCorrect : Media1AnswerTexts.AnswerWrong, correct ? FeedbackKind.Success : FeedbackKind.Warning);
    }

    // ---------------- jawaban siswa (panel hasil dan numpad) ----------------
    void OnAnswerCellTapped(NumberLineSoalPanel panel, NumberLineCell cell)
    {
        numpad.Open(answerInput, cell);
        panel.SetFocusedCell(cell);
    }

    void OnNumpadClosed()
    {
        if (resultPanel) resultPanel.SetFocusedCell(null);
    }

    void OnOverflow(int max) => Feedback.Show(NumberLineMessages.CellOverflow(max), FeedbackKind.Warning);

    // Enter: penyebut 0 dikosongkan dengan notifikasi
    void OnNumpadEntered()
    {
        if (answerInput.Evaluate(out _) != NumberLineSoalStatus.Impossible) return;

        answerInput.Clear();
        Feedback.Show(NumberLineMessages.SoalImpossible, FeedbackKind.Warning);
    }

    // Jawaban diubah atau cairan berubah: tanda benar atau salah yang lama tidak berlaku lagi
    void OnAnswerEdited() => ResetIndicator();
    void OnSessionChanged() => ResetIndicator();

    void ResetIndicator()
    {
        if (resultPanel) resultPanel.SetIndicator(NumberLineIndicator.None);
    }

    void ClearAnswer()
    {
        if (numpad) numpad.Close();
        answerInput.Clear();
        ResetIndicator();
    }

    // ---------------- informasi ----------------
    void RefreshInfo()
    {
        if (session == null) return;

        int p = session.Denominator;
        if (infoSyrup) infoSyrup.text = Media1Format.CupFraction(session.SyrupPoured, p, infoMixedNumbers);
        if (infoWater) infoWater.text = Media1Format.CupFraction(session.WaterPoured, p, infoMixedNumbers);
        if (addCupButton) addCupButton.interactable = session.FinalCups.Count == 0;       // gelas hasil sudah muncul: tebakan tidak diperlukan lagi
    }
}