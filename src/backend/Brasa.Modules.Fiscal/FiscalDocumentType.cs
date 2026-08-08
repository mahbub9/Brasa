namespace Brasa.Modules.Fiscal;

/// <summary>
/// Portuguese fiscal document types. See <c>docs/fiscal/README.md</c>.
/// </summary>
/// <remarks>
/// I0 issues only <see cref="SimplifiedInvoice"/> — the restaurant workhorse.
/// The others are named here so the wire contract does not change shape when
/// FIS-14…17 implement them.
/// </remarks>
public enum FiscalDocumentType
{
    /// <summary><c>FS</c> — fatura simplificada. Permitted to final consumers up to €1,000.</summary>
    SimplifiedInvoice = 0,

    /// <summary><c>FT</c> — fatura. Full invoice with the customer's NIF.</summary>
    Invoice = 1,

    /// <summary><c>FR</c> — fatura-recibo. Invoice and receipt combined.</summary>
    InvoiceReceipt = 2,

    /// <summary><c>NC</c> — nota de crédito. The only lawful way to correct an issued document.</summary>
    CreditNote = 3,

    /// <summary>
    /// A pre-bill (<i>a conta</i>) or other <i>documento não fiscal</i>. Never
    /// treated as an invoice — see <c>docs/fiscal/README.md</c> on why issuing
    /// a pre-bill as an invoice would fiscalise every table that asks to see it.
    /// </summary>
    NonFiscal = 4,
}
