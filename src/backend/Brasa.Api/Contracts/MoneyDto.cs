using Brasa.Shared.Primitives;

namespace Brasa.Api.Contracts;

/// <summary>
/// Wire representation of <see cref="Money"/> — a decimal amount for clients to
/// display, alongside the ISO 4217 code.
/// </summary>
/// <remarks>
/// The domain never computes in decimal; this DTO exists only at the JSON
/// boundary. See <c>docs/architecture/money.md</c>.
/// </remarks>
public sealed record MoneyDto(decimal Amount, string Currency);

/// <summary>Maps domain <see cref="Money"/> to its wire form.</summary>
public static class MoneyDtoExtensions
{
    /// <summary>Converts to the wire representation.</summary>
    public static MoneyDto ToDto(this Money money) => new(money.ToDecimal(), money.Currency.AlphabeticCode());
}
