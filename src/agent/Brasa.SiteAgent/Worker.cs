namespace Brasa.SiteAgent;

/// <summary>
/// Placeholder host loop for the Site Agent.
/// </summary>
/// <remarks>
/// <para>
/// The Site Agent runs inside the restaurant and is the reason the POS keeps
/// working when the internet does not. It will own four things
/// (see <c>docs/architecture/site-agent.md</c>):
/// </para>
/// <list type="number">
///   <item><description>Custody of the fiscal RSA private key, and offline document signing</description></item>
///   <item><description>ESC/POS printing to kitchen and bar stations</description></item>
///   <item><description>A LAN REST + SignalR hub the terminals and KDS connect to</description></item>
///   <item><description>Outbox sync to the cloud once connectivity returns</description></item>
/// </list>
/// <para>
/// None of that is implemented yet — it is Month 3 work. This loop only proves the
/// host starts and shuts down cleanly.
/// </para>
/// </remarks>
public sealed partial class Worker(ILogger<Worker> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }

        LogStopping(logger);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Site Agent started.")]
    private static partial void LogStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Site Agent stopping.")]
    private static partial void LogStopping(ILogger logger);
}
