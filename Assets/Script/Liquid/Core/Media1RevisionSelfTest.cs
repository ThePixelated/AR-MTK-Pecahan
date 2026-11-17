using UnityEngine;

public class Media1RevisionSelfTest : MonoBehaviour
{
    int pass, fail;

    [ContextMenu("Run Media1 Revision Tests")]
    public void Run()
    {
        pass = fail = 0;
        TestPartition();
        TestAdd();
        TestMergeAndCounters();
        TestFinalCups();
        TestAnswerFlow();
        TestAnswerEdgeCases();
        TestFormat();
        TestCupFraction();
        TestReconfigure();
        TestLegacy();
        string summary = $"Media1 revision tests: {pass} lulus, {fail} gagal";
        if (fail == 0) Debug.Log(summary); else Debug.LogError(summary);
    }

    void Check(bool ok, string name)
    {
        if (ok) pass++;
        else { fail++; Debug.LogError("GAGAL: " + name); }
    }

    static void Add(Media1Session s, LiquidKind kind, int n)
    {
        for (int i = 0; i < n; i++) s.TryAdd(kind);
    }

    // Menuang seluruh isi wadah ke gelas hasil berurutan, satu bagian per swipe
    static void PourWadahToCups(Media1Session s)
    {
        while (!s.W.IsEmpty)
        {
            int idx = s.FirstOpenFinalCupIndex;
            if (idx >= s.FinalCups.Count) break;
            if (s.TryPour(s.W, s.FinalCups[idx]) != PourError.None) break;
        }
    }

    void TestPartition()
    {
        var s = new Media1Session();
        Check(!s.IsConfigured && s.Denominator == 0 && !s.HasContent && !s.HasLiquid && s.RequiredCups == 0, "awal: belum ada partisi");
        Check(s.SetPartition(s.Limits.MaxDenominator + 1) == ConfigError.DenominatorOutOfRange && !s.IsConfigured, "partisi di atas batas ditolak");
        Check(s.SetPartition(s.Limits.MinDenominator - 1) == ConfigError.DenominatorOutOfRange, "partisi di bawah batas ditolak");

        Check(s.SetPartition(4) == ConfigError.None && s.IsConfigured && s.Denominator == 4, "partisi 4 diterima");
        Check(s.A.CapacitySections == 4 && s.B.CapacitySections == 4 && s.W.CapacitySections == 8, "gelas p bagian, wadah 2p bagian");
        Check(s.A.IsEmpty && s.B.IsEmpty && s.W.IsEmpty && !s.HasContent && s.TotalLiquid == 0, "semuanya kosong");
        Check(!s.NeedsConfirmBeforeAdd && s.WaterPoured == 0 && s.SyrupPoured == 0, "kosong: tidak perlu konfirmasi, belum ada yang dituang");
    }

    void TestAdd()
    {
        var s = new Media1Session();
        s.SetPartition(4);
        int events = 0;
        s.Changed += () => events++;

        Check(s.CheckAdd(LiquidKind.Water) == PourError.None && s.TryAdd(LiquidKind.Water) == PourError.None, "air +1");
        Check(s.A.VolumeSections == 1 && s.B.VolumeSections == 0 && events == 1, "air masuk gelas A, satu Changed");
        Check(s.HasLiquid && s.TotalLiquid == 1, "ada cairan");
        Check(s.TryAdd(LiquidKind.Syrup) == PourError.None && s.B.VolumeSections == 1, "sirup masuk gelas B");
        Add(s, LiquidKind.Water, 3);
        Check(s.A.IsFull, "gelas A penuh (4/4)");
        Check(s.TryAdd(LiquidKind.Water) == PourError.TargetFull && s.A.VolumeSections == 4, "air ke-5 ditolak");
        Check(!s.NeedsConfirmBeforeAdd, "A dan B terisi tapi wadah kosong: tidak perlu konfirmasi");
        Check(new Media1Session().TryAdd(LiquidKind.Water) == PourError.NotConfigured, "tanpa partisi: ditolak");
    }

