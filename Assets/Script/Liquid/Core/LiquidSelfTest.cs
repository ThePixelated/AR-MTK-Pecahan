using UnityEngine;

public class LiquidSelfTest : MonoBehaviour
{
    int passed, failed;

    void Check(bool ok, string name)
    {
        if (ok) { passed++; return; }
        failed++;
        Debug.LogError($"[LiquidTest] GAGAL: {name}");
    }

    [ContextMenu("Run Liquid Tests")]
    public void Run()
    {
        passed = failed = 0;
        TestMath();
        TestMixture();
        TestConfig();
        TestPour();
        TestPredict();
        TestFinalCups();
        TestColor();
        TestConservation();
        TestReset();
        Debug.Log(failed == 0 ? $"[LiquidTest] SEMUA LOLOS ({passed} cek)" : $"[LiquidTest] {failed} GAGAL dari {passed + failed} cek");
    }

    static Media1Session Configured(int p, int water, int syrup)
    {
        var s = new Media1Session();
        s.ApplyConfig(p, water, syrup);
        return s;
    }

    static Mixture M(long syrup, long water) => new Mixture(Fraction.Whole(syrup), Fraction.Whole(water));

    void TestMath()
    {
        Check(LiquidMath.Mul(new Fraction(2, 3), new Fraction(3, 4)).ValueEquals(new Fraction(1, 2)), "2/3 x 3/4 = 1/2");
        Check(LiquidMath.Div(new Fraction(1, 2), new Fraction(1, 4)).ValueEquals(Fraction.Whole(2)), "1/2 : 1/4 = 2");
        Check(LiquidMath.Mul(Fraction.Zero, new Fraction(5, 7)).numerator == 0, "0 x pecahan = 0");

        bool threw = false;
        try { LiquidMath.Div(Fraction.Whole(1), Fraction.Zero); }
        catch (System.DivideByZeroException) { threw = true; }
        Check(threw, "bagi 0 melempar exception");
    }

    void TestMixture()
    {
        var m = M(3, 2);
        Check(m.Total.ValueEquals(Fraction.Whole(5)), "total 3 sirup + 2 air = 5");
        Check(m.Concentration.ValueEquals(new Fraction(3, 5)), "konsentrasi 3/5");

        Mixture taken = m.Take(1);
        Check(taken.syrup.ValueEquals(new Fraction(3, 5)) && taken.water.ValueEquals(new Fraction(2, 5)), "ambil 1 bagian = 3/5 sirup + 2/5 air");

        Mixture rest = m - taken;
        Check(rest.Total.ValueEquals(Fraction.Whole(4)) && rest.Concentration.ValueEquals(new Fraction(3, 5)), "sisa 4 bagian, rasio tetap 3/5");
        Check(Mixture.Empty.IsEmpty && Mixture.Empty.Concentration.numerator == 0, "campuran kosong");
    }

    void TestConfig()
    {
        var s = new Media1Session();
        Check(!s.IsConfigured && !s.NeedsConfirmBeforeReconfigure, "awal: belum dikonfigurasi, tanpa popup");
        Check(s.Validate(1, 1, 1) == ConfigError.DenominatorOutOfRange, "penyebut 1 ditolak");
        Check(s.Validate(11, 1, 1) == ConfigError.DenominatorOutOfRange, "penyebut 11 ditolak (batas 10)");
        Check(s.Validate(6, 0, 3) == ConfigError.NumeratorOutOfRange, "pembilang 0 ditolak");
        Check(s.Validate(6, 7, 3) == ConfigError.NumeratorOutOfRange, "pembilang lebih besar dari penyebut ditolak");
        Check(s.ApplyConfig(6, 3, 5) == ConfigError.None, "konfigurasi valid diterima");

        Check(s.A.VolumeSections == 3 && s.B.VolumeSections == 5 && s.W.VolumeSections == 0, "gelas terisi, wadah kosong");
        Check(s.A.CapacitySections == 6 && s.B.CapacitySections == 6 && s.W.CapacitySections == 8, "kapasitas: gelas = penyebut, wadah = total bagian");
        Check(s.A.Content.syrup.numerator == 0 && s.A.Content.water.ValueEquals(Fraction.Whole(3)), "A = air bening");
        Check(s.B.Content.water.numerator == 0 && s.B.Content.syrup.ValueEquals(Fraction.Whole(5)), "B = sirup");
        Check(!s.NeedsConfirmBeforeReconfigure, "belum ada tuang: ENTER ulang tanpa popup");
        Check(s.RequiredCups == 2, "3/6 + 5/6 = 8/6 butuh 2 gelas");
        Check(Configured(6, 3, 3).RequiredCups == 1 && Configured(6, 2, 1).RequiredCups == 1, "tepat 1 utuh atau kurang = 1 gelas");
    }

