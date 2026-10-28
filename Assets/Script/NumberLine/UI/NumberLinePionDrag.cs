using UnityEngine;
using UnityEngine.EventSystems;

// Hold-drag pion: drag dimulai langsung (tanpa ambang gerak) dan diteruskan ke NumberLineView.
public class NumberLinePionDrag : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] NumberLineView view;
    [SerializeField] bool debugLog;

    public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

    public void OnBeginDrag(PointerEventData e)
    {
        if (debugLog) Debug.Log($"[PionDrag] begin, view = {(view ? view.name : "KOSONG")}", this);
        view.HandlePionDrag(e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (debugLog) Debug.Log("[PionDrag] drag", this);
        view.HandlePionDrag(e);
    }

    public void OnEndDrag(PointerEventData e) { }
}