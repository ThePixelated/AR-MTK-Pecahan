public enum RejectReason
{
    None,
    SoalBelumDibentuk,   // dpz dipakai sebelum tombol +/- ditekan
    KotakTerlaluBesar,   // tidak dipakai lagi sejak revisi 4 (dipertahankan supaya Messages tetap kompilasi)
    HasilTerlaluBesar,   // khusus Add: A + B >= MaxWholeTotal + 1
    DpzPenuh,            // total dpz > MaxWholeTotal + 1, atau blok baru melebihi batas
    KotakBulatPenuh,     // Kotak sudah berisi MaxKotakBulat bilangan bulat
    KotakPecahanBeda,    // Kotak sudah punya jenis pecahan lain
    KotakPecahanPenuh,   // Kotak sudah MaxKotakPecahanRows row pecahan dan semuanya penuh
    KotakTerkunci,       // balok Kotak ini masih di area: kembalikan dulu sebelum mengubah Kotak
    LowerHanyaPecahan,   // area bawah (sisa) tidak menerima bilangan bulat
    LowerSatuanBeda,     // area bawah (sisa) hanya satu jenis pecahan
    BlokSudahDiArea,     // mode salin: row ini sudah ada di area atas
    AreaTidakTersedia    // area dua bagian hanya untuk Add, atau indeks row tidak ada
}

public enum CheckVerdict { Benar, Kurang, Kelebihan, NilaiPasSatuanBeda, UtuhBelumSesuai }

public static class GameRules
{
    public const int MaxDenominator = 25;     // sama dengan bank pecahan
    public const int MaxKotakWhole = 12;      // tidak dipakai lagi sejak revisi 4; boleh dihapus
    public const int MaxKotakBulat = 4;       // bilangan bulat per Kotak
    public const int MaxKotakPecahanRows = 2; // row pecahan per Kotak (row penuh dihitung)
    public const int MaxWholeTotal = 6;       // hasil soal: total < 7
    public const int MaxDpzBlocks = 8;        // jumlah blok di dpz (perilaku lama, Media 6)
    public const int MaxUpperBlocks = 12;     // balok di area atas: cukup untuk semua row dua Kotak (2 x (4 + 2))
    // kapasitas nilai dpz = MaxWholeTotal + 1 (siswa boleh kelebihan sampai 1 utuh)

    public static bool IsAtLeast(Fraction value, long whole) => value.CompareTo(Fraction.Whole(whole)) >= 0;

    // Aturan Kotak: satu jenis bilangan bulat (maks 4) dan satu jenis pecahan (maks 2 row).
    // Potongan pecahan menyatu otomatis ke row sejenis yang belum penuh, jadi row baru hanya terjadi kalau semuanya penuh.
    public static RejectReason CheckKotakDrop(Operation op, KotakContents a, KotakContents b, bool intoA, Fraction piece)
    {
        KotakContents target = intoA ? a : b;

        if (piece.denominator == 1)
        {
            if (target.WholeBlockCount >= MaxKotakBulat) return RejectReason.KotakBulatPenuh;
        }
        else
        {
            if (target.TryGetFractionKind(out long kind) && kind != piece.denominator)
                return RejectReason.KotakPecahanBeda;

            bool merges = false;
            for (int i = 0; i < target.Count && !merges; i++) merges = target.CanMerge(i, piece);
            if (!merges && target.FractionRowCount >= MaxKotakPecahanRows) return RejectReason.KotakPecahanPenuh;
        }

        // Cap hasil per-drop cuma aman buat Add (nambah A atau B selalu menaikkan hasil).
        // Subtract: nambah B malah menurunkan hasil, jadi cap hasil dicek di tombol "-".
        if (op == Operation.Add)
        {
            Fraction newA = intoA ? a.Total() + piece : a.Total();
            Fraction newB = intoA ? b.Total() : b.Total() + piece;
            if (IsAtLeast(newA + newB, MaxWholeTotal + 1)) return RejectReason.HasilTerlaluBesar;
        }

        return RejectReason.None;
    }

    // Perilaku lama (Subtract / Media 6) sampai 4R-e.
    // createsNewBlock: true kalau potongan ini bakal jadi blok baru (bukan merge ke blok yang ada)
    public static RejectReason CheckDpzDrop(KotakContents dpz, Fraction piece, bool createsNewBlock)
    {
        if ((dpz.Total() + piece).CompareTo(Fraction.Whole(MaxWholeTotal + 1)) > 0) return RejectReason.DpzPenuh;
        if (createsNewBlock && dpz.Count >= MaxDpzBlocks) return RejectReason.DpzPenuh;   // merge tetap boleh saat penuh
        return RejectReason.None;
    }

    // Area bawah (Add): hanya pecahan, satu satuan, satu row yang berhenti di penuh (sisa selalu di bawah 1)
    public static RejectReason CheckLowerDrop(KotakContents lower, Fraction piece, bool createsNewBlock)
    {
        if (piece.denominator == 1) return RejectReason.LowerHanyaPecahan;
        if (lower.TryGetFractionKind(out long kind) && kind != piece.denominator) return RejectReason.LowerSatuanBeda;
        if (createsNewBlock && lower.Count >= 1) return RejectReason.DpzPenuh;
        return RejectReason.None;
    }

    // Semua blok pecahan (penyebut != 1) memakai satu penyebut yang sama; blok utuh tidak ikut dihitung
    public static bool IsUnitUniform(KotakContents dpz)
    {
        long den = 0;
        foreach (var p in dpz.Pieces)
        {
            if (p.denominator == 1) continue;
            if (den == 0) den = p.denominator;
            else if (p.denominator != den) return false;
        }
        return true;
    }

    // Perilaku lama (Subtract / Media 6) sampai 4R-e
    public static CheckVerdict CheckResult(Problem problem, KotakContents dpz)
    {
        int cmp = dpz.Total().CompareTo(problem.result);   // 0 persis sama dengan ValueEquals
        if (cmp < 0) return CheckVerdict.Kurang;
        if (cmp > 0) return CheckVerdict.Kelebihan;
        return IsUnitUniform(dpz) ? CheckVerdict.Benar : CheckVerdict.NilaiPasSatuanBeda;
    }

    // Add: sisa pecahan hasil (bagian di bawah 1) dibandingkan dengan nilai area bawah. Opsional: bilangan bulat area atas juga dicek.
    public static CheckVerdict CheckAreaResult(Problem problem, AreaContents upper, KotakContents lower, bool checkWholes)
    {
        Fraction expectedRemainder = problem.result - Fraction.Whole(problem.result.WholePart);

        int cmp = lower.Total().CompareTo(expectedRemainder);
        if (cmp < 0) return CheckVerdict.Kurang;
        if (cmp > 0) return CheckVerdict.Kelebihan;

        if (checkWholes && upper.WholeCount != problem.result.WholePart) return CheckVerdict.UtuhBelumSesuai;
        return CheckVerdict.Benar;
    }
}