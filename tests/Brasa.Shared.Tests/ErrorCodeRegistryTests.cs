using System.Text.RegularExpressions;

namespace Brasa.Shared.Tests;

/// <summary>
/// API-04 — <c>Error.Code</c> is a public contract clients branch on and
/// that must never silently change meaning (hard rule 11,
/// <c>docs/ai/README.md</c>; the registry itself is
/// <c>docs/architecture/error-codes.md</c>).
/// </summary>
/// <remarks>
/// <para>
/// This scans every <c>Error.Validation/NotFound/Conflict/Forbidden/Failure/RateLimited(...)</c>
/// call site under <c>src/</c> via a plain text regex — no Roslyn, no
/// compiled-assembly reflection, just reading <c>.cs</c> files as strings.
/// That is deliberately simple: this test only has to notice a code
/// appearing, disappearing, or changing <see cref="ErrorType"/> between
/// commits, not understand C#.
/// </para>
/// <para>
/// No Docker, no database — this runs as fast as any other unit test in this
/// project despite reading files.
/// </para>
/// </remarks>
public class ErrorCodeRegistryTests
{
    private static readonly Regex CallSitePattern = new(
        "Error\\.(Validation|NotFound|Conflict|Forbidden|Failure|RateLimited)\\(\\s*\"([a-z0-9_.]+)\"",
        RegexOptions.Compiled);

    private static readonly Regex RegistryRowPattern = new(
        @"^\|\s*`([a-z0-9_.]+)`\s*\|\s*(\w+)\s*\|",
        RegexOptions.Compiled | RegexOptions.Multiline);

    [Fact]
    public void Every_error_code_in_source_matches_the_registry_exactly()
    {
        var repoRoot = FindRepoRoot();
        var sourceCodes = ScanSourceForErrorCodes(Path.Combine(repoRoot, "src"));
        var registryCodes = ParseRegistry(Path.Combine(repoRoot, "docs", "architecture", "error-codes.md"));

        var undocumented = sourceCodes.Keys.Except(registryCodes.Keys).Order().ToArray();
        undocumented.ShouldBeEmpty(
            "these error codes exist in source but aren't in docs/architecture/error-codes.md — " +
            $"add a row (same commit as the code that introduced them): {string.Join(", ", undocumented)}");

        var vanished = registryCodes.Keys.Except(sourceCodes.Keys).Order().ToArray();
        vanished.ShouldBeEmpty(
            "these error codes are documented but no longer exist in source. If genuinely retired, " +
            "move the row to the registry's \"Retired codes\" section instead of deleting it — a code " +
            $"disappearing from git history silently is exactly what this test exists to catch: {string.Join(", ", vanished)}");

        foreach (var (code, sourceType) in sourceCodes)
        {
            registryCodes[code].ShouldBe(
                sourceType,
                $"'{code}' is now constructed via Error.{sourceType}(...) but the registry still lists " +
                $"{registryCodes[code]} — that changes the HTTP status a client sees for the same code, " +
                "which is a meaning change, not a documentation lag. Update the registry row.");
        }
    }

    private static Dictionary<string, string> ScanSourceForErrorCodes(string srcDirectory)
    {
        var codes = new Dictionary<string, string>();
        var separators = new[]
        {
            $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
        };

        foreach (var file in Directory.EnumerateFiles(srcDirectory, "*.cs", SearchOption.AllDirectories))
        {
            if (separators.Any(file.Contains))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            foreach (Match match in CallSitePattern.Matches(text))
            {
                // Last writer wins; two call sites sharing one code with the
                // same ErrorType are fine (order.not_open et al. do this).
                codes[match.Groups[2].Value] = match.Groups[1].Value;
            }
        }

        return codes;
    }

    private static Dictionary<string, string> ParseRegistry(string registryPath)
    {
        var text = File.ReadAllText(registryPath);

        // Only the active table counts — rows moved under "## Retired codes"
        // are historical record, not a current requirement.
        var retiredHeadingIndex = text.IndexOf("## Retired codes", StringComparison.Ordinal);
        if (retiredHeadingIndex >= 0)
        {
            text = text[..retiredHeadingIndex];
        }

        return RegistryRowPattern.Matches(text)
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Brasa.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException(
                "Could not find Brasa.slnx walking up from the test runtime directory — " +
                $"started at {AppContext.BaseDirectory}.");
    }
}
