using System.Globalization;
using System.Text;

namespace Brasa.Api;

/// <summary>
/// Opaque cursor pagination (API-09, <c>docs/architecture/api-contract.md</c>
/// §9: "Cursor pagination on every collection. No unbounded lists, ever.").
/// Encodes a single sortable bookmark as a base64 token a client must treat
/// as a black box — echo it back, never construct or parse it.
/// </summary>
/// <remarks>
/// Not a raw timestamp on the wire on purpose: encoding it keeps the token
/// opaque today and leaves room to encode something else (a composite key,
/// a different sort) later without changing the contract a client sees.
/// </remarks>
public static class CursorPagination
{
    /// <summary>Encodes a bookmark value into an opaque cursor token.</summary>
    public static string Encode(DateTimeOffset bookmark)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(bookmark.ToString("O", CultureInfo.InvariantCulture)));

    /// <summary>Decodes a cursor token produced by <see cref="Encode"/>. Never throws — a malformed token is a 400, not a fault.</summary>
    public static bool TryDecode(string? cursor, out DateTimeOffset bookmark)
    {
        bookmark = default;
        if (string.IsNullOrEmpty(cursor))
        {
            return false;
        }

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out bookmark);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