    void TestPour()
    {
        Check(new Media1Session().CheckPour(new LiquidContainer("x", ContainerRole.Give), new LiquidContainer("y", ContainerRole.Receive)) == PourError.NotConfigured,
              "belum dikonfigurasi: tuang ditolak");

        var s = Configured(6, 3, 5);
        Check(s.TryPour(s.B, s.W) == PourError.None, "B ke W diterima");
        Check(s.B.VolumeSections == 4 && s.W.VolumeSections == 1, "B berkurang 1, W bertambah 1");
        Check(s.W.Content.syrup.ValueEquals(Fraction.Whole(1)) && s.W.Content.water.numerator == 0, "W berisi 1 bagian sirup");
        Check(s.PourCount == 1 && s.NeedsConfirmBeforeReconfigure, "sudah ada tuang: ENTER ulang perlu popup");

        Check(s.CheckPour(s.W, s.A) == PourError.TargetCannotReceive, "W ke gelas A ditolak");
        Check(s.CheckPour(s.A, s.B) == PourError.TargetCannotReceive, "A ke B ditolak");
        Check(s.CheckPour(s.W, s.W) == PourError.SameContainer, "ke diri sendiri ditolak");

        while (s.TryPour(s.A, s.W) == PourError.None) { }
        while (s.TryPour(s.B, s.W) == PourError.None) { }
        Check(s.A.IsEmpty && s.B.IsEmpty && s.W.VolumeSections == 8 && s.W.IsFull, "semua pindah ke W, W penuh persis");
        Check(s.CheckPour(s.A, s.W) == PourError.SourceEmpty, "sumber kosong ditolak");
    }

    void TestPredict()
    {
        var s = Configured(6, 3, 5);
        Check(s.TryPredict(0).error == PredictError.OutOfRange, "prediksi 0 di luar rentang");
        Check(s.TryPredict(6).error == PredictError.OutOfRange, "prediksi 6 di luar rentang (batas 5)");

        PredictResult one = s.TryPredict(1);
        Check(one.error == PredictError.None && !one.correct && s.FinalCups.Count == 0, "prediksi 1 salah: tidak ada gelas akhir");
        PredictResult three = s.TryPredict(3);
        Check(!three.correct && s.FinalCups.Count == 0 && s.PredictionAttempts == 2, "prediksi 3 salah, percobaan tercatat");

        int spawned = 0;
        s.FinalCupsChanged += () => spawned++;
        PredictResult ok = s.TryPredict(2);
        Check(ok.correct && s.FinalCups.Count == 2 && spawned == 1, "prediksi 2 benar: 2 gelas akhir muncul");
        Check(s.FinalCups[0].CapacitySections == 6 && s.FinalCups[0].CanReceive && !s.FinalCups[0].CanGive, "gelas akhir: hanya menerima, kapasitas 1 utuh");
        Check(s.TryPredict(2).error == PredictError.AlreadySpawned, "setelah muncul, tidak bisa prediksi ulang");
        Check(new Media1Session().TryPredict(1).error == PredictError.NotConfigured, "belum dikonfigurasi: prediksi ditolak");
    }

    void TestFinalCups()
    {
        var s = Configured(6, 3, 5);
        s.TryPredict(2);

        Check(s.CheckPour(s.A, s.FinalCups[0]) == PourError.FinalCupOnlyFromWadah, "gelas akhir hanya dari wadah");
        Check(s.CheckPour(s.W, s.FinalCups[0]) == PourError.SourceEmpty, "wadah masih kosong: belum bisa");

        while (s.TryPour(s.A, s.W) == PourError.None) { }
        while (s.TryPour(s.B, s.W) == PourError.None) { }
        Check(s.CheckPour(s.W, s.FinalCups[1]) == PourError.FinalCupOutOfOrder, "gelas akhir 2 belum boleh selama gelas 1 belum penuh");

        bool allOk = true;
        for (int i = 0; i < 6; i++) allOk &= s.TryPour(s.W, s.FinalCups[0]) == PourError.None;
        Check(allOk && s.FinalCups[0].IsFull && s.W.VolumeSections == 2, "gelas akhir 1 terisi 6 bagian dan penuh, W sisa 2");
        Check(s.CheckPour(s.W, s.FinalCups[0]) == PourError.TargetFull, "gelas akhir 1 penuh ditolak");

        Check(s.TryPour(s.W, s.FinalCups[1]) == PourError.None && s.TryPour(s.W, s.FinalCups[1]) == PourError.None, "lanjut ke gelas akhir 2");
        Check(s.W.IsEmpty && s.FinalCups[1].VolumeSections == 2, "W kosong, gelas akhir 2 berisi 2 bagian");
        Check(s.CheckPour(s.W, s.FinalCups[1]) == PourError.SourceEmpty, "W kosong ditolak");
    }

