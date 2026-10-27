using System;
using System.Collections.Generic;

// State dan aturan Media 3 (data murni, tanpa Unity). Satu instance dimiliki controller halaman Media 3.
public sealed class Media3Session : IPourSession
{
    public const long PourSections = 1;    // setiap swipe memindahkan tepat 1 bagian

    public Media3Limits Limits { get; }

    public LiquidContainer Syrup { get; } = new LiquidContainer("Gelas sirup", ContainerRole.Give);
    public LiquidContainer Water { get; } = new LiquidContainer("Gelas air", ContainerRole.Give);
    public LiquidContainer W { get; } = new LiquidContainer("Wadah", ContainerRole.Receive);

    readonly List<Media3Entry> entries = new List<Media3Entry>();
    public IReadOnlyList<Media3Entry> Entries => entries;

    public Media3Config? Config { get; private set; }
    public bool IsConfigured => Config.HasValue;
    public int PourCount { get; private set; }
    public int LastDuplicateIndex { get; private set; } = -1;     // indeks entri yang kembar pada penolakan DuplicateEntry terakhir
    public bool TrialsFull => entries.Count >= Limits.MaxTrials;

    public event Action Changed;           // apa pun berubah
    public event Action EntriesChanged;    // daftar entri berubah (tambah, hapus, reset)

    public Media3Session(Media3Limits limits = null) { Limits = limits ?? new Media3Limits(); }

    // true = mengubah konfigurasi akan membuang hasil tuang, jadi UI menampilkan popup dulu
    public bool NeedsConfirmBeforeReconfigure => PourCount > 0;

    long CupCapacity => Limits.MaxPerCup;
    long WadahCapacity => 2L * Limits.MaxPerCup;    // ukuran wadah tetap; yang naik hanya level cairan

    // ---- konfigurasi (stepper Atur Campuran) ----
    public Media3ConfigError Validate(int syrupSections, int waterSections)
    {
        if (syrupSections < Limits.MinPerCup || syrupSections > Limits.MaxPerCup) return Media3ConfigError.SyrupOutOfRange;
        if (waterSections < Limits.MinPerCup || waterSections > Limits.MaxPerCup) return Media3ConfigError.WaterOutOfRange;
        return Media3ConfigError.None;
    }

    // Terapkan konfigurasi: kedua gelas diisi ulang, wadah dikosongkan. Entri tidak tersentuh.
    // UI menampilkan popup dulu kalau NeedsConfirmBeforeReconfigure.
    public Media3ConfigError ApplyConfig(int syrupSections, int waterSections)
    {
        Media3ConfigError err = Validate(syrupSections, waterSections);
        if (err != Media3ConfigError.None) return err;

        Config = new Media3Config(syrupSections, waterSections);
        RefillTrial();
        Changed?.Invoke();
        return Media3ConfigError.None;
    }

    // Gelas diisi sesuai konfigurasi aktif, wadah kosong, hitungan tuang nol
    void RefillTrial()
    {
        if (Config.HasValue)
        {
            Media3Config c = Config.Value;
            Syrup.Configure(CupCapacity, Mixture.Syrup(c.syrupSections));
            Water.Configure(CupCapacity, Mixture.Water(c.waterSections));
            W.Configure(WadahCapacity, Mixture.Empty);
        }
        else
        {
            Syrup.Configure(0, Mixture.Empty);
            Water.Configure(0, Mixture.Empty);
            W.Configure(0, Mixture.Empty);
        }
        PourCount = 0;
    }

    // ---- tuang ----
    public PourError CheckPour(LiquidContainer source, LiquidContainer target)
    {
        if (!IsConfigured) return PourError.NotConfigured;
        if (source == target) return PourError.SameContainer;
        if (!source.CanGive) return PourError.SourceCannotGive;
        if (!target.CanReceive) return PourError.TargetCannotReceive;
        if (source.VolumeSections < PourSections) return PourError.SourceEmpty;
        if (target.SpaceSections < PourSections) return PourError.TargetFull;
        return PourError.None;
    }

    // Pindahkan tepat 1 bagian dari source ke target
    public PourError TryPour(LiquidContainer source, LiquidContainer target)
    {
        PourError err = CheckPour(source, target);
        if (err != PourError.None) return err;

        Mixture moved = source.Take(PourSections);
        target.Put(moved);
        PourCount++;

        source.NotifyChanged();    // setelah keduanya berubah, supaya tampilan tidak melihat keadaan setengah jadi
        target.NotifyChanged();
        Changed?.Invoke();
        return PourError.None;
    }

    // ---- entri (tombol Input) ----
    // Mencatat isi wadah sebagai entri baru, lalu gelas diisi ulang dan wadah dikosongkan.
    // Kalau ditolak, tidak ada yang berubah (wadah dan gelas tetap).
    public Media3InputResult TryInput()
    {
        if (!IsConfigured) return Media3InputResult.NotConfigured;
        if (TrialsFull) return Media3InputResult.TrialsFull;
        if (W.IsEmpty) return Media3InputResult.NothingPoured;
        if (Limits.RequireAllPoured && (!Syrup.IsEmpty || !Water.IsEmpty)) return Media3InputResult.NotAllPoured;

        Mixture m = W.Content;
        var entry = new Media3Entry(WholeSections(m.syrup), WholeSections(m.water));

        for (int i = 0; i < entries.Count; i++)
        {
            if (!entries[i].SamePair(entry)) continue;
            LastDuplicateIndex = i;
            return Media3InputResult.DuplicateEntry;
        }

        entries.Add(entry);
        RefillTrial();
        EntriesChanged?.Invoke();
        Changed?.Invoke();
        return Media3InputResult.Added;
    }

    // Hapus satu entri (nomor entri di bawahnya bergeser). Gelas dan wadah tidak disentuh.
    public bool RemoveEntry(int index)
    {
        if (index < 0 || index >= entries.Count) return false;
        entries.RemoveAt(index);
        EntriesChanged?.Invoke();
        Changed?.Invoke();
        return true;
    }

    // Hapus semua entri, lalu gelas diisi ulang sesuai konfigurasi aktif dan wadah dikosongkan
    public void ResetAll()
    {
        bool hadEntries = entries.Count > 0;
        entries.Clear();
        RefillTrial();
        if (hadEntries) EntriesChanged?.Invoke();
        Changed?.Invoke();
    }

    // Volume wadah selalu bulat karena tiap tuang memindahkan tepat 1 bagian dari gelas yang murni
    static int WholeSections(Fraction f) => (int)(f.numerator / f.denominator);
}