using System.Diagnostics;

namespace Brasa.Api.Jobs;

/// <summary>
/// Wraps the already-verified <c>infra/scripts/backup-database.ps1</c> and
/// <c>restore-drill.ps1</c> as Hangfire recurring jobs (OPS-10 — closing
/// OPS-12's own "nothing schedules it yet" gap). Shells out to the existing
/// PowerShell scripts rather than reimplementing pg_dump/pg_restore
/// orchestration in C# — that logic is already written, already drilled,
/// and already has its own documented reasoning
/// (docs/development/backup-and-restore.md) for things like never piping a
/// binary dump through a PowerShell text redirect; duplicating it here
/// would just be a second place for that reasoning to go stale.
/// </summary>
/// <remarks>
/// Dev-only by construction, not just by registration: both scripts default
/// to the local <c>brasa-postgres</c> Docker container name
/// (infra/docker-compose.yml), which won't exist under a real production
/// topology (OPS-11). <c>Program.cs</c> only registers the recurring jobs
/// outside <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.IsProduction"/>
/// for exactly that reason, not just as a generic safety habit.
/// </remarks>
public sealed partial class DatabaseBackupJob
{
    private readonly ILogger<DatabaseBackupJob> _logger;
    private readonly string _scriptsDirectory;

    public DatabaseBackupJob(ILogger<DatabaseBackupJob> logger, IWebHostEnvironment environment)
    {
        _logger = logger;

        // Brasa.Api's ContentRootPath is src/backend/Brasa.Api; the scripts
        // live at <repo root>/infra/scripts, three levels up.
        var repositoryRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", ".."));
        _scriptsDirectory = Path.Combine(repositoryRoot, "infra", "scripts");
    }

    /// <summary>The frequent, cheap half — a routine backup with no restore/verification step.</summary>
    public Task RunBackupAsync(CancellationToken cancellationToken)
    {
        // backup-database.ps1's own -OutputDir default resolves via
        // $PSScriptRoot inside its param() block. That default is reliable
        // when the script runs nested (e.g. restore-drill.ps1 invoking it
        // via &) but empty when it is itself the direct -File target of a
        // redirected, non-interactive process launch — exactly what
        // Process.Start below does. A real, reproducible Windows
        // PowerShell 5.1 quirk (confirmed live: the same script invoked
        // via -File from a plain non-interactive shell fails identically,
        // with no Hangfire involved at all), not specific to Hangfire.
        // Passing -OutputDir explicitly sidesteps needing $PSScriptRoot to
        // resolve at all, without touching the already-verified script.
        var backupsDirectory = Path.Combine(_scriptsDirectory, "..", "backups");
        return RunScriptAsync("backup-database.ps1", cancellationToken, "-OutputDir", backupsDirectory);
    }

    /// <summary>
    /// The periodic, expensive half — backs up, restores into a scratch
    /// database, and compares row counts table by table, exactly like a
    /// human running the drill by hand. See backup-and-restore.md's own
    /// "why tested, not just automated" reasoning for why this half exists
    /// at all rather than trusting a backup that merely exits <c>0</c>.
    /// </summary>
    /// <remarks>
    /// Unaffected by <see cref="RunBackupAsync"/>'s own <c>$PSScriptRoot</c>
    /// quirk — restore-drill.ps1 resolves its own nested call to
    /// backup-database.ps1 in its script body, not a <c>param()</c> default,
    /// where <c>$PSScriptRoot</c> is reliable regardless of how the
    /// top-level script itself was invoked. Confirmed live, not assumed.
    /// </remarks>
    public Task RunRestoreDrillAsync(CancellationToken cancellationToken) =>
        RunScriptAsync("restore-drill.ps1", cancellationToken);

    private async Task RunScriptAsync(string scriptName, CancellationToken cancellationToken, params string[] scriptArguments)
    {
        var scriptPath = Path.Combine(_scriptsDirectory, scriptName);
        LogStarting(scriptName);

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            ArgumentList = { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in scriptArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            LogFailed(scriptName, process.ExitCode, stdout, stderr);

            // Throwing, not swallowing, is what makes Hangfire's own
            // automatic retry-with-backoff actually engage — a failed
            // backup that silently logs and moves on defeats the entire
            // point of scheduling this at all.
            throw new InvalidOperationException($"{scriptName} failed with exit code {process.ExitCode}: {stderr}");
        }

        LogCompleted(scriptName, stdout);
    }

    // Source-generated (CA1873) -- IsEnabled is checked internally before
    // the message template is formatted, so a disabled log level never
    // pays for formatting stdout/stderr.
    [LoggerMessage(Level = LogLevel.Information, Message = "Starting scheduled {Script}")]
    private partial void LogStarting(string script);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Script} completed. stdout: {Stdout}")]
    private partial void LogCompleted(string script, string stdout);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Script} failed with exit code {ExitCode}. stdout: {Stdout} stderr: {Stderr}")]
    private partial void LogFailed(string script, int exitCode, string stdout, string stderr);
}
