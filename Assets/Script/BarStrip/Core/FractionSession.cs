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
    // Cek aturan tanpa mengubah apa pun
    public RejectReason PreviewDropToKotak(bool intoA, Fraction piece) =>
        GameRules.CheckKotakDrop(Op, A.Total(), B.Total(), intoA, piece);

    // rowIndex >= 0: drop persis di atas row itu. Satuan sama dan belum penuh -> merge, selain itu row baru.
    public RejectReason TryDropToKotak(bool intoA, Fraction piece, int rowIndex = -1)
    {
        RejectReason why = PreviewDropToKotak(intoA, piece);
        if (why != RejectReason.None) return why;

        KotakContents k = Side(intoA);
        if (rowIndex >= 0 && rowIndex < k.Count && k.CanMerge(rowIndex, piece)) k.MergeAt(rowIndex, piece);
        else k.Add(piece);
        return RejectReason.None;
    }

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
    // Blok yang akan menerima potongan ini, atau -1 kalau jadi blok baru.
    // blockIndex >= 0 (persis di atas blok): hanya blok itu; penuh atau beda satuan = blok baru.
    // blockIndex < 0 (zona luar): blok sejenis pertama yang belum penuh.
    public int FindDpzMergeTarget(Fraction piece, int blockIndex = -1)
    {
        if (blockIndex >= 0) return Dpz.CanMerge(blockIndex, piece) ? blockIndex : -1;

        for (int i = 0; i < Dpz.Count; i++)
            if (Dpz.CanMerge(i, piece)) return i;
        return -1;
    }

    public RejectReason PreviewDropToDpz(Fraction piece, int blockIndex = -1)
    {
        if (CurrentPhase == Phase.Menyusun) return RejectReason.SoalBelumDibentuk;
        bool createsNewBlock = FindDpzMergeTarget(piece, blockIndex) < 0;
        return GameRules.CheckDpzDrop(Dpz, piece, createsNewBlock);
    }

    public RejectReason TryDropToDpz(Fraction piece, int blockIndex = -1)
    {
        RejectReason why = PreviewDropToDpz(piece, blockIndex);
        if (why != RejectReason.None) return why;

        int target = FindDpzMergeTarget(piece, blockIndex);
        if (target >= 0) Dpz.MergeAt(target, piece);
        else Dpz.Add(piece);
        return RejectReason.None;
    }

    public void RemoveFromDpz(int index) => Dpz.RemoveAt(index);          // hapus blok utuh
    public void RemoveUnitFromDpz(int index) => Dpz.DecrementAt(index);   // -1 satuan

    // Gabung antar-blok dpz: prediksi berapa satuan yang pindah (tanpa mengubah apa pun)
    public int PreviewMergeDpzBlocks(int source, int target) => Dpz.CountTransferable(source, target);

    // Gabung antar-blok dpz: pindahkan satuan dari source ke target, hanya yang muat; mengembalikan jumlah yang pindah
    public int MergeDpzBlocks(int source, int target) => Dpz.TransferUnits(source, target);
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

    // ---- reaksi otomatis ----
    void OnKotakChanged()
    {
        if (CurrentPhase == Phase.Menyusun) return;

        clearingDpz = true;
        Dpz.Clear();
        clearingDpz = false;

        Current = default;
        LastVerdict = null;
        SetPhase(Phase.Menyusun);   // satu notifikasi, state sudah konsisten
    }

    void OnDpzChanged()
    {
        if (clearingDpz) return;
        if (CurrentPhase == Phase.Dicek) { LastVerdict = null; SetPhase(Phase.Merakit); }
    }

    void SetPhase(Phase p) { CurrentPhase = p; StateChanged?.Invoke(); }
}