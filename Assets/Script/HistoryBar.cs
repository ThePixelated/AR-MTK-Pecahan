using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HistoryBar : MonoBehaviour
{
    public int indexUrut;
    public int liquidAVal;
    public int liquidBVal;
    public TextMeshProUGUI indexTxt;
    public Image liquidAThumbnail;
    public TextMeshProUGUI liquidATxt;
    public Image liquidBThumbnail;
    public TextMeshProUGUI liquidBTxt;

    public void ButtonPressed()
    {
        HistoryManager.instance.BarButtonPressed(this);
    }
}
