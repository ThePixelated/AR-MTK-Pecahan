using System;

public enum BoardConfigResult { Applied, Unchanged, NotSupported, NoSelection, LengthOutOfRange, PartisiOutOfRange, LengthBelowPlaced }

// Papan soal MultiPion: beberapa pion bergerak (satu per tokoh) pada SATU garis bilangan.
// Tiap pion punya partisi sendiri (default: penyebut jawabannya). Garis menampilkan partisi pion yang sedang dipilih.
// Posisi pion disimpan sebagai nilai (tick / partisi pion itu), bukan tick garis, jadi pion lain TIDAK bergeser
// walau partisi garis berganti. Murni data: tanpa UnityEngine, bisa diuji tanpa scene.
//
// Mode Lines (QuestionKind.MultiPionLines): tiap pion juga membawa panjang dan partisi buatan siswa. Garis tetap SATU dan
// di-redraw mengikuti pion terpilih. Supaya semua pion yang sudah ditaruh tetap kelihatan di garis yang sama, panjang garis
// yang tampil = max(panjang pion terpilih, panjang minimum yang dibutuhkan pion lain). Dinilai selalu ByValue.
public sealed class MultiPionBoard
{
    sealed class Pion
    {
        public string label;
        public int partisi;
        public int length;     // hanya mode Lines: panjang garis milik pion ini
        public FractionValue answer;
        public bool placed;
        public int tick;       // tick pada partisi pion ini (0..Length * partisi)
    }

    readonly Pion[] pions;
    readonly NumberLineLimits limits;
    int fixedLength;                                          // mode biasa: panjang garis satu untuk semua pion

    public bool Lines { get; }
    public int Length => Lines ? LinesLength() : fixedLength; // mode Lines: panjang yang TAMPIL
    public PionCheckMode Mode { get; }
    public int SelectedIndex { get; private set; } = -1;      // pion yang partisinya sedang tampil di garis; -1 = belum ada

    public event Action Changed;

    public MultiPionBoard(NumberLineQuestion q, NumberLineLimits limits, int maxPions)
    {
        this.limits = limits ?? new NumberLineLimits();
        Lines = q.kind == QuestionKind.MultiPionLines;
        Mode = Lines ? PionCheckMode.ByValue : q.checkMode;
        fixedLength = Math.Min(Math.Max(q.presetLength, this.limits.MinLength), this.limits.MaxLength);

        int count = q.pions == null ? 0 : Math.Min(q.pions.Count, Math.Max(0, maxPions));
        pions = new Pion[count];
        for (int i = 0; i < count; i++)
        {
            PionAnswer a = q.pions[i] ?? new PionAnswer();
            int p = Lines ? this.limits.DefaultPartisi : a.ResolvedPartisi;      // Lines: mulai dari default, bukan penyebut jawaban (tidak membocorkan)
            if (p <= 0) p = this.limits.DefaultPartisi;
            pions[i] = new Pion
            {
                label = a.label ?? "",
                partisi = Math.Min(Math.Max(p, this.limits.MinPartisi), this.limits.MaxPartisi),
                length = fixedLength,
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
        return (float)pions[i].tick / ((float)Length * pions[i].partisi);      // Length mode Lines selalu >= nilai pion yang ditaruh, jadi hasilnya <= 1
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
        if (Lines) return false;      // mode Lines memakai ApplyActiveConfig

        int clamped = Math.Min(Math.Max(length, limits.MinLength), limits.MaxLength);
        if (clamped == fixedLength) return false;

        fixedLength = clamped;
        foreach (Pion p in pions)
        {
            if (p.placed && p.tick > fixedLength * p.partisi) { p.placed = false; p.tick = 0; }
        }
        Changed?.Invoke();
        return true;
    }

    // ---------- mode Lines ----------
    // Panjang minimum supaya semua pion yang sudah ditaruh tetap ada di garis
    public int RequiredLength()
    {
        int need = limits.MinLength;
        foreach (Pion p in pions)
            if (p.placed) need = Math.Max(need, (p.tick + p.partisi - 1) / p.partisi);
        return need;
    }

    int LinesLength()
    {
        int own = SelectedIndex >= 0 ? pions[SelectedIndex].length : (pions.Length > 0 ? pions[0].length : fixedLength);
        return Math.Min(Math.Max(own, RequiredLength()), limits.MaxLength);
    }

    // true = mengganti partisi pion terpilih ke nilai ini memindahkan pionnya ke posisi 0 (nilainya tidak jatuh di tick partisi baru)
    public bool ResetsActivePionOnPartisi(int partisi)
    {
        if (!Lines || SelectedIndex < 0 || partisi <= 0) return false;
        Pion p = pions[SelectedIndex];
        return p.placed && p.partisi != partisi && ((long)p.tick * partisi) % p.partisi != 0;
    }

    // Menyimpan panjang dan partisi milik pion terpilih. Pion yang sudah di garis dipertahankan nilainya (tick dikonversi);
    // kalau nilainya tidak ada di partisi baru, pion tetap di garis tapi dipindah ke posisi 0. Panjang tidak boleh di bawah posisi pion mana pun yang sudah ditaruh.
    public BoardConfigResult ApplyActiveConfig(int length, int partisi)
    {
        if (!Lines) return BoardConfigResult.NotSupported;
        if (SelectedIndex < 0) return BoardConfigResult.NoSelection;
        if (length < limits.MinLength || length > limits.MaxLength) return BoardConfigResult.LengthOutOfRange;
        if (partisi < limits.MinPartisi || partisi > limits.MaxPartisi) return BoardConfigResult.PartisiOutOfRange;
        if (length < RequiredLength()) return BoardConfigResult.LengthBelowPlaced;

        Pion p = pions[SelectedIndex];
        if (p.length == length && p.partisi == partisi) return BoardConfigResult.Unchanged;

        if (p.placed && p.partisi != partisi)
        {
            long scaled = (long)p.tick * partisi;
            if (scaled % p.partisi == 0) p.tick = (int)(scaled / p.partisi);
            else p.tick = 0;      // tetap di garis, kembali ke 0 (tidak perlu ditarik lagi dari tombolnya)
        }
        p.partisi = partisi;
        p.length = length;
        Changed?.Invoke();
        return BoardConfigResult.Applied;
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
        bool selectionChanged = SelectedIndex != i;
        SelectedIndex = i;                       // dulu: mode Lines menghitung Length dari pion terpilih
        int max = Length * p.partisi;
        int clamped = tick < 0 ? 0 : (tick > max ? max : tick);

        bool changed = !p.placed || p.tick != clamped || selectionChanged;
        p.placed = true;
        p.tick = clamped;
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