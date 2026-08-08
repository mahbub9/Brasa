using Brasa.Shared.Primitives;

namespace Brasa.Modules.Fiscal;

/// <summary>An issued fiscal document.</summary>
/// <remarks>
/// Once returned by <see cref="IFiscalProvider"/>, a document is immutable —
/// there is no update path. Corrections are a new <see cref="FiscalDocumentType.CreditNote"/>.
/// See <c>docs/fiscal/README.md</c>.
/// </remarks>
/// <param name="DocumentType">Which kind of document this is.</param>
/// <param name="SeriesId">
/// The series it was issued against. Real series are registered with AT and
/// numbered independently of each other — see
/// <c>docs/architecture/decisions/0003-site-agent.md</c>.
/// </param>
/// <param name="Number">Sequential number within the series. Gapless by construction.</param>
/// <param name="Atcud">The unique document code. AT-issued in production; a clearly marked placeholder from the mock provider.</param>
/// <param name="IssuedAtUtc">When the document was issued.</param>
/// <param name="NetTotal">Sum of every line's net total.</param>
/// <param name="VatTotal">Sum of every line's VAT.</param>
/// <param name="GrossTotal">Net plus VAT — the amount the guest pays.</param>
/// <param name="QrPayload">The pipe-delimited QR payload. A placeholder from the mock provider.</param>
public sealed record FiscalDocument(
    FiscalDocumentType DocumentType,
    string SeriesId,
    long Number,
    string Atcud,
    DateTimeOffset IssuedAtUtc,
    Money NetTotal,
    Money VatTotal,
    Money GrossTotal,
    string QrPayload)
{
    /// <summary>The human-readable document reference, e.g. <c>FS A/1</c>.</summary>
    public string DocumentNumber => $"{DocumentTypeCode(DocumentType)} {SeriesId}/{Number}";

    private static string DocumentTypeCode(FiscalDocumentType type) => type switch
    {
        FiscalDocumentType.SimplifiedInvoice => "FS",
        FiscalDocumentType.Invoice => "FT",
        FiscalDocumentType.InvoiceReceipt => "FR",
        FiscalDocumentType.CreditNote => "NC",
        FiscalDocumentType.NonFiscal => "DOC",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown document type."),
    };
}
