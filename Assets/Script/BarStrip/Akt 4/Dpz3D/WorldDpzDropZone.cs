using UnityEngine;

public class WorldDpzDropZone : MonoBehaviour, IFractionDropZone
{
    [SerializeField] DpzView dpzView;
    [SerializeField] Camera worldCamera;     // kosong = Camera.main (ARCamera saat aktif)
    [SerializeField] string lostMessage = "Marker hilang. Arahkan kamera ke marker lagi.";

    Camera WorldCam => worldCamera ? worldCamera : Camera.main;

    void OnEnable() => DropZoneRegistry.Register(this);
    void OnDisable() => DropZoneRegistry.Unregister(this);

    static bool OverHud(Vector2 p)
    {
        var drag = FractionDragController.Instance;
        return drag != null && drag.IsOverHud(p);
    }

    bool Cast(Vector2 screenPos, out DpzHit hit)
    {
        hit = default;
        if (OverHud(screenPos)) return false;                       // HUD selalu menang atas dunia
        return dpzView.Raycast(screenPos, WorldCam, out hit);       // false juga kalau marker hilang
    }

    // Marker hilang: zona ini tetap mengklaim area dunia (di luar HUD) supaya lepas drag di sana ditolak dengan pesan
    public bool Contains(Vector2 p, Camera uiCam)
    {
        if (OverHud(p)) return false;
        if (!dpzView.Tracked) return true;
        return Cast(p, out _);
    }

    public void Hover(in DragPayload payload, Vector2 p, Camera uiCam, float dt)
    {
        if (!dpzView.Tracked || !Cast(p, out DpzHit hit)) { dpzView.ClearHover(); return; }

        if (payload.origin == DragOrigin.Bank)
        {
            int blockIndex = hit.kind == DpzColliderKind.Block ? hit.blockIndex : -1;
            bool overInner = hit.kind != DpzColliderKind.Outer;
            dpzView.SetHover(payload.piece, blockIndex, overInner);
        }
        else if (payload.origin == DragOrigin.KotakA || payload.origin == DragOrigin.KotakB)
        {
            dpzView.SetHoverKotak(payload.piece, payload.origin == DragOrigin.KotakA, payload.index);
        }
        else if (payload.origin == DragOrigin.Dpz && hit.kind == DpzColliderKind.Block)
        {
            dpzView.SetHoverLifted(payload.index, hit.blockIndex);   // gabung antar-balok: hanya yang muat
        }
        else
        {
            dpzView.ClearHover();
        }
    }

    public void HoverExit() => dpzView.ClearHover();

    public RejectReason Accept(in DragPayload payload, Vector2 p, Camera uiCam)
    {
        if (!dpzView.Tracked)
        {
            Feedback.Show(lostMessage, FeedbackKind.Warning);       // drop di dunia saat marker hilang ditolak
            return RejectReason.None;
        }

        if (!Cast(p, out DpzHit hit)) return RejectReason.None;

        if (payload.origin == DragOrigin.Dpz)
        {
            int source = payload.index;
            dpzView.ClearHover();
            if (hit.kind == DpzColliderKind.Block) dpzView.TryMergeBlocks(source, hit.blockIndex);   // tidak valid = batal
            return RejectReason.None;
        }

        if (payload.origin == DragOrigin.KotakA || payload.origin == DragOrigin.KotakB)
        {
            bool fromA = payload.origin == DragOrigin.KotakA;
            int row = payload.index;
            dpzView.ClearHover();
            return dpzView.TryMoveFromKotak(fromA, row);      // di mana pun di area: masuk ke bagian atas
        }

        if (payload.origin != DragOrigin.Bank) return RejectReason.None;

        Fraction piece = payload.piece;
        int targetBlock = hit.kind == DpzColliderKind.Block ? hit.blockIndex : -1;
        dpzView.ClearHover();
        return dpzView.TryDrop(piece, targetBlock);   // merge atau blok baru; alasan penolakan jadi notifikasi
    }
}