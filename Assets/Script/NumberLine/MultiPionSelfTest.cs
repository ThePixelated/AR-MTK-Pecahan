using System.Collections.Generic;
using UnityEngine;

public class MultiPionSelfTest : MonoBehaviour
{
    int pass, fail;

    [ContextMenu("Run MultiPion Tests")]
    public void Run()
    {
        pass = fail = 0;
        TestValidate();
        TestLayout();
        TestSelection();
        TestPartitionIndependence();
        TestPlacement();
        TestCheck();
        TestCheckModes();
        TestLockConfig();
        TestFlow();
        string summary = $"MultiPion tests: {pass} lulus, {fail} gagal";
        if (fail == 0) Debug.Log(summary); else Debug.LogError(summary);
    }

    void Check(bool ok, string name)
    {
        if (ok) pass++;
        else { fail++; Debug.LogError("GAGAL: " + name); }
    }

    static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.0005f;
    static FractionValue FV(int w, int n, int d) => new FractionValue(w, n, d);

    static bool Has(List<string> issues, string part)
    {
        foreach (string s in issues) if (s.Contains(part)) return true;
        return false;
    }

    // Soal contoh: lomba marathon. Rahmi 5/10, Ari 3/4, Sinta 3/5 (label kosong = "C")
    static NumberLineQuestion Marathon() => new NumberLineQuestion
    {
        kind = QuestionKind.MultiPion,
        presetLength = 1,
        pions = new List<PionAnswer>
        {
            new PionAnswer { label = "Rahmi", value = FV(0, 5, 10) },
            new PionAnswer { label = "Ari", value = FV(0, 3, 4) },
            new PionAnswer { label = "", value = FV(0, 3, 5) }
        }
    };

    static LatihanSession Start(NumberLineQuestion q, bool stay = true, LatihanOptions options = null)
    {
        var level = new LatihanLevel { title = "L", stayUntilCorrect = stay, questions = new List<NumberLineQuestion> { q } };
        var s = new LatihanSession(new[] { level }, options, 1);
        s.StartLevel(0);
        return s;
    }

    // Menaruh ketiga pion marathon: A tick a (partisi 10), B tick b (partisi 4), C tick c (partisi 5)
    static void PlaceAll(MultiPionBoard b, int a, int bb, int c)
    {
        b.PlaceAtTick(0, a);
        b.PlaceAtTick(1, bb);
        b.PlaceAtTick(2, c);
    }

    void TestValidate()
    {
        var lim = new NumberLineLimits();
        Check(Marathon().Validate(lim, 3, 5).Count == 0, "soal marathon sah (partisi 10, 4, 5 masing-masing)");
        Check(new NumberLineQuestion { kind = QuestionKind.MultiPion }.pions.Count == 3, "soal baru berisi 3 pion");

        var empty = Marathon(); empty.pions = new List<PionAnswer>();
        Check(Has(empty.Validate(lim, 3, 5), "belum ada pion"), "tanpa pion: ditolak");

        var many = Marathon();
        many.pions = new List<PionAnswer>();
        for (int i = 0; i < 6; i++) many.pions.Add(new PionAnswer { value = FV(0, 1, 2) });
        Check(Has(many.Validate(lim, 3, 5), "melebihi"), "6 pion melebihi batas 5");
        Check(!Has(many.Validate(lim, 3, 6), "melebihi"), "batas 6: 6 pion boleh");

        var blank = Marathon(); blank.pions[1].value = FV(0, 1, 0);
        Check(Has(blank.Validate(lim, 3, 5), "belum diisi"), "penyebut 0: nilai belum diisi");

        var beyond = Marathon(); beyond.pions[0].value = FV(1, 1, 2);
        Check(Has(beyond.Validate(lim, 3, 5), "melewati"), "3/2 melewati garis panjang 1");

        var notTick = Marathon(); notTick.pions[1].partisi = 5;
        Check(Has(notTick.Validate(lim, 3, 5), "tidak jatuh"), "3/4 tidak muncul di partisi 5");

        var bigPartisi = Marathon(); bigPartisi.pions[0].partisi = 16;
        Check(Has(bigPartisi.Validate(lim, 3, 5), "di luar"), "partisi 16 di luar batas 15");

        var byTick = Marathon(); byTick.checkMode = PionCheckMode.ByTick; byTick.pions[0].value = FV(0, 1, 2); byTick.pions[0].partisi = 10;
        Check(Has(byTick.Validate(lim, 3, 5), "penyebut"), "ByTick: partisi 10 untuk 1/2 ditolak (harus sama dengan penyebut)");
        var byValue = Marathon(); byValue.pions[0].value = FV(0, 1, 2); byValue.pions[0].partisi = 10;
        Check(byValue.Validate(lim, 3, 5).Count == 0, "ByValue: 1/2 di partisi 10 sah (tick 5)");

        var badLength = Marathon(); badLength.presetLength = 0;
        Check(Has(badLength.Validate(lim, 3, 5), "panjang"), "panjang garis 0 ditolak");
    }

