using System;
using System.Collections.Generic;

// State dan aturan Media 3 (data murni). Gelas air dan sirup tidak pernah habis; tiap swipe menambah 1 cangkir ke wadah.
public sealed class Media3Session : IPourSession
{
    public Media3Limits Limits { get; }

    // Penanda untuk PourController dan binder (isinya tidak dipakai): dua gelas pemberi dan satu wadah penerima
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

    public bool TrialsFull => entries.Count >= Limits.MaxTrials;
    public int LastDuplicateIndex { get; private set; } = -1;     // indeks entri yang kembar pada penolakan DuplicateEntry terakhir

    public event Action Changed;           // apa pun berubah
    public event Action EntriesChanged;    // daftar entri berubah (tambah, hapus, reset)

    public Media3Session(Media3Limits limits = null) { Limits = limits ?? new Media3Limits(); }

    // ---- tuang: satu swipe = 1 cangkir ----
    public PourError CheckPour(LiquidContainer source, LiquidContainer target)
    {
        if (source == target) return PourError.SameContainer;
        if (!source.CanGive) return PourError.SourceCannotGive;
        if (!target.CanReceive) return PourError.TargetCannotReceive;
        if (source == Water && WaterCount >= Limits.MaxPerCup) return PourError.TargetFull;
        if (source == Syrup && SyrupCount >= Limits.MaxPerCup) return PourError.TargetFull;
        return PourError.None;
    }

    public PourError TryPour(LiquidContainer source, LiquidContainer target)
    {
        PourError err = CheckPour(source, target);
        if (err != PourError.None) return err;

        if (source == Water) WaterCount++;
        else SyrupCount++;
        Changed?.Invoke();
        return PourError.None;
    }

    // ---- Gabungkan: air +1 dan sirup +1 sekaligus; keduanya harus muat, kalau tidak tidak ada yang berubah ----
    public PourError CheckMerge() =>
        WaterCount >= Limits.MaxPerCup || SyrupCount >= Limits.MaxPerCup ? PourError.TargetFull : PourError.None;

    public PourError TryMerge()
    {
        PourError err = CheckMerge();
        if (err != PourError.None) return err;

        WaterCount++;
        SyrupCount++;
        Changed?.Invoke();
        return PourError.None;
    }

    // ---- entri (tombol Input) ----
    // Mencatat isi wadah sebagai entri baru, lalu hitungan dikembalikan ke 0 (wadah berwarna air lagi).
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
        WaterCount = 0;
        SyrupCount = 0;
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

    // Kosongkan isi wadah saja (entri tetap)
    public void ResetMixture()
    {
        if (IsEmpty) return;
        WaterCount = 0;
        SyrupCount = 0;
        Changed?.Invoke();
    }

    // Hapus semua entri dan kosongkan wadah
    public void ResetAll()
    {
        bool hadEntries = entries.Count > 0;
        entries.Clear();
        WaterCount = 0;
        SyrupCount = 0;
        if (hadEntries) EntriesChanged?.Invoke();
        Changed?.Invoke();
    }
}