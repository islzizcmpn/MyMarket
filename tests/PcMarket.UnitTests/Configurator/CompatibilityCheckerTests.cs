using PcMarket.Domain.Configurator;

namespace PcMarket.UnitTests.Configurator;

/// <summary>
/// Stage-1 coverage for the configurator's rules engine: a passing and a failing case per rule,
/// plus the boundary each rule turns on and the "incomplete build stays quiet" behaviour.
/// <para>
/// Parts are built by the local helpers rather than taken from <see cref="ComponentCatalog"/>, so a
/// catalog edit can never quietly change what a rule test is asserting. The catalog gets its own
/// tests in <see cref="ComponentCatalogTests"/>.
/// </para>
/// </summary>
public class CompatibilityCheckerTests
{
    // ---------- Rule 1: CPU socket must equal motherboard socket ----------

    [Fact]
    public void Socket_MatchingSockets_NoWarning()
    {
        Assert.Null(CompatibilityChecker.CheckSocket(
            Cpu(socket: Sockets.Lga1700),
            Motherboard(socket: Sockets.Lga1700)));
    }

    [Fact]
    public void Socket_MismatchedSockets_Warns()
    {
        var warning = CompatibilityChecker.CheckSocket(
            Cpu(socket: Sockets.Am5),
            Motherboard(socket: Sockets.Lga1700));

        Assert.NotNull(warning);
        Assert.Equal(CompatibilityIssue.SocketMismatch, warning.Issue);
        Assert.Contains("AM5", warning.Message);
        Assert.Contains("LGA1700", warning.Message);
        Assert.Equal(["cpu", "mb"], warning.ComponentIds);
    }

    [Fact]
    public void Socket_DiffersOnlyByCase_NoWarning()
    {
        // Socket names may arrive from an import later; "lga1700" is the same socket as "LGA1700".
        Assert.Null(CompatibilityChecker.CheckSocket(
            Cpu(socket: "lga1700"),
            Motherboard(socket: "LGA1700")));
    }

    // ---------- Rule 2: RAM generation must equal motherboard RAM type ----------

    [Fact]
    public void RamType_MatchingGeneration_NoWarning()
    {
        Assert.Null(CompatibilityChecker.CheckRamType(
            Ram(RamType.Ddr5),
            Motherboard(ramType: RamType.Ddr5)));
    }

    [Fact]
    public void RamType_Ddr4InDdr5Board_Warns()
    {
        var warning = CompatibilityChecker.CheckRamType(
            Ram(RamType.Ddr4),
            Motherboard(ramType: RamType.Ddr5));

        Assert.NotNull(warning);
        Assert.Equal(CompatibilityIssue.RamTypeMismatch, warning.Issue);
        Assert.Contains("DDR5", warning.Message);
        Assert.Contains("DDR4", warning.Message);
    }

    // ---------- Rule 3: PSU wattage >= total draw x 1.3 ----------

    [Fact]
    public void Psu_ComfortablyOverBudget_NoWarning()
    {
        // 125 + 220 = 345W draw -> 448.5 required. 750W supply is ample.
        Assert.Null(CompatibilityChecker.CheckPsuWattage(
            [Cpu(power: 125), Gpu(power: 220), Psu(750)],
            Psu(750)));
    }

    [Fact]
    public void Psu_UnderBudget_Warns()
    {
        // 125 + 320 = 445W draw -> 578.5 -> 579 required. A 500W supply is short.
        var warning = CompatibilityChecker.CheckPsuWattage(
            [Cpu(power: 125), Gpu(power: 320), Psu(500)],
            Psu(500));

        Assert.NotNull(warning);
        Assert.Equal(CompatibilityIssue.InsufficientPsuWattage, warning.Issue);
        Assert.Contains("500W", warning.Message);
        Assert.Contains("445W", warning.Message);
        Assert.Contains("579W", warning.Message);
    }

