using UnityEngine;
using UnityEngine.UI;

public class InputHandlerGarisBilangan : MonoBehaviour
{
    [SerializeField] private ConfigGarisBilangan m_configGarisBilangan;
    [SerializeField] private Scrollbar scrollBar;

    [Header("Input via button")]
    [SerializeField] private Button forwardBtn;
    [SerializeField] private Button BackwardBtn;


    private void Start()
    {
        scrollBar = (scrollBar || scrollBar != m_configGarisBilangan.ScrollBar) ? scrollBar : m_configGarisBilangan.ScrollBar;
    }

    public void MoveForward()
    {
        scrollBar.value += m_configGarisBilangan.ValueStep;
    }

    public void MoveBackward()
    {
        scrollBar.value -= m_configGarisBilangan.ValueStep;
    }
}
