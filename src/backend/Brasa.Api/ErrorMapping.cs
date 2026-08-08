using Brasa.Shared.Primitives;

namespace Brasa.Api;

/// <summary>
/// Turns a domain <see cref="Error"/> into an RFC 9457 ProblemDetails response.
/// </summary>
/// <remarks>
/// This is the only place <see cref="ErrorType"/> is translated to an HTTP
/// status. Domain and module code never reference HTTP at all — see
/// <c>docs/architecture/module-boundaries.md</c>.
/// </remarks>
public static class ErrorMapping
{
    /// <summary>
    /// Maps an <see cref="Error"/> to a ProblemDetails result carrying a stable
    /// <c>code</c> extension.
    /// </summary>
    /// <remarks>
    /// <see cref="Error.Code"/> is a public contract per
    /// <c>docs/architecture/api-contract.md</c> — mobile clients branch on it,
    /// and its meaning must never change once released.
    /// </remarks>
    public static IResult ToProblem(this Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Failure => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Problem(
            title: error.Description,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
