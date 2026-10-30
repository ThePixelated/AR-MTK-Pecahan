using System;

public enum Phase { Menyusun, Merakit, Dicek }

public class FractionSession
{
    public Operation Op { get; }
    public SessionOptions Options { get; }
    public KotakContents A { get; } = new KotakContents();
    public KotakContents B { get; } = new KotakContents();

    // Area Balok. Add: Upper = balok dari Kotak, Dpz (= Lower) = balok bank untuk sisa. Subtract: hanya Dpz (perilaku lama sampai 4R-e).
    public AreaContents Upper { get; } = new AreaContents();
    public KotakContents Dpz { get; } = new KotakContents();
    public KotakContents Lower => Dpz;

    public Phase CurrentPhase { get; private set; } = Phase.Menyusun;
    public Problem Current { get; private set; }            // valid kalau CurrentPhase != Menyusun
    public CheckVerdict? LastVerdict { get; private set; }

    public event Action StateChanged;

    // true = ubah Kotak sekarang bakal menghapus area (UI bisa munculin konfirmasi dulu)
    public bool KotakEditClearsDpz => CurrentPhase != Phase.Menyusun;

    // Mode pindah: selama balok sebuah Kotak ada di area, Kotak itu tidak boleh diubah
    public bool IsKotakLocked(bool isA) => Options.MoveFromKotak && Upper.CountFrom(isA) > 0;

    int busy;      // > 0 = perubahan sedang dilakukan sesi sendiri, jangan dianggap edit pengguna

    public FractionSession(Operation op, SessionOptions options = null)
    {
        Op = op;
        Options = options ?? new SessionOptions();
        A.Changed += OnKotakChanged;
        B.Changed += OnKotakChanged;
        Dpz.Changed += OnDpzChanged;
        Upper.Changed += OnUpperChanged;
    }

    bool IsSubtracted(bool fromA) => Op == Operation.Subtract && !fromA;

    KotakContents Side(bool isA) => isA ? A : B;

    // ---- Kotak A/B ----
    // Cek aturan tanpa mengubah apa pun
    public RejectReason PreviewDropToKotak(bool intoA, Fraction piece)
    {
        if (IsKotakLocked(intoA)) return RejectReason.KotakTerkunci;
        return GameRules.CheckKotakDrop(Op, A, B, intoA, piece);
    }

    // Row pecahan sejenis pertama yang belum penuh, atau -1 (row baru)
    public int FindKotakMergeTarget(bool intoA, Fraction piece)
    {
        if (piece.denominator == 1) return -1;      // bilangan bulat: selalu row sendiri

        KotakContents k = Side(intoA);
        for (int i = 0; i < k.Count; i++)
            if (k.CanMerge(i, piece)) return i;
        return -1;
    }

    // Merge otomatis: potongan pecahan masuk ke row sejenis yang belum penuh, di mana pun dijatuhkan.
    // rowIndex tidak dipakai lagi (dipertahankan supaya pemanggil lama tetap kompilasi).
    public RejectReason TryDropToKotak(bool intoA, Fraction piece, int rowIndex = -1)
    {
        RejectReason why = PreviewDropToKotak(intoA, piece);
        if (why != RejectReason.None) return why;

        KotakContents k = Side(intoA);
        int target = FindKotakMergeTarget(intoA, piece);
        if (target >= 0) k.MergeAt(target, piece);
        else k.Add(piece);
        return RejectReason.None;
    }

    // Kotak yang terkunci (balok masih di area) tidak bisa diubah; pemanggil mengecek IsKotakLocked untuk menampilkan pesan
    public void RemoveFromKotak(bool fromA, int index)
    {
        if (IsKotakLocked(fromA)) return;
        Side(fromA).RemoveAt(index);
    }

    public void ClearKotak(bool isA)
    {
        if (IsKotakLocked(isA)) return;
        Side(isA).Clear();
    }

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

    // ---- area atas: balok dari Kotak (hanya Add) ----
    public RejectReason PreviewMoveToArea(bool fromA, int rowIndex)
    {
        // NOT USED - if (Op != Operation.Add) return RejectReason.AreaTidakTersedia;

        if (CurrentPhase == Phase.Menyusun) return RejectReason.SoalBelumDibentuk;

        KotakContents k = Side(fromA);
        if (rowIndex < 0 || rowIndex >= k.Count) return RejectReason.AreaTidakTersedia;
        if (!Options.MoveFromKotak && Upper.Contains(fromA, rowIndex)) return RejectReason.BlokSudahDiArea;
        if (Upper.Count >= GameRules.MaxUpperBlocks) return RejectReason.DpzPenuh;
        return RejectReason.None;
    }

