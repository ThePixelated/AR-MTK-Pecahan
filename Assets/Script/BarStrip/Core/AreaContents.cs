using System;
using System.Collections.Generic;

// Satu balok di area atas: row yang diambil dari Kotak A atau B
public readonly struct AreaBlock
{
    public readonly Fraction piece;
    public readonly bool fromA;
    public readonly int originIndex;     // posisi row di Kotak asal sebelum ada yang dipindah (untuk memulihkan urutan)
    public readonly bool subtracted;     // Subtract: balok dari Kotak B dikurangkan

    public AreaBlock(Fraction piece, bool fromA, int originIndex, bool subtracted = false)
    {
        this.piece = piece;
        this.fromA = fromA;
        this.originIndex = originIndex;
        this.subtracted = subtracted;
    }
}

// Bagian atas Area Balok: balok yang ditarik dari Kotak. Urutan tidak berpengaruh pada nilai.
public class AreaContents
{
    readonly List<AreaBlock> blocks = new List<AreaBlock>();

    public IReadOnlyList<AreaBlock> Blocks => blocks;
    public int Count => blocks.Count;
    public bool IsEmpty => blocks.Count == 0;
    public int LastRemovedIndex { get; private set; } = -1;     // dipakai animasi reflow, diisi sebelum Changed

    public event Action Changed;

    public void Add(AreaBlock block)
    {
        blocks.Add(block);
        LastRemovedIndex = -1;
        Changed?.Invoke();
    }

    public void RemoveAt(int index)
    {
        blocks.RemoveAt(index);
        LastRemovedIndex = index;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (blocks.Count == 0) return;
        blocks.Clear();
        LastRemovedIndex = -1;
        Changed?.Invoke();
    }

    // Bertanda: balok yang dikurangkan (Subtract, Kotak B) bernilai negatif
    public Fraction Total()
    {
        Fraction sum = Fraction.Zero;
        foreach (AreaBlock b in blocks) sum += b.subtracted ? Fraction.Zero - b.piece : b.piece;
        return sum;
    }

    // Row penuh (n/n, termasuk bilangan bulat) berperilaku sebagai satu bilangan bulat; yang dikurangkan dihitung negatif
    public int FullRowCount
    {
        get
        {
            int n = 0;
            foreach (AreaBlock b in blocks)
                if (b.piece.numerator == b.piece.denominator) n += b.subtracted ? -1 : 1;
            return n;
        }
    }

    // Jumlah bertanda row yang belum penuh: bagian pecahan yang harus dijumlahkan (atau dikurangkan)
    public Fraction PartialTotal()
    {
        Fraction sum = Fraction.Zero;
        foreach (AreaBlock b in blocks)
            if (b.piece.numerator != b.piece.denominator) sum += b.subtracted ? Fraction.Zero - b.piece : b.piece;
        return sum;
    }

    // 5/4 -> 1 (carry); -1/4 -> -1 (pinjam 1)
    public long CarryWholes => FloorOf(PartialTotal());

    // Bilangan bulat bersih yang terlihat di area: row penuh ditambah carry
    public long WholeCount => FullRowCount + CarryWholes;

    static long FloorOf(Fraction f)
    {
        long n = f.numerator, d = f.denominator;
        if (d == 0) return 0;
        long q = n / d;
        if (n % d != 0 && ((n < 0) != (d < 0))) q--;
        return q;
    }

    public int CountFrom(bool fromA)
    {
        int n = 0;
        foreach (AreaBlock b in blocks) if (b.fromA == fromA) n++;
        return n;
    }

    // Mode salin: row asal ini sudah ada di area?
    public bool Contains(bool fromA, int originIndex)
    {
        foreach (AreaBlock b in blocks)
            if (b.fromA == fromA && b.originIndex == originIndex) return true;
        return false;
    }

    // Indeks row di Kotak yang sudah berkurang -> indeks aslinya sebelum ada yang dipindah
    public int OriginIndexFor(bool fromA, int currentIndex)
    {
        var taken = new List<int>();
        foreach (AreaBlock b in blocks) if (b.fromA == fromA) taken.Add(b.originIndex);
        taken.Sort();

        int origin = currentIndex;
        foreach (int r in taken) if (r <= origin) origin++;
        return origin;
    }

    // Indeks sisip di Kotak saat balok dengan originIndex ini dikembalikan (balok lain dari Kotak itu yang masih di area tidak dihitung)
    public int RestoreIndexFor(bool fromA, int originIndex)
    {
        int before = 0;
        foreach (AreaBlock b in blocks)
            if (b.fromA == fromA && b.originIndex < originIndex) before++;
        return originIndex - before;
    }
}