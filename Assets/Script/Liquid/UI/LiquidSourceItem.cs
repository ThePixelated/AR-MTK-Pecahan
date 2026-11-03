using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Gambar sumber larutan di HUD (air atau botol sirup). Ditarik ke gelas A (air) atau gelas B (sirup).
public class LiquidSourceItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] LiquidKind kind;
    [SerializeField] LiquidDragController drag;
    [SerializeField] Image sourceImage;      // gambar yang ditiru ghost; kosong = Image di objek ini

    bool active;

    public void OnBeginDrag(PointerEventData e)
    {
        Image img = sourceImage ? sourceImage : GetComponent<Image>();
        Sprite sprite = img ? img.sprite : null;
        Vector2 size = ((RectTransform)(img ? img.transform : transform)).rect.size;
        active = drag != null && drag.Begin(kind, sprite, size, e.position, e.pressEventCamera);
    }

    public void OnDrag(PointerEventData e)
    {
        if (active) drag.Move(e.position);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!active) return;
        active = false;
        drag.End(e.position);
    }
}