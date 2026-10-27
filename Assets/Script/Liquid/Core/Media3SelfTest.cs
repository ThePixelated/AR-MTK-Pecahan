using UnityEngine;

// Tes logika inti Media 3 (tanpa scene). Klik kanan komponen > "Run Media3 Tests"
public class Media3SelfTest : MonoBehaviour
{
    int pass, fail;

    [ContextMenu("Run Media3 Tests")]
    public void Run()
    {
        pass = fail = 0;

        TestConfig();
        TestPour();
        TestInputDefault();
        TestInputStrict();
        TestPartialEntries();
        TestEquivalenceAndDuplicate();
        TestTrialLimitAndRemove();
        TestResetAndReconfigure();
        TestEntryMath();

        string summary = $"Media3 tests: {pass} lulus, {fail} gagal";
        if (fail == 0) Debug.Log(summary); else Debug.LogError(summary);
    }

    void Check(bool ok, string name)
    {
        if (ok) pass++;
        else { fail++; Debug.LogError("GAGAL: " + name); }
    }

    static void PourAll(Media3Session s, LiquidContainer src)
    {
        while (s.TryPour(src, s.W) == PourError.None) { }
    }

    // Satu percobaan lengkap: konfigurasi, tuang semua, lalu Input
    static Media3InputResult DoTrial(Media3Session s, int syrup, int water)
    {
        s.ApplyConfig(syrup, water);
        PourAll(s, s.Syrup);
        PourAll(s, s.Water);
        return s.TryInput();
    }

    void TestConfig()
    {
        var s = new Media3Session();
        Check(!s.IsConfigured, "awal: belum dikonfigurasi");
        Check(s.TryPour(s.Syrup, s.W) == PourError.NotConfigured, "tuang sebelum konfigurasi ditolak");
        Check(s.TryInput() == Media3InputResult.NotConfigured, "input sebelum konfigurasi ditolak");

        Check(s.Validate(0, 3) == Media3ConfigError.SyrupOutOfRange, "sirup 0 ditolak");
        Check(s.Validate(11, 3) == Media3ConfigError.SyrupOutOfRange, "sirup 11 ditolak");
        Check(s.Validate(3, 0) == Media3ConfigError.WaterOutOfRange, "air 0 ditolak");
        Check(s.Validate(3, 11) == Media3ConfigError.WaterOutOfRange, "air 11 ditolak");
        Check(s.Validate(10, 10) == Media3ConfigError.None, "10 dan 10 diterima");

        Check(s.ApplyConfig(0, 3) == Media3ConfigError.SyrupOutOfRange && !s.IsConfigured, "konfigurasi tidak valid tidak diterapkan");

        Check(s.ApplyConfig(3, 5) == Media3ConfigError.None, "konfigurasi 3 sirup 5 air diterima");
        Check(s.Syrup.VolumeSections == 3 && s.Water.VolumeSections == 5, "gelas terisi sesuai konfigurasi");
        Check(s.Syrup.CapacitySections == 10 && s.Water.CapacitySections == 10, "kapasitas gelas = MaxPerCup");
        Check(s.W.CapacitySections == 20 && s.W.IsEmpty, "wadah berkapasitas 2x MaxPerCup dan kosong");
        Check(!s.NeedsConfirmBeforeReconfigure, "belum ada tuang: tidak perlu konfirmasi");
    }

    void TestPour()
    {
        var s = new Media3Session();
        s.ApplyConfig(3, 5);

        Check(s.TryPour(s.W, s.W) == PourError.SameContainer, "wadah ke wadah ditolak");
        Check(s.TryPour(s.W, s.Syrup) == PourError.SourceCannotGive, "wadah tidak boleh memberi");
        Check(s.TryPour(s.Syrup, s.Water) == PourError.TargetCannotReceive, "gelas tidak boleh menerima");

        Check(s.TryPour(s.Syrup, s.W) == PourError.None, "tuang 1 bagian sirup");
        Check(s.W.VolumeSections == 1 && s.Syrup.VolumeSections == 2, "volume berpindah tepat 1 bagian");
        Check(s.PourCount == 1 && s.NeedsConfirmBeforeReconfigure, "setelah tuang: perlu konfirmasi");

        PourAll(s, s.Syrup);
        Check(s.Syrup.IsEmpty && s.W.VolumeSections == 3, "semua sirup pindah ke wadah");
        Check(s.TryPour(s.Syrup, s.W) == PourError.SourceEmpty, "gelas kosong tidak bisa dituang");

        PourAll(s, s.Water);
        Check(s.W.VolumeSections == 8, "wadah berisi 3 sirup + 5 air");
        Check(s.W.Content.Concentration.ValueEquals(new Fraction(3, 8)), "konsentrasi wadah 3/8");
    }

