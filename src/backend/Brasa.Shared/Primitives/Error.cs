namespace Brasa.Shared.Primitives;

/// <summary>
/// Why an operation failed, in a form the API layer can turn into an RFC 9457
/// ProblemDetails response without the domain knowing anything about HTTP.
/// </summary>
/// <param name="Code">
/// A stable, machine-readable identifier such as <c>order.already_closed</c>.
/// Clients branch on this; it must not change once released.
/// </param>
/// <param name="Description">A human-readable explanation, in English, for developers and logs.</param>
/// <param name="Type">The category, which determines the HTTP status the API maps to.</param>
public readonly record struct Error(string Code, string Description, ErrorType Type)
{
    /// <summary>The absence of an error.</summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    /// <summary>Input failed validation — maps to HTTP 400.</summary>
    public static Error Validation(string code, string description)
        => new(code, description, ErrorType.Validation);

    /// <summary>The requested resource does not exist — maps to HTTP 404.</summary>
    public static Error NotFound(string code, string description)
        => new(code, description, ErrorType.NotFound);

    /// <summary>
    /// The request conflicts with current state, e.g. reopening a closed cash
    /// session — maps to HTTP 409.
    /// </summary>
    public static Error Conflict(string code, string description)
        => new(code, description, ErrorType.Conflict);

    /// <summary>The caller is authenticated but not permitted — maps to HTTP 403.</summary>
    public static Error Forbidden(string code, string description)
        => new(code, description, ErrorType.Forbidden);

    /// <summary>An unexpected failure — maps to HTTP 500.</summary>
    public static Error Failure(string code, string description)
        => new(code, description, ErrorType.Failure);

    /// <summary>The caller exceeded a rate limit — maps to HTTP 429.</summary>
    public static Error RateLimited(string code, string description)
        => new(code, description, ErrorType.RateLimited);

    /// <summary>True when this instance represents success.</summary>
    public bool IsNone => Code.Length == 0;
}

/// <summary>Categories of <see cref="Error"/>, used to pick an HTTP status code.</summary>
public enum ErrorType
{
    /// <summary>Unexpected failure. HTTP 500.</summary>
    Failure = 0,

    /// <summary>Invalid input. HTTP 400.</summary>
    Validation = 1,

    /// <summary>Resource does not exist. HTTP 404.</summary>
    NotFound = 2,

    /// <summary>Conflicts with current state. HTTP 409.</summary>
    Conflict = 3,

    /// <summary>Not permitted. HTTP 403.</summary>
    Forbidden = 4,

    /// <summary>Too many requests. HTTP 429.</summary>
    RateLimited = 5,
}
