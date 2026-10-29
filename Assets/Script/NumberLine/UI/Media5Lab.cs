using UnityEngine;

// Uji 5b: menghubungkan Media5Session ke NumberLineView dan tombol lompat. Ubah nilai di Inspector saat Play;
// "Buat soal", "Coba lagi", dan "CEK" lewat menu klik kanan komponen.
public class Media5Lab : MonoBehaviour
{
    [SerializeField] NumberLineView view;
    [SerializeField] HoldRepeatButton forwardButton;     // LompatMajuBtn
    [SerializeField] HoldRepeatButton backwardButton;    // LompatMundurBtn
    [SerializeField] Media5Limits limits = new Media5Limits();
    [SerializeField, Range(1, 5)] int length = 1;
    [SerializeField, Range(1, 15)] int partisi = 5;
    [SerializeField] bool showTrail;

    Media5Session session;

    void Start()
    {
        session = new Media5Session(limits);
        view.DragEnabled = false;           // Media 5: pion tidak di-drag
        session.Changed += Sync;
        if (forwardButton) forwardButton.Step += OnForward;
        if (backwardButton) backwardButton.Step += OnBackward;
        ApplyConfig();
    }

    void OnDestroy()
    {
        if (forwardButton) forwardButton.Step -= OnForward;
        if (backwardButton) backwardButton.Step -= OnBackward;
    }

    void OnValidate()
    {
        if (!Application.isPlaying || session == null) return;
        view.ShowTrail = showTrail;
        ApplyConfig();
    }

    void ApplyConfig()
    {
        session.ApplyConfig(length, partisi);
        view.ShowTrail = showTrail;
        view.ClearTrail();
        Sync();
        view.SetPionTick(session.PionTick, true);
    }

    // Tampilan mengikuti session; tombol lompat nonaktif di ujung garis atau tanpa soal
    void Sync()
    {
        view.SetConfig(session.Config);
        view.SetPionTick(session.PionTick);

        bool canMove = session.HasProblem;
        if (backwardButton) backwardButton.Interactable = canMove && session.PionTick > 0;
        if (forwardButton) forwardButton.Interactable = canMove && session.PionTick < session.Config.MaxTick;
    }

    void OnForward() => Jump(true);
    void OnBackward() => Jump(false);

    void Jump(bool forward)
    {
        int before = session.PionTick;
        Media5JumpResult r = forward ? session.JumpForward() : session.JumpBackward();
        if (r == Media5JumpResult.Moved) view.AddTrailArc(before, session.PionTick);
        else Debug.Log($"[Media5Lab] lompat {(forward ? "maju" : "mundur")}: {r}", this);
    }

    string Mixed(int tick)
    {
        session.Config.TickToMixed(tick, out int whole, out int numerator);
        int den = session.Config.partisi;
        if (numerator == 0) return whole.ToString();
        return whole == 0 ? $"{numerator}/{den}" : $"{whole} {numerator}/{den}";
    }

    [ContextMenu("Uji: buat soal")]
    void NewProblem()
    {
        Media5ProblemError e = session.TryNewProblem();
        if (e != Media5ProblemError.None) { Debug.Log($"[Media5Lab] buat soal: {e}", this); return; }

        NumberLineProblem p = session.Problem.Value;
        string op = p.operation == NumberLineOperation.Subtract ? "-" : "+";
        Debug.Log($"[Media5Lab] soal: {Mixed(p.aTick)} {op} {Mixed(p.bTick)} = {Mixed(p.ResultTick)} (tick {p.aTick} {op} {p.bTick} = {p.ResultTick})", this);

        view.ClearTrail();
        view.SetPionTick(session.PionTick, true);
    }

    [ContextMenu("Uji: coba lagi")]
    void Retry()
    {
        Media5RetryResult r = session.RetryPosition();
        Debug.Log($"[Media5Lab] coba lagi: {r}", this);
        if (r == Media5RetryResult.Moved) view.ClearTrail();      // percobaan baru: jejak lama dibuang
    }

    [ContextMenu("Uji: CEK")]
    void CheckAnswer() => Debug.Log($"[Media5Lab] CEK: {session.Check()}", this);

    [ContextMenu("Uji: reset")]
    void ResetAll()
    {
        session.ResetAll();
        view.ClearTrail();
        view.SetPionTick(session.PionTick, true);
    }
}