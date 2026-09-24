using System;
using UnityEngine;

public class ToggleButton : MonoBehaviour
{
    [SerializeField] private Vector2Int targetIndex;
    [SerializeField] private bool isToggled = false;
    [SerializeField] private PanelAnimator m_panelAnimator;

    public void TriggerPanel()
    {
        if (!isToggled) m_panelAnimator.Trigger(targetIndex.x);
        else m_panelAnimator.Trigger(targetIndex.y);

        isToggled = !isToggled;
    }
}
