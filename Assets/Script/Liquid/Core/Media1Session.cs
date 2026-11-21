using System;
using System.Collections.Generic;

public enum LiquidKind { Water, Syrup }

public enum Media1AddCupResult { Added, NotConfigured, MaxReached }

public enum Media1CheckOutcome
{
    NotConfigured,
    NothingToCheck,    // belum ada air atau sirup sama sekali
    NotAllPoured,      // masih ada cairan di gelas A, B, atau wadah: tuang semuanya ke gelas hasil dulu
    Correct,
    Wrong
}

// State dan aturan Media 1 (data murni). Satu instance dimiliki controller halaman Media 1.
// Alur: atur partisi, tarik air ke gelas A dan sirup ke gelas B (1 bagian per tarikan), Gabungkan ke wadah,
// tambah gelas hasil (satu per tekan), tuang wadah ke gelas hasil (1 bagian per swipe), lalu siswa mengisi jawabannya (pecahan campuran)
// dan menekan Cek hasil. Jawaban benar kalau nilainya sama dengan total cairan dalam satuan gelas (1 3/4 sama dengan 7/4).
public sealed class Media1Session : IPourSession
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

    // Total bagian air dan sirup yang sudah dituang ke gelas A dan B dari gambar sejak pengaturan terakhir.
    // Tetap tampil walau gelas sudah digabung ke wadah; kembali 0 hanya saat reset (dipakai panel Informasi Larutan: "Air 3/4")
    public long WaterPoured { get; private set; }
    public long SyrupPoured { get; private set; }

    public event Action Changed;             // apa pun berubah
    public event Action FinalCupsChanged;    // gelas akhir muncul atau hilang (tampilan membuat atau membuang objeknya)

    public Media1Session(Media1Limits limits = null) { Limits = limits ?? new Media1Limits(); }

    // true = ENTER ulang akan menghapus kemajuan, jadi UI menampilkan popup dulu
    public bool NeedsConfirmBeforeReconfigure => PourCount > 0 || finalCups.Count > 0;

    // Jumlah gelas akhir yang dibutuhkan = ceil(total cairan / penyebut); 0 kalau belum ada partisi
    public int RequiredCups
    {
        get
        {
            if (!Config.HasValue) return 0;
            int p = Config.Value.denominator;
            return (int)((TotalLiquid + p - 1) / p);
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

    // ---------- alur: partisi realtime, larutan ditarik per bagian ----------
    public int Denominator => Config.HasValue ? Config.Value.denominator : 0;     // partisi aktif

    // Total cairan di seluruh sistem (gelas A, B, wadah, dan gelas akhir), dalam bagian
    public long TotalLiquid
    {
        get
        {
            long sum = A.VolumeSections + B.VolumeSections + W.VolumeSections;
            foreach (LiquidContainer c in finalCups) sum += c.VolumeSections;
            return sum;
        }
    }

    public long FinalCupsLiquid
    {
        get
        {
            long sum = 0;
            foreach (LiquidContainer c in finalCups) sum += c.VolumeSections;
            return sum;
        }
    }

    // Ada cairan yang akan hilang kalau partisi diubah: UI menampilkan popup dulu
    public bool HasLiquid => TotalLiquid > 0;

    // Ada yang bisa dihapus lewat Ulangi (cairan atau gelas hasil, termasuk yang masih kosong)
    public bool HasContent => HasLiquid || finalCups.Count > 0;

    // Menambah larutan butuh konfirmasi kalau wadah sudah berisi atau gelas hasil sudah berisi (gelas hasil yang masih kosong tidak dihitung)
    public bool NeedsConfirmBeforeAdd => !W.IsEmpty || FinalCupsLiquid > 0;

    // Atur partisi: semua gelas kosong, gelas hasil dihapus. UI menampilkan popup dulu kalau HasLiquid.
    public ConfigError SetPartition(int denominator)
    {
        if (denominator < Limits.MinDenominator || denominator > Limits.MaxDenominator) return ConfigError.DenominatorOutOfRange;

        bool hadFinalCups = finalCups.Count > 0;
        finalCups.Clear();

        Config = new Media1Config(denominator, 0, 0);
        A.Configure(denominator, Mixture.Empty);
        B.Configure(denominator, Mixture.Empty);
        W.Configure(2L * denominator, Mixture.Empty);      // wadah menampung kedua gelas yang penuh
        PourCount = 0;
        PredictionAttempts = 0;
        WaterPoured = 0;
        SyrupPoured = 0;

        if (hadFinalCups) FinalCupsChanged?.Invoke();
        Changed?.Invoke();
        return ConfigError.None;
    }

    // Kosongkan semuanya dengan partisi yang sama
    public void ResetContent()
    {
        if (IsConfigured) SetPartition(Denominator);
    }

    // Satu tarikan air (ke gelas A) atau sirup (ke gelas B) = 1 bagian
    public PourError CheckAdd(LiquidKind kind)
    {
        if (!IsConfigured) return PourError.NotConfigured;
        LiquidContainer cup = kind == LiquidKind.Water ? A : B;
        return cup.SpaceSections < PourSections ? PourError.TargetFull : PourError.None;
    }

    public PourError TryAdd(LiquidKind kind)
    {
        PourError err = CheckAdd(kind);
        if (err != PourError.None) return err;

        LiquidContainer cup = kind == LiquidKind.Water ? A : B;
        cup.Put(kind == LiquidKind.Water ? Mixture.Water(1) : Mixture.Syrup(1));
        if (kind == LiquidKind.Water) WaterPoured += PourSections;
        else SyrupPoured += PourSections;
        cup.NotifyChanged();
        Changed?.Invoke();
        return PourError.None;
    }

    // ---------- gelas hasil (tombol Tambah Gelas) ----------
    public int MaxFinalCups => Limits.MaxPredictCups;

    public Media1AddCupResult CheckAddCup()
    {
        if (!IsConfigured) return Media1AddCupResult.NotConfigured;
        return finalCups.Count >= MaxFinalCups ? Media1AddCupResult.MaxReached : Media1AddCupResult.Added;
    }

    // Tambah satu gelas hasil (kosong, kapasitas = penyebut). Gelas diisi berurutan: gelas berikutnya baru boleh kalau sebelumnya penuh.
    public Media1AddCupResult TryAddCup()
    {
        Media1AddCupResult r = CheckAddCup();
        if (r != Media1AddCupResult.Added) return r;

        finalCups.Add(new LiquidContainer($"Gelas akhir {finalCups.Count + 1}", ContainerRole.Receive, Denominator));
        FinalCupsChanged?.Invoke();
        Changed?.Invoke();
        return r;
    }

    // ---------- Gabungkan: seluruh isi gelas A atau B ke wadah ----------
    public PourError CheckPourAll(LiquidContainer source)
    {
        if (!IsConfigured) return PourError.NotConfigured;
        if (source != A && source != B) return PourError.SourceCannotGive;
        if (source.VolumeSections < PourSections) return PourError.SourceEmpty;
        if (W.SpaceSections < source.VolumeSections) return PourError.TargetFull;
        return PourError.None;
    }

    public PourError TryPourAll(LiquidContainer source)
    {
        PourError err = CheckPourAll(source);
        if (err != PourError.None) return err;

        long n = source.VolumeSections;
        Mixture moved = source.Take(n);
        W.Put(moved);
        PourCount += (int)n;
        //CountPoured(source, W, n);

        source.NotifyChanged();
        W.NotifyChanged();
        Changed?.Invoke();
        return PourError.None;
    }

    //void CountPoured(LiquidContainer source, LiquidContainer target, long sections)
    //{
    //    if (target != W) return;
    //    if (source == A) WaterPoured += sections;
    //    else if (source == B) SyrupPoured += sections;
    //}

    // ---------- konfigurasi lama (tombol ENTER; tetap ada supaya tes lama berjalan) ----------
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
        WaterPoured = 0;
        SyrupPoured = 0;

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
        WaterPoured = 0;
        SyrupPoured = 0;

        if (hadFinalCups) FinalCupsChanged?.Invoke();
        Changed?.Invoke();
    }

    // ---------- tuang (1 bagian per swipe) ----------
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
        //CountPoured(source, target, PourSections);

        source.NotifyChanged();    // setelah keduanya berubah, supaya tampilan tidak melihat keadaan setengah jadi
        target.NotifyChanged();
        Changed?.Invoke();
        return PourError.None;
    }

    // ---------- jawaban siswa (tombol Cek hasil) ----------
    // Semua cairan sudah berada di gelas hasil: gelas A, B, dan wadah kosong
    public bool AllPoured => TotalLiquid > 0 && A.IsEmpty && B.IsEmpty && W.IsEmpty && FinalCupsLiquid == TotalLiquid;

    // Total cairan dalam satuan gelas (mis. 7 bagian dari partisi 4 = 7/4 = 1 3/4)
    public Fraction TotalValue => Denominator > 0 ? new Fraction(TotalLiquid, Denominator) : Fraction.Zero;

    // Jawaban dibandingkan menurut nilai: 1 3/4 dan 7/4 sama-sama benar
    public Media1CheckOutcome CheckAnswer(Fraction answer)
    {
        if (!IsConfigured) return Media1CheckOutcome.NotConfigured;
        if (TotalLiquid == 0) return Media1CheckOutcome.NothingToCheck;
        if (!AllPoured) return Media1CheckOutcome.NotAllPoured;
        return answer.ValueEquals(TotalValue) ? Media1CheckOutcome.Correct : Media1CheckOutcome.Wrong;
    }

    // ---------- prediksi jumlah gelas akhir (alur lama, tidak dipakai lagi oleh UI baru) ----------
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