    void TestMergeAndCounters()
    {
        var s = new Media1Session();
        s.SetPartition(4);
        Add(s, LiquidKind.Water, 3);
        Add(s, LiquidKind.Syrup, 4);
        Check(s.RequiredCups == 2, "7 bagian dari partisi 4: butuh 2 gelas hasil");

        Check(s.CheckPourAll(s.W) == PourError.SourceCannotGive, "wadah bukan sumber Gabungkan");
        Check(s.TryPourAll(s.A) == PourError.None && s.A.IsEmpty && s.W.VolumeSections == 3, "gelas A pindah semuanya ke wadah");
        Check(s.WaterPoured == 3 && s.SyrupPoured == 0, "hitungan air dituang = 3");
        Check(s.NeedsConfirmBeforeAdd, "wadah berisi: menambah larutan perlu konfirmasi");
        Check(s.TryPourAll(s.B) == PourError.None && s.W.VolumeSections == 7, "gelas B menyusul");
        Check(s.WaterPoured == 3 && s.SyrupPoured == 4, "hitungan sirup dituang = 4");
        Check(s.TotalLiquid == 7 && s.W.Content.Concentration.ValueEquals(new Fraction(4, 7)), "total 7, konsentrasi sirup 4/7");

        // tuang manual 1 bagian dari A ke wadah ikut dihitung
        var m = new Media1Session();
        m.SetPartition(4);
        Add(m, LiquidKind.Water, 2);
        Check(m.TryPour(m.A, m.W) == PourError.None && m.WaterPoured == 1 && m.SyrupPoured == 0, "tuang manual air 1 bagian: terhitung");
        Check(m.CheckPourAll(m.A) == PourError.None && m.TryPourAll(m.A) == PourError.None && m.WaterPoured == 2, "sisanya digabungkan: terhitung");

        // tuang dari wadah ke gelas hasil tidak menambah hitungan
        var f = new Media1Session();
        f.SetPartition(4);
        Add(f, LiquidKind.Water, 2);
        f.TryPourAll(f.A);
        f.TryAddCup();
        f.TryPour(f.W, f.FinalCups[0]);
        Check(f.WaterPoured == 2 && f.SyrupPoured == 0, "wadah ke gelas hasil: hitungan tetap");
    }

    void TestFinalCups()
    {
        var s = new Media1Session();
        Check(s.CheckAddCup() == Media1AddCupResult.NotConfigured && s.TryAddCup() == Media1AddCupResult.NotConfigured, "tanpa partisi: tidak bisa tambah gelas");

        s.SetPartition(4);
        int cupEvents = 0;
        s.FinalCupsChanged += () => cupEvents++;

        Check(s.TryAddCup() == Media1AddCupResult.Added && s.FinalCups.Count == 1 && cupEvents == 1, "gelas hasil pertama ditambah");
        Check(s.FinalCups[0].CapacitySections == 4 && s.FinalCups[0].IsEmpty, "gelas hasil kosong berkapasitas partisi");
        Check(s.HasContent && !s.HasLiquid && !s.NeedsConfirmBeforeAdd, "gelas kosong: ada isi untuk Ulangi, tapi tanpa cairan dan tanpa konfirmasi");

        while (s.TryAddCup() == Media1AddCupResult.Added) { }
        Check(s.FinalCups.Count == s.MaxFinalCups && s.MaxFinalCups == 5, "maksimal 5 gelas hasil");
        Check(s.CheckAddCup() == Media1AddCupResult.MaxReached && s.TryAddCup() == Media1AddCupResult.MaxReached && s.FinalCups.Count == 5, "gelas ke-6 ditolak");

        // urutan pengisian
        var o = new Media1Session();
        o.SetPartition(4);
        Add(o, LiquidKind.Water, 4);
        o.TryPourAll(o.A);
        o.TryAddCup(); o.TryAddCup();
        Check(o.CheckPour(o.W, o.FinalCups[1]) == PourError.FinalCupOutOfOrder, "gelas hasil kedua belum boleh diisi duluan");
        Check(o.TryPour(o.W, o.FinalCups[0]) == PourError.None && o.FinalCups[0].VolumeSections == 1, "gelas hasil pertama diisi dulu");
        Check(o.CheckPour(o.A, o.FinalCups[0]) == PourError.FinalCupOnlyFromWadah, "gelas hasil hanya dari wadah");
        Check(o.NeedsConfirmBeforeAdd, "gelas hasil berisi: menambah larutan perlu konfirmasi");

        // ubah partisi menghapus gelas hasil
        int before = cupEvents;
        s.SetPartition(5);
        Check(s.FinalCups.Count == 0 && cupEvents == before + 1, "ubah partisi: gelas hasil dihapus lewat event");
    }

    // Soal contoh: air 3 + sirup 4 pada partisi 4 = 7/4 = 1 3/4
    static Media1Session Scenario(int cups)
    {
        var s = new Media1Session();
        s.SetPartition(4);
        Add(s, LiquidKind.Water, 3);
        Add(s, LiquidKind.Syrup, 4);
        s.TryPourAll(s.A);
        s.TryPourAll(s.B);
        for (int i = 0; i < cups; i++) s.TryAddCup();
        return s;
    }