    [Theory]
    // Draw 100W -> requires exactly 130W. The margin is inclusive, so 130 passes and 129 does not.
    [InlineData(130, false)]
    [InlineData(129, true)]
    public void Psu_AtTheMarginBoundary_TurnsOnExactlyAtRequired(int supplied, bool expectWarning)
    {
        var warning = CompatibilityChecker.CheckPsuWattage(
            [Cpu(power: 100), Psu(supplied)],
            Psu(supplied));

        Assert.Equal(expectWarning, warning is not null);
    }

    [Fact]
    public void Psu_NothingDrawingPowerYet_NoWarning()
    {
        // A PSU on its own says nothing about a build that has no CPU or GPU in it.
        Assert.Null(CompatibilityChecker.CheckPsuWattage([Psu(500)], Psu(500)));
    }

    [Fact]
    public void RequiredWattage_RoundsUp()
    {
        // 125 x 1.3 = 162.5 -> 163, never 162.
        Assert.Equal(163, CompatibilityChecker.RequiredWattage([Cpu(power: 125)]));
        Assert.Equal(125, CompatibilityChecker.TotalPowerDraw([Cpu(power: 125)]));
    }

    // ---------- Rule 4: motherboard form factor must be supported by the case ----------

    [Fact]
    public void FormFactor_BoardFitsCase_NoWarning()
    {
        Assert.Null(CompatibilityChecker.CheckCaseFormFactor(
            Motherboard(formFactor: FormFactor.MicroAtx),
            Case(supported: [FormFactor.Itx, FormFactor.MicroAtx, FormFactor.Atx])));
    }

    [Fact]
    public void FormFactor_AtxBoardInItxCase_Warns()
    {
        var warning = CompatibilityChecker.CheckCaseFormFactor(
            Motherboard(formFactor: FormFactor.Atx),
            Case(supported: [FormFactor.Itx]));

        Assert.NotNull(warning);
        Assert.Equal(CompatibilityIssue.CaseFormFactorUnsupported, warning.Issue);
        Assert.Contains("ATX", warning.Message);
        Assert.Contains("Mini-ITX", warning.Message);
    }

    // ---------- Rule 5: GPU length <= case maximum ----------

    [Fact]
    public void GpuLength_CardFits_NoWarning()
    {
        Assert.Null(CompatibilityChecker.CheckGpuLength(Gpu(length: 305), Case(maxGpu: 360)));
    }

    [Fact]
    public void GpuLength_CardTooLong_Warns()
    {
        var warning = CompatibilityChecker.CheckGpuLength(Gpu(length: 336), Case(maxGpu: 240));

        Assert.NotNull(warning);
        Assert.Equal(CompatibilityIssue.GpuTooLong, warning.Issue);
        Assert.Contains("336mm", warning.Message);
        Assert.Contains("240mm", warning.Message);
        Assert.Contains("96mm", warning.Message); // the overshoot, spelled out
    }

    [Fact]
    public void GpuLength_ExactlyAtTheLimit_NoWarning()
    {
        Assert.Null(CompatibilityChecker.CheckGpuLength(Gpu(length: 360), Case(maxGpu: 360)));
    }

    // ---------- Rule 6a: cooler height <= case maximum ----------

    [Fact]
    public void CoolerHeight_CoolerFits_NoWarning()
    {
        Assert.Null(CompatibilityChecker.CheckCoolerHeight(Cooler(height: 154), Case(maxCooler: 170)));
    }

    [Fact]
    public void CoolerHeight_CoolerTooTall_Warns()
    {
        var warning = CompatibilityChecker.CheckCoolerHeight(Cooler(height: 160), Case(maxCooler: 120));

        Assert.NotNull(warning);
        Assert.Equal(CompatibilityIssue.CoolerTooTall, warning.Issue);
        Assert.Contains("160mm", warning.Message);
        Assert.Contains("120mm", warning.Message);
    }

