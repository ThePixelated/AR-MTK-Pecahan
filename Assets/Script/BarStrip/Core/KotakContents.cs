using System;
using System.Collections.Generic;

public class KotakContents
{
    readonly List<Fraction> pieces = new List<Fraction>();
    public IReadOnlyList<Fraction> Pieces => pieces;
    public int Count => pieces.Count;
    public bool IsEmpty => pieces.Count == 0;

    // Index blok yang dihapus oleh perubahan terakhir, atau -1 (dipakai animasi reflow). Diisi sebelum Changed.
    public int LastRemovedIndex { get; private set; } = -1;

    public event Action Changed;   // row UI dan balok 3D subscribe ke sini (view ikut data)

    public void Add(Fraction piece)
    {
        pieces.Add(piece);
        LastRemovedIndex = -1;
        Changed?.Invoke();
    }

    // Satu row/blok = maksimal satu utuh (d/d). Penyebut sama tapi sudah penuh = bukan merge, jadi blok baru.
    public bool CanMerge(int index, Fraction incoming)
    {
        if (index < 0 || index >= pieces.Count) return false;
        Fraction p = pieces[index];
        return p.denominator == incoming.denominator
            && p.numerator + incoming.numerator <= p.denominator;
    }

    public void MergeAt(int index, Fraction incoming)
    {
        if (!CanMerge(index, incoming)) throw new InvalidOperationException("Tidak bisa merge: penyebut beda atau blok sudah penuh");
        Fraction p = pieces[index];
        pieces[index] = new Fraction(p.numerator + incoming.numerator, p.denominator);   // sengaja TIDAK di-reduce
        LastRemovedIndex = -1;
        Changed?.Invoke();
    }

    // -1 satuan: n/d menjadi (n-1)/d; kalau satuannya habis, blok dihapus
    public void DecrementAt(int index)
    {
        Fraction p = pieces[index];
        if (p.numerator <= 1) { pieces.RemoveAt(index); LastRemovedIndex = index; }
        else { pieces[index] = new Fraction(p.numerator - 1, p.denominator); LastRemovedIndex = -1; }
        Changed?.Invoke();
    }

    // Berapa satuan yang bisa pindah dari blok source ke blok target:
    // penyebut sama, bukan blok yang sama, target belum penuh; hanya sebanyak yang muat
    public int CountTransferable(int source, int target)
    {
        if (source == target || source < 0 || target < 0 || source >= pieces.Count || target >= pieces.Count) return 0;

        Fraction s = pieces[source], t = pieces[target];
        if (s.denominator != t.denominator) return 0;

        long room = t.denominator - t.numerator;
        return (int)Math.Max(0L, Math.Min(s.numerator, room));
    }

    // Pindahkan satuan dari source ke target (hanya yang muat). Sumber habis = blok sumber dihapus.
    // Satu notifikasi Changed setelah semua berubah. Mengembalikan jumlah satuan yang pindah.
    public int TransferUnits(int source, int target)
    {
        int moved = CountTransferable(source, target);
        if (moved <= 0) return 0;

        Fraction s = pieces[source], t = pieces[target];
        pieces[target] = new Fraction(t.numerator + moved, t.denominator);

        long left = s.numerator - moved;
        if (left <= 0) { pieces.RemoveAt(source); LastRemovedIndex = source; }
        else { pieces[source] = new Fraction(left, s.denominator); LastRemovedIndex = -1; }

        Changed?.Invoke();
        return moved;
    }

    public void RemoveAt(int index)
    {
        pieces.RemoveAt(index);
        LastRemovedIndex = index;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (pieces.Count == 0) return;
        pieces.Clear();
        LastRemovedIndex = -1;      // semua hilang sekaligus: tanpa reflow
        Changed?.Invoke();
    }

    // ---- aturan Kotak: satu jenis bilangan bulat dan satu jenis pecahan ----
    // Jumlah row bilangan bulat (penyebut 1)
    public int WholeBlockCount
    {
        get
        {
            int n = 0;
            foreach (var p in pieces) if (p.denominator == 1) n++;
            return n;
        }
    }

    // Jumlah row pecahan (penyebut > 1), termasuk row penuh seperti 2/2
    public int FractionRowCount
    {
        get
        {
            int n = 0;
            foreach (var p in pieces) if (p.denominator != 1) n++;
            return n;
        }
    }

    // Penyebut dari satu-satunya jenis pecahan di Kotak ini; false kalau belum ada row pecahan
    public bool TryGetFractionKind(out long denominator)
    {
        foreach (var p in pieces)
        {
            if (p.denominator == 1) continue;
            denominator = p.denominator;
            return true;
        }
        denominator = 0;
        return false;
    }

    // Row penuh (n/n, termasuk bilangan bulat) berperilaku sebagai satu bilangan bulat
    public int FullRowCount
    {
        get
        {
            int n = 0;
            foreach (var p in pieces) if (p.numerator == p.denominator) n++;
            return n;
        }
    }

    // Jumlah row yang belum penuh: bagian pecahan yang harus dijumlahkan
    public Fraction PartialTotal()
    {
        Fraction sum = Fraction.Zero;
        foreach (var p in pieces) if (p.numerator != p.denominator) sum += p;
        return sum;
    }

    public Fraction Total()
    {
        Fraction sum = Fraction.Zero;
        foreach (var p in pieces) sum += p;
        return sum;
    }

    // Sisipkan row di posisi tertentu (dipakai mengembalikan balok dari area ke urutan aslinya); di luar jangkauan = ditaruh paling bawah
    public void InsertAt(int index, Fraction piece)
    {
        index = Math.Max(0, Math.Min(index, pieces.Count));
        pieces.Insert(index, piece);
        LastRemovedIndex = -1;
        Changed?.Invoke();
    }
}