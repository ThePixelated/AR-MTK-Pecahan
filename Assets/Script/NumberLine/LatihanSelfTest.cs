using System.Collections.Generic;
using UnityEngine;

public class LatihanSelfTest : MonoBehaviour
{
    int pass, fail;

    [ContextMenu("Run Latihan Tests")]
    public void Run()
    {
        pass = fail = 0;
        TestValidate();
        TestLevel1Flow();
        TestLevel1Variants();
        TestLevel2();
        TestLevel3();
        TestScaffolds();
        string summary = $"Latihan tests: {pass} lulus, {fail} gagal";
        if (fail == 0) Debug.Log(summary); else Debug.LogError(summary);
    }

    void Check(bool ok, string name)
    {
        if (ok) pass++;
        else { fail++; Debug.LogError("GAGAL: " + name); }
    }

    static FractionValue FV(int w, int n, int d) => new FractionValue(w, n, d);

    static void Type(NumberLineSoalInput s, NumberLineCell cell, string digits)
    {
        foreach (char ch in digits) s.TypeDigit(cell, ch - '0');
    }

    // Isi satu panel dari teks; string kosong = kotak dibiarkan kosong
    static void Fill(NumberLineSoalInput s, string whole, string num, string den)
    {
        s.Clear();
        Type(s, NumberLineCell.Whole, whole);
        Type(s, NumberLineCell.Numerator, num);
        Type(s, NumberLineCell.Denominator, den);
    }

    // ---------- data contoh ----------
    static NumberLineQuestion LineOnlyQ() => new NumberLineQuestion
    {
        kind = QuestionKind.LineOnly,
        lineAnswer = new LineAnswer(1, 4),
        alternativeLineAnswers = new List<LineAnswer> { new LineAnswer(2, 8) }
    };

    static NumberLineQuestion PionOnlyQ() => new NumberLineQuestion
    {
        kind = QuestionKind.PionOnly,
        presetLength = 1,
        presetPartisi = 5,
        target = FV(0, 3, 5)
    };

    static NumberLineQuestion LineAndPionQ() => new NumberLineQuestion
    {
        kind = QuestionKind.LineAndPion,
        target = FV(1, 1, 4)           // 1 1/4 = 5/4
    };

    static NumberLineQuestion MultiQ() => new NumberLineQuestion
    {
        kind = QuestionKind.MultiPosition,
        presetLength = 2,
        presetPartisi = 4,
        markers = new List<FractionValue> { FV(0, 1, 4), FV(0, 3, 4), FV(1, 2, 4) }       // A = 1/4, B = 3/4, C = 1 1/2
    };

    static NumberLineQuestion ExactQ() => new NumberLineQuestion { kind = QuestionKind.Comparison, rule = ComparisonRule.Exact, exact = FV(0, 3, 8) };

    static NumberLineQuestion BetweenQ() => new NumberLineQuestion
    {
        kind = QuestionKind.Comparison,
        rule = ComparisonRule.StrictlyBetween,
        lowerBound = FV(0, 1, 2),
        upperBound = FV(0, 3, 4)
    };

    static List<LatihanLevel> Levels(bool stay = true) => new List<LatihanLevel>
    {
        new LatihanLevel { title = "Level 1", stayUntilCorrect = stay, questions = new List<NumberLineQuestion> { LineOnlyQ(), PionOnlyQ(), LineAndPionQ() } },
        new LatihanLevel { title = "Level 2", stayUntilCorrect = stay, questions = new List<NumberLineQuestion> { MultiQ() } },
        new LatihanLevel { title = "Level 3", stayUntilCorrect = stay, questions = new List<NumberLineQuestion> { ExactQ(), BetweenQ() } }
    };

    static LatihanSession Open(LatihanOptions o = null, bool stay = true) =>
        new LatihanSession(Levels(stay), o ?? new LatihanOptions { UnlockAllLevels = true }, 1);

