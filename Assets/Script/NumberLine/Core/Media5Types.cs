using System;

public enum Media5OperationMode { SubtractOnly, AddOnly, Both }

// Pengaturan Media 5, bisa diatur dari Inspector lewat controller.
[Serializable]
public class Media5Limits
{
    public NumberLineLimits Line = new NumberLineLimits();                    // panjang, partisi, default (sama dengan Media 2)
    public Media5OperationMode Operations = Media5OperationMode.SubtractOnly; // client meminta pengurangan saja
    public bool StartAtOperand = true;        // true: pion mulai di A; false: mulai di 0
    public int AntiRepeatWindow = 5;          // soal yang sama tidak muncul lagi dalam N soal terakhir (0 = tanpa memori)
    public int MinPartisiForProblem = 1;      // partisi minimal untuk boleh membuat soal (1 = partisi 1 dibolehkan)
}

public enum Media5ProblemError
{
    None,
    PartisiTooSmall,      // partisi lebih kecil dari MinPartisiForProblem
    NoProblemPossible     // garis terlalu pendek: tidak ada soal yang sah (MaxTick < 2)
}

public enum Media5JumpResult { Moved, NoProblem, AtStart, AtEnd }

public enum Media5CheckOutcome { Correct, Wrong, NoProblem }
public enum Media5RetryResult { Moved, NoProblem, AlreadyAtStart }