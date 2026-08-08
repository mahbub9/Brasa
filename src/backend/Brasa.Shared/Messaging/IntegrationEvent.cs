namespace Brasa.Shared.Messaging;

/// <summary>
/// A fact that one module publishes and others may react to, such as
/// <c>OrderClosed</c> or <c>FiscalDocumentIssued</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the <b>only</b> sanctioned way for modules to talk to each other.
/// Modules do not reference each other's projects and never read each other's
/// tables; see <c>docs/architecture/module-boundaries.md</c>.
/// </para>
/// <para>
/// Events are written to the outbox in the same transaction as the state change
/// that produced them, then dispatched afterwards. Today dispatch is in-process.
/// When a module is extracted into its own service, only the dispatcher changes —
/// publishers and handlers do not.
/// </para>
/// </remarks>
public interface IIntegrationEvent
{
    /// <summary>Unique id of this occurrence, used for idempotent handling.</summary>
    Guid EventId { get; }

    /// <summary>When the fact occurred, in UTC.</summary>
    DateTimeOffset OccurredAtUtc { get; }
}

/// <summary>Convenience base for <see cref="IIntegrationEvent"/> implementations.</summary>
/// <param name="EventId">Unique id of this occurrence.</param>
/// <param name="OccurredAtUtc">When the fact occurred, in UTC.</param>
public abstract record IntegrationEvent(Guid EventId, DateTimeOffset OccurredAtUtc) : IIntegrationEvent;

/// <summary>Handles an integration event of a specific type.</summary>
/// <typeparam name="TEvent">The event handled.</typeparam>
/// <remarks>
/// Handlers must be idempotent. The outbox guarantees at-least-once delivery, so a
/// handler can and will see the same <see cref="IIntegrationEvent.EventId"/> twice
/// after a retry or a crash between dispatch and acknowledgement.
/// </remarks>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    /// <summary>Reacts to the event.</summary>
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}

/// <summary>Delivers integration events to their handlers.</summary>
public interface IIntegrationEventDispatcher
{
    /// <summary>Dispatches one event to every registered handler for its type.</summary>
    Task DispatchAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken);
}