    // ---------- tes ----------
    void TestValidate()
    {
        var limits = new NumberLineLimits();
        Check(LineOnlyQ().Validate(limits, 3).Count == 0 && PionOnlyQ().Validate(limits, 3).Count == 0
              && LineAndPionQ().Validate(limits, 3).Count == 0 && MultiQ().Validate(limits, 3).Count == 0
              && ExactQ().Validate(limits, 3).Count == 0 && BetweenQ().Validate(limits, 3).Count == 0, "soal contoh semuanya sah");

        var badLine = LineOnlyQ(); badLine.lineAnswer = new LineAnswer(6, 5);
        Check(badLine.Validate(limits, 3).Count == 1, "LineOnly: panjang 6 di luar batas");

        var badPion = PionOnlyQ(); badPion.target = FV(0, 1, 3);
        Check(badPion.Validate(limits, 3).Count == 1, "PionOnly: 1/3 tidak jatuh di tick garis 1 x 5");

        var badBoth1 = LineAndPionQ(); badBoth1.target = FV(0, 1, 16);
        Check(badBoth1.Validate(limits, 3).Count == 1, "LineAndPion: 1/16 butuh partisi 16");
        var badBoth2 = LineAndPionQ(); badBoth2.target = FV(6, 0, 1);
        Check(badBoth2.Validate(limits, 3).Count == 1, "LineAndPion: 6 melebihi panjang maksimal");

        var badMulti = MultiQ(); badMulti.markers.Add(FV(0, 1, 3));
        Check(badMulti.Validate(limits, 3).Count >= 2, "MultiPosition: titik ke-4 melebihi batas dan 1/3 tidak jatuh di tick");

        var badBetween = BetweenQ(); badBetween.lowerBound = FV(0, 3, 4);
        Check(badBetween.Validate(limits, 3).Count == 1, "Comparison: batas bawah tidak lebih kecil dari batas atas");

        Check(FV(1, 3, 5).ToText() == "1 3/5" && FV(0, 3, 5).ToText() == "3/5" && FV(2, 0, 1).ToText() == "2", "teks nilai");
        Check(FV(1, 3, 5).ToFraction().ValueEquals(new Fraction(8, 5)), "1 3/5 = 8/5");

        var q = PionOnlyQ(); q.text = "Tentukan posisi {target}";
        Check(q.FormattedText() == "Tentukan posisi 3/5", "{target} diganti nilai target");

        Check(QuestionLayout.For(LineOnlyQ()).lineUnlocked && !QuestionLayout.For(LineOnlyQ()).showPion, "layout LineOnly");
        QuestionLayout lp = QuestionLayout.For(PionOnlyQ());
        Check(lp.showLine && !lp.lineUnlocked && lp.showPion && lp.pionUnlocked && lp.panelCount == 0, "layout PionOnly");
        QuestionLayout lm = QuestionLayout.For(MultiQ());
        Check(lm.showMarkers && !lm.showPion && lm.panelCount == 3 && lm.NeedsNumpad, "layout MultiPosition");
        QuestionLayout lc = QuestionLayout.For(ExactQ());
        Check(!lc.showLine && lc.panelCount == 1 && lc.NeedsNumpad, "layout Comparison");
    }

