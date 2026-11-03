using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Controller halaman Media 3: swipe gelas air dan sirup, Gabungkan, Input, Reset All, popup, daftar percobaan, mode perbandingan.
// Satu instance per halaman.
public class Media3Controller : MonoBehaviour
{
    [SerializeField] Media3Limits limits = new Media3Limits();
    [SerializeField] Media3VisualBinder binder;
    [SerializeField] PourController pourController;          // untuk menolak aksi saat tuangan berjalan dan menjalankan Gabungkan
    [SerializeField] SimpleConfirmDialog confirmDialog;      // popup konfirmasi Reset All

    [Header("Gabungkan")]
    [SerializeField] VesselActor actorWater;                 // gelas air
    [SerializeField] VesselActor actorSyrup;                 // gelas sirup
    [SerializeField] VesselActor actorWadah;                 // wadah gabungan
    [SerializeField] bool sequentialMerge = false;           // false: kedua gelas menuang bersamaan; true: bergiliran (air dulu)

    [Header("Informasi")]
    [SerializeField] Media3InfoView infoView;                // panel dunia: air, sirup, total
    [SerializeField] TMP_Text infoWater;                     // opsional: teks UI biasa
    [SerializeField] TMP_Text infoSyrup;
    [SerializeField] TMP_Text infoTotal;
    [SerializeField] TMP_Text trialCounter;                  // "2/4"
    [SerializeField] TrialListView trialList;                // daftar percobaan (opsional)

    [Header("Mode perbandingan")]
    [SerializeField] TrialGridView trialGrid;
    [SerializeField] GameObject[] entryModeObjects;          // hanya tampil di mode entri (gelas dan wadah, panel informasi, tombol Input dan Gabungkan)
    [SerializeField] GameObject[] compareModeObjects;        // hanya tampil di mode perbandingan (grid)
    [SerializeField] TMP_Text compareButtonLabel;            // opsional: teks tombol berganti sesuai mode
    [SerializeField] string labelToCompare = "PERBANDINGAN";
    [SerializeField] string labelToEntry = "MODE ENTRI";

    [Header("Objek dunia milik aktivitas ini")]
    [SerializeField] GameObject[] worldRoots;                // induk semua objek dunia Media 3 (awalnya NONAKTIF di scene)

    Media3Session session;
    public Media3Session Session => session ??= new Media3Session(limits);

    bool compareMode;

    void OnEnable()
    {
        if (binder == null)
        {
            Debug.LogError($"[{name}] Media3Controller: Binder harus diisi.", this);
            return;
        }

        SetWorldRoots(true);
        binder.Bind(Session);
        Session.Changed += RefreshInfo;
        if (trialList) trialList.Bind(Session);
        if (trialGrid) trialGrid.Bind(Session);

        RefreshInfo();
        compareMode = false;       // selalu mulai di mode entri saat halaman dibuka
        ApplyMode();
    }

    void OnDisable()
    {
        if (session != null) session.Changed -= RefreshInfo;
        if (trialList) trialList.Unbind();
        if (trialGrid) trialGrid.Unbind();
        if (gameObject.scene.isLoaded) SetWorldRoots(false);
    }

    void SetWorldRoots(bool on)
    {
        if (worldRoots == null) return;
        foreach (GameObject r in worldRoots)
            if (r && r.activeSelf != on) r.SetActive(on);
    }

    // ---------------- tombol ----------------
    // onClick tombol Gabungkan: air +1 dan sirup +1 lewat animasi (bersamaan, atau bergiliran kalau Sequential Merge)
    public void OnMergePressed()
    {
        if (compareMode)
        {
            Feedback.Show(Media3Messages.InputInCompareMode, FeedbackKind.Info);
            return;
        }
        if (pourController != null && pourController.IsBusy)
        {
            Feedback.Show(Media3Messages.PourBusy, FeedbackKind.Info);
            return;
        }
        if (Session.CheckMerge() != PourError.None)
        {
            Feedback.Show(Media3Messages.MergeBlocked(limits), FeedbackKind.Warning);
            return;
        }

        if (pourController == null || actorWater == null || actorSyrup == null || actorWadah == null)
        {
            Session.TryMerge();      // tanpa animasi kalau gelas belum dipasang
            return;
        }

        var jobs = new List<PourJob>
        {
            new PourJob { source = actorWater, target = actorWadah, commit = () => Session.TryPour(Session.Water, Session.W) },
            new PourJob { source = actorSyrup, target = actorWadah, commit = () => Session.TryPour(Session.Syrup, Session.W) }
        };
        pourController.StartJobs(jobs, sequentialMerge);
    }

    // onClick tombol Input
    public void OnInputPressed()
    {
        if (compareMode)
        {
            Feedback.Show(Media3Messages.InputInCompareMode, FeedbackKind.Info);
            return;
        }
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
        if (Session.Entries.Count == 0 && Session.IsEmpty)
        {
            Feedback.Show(Media3Messages.NothingToReset, FeedbackKind.Info);
            return;
        }

        if (confirmDialog != null) confirmDialog.Ask(Media3Messages.ResetAllWarning, ResetAll);
        else ResetAll();
    }

    void ResetAll()
    {
        Session.ResetAll();
        Feedback.Show(Media3Messages.AllReset, FeedbackKind.Success);
    }

    // onClick tombol Perbandingan (toggle)
    public void OnComparePressed()
    {
        if (pourController != null && pourController.IsBusy)
        {
            Feedback.Show(Media3Messages.PourBusy, FeedbackKind.Info);
            return;
        }

        compareMode = !compareMode;
        ApplyMode();

        string msg = !compareMode ? Media3Messages.EnterEntry
                   : Session.Entries.Count == 0 ? Media3Messages.EnterCompareEmpty
                   : Media3Messages.EnterCompare;
        Feedback.Show(msg, FeedbackKind.Info);
    }

    void ApplyMode()
    {
        SetAll(entryModeObjects, !compareMode);
        SetAll(compareModeObjects, compareMode);
        if (compareButtonLabel) compareButtonLabel.text = compareMode ? labelToEntry : labelToCompare;
    }

    static void SetAll(GameObject[] objects, bool on)
    {
        if (objects == null) return;
        foreach (GameObject o in objects)
            if (o && o.activeSelf != on) o.SetActive(on);
    }

    // ---------------- informasi ----------------
    void RefreshInfo()
    {
        if (session == null) return;

        int water = session.WaterCount, syrup = session.SyrupCount;
        if (infoView) infoView.Show(water, syrup);
        if (infoWater) infoWater.text = water.ToString();
        if (infoSyrup) infoSyrup.text = syrup.ToString();
        if (infoTotal) infoTotal.text = (water + syrup).ToString();
        if (trialCounter) trialCounter.text = $"{session.Entries.Count}/{limits.MaxTrials}";
    }
}