using UnityEngine;

// Uji 2b: menghubungkan NumberLineSession ke NumberLineView. Ubah nilai di Inspector saat Play.
public class NumberLineLab : MonoBehaviour
{
    [SerializeField] NumberLineView view;
    [SerializeField, Range(1, 5)] int length = 1;
    [SerializeField, Range(1, 15)] int partisi = 5;
    [SerializeField] bool dragEnabled = true;

    NumberLineSession session;

    void Start()
    {
        session = new NumberLineSession();
        view.PionDragged += OnDragged;
        session.Changed += Sync;
        Apply();
    }

    void OnDestroy()
    {
        if (view) view.PionDragged -= OnDragged;
    }

    void OnValidate()
    {
        if (Application.isPlaying && session != null) Apply();
    }

    void Apply()
    {
        session.ApplyConfig(length, partisi);
        view.DragEnabled = dragEnabled;
        Sync();
    }

    void Sync()
    {
        view.SetConfig(session.Config);
        view.SetPionTick(session.PionTick);
    }

    void OnDragged(int tick)
    {
        session.SetPionTick(tick);
        Fraction v = session.PionValue;
        Debug.Log($"[NumberLineLab] tick {session.PionTick}/{session.Config.MaxTick} = {v.numerator}/{v.denominator}");
    }
}