    void TestLevel1Flow()
    {
        var s = new LatihanSession(Levels(), new LatihanOptions(), 1);
        Check(s.IsUnlocked(0) && !s.IsUnlocked(1) && !s.IsUnlocked(2), "hanya Level 1 yang terbuka");
        Check(s.StartLevel(1) == LatihanStart.Locked, "Level 2 terkunci");
        Check(s.StartLevel(9) == LatihanStart.InvalidLevel, "level tidak ada");
        Check(s.StartLevel(0) == LatihanStart.Started && s.CurrentLevel == 0 && s.QuestionIndex == 0 && s.QuestionCount == 3, "mulai Level 1");

        // soal 1: LineOnly
        Check(s.Current.kind == QuestionKind.LineOnly && s.Layout.lineUnlocked && !s.Layout.showPion, "soal 1: LineOnly");
        Check(s.Config.length == 1 && s.Config.partisi == 5, "garis mulai dari default 1 x 5");
        Check(s.Check().outcome == LatihanOutcome.Wrong && !s.CanAdvance, "1 x 5 salah, belum boleh lanjut");
        Check(s.Advance() == LatihanAdvance.NotAllowed, "Advance ditolak");
        Check(s.SetPionTick(3) == 0, "pion terkunci");
        Check(s.ApplyConfig(1, 4) == LatihanConfigResult.Applied, "atur garis 1 x 4");
        Check(s.ApplyConfig(1, 4) == LatihanConfigResult.Unchanged, "nilai sama: tidak berubah");
        Check(s.ApplyConfig(9, 4) == LatihanConfigResult.LengthOutOfRange && s.ApplyConfig(1, 99) == LatihanConfigResult.PartisiOutOfRange, "di luar batas ditolak");
        Check(s.Check().outcome == LatihanOutcome.Correct && s.IsSolved && s.CanAdvance, "1 x 4 benar");
        Check(s.Advance() == LatihanAdvance.NextQuestion && s.QuestionIndex == 1, "lanjut ke soal 2");

        // soal 2: PionOnly (garis tetap 1 x 5, target 3/5)
        Check(s.Layout.showPion && s.Layout.pionUnlocked && !s.Layout.lineUnlocked, "soal 2: PionOnly");
        Check(s.Config.length == 1 && s.Config.partisi == 5, "garis tetap 1 x 5");
        Check(s.ApplyConfig(2, 4) == LatihanConfigResult.Locked && s.Config.partisi == 5, "atur garis terkunci");
        Check(!s.IsSolved, "status terjawab direset untuk soal baru");
        Check(s.SetPionTick(2) == 2 && s.Check().outcome == LatihanOutcome.Wrong, "tick 2 salah");
        Check(s.SetPionTick(3) == 3 && s.Check().outcome == LatihanOutcome.Correct, "tick 3 benar");
        Check(s.SetPionTick(99) == 5, "tick dibatasi ke ujung garis");
        Check(s.Advance() == LatihanAdvance.NextQuestion, "lanjut ke soal 3");

        // soal 3: LineAndPion (target 1 1/4 = 5/4)
        Check(s.Layout.lineUnlocked && s.Layout.pionUnlocked, "soal 3: LineAndPion");
        s.SetPionTick(3);
        Check(s.Check().outcome == LatihanOutcome.Wrong, "garis 1 x 5: 1 1/4 tidak terjangkau");
        Check(s.NeedsConfirmBeforeReconfigure, "pion sudah digeser: perlu konfirmasi");
        Check(s.ApplyConfig(2, 5) == LatihanConfigResult.Applied && s.PionTick == 0 && !s.NeedsConfirmBeforeReconfigure, "ubah garis: pion kembali ke 0");
        s.SetPionTick(6);
        Check(s.Check().outcome == LatihanOutcome.Wrong, "garis 2 x 5: 1 1/4 tidak jatuh di tick");
        s.ApplyConfig(1, 4);
        s.SetPionTick(4);
        Check(s.Check().outcome == LatihanOutcome.Wrong, "garis 1 x 4: 1 1/4 melewati garis");
        s.ApplyConfig(2, 4);
        s.SetPionTick(5);
        Check(s.Check().outcome == LatihanOutcome.Correct, "garis 2 x 4, pion di tick 5 = 5/4: benar");
        Check(s.Advance() == LatihanAdvance.LevelCompleted && s.IsFinished && s.Current == null, "Level 1 selesai");
        Check(s.IsCompleted(0) && s.IsUnlocked(1) && !s.IsUnlocked(2), "Level 2 terbuka, Level 3 belum");
        Check(s.Check().outcome == LatihanOutcome.NoQuestion, "tidak ada soal setelah selesai");
        s.ExitLevel();
        Check(s.CurrentLevel == -1 && s.Current == null, "keluar ke pemilih level");
    }

    void TestLevel1Variants()
    {
        var alt = Open();
        alt.StartLevel(0);
        alt.ApplyConfig(2, 8);
        Check(alt.Check().outcome == LatihanOutcome.Correct, "jawaban alternatif 2 x 8 diterima");

        // mode continue: setelah CEK (salah pun) boleh lanjut
        var cont = Open(null, stay: false);
        cont.StartLevel(0);
        Check(!cont.CanAdvance, "continue: sebelum CEK belum boleh lanjut");
        Check(cont.Check().outcome == LatihanOutcome.Wrong && cont.CanAdvance, "continue: setelah salah boleh lanjut");
        Check(cont.Advance() == LatihanAdvance.NextQuestion, "continue: lanjut");

        // CEK yang belum lengkap tidak membuka jalan lanjut
        var inc = Open(null, stay: false);
        inc.StartLevel(1);
        Check(inc.Check().outcome == LatihanOutcome.Incomplete && !inc.HasChecked && !inc.CanAdvance, "belum lengkap: bukan dinilai");
    }

