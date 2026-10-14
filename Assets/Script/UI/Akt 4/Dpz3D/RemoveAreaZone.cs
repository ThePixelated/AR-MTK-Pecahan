using UnityEngine;
using UnityEngine.Events;

// Blok dpz yang diangkat dan dilepas di sini dihapus
public class RemoveAreaZone : MonoBehaviour, IFractionDropZone
{
    [SerializeField] Activity4Controller controller;
    [SerializeField] Collider areaCollider;          // collider yang menutupi area hapus di dunia
    [SerializeField] Camera worldCamera;             // kosong = Camera.main
    [SerializeField] float maxDistance = 5f;
    [SerializeField] UnityEvent<bool> onHover;       // sambungkan ke highlight area hapus

    bool hovering;
    Camera WorldCam => worldCamera ? worldCamera : Camera.main;

    void OnEnable() => DropZoneRegistry.Register(this);
    void OnDisable() { DropZoneRegistry.Unregister(this); SetHovering(false); }

    bool Cast(Vector2 p)
    {
        var drag = FractionDragController.Instance;
        if (drag != null && drag.IsOverHud(p)) return false;
        if (!areaCollider || !areaCollider.enabled || WorldCam == null) return false;
        return areaCollider.Raycast(WorldCam.ScreenPointToRay(p), out _, maxDistance);
    }

    public bool Contains(Vector2 p, Camera uiCam) => Cast(p);

    //public void Hover(in DragPayload payload, Vector2 p, Camera uiCam) { }   // (tidak dipakai, lihat overload di bawah)
    public void Hover(in DragPayload payload, Vector2 p, Camera uiCam, float dt) =>
        SetHovering(payload.origin == DragOrigin.Dpz);   // hanya blok yang diangkat yang bisa dihapus di sini

    public void HoverExit() => SetHovering(false);

    public RejectReason Accept(in DragPayload payload, Vector2 p, Camera uiCam)
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