public enum RejectReason
{
    None,
    SoalBelumDibentuk,   // dpz dipakai sebelum tombol +/- ditekan
    KotakTerlaluBesar,   // satu Kotak >= MaxKotakWhole + 1
    HasilTerlaluBesar,   // khusus Add: A + B >= MaxWholeTotal + 1
    DpzPenuh             // total dpz > MaxWholeTotal + 1, atau blok baru melebihi MaxDpzBlocks
}

public enum CheckVerdict { Benar, Kurang, Kelebihan, NilaiPasSatuanBeda }

public static class GameRules
{
    public const int MaxDenominator = 25;   // sama dengan bank pecahan
    public const int MaxKotakWhole = 12;   // tiap Kotak: total < 13
    public const int MaxWholeTotal = 6;    // hasil soal: total < 7
    public const int MaxDpzBlocks = 8;    // jumlah blok (baris) di dpz, nanti disetel bareng batas N x N cm
    // kapasitas nilai dpz = MaxWholeTotal + 1 (siswa boleh kelebihan sampai 1 utuh)

    public static bool IsAtLeast(Fraction value, long whole) => value.CompareTo(Fraction.Whole(whole)) >= 0;

    public static RejectReason CheckKotakDrop(Operation op, Fraction totalA, Fraction totalB, bool intoA, Fraction piece)
    {
        Fraction newA = intoA ? totalA + piece : totalA;
        Fraction newB = intoA ? totalB : totalB + piece;

        if (IsAtLeast(newA, MaxKotakWhole + 1) || IsAtLeast(newB, MaxKotakWhole + 1))
            return RejectReason.KotakTerlaluBesar;

        // Cap hasil per-drop cuma aman buat Add (nambah A atau B selalu menaikkan hasil).
        // Subtract: nambah B malah menurunkan hasil, jadi cap hasil dicek di tombol "-".
        if (op == Operation.Add && IsAtLeast(newA + newB, MaxWholeTotal + 1))
            return RejectReason.HasilTerlaluBesar;

        return RejectReason.None;
    }

    // createsNewBlock: true kalau potongan ini bakal jadi blok baru (bukan merge ke blok yang ada)
    public static RejectReason CheckDpzDrop(KotakContents dpz, Fraction piece, bool createsNewBlock)
    {
        if ((dpz.Total() + piece).CompareTo(Fraction.Whole(MaxWholeTotal + 1)) > 0) return RejectReason.DpzPenuh;
        if (createsNewBlock && dpz.Count >= MaxDpzBlocks) return RejectReason.DpzPenuh;   // merge tetap boleh saat penuh
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

    public static CheckVerdict CheckResult(Problem problem, KotakContents dpz)
    {
        int cmp = dpz.Total().CompareTo(problem.result);   // 0 persis sama dengan ValueEquals
        if (cmp < 0) return CheckVerdict.Kurang;
        if (cmp > 0) return CheckVerdict.Kelebihan;
        return IsUnitUniform(dpz) ? CheckVerdict.Benar : CheckVerdict.NilaiPasSatuanBeda;
    }
}