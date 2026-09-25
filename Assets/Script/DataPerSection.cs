using TMPro;
using UnityEngine;

public class DataPerSection : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI numberVal;
    public int IntValue { get; private set; } = 0;

    public void Penjumlahan(int increment = 1)
    {
        IntValue = (IntValue + increment < 0 ? 0 : IntValue + increment);

        if (numberVal != null) numberVal.text = IntValue.ToString();
    }
}
