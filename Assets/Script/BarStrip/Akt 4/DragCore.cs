using System.Collections.Generic;
using UnityEngine;

//public enum DragOrigin { Bank, Dpz }   // Dpz dipakai di step 5
public enum DragOrigin { Bank, Dpz, KotakA, KotakB }

public readonly struct DragPayload
{
    public readonly Fraction piece;
    public readonly DragOrigin origin;
    public readonly int index;          // posisi di dpz kalau origin == Dpz, selain itu -1

    public DragPayload(Fraction piece, DragOrigin origin, int index = -1)
    { this.piece = piece; this.origin = origin; this.index = index; }

    public static DragPayload FromBank(Fraction piece) => new DragPayload(piece, DragOrigin.Bank);
}

public interface IFractionDropZone
{
    bool Contains(Vector2 screenPos, Camera cam);
    void Hover(in DragPayload payload, Vector2 screenPos, Camera cam, float dt);   // tiap frame selama ghost di zona ini
    void HoverExit();
    // None = diterima ATAU ditunda (zona sendiri yang urus popup konfirmasi)
    RejectReason Accept(in DragPayload payload, Vector2 screenPos, Camera cam);
}

public static class DropZoneRegistry
{
    static readonly List<IFractionDropZone> zones = new List<IFractionDropZone>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => zones.Clear();

    public static void Register(IFractionDropZone z) { if (!zones.Contains(z)) zones.Add(z); }
    public static void Unregister(IFractionDropZone z) => zones.Remove(z);

    public static IFractionDropZone Find(Vector2 screenPos, Camera cam)
    {
        for (int i = zones.Count - 1; i >= 0; i--)   // terdaftar terakhir menang kalau tumpang tindih
            if (zones[i].Contains(screenPos, cam)) return zones[i];
        return null;
    }
}