    void TestLayout()
    {
        QuestionLayout l = QuestionLayout.For(Marathon());
        Check(l.multiPion && l.showLine && !l.showPion && !l.showMarkers && !l.pionUnlocked && l.panelCount == 0 && !l.NeedsNumpad, "layout MultiPion: garis, tanpa pion tunggal dan panel soal");
        Check(!l.lineUnlocked && l.chooserLocked, "lockConfig menyala: garis dan pemilih hewan terkunci");

        var open = Marathon(); open.lockConfig = false;
        QuestionLayout o = QuestionLayout.For(open);
        Check(o.lineUnlocked && !o.chooserLocked, "lockConfig mati: terbuka");

        QuestionLayout old = QuestionLayout.For(new NumberLineQuestion { kind = QuestionKind.PionOnly });
        Check(!old.multiPion && !old.chooserLocked && old.showPion, "kind lama tidak berubah");
    }

    void TestSelection()
    {
        LatihanSession s = Start(Marathon());
        MultiPionBoard b = s.Board;
        Check(b != null && b.Count == 3 && s.Current.kind == QuestionKind.MultiPion, "papan MultiPion tersedia, 3 pion");
        Check(b.Label(0) == "Rahmi" && b.Label(1) == "Ari" && b.Label(2) == "C", "label dari soal; kosong = huruf");
        Check(b.Partisi(0) == 10 && b.Partisi(1) == 4 && b.Partisi(2) == 5, "partisi otomatis = penyebut jawaban");
        Check(b.SelectedIndex == -1 && b.Length == 1, "awal: belum ada yang dipilih, panjang 1");
        Check(s.Config.length == 1 && s.Config.partisi == 10, "belum ada pilihan: garis memakai partisi pion pertama");

        int changes = 0;
        s.Changed += () => changes++;
        Check(b.Select(1) && b.SelectedIndex == 1 && s.Config.partisi == 4 && changes == 1, "pilih pion 2: garis jadi partisi 4");
        Check(b.Select(1) && changes == 1, "pilih pion yang sama: tidak ada perubahan");
        Check(!b.Select(7) && !b.Select(-1) && s.Config.partisi == 4, "indeks salah ditolak");
        b.ClearSelection();
        Check(b.SelectedIndex == -1 && s.Config.partisi == 10 && changes == 2, "batal pilih: kembali ke partisi pion pertama");

        var o = Marathon(); o.pions[0].partisi = 12;
        Check(Start(o).Board.Partisi(0) == 12, "partisi eksplisit menimpa penyebut");
        var tooBig = Marathon(); tooBig.pions[0].partisi = 16;
        Check(Start(tooBig).Board.Partisi(0) == 15, "partisi di atas batas dibatasi ke 15 di papan");

        var capped = Marathon();
        capped.pions.Add(new PionAnswer { value = FV(0, 1, 2) });
        var opt = new LatihanOptions { MaxPions = 3 };
        Check(Start(capped, true, opt).Board.Count == 3, "pion melebihi MaxPions dipotong");
    }