    void TestLevel2()
    {
        var s = Open();
        Check(s.StartLevel(1) == LatihanStart.Started, "mulai Level 2");
        Check(s.Layout.showLine && !s.Layout.lineUnlocked && !s.Layout.showPion && s.Layout.showMarkers && s.Layout.panelCount == 3, "layout Level 2");
        Check(s.Config.length == 2 && s.Config.partisi == 4, "garis tetap 2 x 4");
        Check(s.Soal.Length == 3 && s.MarkerTicks.Length == 3 && s.MarkerTicks[0] == 1 && s.MarkerTicks[1] == 3 && s.MarkerTicks[2] == 6,
              "titik A, B, C di tick 1, 3, 6");

        LatihanCheckResult r = s.Check();
        Check(r.outcome == LatihanOutcome.Incomplete && r.indicators.Length == 3 && s.WrongStrikes == 0, "semua panel kosong: belum lengkap");

        Fill(s.Soal[0], "", "1", "4");        // A = 1/4: benar
        Fill(s.Soal[1], "", "2", "4");        // B = 2/4: salah (seharusnya 3/4)
        Check(s.Check().outcome == LatihanOutcome.Incomplete, "panel C masih kosong: belum lengkap");
        Fill(s.Soal[2], "1", "1", "2");       // C = 1 1/2: benar
        r = s.Check();
        Check(r.outcome == LatihanOutcome.Wrong && !s.CanAdvance, "B salah: Wrong");
        Check(r.indicators[0] == NumberLineIndicator.Correct && r.indicators[1] == NumberLineIndicator.Wrong && r.indicators[2] == NumberLineIndicator.Correct,
              "indikator: hijau, merah, hijau");

        Fill(s.Soal[1], "", "6", "8");        // 6/8 = 3/4: benar menurut nilai
        r = s.Check();
        Check(r.outcome == LatihanOutcome.Correct && r.indicators[1] == NumberLineIndicator.Correct && s.CanAdvance, "6/8 senilai 3/4: semua benar");

        // panel yang sudah benar boleh diubah lagi lalu dinilai ulang
        Fill(s.Soal[0], "", "3", "4");
        Check(s.Check().outcome == LatihanOutcome.Wrong && s.IsSolved, "panel benar diubah jadi salah: dinilai ulang, status terjawab tetap");

        // penyebut 0: dikosongkan dan dianggap belum lengkap
        var z = Open();
        z.StartLevel(1);
        Fill(z.Soal[0], "", "1", "4"); Fill(z.Soal[1], "", "3", "4"); Fill(z.Soal[2], "", "3", "0");
        Check(z.Check().outcome == LatihanOutcome.Incomplete && z.Soal[2].IsEmpty, "penyebut 0: dikosongkan, belum lengkap");

        Fill(s.Soal[0], "", "1", "4");
        Check(s.Advance() == LatihanAdvance.LevelCompleted && s.IsUnlocked(2), "Level 2 selesai membuka Level 3");
    }

    void TestLevel3()
    {
        var s = Open();
        s.StartLevel(2);
        Check(!s.Layout.showLine && s.Layout.panelCount == 1 && !s.Layout.showMarkers, "layout Level 3");
        Check(s.Check().outcome == LatihanOutcome.Incomplete, "kosong: belum lengkap");

        // soal 1: dekat 1/2, 3/8 atau 7/8? Jawaban 3/8
        Fill(s.Soal[0], "", "7", "8");
        Check(s.Check().outcome == LatihanOutcome.Wrong, "7/8 salah");
        Fill(s.Soal[0], "", "3", "8");
        Check(s.Check().outcome == LatihanOutcome.Correct && s.CanAdvance, "3/8 benar");
        Fill(s.Soal[0], "", "6", "16");
        Check(s.Check().outcome == LatihanOutcome.Correct, "6/16 senilai 3/8: benar");
        Check(s.Advance() == LatihanAdvance.NextQuestion && s.Soal[0].IsEmpty, "soal berikutnya dengan panel kosong");

        // soal 2: tepat di antara 1/2 dan 3/4
        var cases = new (string w, string n, string d, bool correct)[]
        {
            ("", "5", "8", true), ("", "2", "3", true), ("", "1", "2", false), ("", "3", "4", false),
            ("", "4", "5", false), ("", "1", "4", false), ("1", "1", "2", false), ("", "9", "16", true)
        };
        foreach (var c in cases)
        {
            Fill(s.Soal[0], c.w, c.n, c.d);
            bool ok = s.Check().outcome == LatihanOutcome.Correct;
            Check(ok == c.correct, $"antara 1/2 dan 3/4: {c.w} {c.n}/{c.d} -> {(c.correct ? "benar" : "salah")}");
        }
        Fill(s.Soal[0], "", "5", "8");
        s.Check();
        Check(s.Advance() == LatihanAdvance.LevelCompleted && s.IsCompleted(2), "Level 3 selesai");
    }

