using System;
using UnityEngine;

public class ObjectManager : MonoBehaviour
{
    public static bool IsPouring { get; private set; } = false;
    //public static Action onSetPour;

    public static void SetPourAction(bool state)
    {
        //onSetPour?.Invoke();
        IsPouring = state;
    }
}
