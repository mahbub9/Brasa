using Brasa.Modules.Catalog.Domain;
using Brasa.Modules.Catalog.Persistence;
using Brasa.Shared.Primitives;
using Brasa.Shared.Tenancy;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Api.Seed;

/// <summary>
/// Seeds a demo menu for <see cref="DevTenant"/> on startup.
/// </summary>
/// <remarks>
/// Stands in for CAT's back-office menu editor (I1) and for real tenant
/// onboarding (IDN-13). It is guarded the same way as
/// <c>DevTenantMiddleware</c> and <c>MockFiscalProvider</c> — never in
/// Production — because seeding on every startup of a real deployment would be
/// actively harmful, not merely pointless.
/// </remarks>
public static class DevCatalogSeeder
{
    /// <summary>Seeds the demo menu if <see cref="DevTenant"/> has none yet. Idempotent.</summary>
    /// <exception cref="InvalidOperationException"><paramref name="environment"/> reports Production.</exception>
    public static async Task SeedAsync(IServiceProvider services, IHostEnvironment environment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsProduction())
        {
            throw new InvalidOperationException("DevCatalogSeeder must never run in Production.");
        }

        await using var scope = services.CreateAsyncScope();

        // Seeding happens outside any HTTP request, so there is no middleware to
        // resolve the tenant. Resolve it here, in this scope, before touching the
        // DbContext — RLS and tenant assignment both depend on it being set first.
        var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantContext.Resolve(DevTenant.Id);

        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        // Tax rules are seeded independently of the menu below — this
        // codebase's dev database is long-lived, not reset per run (QA-02),
        // so by the time CAT-07/08 shipped, every dev database already had
        // categories from an earlier session and would otherwise skip this
        // entirely via the early return just below.
        await SeedTaxRulesAsync(db, cancellationToken).ConfigureAwait(false);

        if (await db.Categories.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var entradas = new MenuCategory("Entradas", 1);
        var principais = new MenuCategory("Pratos Principais", 2);
        var bebidas = new MenuCategory("Bebidas", 3);
        var sobremesas = new MenuCategory("Sobremesas", 4);

        db.Categories.AddRange(entradas, principais, bebidas, sobremesas);

        var frango = new MenuItem(principais.Id, "Frango na Brasa", Money.FromDecimal(9.50m), VatRate.IntermediateMainland);
        var agua = new MenuItem(bebidas.Id, "Água", Money.FromDecimal(1.50m), VatRate.IntermediateMainland);

        // CAT-02: description and declared allergens on a few items — enough
        // to prove both render, not a claim that every item has been
        // reviewed. Most seeded items deliberately carry neither, the same
        // way most don't carry modifier groups; leaving them blank is
        // correct seed data, not a gap to fill in for its own sake.
        frango.SetDescription("Meio frango grelhado em brasa de carvão, tempero da casa.");
        var bacalhauBras = new MenuItem(principais.Id, "Bacalhau à Brás", Money.FromDecimal(11.00m), VatRate.IntermediateMainland);
        bacalhauBras.SetDescription("Bacalhau desfiado com batata palha, ovo e azeitonas.");
        bacalhauBras.SetAllergens([Allergen.Fish, Allergen.Eggs]);
        var pastelDeNata = new MenuItem(sobremesas.Id, "Pastel de Nata", Money.FromDecimal(1.50m), VatRate.IntermediateMainland);
        pastelDeNata.SetAllergens([Allergen.Gluten, Allergen.Eggs, Allergen.Milk]);

        // CAT-03/CAT-04: two items carry modifier groups, enough to prove a
        // required single-select group and an optional multi-select group
        // both work end to end. The rest stay plain — not every item needs
        // modifiers, and forcing them on would just be seed-data noise.
        var tamanho = frango.AddModifierGroup("Tamanho", isRequired: true, minSelect: 1, maxSelect: 1, displayOrder: 1);
        tamanho.AddModifier("Dose normal", Money.Zero, displayOrder: 1);
        tamanho.AddModifier("Meia dose", Money.FromDecimal(-2.50m), displayOrder: 2);

        var extras = frango.AddModifierGroup("Extras", isRequired: false, minSelect: 0, maxSelect: 3, displayOrder: 2);
        extras.AddModifier("Extra queijo", Money.FromDecimal(1.00m), displayOrder: 1);
        extras.AddModifier("Batata doce em vez de arroz", Money.FromDecimal(1.50m), displayOrder: 2);
        extras.AddModifier("Sem piri-piri", Money.Zero, displayOrder: 3);

        var tipoAgua = agua.AddModifierGroup("Tipo", isRequired: true, minSelect: 1, maxSelect: 1, displayOrder: 1);
        tipoAgua.AddModifier("Com gás", Money.Zero, displayOrder: 1);
        tipoAgua.AddModifier("Sem gás", Money.Zero, displayOrder: 2);

        db.Items.AddRange(
            new MenuItem(entradas.Id, "Pão e Azeitonas", Money.FromDecimal(2.50m), VatRate.IntermediateMainland),
            new MenuItem(entradas.Id, "Sopa do Dia", Money.FromDecimal(3.50m), VatRate.IntermediateMainland),
            frango,
            bacalhauBras,
            new MenuItem(principais.Id, "Bife à Portuguesa", Money.FromDecimal(12.50m), VatRate.IntermediateMainland),
            agua,
            // Alcoholic drinks sit in the 23% band and must be itemised
            // separately from food on the invoice — see docs/fiscal/README.md.
            new MenuItem(bebidas.Id, "Vinho da Casa (copo)", Money.FromDecimal(3.00m), VatRate.StandardMainland, isAlcoholic: true),
            new MenuItem(bebidas.Id, "Imperial", Money.FromDecimal(1.80m), VatRate.StandardMainland, isAlcoholic: true),
            pastelDeNata,
            new MenuItem(sobremesas.Id, "Arroz Doce", Money.FromDecimal(3.00m), VatRate.IntermediateMainland));

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Seeds mainland-only dine-in/takeaway tax rules for both bands
    /// (CAT-07/08) — the same two rates <see cref="VatRate"/> already names
    /// as unconfirmed by an accountant, now as effective-dated rows rather
    /// than the flat <c>MenuItem.VatRate</c> constant. Madeira/Azores rows
    /// are deliberately not seeded — no seeded site claims either region
    /// yet, and inventing rates nobody asked for would be worse than
    /// leaving the gap honest. Effective from a fixed anchor in the past,
    /// not "now," so resolving "today" always finds a rule regardless of
    /// which day the seeder happens to run.
    /// </summary>
    private static async Task SeedTaxRulesAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        if (await db.TaxRules.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var effectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        db.TaxRules.AddRange(
            new TaxRule(isAlcoholic: false, isTakeaway: false, PortugueseRegion.Continental, VatRate.IntermediateMainland, effectiveFrom, null),
            new TaxRule(isAlcoholic: false, isTakeaway: true, PortugueseRegion.Continental, VatRate.IntermediateMainland, effectiveFrom, null),
            new TaxRule(isAlcoholic: true, isTakeaway: false, PortugueseRegion.Continental, VatRate.StandardMainland, effectiveFrom, null),
            new TaxRule(isAlcoholic: true, isTakeaway: true, PortugueseRegion.Continental, VatRate.StandardMainland, effectiveFrom, null));

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
