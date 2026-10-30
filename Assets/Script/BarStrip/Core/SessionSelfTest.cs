using UnityEngine;

public class SessionSelfTest : MonoBehaviour
{
    static readonly Fraction One = Fraction.Whole(1);
    static Fraction U(long den) => Fraction.Unit(den);

    int failed;

    void Check(bool ok, string label)
    {
        if (ok) Debug.Log("ok: " + label);
        else { failed++; Debug.LogError("FAIL: " + label); }
    }

    bool DropN(FractionSession s, bool intoA, Fraction piece, int n)
    {
        for (int i = 0; i < n; i++)
            if (s.TryDropToKotak(intoA, piece) != RejectReason.None) return false;
        return true;
    }

    // bangun satu row n/den lewat merge otomatis (Kotak harus masih kosong)
    void BuildRow(FractionSession s, bool intoA, long den, int n)
    {
        for (int i = 0; i < n; i++) s.TryDropToKotak(intoA, U(den));
    }

    // Contoh klien: A = [1, 1/2], B = [3/4] -> 1 1/2 + 3/4 = 2 1/4 (sisa 1/4)
    FractionSession ClientExample(SessionOptions options = null)
    {
        var s = new FractionSession(Operation.Add, options);
        s.TryDropToKotak(true, One);
        s.TryDropToKotak(true, U(2));
        BuildRow(s, false, 4, 3);
        s.CommitProblem();
        return s;
    }

    // Soal 1 - 1/4 = 3/4 untuk menguji perilaku dpz lama (Subtract)
    FractionSession SubtractExample()
    {
        var s = new FractionSession(Operation.Subtract);
        s.TryDropToKotak(true, One);
        s.TryDropToKotak(false, U(4));
        s.CommitProblem();
        return s;
    }

