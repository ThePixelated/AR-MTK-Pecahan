using System;
using UnityEngine;

public class Media25Manager : MonoBehaviour
{
    public static Media25Manager Instance;
    [SerializeField] private UIStepper partialUIStepper;

    public UIStepper PartialUIStepper { get { return partialUIStepper; } set { partialUIStepper = value; } }

    private void Awake()
    {
        Instance = this;

        //partialUIStepper.Set = 5; // Hardcoded
    }


    public Action<int> onPanjangGarisPressed;
    public void PanjangGarisInput(int index)
    {
        onPanjangGarisPressed?.Invoke(index);
    }
}
