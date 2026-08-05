namespace PcMarket.Domain.Configurator;

/// <summary>
/// The configurator's compatibility rules (Phase 19, stage 1).
/// <para>
/// Every rule is a pure static function: same parts in, same warnings out, no state, no I/O, no
/// clock. That is what lets the whole engine be tested before any UI exists, and it is why each rule
/// is public rather than a private step inside <see cref="Check"/> — a test can drive one rule with
/// two parts instead of assembling a whole build to reach it.
/// </para>
/// <para>
/// <b>Nothing here blocks.</b> A rule returns a warning or it returns nothing; refusing a selection
/// is not this type's job. An incomplete build is silent by design too — every rule returns
/// <see langword="null"/> when a part it needs is missing or when that part lacks the spec the rule
/// reads. A shopper who has picked a CPU and no motherboard yet should not be told they are
/// incompatible; they are unfinished.
/// </para>
/// </summary>
public static class CompatibilityChecker
{
    /// <summary>
    /// Headroom multiplier applied to measured draw when sizing the PSU. 1.3 covers the parts that
    /// carry no <see cref="Component.PowerDraw"/> of their own (board, drives, fans) plus transient
    /// spikes, which are what actually trip a supply that looks adequate on paper.
    /// </summary>
    public const decimal PsuSafetyMargin = 1.3m;

    /// <summary>
    /// Runs every rule over <paramref name="selected"/> and returns the warnings, in rule order.
    /// Empty means "nothing found" — which for a part-built machine means "nothing found *yet*",
    /// not "verified complete".
    /// </summary>
    public static IReadOnlyList<CompatibilityWarning> Check(IEnumerable<Component> selected)
    {
        ArgumentNullException.ThrowIfNull(selected);

        var parts = selected.Where(part => part is not null).ToList();

        // First of each category. Builds with two drives or two sticks of RAM are normal; two CPUs
        // or two cases are not, so taking the first is right for every slot a rule reads.
        var cpu = First(parts, ComponentCategory.Cpu);
        var motherboard = First(parts, ComponentCategory.Motherboard);
        var ram = First(parts, ComponentCategory.Ram);
        var gpu = First(parts, ComponentCategory.Gpu);
        var psu = First(parts, ComponentCategory.Psu);
        var chassis = First(parts, ComponentCategory.Case);
        var cooler = First(parts, ComponentCategory.Cooler);

        var warnings = new List<CompatibilityWarning?>
        {
            CheckSocket(cpu, motherboard),
            CheckRamType(ram, motherboard),
            CheckPsuWattage(parts, psu),
            CheckCaseFormFactor(motherboard, chassis),
            CheckGpuLength(gpu, chassis),
            CheckCoolerHeight(cooler, chassis),
            CheckCoolerSocket(cooler, cpu),
        };

        return [.. warnings.OfType<CompatibilityWarning>()];
    }

    /// <summary>CPU socket must match the motherboard's socket.</summary>
    public static CompatibilityWarning? CheckSocket(Component? cpu, Component? motherboard)
    {
        if (cpu?.Socket is not { } cpuSocket || motherboard?.Socket is not { } boardSocket)
        {
            return null;
        }

        if (SocketEquals(cpuSocket, boardSocket))
        {
            return null;
        }

        return new CompatibilityWarning(
            CompatibilityIssue.SocketMismatch,
            $"{cpu.Name} is a {cpuSocket} processor, but {motherboard.Name} has a {boardSocket} socket. " +
            "Pick a board and a processor that use the same socket.",
            [cpu.Id, motherboard.Id]);
    }

    /// <summary>RAM generation must match what the motherboard accepts.</summary>
    public static CompatibilityWarning? CheckRamType(Component? ram, Component? motherboard)
    {
        if (ram?.RamType is not { } ramType || motherboard?.RamType is not { } boardType)
        {
            return null;
        }

        if (ramType == boardType)
        {
            return null;
        }

        return new CompatibilityWarning(
            CompatibilityIssue.RamTypeMismatch,
            $"{motherboard.Name} needs {Label(boardType)} — your memory is {Label(ramType)}. " +
            $"DDR4 and DDR5 do not fit the same slots, so swap one of them.",
            [ram.Id, motherboard.Id]);
    }

    /// <summary>
    /// PSU output must cover measured draw plus <see cref="PsuSafetyMargin"/>. Takes the whole
    /// selection rather than a pair, since the draw is the sum across every selected part.
    /// </summary>
    public static CompatibilityWarning? CheckPsuWattage(IEnumerable<Component> selected, Component? psu)
    {
        ArgumentNullException.ThrowIfNull(selected);

        if (psu?.Wattage is not { } supplied)
        {
            return null;
        }

        var parts = selected.Where(part => part is not null).ToList();
        var draw = TotalPowerDraw(parts);

        // Nothing in the build draws measurable power yet — a PSU alone tells us nothing.
        if (draw == 0)
        {
            return null;
        }

        var required = RequiredWattage(parts);
        if (supplied >= required)
        {
            return null;
        }

        return new CompatibilityWarning(
            CompatibilityIssue.InsufficientPsuWattage,
            $"{psu.Name} supplies {supplied}W, but this build draws about {draw}W and wants at least " +
            $"{required}W with headroom. Choose a larger power supply.",
            [psu.Id]);
    }

