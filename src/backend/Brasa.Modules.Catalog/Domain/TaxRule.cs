using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;
using Brasa.Shared.Time;

namespace Brasa.Modules.Catalog.Domain;

/// <summary>
/// The VAT rate that applies to a category of sale — alcoholic or not,
/// dine-in or takeaway, in a given <see cref="PortugueseRegion"/> — during
/// an effective date range (CAT-07/08).
/// </summary>
/// <remarks>
/// <para>
/// Portuguese VAT law taxes categories of goods and the channel they're
/// sold through, never an individual menu item by name — the same reason
/// CAT-10's combo decomposition allocates by item, and CAT-09's
/// <c>MenuItem.IsAlcoholic</c> flag exists at all. <see cref="IsAlcoholic"/>
/// is therefore the whole "item" dimension this row's own title names, not
/// a per-<c>MenuItemId</c> key. Delivery is out of scope — the same gap
/// CAT-06's channel pricing already named, since no delivery order path
/// exists in this codebase at all yet.
/// </para>
/// <para>
/// Rates are data, never a hardcoded constant — see <see cref="VatRate"/>'s
/// own remarks and <c>docs/fiscal/README.md</c>: current rates are
/// unconfirmed by an accountant. This type is what lets a corrected or
/// changed rate be entered as a new, effective-dated row rather than an
/// edit that would silently rewrite what rate applied to a sale already
/// made — the same "never mutate, only add" instinct fiscal documents
/// themselves already follow, applied here even though a <c>TaxRule</c>
/// is not itself a fiscal document.
/// </para>
/// <para>
/// <b>Not yet wired into ordering.</b> <c>MenuItem.VatRate</c> stays the
/// live source `AddLine`/`AddComboLineAsync`/the fiscal document builder
/// all read from — swapping every one of those call sites to resolve
/// through a <c>TaxRule</c> lookup instead touches the most
/// fiscal-sensitive code in the whole system and deserves its own
/// dedicated, carefully-verified pass, not a side effect of shipping the
/// data model. This ships the model and a standalone resolution
/// endpoint, verified at the API level only — the same "mechanism before
/// the trigger" shape CAT-05/CAT-10/CAT-16/FLR-05 already established.
/// </para>
/// </remarks>
public sealed class TaxRule : Entity
{
    private TaxRule()
    {
        // EF Core materialisation.
    }

    /// <summary>Creates a new effective-dated rate for one (alcohol band, channel, region) combination.</summary>
    public TaxRule(
        bool isAlcoholic,
        bool isTakeaway,
        PortugueseRegion region,
        VatRate rate,
        DateTimeOffset effectiveFromUtc,
        DateTimeOffset? effectiveToUtc)
    {
        if (effectiveToUtc is not null && effectiveToUtc <= effectiveFromUtc)
        {
            throw new ArgumentException(
                "Effective-to must be after effective-from.", nameof(effectiveToUtc));
        }

        IsAlcoholic = isAlcoholic;
        IsTakeaway = isTakeaway;
        Region = region;
        Rate = rate;
        EffectiveFromUtc = effectiveFromUtc;
        EffectiveToUtc = effectiveToUtc;
    }

    /// <summary>Whether this rule prices the alcoholic band (23%, as of 2026) or the ordinary one.</summary>
    public bool IsAlcoholic { get; private set; }

    /// <summary>Whether this rule applies to takeaway sales rather than dine-in. Delivery is out of scope.</summary>
    public bool IsTakeaway { get; private set; }

    /// <summary>Which Portuguese region this rate applies in — mainland, Madeira or the Azores.</summary>
    public PortugueseRegion Region { get; private set; }

    /// <summary>The VAT rate this rule resolves to.</summary>
    public VatRate Rate { get; private set; }

    /// <summary>When this rule starts applying, inclusive.</summary>
    public DateTimeOffset EffectiveFromUtc { get; private set; }

    /// <summary>When this rule stops applying, exclusive. Null means it has no known end yet.</summary>
    public DateTimeOffset? EffectiveToUtc { get; private set; }

    /// <summary>
    /// Whether this rule is the one in force for the given combination at
    /// the given instant.
    /// </summary>
    public bool AppliesAt(bool isAlcoholic, bool isTakeaway, PortugueseRegion region, DateTimeOffset atUtc)
        => IsAlcoholic == isAlcoholic
        && IsTakeaway == isTakeaway
        && Region == region
        && atUtc >= EffectiveFromUtc
        && (EffectiveToUtc is null || atUtc < EffectiveToUtc.Value);

    /// <summary>
    /// Picks the rule in force for a combination at an instant, out of
    /// every rule on file. Two rules should never both apply to the same
    /// combination at the same instant — that would be a data-entry
    /// mistake, an overlapping effective range — but if it happens anyway,
    /// the most recently *started* rule wins, the same "last entry
    /// supersedes" resolution a correction would rely on.
    /// </summary>
    public static TaxRule? Resolve(
        IEnumerable<TaxRule> rules, bool isAlcoholic, bool isTakeaway, PortugueseRegion region, DateTimeOffset atUtc)
        => rules
            .Where(r => r.AppliesAt(isAlcoholic, isTakeaway, region, atUtc))
            .OrderByDescending(r => r.EffectiveFromUtc)
            .FirstOrDefault();
}
