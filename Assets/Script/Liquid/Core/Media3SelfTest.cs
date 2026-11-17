using UnityEngine;

public class Media3SelfTest : MonoBehaviour
{
    int pass, fail;

    [ContextMenu("Run Media3 Tests")]
    public void Run()
    {
        pass = fail = 0;
        TestInitial();
        TestPourAndRefill();
        TestCapacity();
        TestMerge();
        TestInput();
        TestResets();
        TestEvents();
        TestMessages();
        string summary = $"Media3 tests: {pass} lulus, {fail} gagal";
        if (fail == 0) Debug.Log(summary); else Debug.LogError(summary);
    }

    void Check(bool ok, string name)
    {
        if (ok) pass++;
        else { fail++; Debug.LogError("GAGAL: " + name); }
    }

    static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.0001f;

    // Menuang gelas penuh ke wadah (swipe), lalu mengisinya lagi
    static PourError PourKind(Media3Session s, LiquidKind kind) =>
        s.TryPour(kind == LiquidKind.Water ? s.Water : s.Syrup, s.W);

    static void PourRefill(Media3Session s, LiquidKind kind, int times)
    {
        for (int i = 0; i < times; i++)
        {
            PourKind(s, kind);
            s.TryRefill(kind);
        }
    }

    void TestInitial()
    {
        var s = new Media3Session();
        Check(s.Limits.MaxTotalPours == 20 && s.Limits.MaxTrials == 4, "batas bawaan: 20 tuangan, 4 percobaan");
        Check(s.IsEmpty && s.WaterCount == 0 && s.SyrupCount == 0 && s.TotalCount == 0, "wadah awalnya kosong");
        Check(s.WaterCupFull && s.SyrupCupFull, "kedua gelas awalnya penuh");
        Check(Near(s.FillRatio, 0f) && !s.WadahFull, "tinggi isi wadah awal 0");
        Check(s.CheckPour(s.Water, s.Syrup) == PourError.TargetCannotReceive, "gelas bukan penerima");
        Check(s.CheckPour(s.W, s.Water) == PourError.SourceCannotGive, "wadah bukan pemberi");
        Check(s.CheckPour(s.Water, s.Water) == PourError.SameContainer, "gelas ke dirinya sendiri ditolak");
    }

    void TestPourAndRefill()
    {
        var s = new Media3Session();
        Check(s.CheckPour(s.Water, s.W) == PourError.None && PourKind(s, LiquidKind.Water) == PourError.None, "tuang gelas air");
        Check(s.WaterCount == 1 && !s.WaterCupFull && s.SyrupCupFull && s.SyrupCount == 0, "gelas air kosong, hitungan air 1, gelas sirup tetap penuh");
        Check(Near(s.FillRatio, 0.05f), "satu tuangan = 1/20 tinggi wadah");

        Check(s.CheckPour(s.Water, s.W) == PourError.SourceEmpty && PourKind(s, LiquidKind.Water) == PourError.SourceEmpty, "gelas kosong tidak bisa dituang lagi");
        Check(s.WaterCount == 1, "penolakan tidak mengubah hitungan");
        Check(PourKind(s, LiquidKind.Syrup) == PourError.None && s.SyrupCount == 1 && !s.SyrupCupFull, "gelas sirup tetap bisa dituang");

        Check(s.CheckRefill(LiquidKind.Water) == Media3RefillResult.Refilled && s.TryRefill(LiquidKind.Water) == Media3RefillResult.Refilled && s.WaterCupFull, "isi ulang gelas air");
        Check(s.TryRefill(LiquidKind.Water) == Media3RefillResult.AlreadyFull, "gelas yang masih penuh tidak perlu diisi");
        Check(s.WaterCount == 1, "isi ulang tidak mengubah hitungan");
        Check(PourKind(s, LiquidKind.Water) == PourError.None && s.WaterCount == 2, "setelah diisi, bisa dituang lagi");
    }