    // Inti permintaan: tiap pion punya garisnya sendiri pada satu garis; berganti partisi tidak menggeser pion lain
    void TestPartitionIndependence()
    {
        LatihanSession s = Start(Marathon());
        MultiPionBoard b = s.Board;

        Check(b.PlaceAtNormalized(0, 0.6f) == 6, "pion A ditaruh di 0.6 pada partisi 10: tick 6 (salah, bukan 5)");
        Check(b.ValueOf(0).ValueEquals(new Fraction(3, 5)) && s.Config.partisi == 10 && b.SelectedIndex == 0, "A di 6/10, garis partisi 10, A terpilih");

        b.Select(2);
        Check(s.Config.partisi == 5, "pilih C: garis berganti partisi 5");
        Check(b.TickOf(0) == 6 && b.Partisi(0) == 10 && Near(b.NormalizedOf(0), 0.6f), "pion A tidak bergeser sedikit pun walau garis jadi partisi 5");

        Check(b.PlaceAtNormalized(2, 0.58f) == 3 && Near(b.NormalizedOf(2), 0.6f), "C snap ke tick 3 dari partisi 5 (0.58 mendekati 0.6)");
        b.Select(1);
        Check(s.Config.partisi == 4 && Near(b.NormalizedOf(0), 0.6f) && Near(b.NormalizedOf(2), 0.6f), "garis partisi 4: A dan C tetap di tempatnya");

        // pion di nilai yang tidak jatuh di tick partisi garis tampil: tetap sah
        b.PlaceAtTick(1, 3);
        Check(Near(b.NormalizedOf(1), 0.75f), "B di 3/4 pada garis partisi 4");
        b.Select(0);
        Check(s.Config.partisi == 10 && Near(b.NormalizedOf(1), 0.75f), "kembali ke partisi 10: B (0.75, bukan tick partisi 10) tidak bergeser");
    }

    void TestPlacement()
    {
        LatihanSession s = Start(Marathon());
        MultiPionBoard b = s.Board;

        Check(b.PlacedCount == 0 && !b.AllPlaced && b.TickOf(1) == -1 && b.NormalizedOf(1) == 0f, "awal: tidak ada pion di garis");
        Check(b.PlaceAtNormalized(1, 1.4f) == 4 && b.PlaceAtNormalized(1, -0.3f) == 0, "di luar ujung: dibatasi ke 0 dan ujung kanan");
        Check(b.PlaceAtTick(1, 99) == 4 && b.PlaceAtTick(1, -5) == 0, "tick dibatasi ke 0..MaxTick");
        Check(b.PlaceAtTick(9, 1) == -1 && b.PlaceAtNormalized(-1, 0.5f) == -1, "indeks salah: -1");

        b.PlaceAtTick(0, 5); b.PlaceAtTick(2, 3);
        Check(b.PlacedCount == 3 && b.AllPlaced, "ketiga pion ditempatkan");

        int changes = 0;
        s.Changed += () => changes++;
        b.PlaceAtTick(2, 3);
        Check(changes == 0, "menaruh di tempat yang sama dan sudah terpilih: tidak ada perubahan");

        Check(b.Remove(1) && !b.IsPlaced(1) && b.TickOf(1) == -1 && b.PlacedCount == 2 && !b.AllPlaced, "pion dikembalikan ke tombol");
        Check(!b.Remove(1) && !b.Remove(9), "mencabut yang tidak ada di garis: false");

        // dua pion di nilai yang sama diperbolehkan
        b.PlaceAtTick(1, 2);
        b.PlaceAtTick(0, 5);
        Check(Near(b.NormalizedOf(0), b.NormalizedOf(1)), "dua pion boleh di posisi yang sama (5/10 dan 2/4)");
    }

