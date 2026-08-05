namespace PcMarket.Domain.Configurator;

/// <summary>
/// The seeded parts list behind the Phase 19 configurator, plus the ready-made bundles.
/// <para>
/// Static reference data in the same spirit as <c>UzbekistanRegions</c>: fixed for the life of the
/// process, no I/O, no DI. That is what lets the compatibility tests run against the real catalog
/// rather than against fixtures invented for the tests — a seeded bundle that stops being
/// compatible fails the suite.
/// </para>
/// <para>
/// <b>Specs are representative, not authoritative.</b> Sockets, memory generations, form factors and
/// the ordering between values are real, because that is what the rules reason about; wattages,
/// lengths and prices are plausible round numbers for a demo catalog and are not vendor-verified.
/// When these parts become admin-managed this class is the seam — swap the arrays for a repository
/// and neither the rules nor their tests change.
/// </para>
/// <para>
/// Artwork reuses the existing storefront photography in <c>wwwroot/images/home</c>; no new image
/// files were added for the configurator. Prices are UZS, the store's base currency.
/// </para>
/// </summary>
public static class ComponentCatalog
{
    private const string Img = "/images/home/";

    // ---------- Processors ----------
    // Intel-weighted, matching the reference site's Intel-first configurator; AM5 covers the AMD path.
    private static readonly Component[] Cpus =
    [
        new()
        {
            Id = "cpu-i3-14100", Name = "Intel Core i3-14100", Category = ComponentCategory.Cpu,
            Platform = ComponentPlatform.Intel, Price = 1_450_000m,
            Socket = Sockets.Lga1700, PowerDraw = 58, ImageUrl = Img + "feat-mobo-4.jpg"
        },
        new()
        {
            Id = "cpu-i5-14400f", Name = "Intel Core i5-14400F", Category = ComponentCategory.Cpu,
            Platform = ComponentPlatform.Intel, Price = 2_350_000m,
            Socket = Sockets.Lga1700, PowerDraw = 65, ImageUrl = Img + "feat-mobo-4.jpg"
        },
        new()
        {
            Id = "cpu-i5-14600k", Name = "Intel Core i5-14600K", Category = ComponentCategory.Cpu,
            Platform = ComponentPlatform.Intel, Price = 3_600_000m,
            Socket = Sockets.Lga1700, PowerDraw = 125, ImageUrl = Img + "feat-mobo-4.jpg"
        },
        new()
        {
            Id = "cpu-i7-14700k", Name = "Intel Core i7-14700K", Category = ComponentCategory.Cpu,
            Platform = ComponentPlatform.Intel, Price = 5_200_000m,
            Socket = Sockets.Lga1700, PowerDraw = 125, ImageUrl = Img + "feat-mobo-4.jpg"
        },
        new()
        {
            // The one LGA1851 part in the catalog: it is what makes the cooler-socket rule
            // reachable with real data, since the older LGA1700-only cooler cannot mount on it.
            Id = "cpu-ultra5-245k", Name = "Intel Core Ultra 5 245K", Category = ComponentCategory.Cpu,
            Platform = ComponentPlatform.Intel, Price = 4_300_000m,
            Socket = Sockets.Lga1851, PowerDraw = 125, ImageUrl = Img + "feat-mobo-4.jpg"
        },
        new()
        {
            Id = "cpu-ryzen5-7600", Name = "AMD Ryzen 5 7600", Category = ComponentCategory.Cpu,
            Platform = ComponentPlatform.Amd, Price = 2_600_000m,
            Socket = Sockets.Am5, PowerDraw = 65, ImageUrl = Img + "feat-mobo-4.jpg"
        },
        new()
        {
            Id = "cpu-ryzen7-7800x3d", Name = "AMD Ryzen 7 7800X3D", Category = ComponentCategory.Cpu,
            Platform = ComponentPlatform.Amd, Price = 5_900_000m,
            Socket = Sockets.Am5, PowerDraw = 120, ImageUrl = Img + "feat-mobo-4.jpg"
        },
    ];