    void TestColor()
    {
        Check(LiquidColorLevel.Level(Mixture.Empty, 4) == 0, "kosong = tingkat 0");
        Check(LiquidColorLevel.Level(M(0, 5), 4) == 0, "air bening = tingkat 0");
        Check(LiquidColorLevel.Level(M(5, 0), 4) == 4, "sirup murni = tingkat tertinggi");
        Check(LiquidColorLevel.Level(M(1, 3), 4) == 1, "1/4 sirup = tingkat 1");
        Check(LiquidColorLevel.Level(M(1, 1), 4) == 2, "1/2 sirup = tingkat 2");
        Check(LiquidColorLevel.Level(M(3, 1), 4) == 3, "3/4 sirup = tingkat 3");

        int a = LiquidColorLevel.Level(M(1, 2), 4);
        Check(a == LiquidColorLevel.Level(M(2, 4), 4) && a == LiquidColorLevel.Level(M(3, 6), 4), "perbandingan senilai 1:2 = 2:4 = 3:6 satu tingkat");
        Check(LiquidColorLevel.Level(M(1, 3), 4) != LiquidColorLevel.Level(M(1, 1), 4), "perbandingan berbeda jauh, tingkat berbeda");
    }

    void TestConservation()
    {
        var s = Configured(6, 3, 5);
        s.TryPredict(2);

        Mixture Sum() => s.A.Content + s.B.Content + s.W.Content + s.FinalCups[0].Content + s.FinalCups[1].Content;
        Mixture start = Sum();

        s.TryPour(s.B, s.W);
        s.TryPour(s.B, s.W);
        s.TryPour(s.W, s.FinalCups[0]);    // gelas akhir 1: 1 bagian sirup
        s.TryPour(s.A, s.W);
        Check(s.W.Content.syrup.ValueEquals(Fraction.Whole(1)) && s.W.Content.water.ValueEquals(Fraction.Whole(1)), "W: 1 sirup + 1 air");

        s.TryPour(s.W, s.FinalCups[0]);    // memindahkan 1 bagian campuran 1:1
        Check(s.FinalCups[0].Content.syrup.ValueEquals(new Fraction(3, 2)) && s.FinalCups[0].Content.water.ValueEquals(new Fraction(1, 2)),
              "gelas akhir 1: 3/2 sirup + 1/2 air");
        Check(s.FinalCups[0].VolumeSections == 2, "volume tetap bulat (2 bagian)");
        Check(Sum().ValueEquals(start), "jumlah sirup dan air di semua wadah tidak berubah (kekekalan)");
    }

    void TestReset()
    {
        var s = Configured(6, 3, 5);
        s.TryPour(s.A, s.W);
        s.TryPredict(2);
        Check(s.NeedsConfirmBeforeReconfigure && s.FinalCups.Count == 2, "ada tuang dan gelas akhir: perlu popup");

        Check(s.ApplyConfig(4, 1, 2) == ConfigError.None, "konfigurasi baru diterima");
        Check(s.PourCount == 0 && s.FinalCups.Count == 0 && s.A.VolumeSections == 1 && s.B.VolumeSections == 2
              && s.W.CapacitySections == 3 && s.W.IsEmpty && !s.NeedsConfirmBeforeReconfigure,
              "ApplyConfig mengatur ulang semuanya");

        s.ResetAll();
        Check(!s.IsConfigured && s.A.IsEmpty && s.B.IsEmpty && s.A.CapacitySections == 0, "ResetAll kembali ke keadaan awal");
        Check(s.CheckPour(s.A, s.W) == PourError.NotConfigured, "setelah ResetAll tuang ditolak lagi");
    }
}