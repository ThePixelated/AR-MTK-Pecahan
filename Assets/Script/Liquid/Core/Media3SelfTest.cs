using UnityEngine;

public class Media3SelfTest : MonoBehaviour
{
    int pass, fail;

    [ContextMenu("Run Media3 Tests")]
    public void Run()
    {
        pass = fail = 0;
        TestPour();
        TestMax();
        TestMerge();
        TestInput();
        TestEntriesAndReset();
        TestEntryMath();
        string summary = $"Media3 tests: {pass} lulus, {fail} gagal";
        if (fail == 0) Debug.Log(summary); else Debug.LogError(summary);
    }

    void Check(bool ok, string name)
    {
        if (ok) pass++;
        else { fail++; Debug.LogError("GAGAL: " + name); }
    }

    static void Pour(Media3Session s, LiquidContainer from, int n)
    {
        for (int i = 0; i < n; i++) s.TryPour(from, s.W);
    }

    void TestPour()
    {
        var s = new Media3Session();
        Check(s.IsEmpty && s.WaterCount == 0 && s.SyrupCount == 0 && s.SyrupRatio == 0f, "awal: wadah kosong, rasio 0");

        Check(s.CheckPour(s.W, s.W) == PourError.SameContainer, "wadah ke wadah ditolak");
        Check(s.CheckPour(s.W, s.Water) == PourError.SourceCannotGive, "wadah tidak boleh memberi");
        Check(s.CheckPour(s.Water, s.Syrup) == PourError.TargetCannotReceive, "air ke gelas sirup ditolak");
        Check(s.CheckPour(s.Syrup, s.Water) == PourError.TargetCannotReceive, "sirup ke gelas air ditolak");

        int events = 0;
        s.Changed += () => events++;

        Check(s.TryPour(s.Water, s.W) == PourError.None && s.WaterCount == 1 && s.SyrupCount == 0, "swipe gelas air: air +1");
        Check(s.SyrupRatio == 0f && !s.IsEmpty, "air saja: rasio 0 (warna air)");
        Check(s.TryPour(s.Syrup, s.W) == PourError.None && s.SyrupCount == 1 && s.TotalCount == 2, "swipe gelas sirup: sirup +1");
        Check(Mathf.Approximately(s.SyrupRatio, 0.5f), "1 air 1 sirup: rasio 0.5");
        Check(events == 2, "tiap swipe memicu satu Changed");

        Pour(s, s.Syrup, 2);
        Check(Mathf.Approximately(s.SyrupRatio, 0.75f), "1 air 3 sirup: rasio 0.75");
    }

    void TestMax()
    {
        var s = new Media3Session();
        Pour(s, s.Water, 10);
        Check(s.WaterCount == 10, "air 10 cangkir diterima");
        Check(s.CheckPour(s.Water, s.W) == PourError.TargetFull, "air ke-11 ditolak");
        Check(s.TryPour(s.Water, s.W) == PourError.TargetFull && s.WaterCount == 10, "penolakan tidak mengubah hitungan");
        Check(s.TryPour(s.Syrup, s.W) == PourError.None && s.SyrupCount == 1, "batas sirup terpisah dari batas air");

        var small = new Media3Session(new Media3Limits { MaxPerCup = 2 });
        Pour(small, small.Syrup, 3);
        Check(small.SyrupCount == 2, "MaxPerCup kustom 2");
    }

    void TestMerge()
    {
        var s = new Media3Session();
        Check(s.CheckMerge() == PourError.None && s.TryMerge() == PourError.None && s.WaterCount == 1 && s.SyrupCount == 1, "Gabungkan: air +1 dan sirup +1");
        Check(Mathf.Approximately(s.SyrupRatio, 0.5f), "setelah Gabungkan rasio 0.5");

        var full = new Media3Session();
        Pour(full, full.Water, 10);
        Pour(full, full.Syrup, 3);
        Check(full.CheckMerge() == PourError.TargetFull, "air sudah maksimal: Gabungkan ditolak");
        Check(full.TryMerge() == PourError.TargetFull && full.WaterCount == 10 && full.SyrupCount == 3, "ditolak seluruhnya: sirup juga tidak bertambah");

        var other = new Media3Session();
        Pour(other, other.Syrup, 10);
        Check(other.TryMerge() == PourError.TargetFull && other.WaterCount == 0, "sirup maksimal: air tidak bertambah");
    }

