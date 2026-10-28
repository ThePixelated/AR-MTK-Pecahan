using System;

public enum NumberLineCell { Whole, Numerator, Denominator }

public enum NumberLineSoalStatus
{
    Empty,        // belum ada yang diisi
    Incomplete,   // pembilang atau penyebut terisi sebelah saja
    Impossible,   // penyebut 0
    Valid
}

public enum NumberLineEditResult { Ok, Overflow }    // Overflow: lebih dari MaxCell, kotak dikosongkan

public sealed class NumberLineSoalInput
{
    public int MaxCell { get; }
    int? whole, numerator, denominator;

    public event Action Changed;

    public NumberLineSoalInput(int maxCell = 25) { MaxCell = maxCell; }

    public int? Get(NumberLineCell cell) =>
        cell == NumberLineCell.Whole ? whole : cell == NumberLineCell.Numerator ? numerator : denominator;

    void Set(NumberLineCell cell, int? v)
    {
        if (cell == NumberLineCell.Whole) whole = v;
        else if (cell == NumberLineCell.Numerator) numerator = v;
        else denominator = v;
    }

    public bool IsEmpty => !whole.HasValue && !numerator.HasValue && !denominator.HasValue;

    // Teks yang tampil di kotak; whole = 0 dikosongkan
    public string TextOf(NumberLineCell cell)
    {
        int? v = Get(cell);
        if (!v.HasValue) return "";
        if (cell == NumberLineCell.Whole && v.Value == 0) return "";
        return v.Value.ToString();
    }

    public NumberLineEditResult TypeDigit(NumberLineCell cell, int digit)
    {
        if (digit < 0 || digit > 9) return NumberLineEditResult.Ok;

        int next = (Get(cell) ?? 0) * 10 + digit;
        if (next > MaxCell)
        {
            Set(cell, null);
            Changed?.Invoke();
            return NumberLineEditResult.Overflow;
        }

        Set(cell, next);
        Changed?.Invoke();
        return NumberLineEditResult.Ok;
    }

    public void Backspace(NumberLineCell cell)
    {
        int? v = Get(cell);
        if (!v.HasValue) return;
        Set(cell, v.Value < 10 ? (int?)null : v.Value / 10);
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (IsEmpty) return;
        whole = null;
        numerator = null;
        denominator = null;
        Changed?.Invoke();
    }

    // Nilai Soal sebagai pecahan tak sejati (tidak disederhanakan): whole*penyebut + pembilang, per penyebut
    public NumberLineSoalStatus Evaluate(out Fraction value)
    {
        value = Fraction.Zero;

        if (!numerator.HasValue && !denominator.HasValue)
        {
            if (!whole.HasValue) return NumberLineSoalStatus.Empty;
            value = Fraction.Whole(whole.Value);       // hanya bilangan bulat
            return NumberLineSoalStatus.Valid;
        }

        if (!numerator.HasValue || !denominator.HasValue) return NumberLineSoalStatus.Incomplete;
        if (denominator.Value == 0) return NumberLineSoalStatus.Impossible;

        long den = denominator.Value;
        value = new Fraction((long)(whole ?? 0) * den + numerator.Value, den);
        return NumberLineSoalStatus.Valid;
    }
}