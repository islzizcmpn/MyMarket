using PcMarket.Domain.Configurator;

namespace PcMarket.UnitTests.Configurator;

/// <summary>
/// Integrity of the seeded catalog itself. These are what turn the rules engine from "correct in
/// principle" into "correct against the data we actually ship" — a spec typo that makes a bundle
/// incompatible fails here rather than surfacing as a warning banner in the Stage 2 UI.
/// </summary>
public class ComponentCatalogTests
{
    [Fact]
    public void EveryBundle_IsFreeOfCompatibilityWarnings()
    {
        foreach (var bundle in ComponentCatalog.Bundles)
        {
            var warnings = CompatibilityChecker.Check(ComponentCatalog.Resolve(bundle));

            Assert.True(
                warnings.Count == 0,
                $"Bundle '{bundle.Name}' is not clean:{Environment.NewLine}" +
                string.Join(Environment.NewLine, warnings.Select(w => $"  - [{w.Issue}] {w.Message}")));
        }
    }

    [Fact]
    public void EveryBundle_ResolvesAndCoversTheEssentialSlots()
    {
        // A ready-made assembly a shopper can buy has to be a whole machine. Graphics is the one
        // optional slot: the office build runs on the processor's integrated GPU.
        ComponentCategory[] essential =
        [
            ComponentCategory.Motherboard, ComponentCategory.Cpu, ComponentCategory.Cooler,
            ComponentCategory.Ram, ComponentCategory.Storage, ComponentCategory.Psu,
            ComponentCategory.Case
        ];

        Assert.NotEmpty(ComponentCatalog.Bundles);

        foreach (var bundle in ComponentCatalog.Bundles)
        {
            var parts = ComponentCatalog.Resolve(bundle);

            Assert.Equal(bundle.ComponentIds.Count, parts.Count);
            Assert.All(essential, category =>
                Assert.True(
                    parts.Any(part => part.Category == category),
                    $"Bundle '{bundle.Name}' has no {category}."));
            Assert.True(ComponentCatalog.TotalPrice(parts) > 0m);
        }
    }

    [Fact]
    public void EveryBundle_UsesPartsFromItsOwnPlatform()
    {
        foreach (var bundle in ComponentCatalog.Bundles)
        {
            Assert.All(ComponentCatalog.Resolve(bundle), part =>
                Assert.True(
                    part.Platform == bundle.Platform || part.Platform == ComponentPlatform.Either,
                    $"Bundle '{bundle.Name}' ({bundle.Platform}) contains {part.Platform} part '{part.Name}'."));
        }
    }

