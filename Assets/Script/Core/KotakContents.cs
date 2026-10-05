using System;
using System.Collections.Generic;

public class KotakContents
{
    readonly List<Fraction> pieces = new List<Fraction>();
    public IReadOnlyList<Fraction> Pieces => pieces;
    public int Count => pieces.Count;
    public bool IsEmpty => pieces.Count == 0;

    public event Action Changed;   // row UI dan balok 3D subscribe ke sini (view ikut data)

    public void Add(Fraction piece) { pieces.Add(piece); Changed?.Invoke(); }

    // Satu row maksimal satu utuh (d/d). Penyebut sama tapi row sudah penuh = bukan merge, jadi row baru.
    public bool CanMerge(int index, Fraction incoming)
    {
        if (index < 0 || index >= pieces.Count) return false;
        Fraction p = pieces[index];
        return p.denominator == incoming.denominator
            && p.numerator + incoming.numerator <= p.denominator;
    }

    public void MergeAt(int index, Fraction incoming)
    {
        if (!CanMerge(index, incoming)) throw new InvalidOperationException("Tidak bisa merge: penyebut beda atau row sudah penuh");
        Fraction p = pieces[index];
        pieces[index] = new Fraction(p.numerator + incoming.numerator, p.denominator);   // sengaja TIDAK di-reduce
        Changed?.Invoke();
    }

    public void RemoveAt(int index) { pieces.RemoveAt(index); Changed?.Invoke(); }
    public void Clear() { if (pieces.Count == 0) return; pieces.Clear(); Changed?.Invoke(); }

    public Fraction Total()
    {
        Fraction sum = Fraction.Zero;
        foreach (var p in pieces) sum += p;
        return sum;
    }
}