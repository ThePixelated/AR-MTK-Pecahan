using UnityEngine;
using UnityEngine.Events;

// Panel bank di HUD: blok dpz yang diangkat dan dilepas di sini dihapus ("kembali ke bank", bank tak terbatas)
public class BankRemoveZone : MonoBehaviour, IFractionDropZone
{
    [SerializeField] Activity4Controller controller;
    [SerializeField] RectTransform zoneRect;         // PecahanPanel
    [SerializeField] UnityEvent<bool> onHover;       // misal mengaktifkan overlay merah di panel bank

    bool hovering;

    void OnEnable() => DropZoneRegistry.Register(this);
    void OnDisable() { DropZoneRegistry.Unregister(this); SetHovering(false); }

    public bool Contains(Vector2 p, Camera cam) => RectTransformUtility.RectangleContainsScreenPoint(zoneRect, p, cam);

    public void Hover(in DragPayload payload, Vector2 p, Camera cam, float dt) =>
        SetHovering(payload.origin == DragOrigin.Dpz);

    public void HoverExit() => SetHovering(false);

    public RejectReason Accept(in DragPayload payload, Vector2 p, Camera cam)
    {
        bool removable = payload.origin == DragOrigin.Dpz;
        int index = payload.index;
        SetHovering(false);
        if (!removable) return RejectReason.None;

        FractionSession s = controller.Session;
        if (index >= 0 && index < s.Dpz.Count) s.RemoveFromDpz(index);
        return RejectReason.None;
    }

    void SetHovering(bool value)
    {
        if (hovering == value) return;
        hovering = value;
        onHover?.Invoke(value);
    }
}