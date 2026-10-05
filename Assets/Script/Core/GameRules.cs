public enum RejectReason
{
    None,
    SoalBelumDibentuk,   // dpz dipakai sebelum tombol +/- ditekan
    KotakTerlaluBesar,   // satu Kotak >= MaxKotakWhole + 1
    HasilTerlaluBesar,   // khusus Add: A + B >= MaxWholeTotal + 1
    SatuanBeda,          // dpz: balok pecahan beda penyebut dari yang sudah ada
    DpzPenuh             // total dpz > MaxWholeTotal + 1
}

public enum CheckVerdict { Benar, Kurang, Kelebihan }

public static class GameRules
{
    public const int MaxDenominator = 25;   // sama dengan bank pecahan
    public const int MaxKotakWhole = 12;   // tiap Kotak: total < 13 (list gak kepanjangan, Soal muat)
    public const int MaxWholeTotal = 6;    // hasil soal: total < 7
    // kapasitas dpz = MaxWholeTotal + 1, supaya siswa bisa "kelebihan" sampai 1 utuh

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

    public static RejectReason CheckDpzDrop(KotakContents dpz, Fraction piece)
    {
        if (piece.denominator != 1 && TryGetLockedDenominator(dpz, out long locked) && locked != piece.denominator)
            return RejectReason.SatuanBeda;

        if ((dpz.Total() + piece).CompareTo(Fraction.Whole(MaxWholeTotal + 1)) > 0)
            return RejectReason.DpzPenuh;

        return RejectReason.None;
    }

    // Balok pecahan pertama di dpz ngunci penyebut; balok utuh (penyebut 1) gak kena kunci
    public static bool TryGetLockedDenominator(KotakContents dpz, out long denominator)
    {
        foreach (var p in dpz.Pieces)
            if (p.denominator != 1) { denominator = p.denominator; return true; }
        denominator = 0;
        return false;
    }

    public static CheckVerdict CheckResult(Problem problem, KotakContents dpz)
    {
        int cmp = dpz.Total().CompareTo(problem.result);   // 0 persis sama dengan ValueEquals
        return cmp == 0 ? CheckVerdict.Benar : cmp < 0 ? CheckVerdict.Kurang : CheckVerdict.Kelebihan;
    }
}