    /// <summary>Motherboard form factor must be one the case has standoffs for.</summary>
    public static CompatibilityWarning? CheckCaseFormFactor(Component? motherboard, Component? chassis)
    {
        if (motherboard?.FormFactor is not { } boardSize ||
            chassis?.SupportedFormFactors is not { Count: > 0 } supported)
        {
            return null;
        }

        if (supported.Contains(boardSize))
        {
            return null;
        }

        var fits = string.Join(", ", supported.Select(Label));

        return new CompatibilityWarning(
            CompatibilityIssue.CaseFormFactorUnsupported,
            $"{motherboard.Name} is {Label(boardSize)}, and {chassis.Name} only fits {fits}. " +
            "Choose a larger case or a smaller board.",
            [motherboard.Id, chassis.Id]);
    }

    /// <summary>Graphics card must be no longer than the case allows.</summary>
    public static CompatibilityWarning? CheckGpuLength(Component? gpu, Component? chassis)
    {
        if (gpu?.LengthMm is not { } length || chassis?.MaxGpuLengthMm is not { } maxLength)
        {
            return null;
        }

        if (length <= maxLength)
        {
            return null;
        }

        return new CompatibilityWarning(
            CompatibilityIssue.GpuTooLong,
            $"{gpu.Name} is {length}mm long, and {chassis.Name} takes cards up to {maxLength}mm. " +
            $"It will not fit — you are over by {length - maxLength}mm.",
            [gpu.Id, chassis.Id]);
    }

    /// <summary>Cooler must be no taller than the case's side panel allows.</summary>
    public static CompatibilityWarning? CheckCoolerHeight(Component? cooler, Component? chassis)
    {
        if (cooler?.HeightMm is not { } height || chassis?.MaxCoolerHeightMm is not { } maxHeight)
        {
            return null;
        }

        if (height <= maxHeight)
        {
            return null;
        }

        return new CompatibilityWarning(
            CompatibilityIssue.CoolerTooTall,
            $"{cooler.Name} stands {height}mm tall, and {chassis.Name} clears {maxHeight}mm. " +
            "The side panel will not close — choose a lower-profile cooler.",
            [cooler.Id, chassis.Id]);
    }

    /// <summary>Cooler must ship mounting hardware for the CPU's socket.</summary>
    public static CompatibilityWarning? CheckCoolerSocket(Component? cooler, Component? cpu)
    {
        if (cooler?.SocketSupport is not { Count: > 0 } supported || cpu?.Socket is not { } cpuSocket)
        {
            return null;
        }

        if (supported.Any(socket => SocketEquals(socket, cpuSocket)))
        {
            return null;
        }

        var fits = string.Join(", ", supported);

        return new CompatibilityWarning(
            CompatibilityIssue.CoolerSocketUnsupported,
            $"{cooler.Name} has no mounting hardware for {cpuSocket} — it supports {fits}. " +
            "Choose a cooler that fits your processor's socket.",
            [cooler.Id, cpu.Id]);
    }

    /// <summary>Measured draw across the selection, in watts. Parts with no rating count as zero.</summary>
    public static int TotalPowerDraw(IEnumerable<Component> selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        return selected.Where(part => part is not null).Sum(part => part.PowerDraw ?? 0);
    }

    /// <summary>
    /// Smallest PSU this build should be paired with: draw × <see cref="PsuSafetyMargin"/>, rounded
    /// up. Public because the UI wants to show the recommendation next to the PSU list, not only
    /// complain after the fact.
    /// </summary>
    public static int RequiredWattage(IEnumerable<Component> selected) =>
        (int)Math.Ceiling(TotalPowerDraw(selected) * PsuSafetyMargin);

    private static Component? First(IEnumerable<Component> parts, ComponentCategory category) =>
        parts.FirstOrDefault(part => part.Category == category);

    // Socket names come from seeded data and, later, possibly an import — compare them the way a
    // human would rather than letting "lga1700" miss "LGA1700".
    private static bool SocketEquals(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string Label(RamType type) => type switch
    {
        Configurator.RamType.Ddr4 => "DDR4",
        Configurator.RamType.Ddr5 => "DDR5",
        _ => type.ToString()
    };

    private static string Label(FormFactor formFactor) => formFactor switch
    {
        Configurator.FormFactor.Itx => "Mini-ITX",
        Configurator.FormFactor.MicroAtx => "Micro-ATX",
        Configurator.FormFactor.Atx => "ATX",
        _ => formFactor.ToString()
    };
}
