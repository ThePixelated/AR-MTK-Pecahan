using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TapAwayDismiss : MonoBehaviour
{
    static readonly List<RaycastResult> hits = new List<RaycastResult>();

    void Update()
    {
        Pointer p = Pointer.current;
        if (p == null || !p.press.wasPressedThisFrame) return;

        FractionBarRow row = RowSelection.Current;
        if (!row || EventSystem.current == null) return;

        var data = new PointerEventData(EventSystem.current) { position = p.position.ReadValue() };
        hits.Clear();
        EventSystem.current.RaycastAll(data, hits);   // urut dari paling atas

        bool overRow = hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(row.transform);
        if (!overRow) RowSelection.Clear();
    }
}