    void TestCapacity()
    {
        var s = new Media3Session();
        PourRefill(s, LiquidKind.Water, 11);
        PourRefill(s, LiquidKind.Syrup, 5);
        Check(s.WaterCount == 11 && s.SyrupCount == 5 && s.TotalCount == 16, "11 air + 5 sirup");
        Check(Near(s.FillRatio, 0.8f), "16 dari 20 = tinggi 0.8");
        Check(Near(s.SyrupRatio, 5f / 16f), "rasio sirup 5/16");

        PourRefill(s, LiquidKind.Syrup, 4);
        Check(s.TotalCount == 20 && s.WadahFull && Near(s.FillRatio, 1f), "tuangan ke-20: wadah penuh");
        Check(s.CheckPour(s.Water, s.W) == PourError.TargetFull, "tuangan ke-21 ditolak (gelas penuh tapi wadah penuh)");
        Check(PourKind(s, LiquidKind.Water) == PourError.TargetFull && s.TotalCount == 20, "penolakan tidak mengubah isi");

        // 20 air saja, tanpa sirup (urutan bebas)
        var t = new Media3Session();
        PourRefill(t, LiquidKind.Water, 20);
        Check(t.WaterCount == 20 && t.SyrupCount == 0 && t.WadahFull && Near(t.SyrupRatio, 0f), "20 air saja diperbolehkan");

        // batas bisa diubah
        var small = new Media3Session(new Media3Limits { MaxTotalPours = 3, MaxTrials = 2 });
        PourRefill(small, LiquidKind.Water, 3);
        Check(small.WadahFull && PourKind(small, LiquidKind.Syrup) == PourError.TargetFull, "batas 3: tuangan ke-4 ditolak");
    }

    void TestMerge()
    {
        var s = new Media3Session();
        Check(s.CheckMerge() == PourError.None && s.TryMerge() == PourError.None, "Gabungkan dari awal");
        Check(s.WaterCount == 1 && s.SyrupCount == 1 && !s.WaterCupFull && !s.SyrupCupFull, "air +1, sirup +1, kedua gelas kosong");
        Check(s.CheckMerge() == PourError.SourceEmpty && s.TryMerge() == PourError.SourceEmpty && s.TotalCount == 2, "gelas kosong: Gabungkan ditolak");

        s.TryRefill(LiquidKind.Water);
        Check(s.CheckMerge() == PourError.SourceEmpty, "hanya satu gelas terisi: ditolak");
        s.TryRefill(LiquidKind.Syrup);
        Check(s.TryMerge() == PourError.None && s.TotalCount == 4, "kedua gelas terisi lagi: bisa");

        // muat 2 tuangan? 19 + 2 > 20
        var t = new Media3Session();
        PourRefill(t, LiquidKind.Water, 19);
        Check(t.CheckMerge() == PourError.TargetFull && t.TryMerge() == PourError.TargetFull && t.TotalCount == 19, "19 tuangan: dua lagi tidak muat");
        Check(PourKind(t, LiquidKind.Syrup) == PourError.None && t.TotalCount == 20, "tetapi satu tuangan masih muat");
        var u = new Media3Session();
        PourRefill(u, LiquidKind.Water, 18);
        Check(u.TryMerge() == PourError.None && u.TotalCount == 20 && u.WadahFull, "18 + 2 = 20 muat");
    }

