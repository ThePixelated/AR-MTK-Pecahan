using System;
using System.Collections.Generic;

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

    // Balok area atas yang digambar (hasil AreaComposer); selalu mutakhir saat Upper.Changed diterima pelihat
    public IReadOnlyList<ComposedBlock> Composed => composed;
    readonly List<ComposedBlock> composed = new List<ComposedBlock>();


    public Phase CurrentPhase { get; private set; } = Phase.Menyusun;
    public Problem Current { get; private set; }            // valid kalau CurrentPhase != Menyusun
    public CheckVerdict? LastVerdict { get; private set; }

    public event Action StateChanged;
    public event Action UpperViewChanged;      // balok area atas perlu digambar ulang: isi area berubah, atau tombol Jumlahkan ditekan

    // Balok area atas digabung (hasil tombol Jumlahkan). Kembali false kalau isi area berubah atau soal diatur ulang.
    public bool Combined { get; private set; }

    // Setelah dijumlahkan dan Lock After Combine menyala: Kotak A, Kotak B, dan balok area atas tidak bisa diubah sampai atur ulang
    public bool IsCombineLocked => Options.LockAfterCombine && Combined;

    // true = ubah Kotak sekarang bakal menghapus area (UI bisa munculin konfirmasi dulu)
    public bool KotakEditClearsDpz => CurrentPhase != Phase.Menyusun;

    // Mode pindah: selama balok sebuah Kotak ada di area, Kotak itu tidak boleh diubah
    public bool IsKotakLocked(bool isA) => IsCombineLocked || (Options.MoveFromKotak && Upper.CountFrom(isA) > 0);

    // Alasan Kotak terkunci (untuk pesan): terkunci karena sudah dijumlahkan, atau karena balok Kotak ini masih di area
    public RejectReason KotakLockReason(bool isA)
    {
        if (IsCombineLocked) return RejectReason.AreaTerkunci;
        return Options.MoveFromKotak && Upper.CountFrom(isA) > 0 ? RejectReason.KotakTerkunci : RejectReason.None;
    }

    int busy;      // > 0 = perubahan sedang dilakukan sesi sendiri, jangan dianggap edit pengguna

    public FractionSession(Operation op, SessionOptions options = null)
    {
        Op = op;
        Options = options ?? new SessionOptions();
        Upper.Changed += OnUpperDataChanged;
        A.Changed += OnKotakChanged;
        B.Changed += OnKotakChanged;
        Dpz.Changed += OnDpzChanged;
        Upper.Changed += OnUpperChanged;
    }

    bool IsSubtracted(bool fromA) => Op == Operation.Subtract && !fromA;

    KotakContents Side(bool isA) => isA ? A : B;

    void RecomputeComposed()
    {
        composed.Clear();
        composed.AddRange(Combined ? AreaComposer.Compose(Op, Upper.Blocks) : AreaComposer.Separate(Upper.Blocks));
    }

    // Isi area atas berubah: gabungan lama tidak berlaku lagi, balok tampil terpisah sampai Jumlahkan ditekan lagi
    void OnUpperDataChanged()
    {
        Combined = false;
        RecomputeComposed();
        UpperViewChanged?.Invoke();
    }

    // ---- tombol Jumlahkan ----
    public CombineResult PreviewCombine()
    {
        if (CurrentPhase == Phase.Menyusun) return CombineResult.SoalBelumDibentuk;
        if (Combined) return CombineResult.SudahDijumlahkan;
        if (Upper.CountFrom(true) == 0 || Upper.CountFrom(false) == 0) return CombineResult.PerluKeduaKotak;
        return CombineResult.Berhasil;
    }

    // Menggabungkan balok area atas: satuan pecahan disamakan, carry terbentuk, bilangan bulat dipisah
    public CombineResult CombineUpper()
    {
        CombineResult r = PreviewCombine();
        if (r != CombineResult.Berhasil) return r;

        Combined = true;
        RecomputeComposed();
        UpperViewChanged?.Invoke();
        StateChanged?.Invoke();
        return CombineResult.Berhasil;
    }

    // Tap balok gabungan: semua row yang membentuknya (dan balok lain yang berbagi sumber dengannya) kembali ke Kotak asalnya
    public bool ReturnComposed(int composedIndex)
    {
        if (IsCombineLocked) return false;
        if (composedIndex < 0 || composedIndex >= composed.Count) return false;

        var set = new HashSet<int>(composed[composedIndex].sources);
        bool grew;
        do
        {
            grew = false;
            foreach (ComposedBlock c in composed)
            {
                bool touches = false;
                foreach (int s in c.sources) if (set.Contains(s)) { touches = true; break; }
                if (!touches) continue;
                foreach (int s in c.sources) if (set.Add(s)) grew = true;
            }
        } while (grew);

        var order = new List<int>(set);
        order.Sort();
        for (int i = order.Count - 1; i >= 0; i--) ReturnBlock(order[i]);     // dari belakang supaya indeks tidak bergeser
        AfterAreaEdit();
        return true;
    }

    // ---- Kotak A/B ----
    // Cek aturan tanpa mengubah apa pun
    public RejectReason PreviewDropToKotak(bool intoA, Fraction piece)
    {
        RejectReason lockReason = KotakLockReason(intoA);
        if (lockReason != RejectReason.None) return lockReason;
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
        Combined = false;
        Dpz.Clear();
        SetPhase(Phase.Merakit);
        return ProblemError.None;
    }

    // ---- area atas: balok dari Kotak (hanya Add) ----
    public RejectReason PreviewMoveToArea(bool fromA, int rowIndex)
    {
        // NOT USED - if (Op != Operation.Add) return RejectReason.AreaTidakTersedia;

        if (CurrentPhase == Phase.Menyusun) return RejectReason.SoalBelumDibentuk;
        if (IsCombineLocked) return RejectReason.AreaTerkunci;

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
        if (IsCombineLocked) return false;
        if (areaIndex < 0 || areaIndex >= Upper.Count) return false;
        ReturnBlock(areaIndex);
        AfterAreaEdit();
        return true;
    }

    public void ReturnAllToKotak()
    {
        if (IsCombineLocked || Upper.IsEmpty) return;
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

    // ---- Cek Hasil dua tahap: balok area bawah dan jawaban di kotak "?" ----
    // Bagian pecahan hasil (di bawah 1)
    Fraction ResultRemainder => Current.result - Fraction.Whole(Current.result.WholePart);

    // answer = nilai yang diisi siswa di kotak "?" (null = belum diisi). Dinilai menurut nilai: 7/4 dan 1 3/4 sama.
    // Urutan penolakan: soal belum dibentuk, belum dijumlahkan, lalu yang kosong. Balok dan jawaban dinilai terpisah.
    public FullCheckResult CheckFull(Fraction? answer)
    {
        if (CurrentPhase == Phase.Menyusun) return new FullCheckResult(FullCheckOutcome.SoalBelumDibentuk);
        if (!Combined) return new FullCheckResult(FullCheckOutcome.BelumDijumlahkan);

        // hasil bilangan bulat: sisa pecahannya 0, jadi area bawah yang kosong justru benar
        bool blocksEmpty = Dpz.IsEmpty && ResultRemainder.Sign != 0;
        bool answered = answer.HasValue;

        if (blocksEmpty && !answered) return new FullCheckResult(FullCheckOutcome.KeduanyaKosong);
        if (blocksEmpty) return new FullCheckResult(FullCheckOutcome.BalokKosong);
        if (!answered) return new FullCheckResult(FullCheckOutcome.JawabanKosong);

        CheckVerdict verdict = GameRules.CheckAreaResult(Current, Upper, Dpz, Options.CheckWholes);
        bool blocksOk = verdict == CheckVerdict.Benar;
        bool answerOk = answer.Value.ValueEquals(Current.result);

        LastVerdict = verdict;
        SetPhase(Phase.Dicek);

        FullCheckOutcome outcome = blocksOk && answerOk ? FullCheckOutcome.Benar
                                 : blocksOk ? FullCheckOutcome.JawabanSalah
                                 : answerOk ? FullCheckOutcome.BalokSalah
                                 : FullCheckOutcome.KeduanyaSalah;
        return new FullCheckResult(outcome, verdict);
    }

    // ---- Cek Hasil (hanya balok; dipertahankan untuk tes dan pemanggil lama) ----
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

        if (Combined)
        {
            Combined = false;
            RecomputeComposed();
            UpperViewChanged?.Invoke();
        }

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