    void TestCheck()
    {
        LatihanSession s = Start(Marathon());
        MultiPionBoard b = s.Board;

        LatihanCheckResult r = s.Check();
        Check(r.outcome == LatihanOutcome.Incomplete && r.indicators.Length == 3 && r.indicators[0] == NumberLineIndicator.None, "belum ada pion di garis: belum lengkap");
        Check(!s.HasChecked && s.WrongStrikes == 0, "belum lengkap: tidak dihitung salah");

        b.PlaceAtTick(0, 5); b.PlaceAtTick(1, 3);
        r = s.Check();
        Check(r.outcome == LatihanOutcome.Incomplete && r.indicators[0] == NumberLineIndicator.None && r.indicators[1] == NumberLineIndicator.None,
              "dua dari tiga: belum lengkap, benar atau salahnya tidak bocor");

        b.PlaceAtTick(2, 4);   // C seharusnya tick 3
        r = s.Check();
        Check(r.outcome == LatihanOutcome.Wrong && r.indicators[0] == NumberLineIndicator.Correct && r.indicators[1] == NumberLineIndicator.Correct && r.indicators[2] == NumberLineIndicator.Wrong,
              "lengkap, C salah: indikator per pion");
        Check(s.HasChecked && !s.IsSolved && !s.CanAdvance, "salah: belum selesai, tidak bisa lanjut (stay)");

        b.PlaceAtTick(2, 3);   // dibetulkan, pion yang benar tidak disentuh
        r = s.Check();
        Check(r.outcome == LatihanOutcome.Correct && r.indicators[0] == NumberLineIndicator.Correct && r.indicators[2] == NumberLineIndicator.Correct, "setelah dibetulkan: benar semua");
        Check(s.IsSolved && s.CanAdvance, "selesai dan boleh lanjut");

        // mode continue
        LatihanSession c = Start(Marathon(), false);
        PlaceAll(c.Board, 6, 3, 3);
        Check(c.Check().outcome == LatihanOutcome.Wrong && c.CanAdvance, "mode continue: salah pun boleh lanjut setelah CEK");

        // strike
        var opt = new LatihanOptions { EnableWrongStrike = true, MaxWrongStrikes = 2 };
        LatihanSession k = Start(Marathon(), true, opt);
        PlaceAll(k.Board, 6, 3, 3);
        k.Check(); k.Check();
        Check(k.WrongStrikes == 2 && k.Failed, "dua kali salah: level gagal");
        Check(k.Check().outcome == LatihanOutcome.Blocked, "setelah gagal: CEK diblokir");
    }

    void TestCheckModes()
    {
        // ByValue: jawaban 1/2 dengan garis partisi 10 diterima di tick 5 (5/10)
        var byValue = Marathon(); byValue.pions[0].value = FV(0, 1, 2); byValue.pions[0].partisi = 10;
        LatihanSession v = Start(byValue);
        PlaceAll(v.Board, 5, 3, 3);
        Check(v.Board.Partisi(0) == 10 && v.Check().outcome == LatihanOutcome.Correct, "ByValue: 5/10 sama dengan 1/2");

        // ByTick: partisi harus sama dengan penyebut jawaban
        var byTick = Marathon(); byTick.checkMode = PionCheckMode.ByTick; byTick.pions[0].value = FV(0, 1, 2); byTick.pions[0].partisi = 10;
        LatihanSession t = Start(byTick);
        PlaceAll(t.Board, 5, 3, 3);
        LatihanCheckResult r = t.Check();
        Check(r.outcome == LatihanOutcome.Wrong && r.indicators[0] == NumberLineIndicator.Wrong && r.indicators[1] == NumberLineIndicator.Correct, "ByTick: 5/10 bukan jawaban 1/2 (partisi harus sama dengan penyebut)");

        var plain = Marathon(); plain.checkMode = PionCheckMode.ByTick;
        LatihanSession p = Start(plain);
        PlaceAll(p.Board, 5, 3, 3);
        Check(p.Check().outcome == LatihanOutcome.Correct, "ByTick dengan partisi otomatis: tick yang tepat benar");

        // pecahan campuran 1 1/4 pada garis panjang 2
        foreach (PionCheckMode mode in new[] { PionCheckMode.ByValue, PionCheckMode.ByTick })
        {
            var mixed = new NumberLineQuestion
            {
                kind = QuestionKind.MultiPion, presetLength = 2, checkMode = mode,
                pions = new List<PionAnswer> { new PionAnswer { value = FV(1, 1, 4) } }
            };
            LatihanSession m = Start(mixed);
            Check(m.Board.Partisi(0) == 4 && m.Board.Config.MaxTick == 8, $"1 1/4 ({mode}): partisi 4, garis 2 satuan punya 8 tick");
            m.Board.PlaceAtTick(0, 5);
            Check(m.Check().outcome == LatihanOutcome.Correct, $"1 1/4 ({mode}): tick 5 benar");
            m.Board.PlaceAtTick(0, 4);
            Check(m.Check().outcome == LatihanOutcome.Wrong, $"1 1/4 ({mode}): tick 4 salah");
        }
    }