    [Fact]
    public void CoolerHeight_ExactlyAtTheLimit_NoWarning()
    {
        Assert.Null(CompatibilityChecker.CheckCoolerHeight(Cooler(height: 170), Case(maxCooler: 170)));
    }

    // ---------- Rule 6b: cooler must support the CPU's socket ----------

    [Fact]
    public void CoolerSocket_SocketSupported_NoWarning()
    {
        Assert.Null(CompatibilityChecker.CheckCoolerSocket(
            Cooler(sockets: [Sockets.Lga1700, Sockets.Am5]),
            Cpu(socket: Sockets.Am5)));
    }

    [Fact]
    public void CoolerSocket_SocketNotSupported_Warns()
    {
        var warning = CompatibilityChecker.CheckCoolerSocket(
            Cooler(sockets: [Sockets.Lga1700]),
            Cpu(socket: Sockets.Lga1851));

        Assert.NotNull(warning);
        Assert.Equal(CompatibilityIssue.CoolerSocketUnsupported, warning.Issue);
        Assert.Contains("LGA1851", warning.Message);
        Assert.Contains("LGA1700", warning.Message);
    }

    // ---------- Incomplete builds ----------

    [Fact]
    public void EveryRule_MissingCounterpart_StaysQuiet()
    {
        // A half-built machine is unfinished, not incompatible. Each rule needs both sides.
        Assert.Null(CompatibilityChecker.CheckSocket(Cpu(socket: Sockets.Am5), null));
        Assert.Null(CompatibilityChecker.CheckRamType(null, Motherboard(ramType: RamType.Ddr5)));
        Assert.Null(CompatibilityChecker.CheckPsuWattage([Cpu(power: 300)], null));
        Assert.Null(CompatibilityChecker.CheckCaseFormFactor(Motherboard(formFactor: FormFactor.Atx), null));
        Assert.Null(CompatibilityChecker.CheckGpuLength(Gpu(length: 400), null));
        Assert.Null(CompatibilityChecker.CheckCoolerHeight(Cooler(height: 200), null));
        Assert.Null(CompatibilityChecker.CheckCoolerSocket(Cooler(sockets: [Sockets.Am5]), null));
    }

    [Fact]
    public void Check_EmptySelection_NoWarnings()
    {
        Assert.Empty(CompatibilityChecker.Check([]));
    }

    [Fact]
    public void Check_PartLacksTheSpecARuleReads_StaysQuiet()
    {
        // A case with no declared limits cannot contradict anything.
        Assert.Null(CompatibilityChecker.CheckGpuLength(Gpu(length: 400), Case()));
        Assert.Null(CompatibilityChecker.CheckCaseFormFactor(Motherboard(formFactor: FormFactor.Atx), Case()));
    }

    // ---------- Whole-build composition ----------

    [Fact]
    public void Check_FullyCompatibleBuild_NoWarnings()
    {
        Assert.Empty(CompatibilityChecker.Check(
        [
            Cpu(socket: Sockets.Lga1700, power: 125),
            Motherboard(socket: Sockets.Lga1700, ramType: RamType.Ddr5, formFactor: FormFactor.MicroAtx),
            Ram(RamType.Ddr5),
            Gpu(power: 220, length: 305),
            Psu(750),
            Case(supported: [FormFactor.MicroAtx, FormFactor.Atx], maxGpu: 360, maxCooler: 170),
            Cooler(height: 160, sockets: [Sockets.Lga1700]),
        ]));
    }

