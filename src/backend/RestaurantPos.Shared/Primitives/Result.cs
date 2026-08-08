namespace RestaurantPos.Shared.Primitives;

/// <summary>
/// The outcome of an operation that can fail for an expected, domain-level reason.
/// </summary>
/// <remarks>
/// <para>
/// Expected failures — "that table is already closed", "PIN incorrect" — are
/// returned as <see cref="Result"/>, not thrown. Exceptions are reserved for
/// genuine faults: a dropped database connection, a bug, a violated invariant.
/// </para>
/// <para>
/// This keeps the POS responsive: a waiter tapping the wrong button produces a
/// cheap value, not a stack unwind, and the API layer maps it to a clean
/// ProblemDetails response.
/// </para>
/// </remarks>
public readonly record struct Result
{
    private Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>True when the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>True when the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The reason for failure, or <see cref="Error.None"/> on success.</summary>
    public Error Error { get; }

    /// <summary>A successful outcome.</summary>
    public static Result Success() => new(true, Error.None);

    /// <summary>A failed outcome.</summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>A successful outcome carrying a value.</summary>
    public static Result<TValue> Success<TValue>(TValue value) => Result<TValue>.Success(value);

    /// <summary>A failed outcome for an operation that would have produced a value.</summary>
    public static Result<TValue> Failure<TValue>(Error error) => Result<TValue>.Failure(error);
}

/// <summary>
/// The outcome of an operation that returns <typeparamref name="TValue"/> on success.
/// </summary>
/// <typeparam name="TValue">The value produced when the operation succeeds.</typeparam>
public readonly record struct Result<TValue>
{
    private readonly TValue? _value;

    private Result(bool isSuccess, TValue? value, Error error)
    {
        IsSuccess = isSuccess;
        _value = value;
        Error = error;
    }

    /// <summary>True when the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>True when the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The reason for failure, or <see cref="Error.None"/> on success.</summary>
    public Error Error { get; }

    /// <summary>The produced value.</summary>
    /// <exception cref="InvalidOperationException">The result is a failure.</exception>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result is a failure ({Error.Code}); Value is not available.");

    /// <summary>A successful outcome carrying <paramref name="value"/>.</summary>
    public static Result<TValue> Success(TValue value) => new(true, value, Error.None);

    /// <summary>A failed outcome.</summary>
    public static Result<TValue> Failure(Error error) => new(false, default, error);

    /// <summary>Implicitly wraps a value as a successful result.</summary>
    public static implicit operator Result<TValue>(TValue value) => Success(value);

    /// <summary>Discards the value, keeping only success or failure.</summary>
    public Result ToResult() => IsSuccess ? Result.Success() : Result.Failure(Error);

    /// <summary>
    /// Collapses both branches into a single value, so callers handle failure
    /// explicitly rather than reaching for <see cref="Value"/>.
    /// </summary>
    public TOut Match<TOut>(Func<TValue, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess ? onSuccess(_value!) : onFailure(Error);
    }
}
