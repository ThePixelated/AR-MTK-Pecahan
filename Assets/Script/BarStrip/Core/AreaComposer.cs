using System;
using System.Collections.Generic;

public enum ComposedKind { Whole, Carry, Fraction }

// Satu balok yang digambar di area atas
public sealed class ComposedBlock
{
    public ComposedKind kind;
    public Fraction piece;        // n/d: d = jumlah sel di track, n = sel terisi
    public int[] cellSource;      // indeks AreaBlock sumber tiap sel terisi (panjang = n): menentukan warna sel
    public int[] sources;         // indeks AreaBlock yang membentuk balok ini (tanpa duplikat): yang kembali ke Kotak saat ditap
}

// Menyusun balok area atas dari row yang ditarik dari Kotak. Add: carry, bilangan bulat, lalu pecahan. Subtract: nilai bersih.
public static class AreaComposer
{
    public const long MaxComposedDenominator = 60;     // KPK lebih besar dari ini: balok tidak digabung, tampil sendiri-sendiri

    public static List<ComposedBlock> Compose(Operation op, IReadOnlyList<AreaBlock> blocks)
    {
        if (blocks.Count == 0) return new List<ComposedBlock>();
        return op == Operation.Add ? ComposeAdd(blocks) : ComposeSubtract(blocks);
    }

    // ---------- Add ----------
    static List<ComposedBlock> ComposeAdd(IReadOnlyList<AreaBlock> blocks)
    {
        var carry = new List<ComposedBlock>();
        var wholes = new List<ComposedBlock>();
        var fractions = new List<ComposedBlock>();

        var partials = new List<int>();
        for (int i = 0; i < blocks.Count; i++)
        {
            Fraction p = blocks[i].piece;
            if (p.numerator == p.denominator) wholes.Add(Single(ComposedKind.Whole, blocks, i));   // row penuh = bilangan bulat
            else partials.Add(i);
        }

        if (partials.Count == 1)
        {
            fractions.Add(Single(ComposedKind.Fraction, blocks, partials[0]));
        }
        else if (partials.Count > 1)
        {
            long l = 1;
            foreach (int i in partials) l = Lcm(l, blocks[i].piece.denominator);

            if (l > MaxComposedDenominator)
            {
                foreach (int i in partials) fractions.Add(Single(ComposedKind.Fraction, blocks, i));
            }
            else
            {
                // satuan terbesar (penyebut kecil) dulu; urutan tarik tidak berpengaruh
                partials.Sort((x, y) =>
                {
                    int c = blocks[x].piece.denominator.CompareTo(blocks[y].piece.denominator);
                    return c != 0 ? c : x.CompareTo(y);
                });

                var cells = new List<int>();
                foreach (int i in partials)
                {
                    long count = blocks[i].piece.numerator * (l / blocks[i].piece.denominator);
                    for (long c = 0; c < count; c++) cells.Add(i);
                }

                int full = (int)(cells.Count / l);
                int rest = (int)(cells.Count % l);
                for (int k = 0; k < full; k++)
                    carry.Add(Make(ComposedKind.Carry, new Fraction(l, l), cells.GetRange((int)(k * l), (int)l)));
                if (rest > 0)
                    fractions.Add(Make(ComposedKind.Fraction, new Fraction(rest, l), cells.GetRange((int)(full * l), rest)));
            }
        }

        var all = new List<ComposedBlock>(carry);
        all.AddRange(wholes);
        all.AddRange(fractions);
        return all;
    }

    // ---------- Subtract ----------
    static List<ComposedBlock> ComposeSubtract(IReadOnlyList<AreaBlock> blocks)
    {
        Fraction net = Fraction.Zero;
        foreach (AreaBlock b in blocks) net += b.subtracted ? Fraction.Zero - b.piece : b.piece;

        // belum positif (mis. baru Kotak B yang ditarik): tampilkan tiap balok sendiri supaya masih bisa dikembalikan
        if (net.Sign <= 0) return Raw(blocks);

        Fraction r = net.Reduce();
        long whole = r.numerator / r.denominator;
        long remN = r.numerator % r.denominator;

        var all = new List<int>();
        for (int i = 0; i < blocks.Count; i++) all.Add(i);

        // sumber warna: pecahan sebagian milik A, kalau tidak ada row A pertama
        int source = 0;
        bool found = false;
        for (int i = 0; i < blocks.Count && !found; i++)
            if (!blocks[i].subtracted && blocks[i].piece.numerator != blocks[i].piece.denominator) { source = i; found = true; }
        for (int i = 0; i < blocks.Count && !found; i++)
            if (!blocks[i].subtracted) { source = i; found = true; }

        var result = new List<ComposedBlock>();
        for (long w = 0; w < whole; w++) result.Add(Make(ComposedKind.Whole, new Fraction(1, 1), new List<int> { source }, all));

        if (remN > 0)
        {
            long l = 1;
            foreach (AreaBlock b in blocks)
                if (b.piece.numerator != b.piece.denominator) l = Lcm(l, b.piece.denominator);
            if (l > MaxComposedDenominator || l % r.denominator != 0) l = r.denominator;

            long fn = remN * (l / r.denominator);
            var cells = new List<int>();
            for (long c = 0; c < fn; c++) cells.Add(source);
            result.Add(Make(ComposedKind.Fraction, new Fraction(fn, l), cells, all));
        }
        return result;
    }

    static List<ComposedBlock> Raw(IReadOnlyList<AreaBlock> blocks)
    {
        var result = new List<ComposedBlock>();
        for (int i = 0; i < blocks.Count; i++)
        {
            bool full = blocks[i].piece.numerator == blocks[i].piece.denominator;
            result.Add(Single(full ? ComposedKind.Whole : ComposedKind.Fraction, blocks, i));
        }
        return result;
    }

    // ---------- pembantu ----------
    static ComposedBlock Single(ComposedKind kind, IReadOnlyList<AreaBlock> blocks, int index)
    {
        var cells = new List<int>();
        for (long c = 0; c < blocks[index].piece.numerator; c++) cells.Add(index);
        return Make(kind, blocks[index].piece, cells);
    }

    static ComposedBlock Make(ComposedKind kind, Fraction piece, List<int> cells, List<int> sourcesOverride = null)
    {
        var distinct = new List<int>();
        foreach (int i in (sourcesOverride ?? cells)) if (!distinct.Contains(i)) distinct.Add(i);
        distinct.Sort();
        return new ComposedBlock { kind = kind, piece = piece, cellSource = cells.ToArray(), sources = distinct.ToArray() };
    }

    static long Gcd(long a, long b)
    {
        a = Math.Abs(a); b = Math.Abs(b);
        while (b != 0) { long t = a % b; a = b; b = t; }
        return a == 0 ? 1 : a;
    }

    static long Lcm(long a, long b) => a / Gcd(a, b) * b;
}