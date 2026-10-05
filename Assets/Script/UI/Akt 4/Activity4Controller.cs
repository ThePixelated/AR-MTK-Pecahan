using System;
using UnityEngine;

public class Activity4Controller : MonoBehaviour
{
    [SerializeField] Operation operation = Operation.Add;    // Media 6: Subtract
    [SerializeField] FractionPalette palette;
    [SerializeField] Transform bankContent;                  // PecahanPanel/.../Scroll View/Viewport/Content
    [SerializeField] FractionBankItem bankItemPrefab;
    [SerializeField] SimpleConfirmDialog confirmDialog;

    FractionSession session;
    public FractionSession Session => session ??= new FractionSession(operation);

    public FractionPalette Palette => palette;

    void Awake()
    {
        //Session = new FractionSession(operation);
        BuildBank();
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

    [ContextMenu("Debug: Commit Problem")]
    void DebugCommit() => Debug.Log("Commit: " + Session.CommitProblem());

    [ContextMenu("Debug: Log State")]
    void DebugLog() => Debug.Log($"{Session.CurrentPhase} | A={Session.A.Total().ToMixedString()} ({Session.A.Count} row) | B={Session.B.Total().ToMixedString()} ({Session.B.Count} row) | dpz={Session.Dpz.Count}");
}