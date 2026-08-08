using System.Collections.Concurrent;
using System.Globalization;
using Brasa.Modules.Fiscal;
using Brasa.Shared.Primitives;
using Brasa.Shared.Time;

namespace Brasa.Fiscal.Mock;

/// <summary>
/// Deterministic, fiscally meaningless <see cref="IFiscalProvider"/> for
/// development and tests.
/// </summary>
/// <remarks>
/// <para>
/// Produces structurally valid documents — a document number, an ATCUD-shaped
/// string, a QR-shaped string — so the POS can be built and demoed end to end
/// before the certified engine (<c>Brasa.Fiscal.Portugal</c>, I7) exists. Every
/// mock value is clearly prefixed <c>MOCK-</c> so it can never be mistaken for a
/// real fiscal artefact in a screenshot, a log, or a support ticket.
/// </para>
/// <para>
/// Numbering is in-memory and resets on restart. That is intentional: the
/// gapless, crash-safe numbering guarantee (FIS-10) is exactly the property this
/// provider must not appear to have, because appearing to have it would make a
/// developer trust output that no certification body ever reviewed.
/// </para>
/// <para><b>Must never run in Production</b> — enforced by <see cref="FiscalMockServiceCollectionExtensions"/>.</para>
/// </remarks>
public sealed class MockFiscalProvider(IClock clock) : IFiscalProvider
{
    private const string SeriesId = "MOCK-A";

    private readonly ConcurrentDictionary<Guid, long> _sequenceByTenant = new();

    /// <inheritdoc/>
    public Task<Result<FiscalDocument>> IssueSimplifiedInvoiceAsync(
        FiscalDocumentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Lines.Count == 0)
        {
            return Task.FromResult(Result.Failure<FiscalDocument>(
                Error.Validation("fiscal.no_lines", "Cannot issue a document with no lines.")));
        }

        var number = _sequenceByTenant.AddOrUpdate(request.TenantId, 1, static (_, current) => current + 1);

        var netTotal = Money.Sum(request.Lines.Select(l => l.NetTotal));
        var vatTotal = Money.Sum(request.Lines.Select(l => l.VatAmount));
        var grossTotal = netTotal + vatTotal;

        var issuedAt = clock.UtcNow;
        var atcud = string.Create(CultureInfo.InvariantCulture, $"MOCK-{SeriesId}-{number}");
        var qrPayload = string.Create(
            CultureInfo.InvariantCulture,
            $"MOCK|A:{request.TenantId:N}|G:{grossTotal.MinorUnits}|N:{number}|NOT-FOR-FISCAL-USE");

        var document = new FiscalDocument(
            FiscalDocumentType.SimplifiedInvoice,
            SeriesId,
            number,
            atcud,
            issuedAt,
            netTotal,
            vatTotal,
            grossTotal,
            qrPayload);

        return Task.FromResult(Result.Success(document));
    }
}
