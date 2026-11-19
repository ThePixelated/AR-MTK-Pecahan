using System;

public enum Media5OperationMode { SubtractOnly, AddOnly, Both }

// Pengaturan Media 5, bisa diatur dari Inspector lewat controller.
[Serializable]
public class Media5Limits
{
    public NumberLineLimits Line = new NumberLineLimits();                    // panjang, partisi, default (sama dengan Media 2)
    public Media5OperationMode Operations = Media5OperationMode.SubtractOnly; // client meminta pengurangan saja
    public int AntiRepeatWindow = 5;          // soal yang sama tidak muncul lagi dalam N soal terakhir (0 = tanpa memori)
    public int MinPartisiForProblem = 1;      // partisi minimal untuk boleh membuat soal (1 = partisi 1 dibolehkan)
}

// Gagal membuat soal lewat Generate Soal
public enum Media5ProblemError
{
    None,
    PartisiTooSmall,      // partisi lebih kecil dari MinPartisiForProblem
    NoProblemPossible     // garis terlalu pendek: tidak ada soal yang sah (MaxTick < 2)
}

// Gagal memakai soal isian siswa lewat Enter Soal
public enum Media5EnterError
{
    None,
    AMissing, BMissing,               // belum diisi sama sekali
    AIncomplete, BIncomplete,         // pembilang atau penyebut terisi sebelah saja
    AImpossible, BImpossible,         // penyebut 0
    ANotOnTick, BNotOnTick,           // jatuh di antara dua tick garis aktif
    AExceedsLine, BExceedsLine,       // melewati panjang garis
    BZero,                            // B harus lebih dari 0
    AMustBeGreater                    // A harus lebih besar dari B
}

public enum Media5JumpResult { Moved, NoProblem, AtStart }
public enum Media5RetryResult { Moved, NoProblem, AlreadyAtStart }

// Keadaan isi Panel Jawaban saat Cek Hasil
public enum Media5AnswerState
{
    Empty,        // belum diisi
    Incomplete,   // pembilang atau penyebut sebelah saja, atau penyebut 0
    Wrong,
    Correct
}

public enum Media5CheckOutcome
{
    NoProblem,
    Correct,             // posisi pion benar dan jawaban benar
    WrongPosition,       // jawaban benar, posisi pion salah
    WrongAnswer,         // posisi pion benar, jawaban salah
    WrongBoth,           // dua-duanya belum benar (jawaban salah, kosong, atau belum lengkap)
    AnswerEmpty,         // posisi pion benar, jawaban belum diisi
    AnswerIncomplete     // posisi pion benar, jawaban belum lengkap
}

public readonly struct Media5CheckResult
{
    public readonly Media5CheckOutcome Outcome;
    public readonly Media5AnswerState AnswerState;

    public Media5CheckResult(Media5CheckOutcome outcome, Media5AnswerState answerState)
    {
        Outcome = outcome;
        AnswerState = answerState;
    }
}