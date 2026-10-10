using UnityEngine;

public class FractionDragController : MonoBehaviour
{
    public static FractionDragController Instance { get; private set; }

    [SerializeField] RectTransform ghostRoot;
    [SerializeField] FractionGhost ghostPrefab;
    [SerializeField] FractionPalette palette;
    [SerializeField] Vector2 fingerOffset = new Vector2(0f, 90f);

    [Header("Dunia 3D (opsional)")]
    [SerializeField] HeldGhost3D heldGhost3D;     // tanpa ini ghost selalu 2D
    [SerializeField] RectTransform hudRect;       // MainPanel/Backpanel (rect live): di dalamnya ghost 2D, di luarnya ghost 3D
    [SerializeField] Camera worldCamera;          // kosong = Camera.main

    public bool IsDragging { get; private set; }

    DragPayload payload;
    FractionGhost ghost;
    Camera cam;
    Vector2 pointerPos;
    IFractionDropZone hoverZone;

    Vector2 Aim => pointerPos + fingerOffset;   // zona dicek pakai posisi ghost, bukan jari
    Camera WorldCam => worldCamera ? worldCamera : Camera.main;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }
    void OnDisable() => Cancel();

    public bool IsOverHud(Vector2 screenPos) =>
        hudRect && RectTransformUtility.RectangleContainsScreenPoint(hudRect, screenPos, cam);

    public bool TryBegin(in DragPayload p, Vector2 screenPos, Camera eventCam)
    {
        if (IsDragging && ghost) return false;   // satu pointer aktif, jari kedua diabaikan
        if (IsDragging) Cleanup();               // state rusak (ghost sudah hilang): bersihkan dulu

        payload = p; cam = eventCam; IsDragging = true;
        ghost = Instantiate(ghostPrefab, ghostRoot);
        ghost.Setup(p.piece, palette);
        if (heldGhost3D) heldGhost3D.Begin(p.piece, palette.Get(p.piece.denominator));
        Move(screenPos);
        return true;
    }

    public void Move(Vector2 screenPos)
    {
        if (!IsDragging || !ghost) return;
        pointerPos = screenPos;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(ghostRoot, Aim, cam, out Vector2 local))
            ghost.transform.localPosition = local;
        UpdateGhostMode();
    }

    // Di dalam HUD: ghost 2D. Di luar HUD dan kena bidang dpz: ghost 3D (2D disembunyikan).
    void UpdateGhostMode()
    {
        if (!ghost) return;
        bool use3D = heldGhost3D != null && !IsOverHud(Aim) && heldGhost3D.TryPlace(Aim, WorldCam);
        if (ghost.gameObject.activeSelf == use3D) ghost.gameObject.SetActive(!use3D);
        if (!use3D && heldGhost3D != null) heldGhost3D.Hide();
    }

    void Update()
    {
        if (!IsDragging) return;   // jalan tiap frame supaya auto-scroll dan ghost 3D ikut bergerak saat kamera AR bergerak
        UpdateGhostMode();

        IFractionDropZone zone = DropZoneRegistry.Find(Aim, cam);
        if (zone != hoverZone) { hoverZone?.HoverExit(); hoverZone = zone; }
        zone?.Hover(in payload, Aim, cam, Time.unscaledDeltaTime);
    }

    public void End(Vector2 screenPos)
    {
        if (!IsDragging) return;
        Move(screenPos);

        IFractionDropZone zone = DropZoneRegistry.Find(Aim, cam);
        hoverZone?.HoverExit(); hoverZone = null;

        RejectReason why = RejectReason.None;
        try
        {
            if (zone != null) why = zone.Accept(in payload, Aim, cam);
        }
        finally
        {
            Cleanup();   // apa pun yang terjadi di Accept (termasuk exception), ghost hilang dan drag terlepas
        }

        if (why != RejectReason.None) Feedback.Show(Messages.For(why), FeedbackKind.Warning);
    }

    public void Cancel()
    {
        if (!IsDragging) return;
        hoverZone?.HoverExit(); hoverZone = null;
        Cleanup();
    }

    void Cleanup()
    {
        if (ghost) Destroy(ghost.gameObject);
        ghost = null;
        if (heldGhost3D) heldGhost3D.Hide();
        IsDragging = false;
    }
}