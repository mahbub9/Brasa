using Brasa.Fiscal.Mock;
using Brasa.Modules.Fiscal;
using Brasa.Shared.Primitives;
using Brasa.Shared.Time;

namespace Brasa.Fiscal.Portugal.Tests;

/// <summary>Fixed instant, so document timestamps are deterministic in tests.</summary>
internal sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; } = now;
}

public sealed class MockFiscalProviderTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 8, 8, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Every_mock_value_is_unmistakably_marked_as_mock()
    {
        // A mock document must never be confused with a real one in a
        // screenshot, a log, or a support ticket. See Brasa.Fiscal.Mock's
        // remarks on why this is a design requirement, not decoration.
        var provider = new MockFiscalProvider(new FixedClock(FixedNow));
        var request = SingleLineRequest();

        var result = await provider.IssueSimplifiedInvoiceAsync(request, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Atcud.ShouldStartWith("MOCK-");
        result.Value.QrPayload.ShouldStartWith("MOCK|");
        result.Value.QrPayload.ShouldContain("NOT-FOR-FISCAL-USE");
        result.Value.SeriesId.ShouldStartWith("MOCK-");
    }

    [Fact]
    public async Task Numbering_is_sequential_and_gapless_per_tenant()
    {
        var provider = new MockFiscalProvider(new FixedClock(FixedNow));
        var tenantId = Guid.NewGuid();

        var first = await provider.IssueSimplifiedInvoiceAsync(SingleLineRequest(tenantId), CancellationToken.None);
        var second = await provider.IssueSimplifiedInvoiceAsync(SingleLineRequest(tenantId), CancellationToken.None);
        var third = await provider.IssueSimplifiedInvoiceAsync(SingleLineRequest(tenantId), CancellationToken.None);

        first.Value.Number.ShouldBe(1);
        second.Value.Number.ShouldBe(2);
        third.Value.Number.ShouldBe(3);
    }

    [Fact]
    public async Task Each_tenant_gets_its_own_independent_sequence()
    {
        // Two tenants issuing concurrently must never share or race on a
        // sequence — that would leak one tenant's document count to another
        // and could produce colliding document numbers.
        var provider = new MockFiscalProvider(new FixedClock(FixedNow));
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var a1 = await provider.IssueSimplifiedInvoiceAsync(SingleLineRequest(tenantA), CancellationToken.None);
        var b1 = await provider.IssueSimplifiedInvoiceAsync(SingleLineRequest(tenantB), CancellationToken.None);
        var a2 = await provider.IssueSimplifiedInvoiceAsync(SingleLineRequest(tenantA), CancellationToken.None);

        a1.Value.Number.ShouldBe(1);
        b1.Value.Number.ShouldBe(1);
        a2.Value.Number.ShouldBe(2);
    }

    [Fact]
    public async Task Totals_reconcile_across_mixed_VAT_rates()
    {
        // The exact I0 reproduction: food at 13%, alcohol at 23%, on one
        // document — the case that must never let net+vat drift from gross.
        var provider = new MockFiscalProvider(new FixedClock(FixedNow));
        var request = new FiscalDocumentRequest(Guid.NewGuid(), Guid.NewGuid(),
        [
            new FiscalDocumentLine("Frango na Brasa", 2, Money.FromDecimal(9.50m), 0.13m),
            new FiscalDocumentLine("Imperial", 2, Money.FromDecimal(1.80m), 0.23m),
        ]);

        var result = await provider.IssueSimplifiedInvoiceAsync(request, CancellationToken.None);

        result.Value.GrossTotal.ShouldBe(Money.FromDecimal(22.60m));
        (result.Value.NetTotal + result.Value.VatTotal).ShouldBe(result.Value.GrossTotal);
    }

    [Fact]
    public async Task Refuses_to_issue_a_document_with_no_lines()
    {
        var provider = new MockFiscalProvider(new FixedClock(FixedNow));
        var request = new FiscalDocumentRequest(Guid.NewGuid(), Guid.NewGuid(), []);

        var result = await provider.IssueSimplifiedInvoiceAsync(request, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("fiscal.no_lines");
    }

    private static FiscalDocumentRequest SingleLineRequest(Guid? tenantId = null) => new(
        tenantId ?? Guid.NewGuid(),
        Guid.NewGuid(),
        [new FiscalDocumentLine("Sopa do Dia", 1, Money.FromDecimal(3.50m), 0.13m)]);
}