    void TestAnswerFlow()
    {
        var s = new Media1Session();
        s.SetPartition(4);
        Add(s, LiquidKind.Water, 3);
        Add(s, LiquidKind.Syrup, 4);
        Check(s.CheckAnswer(new Fraction(7, 4)) == Media1CheckOutcome.NotAllPoured, "cairan masih di gelas A dan B: belum bisa dicek");
        s.TryPourAll(s.A); s.TryPourAll(s.B);
        Check(!s.AllPoured && s.CheckAnswer(new Fraction(7, 4)) == Media1CheckOutcome.NotAllPoured, "cairan masih di wadah: belum bisa dicek");

        s.TryAddCup(); s.TryAddCup();
        PourWadahToCups(s);
        Check(s.W.IsEmpty && s.FinalCups[0].IsFull && s.FinalCups[1].VolumeSections == 3, "wadah habis: gelas 1 penuh (4), gelas 2 berisi 3");
        Check(s.AllPoured && s.FinalCupsLiquid == 7, "semua cairan di gelas hasil");
        Check(s.TotalValue.ValueEquals(new Fraction(7, 4)), "total bernilai 7/4");

        Check(s.CheckAnswer(new Fraction(7, 4)) == Media1CheckOutcome.Correct, "jawaban 7/4 benar");
        Check(s.CheckAnswer(new Fraction(1 * 4 + 3, 4)) == Media1CheckOutcome.Correct, "jawaban 1 3/4 (bilangan campuran) benar");
        Check(s.CheckAnswer(new Fraction(14, 8)) == Media1CheckOutcome.Correct, "jawaban senilai (14/8) benar");
        Check(s.CheckAnswer(new Fraction(3, 2)) == Media1CheckOutcome.Wrong, "jawaban 3/2 salah");
        Check(s.CheckAnswer(new Fraction(6, 4)) == Media1CheckOutcome.Wrong && s.CheckAnswer(new Fraction(2, 1)) == Media1CheckOutcome.Wrong, "6/4 dan 2 salah");

        // gelas hasil tidak cukup: tidak semua cairan bisa dituang
        var few = Scenario(1);
        PourWadahToCups(few);
        Check(!few.W.IsEmpty && few.FinalCups[0].IsFull && !few.AllPoured, "satu gelas hasil: wadah masih berisi");
        Check(few.CheckAnswer(new Fraction(7, 4)) == Media1CheckOutcome.NotAllPoured, "gelas kurang: belum bisa dicek");
        few.TryAddCup();
        PourWadahToCups(few);
        Check(few.AllPoured && few.CheckAnswer(new Fraction(7, 4)) == Media1CheckOutcome.Correct, "setelah gelas ditambah dan dituang: benar");

        // dua gelas penuh persis
        var exact = new Media1Session();
        exact.SetPartition(3);
        Add(exact, LiquidKind.Water, 3); Add(exact, LiquidKind.Syrup, 3);
        exact.TryPourAll(exact.A); exact.TryPourAll(exact.B);
        exact.TryAddCup(); exact.TryAddCup();
        PourWadahToCups(exact);
        Check(exact.AllPoured && exact.CheckAnswer(new Fraction(2, 1)) == Media1CheckOutcome.Correct, "6/3 = 2 gelas utuh: jawaban 2 benar");
        Check(exact.CheckAnswer(new Fraction(6, 3)) == Media1CheckOutcome.Correct && exact.CheckAnswer(new Fraction(1, 1)) == Media1CheckOutcome.Wrong, "6/3 benar, 1 salah");
    }

    void TestAnswerEdgeCases()
    {
        var s = new Media1Session();
        Check(s.CheckAnswer(new Fraction(1, 1)) == Media1CheckOutcome.NotConfigured, "tanpa partisi: tidak bisa dicek");
        s.SetPartition(4);
        Check(s.CheckAnswer(new Fraction(0, 1)) == Media1CheckOutcome.NothingToCheck, "belum ada cairan: tidak ada yang dicek");
        Check(!s.AllPoured && s.TotalValue.ValueEquals(Fraction.Zero), "tanpa cairan: AllPoured false, nilai 0");

        // cairan sedikit: 1 bagian air
        Add(s, LiquidKind.Water, 1);
        s.TryPourAll(s.A);
        s.TryAddCup();
        PourWadahToCups(s);
        Check(s.AllPoured && s.CheckAnswer(new Fraction(1, 4)) == Media1CheckOutcome.Correct, "1/4 saja: benar");
        Check(s.CheckAnswer(new Fraction(2, 4)) == Media1CheckOutcome.Wrong, "2/4 salah");

        // menambah cairan lagi membuat jawaban lama tidak berlaku
        var t = new Media1Session();
        t.SetPartition(4);
        Add(t, LiquidKind.Water, 1);
        t.TryPourAll(t.A); t.TryAddCup(); PourWadahToCups(t);
        Add(t, LiquidKind.Syrup, 1);
        Check(!t.AllPoured && t.CheckAnswer(new Fraction(1, 4)) == Media1CheckOutcome.NotAllPoured, "cairan baru di gelas B: belum semua dituang");
    }

