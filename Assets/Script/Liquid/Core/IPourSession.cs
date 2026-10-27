// Kontrak minimum yang dibutuhkan pengendali tuang dari sebuah session cairan.
// Media1Session dan Media3Session sama-sama mengimplementasikannya.
public interface IPourSession
{
    PourError CheckPour(LiquidContainer source, LiquidContainer target);
    PourError TryPour(LiquidContainer source, LiquidContainer target);
}