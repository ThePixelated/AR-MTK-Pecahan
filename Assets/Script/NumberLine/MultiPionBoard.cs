using System;

// Papan soal MultiPion: beberapa pion bergerak (satu per tokoh) pada SATU garis bilangan.
// Tiap pion punya partisi sendiri (default: penyebut jawabannya). Garis menampilkan partisi pion yang sedang dipilih.
// Posisi pion disimpan sebagai nilai (tick / partisi pion itu), bukan tick garis, jadi pion lain TIDAK bergeser
// walau partisi garis berganti. Murni data: tanpa UnityEngine, bisa diuji tanpa scene.
public sealed class MultiPionBoard
{
    sealed class Pion
    {
        public string label;
        public int partisi;
        public FractionValue answer;
        public bool placed;
        public int tick;       // tick pada partisi pion ini (0..Length * partisi)
    }

    readonly Pion[] pions;
    readonly NumberLineLimits limits;

    public int Length { get; private set; }
    public PionCheckMode Mode { get; }
    public int SelectedIndex { get; private set; } = -1;      // pion yang partisinya sedang tampil di garis; -1 = belum ada

    public event Action Changed;

    public MultiPionBoard(NumberLineQuestion q, NumberLineLimits limits, int maxPions)
    {
        this.limits = limits ?? new NumberLineLimits();
        Mode = q.checkMode;
        Length = Math.Min(Math.Max(q.presetLength, this.limits.MinLength), this.limits.MaxLength);

        int count = q.pions == null ? 0 : Math.Min(q.pions.Count, Math.Max(0, maxPions));
        pions = new Pion[count];
        for (int i = 0; i < count; i++)
        {
            PionAnswer a = q.pions[i] ?? new PionAnswer();
            int p = a.ResolvedPartisi;
            if (p <= 0) p = this.limits.DefaultPartisi;
            pions[i] = new Pion
            {
                label = a.label ?? "",
                partisi = Math.Min(Math.Max(p, this.limits.MinPartisi), this.limits.MaxPartisi),
                answer = a.value
            };
        }
    }

    // ---------- data pion ----------
    public int Count => pions.Length;

    bool Valid(int i) => i >= 0 && i < pions.Length;

    // Teks tombol tokoh; kosong di soal = A, B, C ...
    public string Label(int i)
    {
        if (!Valid(i)) return "";
        return string.IsNullOrWhiteSpace(pions[i].label) ? NumberLineLabels.Letter(i) : pions[i].label.Trim();
    }

    public int Partisi(int i) => Valid(i) ? pions[i].partisi : 0;
    public bool IsPlaced(int i) => Valid(i) && pions[i].placed;
    public int TickOf(int i) => IsPlaced(i) ? pions[i].tick : -1;                       // tick pada partisi pion itu sendiri
    public Fraction ValueOf(int i) => IsPlaced(i) ? new Fraction(pions[i].tick, pions[i].partisi) : Fraction.Zero;

    // Posisi 0..1 sepanjang garis; tidak bergantung pada partisi garis yang sedang tampil
    public float NormalizedOf(int i)
    {
        if (!IsPlaced(i)) return 0f;
        return (float)pions[i].tick / ((float)Length * pions[i].partisi);
    }

    public int PlacedCount
    {
        get
        {
            int n = 0;
            foreach (Pion p in pions) if (p.placed) n++;
            return n;
        }
    }

    public bool AllPlaced => pions.Length > 0 && PlacedCount == pions.Length;

    // ---------- garis ----------
    // Partisi yang tampil: milik pion yang dipilih; belum ada yang dipilih = milik pion pertama
    public int ActivePartisi =>
        SelectedIndex >= 0 ? pions[SelectedIndex].partisi : (pions.Length > 0 ? pions[0].partisi : limits.DefaultPartisi);

    public NumberLineConfig Config => new NumberLineConfig(Length, ActivePartisi);

    // Panjang garis berubah (hanya kalau soal tidak mengunci konfigurasi). Pion yang jadi di luar garis dicabut.
    public bool ApplyLength(int length)
    {
        int clamped = Math.Min(Math.Max(length, limits.MinLength), limits.MaxLength);
        if (clamped == Length) return false;

        Length = clamped;
        foreach (Pion p in pions)
        {
            if (p.placed && p.tick > Length * p.partisi) { p.placed = false; p.tick = 0; }
        }
        Changed?.Invoke();
        return true;
    }

    // ---------- interaksi ----------
    // Tap tombol tokoh, atau tap atau mulai drag pion di garis. Bukan hover.
    public bool Select(int i)
    {
        if (!Valid(i)) return false;
        if (i != SelectedIndex)
        {
            SelectedIndex = i;
            Changed?.Invoke();
        }
        return true;
    }

    public void ClearSelection()
    {
        if (SelectedIndex < 0) return;
        SelectedIndex = -1;
        Changed?.Invoke();
    }

    // Menaruh pion di posisi 0..1 sepanjang garis, snap ke tick terdekat pada partisi pion itu. Mengembalikan tick, atau -1 kalau indeks salah.
    public int PlaceAtNormalized(int i, float normalized)
    {
        if (!Valid(i)) return -1;
        int tick = new NumberLineConfig(Length, pions[i].partisi).NearestTick(normalized);
        return PlaceAtTick(i, tick);
    }

    public int PlaceAtTick(int i, int tick)
    {
        if (!Valid(i)) return -1;

        Pion p = pions[i];
        int max = Length * p.partisi;
        int clamped = tick < 0 ? 0 : (tick > max ? max : tick);

        bool changed = !p.placed || p.tick != clamped || SelectedIndex != i;
        p.placed = true;
        p.tick = clamped;
        SelectedIndex = i;
        if (changed) Changed?.Invoke();
        return clamped;
    }

    // Pion dikembalikan ke tombolnya (dilepas di sisi kiri)
    public bool Remove(int i)
    {
        if (!IsPlaced(i)) return false;
        pions[i].placed = false;
        pions[i].tick = 0;
        Changed?.Invoke();
        return true;
    }

    // ---------- penilaian ----------
    public bool IsCorrect(int i)
    {
        if (!IsPlaced(i)) return false;

        Pion p = pions[i];
        FractionValue a = p.answer;
        if (!a.IsValid) return false;

        if (Mode == PionCheckMode.ByTick)
            return p.partisi == a.denominator && p.tick == (long)a.whole * a.denominator + a.numerator;

        return ValueOf(i).ValueEquals(a.ToFraction());
    }

    // Belum semua pion ditempatkan = Incomplete, tanpa membocorkan benar atau salah
    public LatihanOutcome Evaluate(out NumberLineIndicator[] indicators)
    {
        indicators = new NumberLineIndicator[pions.Length];
        if (!AllPlaced) return LatihanOutcome.Incomplete;

        bool all = true;
        for (int i = 0; i < pions.Length; i++)
        {
            bool ok = IsCorrect(i);
            indicators[i] = ok ? NumberLineIndicator.Correct : NumberLineIndicator.Wrong;
            if (!ok) all = false;
        }
        return all ? LatihanOutcome.Correct : LatihanOutcome.Wrong;
    }
}