    // ---------- Motherboards ----------
    // Deliberately spans both memory generations on the same socket (H610M is DDR4, B760M is DDR5),
    // which is what makes the RAM rule a real choice rather than a formality.
    private static readonly Component[] Motherboards =
    [
        new()
        {
            Id = "mb-h610m-k-d4", Name = "ASUS PRIME H610M-K D4", Category = ComponentCategory.Motherboard,
            Platform = ComponentPlatform.Intel, Price = 1_250_000m,
            Socket = Sockets.Lga1700, RamType = RamType.Ddr4, FormFactor = FormFactor.MicroAtx,
            ImageUrl = Img + "feat-mobo-1.jpg"
        },
        new()
        {
            Id = "mb-b760m-a-wifi", Name = "MSI PRO B760M-A WIFI", Category = ComponentCategory.Motherboard,
            Platform = ComponentPlatform.Intel, Price = 2_150_000m,
            Socket = Sockets.Lga1700, RamType = RamType.Ddr5, FormFactor = FormFactor.MicroAtx,
            ImageUrl = Img + "feat-mobo-2.jpg"
        },
        new()
        {
            Id = "mb-z790-ud-ax", Name = "Gigabyte Z790 UD AX", Category = ComponentCategory.Motherboard,
            Platform = ComponentPlatform.Intel, Price = 3_400_000m,
            Socket = Sockets.Lga1700, RamType = RamType.Ddr5, FormFactor = FormFactor.Atx,
            ImageUrl = Img + "feat-mobo-3.jpg"
        },
        new()
        {
            Id = "mb-z890-p", Name = "ASUS PRIME Z890-P", Category = ComponentCategory.Motherboard,
            Platform = ComponentPlatform.Intel, Price = 4_100_000m,
            Socket = Sockets.Lga1851, RamType = RamType.Ddr5, FormFactor = FormFactor.Atx,
            ImageUrl = Img + "feat-mobo-5.jpg"
        },
        new()
        {
            Id = "mb-b650m-p", Name = "MSI PRO B650M-P", Category = ComponentCategory.Motherboard,
            Platform = ComponentPlatform.Amd, Price = 2_050_000m,
            Socket = Sockets.Am5, RamType = RamType.Ddr5, FormFactor = FormFactor.MicroAtx,
            ImageUrl = Img + "feat-mobo-6.jpg"
        },
        new()
        {
            Id = "mb-x670e-plus", Name = "ASUS TUF GAMING X670E-PLUS", Category = ComponentCategory.Motherboard,
            Platform = ComponentPlatform.Amd, Price = 4_600_000m,
            Socket = Sockets.Am5, RamType = RamType.Ddr5, FormFactor = FormFactor.Atx,
            ImageUrl = Img + "feat-mobo-2.jpg"
        },
    ];

    // ---------- Memory ----------
    private static readonly Component[] Ram =
    [
        new()
        {
            Id = "ram-fury-16-ddr4", Name = "Kingston FURY Beast 16GB DDR4-3200", Category = ComponentCategory.Ram,
            Price = 620_000m, RamType = RamType.Ddr4, ImageUrl = Img + "feat-mobo-4.jpg"
        },
        new()
        {
            Id = "ram-fury-32-ddr4", Name = "Kingston FURY Beast 32GB DDR4-3600", Category = ComponentCategory.Ram,
            Price = 1_150_000m, RamType = RamType.Ddr4, ImageUrl = Img + "feat-mobo-4.jpg"
        },
        new()
        {
            Id = "ram-fury-16-ddr5", Name = "Kingston FURY Beast 16GB DDR5-5600", Category = ComponentCategory.Ram,
            Price = 890_000m, RamType = RamType.Ddr5, ImageUrl = Img + "feat-mobo-4.jpg"
        },
        new()
        {
            Id = "ram-vengeance-32-ddr5", Name = "Corsair Vengeance 32GB DDR5-6000", Category = ComponentCategory.Ram,
            Price = 1_680_000m, RamType = RamType.Ddr5, ImageUrl = Img + "feat-mobo-4.jpg"
        },
        new()
        {
            Id = "ram-trident-32-ddr5", Name = "G.Skill Trident Z5 32GB DDR5-6400", Category = ComponentCategory.Ram,
            Price = 1_950_000m, RamType = RamType.Ddr5, ImageUrl = Img + "feat-mobo-4.jpg"
        },
    ];

