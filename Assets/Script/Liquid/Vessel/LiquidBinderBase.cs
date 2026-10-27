using UnityEngine;

// Kontrak binder yang dipakai PourController: sesi tuang, pencarian container, jendela animasi tuang.
public abstract class LiquidBinderBase : MonoBehaviour
{
    public abstract IPourSession PourSession { get; }
    public abstract bool TryGetContainer(LiquidVessel vessel, out LiquidContainer container);
    public abstract void BeginPourWindow(float seconds);
}