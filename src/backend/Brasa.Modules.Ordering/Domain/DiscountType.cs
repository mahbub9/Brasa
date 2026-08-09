namespace Brasa.Modules.Ordering.Domain;

/// <summary>How a discount (ORD-11) reduces the amount it's applied to.</summary>
public enum DiscountType
{
    /// <summary>A fraction of the pre-discount total, e.g. 10 for 10% off.</summary>
    Percentage = 0,

    /// <summary>A flat amount in major units (euros), e.g. 5.00 for €5.00 off.</summary>
    FixedAmount = 1,
}
