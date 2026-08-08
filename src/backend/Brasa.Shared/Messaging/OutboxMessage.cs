namespace Brasa.Shared.Messaging;

/// <summary>
/// A serialised <see cref="IIntegrationEvent"/> awaiting delivery, written in the
/// same database transaction as the state change that produced it.
/// </summary>
/// <remarks>
/// <para>
/// The transactional outbox is what makes "the order was closed" and "the kitchen
/// was told" impossible to disagree about. Without it, a crash between committing
/// the order and publishing the event silently loses the notification.
/// </para>
/// <para>
/// The same pattern carries POS mutations from a terminal to the Site Agent, and
/// from the Site Agent to the cloud. In an offline-first system the outbox is not
/// an optimisation — it is the sync mechanism.
/// </para>
/// </remarks>
public sealed class OutboxMessage
{
    /// <summary>Row id, and also the <see cref="IIntegrationEvent.EventId"/> it carries.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Owning tenant, so the dispatcher can restore the right tenant context before
    /// invoking handlers.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>Assembly-qualified CLR type name used to deserialise <see cref="Payload"/>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The event, serialised as JSON.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>When the event occurred, in UTC.</summary>
    public DateTimeOffset OccurredAtUtc { get; set; }

    /// <summary>When delivery succeeded, or null while still pending.</summary>
    public DateTimeOffset? ProcessedAtUtc { get; set; }

    /// <summary>How many delivery attempts have been made.</summary>
    public int AttemptCount { get; set; }

    /// <summary>The last failure, kept for diagnosis. Null while healthy.</summary>
    public string? LastError { get; set; }

    /// <summary>
    /// When the next attempt becomes eligible. Set on failure to back off; a
    /// permanently failing message stops being retried and is surfaced for a human.
    /// </summary>
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
}
