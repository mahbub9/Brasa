using Brasa.Shared.Primitives;

namespace Brasa.Modules.Fiscal;

/// <summary>
/// Issues Portuguese fiscal documents. The country seam: every implementation
/// behind this interface can be swapped without changing a caller.
/// </summary>
/// <remarks>
/// <para>
/// Two implementations exist. <c>Brasa.Fiscal.Mock</c> is deterministic and
/// fiscally meaningless — for development and tests, and it must never load in
/// Production (see the guard on its registration extension). <c>Brasa.Fiscal.Portugal</c>
/// is the real, AT-certifiable engine and lands in I7.
/// </para>
/// <para>
/// I0 issues only the simplified invoice. See <c>docs/fiscal/README.md</c> for
/// the full document lifecycle this interface will grow to cover.
/// </para>
/// </remarks>
public interface IFiscalProvider
{
    /// <summary>
    /// Issues a fatura simplificada (<c>FS</c>) for the given lines.
    /// </summary>
    /// <remarks>
    /// Never throws for an expected failure — an unregistered series, a broken
    /// chain — those return <see cref="Result{TValue}.Failure"/>. Exceptions are
    /// reserved for genuine faults (network, storage).
    /// </remarks>
    Task<Result<FiscalDocument>> IssueSimplifiedInvoiceAsync(
        FiscalDocumentRequest request,
        CancellationToken cancellationToken);
}