    [Fact]
    public void Check_BuildBreakingEveryRule_ReportsAllSevenOnce()
    {
        var warnings = CompatibilityChecker.Check(
        [
            Cpu(socket: Sockets.Lga1851, power: 125),                                   // wrong socket
            Motherboard(socket: Sockets.Am5, ramType: RamType.Ddr5, formFactor: FormFactor.Atx),
            Ram(RamType.Ddr4),                                                          // wrong memory
            Gpu(power: 320, length: 336),                                               // too long
            Psu(400),                                                                   // too small
            Case(supported: [FormFactor.Itx], maxGpu: 240, maxCooler: 120),             // too small
            Cooler(height: 160, sockets: [Sockets.Lga1700]),                            // too tall, wrong socket
        ]);

        Assert.Equal(
            [
                CompatibilityIssue.SocketMismatch,
                CompatibilityIssue.RamTypeMismatch,
                CompatibilityIssue.InsufficientPsuWattage,
                CompatibilityIssue.CaseFormFactorUnsupported,
                CompatibilityIssue.GpuTooLong,
                CompatibilityIssue.CoolerTooTall,
                CompatibilityIssue.CoolerSocketUnsupported,
            ],
            warnings.Select(warning => warning.Issue));
    }

    [Fact]
    public void Check_EveryWarningNamesItsPartsAndReadsAsASentence()
    {
        var warnings = CompatibilityChecker.Check(
        [
            Cpu(socket: Sockets.Am5, power: 120),
            Motherboard(socket: Sockets.Lga1700, ramType: RamType.Ddr5, formFactor: FormFactor.Atx),
            Ram(RamType.Ddr4),
            Case(supported: [FormFactor.Itx], maxGpu: 200, maxCooler: 100),
            Gpu(power: 300, length: 336),
            Cooler(height: 160, sockets: [Sockets.Lga1700]),
            Psu(300),
        ]);

        Assert.NotEmpty(warnings);
        Assert.All(warnings, warning =>
        {
            Assert.NotEmpty(warning.ComponentIds);
            Assert.EndsWith(".", warning.Message);
            // Long enough to be an explanation rather than a code, and it names a real part.
            Assert.True(warning.Message.Length > 40, $"Terse message: {warning.Message}");
        });
    }

    [Fact]
    public void Check_NullSelection_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CompatibilityChecker.Check(null!));
    }

    // ---------- Builders. Only the fields a given rule reads are set; everything else stays null,
    // which is also what proves the rules never depend on a spec they did not ask for. ----------

    private static Component Cpu(string socket = Sockets.Lga1700, int? power = null) => new()
    {
        Id = "cpu", Name = "Test CPU", Category = ComponentCategory.Cpu, Price = 1m,
        Socket = socket, PowerDraw = power
    };

    private static Component Motherboard(
        string socket = Sockets.Lga1700,
        RamType? ramType = null,
        FormFactor? formFactor = null) => new()
        {
            Id = "mb", Name = "Test Motherboard", Category = ComponentCategory.Motherboard, Price = 1m,
            Socket = socket, RamType = ramType, FormFactor = formFactor
        };

    private static Component Ram(RamType type) => new()
    {
        Id = "ram", Name = "Test Memory", Category = ComponentCategory.Ram, Price = 1m, RamType = type
    };

    private static Component Gpu(int? power = null, int? length = null) => new()
    {
        Id = "gpu", Name = "Test Graphics Card", Category = ComponentCategory.Gpu, Price = 1m,
        PowerDraw = power, LengthMm = length
    };

    private static Component Psu(int wattage) => new()
    {
        Id = "psu", Name = "Test Power Supply", Category = ComponentCategory.Psu, Price = 1m,
        Wattage = wattage
    };

    private static Component Case(
        IReadOnlyList<FormFactor>? supported = null,
        int? maxGpu = null,
        int? maxCooler = null) => new()
        {
            Id = "case", Name = "Test Case", Category = ComponentCategory.Case, Price = 1m,
            SupportedFormFactors = supported, MaxGpuLengthMm = maxGpu, MaxCoolerHeightMm = maxCooler
        };

    private static Component Cooler(int? height = null, IReadOnlyList<string>? sockets = null) => new()
    {
        Id = "cooler", Name = "Test Cooler", Category = ComponentCategory.Cooler, Price = 1m,
        HeightMm = height, SocketSupport = sockets
    };
}