    void TestFormat()
    {
        Check(Media1Format.CupText(3, 4) == "3/4", "3 dari 4 = 3/4");
        Check(Media1Format.CupText(5, 4) == "1 1/4", "5 dari 4 = 1 1/4");
        Check(Media1Format.CupText(8, 4) == "2" && Media1Format.CupText(4, 4) == "1", "kelipatan penyebut = bilangan bulat");
        Check(Media1Format.CupText(2, 4) == "2/4", "tidak disederhanakan (2/4)");
        Check(Media1Format.CupText(0, 4) == "0", "kosong = 0");
        Check(Media1Format.CupText(3, 0) == "-", "belum ada partisi = -");
        Check(Media1Format.CupText(7, 3) == "2 1/3", "7 dari 3 = 2 1/3");
    }

    void TestCupFraction()
    {
        Check(Media1Format.CupFraction(3, 4) == "3/4", "info: 3 dari 4 = 3/4");
        Check(Media1Format.CupFraction(5, 5) == "5/5" && Media1Format.CupFraction(4, 4) == "4/4", "info: bilangan bulat tetap pecahan (5/5, 4/4)");
        Check(Media1Format.CupFraction(0, 4) == "0/4", "info: kosong = 0/4");
        Check(Media1Format.CupFraction(5, 4) == "5/4", "info: tak sejati tetap n/p");
        Check(Media1Format.CupFraction(5, 4, true) == "1 1/4", "info campuran: 5/4 = 1 1/4");
        Check(Media1Format.CupFraction(8, 4, true) == "8/4" && Media1Format.CupFraction(4, 4, true) == "4/4", "info campuran: kelipatan penyebut tetap pecahan");
        Check(Media1Format.CupFraction(3, 0) == "-" && Media1Format.CupFraction(-2, 4) == "0/4", "info: tanpa partisi '-', negatif dianggap 0");
    }

    void TestReconfigure()
    {
        var s = Scenario(2);
        PourWadahToCups(s);
        Check(s.WaterPoured == 3 && s.SyrupPoured == 4 && s.FinalCups.Count == 2 && s.HasLiquid, "keadaan penuh sebelum diatur ulang");

        int cupEvents = 0;
        s.FinalCupsChanged += () => cupEvents++;
        Check(s.SetPartition(6) == ConfigError.None && s.Denominator == 6, "ubah partisi ke 6");
        Check(s.A.IsEmpty && s.B.IsEmpty && s.W.IsEmpty && s.FinalCups.Count == 0 && !s.HasContent, "semuanya direset");
        Check(s.A.CapacitySections == 6 && s.W.CapacitySections == 12 && s.PredictionAttempts == 0 && cupEvents == 1, "kapasitas baru, gelas hasil dihapus lewat event");
        Check(s.WaterPoured == 0 && s.SyrupPoured == 0, "hitungan dituang direset");

        Add(s, LiquidKind.Syrup, 3);
        s.ResetContent();
        Check(s.Denominator == 6 && !s.HasContent && s.B.IsEmpty, "ResetContent: partisi tetap, isi kosong");
    }

    void TestLegacy()
    {
        // API lama (dipakai Run Liquid Tests) tetap bekerja
        var s = new Media1Session();
        Check(s.ApplyConfig(4, 2, 3) == ConfigError.None, "ApplyConfig lama diterima");
        Check(s.A.VolumeSections == 2 && s.B.VolumeSections == 3 && s.W.CapacitySections == 5, "gelas terisi sesuai konfigurasi, wadah berkapasitas total");
        Check(s.RequiredCups == 2, "RequiredCups: 5/4 -> 2");
        Check(s.TryPour(s.A, s.W) == PourError.None && s.RequiredCups == 2, "tuang 1 bagian: total tetap");

        // prediksi lama masih berfungsi
        var p = new Media1Session();
        p.SetPartition(4);
        Add(p, LiquidKind.Water, 2); Add(p, LiquidKind.Syrup, 3);
        Check(p.TryPredict(1).error == PredictError.None && !p.TryPredict(1).correct && p.FinalCups.Count == 0, "prediksi salah: belum ada gelas");
        PredictResult ok = p.TryPredict(2);
        Check(ok.correct && p.FinalCups.Count == 2 && p.TryPredict(2).error == PredictError.AlreadySpawned, "prediksi benar: dua gelas muncul");
    }
}