    void TestInput()
    {
        var s = new Media3Session();
        Check(s.TryInput() == Media3InputResult.NothingPoured, "wadah kosong: tidak bisa dicatat");

        PourRefill(s, LiquidKind.Water, 7);
        PourRefill(s, LiquidKind.Syrup, 4);
        Check(s.TryInput() == Media3InputResult.Added && s.Entries.Count == 1, "entri pertama tercatat");
        Media3Entry e = s.Entries[0];
        Check(e.waterSections == 7 && e.syrupSections == 4 && e.TotalSections == 11, "entri 4 sirup : 7 air");
        Check(Near(e.LevelRatio(s.Limits), 0.55f), "tinggi wadah entri 4:7 = 11/20 = 0.55");
        Check(s.IsEmpty && s.WaterCupFull && s.SyrupCupFull && Near(s.FillRatio, 0f), "setelah Input: wadah kosong, kedua gelas penuh");

        // entri kembar
        PourRefill(s, LiquidKind.Water, 7);
        PourRefill(s, LiquidKind.Syrup, 4);
        Check(s.TryInput() == Media3InputResult.DuplicateEntry && s.LastDuplicateIndex == 0 && s.Entries.Count == 1, "pasangan sama persis ditolak");
        Check(s.WaterCount == 7 && s.SyrupCount == 4, "penolakan tidak mengosongkan wadah");

        // percobaan kedua 5 sirup : 11 air dari soal (11:5 air:sirup) => tinggi 0.8
        var t = new Media3Session();
        PourRefill(t, LiquidKind.Water, 11);
        PourRefill(t, LiquidKind.Syrup, 5);
        t.TryInput();
        Check(Near(t.Entries[0].LevelRatio(t.Limits), 0.8f), "entri 11:5 tingginya 0.8");

        // rasio senilai tetap kembar? tidak: angka beda boleh
        var r = new Media3Session();
        PourRefill(r, LiquidKind.Water, 2); PourRefill(r, LiquidKind.Syrup, 1); Check(r.TryInput() == Media3InputResult.Added, "2:1");
        PourRefill(r, LiquidKind.Water, 4); PourRefill(r, LiquidKind.Syrup, 2); Check(r.TryInput() == Media3InputResult.Added, "4:2 (rasio senilai) boleh");
        Check(r.Entries[0].SameRatio(r.Entries[1]) && !r.Entries[0].SamePair(r.Entries[1]), "rasio senilai terdeteksi, pasangan beda");

        // penuh 4 percobaan
        var f = new Media3Session();
        for (int i = 1; i <= 4; i++) { PourRefill(f, LiquidKind.Water, i); Check(f.TryInput() == Media3InputResult.Added, $"percobaan {i}"); }
        Check(f.TrialsFull, "4 percobaan: penuh");
        PourRefill(f, LiquidKind.Water, 9);
        Check(f.TryInput() == Media3InputResult.TrialsFull && f.WaterCount == 9, "percobaan ke-5 ditolak, isi wadah tetap");

        // hapus entri
        Check(f.RemoveEntry(1) && f.Entries.Count == 3 && !f.RemoveEntry(9) && !f.RemoveEntry(-1), "hapus entri");
    }

    void TestResets()
    {
        var s = new Media3Session();
        PourRefill(s, LiquidKind.Water, 3);
        PourKind(s, LiquidKind.Syrup);                    // gelas sirup kosong, hitungan sirup 1
        s.ResetMixture();
        Check(s.IsEmpty && s.WaterCupFull && s.SyrupCupFull, "ResetMixture: wadah kosong, gelas penuh");

        PourRefill(s, LiquidKind.Water, 2);
        s.TryInput();
        PourKind(s, LiquidKind.Syrup);
        s.ResetAll();
        Check(s.Entries.Count == 0 && s.IsEmpty && s.WaterCupFull && s.SyrupCupFull, "ResetAll: entri dihapus, wadah kosong, gelas penuh");
    }

    void TestEvents()
    {
        var s = new Media3Session();
        int changed = 0, entriesChanged = 0;
        s.Changed += () => changed++;
        s.EntriesChanged += () => entriesChanged++;

        PourKind(s, LiquidKind.Water);
        Check(changed == 1, "tuang: satu Changed");
        PourKind(s, LiquidKind.Water);
        Check(changed == 1, "tuang ditolak: tidak ada Changed");
        s.TryRefill(LiquidKind.Water);
        Check(changed == 2, "isi ulang: Changed");
        s.TryRefill(LiquidKind.Water);
        Check(changed == 2, "isi ulang yang tidak perlu: tidak ada Changed");
        s.TryInput();
        Check(changed == 3 && entriesChanged == 1, "Input: Changed dan EntriesChanged");
        s.ResetMixture();
        Check(changed == 3, "ResetMixture pada keadaan bersih: tidak ada Changed");
    }

    void TestMessages()
    {
        var lim = new Media3Limits();
        Check(Media3Messages.ForPour(PourError.SourceEmpty, LiquidKind.Water, lim).Contains("air") && Media3Messages.ForPour(PourError.SourceEmpty, LiquidKind.Syrup, lim).Contains("sirup"), "pesan gelas kosong menyebut larutannya");
        Check(Media3Messages.ForPour(PourError.TargetFull, LiquidKind.Water, lim).Contains("20"), "pesan wadah penuh menyebut batas 20");
        Check(Media3Messages.ForMerge(PourError.SourceEmpty, lim) == Media3Messages.MergeNeedsFull, "pesan Gabungkan butuh gelas penuh");
        Check(Media3Messages.ForPour(PourError.None, LiquidKind.Water, lim) == "", "tanpa error: pesan kosong");
    }
}