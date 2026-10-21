using System;
using TMPro;
using UnityEngine;

// Controller halaman Media 4 (Add) dan Media 6 (Subtract). Satu instance per halaman.
public class Activity4Controller : MonoBehaviour
{
    [SerializeField] Operation operation = Operation.Add;    // Media 6: Subtract
    [SerializeField] FractionPalette palette;
    [SerializeField] Transform bankContent;
    [SerializeField] FractionBankItem bankItemPrefab;
    [SerializeField] SimpleConfirmDialog confirmDialog;
    [SerializeField] TMP_Text commitLabel;                   // teks tombol +/- (AdditionBtn/Text (TMP))

    [Header("Objek dunia milik aktivitas ini")]
    [SerializeField] GameObject[] worldRoots;                // DpzRoot, area hapus, HeldGhost3D: aktif hanya selama halaman ini aktif (awalnya NONAKTIF di scene)

    FractionSession session;
    public FractionSession Session => session ??= new FractionSession(operation);   // lazy: tidak tergantung urutan Awake
    public FractionPalette Palette => palette;

    void Awake()
    {
        if (commitLabel) commitLabel.text = operation == Operation.Add ? "+" : "-";
        BuildBank();
    }

    // Objek dunia mengikuti halaman: tidak ada dua aktivitas yang berebut input
    void OnEnable() => SetWorldRoots(true);

    void OnDisable()
    {
        if (gameObject.scene.isLoaded) SetWorldRoots(false);
    }

    void SetWorldRoots(bool on)
    {
        if (worldRoots == null) return;
        foreach (GameObject r in worldRoots)
            if (r && r.activeSelf != on) r.SetActive(on);
    }

    void BuildBank()
    {
        for (int i = bankContent.childCount - 1; i >= 0; i--)    // buang kartu dummy dari scene
        {
            GameObject c = bankContent.GetChild(i).gameObject;
            c.SetActive(false);
            Destroy(c);
        }
        for (int den = 1; den <= GameRules.MaxDenominator; den++)   // 1 (utuh) sampai 1/25
            Instantiate(bankItemPrefab, bankContent).Setup(den, this);
    }

    // Jalankan langsung kalau tidak perlu konfirmasi (atau dialog belum dipasang)
    public void ConfirmOrRun(bool needsConfirm, string message, Action action)
    {
        if (needsConfirm && confirmDialog != null) confirmDialog.Ask(message, action);
        else action();
    }

    // onClick tombol +/-
    public void OnCommitPressed()
    {
        if (Session.CurrentPhase != Phase.Menyusun)
        {
            Feedback.Show(Messages.SoalSudahDibentuk, FeedbackKind.Info);
            return;
        }

        ProblemError err = Session.CommitProblem();   // gagal = soal tidak terbentuk, isi Kotak A dan B tidak berubah
        if (err == ProblemError.None) return;

        Feedback.Show(Messages.For(err), FeedbackKind.Warning);

        // Kotak A tidak lebih besar dari B (hanya mungkin di operasi kurang): popup peringatan, selain notifikasi
        if (err == ProblemError.HasilTidakPositif && confirmDialog != null)
            confirmDialog.Inform(Messages.KurangPeringatanJudul, Messages.KurangPeringatanIsi);
    }

    // onClick CekHasilBtn
    public void OnCheckPressed()
    {
        CheckVerdict? v = Session.Check();
        if (v == null)
        {
            Feedback.Show(Messages.For(RejectReason.SoalBelumDibentuk), FeedbackKind.Warning);
            return;
        }

        Feedback.Show(Messages.For(v.Value), v == CheckVerdict.Benar ? FeedbackKind.Success : FeedbackKind.Warning);
    }

    [ContextMenu("Debug: Commit Problem")]
    void DebugCommit() => Debug.Log("Commit: " + Session.CommitProblem());

    [ContextMenu("Debug: Log State")]
    void DebugLog() => Debug.Log($"{Session.CurrentPhase} | A={Session.A.Total().ToMixedString()} ({Session.A.Count} row) | B={Session.B.Total().ToMixedString()} ({Session.B.Count} row) | dpz={Session.Dpz.Count}");

    [ContextMenu("Debug: Cetak status aktivitas")]
    void DebugActivity()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"[Aktivitas] {name} | operasi={operation} | fase={Session.CurrentPhase} | Drag.Instance=");
        sb.Append(FractionDragController.Instance ? FractionDragController.Instance.name : "null");
        if (worldRoots != null)
            foreach (GameObject r in worldRoots) sb.Append($" | {(r ? r.name : "(kosong)")}={(r && r.activeSelf)}");
        Debug.Log(sb.ToString(), this);
    }
}