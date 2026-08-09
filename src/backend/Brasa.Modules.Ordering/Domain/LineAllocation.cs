namespace Brasa.Modules.Ordering.Domain;

/// <summary>
/// A quantity of one order line assigned to a single guest's share when
/// splitting a bill by item (ORD-16), rather than evenly. E.g. "2 of the 3
/// Imperiais" go to one guest's group, the other 1 to someone else's.
/// </summary>
public sealed record LineAllocation(Guid LineId, int Quantity);
