using System;
using System.Collections.Generic;

// State dan aturan Media 1 (data murni). Satu instance dimiliki controller halaman Media 1.
public sealed class Media1Session
{
    public const long PourSections = 1;    // setiap swipe memindahkan tepat 1 bagian

    public Media1Limits Limits { get; }

    public LiquidContainer A { get; } = new LiquidContainer("Gelas A (air)", ContainerRole.Give);
    public LiquidContainer B { get; } = new LiquidContainer("Gelas B (sirup)", ContainerRole.Give);
    public LiquidContainer W { get; } = new LiquidContainer("Wadah", ContainerRole.Give | ContainerRole.Receive);

    readonly List<LiquidContainer> finalCups = new List<LiquidContainer>();
    public IReadOnlyList<LiquidContainer> FinalCups => finalCups;

    public Media1Config? Config { get; private set; }
    public bool IsConfigured => Config.HasValue;
    public int PourCount { get; private set; }
    public int PredictionAttempts { get; private set; }

    public event Action Changed;             // apa pun berubah
    public event Action FinalCupsChanged;    // gelas akhir muncul atau hilang (tampilan membuat atau membuang objeknya)

    public Media1Session(Media1Limits limits = null) { Limits = limits ?? new Media1Limits(); }

    // true = ENTER ulang akan menghapus kemajuan, jadi UI menampilkan popup dulu
    public bool NeedsConfirmBeforeReconfigure => PourCount > 0 || finalCups.Count > 0;

    // Jumlah gelas akhir yang dibutuhkan = ceil(total bagian / penyebut); 0 kalau belum dikonfigurasi
    public int RequiredCups
    {
        get
        {
            if (!Config.HasValue) return 0;
            int p = Config.Value.denominator;
            return (Config.Value.TotalSections + p - 1) / p;
        }
    }

    // Index gelas akhir pertama yang belum penuh (= Count kalau semuanya penuh atau belum ada)
    public int FirstOpenFinalCupIndex
    {
        get
        {
            for (int i = 0; i < finalCups.Count; i++)
                if (!finalCups[i].IsFull) return i;
            return finalCups.Count;
        }
    }

    // ---- konfigurasi (tombol ENTER) ----
    public ConfigError Validate(int denominator, int waterSections, int syrupSections)
    {
        if (denominator < Limits.MinDenominator || denominator > Limits.MaxDenominator) return ConfigError.DenominatorOutOfRange;
        if (waterSections < Limits.MinNumerator || waterSections > denominator) return ConfigError.NumeratorOutOfRange;
        if (syrupSections < Limits.MinNumerator || syrupSections > denominator) return ConfigError.NumeratorOutOfRange;
        return ConfigError.None;
    }

    // Terapkan konfigurasi. Selalu mengatur ulang semuanya (UI menampilkan popup dulu kalau NeedsConfirmBeforeReconfigure).
    public ConfigError ApplyConfig(int denominator, int waterSections, int syrupSections)
    {
        ConfigError err = Validate(denominator, waterSections, syrupSections);
        if (err != ConfigError.None) return err;

        bool hadFinalCups = finalCups.Count > 0;
        finalCups.Clear();

        Config = new Media1Config(denominator, waterSections, syrupSections);
        A.Configure(denominator, Mixture.Water(waterSections));
        B.Configure(denominator, Mixture.Syrup(syrupSections));
        W.Configure(waterSections + syrupSections, Mixture.Empty);    // wadah penuh persis saat A dan B habis
        PourCount = 0;
        PredictionAttempts = 0;

        if (hadFinalCups) FinalCupsChanged?.Invoke();
        Changed?.Invoke();
        return ConfigError.None;
    }

    // Kembali ke keadaan awal (belum dikonfigurasi)
    public void ResetAll()
    {
        bool hadFinalCups = finalCups.Count > 0;
        finalCups.Clear();

        Config = null;
        A.Configure(0, Mixture.Empty);
        B.Configure(0, Mixture.Empty);
        W.Configure(0, Mixture.Empty);
        PourCount = 0;
        PredictionAttempts = 0;

        if (hadFinalCups) FinalCupsChanged?.Invoke();
        Changed?.Invoke();
    }

    // ---- tuang ----
    // Cek tanpa mengubah apa pun (dipakai saat gelas dilepas di atas target, sebelum animasi)
    public PourError CheckPour(LiquidContainer source, LiquidContainer target)
    {
        if (!IsConfigured) return PourError.NotConfigured;
        if (source == target) return PourError.SameContainer;
        if (!source.CanGive) return PourError.SourceCannotGive;
        if (!target.CanReceive) return PourError.TargetCannotReceive;

        int cupIndex = finalCups.IndexOf(target);
        if (cupIndex >= 0 && source != W) return PourError.FinalCupOnlyFromWadah;

        if (source.VolumeSections < PourSections) return PourError.SourceEmpty;
        if (target.SpaceSections < PourSections) return PourError.TargetFull;

        if (cupIndex >= 0 && cupIndex != FirstOpenFinalCupIndex) return PourError.FinalCupOutOfOrder;
        return PourError.None;
    }

    // Pindahkan tepat 1 bagian dari source ke target (campuran diambil menurut rasio isi source)
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

    // ---- prediksi jumlah gelas akhir (tombol CEK) ----
    public PredictResult TryPredict(int cups)
    {
        if (!IsConfigured) return new PredictResult(PredictError.NotConfigured, false);
        if (finalCups.Count > 0) return new PredictResult(PredictError.AlreadySpawned, false);
        if (cups < 1 || cups > Limits.MaxPredictCups) return new PredictResult(PredictError.OutOfRange, false);

        PredictionAttempts++;
        if (cups != RequiredCups)
        {
            Changed?.Invoke();
            return new PredictResult(PredictError.None, false);
        }

        int p = Config.Value.denominator;
        for (int i = 0; i < cups; i++)
            finalCups.Add(new LiquidContainer($"Gelas akhir {i + 1}", ContainerRole.Receive, p));

        FinalCupsChanged?.Invoke();
        Changed?.Invoke();
        return new PredictResult(PredictError.None, true);
    }
}