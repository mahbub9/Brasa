namespace Brasa.Modules.Fiscal;

/// <summary>What to issue a fiscal document for.</summary>
/// <param name="TenantId">The issuing organization.</param>
/// <param name="OrderId">The order this document settles. Traceability only — providers never reach into Ordering's tables.</param>
/// <param name="Lines">
/// The priced lines. The document's totals are always derived from these — never
/// passed separately — so there is exactly one source of truth for what was sold.
/// </param>
public sealed record FiscalDocumentRequest(Guid TenantId, Guid OrderId, IReadOnlyList<FiscalDocumentLine> Lines);
