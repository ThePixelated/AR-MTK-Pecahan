using System;
using System.Collections.Generic;

// State dan aturan Media 3 (data murni).
// Gelas air dan gelas sirup awalnya penuh. Satu swipe menuang SELURUH gelas ke wadah: gelas jadi kosong dan hitungan larutan itu naik 1.
// Tombol Isi mengisi gelas itu lagi. Wadah awalnya kosong; tinggi isinya = total tuangan / MaxTotalPours.
public sealed class Media3Session : IPourSession
{
    public Media3Limits Limits { get; }

    // Penanda untuk PourController dan binder: dua gelas pemberi dan satu wadah penerima
    public LiquidContainer Syrup { get; } = new LiquidContainer("Gelas sirup", ContainerRole.Give);
    public LiquidContainer Water { get; } = new LiquidContainer("Gelas air", ContainerRole.Give);
    public LiquidContainer W { get; } = new LiquidContainer("Wadah", ContainerRole.Receive);

    readonly List<Media3Entry> entries = new List<Media3Entry>();
    public IReadOnlyList<Media3Entry> Entries => entries;

    public int WaterCount { get; private set; }
    public int SyrupCount { get; private set; }
    public int TotalCount => WaterCount + SyrupCount;
    public bool IsEmpty => TotalCount == 0;
    public float SyrupRatio => TotalCount == 0 ? 0f : (float)SyrupCount / TotalCount;     // 0 = air bening

    public int Capacity => Limits.MaxTotalPours;
    public bool WadahFull => TotalCount >= Capacity;
    public float FillRatio => Capacity <= 0 ? 0f : (float)TotalCount / Capacity;           // tinggi isi wadah 0..1

    public bool WaterCupFull { get; private set; } = true;
    public bool SyrupCupFull { get; private set; } = true;

    public bool TrialsFull => entries.Count >= Limits.MaxTrials;
    public int LastDuplicateIndex { get; private set; } = -1;     // indeks entri yang kembar pada penolakan DuplicateEntry terakhir

    public event Action Changed;           // apa pun berubah
    public event Action EntriesChanged;    // daftar entri berubah (tambah, hapus, reset)

    public Media3Session(Media3Limits limits = null) { Limits = limits ?? new Media3Limits(); }

    bool CupFull(LiquidKind kind) => kind == LiquidKind.Water ? WaterCupFull : SyrupCupFull;

    // ---- tuang: satu swipe = seluruh gelas ----
    public PourError CheckPour(LiquidContainer source, LiquidContainer target)
    {
        if (source == target) return PourError.SameContainer;
        if (!source.CanGive) return PourError.SourceCannotGive;
        if (!target.CanReceive) return PourError.TargetCannotReceive;

        LiquidKind kind = source == Water ? LiquidKind.Water : LiquidKind.Syrup;
        if (!CupFull(kind)) return PourError.SourceEmpty;       // gelas sudah kosong: isi dulu
        if (WadahFull) return PourError.TargetFull;
        return PourError.None;
    }

    public PourError TryPour(LiquidContainer source, LiquidContainer target)
    {
        PourError err = CheckPour(source, target);
        if (err != PourError.None) return err;

        if (source == Water) { WaterCount++; WaterCupFull = false; }
        else { SyrupCount++; SyrupCupFull = false; }
        Changed?.Invoke();
        return PourError.None;
    }

    // ---- isi ulang ----
    public Media3RefillResult CheckRefill(LiquidKind kind) =>
        CupFull(kind) ? Media3RefillResult.AlreadyFull : Media3RefillResult.Refilled;

    public Media3RefillResult TryRefill(LiquidKind kind)
    {
        Media3RefillResult r = CheckRefill(kind);
        if (r != Media3RefillResult.Refilled) return r;

        if (kind == LiquidKind.Water) WaterCupFull = true;
        else SyrupCupFull = true;
        Changed?.Invoke();
        return r;
    }

    // ---- Gabungkan: kedua gelas (harus penuh) menuang sekaligus; air +1 dan sirup +1 ----
    public PourError CheckMerge()
    {
        if (!WaterCupFull || !SyrupCupFull) return PourError.SourceEmpty;
        if (TotalCount + 2 > Capacity) return PourError.TargetFull;
        return PourError.None;
    }

    public PourError TryMerge()
    {
        PourError err = CheckMerge();
        if (err != PourError.None) return err;

        WaterCount++; SyrupCount++;
        WaterCupFull = false; SyrupCupFull = false;
        Changed?.Invoke();
        return PourError.None;
    }

    // ---- entri (tombol Input) ----
    // Mencatat isi wadah sebagai entri baru, lalu wadah dikosongkan dan kedua gelas diisi lagi untuk percobaan berikutnya.
    // Kalau ditolak, tidak ada yang berubah.
    public Media3InputResult TryInput()
    {
        if (TrialsFull) return Media3InputResult.TrialsFull;
        if (IsEmpty) return Media3InputResult.NothingPoured;

        var entry = new Media3Entry(SyrupCount, WaterCount);
        for (int i = 0; i < entries.Count; i++)
        {
            if (!entries[i].SamePair(entry)) continue;
            LastDuplicateIndex = i;
            return Media3InputResult.DuplicateEntry;
        }

        entries.Add(entry);
        ClearMixture();
        EntriesChanged?.Invoke();
        Changed?.Invoke();
        return Media3InputResult.Added;
    }

    // Hapus satu entri (nomor entri di bawahnya bergeser). Isi wadah tidak disentuh.
    public bool RemoveEntry(int index)
    {
        if (index < 0 || index >= entries.Count) return false;
        entries.RemoveAt(index);
        EntriesChanged?.Invoke();
        Changed?.Invoke();
        return true;
    }

    // Kosongkan wadah (entri tetap); kedua gelas penuh lagi
    public void ResetMixture()
    {
        if (IsEmpty && WaterCupFull && SyrupCupFull) return;
        ClearMixture();
        Changed?.Invoke();
    }

    // Hapus semua entri, kosongkan wadah, isi kedua gelas
    public void ResetAll()
    {
        bool hadEntries = entries.Count > 0;
        entries.Clear();
        ClearMixture();
        if (hadEntries) EntriesChanged?.Invoke();
        Changed?.Invoke();
    }

    void ClearMixture()
    {
        WaterCount = 0;
        SyrupCount = 0;
        WaterCupFull = true;
        SyrupCupFull = true;
    }
}