// Format angka untuk panel Informasi Larutan dan Hasil (satuan gelas, penyebut = partisi, tidak disederhanakan)
public static class Media1Format
{
    // Selalu pecahan "n/p": 5/5 tetap 5/5 (bukan 1) dan 0 menjadi 0/p, supaya siswa tidak bingung.
    // mixed = true: pecahan tak sejati yang bukan bilangan bulat ditulis campuran (5/4 -> "1 1/4"). Panel Informasi Larutan memakai ini.
    public static string CupFraction(long sections, int partition, bool mixed = false)
    {
        if (partition <= 0) return "-";
        if (sections < 0) sections = 0;
        if (mixed && sections > partition && sections % partition != 0)
            return $"{sections / partition} {sections % partition}/{partition}";
        return $"{sections}/{partition}";
    }

    // 3 bagian dari p=4 -> "3/4"; 5 -> "1 1/4"; 8 -> "2"; 0 -> "0"; p belum ada -> "-"
    public static string CupText(long sections, int partition)
    {
        if (partition <= 0) return "-";
        if (sections <= 0) return "0";

        long whole = sections / partition, rem = sections % partition;
        if (rem == 0) return whole.ToString();
        return whole == 0 ? $"{rem}/{partition}" : $"{whole} {rem}/{partition}";
    }
}