using UnityEngine;

public class WorldDpzDropZone : MonoBehaviour, IFractionDropZone
{
    [SerializeField] DpzView dpzView;
    [SerializeField] Camera worldCamera;     // kosong = Camera.main (ARCamera saat aktif)

    Camera WorldCam => worldCamera ? worldCamera : Camera.main;

    void OnEnable() => DropZoneRegistry.Register(this);
    void OnDisable() => DropZoneRegistry.Unregister(this);

    bool Cast(Vector2 screenPos, out DpzHit hit)
    {
        hit = default;
        var drag = FractionDragController.Instance;
        if (drag != null && drag.IsOverHud(screenPos)) return false;   // HUD selalu menang atas dunia
        return dpzView.Raycast(screenPos, WorldCam, out hit);
    }

    public bool Contains(Vector2 p, Camera uiCam) => Cast(p, out _);

    public void Hover(in DragPayload payload, Vector2 p, Camera uiCam, float dt)
    {
        if (!Cast(p, out DpzHit hit)) { dpzView.ClearHover(); return; }

        int blockIndex = hit.kind == DpzColliderKind.Block ? hit.blockIndex : -1;
        bool overInner = hit.kind != DpzColliderKind.Outer;
        dpzView.SetHover(payload.piece, blockIndex, overInner);
    }

    public void HoverExit() => dpzView.ClearHover();

    public RejectReason Accept(in DragPayload payload, Vector2 p, Camera uiCam)
    {
        if (payload.origin != DragOrigin.Bank) return RejectReason.None;   // asal lain diurus di 5b-2b
        if (!Cast(p, out DpzHit hit)) return RejectReason.None;

        Fraction piece = payload.piece;
        int blockIndex = hit.kind == DpzColliderKind.Block ? hit.blockIndex : -1;
        dpzView.ClearHover();
        return dpzView.TryDrop(piece, blockIndex);   // merge atau blok baru; alasan penolakan jadi notifikasi
    }
}