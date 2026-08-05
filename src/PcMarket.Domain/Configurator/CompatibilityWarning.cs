namespace PcMarket.Domain.Configurator;

/// <summary>Which rule produced a warning. Stable, so tests and the UI can key off it.</summary>
public enum CompatibilityIssue
{
    SocketMismatch = 0,
    RamTypeMismatch = 1,
    InsufficientPsuWattage = 2,
    CaseFormFactorUnsupported = 3,
    GpuTooLong = 4,
    CoolerTooTall = 5,
    CoolerSocketUnsupported = 6
}

/// <summary>
/// One compatibility problem found in a build. Advisory only — the configurator surfaces these and
/// still lets the shopper proceed, because the catalog can be wrong and a determined builder is
/// sometimes right.
/// <para>
/// Carries a machine-readable <see cref="Issue"/> alongside the human <see cref="Message"/> on
/// purpose. Tests assert on the code, which never changes; the Stage 2 UI can render
/// <see cref="Message"/> directly, or map the code to a <c>Configurator.*</c> resx key once these
/// need to appear in RU and UZ as well. Putting only a string here would have forced the UI to
/// pattern-match English prose to localize it.
/// </para>
/// </summary>
/// <param name="Issue">The rule that fired.</param>
/// <param name="Message">Plain-English explanation naming both offending parts.</param>
/// <param name="ComponentIds">Ids of the parts involved, so the UI can highlight the offending rows.</param>
public sealed record CompatibilityWarning(
    CompatibilityIssue Issue,
    string Message,
    IReadOnlyList<string> ComponentIds);
