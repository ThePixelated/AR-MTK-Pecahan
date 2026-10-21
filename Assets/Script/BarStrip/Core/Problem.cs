public enum Operation { Add, Subtract }

public enum ProblemError
{
    None,
    KotakKosong,           // A atau B masih kosong
    HasilTidakPositif,     // Subtract: A harus lebih besar dari B
    HasilTerlaluBesar,     // hasil >= MaxWholeTotal + 1, gak muat di dpz
    PenyebutTerlaluRumit   // penyebut A, B, atau hasil (tereduksi) > MaxDenominator
}

public readonly struct Problem
{
    public readonly Operation op;
    public readonly Fraction a, b, result;   // semuanya sudah reduced

    Problem(Operation op, Fraction a, Fraction b, Fraction result)
    { this.op = op; this.a = a; this.b = b; this.result = result; }

    public static ProblemError TryCreate(Operation op, Fraction totalA, Fraction totalB, out Problem problem)
    {
        problem = default;

        Fraction a = totalA.Reduce(), b = totalB.Reduce();
        if (a.Sign <= 0 || b.Sign <= 0) return ProblemError.KotakKosong;

        Fraction result = op == Operation.Add ? a + b : a - b;
        if (result.Sign <= 0) return ProblemError.HasilTidakPositif;
        if (GameRules.IsAtLeast(result, GameRules.MaxWholeTotal + 1)) return ProblemError.HasilTerlaluBesar;
        if (a.denominator > GameRules.MaxDenominator || b.denominator > GameRules.MaxDenominator ||
            result.denominator > GameRules.MaxDenominator) return ProblemError.PenyebutTerlaluRumit;

        problem = new Problem(op, a, b, result);
        return ProblemError.None;
    }
}