    // Mode pindah: row keluar dari Kotak. Mode salin: Kotak tetap utuh. Soal tidak berubah.
    public RejectReason TryMoveToArea(bool fromA, int rowIndex)
    {
        RejectReason why = PreviewMoveToArea(fromA, rowIndex);
        if (why != RejectReason.None) return why;

        KotakContents k = Side(fromA);
        Fraction piece = k.Pieces[rowIndex];

        if (Options.MoveFromKotak)
        {
            int origin = Upper.OriginIndexFor(fromA, rowIndex);
            busy++;
            try { k.RemoveAt(rowIndex); }
            finally { busy--; }
            Upper.Add(new AreaBlock(piece, fromA, origin, IsSubtracted(fromA)));
        }
        else
        {
            Upper.Add(new AreaBlock(piece, fromA, rowIndex, IsSubtracted(fromA)));
        }
        return RejectReason.None;
    }

    // Kembalikan satu balok area atas ke Kotak asalnya (mode salin: salinan dihapus)
    public bool ReturnToKotak(int areaIndex)
    {
        if (areaIndex < 0 || areaIndex >= Upper.Count) return false;
        ReturnBlock(areaIndex);
        AfterAreaEdit();
        return true;
    }

    public void ReturnAllToKotak()
    {
        if (Upper.IsEmpty) return;
        ReturnAll();
        AfterAreaEdit();
    }

    void ReturnBlock(int areaIndex)
    {
        AreaBlock block = Upper.Blocks[areaIndex];
        busy++;
        try
        {
            Upper.RemoveAt(areaIndex);
            if (Options.MoveFromKotak)
                Side(block.fromA).InsertAt(Upper.RestoreIndexFor(block.fromA, block.originIndex), block.piece);
        }
        finally { busy--; }
    }

    void ReturnAll()
    {
        while (Upper.Count > 0) ReturnBlock(Upper.Count - 1);
    }

    // ---- area bawah (dpz): balok bank ----
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
        return GameRules.CheckLowerDrop(Dpz, piece, createsNewBlock);
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

    // Gabung antar-blok dpz (perilaku lama): prediksi berapa satuan yang pindah (tanpa mengubah apa pun)
    public int PreviewMergeDpzBlocks(int source, int target) => Dpz.CountTransferable(source, target);

    // Gabung antar-blok dpz (perilaku lama): pindahkan satuan dari source ke target, hanya yang muat
    public int MergeDpzBlocks(int source, int target) => Dpz.TransferUnits(source, target);
    public void ClearDpz() => Dpz.Clear();

    // ---- Cek Hasil ----
    public CheckVerdict? Check()
    {
        if (CurrentPhase == Phase.Menyusun) return null;
        LastVerdict = GameRules.CheckAreaResult(Current, Upper, Dpz, Options.CheckWholes);
        SetPhase(Phase.Dicek);
        return LastVerdict;
    }

    // Mengosongkan semuanya: balok di area tidak dikembalikan karena Kotak ikut dikosongkan
    public void ResetAll()
    {
        busy++;
        try
        {
            Upper.Clear();
            A.Clear();
            B.Clear();
            Dpz.Clear();
        }
        finally { busy--; }

        if (CurrentPhase != Phase.Menyusun)
        {
            Current = default;
            LastVerdict = null;
            SetPhase(Phase.Menyusun);
        }
    }

    // ---- reaksi otomatis ----
    // Pengguna mengubah Kotak setelah soal dibentuk: balok yang masih di area kembali ke Kotaknya, area dikosongkan, soal dibatalkan
    void OnKotakChanged()
    {
        if (busy > 0 || CurrentPhase == Phase.Menyusun) return;

        busy++;
        try
        {
            ReturnAll();
            Dpz.Clear();
        }
        finally { busy--; }

        Current = default;
        LastVerdict = null;
        SetPhase(Phase.Menyusun);   // satu notifikasi, state sudah konsisten
    }

    void OnDpzChanged()
    {
        if (busy > 0) return;
        AfterAreaEdit();
    }

    void OnUpperChanged()
    {
        if (busy > 0) return;
        AfterAreaEdit();
    }

    // Area diubah setelah dicek: hasil lama tidak berlaku lagi
    void AfterAreaEdit()
    {
        if (CurrentPhase == Phase.Dicek) { LastVerdict = null; SetPhase(Phase.Merakit); }
    }

    void SetPhase(Phase p) { CurrentPhase = p; StateChanged?.Invoke(); }
}