    [ContextMenu("Run Session Tests")]
    void Run()
    {
        failed = 0;

        // 1. Add: jalur normal; hanya sisa pecahan yang dicek (default)
        var pre = new FractionSession(Operation.Add);
        Check(pre.TryDropToDpz(U(2)) == RejectReason.SoalBelumDibentuk, "dpz ditolak sebelum + ditekan");
        Check(pre.Check() == null, "Cek Hasil sebelum + -> null");

        var s = ClientExample();
        Check(s.CurrentPhase == Phase.Merakit, "+ lolos -> Merakit");
        Check(s.Current.result.ValueEquals(new Fraction(9, 4)), "1 1/2 + 3/4 = 9/4");
        Check(s.Check() == CheckVerdict.Kurang && s.CurrentPhase == Phase.Dicek, "area bawah kosong vs sisa 1/4 -> Kurang");
        Check(s.TryDropToDpz(U(4)) == RejectReason.None && s.CurrentPhase == Phase.Merakit, "sisa 1/4 diterima, ubah area -> balik Merakit");
        Check(s.Check() == CheckVerdict.Benar, "sisa 1/4 -> Benar (hanya sisa yang dicek)");
        s.TryDropToDpz(U(4));
        Check(s.Check() == CheckVerdict.Kelebihan, "2/4 vs sisa 1/4 -> Kelebihan");

        // 2. Area bawah: hanya pecahan, satu satuan
        var s2 = ClientExample();
        Check(s2.TryDropToDpz(U(8)) == RejectReason.None, "sisa: 1/8 diterima");
        Check(s2.TryDropToDpz(U(24)) == RejectReason.LowerSatuanBeda, "sisa: 1/24 ditolak (satuan sudah 1/8)");
        Check(s2.TryDropToDpz(One) == RejectReason.LowerHanyaPecahan, "sisa: bilangan bulat ditolak");
        Check(s2.TryDropToDpz(U(8)) == RejectReason.None && s2.Dpz.Count == 1 && s2.Dpz.Pieces[0].numerator == 2, "satuan sama menyatu: 2/8");
        Check(s2.Check() == CheckVerdict.Benar, "2/8 senilai 1/4 -> Benar");
        s2.ClearDpz();
        bool enam = true;
        for (int i = 0; i < 6; i++) enam &= s2.TryDropToDpz(U(24)) == RejectReason.None;
        Check(enam && s2.Check() == CheckVerdict.Benar, "6 x 1/24 senilai 1/4 -> Benar juga");
        s2.ClearDpz();
        s2.TryDropToDpz(U(8));
        Check(s2.Check() == CheckVerdict.Kurang, "1/8 vs 1/4 -> Kurang");
        s2.TryDropToDpz(U(8)); s2.TryDropToDpz(U(8));
        Check(s2.Check() == CheckVerdict.Kelebihan, "3/8 vs 1/4 -> Kelebihan");

        // 3. Gate penyebut di tombol +
        var s3 = new FractionSession(Operation.Add);
        s3.TryDropToKotak(true, U(7)); s3.TryDropToKotak(false, U(4));
        Check(s3.CommitProblem() == ProblemError.PenyebutTerlaluRumit && s3.CurrentPhase == Phase.Menyusun,
              "1/7 + 1/4 (penyebut 28) ditolak di +");
        Check(new FractionSession(Operation.Add).CommitProblem() == ProblemError.KotakKosong, "+ dengan Kotak kosong ditolak");

        // 4. Subtract: hasil harus > 0
        var s4 = new FractionSession(Operation.Subtract);
        s4.TryDropToKotak(true, U(2)); BuildRow(s4, false, 4, 3);
        Check(s4.CommitProblem() == ProblemError.HasilTidakPositif, "1/2 - 3/4 ditolak (negatif)");
        var s4b = new FractionSession(Operation.Subtract);
        s4b.TryDropToKotak(true, U(2)); s4b.TryDropToKotak(false, U(2));
        Check(s4b.CommitProblem() == ProblemError.HasilTidakPositif, "1/2 - 1/2 = 0 ditolak");

        // 5. Subtract: jalur normal + nilai sama (3/6 == 1/2), perilaku dpz lama
        var s5 = new FractionSession(Operation.Subtract);
        BuildRow(s5, true, 6, 4); s5.TryDropToKotak(false, U(6));
        Check(s5.CommitProblem() == ProblemError.None && s5.Current.result.ValueEquals(new Fraction(1, 2)), "4/6 - 1/6 = 1/2");
        s5.TryDropToDpz(U(2));
        Check(s5.Check() == CheckVerdict.Benar, "dpz satu balok 1/2 -> Benar");
        s5.ClearDpz();
        bool tiga = true;
        for (int i = 0; i < 3; i++) tiga &= s5.TryDropToDpz(U(6)) == RejectReason.None;
        Check(tiga && s5.Check() == CheckVerdict.Benar, "dpz tiga balok 1/6 -> Benar juga (nilai sama)");

        // 6. Add: cap hasil saat drop (A = 6 maksimal; B + 1 utuh -> 7 ditolak)
        var s6 = new FractionSession(Operation.Add);
        Check(DropN(s6, true, One, 4) && DropN(s6, true, U(2), 4), "A: 4 bulat + dua row 2/2 = 6 diterima");
        Check(s6.A.Count == 6 && s6.A.Total().ValueEquals(Fraction.Whole(6)), "A bernilai 6");
        Check(s6.TryDropToKotak(false, One) == RejectReason.HasilTerlaluBesar, "B: +1 utuh -> hasil 7, ditolak");
        Check(s6.TryDropToKotak(false, U(2)) == RejectReason.None, "B: +1/2 -> 6 1/2, diterima");
        Check(s6.TryDropToKotak(false, U(2)) == RejectReason.HasilTerlaluBesar, "B: +1/2 lagi (jadi 2/2) -> 7, ditolak");

        // 7. Subtract: cap hasil tidak dicek per drop; hasil maksimal 6 - 1 = 5
        var s7 = new FractionSession(Operation.Subtract);
        Check(DropN(s7, true, One, 4) && DropN(s7, true, U(2), 4), "Subtract: A = 6 diterima");
        Check(s7.CommitProblem() == ProblemError.KotakKosong, "B masih kosong");
        Check(DropN(s7, false, One, 4), "B: 4 utuh");
        Check(s7.TryDropToKotak(false, One) == RejectReason.KotakBulatPenuh, "B: utuh ke-5 ditolak");
        Check(s7.CommitProblem() == ProblemError.None && s7.Current.result.ValueEquals(Fraction.Whole(2)), "6 - 4 = 2 lolos");

        // 8. Edit Kotak setelah + -> area dikosongkan, balik Menyusun
        var s8 = new FractionSession(Operation.Add);
        s8.TryDropToKotak(true, U(2)); s8.TryDropToKotak(false, U(4)); s8.CommitProblem(); s8.TryDropToDpz(U(4));
        Check(s8.KotakEditClearsDpz, "setelah +, edit Kotak bakal hapus area");
        s8.TryDropToKotak(true, U(2));
        Check(s8.CurrentPhase == Phase.Menyusun && s8.Dpz.IsEmpty && s8.LastVerdict == null, "edit Kotak -> Menyusun, area kosong");

        // 9. Area bawah (sisa) berhenti di penuh
        var s9 = new FractionSession(Operation.Add);
        s9.TryDropToKotak(true, U(2)); s9.TryDropToKotak(false, U(3)); s9.CommitProblem();
        bool tigaSepertiga = true;
        for (int i = 0; i < 3; i++) tigaSepertiga &= s9.TryDropToDpz(U(3)) == RejectReason.None;
        Check(tigaSepertiga && s9.Dpz.Count == 1 && s9.Dpz.Pieces[0].numerator == 3, "sisa: 3/3 masih muat");
        Check(s9.TryDropToDpz(U(3)) == RejectReason.DpzPenuh && s9.Dpz.Count == 1, "sisa: lewat 1 ditolak");
        s9.RemoveUnitFromDpz(0);
        Check(s9.TryDropToDpz(U(3)) == RejectReason.None && s9.Dpz.Pieces[0].numerator == 3, "-1 satuan lalu tambah lagi diterima");

        // 10. Preview tidak mengubah state
        var s10 = new FractionSession(Operation.Add);
        Check(s10.PreviewDropToKotak(true, One) == RejectReason.None && s10.A.IsEmpty, "Preview: diterima, tapi A tetap kosong");
        DropN(s10, true, One, 4); DropN(s10, true, U(2), 4);
        Check(s10.PreviewDropToKotak(false, One) == RejectReason.HasilTerlaluBesar && s10.B.IsEmpty, "Preview: ditolak, B tetap kosong");
        Check(s10.PreviewDropToDpz(U(2)) == RejectReason.SoalBelumDibentuk, "Preview dpz sebelum +");
        var s11 = new FractionSession(Operation.Add);
        s11.TryDropToKotak(true, U(2)); s11.TryDropToKotak(false, U(4)); s11.CommitProblem();
        Check(s11.PreviewDropToDpz(U(4)) == RejectReason.None && s11.Dpz.IsEmpty, "Preview dpz: diterima, dpz tetap kosong");

        // 12. Row penuh: potongan berikutnya jadi row baru
        var s12 = new FractionSession(Operation.Add);
        BuildRow(s12, true, 4, 4);
        Check(s12.A.Count == 1 && s12.A.Pieces[0].numerator == 4, "A: satu row 4/4");
        Check(s12.TryDropToKotak(true, U(4), 0) == RejectReason.None, "drop 1/4 di atas row penuh diterima");
        Check(s12.A.Count == 2 && s12.A.Pieces[0].numerator == 4 && s12.A.Pieces[1].numerator == 1, "jadi row baru [4/4, 1/4]");
        Check(!s12.A.CanMerge(0, U(4)) && s12.A.CanMerge(1, U(4)), "row 4/4 penuh, row 1/4 masih bisa merge");

        // 13. Utuh selalu satu row sendiri
        var s13 = new FractionSession(Operation.Add);
        s13.TryDropToKotak(true, One); s13.TryDropToKotak(true, One, 0);
        Check(s13.A.Count == 2, "dua utuh jadi dua row");

        // 14. Area bawah (Add): merge sampai penuh, lalu ditolak
        var s14 = new FractionSession(Operation.Add);
        s14.TryDropToKotak(true, One); s14.TryDropToKotak(false, U(4)); s14.CommitProblem();   // 1 + 1/4
        s14.TryDropToDpz(U(4)); s14.TryDropToDpz(U(4));
        Check(s14.Dpz.Count == 1 && s14.Dpz.Pieces[0].numerator == 2, "zona luar: 1/4 + 1/4 merge jadi 2/4");
        s14.TryDropToDpz(U(4)); s14.TryDropToDpz(U(4));
        Check(s14.Dpz.Count == 1 && s14.Dpz.Pieces[0].numerator == 4, "merge sampai 4/4");
        Check(s14.TryDropToDpz(U(4)) == RejectReason.DpzPenuh && s14.Dpz.Count == 1, "4/4 penuh: sisa tidak boleh melewati 1");

        // 15 sampai 19: perilaku dpz lama (Subtract, dipakai Media 6 sampai 4R-e)
        // 15. dpz: drop persis di atas blok
        var s15 = SubtractExample();
        s15.TryDropToDpz(U(4));        // blok 0: 1/4
        s15.TryDropToDpz(U(3));        // blok 1: 1/3
        s15.TryDropToDpz(U(3), 0);     // persis di atas 1/4 tapi penyebut beda -> blok baru
        Check(s15.Dpz.Count == 3 && s15.Dpz.Pieces[2].denominator == 3, "di atas blok beda satuan: jadi blok baru");
        s15.TryDropToDpz(U(4), 0);     // persis di atas 1/4 -> merge
        Check(s15.Dpz.Count == 3 && s15.Dpz.Pieces[0].numerator == 2, "di atas blok sejenis: merge");
        Check(s15.FindDpzMergeTarget(U(3)) == 1, "zona luar: blok 1/3 pertama yang belum penuh menerima 1/3");

        // 16. Nilai pas tapi satuan campur (Subtract: 1 - 1/4 = 3/4)
        var s16 = SubtractExample();
        s16.TryDropToDpz(U(2)); s16.TryDropToDpz(U(4));
        Check(s16.Check() == CheckVerdict.NilaiPasSatuanBeda, "1/2 + 1/4 = 3/4 tapi satuan campur");
        Check(s16.CurrentPhase == Phase.Dicek && s16.LastVerdict != CheckVerdict.Benar, "belum Benar, hasil di Soal tetap ?");
        s16.ClearDpz();
        for (int i = 0; i < 3; i++) s16.TryDropToDpz(U(4));
        Check(s16.Dpz.Count == 1 && s16.Check() == CheckVerdict.Benar, "3 x 1/4 (satu blok 3/4) -> Benar");

        // 17. Batas jumlah blok: blok baru ditolak, merge tetap boleh
        var s17 = SubtractExample();
        bool penuh = true;
        for (int den = 2; den < 2 + GameRules.MaxDpzBlocks; den++) penuh &= s17.TryDropToDpz(U(den)) == RejectReason.None;
        Check(penuh && s17.Dpz.Count == GameRules.MaxDpzBlocks, "blok sebanyak batas diterima");
        Check(s17.TryDropToDpz(U(2 + GameRules.MaxDpzBlocks)) == RejectReason.DpzPenuh, "blok melebihi batas ditolak");
        Check(s17.TryDropToDpz(U(2)) == RejectReason.None && s17.Dpz.Count == GameRules.MaxDpzBlocks, "merge ke blok yang ada tetap boleh");

        // 18. -1 satuan dan hapus blok
        var s18 = SubtractExample();
        s18.TryDropToDpz(U(4)); s18.TryDropToDpz(U(4)); s18.TryDropToDpz(U(4));   // satu blok 3/4
        s18.RemoveUnitFromDpz(0);
        Check(s18.Dpz.Count == 1 && s18.Dpz.Pieces[0].numerator == 2, "3/4 -1 satuan = 2/4");
        s18.RemoveFromDpz(0);
        Check(s18.Dpz.IsEmpty, "hapus blok utuh");

        // 19. Gabung antar-blok dpz lewat sesi
        var s19 = new FractionSession(Operation.Subtract);
        s19.TryDropToKotak(true, One); s19.TryDropToKotak(false, U(5)); s19.CommitProblem();
        for (int i = 0; i < 5; i++) s19.TryDropToDpz(U(5));          // satu blok 5/5
        s19.TryDropToDpz(U(5), 0);                                   // persis di atas blok penuh -> blok baru 1/5
        s19.RemoveUnitFromDpz(0); s19.RemoveUnitFromDpz(0);          // blok 0 jadi 3/5
        Check(s19.Dpz.Count == 2 && s19.Dpz.Pieces[0].numerator == 3 && s19.Dpz.Pieces[1].numerator == 1, "susunan [3/5, 1/5]");
        Check(s19.PreviewMergeDpzBlocks(1, 0) == 1, "prediksi: 1 satuan pindah");
        Check(s19.MergeDpzBlocks(1, 0) == 1 && s19.Dpz.Count == 1 && s19.Dpz.Pieces[0].numerator == 4, "gabung: [3/5, 1/5] -> [4/5]");

        // 20. Area atas: pindah balok dari Kotak (mode pindah, default), urutan asal tercatat
        var s20 = ClientExample();
        Check(s20.PreviewMoveToArea(true, 0) == RejectReason.None, "preview pindah: diterima");
        Check(s20.TryMoveToArea(true, 0) == RejectReason.None && s20.Upper.Count == 1 && s20.A.Count == 1 && s20.A.Pieces[0].denominator == 2,
              "bilangan bulat A pindah ke area, A tersisa 1/2");
        Check(s20.CurrentPhase == Phase.Merakit && s20.Current.result.ValueEquals(new Fraction(9, 4)), "memindah balok tidak mereset soal");
        Check(s20.TryMoveToArea(true, 0) == RejectReason.None && s20.A.IsEmpty && s20.Upper.Blocks[1].originIndex == 1,
              "1/2 tercatat berasal dari indeks 1 walau sekarang indeks 0");
        Check(s20.TryMoveToArea(false, 0) == RejectReason.None && s20.B.IsEmpty && s20.Upper.Count == 3, "3/4 dari B ikut pindah");
        Check(s20.Upper.FullRowCount == 1 && s20.Upper.PartialTotal().ValueEquals(new Fraction(5, 4)), "area atas: 1 row penuh + sebagian 5/4");
        Check(s20.Upper.CarryWholes == 1 && s20.Upper.WholeCount == 2, "carry 1, jadi 2 bilangan bulat");
        Check(s20.Upper.Total().ValueEquals(new Fraction(9, 4)), "total area atas 9/4");
        Check(s20.IsKotakLocked(true) && s20.IsKotakLocked(false), "kedua Kotak terkunci selama balok mereka di area");

        Check(s20.ReturnToKotak(1) && s20.A.Count == 1 && s20.A.Pieces[0].denominator == 2, "kembalikan 1/2 dulu: A = [1/2]");
        Check(s20.ReturnToKotak(0) && s20.A.Count == 2 && s20.A.Pieces[0].denominator == 1 && s20.A.Pieces[1].denominator == 2,
              "kembalikan bilangan bulat: urutan asal pulih [1, 1/2]");
        Check(s20.Upper.Count == 1 && !s20.IsKotakLocked(true) && s20.IsKotakLocked(false), "A tidak terkunci lagi, B masih");
        Check(!s20.ReturnToKotak(5), "indeks di luar jangkauan gagal");
        s20.ReturnAllToKotak();
        Check(s20.Upper.IsEmpty && s20.B.Count == 1 && s20.B.Pieces[0].numerator == 3, "kembalikan semua");

        // 21. Cek bilangan bulat (CheckWholes): sisa dulu, lalu bulat
        var s21 = ClientExample(new SessionOptions { CheckWholes = true });
        s21.TryDropToDpz(U(8)); s21.TryDropToDpz(U(8));
        Check(s21.Check() == CheckVerdict.UtuhBelumSesuai, "sisa pas, tapi area atas masih kosong");
        s21.TryMoveToArea(true, 0);        // 1
        Check(s21.Check() == CheckVerdict.UtuhBelumSesuai, "baru 1 dari 2 bilangan bulat");
        s21.TryMoveToArea(true, 0);        // 1/2
        s21.TryMoveToArea(false, 0);       // 3/4
        Check(s21.Check() == CheckVerdict.Benar, "1 + carry 1 = 2 bilangan bulat dan sisa pas -> Benar");
        s21.ClearDpz();
        Check(s21.Check() == CheckVerdict.Kurang, "sisa yang salah didahulukan: Kurang");

        // 22. Mode salin
        var s22 = ClientExample(new SessionOptions { MoveFromKotak = false });
        Check(s22.TryMoveToArea(true, 0) == RejectReason.None && s22.A.Count == 2 && s22.Upper.Count == 1, "mode salin: Kotak A tetap utuh");
        Check(s22.TryMoveToArea(true, 0) == RejectReason.BlokSudahDiArea && s22.Upper.Count == 1, "row yang sama tidak bisa disalin dua kali");
        Check(s22.TryMoveToArea(true, 1) == RejectReason.None && s22.TryMoveToArea(false, 0) == RejectReason.None && s22.Upper.Count == 3,
              "row lain tetap bisa disalin");
        Check(!s22.IsKotakLocked(true) && !s22.IsKotakLocked(false), "mode salin: Kotak tidak terkunci");
        Check(s22.ReturnToKotak(0) && s22.A.Count == 2 && s22.Upper.Count == 2, "hapus salinan: Kotak tetap utuh");
        Check(s22.TryMoveToArea(true, 0) == RejectReason.None, "setelah dihapus, row bisa disalin lagi");
        Check(s22.TryDropToKotak(true, U(2)) == RejectReason.None && s22.CurrentPhase == Phase.Menyusun && s22.Upper.IsEmpty,
              "mengubah Kotak mereset soal dan mengosongkan area");

        // 23. Kunci Kotak (mode pindah) dan pemulihan balok saat Kotak lain diubah
        var s23 = ClientExample();
        s23.TryMoveToArea(true, 0);        // bilangan bulat A keluar
        Check(s23.IsKotakLocked(true) && !s23.IsKotakLocked(false), "A terkunci, B tidak");
        Check(s23.TryDropToKotak(true, U(2)) == RejectReason.KotakTerkunci && s23.PreviewDropToKotak(true, U(2)) == RejectReason.KotakTerkunci,
              "drop ke Kotak terkunci ditolak");
        int rows = s23.A.Count;
        s23.RemoveFromKotak(true, 0);
        s23.ClearKotak(true);
        Check(s23.A.Count == rows, "hapus dan kosongkan Kotak terkunci tidak melakukan apa-apa");
        Check(s23.TryDropToKotak(false, U(4)) == RejectReason.None, "B boleh diubah (3/4 jadi 4/4)");
        Check(s23.CurrentPhase == Phase.Menyusun && s23.Upper.IsEmpty, "mengubah B mereset soal dan mengosongkan area");
        Check(s23.A.Count == 2 && s23.A.Pieces[0].denominator == 1 && s23.A.Pieces[1].denominator == 2,
              "balok A yang ada di area dikembalikan, urutan asal [1, 1/2]");
        Check(s23.B.Count == 1 && s23.B.Pieces[0].numerator == 4, "B = [4/4]");

        // 24. Penolakan dan transisi fase
        Check(new FractionSession(Operation.Add).TryMoveToArea(true, 0) == RejectReason.SoalBelumDibentuk, "pindah sebelum + ditolak");
        Check(SubtractExample().TryMoveToArea(true, 0) == RejectReason.AreaTidakTersedia, "area dua bagian hanya untuk Add");
        var s24 = ClientExample();
        Check(s24.TryMoveToArea(true, 9) == RejectReason.AreaTidakTersedia && s24.TryMoveToArea(false, -1) == RejectReason.AreaTidakTersedia,
              "indeks row yang tidak ada ditolak");
        s24.TryDropToDpz(U(4));
        Check(s24.Check() == CheckVerdict.Benar && s24.CurrentPhase == Phase.Dicek, "sisa 1/4 -> Benar");
        s24.TryMoveToArea(true, 0);
        Check(s24.CurrentPhase == Phase.Merakit && s24.LastVerdict == null, "memindah balok setelah dicek: balik Merakit");
        s24.Check();
        s24.ReturnToKotak(0);
        Check(s24.CurrentPhase == Phase.Merakit, "mengembalikan balok setelah dicek: balik Merakit");

        // 25. Reset semua mengosongkan area atas dan bawah
        var s25 = ClientExample();
        s25.TryMoveToArea(true, 0); s25.TryMoveToArea(false, 0); s25.TryDropToDpz(U(4));
        s25.ResetAll();
        Check(s25.A.IsEmpty && s25.B.IsEmpty && s25.Upper.IsEmpty && s25.Dpz.IsEmpty, "reset: Kotak dan area kosong");
        Check(s25.CurrentPhase == Phase.Menyusun && s25.LastVerdict == null, "reset: balik Menyusun");

        // 26. Area atas penuh pada batasnya
        var s26 = ClientExample();
        Check(s26.TryMoveToArea(true, 0) == RejectReason.None && s26.Upper.Count <= GameRules.MaxUpperBlocks, "area atas di bawah batas");

        Debug.Log(failed == 0 ? "SEMUA LOLOS (session)" : failed + " tes GAGAL (session)");
    }
}