public enum NumberLineOperation { Subtract, Add }

// Satu soal Media 5 dalam satuan tick (penyebut = partisi). Pengurangan: A - B; penjumlahan: A + B.
public readonly struct NumberLineProblem
{
    public readonly NumberLineOperation operation;
    public readonly int aTick;
    public readonly int bTick;

    public NumberLineProblem(NumberLineOperation operation, int aTick, int bTick)
    {
        this.operation = operation;
        this.aTick = aTick;
        this.bTick = bTick;
    }

    public int ResultTick => operation == NumberLineOperation.Subtract ? aTick - bTick : aTick + bTick;

    // Operasi dan angka sama persis (dipakai memori anti-ulang)
    public bool SamePair(NumberLineProblem other) =>
        operation == other.operation && aTick == other.aTick && bTick == other.bTick;

    // Soal yang sah pada garis ini: pengurangan A > B >= 1 dan A <= MaxTick; penjumlahan A, B >= 1 dan A + B <= MaxTick
    public bool IsValidFor(NumberLineConfig config)
    {
        int max = config.MaxTick;
        if (operation == NumberLineOperation.Subtract) return bTick >= 1 && aTick > bTick && aTick <= max;
        return aTick >= 1 && bTick >= 1 && aTick + bTick <= max;
    }
}