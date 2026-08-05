namespace PcMarket.Domain.Configurator;

/// <summary>The part slots a build is assembled from (Phase 19 configurator).</summary>
public enum ComponentCategory
{
    Motherboard = 0,
    Cpu = 1,
    Cooler = 2,
    Ram = 3,
    Storage = 4,
    Gpu = 5,
    Psu = 6,
    Case = 7
}

/// <summary>
/// Which build a part belongs to. <see cref="Either"/> is the common case — cases, PSUs, storage and
/// most coolers do not care about the CPU vendor — and is what lets the Intel and AMD entry paths
/// share the bulk of the catalog.
/// </summary>
public enum ComponentPlatform
{
    Either = 0,
    Intel = 1,
    Amd = 2
}

/// <summary>Memory generation. A motherboard supports exactly one of these, never both.</summary>
public enum RamType
{
    Ddr4 = 0,
    Ddr5 = 1
}

/// <summary>Motherboard board size, and the sizes a case has standoffs for.</summary>
public enum FormFactor
{
    Itx = 0,
    MicroAtx = 1,
    Atx = 2
}

/// <summary>
/// CPU socket names. Constants rather than an enum because the value is a free-form string on
/// <see cref="Component.Socket"/> — new sockets arrive every generation and should not need a
/// domain change — but the seeded catalog referring to them by name keeps typos out of the data.
/// </summary>
public static class Sockets
{
    public const string Lga1700 = "LGA1700";
    public const string Lga1851 = "LGA1851";
    public const string Am5 = "AM5";
}