    void TestScaffolds()
    {
        // wrong-strike
        var st = Open(new LatihanOptions { UnlockAllLevels = true, EnableWrongStrike = true, MaxWrongStrikes = 2 });
        st.StartLevel(0);
        Check(st.Check().outcome == LatihanOutcome.Wrong && st.WrongStrikes == 1 && !st.Failed, "salah ke-1: belum gagal");
        Check(st.Check().outcome == LatihanOutcome.Wrong && st.WrongStrikes == 2 && st.FailReason == LatihanFailReason.StrikeLimit, "salah ke-2: level gagal");
        Check(st.Check().outcome == LatihanOutcome.Blocked && st.Advance() == LatihanAdvance.NotAllowed, "setelah gagal: ditolak");
        Check(st.RestartLevel() == LatihanStart.Started && st.WrongStrikes == 0 && !st.Failed && st.QuestionIndex == 0, "ulang level: bersih");

        var off = Open();
        off.StartLevel(0);
        for (int i = 0; i < 5; i++) off.Check();
        Check(off.WrongStrikes == 0 && !off.Failed, "strike mati: tidak pernah gagal");

        // timer per soal
        var tm = Open(new LatihanOptions { UnlockAllLevels = true, EnableTimer = true, TimerSeconds = 5f });
        tm.StartLevel(0);
        Check(Mathf.Approximately(tm.TimeLeft, 5f), "timer mulai 5 detik");
        tm.Tick(3f);
        Check(Mathf.Approximately(tm.TimeLeft, 2f) && !tm.Failed, "tersisa 2 detik");
        tm.Tick(3f);
        Check(tm.TimeLeft <= 0f && tm.FailReason == LatihanFailReason.TimeUp && tm.Check().outcome == LatihanOutcome.Blocked, "waktu habis: level gagal");

        var solved = Open(new LatihanOptions { UnlockAllLevels = true, EnableTimer = true, TimerSeconds = 5f });
        solved.StartLevel(0);
        solved.ApplyConfig(1, 4);
        solved.Check();
        solved.Tick(10f);
        Check(!solved.Failed, "soal sudah benar: timer berhenti");
        solved.Advance();
        Check(Mathf.Approximately(solved.TimeLeft, 5f), "soal baru: timer direset");

        var noTimer = Open();
        noTimer.StartLevel(0);
        noTimer.Tick(100f);
        Check(!noTimer.Failed, "timer mati: tidak berpengaruh");

        // bank soal: acak dan batasi
        var bankOptions = new LatihanOptions { UnlockAllLevels = true, EnableSoalBank = true, BankPickCount = 2 };
        var b1 = new LatihanSession(Levels(), bankOptions, 7);
        var b2 = new LatihanSession(Levels(), bankOptions, 7);
        b1.StartLevel(0); b2.StartLevel(0);
        Check(b1.QuestionCount == 2, "bank: diambil 2 dari 3 soal");
        Check(b1.Current.kind == b2.Current.kind, "seed sama: urutan sama");

        var all = new LatihanSession(Levels(), new LatihanOptions { UnlockAllLevels = true, EnableSoalBank = true }, 7);
        all.StartLevel(0);
        Check(all.QuestionCount == 3, "bank tanpa batas: semua soal diacak");

        int changes = 0;
        var ev = Open();
        ev.Changed += () => changes++;
        ev.StartLevel(0);
        Check(changes >= 1, "Changed terpicu saat level dimulai");
    }
}