    // ---------- Graphics ----------
    private static readonly Component[] Gpus =
    [
        new()
        {
            Id = "gpu-rtx4060", Name = "NVIDIA GeForce RTX 4060 8GB", Category = ComponentCategory.Gpu,
            Price = 3_900_000m, PowerDraw = 115, LengthMm = 245, ImageUrl = Img + "feat-gpu-1.jpg"
        },
        new()
        {
            Id = "gpu-rtx4060ti", Name = "NVIDIA GeForce RTX 4060 Ti 16GB", Category = ComponentCategory.Gpu,
            Price = 5_600_000m, PowerDraw = 165, LengthMm = 280, ImageUrl = Img + "feat-gpu-2.jpg"
        },
        new()
        {
            Id = "gpu-rtx4070s", Name = "NVIDIA GeForce RTX 4070 SUPER", Category = ComponentCategory.Gpu,
            Price = 8_400_000m, PowerDraw = 220, LengthMm = 305, ImageUrl = Img + "feat-gpu-3.jpg"
        },
        new()
        {
            // The long card. Fits the mid-tower and up, not the compact builds — this is the part
            // that makes the GPU-length rule bite.
            Id = "gpu-rtx4080s", Name = "NVIDIA GeForce RTX 4080 SUPER", Category = ComponentCategory.Gpu,
            Price = 14_900_000m, PowerDraw = 320, LengthMm = 336, ImageUrl = Img + "feat-gpu-4.jpg"
        },
        new()
        {
            Id = "gpu-rx7600", Name = "AMD Radeon RX 7600", Category = ComponentCategory.Gpu,
            Price = 3_500_000m, PowerDraw = 165, LengthMm = 270, ImageUrl = Img + "feat-gpu-5.jpg"
        },
        new()
        {
            Id = "gpu-rx7800xt", Name = "AMD Radeon RX 7800 XT", Category = ComponentCategory.Gpu,
            Price = 7_300_000m, PowerDraw = 263, LengthMm = 320, ImageUrl = Img + "feat-gpu-6.jpg"
        },
    ];

    // ---------- Power supplies ----------
    private static readonly Component[] PowerSupplies =
    [
        new()
        {
            Id = "psu-500-bronze", Name = "Deepcool PF500 500W 80+ Bronze", Category = ComponentCategory.Psu,
            Price = 480_000m, Wattage = 500, ImageUrl = Img + "feat-case-2.jpg"
        },
        new()
        {
            Id = "psu-650-bronze", Name = "Cooler Master MWE 650W 80+ Bronze", Category = ComponentCategory.Psu,
            Price = 720_000m, Wattage = 650, ImageUrl = Img + "feat-case-2.jpg"
        },
        new()
        {
            Id = "psu-750-gold", Name = "Corsair RM750e 750W 80+ Gold", Category = ComponentCategory.Psu,
            Price = 1_150_000m, Wattage = 750, ImageUrl = Img + "feat-case-2.jpg"
        },
        new()
        {
            Id = "psu-850-gold", Name = "Seasonic Focus GX-850 850W 80+ Gold", Category = ComponentCategory.Psu,
            Price = 1_640_000m, Wattage = 850, ImageUrl = Img + "feat-case-2.jpg"
        },
        new()
        {
            Id = "psu-1000-platinum", Name = "MSI MPG A1000G 1000W 80+ Platinum", Category = ComponentCategory.Psu,
            Price = 2_450_000m, Wattage = 1000, ImageUrl = Img + "feat-case-2.jpg"
        },
    ];

    // ---------- Cases ----------
    // Ordered small to large, so each successive case relaxes one of the three limits the rules read.
    private static readonly Component[] Cases =
    [
        new()
        {
            Id = "case-itx-compact", Name = "Cooler Master NR200 (Mini-ITX)", Category = ComponentCategory.Case,
            Price = 890_000m,
            SupportedFormFactors = [FormFactor.Itx],
            MaxGpuLengthMm = 240, MaxCoolerHeightMm = 120, ImageUrl = Img + "feat-case-1.jpg"
        },
        new()
        {
            Id = "case-matx-mid", Name = "Deepcool MATREXX 40 (Micro-ATX)", Category = ComponentCategory.Case,
            Price = 620_000m,
            SupportedFormFactors = [FormFactor.Itx, FormFactor.MicroAtx],
            MaxGpuLengthMm = 330, MaxCoolerHeightMm = 160, ImageUrl = Img + "feat-case-2.jpg"
        },
        new()
        {
            Id = "case-atx-mid", Name = "MSI MAG FORGE 100R (ATX)", Category = ComponentCategory.Case,
            Price = 950_000m,
            SupportedFormFactors = [FormFactor.Itx, FormFactor.MicroAtx, FormFactor.Atx],
            MaxGpuLengthMm = 360, MaxCoolerHeightMm = 170, ImageUrl = Img + "feat-case-3.jpg"
        },
        new()
        {
            Id = "case-atx-airflow", Name = "Lian Li Lancool 216 (ATX Airflow)", Category = ComponentCategory.Case,
            Price = 1_380_000m,
            SupportedFormFactors = [FormFactor.Itx, FormFactor.MicroAtx, FormFactor.Atx],
            MaxGpuLengthMm = 400, MaxCoolerHeightMm = 180, ImageUrl = Img + "feat-case-1.jpg"
        },
        new()
        {
            Id = "case-full-tower", Name = "Phanteks Enthoo Pro 2 (Full Tower)", Category = ComponentCategory.Case,
            Price = 2_100_000m,
            SupportedFormFactors = [FormFactor.Itx, FormFactor.MicroAtx, FormFactor.Atx],
            MaxGpuLengthMm = 450, MaxCoolerHeightMm = 190, ImageUrl = Img + "feat-case-3.jpg"
        },
    ];

