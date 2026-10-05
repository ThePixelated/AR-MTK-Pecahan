using UnityEngine;

public class FractionDragController : MonoBehaviour
{
    public static FractionDragController Instance { get; private set; }

    [SerializeField] RectTransform ghostRoot;
    [SerializeField] FractionGhost ghostPrefab;
    [SerializeField] FractionPalette palette;
    [SerializeField] Vector2 fingerOffset = new Vector2(0f, 90f);

    public bool IsDragging { get; private set; }

    DragPayload payload;
    FractionGhost ghost;
    Camera cam;
    Vector2 pointerPos;
    IFractionDropZone hoverZone;

    Vector2 Aim => pointerPos + fingerOffset;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }
    void OnDisable() => Cancel();

    public bool TryBegin(in DragPayload p, Vector2 screenPos, Camera eventCam)
    {
        if (IsDragging && ghost) return false;   // satu pointer aktif, jari kedua diabaikan
        if (IsDragging) Cleanup();               // state rusak (ghost sudah hilang): bersihkan dulu

        payload = p; cam = eventCam; IsDragging = true;
        ghost = Instantiate(ghostPrefab, ghostRoot);
        ghost.Setup(p.piece, palette);
        Move(screenPos);
        return true;
    }

    public void Move(Vector2 screenPos)
    {
        if (!IsDragging || !ghost) return;
        pointerPos = screenPos;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(ghostRoot, Aim, cam, out Vector2 local))
            ghost.transform.localPosition = local;
    }

    void Update()
    {
        if (!IsDragging) return;

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
        IsDragging = false;
    }
}