    void TestInputDefault()
    {
        var s = new Media3Session();    // RequireAllPoured = false
        s.ApplyConfig(3, 5);

        Check(s.TryInput() == Media3InputResult.NothingPoured, "wadah kosong: input ditolak");

        s.TryPour(s.Syrup, s.W);
        Check(s.TryInput() == Media3InputResult.Added, "1 bagian dari satu gelas saja sudah boleh");
        Check(s.Entries.Count == 1 && s.Entries[0].syrupSections == 1 && s.Entries[0].waterSections == 0, "entri berisi 1 sirup 0 air");
        Check(s.Entries[0].Concentration.ValueEquals(Fraction.Whole(1)), "sirup saja = konsentrasi 1");
        Check(s.Syrup.VolumeSections == 3 && s.Water.VolumeSections == 5 && s.W.IsEmpty && s.PourCount == 0,
              "setelah input: gelas terisi ulang, wadah kosong");
    }

    void TestInputStrict()
    {
        var s = new Media3Session(new Media3Limits { RequireAllPoured = true });
        s.ApplyConfig(3, 5);

        s.TryPour(s.Syrup, s.W);
        Check(s.TryInput() == Media3InputResult.NotAllPoured, "mode penuh: masih ada cairan di gelas, ditolak");
        Check(s.Entries.Count == 0 && s.W.VolumeSections == 1, "penolakan tidak mengubah apa pun");

        PourAll(s, s.Syrup);
        PourAll(s, s.Water);
        Check(s.TryInput() == Media3InputResult.Added, "mode penuh: semua dituang, diterima");
        Check(s.Entries[0].syrupSections == 3 && s.Entries[0].waterSections == 5, "entri 3 sirup 5 air");
    }

    void TestPartialEntries()
    {
        var s = new Media3Session();

        s.ApplyConfig(3, 5);
        s.TryPour(s.Water, s.W);
        s.TryPour(s.Water, s.W);
        Check(s.TryInput() == Media3InputResult.Added, "air saja diterima");
        Check(s.Entries[0].syrupSections == 0 && s.Entries[0].waterSections == 2, "entri 0 sirup 2 air");
        Check(s.Entries[0].Concentration.ValueEquals(Fraction.Zero), "air saja = konsentrasi 0");

        s.ApplyConfig(4, 4);
        PourAll(s, s.Syrup);
        Check(s.TryInput() == Media3InputResult.Added, "4 sirup 0 air diterima");

        s.ApplyConfig(2, 4);
        s.TryPour(s.Syrup, s.W);
        s.TryPour(s.Syrup, s.W);
        Check(s.TryInput() == Media3InputResult.Added, "2 sirup 0 air: angka beda, tetap diterima");
        Check(s.Entries[1].SameRatio(s.Entries[2]) && !s.Entries[1].SamePair(s.Entries[2]), "4:0 dan 2:0 senilai tapi bukan pasangan sama");
        Check(!s.Entries[0].SameRatio(s.Entries[1]), "semua air dan semua sirup tidak senilai");
    }

    void TestEquivalenceAndDuplicate()
    {
        var s = new Media3Session();
        int entryEvents = 0;
        s.EntriesChanged += () => entryEvents++;

        Check(DoTrial(s, 3, 5) == Media3InputResult.Added, "entri 3:5 diterima");
        Check(DoTrial(s, 6, 10) == Media3InputResult.Added, "6:10 senilai tapi angka beda, tetap diterima");
        Check(s.Entries[0].SameRatio(s.Entries[1]) && !s.Entries[0].SamePair(s.Entries[1]), "3:5 dan 6:10 senilai");
        Check(entryEvents == 2, "dua event entri");

        Check(DoTrial(s, 3, 5) == Media3InputResult.DuplicateEntry, "3:5 sama persis ditolak");
        Check(s.Entries.Count == 2 && entryEvents == 2, "duplikat tidak menambah entri maupun event");
        Check(s.W.VolumeSections == 8 && s.PourCount == 8, "duplikat: wadah dan gelas tidak direset");
        Check(s.LastDuplicateIndex == 0, "indeks entri yang kembar dicatat");
    }