    void TestLockConfig()
    {
        var q = new NumberLineQuestion
        {
            kind = QuestionKind.MultiPion, presetLength = 2,
            pions = new List<PionAnswer> { new PionAnswer { value = FV(0, 1, 2) }, new PionAnswer { value = FV(1, 1, 2) } }
        };
        LatihanSession locked = Start(q);
        Check(locked.ApplyConfig(1, 5) == LatihanConfigResult.Locked && locked.Config.length == 2, "terkunci: panjang tidak bisa diubah");

        q.lockConfig = false;
        LatihanSession s = Start(q);
        MultiPionBoard b = s.Board;
        b.PlaceAtTick(0, 1);     // 1/2
        b.PlaceAtTick(1, 3);     // 3/2
        Check(s.ApplyConfig(9, 5) == LatihanConfigResult.LengthOutOfRange, "terbuka: panjang di luar batas ditolak");
        Check(s.ApplyConfig(2, 5) == LatihanConfigResult.Unchanged, "panjang yang sama: tidak berubah");
        Check(s.ApplyConfig(1, 9) == LatihanConfigResult.Applied && s.Config.length == 1, "panjang jadi 1");
        Check(s.Config.partisi == 2, "partisi garis tetap mengikuti pion, argumen partisi diabaikan");
        Check(b.IsPlaced(0) && !b.IsPlaced(1), "pion yang jadi di luar garis dicabut, yang lain tetap");
        Check(s.ApplyConfig(2, 5) == LatihanConfigResult.Applied && b.IsPlaced(0) && !b.IsPlaced(1), "panjang kembali 2: pion yang dicabut tidak muncul sendiri");
    }

    void TestFlow()
    {
        var level = new LatihanLevel
        {
            title = "L",
            questions = new List<NumberLineQuestion>
            {
                Marathon(),
                new NumberLineQuestion { kind = QuestionKind.PionOnly, presetLength = 1, presetPartisi = 5, target = FV(0, 3, 5) }
            }
        };
        var s = new LatihanSession(new[] { level }, null, 1);
        s.StartLevel(0);
        PlaceAll(s.Board, 5, 3, 3);
        s.Check();
        Check(s.Advance() == LatihanAdvance.NextQuestion, "soal MultiPion selesai: lanjut");
        Check(s.Board == null && s.Config.partisi == 5, "soal berikutnya (PionOnly): papan hilang, garis dari soal");

        s.RestartLevel();
        Check(s.Board != null && s.Board.PlacedCount == 0 && s.Board.SelectedIndex == -1, "mulai ulang: papan baru, tanpa pion di garis");

        s.ExitLevel();
        Check(s.Board == null, "keluar level: papan hilang");
    }
}
