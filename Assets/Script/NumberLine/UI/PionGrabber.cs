using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Panel hewan Media 5. Hierarki yang diharapkan:
//
//   PionGrabber (objek ini)
//   |- iconPion          Button + Image + PionTokenButton   (sumber drag; interactable mati selama hewan ada di garis)
//   '- callBackHewanBtn  Button                             (Tarik Kembali: hewan keluar dari garis)
//
// Drag dari iconPion memunculkan "ghost" hewan (transparan) yang mengikuti pointer, tanda bahwa ini masih eksplorasi.
// Saat pointer berada di drop zone, ghost menempel ke tick terdekat. Dilepas di drop zone = hewan ditaruh di tick itu;
// dilepas di luar = dibatalkan. Komponen ini hanya melapor lewat event; keputusan ada di Media5Session lewat controller.
public class PionGrabber : MonoBehaviour
{
    [Header("Hierarki")]
    [SerializeField] PionTokenButton iconPion;       // komponen PionTokenButton di iconPion
    [SerializeField] Image iconImage;                // opsional: Image ikon hewan di iconPion (kosong = Image di iconPion)
    [SerializeField] Button callBackButton;          // callBackHewanBtn

    [Header("Garis")]
    [SerializeField] NumberLineView view;
    [SerializeField] Image pionImage;                // Image pion di garis (RectTransform 'pion' milik NumberLineView)
    [SerializeField] RectTransform dropZone;         // area drop (mis. RightSide ViewPort); kosong = garis itu sendiri
    [SerializeField] RectTransform dragRoot;         // induk ghost; kosong = Canvas root

    [Header("Hewan")]
    [SerializeField] Sprite animalSprite;            // opsional: paksa sprite hewan (ghost dan pion di garis)
    [SerializeField] Vector2 ghostSize = new Vector2(110f, 110f);
    [SerializeField, Range(0.2f, 1f)] float ghostAlpha = 0.6f;

    // Hewan dilepas di drop zone: tick tujuan
    public event Action<int> Dropped;
    public event Action CallBackPressed;

    Button iconButton;
    Image ghost;
    bool dragging;
    bool canDrag = true;

    void Awake()
    {
        if (iconPion != null)
        {
            iconButton = iconPion.GetComponent<Button>();
            if (iconImage == null) iconImage = iconPion.GetComponent<Image>();
        }

        if (animalSprite != null && pionImage != null) pionImage.sprite = animalSprite;

        if (iconPion == null || view == null || pionImage == null)
            Debug.LogError($"[{name}] PionGrabber: Icon Pion, View, dan Pion Image harus diisi.", this);
        else if (pionImage.GetComponent<NumberLinePionDrag>() == null)
            Debug.LogWarning($"[{name}] PionGrabber: '{pionImage.name}' belum punya NumberLinePionDrag, jadi hewan di garis tidak bisa digeser kiri-kanan.", this);
    }

    void OnEnable()
    {
        if (iconPion != null)
        {
            iconPion.DragBegan += OnDragBegan;
            iconPion.Dragged += OnDragged;
            iconPion.DragEnded += OnDragEnded;
        }
        if (callBackButton != null) callBackButton.onClick.AddListener(OnCallBack);
    }

    void OnDisable()
    {
        if (iconPion != null)
        {
            iconPion.DragBegan -= OnDragBegan;
            iconPion.Dragged -= OnDragged;
            iconPion.DragEnded -= OnDragEnded;
        }
        if (callBackButton != null) callBackButton.onClick.RemoveListener(OnCallBack);
        CancelDrag();
    }

    // Dipanggil controller setiap session berubah.
    // iconPion hanya aktif saat belum ada hewan dan belum ada soal; Tarik Kembali hanya saat ada hewan dan belum ada soal.
    public void SetState(bool hasPion, bool locked)
    {
        canDrag = !hasPion && !locked;
        if (iconButton != null) iconButton.interactable = canDrag;
        if (callBackButton != null) callBackButton.interactable = hasPion && !locked;
        if (!canDrag) CancelDrag();
    }

    void OnCallBack() => CallBackPressed?.Invoke();

    // ---------------- drag ----------------
    void OnDragBegan(PionTokenButton t, PointerEventData e)
    {
        if (!canDrag || view == null) return;

        CreateGhost();
        dragging = true;
        OnDragged(t, e);
    }

    void OnDragged(PionTokenButton t, PointerEventData e)
    {
        if (!dragging || ghost == null) return;

        if (InZone(e) && view.TryPointerToNormalized(e.position, e.pressEventCamera, out float n))
        {
            // di drop zone: menempel ke tick terdekat, pada tinggi hewan berdiri di garis
            NumberLineConfig config = view.Config;
            ghost.transform.position = SnappedWorldPosition(config.TickToNormalized(config.NearestTick(n)));
            return;
        }

        // di luar: mengikuti pointer
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(GhostRoot(), e.position, e.pressEventCamera, out Vector3 world))
            ghost.transform.position = world;
    }

    void OnDragEnded(PionTokenButton t, PointerEventData e)
    {
        if (!dragging) return;

        float n = 0f;
        bool ok = InZone(e) && view.TryPointerToNormalized(e.position, e.pressEventCamera, out n);

        DestroyGhost();
        dragging = false;
        if (ok) Dropped?.Invoke(view.Config.NearestTick(n));
    }

    bool InZone(PointerEventData e)
    {
        RectTransform zone = dropZone != null ? dropZone : view.Line;
        return RectTransformUtility.RectangleContainsScreenPoint(zone, e.position, e.pressEventCamera);
    }

    // Posisi dunia untuk hewan yang berdiri di tick (normalized 0..1). x mengikuti garis, y mengikuti tinggi pion di garis.
    Vector3 SnappedWorldPosition(float normalized)
    {
        RectTransform space = pionImage != null ? pionImage.rectTransform.parent as RectTransform : null;
        if (space == null) space = GhostRoot();

        float x = view.NormalizedToLocalX(normalized, space);
        float y = pionImage != null && pionImage.rectTransform.parent == space
            ? pionImage.rectTransform.localPosition.y
            : space.InverseTransformPoint(view.Line.position).y;
        return space.TransformPoint(new Vector3(x, y, 0f));
    }

    // ---------------- ghost ----------------
    RectTransform GhostRoot()
    {
        if (dragRoot != null) return dragRoot;
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null ? (RectTransform)canvas.rootCanvas.transform : (RectTransform)transform;
    }

    Sprite CurrentSprite()
    {
        if (animalSprite != null) return animalSprite;
        if (pionImage != null && pionImage.sprite != null) return pionImage.sprite;
        return iconImage != null ? iconImage.sprite : null;
    }

    void CreateGhost()
    {
        DestroyGhost();

        var go = new GameObject("PionGhost", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(GhostRoot(), false);

        Vector2 size = pionImage != null ? pionImage.rectTransform.rect.size : Vector2.zero;
        if (size.x <= 1f || size.y <= 1f) size = ghostSize;
        ((RectTransform)go.transform).sizeDelta = size;

        ghost = go.GetComponent<Image>();
        ghost.sprite = CurrentSprite();
        ghost.preserveAspect = true;
        ghost.raycastTarget = false;
        ghost.color = new Color(1f, 1f, 1f, ghostAlpha);
        go.transform.SetAsLastSibling();
    }

    void DestroyGhost()
    {
        if (ghost != null) Destroy(ghost.gameObject);
        ghost = null;
    }

    void CancelDrag()
    {
        DestroyGhost();
        dragging = false;
    }
}