    [Fact]
    public void ComponentIds_AreUnique()
    {
        var duplicates = ComponentCatalog.All
            .GroupBy(part => part.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void EveryCategory_HasSelectableParts()
    {
        foreach (var category in Enum.GetValues<ComponentCategory>())
        {
            Assert.True(
                ComponentCatalog.InCategory(category).Count >= 4,
                $"Category {category} has too few parts to choose between.");
        }
    }

    [Theory]
    [InlineData(ComponentCategory.Cpu)]
    [InlineData(ComponentCategory.Motherboard)]
    public void BothPlatforms_CanBuildAMachine(ComponentCategory category)
    {
        foreach (var platform in new[] { ComponentPlatform.Intel, ComponentPlatform.Amd })
        {
            Assert.Contains(
                ComponentCatalog.ForPlatform(platform),
                part => part.Category == category && part.Platform == platform);
        }
    }

    [Fact]
    public void SpecsRequiredByTheRules_ArePresentForEveryCategoryThatNeedsThem()
    {
        // If a part in one of these categories is missing the spec its rule reads, that rule silently
        // never fires for it — the failure mode this test exists to catch.
        AssertAll(ComponentCategory.Cpu, part => part.Socket is not null && part.PowerDraw is not null);
        AssertAll(ComponentCategory.Motherboard,
            part => part.Socket is not null && part.RamType is not null && part.FormFactor is not null);
        AssertAll(ComponentCategory.Ram, part => part.RamType is not null);
        AssertAll(ComponentCategory.Gpu, part => part.PowerDraw is not null && part.LengthMm is not null);
        AssertAll(ComponentCategory.Psu, part => part.Wattage is not null);
        AssertAll(ComponentCategory.Cooler,
            part => part.HeightMm is not null && part.SocketSupport is { Count: > 0 });
        AssertAll(ComponentCategory.Case,
            part => part.SupportedFormFactors is { Count: > 0 }
                && part.MaxGpuLengthMm is not null
                && part.MaxCoolerHeightMm is not null);

        static void AssertAll(ComponentCategory category, Func<Component, bool> hasSpecs) =>
            Assert.All(ComponentCatalog.InCategory(category), part =>
                Assert.True(hasSpecs(part), $"'{part.Name}' ({category}) is missing a spec its rules read."));
    }

    [Fact]
    public void EveryPart_HasANameAndAPositivePrice()
    {
        Assert.All(ComponentCatalog.All, part =>
        {
            Assert.False(string.IsNullOrWhiteSpace(part.Name));
            Assert.True(part.Price > 0m, $"'{part.Name}' has a non-positive price.");
        });
    }

    [Fact]
    public void CoolerSocketSupport_OnlyNamesSocketsTheCatalogActuallySells()
    {
        var cpuSockets = ComponentCatalog
            .InCategory(ComponentCategory.Cpu)
            .Select(part => part.Socket!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.All(ComponentCatalog.InCategory(ComponentCategory.Cooler), cooler =>
            Assert.All(cooler.SocketSupport!, socket =>
                Assert.True(cpuSockets.Contains(socket),
                    $"'{cooler.Name}' claims socket '{socket}', which no processor in the catalog uses.")));
    }

    [Fact]
    public void Resolve_UnknownComponentId_Throws()
    {
        var broken = new AssemblyBundle("x", "Broken", "-", ComponentPlatform.Intel, ["no-such-part"]);

        var error = Assert.Throws<InvalidOperationException>(() => ComponentCatalog.Resolve(broken));
        Assert.Contains("no-such-part", error.Message);
    }

    [Fact]
    public void Find_IsCaseInsensitive_AndNullForUnknown()
    {
        Assert.NotNull(ComponentCatalog.Find("CPU-I5-14600K"));
        Assert.Null(ComponentCatalog.Find("not-a-part"));
        Assert.Null(ComponentCatalog.Find(null));
    }

    [Fact]
    public void ForPlatform_IncludesVendorNeutralParts()
    {
        var intel = ComponentCatalog.ForPlatform(ComponentPlatform.Intel);

        Assert.Contains(intel, part => part.Platform == ComponentPlatform.Intel);
        Assert.Contains(intel, part => part.Platform == ComponentPlatform.Either);
        Assert.DoesNotContain(intel, part => part.Platform == ComponentPlatform.Amd);
        Assert.Equal(ComponentCatalog.All.Count, ComponentCatalog.ForPlatform(ComponentPlatform.Either).Count);
    }

    /// <summary>
    /// The catalog has to be able to *produce* a mismatch, or the Stage 2 UI would have no way to
    /// show a warning and the rules would be unreachable in practice.
    /// </summary>
    [Fact]
    public void Catalog_CanProduceEachWarning_FromRealParts()
    {
        var lga1700Cpu = ComponentCatalog.Find("cpu-i5-14600k")!;
        var am5Board = ComponentCatalog.Find("mb-x670e-plus")!;
        var ddr4Ram = ComponentCatalog.Find("ram-fury-16-ddr4")!;
        var ddr5Board = ComponentCatalog.Find("mb-b760m-a-wifi")!;
        var bigGpu = ComponentCatalog.Find("gpu-rtx4080s")!;
        var smallCase = ComponentCatalog.Find("case-itx-compact")!;
        var atxBoard = ComponentCatalog.Find("mb-z790-ud-ax")!;
        var tallCooler = ComponentCatalog.Find("cool-ak620")!;
        var smallPsu = ComponentCatalog.Find("psu-500-bronze")!;
        var lga1851Cpu = ComponentCatalog.Find("cpu-ultra5-245k")!;
        var lga1700OnlyCooler = ComponentCatalog.Find("cool-freezer34")!;

        Assert.Equal(CompatibilityIssue.SocketMismatch,
            CompatibilityChecker.CheckSocket(lga1700Cpu, am5Board)!.Issue);
        Assert.Equal(CompatibilityIssue.RamTypeMismatch,
            CompatibilityChecker.CheckRamType(ddr4Ram, ddr5Board)!.Issue);
        Assert.Equal(CompatibilityIssue.InsufficientPsuWattage,
            CompatibilityChecker.CheckPsuWattage([lga1700Cpu, bigGpu, smallPsu], smallPsu)!.Issue);
        Assert.Equal(CompatibilityIssue.CaseFormFactorUnsupported,
            CompatibilityChecker.CheckCaseFormFactor(atxBoard, smallCase)!.Issue);
        Assert.Equal(CompatibilityIssue.GpuTooLong,
            CompatibilityChecker.CheckGpuLength(bigGpu, smallCase)!.Issue);
        Assert.Equal(CompatibilityIssue.CoolerTooTall,
            CompatibilityChecker.CheckCoolerHeight(tallCooler, smallCase)!.Issue);
        Assert.Equal(CompatibilityIssue.CoolerSocketUnsupported,
            CompatibilityChecker.CheckCoolerSocket(lga1700OnlyCooler, lga1851Cpu)!.Issue);
    }
}
