using System;
using System.Collections.Generic;

// Satu balok di area atas: row yang diambil dari Kotak A atau B
public readonly struct AreaBlock
{
    public readonly Fraction piece;
    public readonly bool fromA;
    public readonly int originIndex;     // posisi row di Kotak asal sebelum ada yang dipindah (untuk memulihkan urutan)

    public AreaBlock(Fraction piece, bool fromA, int originIndex)
    {
        this.piece = piece;
        this.fromA = fromA;
        this.originIndex = originIndex;
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

    public Fraction Total()
    {
        Fraction sum = Fraction.Zero;
        foreach (AreaBlock b in blocks) sum += b.piece;
        return sum;
    }

    // Row penuh (n/n, termasuk bilangan bulat) berperilaku sebagai satu bilangan bulat
    public int FullRowCount
    {
        get
        {
            int n = 0;
            foreach (AreaBlock b in blocks) if (b.piece.numerator == b.piece.denominator) n++;
            return n;
        }
    }

    // Jumlah row yang belum penuh: bagian pecahan yang harus dijumlahkan
    public Fraction PartialTotal()
    {
        Fraction sum = Fraction.Zero;
        foreach (AreaBlock b in blocks) if (b.piece.numerator != b.piece.denominator) sum += b.piece;
        return sum;
    }

    // Bilangan bulat tambahan dari penjumlahan row sebagian (5/4 -> 1)
    public long CarryWholes => PartialTotal().WholePart;

    // Bilangan bulat yang terlihat di area: row penuh ditambah carry
    public long WholeCount => FullRowCount + CarryWholes;

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