    void TestTrialLimitAndRemove()
    {
        var s = new Media3Session();    // MaxTrials 4

        Check(DoTrial(s, 1, 1) == Media3InputResult.Added, "entri 1");
        Check(DoTrial(s, 2, 3) == Media3InputResult.Added, "entri 2");
        Check(DoTrial(s, 3, 5) == Media3InputResult.Added, "entri 3");
        Check(!s.TrialsFull, "3 entri: belum penuh");
        Check(DoTrial(s, 4, 1) == Media3InputResult.Added, "entri 4");
        Check(s.TrialsFull, "4 entri: penuh");

        Check(DoTrial(s, 5, 5) == Media3InputResult.TrialsFull, "entri ke-5 ditolak");
        Check(DoTrial(s, 1, 1) == Media3InputResult.TrialsFull, "penuh didahulukan sebelum cek duplikat");
        Check(s.Entries.Count == 4, "jumlah entri tetap 4");

        Check(!s.RemoveEntry(-1) && !s.RemoveEntry(4), "hapus index di luar rentang gagal");
        Check(s.RemoveEntry(0) && s.Entries.Count == 3, "hapus entri 1");
        Check(s.Entries[0].syrupSections == 2 && s.Entries[0].waterSections == 3, "entri di bawahnya bergeser naik");
        Check(!s.TrialsFull, "setelah hapus: tidak penuh lagi");
        Check(DoTrial(s, 5, 5) == Media3InputResult.Added, "bisa menambah lagi setelah hapus");
    }

    void TestResetAndReconfigure()
    {
        var s = new Media3Session();
        int entryEvents = 0;
        s.EntriesChanged += () => entryEvents++;

        DoTrial(s, 3, 5);
        DoTrial(s, 2, 4);
        s.ApplyConfig(2, 4);
        s.TryPour(s.Syrup, s.W);
        Check(s.NeedsConfirmBeforeReconfigure, "ada hasil tuang: perlu konfirmasi");

        entryEvents = 0;
        s.ResetAll();
        Check(s.Entries.Count == 0 && entryEvents == 1, "reset semua menghapus entri dan memicu 1 event");
        Check(s.Syrup.VolumeSections == 2 && s.Water.VolumeSections == 4 && s.W.IsEmpty && s.PourCount == 0,
              "reset semua mengisi ulang gelas sesuai konfigurasi");
        s.ResetAll();
        Check(entryEvents == 1, "reset saat entri kosong tidak memicu event entri");

        s.TryPour(s.Syrup, s.W);
        Check(s.ApplyConfig(4, 4) == Media3ConfigError.None, "ubah konfigurasi");
        Check(!s.NeedsConfirmBeforeReconfigure && s.Syrup.VolumeSections == 4 && s.Water.VolumeSections == 4 && s.W.IsEmpty,
              "konfigurasi baru mengisi ulang dan mengosongkan wadah");

        entryEvents = 0;
        DoTrial(s, 1, 2);
        Check(entryEvents == 1, "tambah entri memicu event");
        s.RemoveEntry(0);
        Check(entryEvents == 2, "hapus entri memicu event");

        DoTrial(s, 3, 5);
        s.ApplyConfig(6, 6);
        Check(s.Entries.Count == 1, "ubah konfigurasi tidak menyentuh entri");
    }

    void TestEntryMath()
    {
        var a = new Media3Entry(3, 5);
        var b = new Media3Entry(6, 10);
        var c = new Media3Entry(2, 5);
        var d = new Media3Entry(2, 1);
        var e = new Media3Entry(8, 4);

        Check(a.Concentration.ValueEquals(new Fraction(3, 8)), "3:5 = konsentrasi 3/8");
        Check(a.Concentration.ValueEquals(b.Concentration), "3:5 dan 6:10 konsentrasi sama");
        Check(!a.Concentration.ValueEquals(c.Concentration), "3:5 dan 2:5 konsentrasi beda");
        Check(d.SameRatio(e) && !d.SamePair(e), "2:1 dan 8:4 senilai tapi bukan pasangan sama");
        Check(d.Concentration.ValueEquals(e.Concentration), "2:1 dan 8:4 warna sama");

        Check(Mathf.Approximately(a.SyrupRatio, 0.375f), "3:5 = rasio sirup 0.375");
        Check(Mathf.Approximately(new Media3Entry(0, 2).SyrupRatio, 0f), "air saja = rasio 0");
        Check(Mathf.Approximately(new Media3Entry(2, 0).SyrupRatio, 1f), "sirup saja = rasio 1");
    }
}