    // ---------- Cooling ----------
    private static readonly Component[] Coolers =
    [
        new()
        {
            Id = "cool-stock-92", Name = "ID-Cooling SE-903-XT (92mm air)", Category = ComponentCategory.Cooler,
            Price = 180_000m, HeightMm = 92,
            SocketSupport = [Sockets.Lga1700, Sockets.Lga1851, Sockets.Am5], ImageUrl = Img + "feat-case-2.jpg"
        },
        new()
        {
            Id = "cool-se224", Name = "ID-Cooling SE-224-XT (120mm air)", Category = ComponentCategory.Cooler,
            Price = 320_000m, HeightMm = 154,
            SocketSupport = [Sockets.Lga1700, Sockets.Lga1851, Sockets.Am5], ImageUrl = Img + "feat-case-2.jpg"
        },
        new()
        {
            Id = "cool-ak620", Name = "Deepcool AK620 (dual-tower air)", Category = ComponentCategory.Cooler,
            Price = 640_000m, HeightMm = 160,
            SocketSupport = [Sockets.Lga1700, Sockets.Lga1851, Sockets.Am5], ImageUrl = Img + "feat-case-2.jpg"
        },
        new()
        {
            // Older stock: LGA1700 mounting only, so pairing it with the Core Ultra (LGA1851)
            // trips the cooler-socket rule using nothing but real catalog parts.
            Id = "cool-freezer34", Name = "Arctic Freezer 34 eSports (LGA1700 only)", Category = ComponentCategory.Cooler,
            Price = 290_000m, HeightMm = 157,
            SocketSupport = [Sockets.Lga1700], ImageUrl = Img + "feat-case-2.jpg"
        },
        new()
        {
            // AIO: the height is the pump block, not the radiator — that is what the side panel clears.
            Id = "cool-aio240", Name = "MSI MAG CORELIQUID 240R (240mm AIO)", Category = ComponentCategory.Cooler,
            Price = 1_180_000m, HeightMm = 52,
            SocketSupport = [Sockets.Lga1700, Sockets.Lga1851, Sockets.Am5], ImageUrl = Img + "feat-case-2.jpg"
        },
    ];

    // ---------- Storage ----------
    // No compatibility spec of its own: every drive here fits every board in the catalog. Present so
    // a build is complete and the total is realistic.
    private static readonly Component[] Storage =
    [
        new()
        {
            Id = "ssd-sata-500", Name = "Kingston A400 500GB SATA SSD", Category = ComponentCategory.Storage,
            Price = 420_000m, ImageUrl = Img + "feat-mobo-4.jpg"
        },
        new()
        {
            Id = "ssd-sata-1tb", Name = "Samsung 870 EVO 1TB SATA SSD", Category = ComponentCategory.Storage,
            Price = 890_000m, ImageUrl = Img + "feat-mobo-4.jpg"
        },
        new()
        {
            Id = "ssd-nvme-1tb", Name = "Samsung 980 PRO 1TB M.2 NVMe", Category = ComponentCategory.Storage,
            Price = 1_240_000m, ImageUrl = Img + "feat-mobo-4.jpg"
        },
        new()
        {
            Id = "ssd-nvme-2tb", Name = "WD Black SN850X 2TB M.2 NVMe", Category = ComponentCategory.Storage,
            Price = 2_380_000m, ImageUrl = Img + "feat-mobo-4.jpg"
        },
        new()
        {
            Id = "hdd-2tb", Name = "Seagate BarraCuda 2TB 7200rpm", Category = ComponentCategory.Storage,
            Price = 640_000m, ImageUrl = Img + "feat-mobo-4.jpg"
        },
    ];