    void TestInput()
    {
        var s = new Media3Session();
        Check(s.TryInput() == Media3InputResult.NothingPoured, "wadah kosong: input ditolak");

        Pour(s, s.Water, 2);
        Pour(s, s.Syrup, 3);
        Check(s.TryInput() == Media3InputResult.Added, "isi wadah dicatat");
        Check(s.Entries.Count == 1 && s.Entries[0].syrupSections == 3 && s.Entries[0].waterSections == 2, "entri 3 sirup 2 air");
        Check(s.IsEmpty && s.SyrupRatio == 0f, "setelah input: wadah kembali kosong dan berwarna air");

        // air saja dan sirup saja boleh dicatat
        Pour(s, s.Water, 2);
        Check(s.TryInput() == Media3InputResult.Added && s.Entries[1].syrupSections == 0 && s.Entries[1].waterSections == 2, "air saja diterima");
        Pour(s, s.Syrup, 4);
        Check(s.TryInput() == Media3InputResult.Added && s.Entries[2].syrupSections == 4 && s.Entries[2].waterSections == 0, "sirup saja diterima");

        // duplikat ditolak tanpa mereset wadah; rasio senilai tetap diterima
        var d = new Media3Session();
        Pour(d, d.Water, 2); Pour(d, d.Syrup, 3);
        d.TryInput();
        Pour(d, d.Water, 2); Pour(d, d.Syrup, 3);
        Check(d.TryInput() == Media3InputResult.DuplicateEntry && d.LastDuplicateIndex == 0, "3:2 yang sama ditolak");
        Check(d.TotalCount == 5 && d.Entries.Count == 1, "duplikat: wadah tidak dikosongkan");
        Pour(d, d.Water, 2); Pour(d, d.Syrup, 3);
        Check(d.TryInput() == Media3InputResult.Added && d.Entries[1].SameRatio(d.Entries[0]) && !d.Entries[1].SamePair(d.Entries[0]), "6:4 senilai tapi angka beda, diterima");

        // percobaan penuh
        var t = new Media3Session();
        for (int i = 1; i <= 4; i++) { Pour(t, t.Water, i); Check(t.TryInput() == Media3InputResult.Added, $"entri {i}"); }
        Check(t.TrialsFull, "4 entri: penuh");
        Pour(t, t.Water, 9);
        Check(t.TryInput() == Media3InputResult.TrialsFull && t.TotalCount == 9, "entri ke-5 ditolak, wadah tidak berubah");
    }

    void TestEntriesAndReset()
    {
        var s = new Media3Session();
        int entryEvents = 0;
        s.EntriesChanged += () => entryEvents++;

        Pour(s, s.Water, 1); s.TryInput();
        Pour(s, s.Water, 2); s.TryInput();
        Pour(s, s.Water, 3); s.TryInput();
        Check(entryEvents == 3, "tiap entri memicu event");

        Check(!s.RemoveEntry(-1) && !s.RemoveEntry(3), "indeks di luar jangkauan gagal");
        Check(s.RemoveEntry(0) && s.Entries.Count == 2 && s.Entries[0].waterSections == 2 && entryEvents == 4, "hapus entri 1: nomor bergeser");

        Pour(s, s.Syrup, 2);
        s.ResetMixture();
        Check(s.IsEmpty && s.Entries.Count == 2, "kosongkan wadah: entri tetap");

        Pour(s, s.Syrup, 2);
        s.ResetAll();
        Check(s.IsEmpty && s.Entries.Count == 0 && entryEvents == 5, "reset semua: entri dan wadah kosong");
        s.ResetAll();
        Check(entryEvents == 5, "reset saat entri kosong tidak memicu event entri");
    }

    void TestEntryMath()
    {
        var a = new Media3Entry(3, 5);
        var b = new Media3Entry(6, 10);
        var c = new Media3Entry(2, 5);
        var d = new Media3Entry(2, 1);
        var e = new Media3Entry(8, 4);

        Check(a.Concentration.ValueEquals(new Fraction(3, 8)), "3:5 = konsentrasi 3/8");
        Check(a.Concentration.ValueEquals(b.Concentration) && !a.Concentration.ValueEquals(c.Concentration), "3:5 dan 6:10 sama, 2:5 beda");
        Check(d.SameRatio(e) && !d.SamePair(e) && d.Concentration.ValueEquals(e.Concentration), "2:1 dan 8:4 senilai tapi bukan pasangan sama");
        Check(Mathf.Approximately(a.SyrupRatio, 0.375f), "3:5 = rasio sirup 0.375");
        Check(Mathf.Approximately(new Media3Entry(0, 2).SyrupRatio, 0f) && Mathf.Approximately(new Media3Entry(2, 0).SyrupRatio, 1f), "air saja 0 dan sirup saja 1");
    }
}