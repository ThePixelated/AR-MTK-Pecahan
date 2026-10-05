using System;

public enum Phase { Menyusun, Merakit, Dicek }

public class FractionSession
{
    public Operation Op { get; }
    public KotakContents A { get; } = new KotakContents();
    public KotakContents B { get; } = new KotakContents();
    public KotakContents Dpz { get; } = new KotakContents();

    public Phase CurrentPhase { get; private set; } = Phase.Menyusun;
    public Problem Current { get; private set; }            // valid kalau CurrentPhase != Menyusun
    public CheckVerdict? LastVerdict { get; private set; }

    public event Action StateChanged;

    // true = ubah Kotak sekarang bakal menghapus dpz (UI bisa munculin konfirmasi dulu)
    public bool KotakEditClearsDpz => CurrentPhase != Phase.Menyusun;

    bool clearingDpz;

    public FractionSession(Operation op)
    {
        Op = op;
        A.Changed += OnKotakChanged;
        B.Changed += OnKotakChanged;
        Dpz.Changed += OnDpzChanged;
    }

    KotakContents Side(bool isA) => isA ? A : B;

    // ---- Kotak A/B ----
    // rowIndex >= 0: drop persis di atas row itu. Satuan sama -> merge, beda -> row baru.
    //public RejectReason TryDropToKotak(bool intoA, Fraction piece, int rowIndex = -1)
    //{
    //    RejectReason why = GameRules.CheckKotakDrop(Op, A.Total(), B.Total(), intoA, piece);
    //    if (why != RejectReason.None) return why;

    //    KotakContents k = Side(intoA);
    //    if (rowIndex >= 0 && rowIndex < k.Count && k.CanMerge(rowIndex, piece)) k.MergeAt(rowIndex, piece);
    //    else k.Add(piece);
    //    return RejectReason.None;
    //}

    public void RemoveFromKotak(bool fromA, int index) => Side(fromA).RemoveAt(index);
    public void ClearKotak(bool isA) => Side(isA).Clear();

    // ---- Tombol + / - ----
    public ProblemError CommitProblem()
    {
        if (CurrentPhase != Phase.Menyusun) return ProblemError.None;

        ProblemError err = Problem.TryCreate(Op, A.Total(), B.Total(), out Problem p);
        if (err != ProblemError.None) return err;

        Current = p;
        LastVerdict = null;
        Dpz.Clear();
        SetPhase(Phase.Merakit);
        return ProblemError.None;
    }

    // ---- dpz ----
    //public RejectReason TryDropToDpz(Fraction piece)
    //{
    //    if (CurrentPhase == Phase.Menyusun) return RejectReason.SoalBelumDibentuk;

    //    RejectReason why = GameRules.CheckDpzDrop(Dpz, piece);
    //    if (why == RejectReason.None) Dpz.Add(piece);
    //    return why;
    //}

    public void RemoveFromDpz(int index) => Dpz.RemoveAt(index);
    public void ClearDpz() => Dpz.Clear();

    // ---- Cek Hasil ----
    public CheckVerdict? Check()
    {
        if (CurrentPhase == Phase.Menyusun) return null;
        LastVerdict = GameRules.CheckResult(Current, Dpz);
        SetPhase(Phase.Dicek);
        return LastVerdict;
    }

    public void ResetAll() { A.Clear(); B.Clear(); Dpz.Clear(); }

    
    void OnKotakChanged()
    {
        if (CurrentPhase == Phase.Menyusun) return;

        clearingDpz = true;
        Dpz.Clear();
        clearingDpz = false;

        Current = default;
        LastVerdict = null;
        SetPhase(Phase.Menyusun);   // satu notif ni, const dipake semua
    }

    // cek dpz
    // masih error di detection, prefab destroyed, value change
    void OnDpzChanged()
    {
        if (clearingDpz) return;
        if (CurrentPhase == Phase.Dicek) { LastVerdict = null; SetPhase(Phase.Merakit); }
    }

    void SetPhase(Phase p) { CurrentPhase = p; StateChanged?.Invoke(); }

    // Cek aturan tanpa mengubah apa pun
    public RejectReason PreviewDropToKotak(bool intoA, Fraction piece) =>
        GameRules.CheckKotakDrop(Op, A.Total(), B.Total(), intoA, piece);

    public RejectReason PreviewDropToDpz(Fraction piece) =>
        CurrentPhase == Phase.Menyusun ? RejectReason.SoalBelumDibentuk : GameRules.CheckDpzDrop(Dpz, piece);

    public RejectReason TryDropToKotak(bool intoA, Fraction piece, int rowIndex = -1)
    {
        RejectReason why = PreviewDropToKotak(intoA, piece);
        if (why != RejectReason.None) return why;

        KotakContents k = Side(intoA);
        if (rowIndex >= 0 && rowIndex < k.Count && k.CanMerge(rowIndex, piece)) k.MergeAt(rowIndex, piece);
        else k.Add(piece);
        return RejectReason.None;
    }

    public RejectReason TryDropToDpz(Fraction piece)
    {
        RejectReason why = PreviewDropToDpz(piece);
        if (why == RejectReason.None) Dpz.Add(piece);
        return why;
    }
}