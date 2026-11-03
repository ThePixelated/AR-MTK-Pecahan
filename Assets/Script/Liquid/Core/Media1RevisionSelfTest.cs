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
        TestMergeAndPredict();
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

    void TestPartition()
    {
        var s = new Media1Session();
        Check(!s.IsConfigured && s.Denominator == 0 && !s.HasContent && s.RequiredCups == 0, "awal: belum ada partisi");
        Check(s.SetPartition(s.Limits.MaxDenominator + 1) == ConfigError.DenominatorOutOfRange && !s.IsConfigured, "partisi di atas batas ditolak");
        Check(s.SetPartition(s.Limits.MinDenominator - 1) == ConfigError.DenominatorOutOfRange, "partisi di bawah batas ditolak");

        Check(s.SetPartition(4) == ConfigError.None && s.IsConfigured && s.Denominator == 4, "partisi 4 diterima");
        Check(s.A.CapacitySections == 4 && s.B.CapacitySections == 4 && s.W.CapacitySections == 8, "gelas p bagian, wadah 2p bagian");
        Check(s.A.IsEmpty && s.B.IsEmpty && s.W.IsEmpty && !s.HasContent && s.TotalLiquid == 0, "semuanya kosong");
        Check(!s.NeedsConfirmBeforeAdd, "kosong: tidak perlu konfirmasi");
    }

    void TestAdd()
    {
        var s = new Media1Session();
        s.SetPartition(4);

        int events = 0;
        s.Changed += () => events++;

        Check(s.CheckAdd(LiquidKind.Water) == PourError.None && s.TryAdd(LiquidKind.Water) == PourError.None, "air +1");
        Check(s.A.VolumeSections == 1 && s.B.VolumeSections == 0 && events == 1, "air masuk gelas A, satu Changed");
        Check(s.HasContent && s.TotalLiquid == 1, "ada isi");

        Check(s.TryAdd(LiquidKind.Syrup) == PourError.None && s.B.VolumeSections == 1, "sirup masuk gelas B");
        Add(s, LiquidKind.Water, 3);
        Check(s.A.VolumeSections == 4 && s.A.IsFull, "gelas A penuh (4/4)");
        Check(s.CheckAdd(LiquidKind.Water) == PourError.TargetFull && s.TryAdd(LiquidKind.Water) == PourError.TargetFull, "air ke-5 ditolak");
        Check(s.A.VolumeSections == 4 && s.CheckAdd(LiquidKind.Syrup) == PourError.None, "penolakan tidak mengubah isi; batas B terpisah");
        Check(!s.NeedsConfirmBeforeAdd, "A dan B terisi tapi wadah kosong: tidak perlu konfirmasi");

        Check(new Media1Session().TryAdd(LiquidKind.Water) == PourError.NotConfigured, "tanpa partisi: ditolak");
    }

    void TestMergeAndPredict()
    {
        var s = new Media1Session();
        s.SetPartition(4);
        Add(s, LiquidKind.Water, 2);
        Add(s, LiquidKind.Syrup, 3);
        Check(s.TotalLiquid == 5 && s.RequiredCups == 2, "5/4 -> butuh 2 gelas akhir");

        Check(s.CheckPourAll(s.W) == PourError.SourceCannotGive, "wadah bukan sumber Gabungkan");
        Check(s.TryPourAll(s.A) == PourError.None && s.A.IsEmpty && s.W.VolumeSections == 2, "gelas A pindah semuanya ke wadah");
        Check(s.NeedsConfirmBeforeAdd, "wadah berisi: menambah larutan perlu konfirmasi");
        Check(s.CheckPourAll(s.A) == PourError.SourceEmpty, "gelas kosong tidak bisa dituang lagi");
        Check(s.TryPourAll(s.B) == PourError.None && s.B.IsEmpty && s.W.VolumeSections == 5, "gelas B menyusul");
        Check(s.W.Content.Concentration.ValueEquals(new Fraction(3, 5)), "konsentrasi sirup di wadah 3/5");
        Check(s.RequiredCups == 2 && s.TotalLiquid == 5, "total tetap 5 setelah dipindah");

        Check(s.TryPredict(0).error == PredictError.OutOfRange, "prediksi 0 di luar rentang");
        PredictResult wrong = s.TryPredict(1);
        Check(wrong.error == PredictError.None && !wrong.correct && s.FinalCups.Count == 0, "prediksi 1: salah, belum ada gelas akhir");
        PredictResult right = s.TryPredict(2);
        Check(right.error == PredictError.None && right.correct && s.FinalCups.Count == 2 && s.FinalCups[0].CapacitySections == 4, "prediksi 2: benar, dua gelas akhir 4 bagian");
        Check(s.TryPredict(2).error == PredictError.AlreadySpawned, "gelas akhir sudah muncul");
        Check(s.PredictionAttempts == 2, "dua percobaan prediksi tercatat");

        // wadah penuh persis saat A dan B penuh
        var f = new Media1Session();
        f.SetPartition(4);
        Add(f, LiquidKind.Water, 4);
        Add(f, LiquidKind.Syrup, 4);
        Check(f.TryPourAll(f.A) == PourError.None && f.TryPourAll(f.B) == PourError.None && f.W.IsFull && f.RequiredCups == 2, "A dan B penuh: wadah 8/8 dan butuh 2 gelas");

        // tuang manual satu bagian tetap jalan di alur baru
        var m = new Media1Session();
        m.SetPartition(4);
        Add(m, LiquidKind.Water, 2);
        Check(m.TryPour(m.A, m.W) == PourError.None && m.A.VolumeSections == 1 && m.W.VolumeSections == 1, "tuang manual 1 bagian A ke wadah");
        Check(m.CheckPourAll(m.A) == PourError.None && m.TryPourAll(m.A) == PourError.None && m.A.IsEmpty && m.W.VolumeSections == 2, "sisanya digabungkan");
    }

    void TestReconfigure()
    {
        var s = new Media1Session();
        s.SetPartition(4);
        Add(s, LiquidKind.Water, 2);
        Add(s, LiquidKind.Syrup, 2);
        s.TryPourAll(s.A);
        s.TryPourAll(s.B);
        s.TryPredict(1);
        s.TryPredict(1);

        int finalEvents = 0;
        s.FinalCupsChanged += () => finalEvents++;
        s.TryPredict(1);
        Check(s.FinalCups.Count == 1, "dua bagian air, dua bagian sirup: 4/4 dalam satu gelas akhir");
        Check(s.HasContent && s.NeedsConfirmBeforeAdd, "gelas akhir ada: perlu konfirmasi");

        Check(s.SetPartition(6) == ConfigError.None && s.Denominator == 6, "ubah partisi ke 6");
        Check(s.A.IsEmpty && s.B.IsEmpty && s.W.IsEmpty && s.FinalCups.Count == 0 && !s.HasContent, "semuanya direset");
        Check(s.A.CapacitySections == 6 && s.W.CapacitySections == 12 && s.PredictionAttempts == 0 && finalEvents == 1, "kapasitas baru, percobaan direset, gelas akhir dihapus lewat event");

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
    }
}