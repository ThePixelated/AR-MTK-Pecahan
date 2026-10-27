using UnityEngine;

public class InputPanjangGaris : MonoBehaviour
{
    public void InputValPanjangGaris(int index)
    {
        Media25Manager.Instance.PanjangGarisInput(index);
    }
}
