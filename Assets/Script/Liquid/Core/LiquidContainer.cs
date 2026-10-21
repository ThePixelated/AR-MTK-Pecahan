using System;

[Flags]
public enum ContainerRole { None = 0, Give = 1, Receive = 2 }

// Wadah cairan (data murni, tanpa Unity). Satuan volume = "bagian" (1/p dari satu gelas utuh).
public sealed class LiquidContainer
{
    public string Name { get; }
    public ContainerRole Role { get; }
    public long CapacitySections { get; private set; }
    public Mixture Content { get; private set; } = Mixture.Empty;

    public event Action Changed;

    public LiquidContainer(string name, ContainerRole role, long capacitySections = 0)
    {
        Name = name;
        Role = role;
        CapacitySections = capacitySections;
    }

    public bool CanGive => (Role & ContainerRole.Give) != 0;
    public bool CanReceive => (Role & ContainerRole.Receive) != 0;

    // Volume selalu bulat: tiap tuang memindahkan tepat 1 bagian
    public long VolumeSections
    {
        get { Fraction t = Content.Total; return t.numerator / t.denominator; }
    }

    public bool IsEmpty => Content.IsEmpty;
    public long SpaceSections => CapacitySections - VolumeSections;
    public bool IsFull => SpaceSections <= 0;

    // Tinggi cairan relatif terhadap kapasitas (0..1), untuk tampilan
    public float FillRatio => CapacitySections <= 0 ? 0f : (float)VolumeSections / CapacitySections;

    internal void Configure(long capacitySections, Mixture content)
    {
        CapacitySections = capacitySections;
        Content = content;
        NotifyChanged();
    }

    internal Mixture Take(long sections)
    {
        Mixture taken = Content.Take(sections);
        Content = Content - taken;
        return taken;
    }

    internal void Put(Mixture m) { Content = Content + m; }

    internal void NotifyChanged() => Changed?.Invoke();
}