    /// <summary>Every part, in category order.</summary>
    public static readonly IReadOnlyList<Component> All =
    [
        .. Motherboards, .. Cpus, .. Coolers, .. Ram, .. Storage, .. Gpus, .. PowerSupplies, .. Cases
    ];

    /// <summary>
    /// The ready-made builds. Every one is compatibility-clean — asserted by the test suite, so a
    /// change to the catalog or the rules that breaks a bundle fails the build rather than shipping.
    /// </summary>
    public static readonly IReadOnlyList<AssemblyBundle> Bundles =
    [
        new(
            Id: "bundle-office",
            Name: "Office / Study PC",
            Description: "Quiet Micro-ATX build for documents, browsing and study. Integrated graphics, no discrete card.",
            Platform: ComponentPlatform.Intel,
            ComponentIds:
            [
                "mb-h610m-k-d4", "cpu-i3-14100", "cool-stock-92", "ram-fury-16-ddr4",
                "ssd-sata-500", "psu-500-bronze", "case-matx-mid"
            ]),
        new(
            Id: "bundle-gaming-intel",
            Name: "Gaming PC — Intel",
            Description: "1440p gaming build on LGA1700 with DDR5 and an RTX 4070 SUPER.",
            Platform: ComponentPlatform.Intel,
            ComponentIds:
            [
                "mb-b760m-a-wifi", "cpu-i5-14600k", "cool-ak620", "ram-vengeance-32-ddr5",
                "ssd-nvme-1tb", "gpu-rtx4070s", "psu-750-gold", "case-atx-mid"
            ]),
        new(
            Id: "bundle-gaming-amd",
            Name: "Gaming PC — AMD",
            Description: "High-frame-rate AM5 build around the Ryzen 7 7800X3D and an RX 7800 XT.",
            Platform: ComponentPlatform.Amd,
            ComponentIds:
            [
                "mb-x670e-plus", "cpu-ryzen7-7800x3d", "cool-aio240", "ram-trident-32-ddr5",
                "ssd-nvme-2tb", "gpu-rx7800xt", "psu-850-gold", "case-atx-airflow"
            ]),
    ];

    private static readonly Dictionary<string, Component> ById =
        All.ToDictionary(part => part.Id, StringComparer.OrdinalIgnoreCase);

    /// <summary>The part with <paramref name="id"/>, or <see langword="null"/> if there is none.</summary>
    public static Component? Find(string? id) =>
        id is not null && ById.TryGetValue(id, out var part) ? part : null;

    /// <summary>Every part in <paramref name="category"/>, in catalog order.</summary>
    public static IReadOnlyList<Component> InCategory(ComponentCategory category) =>
        [.. All.Where(part => part.Category == category)];

    /// <summary>
    /// Parts usable on <paramref name="platform"/> — those built for it plus everything marked
    /// <see cref="ComponentPlatform.Either"/>. Passing <see cref="ComponentPlatform.Either"/> returns
    /// the unfiltered catalog.
    /// </summary>
    public static IReadOnlyList<Component> ForPlatform(ComponentPlatform platform) =>
        platform == ComponentPlatform.Either
            ? All
            : [.. All.Where(part =>
                part.Platform == platform || part.Platform == ComponentPlatform.Either)];

    /// <summary>
    /// The components of <paramref name="bundle"/>, in declared order.
    /// </summary>
    /// <exception cref="InvalidOperationException">A referenced id is not in the catalog. Thrown
    /// rather than skipped so a typo surfaces immediately instead of yielding a build that silently
    /// lost a part.</exception>
    public static IReadOnlyList<Component> Resolve(AssemblyBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        return
        [
            .. bundle.ComponentIds.Select(id =>
                Find(id) ?? throw new InvalidOperationException(
                    $"Bundle '{bundle.Id}' references unknown component '{id}'."))
        ];
    }

    /// <summary>Sum of the parts' prices, in UZS.</summary>
    public static decimal TotalPrice(IEnumerable<Component> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return parts.Sum(part => part.Price);
    }
}
