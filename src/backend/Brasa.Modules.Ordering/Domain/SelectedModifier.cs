using Brasa.Shared.Primitives;

namespace Brasa.Modules.Ordering.Domain;

/// <summary>
/// A modifier the API layer has already resolved and validated against the
/// Catalog module — name and price delta as of right now, ready to snapshot
/// onto an <see cref="OrderLine"/>. Ordering never resolves a modifier id
/// itself; that would mean querying across the module boundary. See
/// <c>docs/architecture/module-boundaries.md</c>.
/// </summary>
public sealed record SelectedModifier(Guid ModifierId, string Name, Money PriceDelta);
