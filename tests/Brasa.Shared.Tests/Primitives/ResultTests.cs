using Brasa.Shared.Primitives;

namespace Brasa.Shared.Tests.Primitives;

/// <summary>
/// <see cref="Result"/>/<see cref="Result{TValue}"/> are how hard rule 5
/// (CLAUDE.md — "expected failures return <c>Result</c>, not exceptions")
/// is actually implemented. These pin the two failure modes a caller could
/// get wrong: reading <see cref="Result{TValue}.Value"/> without checking
/// <see cref="Result{TValue}.IsSuccess"/> first, and assuming
/// <see cref="Result{TValue}.Error"/> is meaningful on a success.
/// </summary>
public sealed class ResultTests
{
    [Fact]
    public void Success_is_successful_with_no_error()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Error.ShouldBe(Error.None);
    }

    [Fact]
    public void Failure_is_not_successful_and_carries_the_given_error()
    {
        var error = Error.Conflict("floor.table_not_free", "Table is not free.");

        var result = Result.Failure(error);

        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
    }

    [Fact]
    public void Generic_success_carries_the_value_and_no_error()
    {
        var result = Result.Success(42);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        result.Error.ShouldBe(Error.None);
    }

    [Fact]
    public void Generic_failure_has_no_value_available()
    {
        var error = Error.NotFound("order.not_found", "Order not found.");

        var result = Result.Failure<int>(error);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Accessing_Value_on_a_failure_names_the_error_code_in_the_exception_message()
    {
        var result = Result.Failure<int>(Error.Conflict("order.already_closed", "Already closed."));

        var exception = Should.Throw<InvalidOperationException>(() => result.Value);

        exception.Message.ShouldContain("order.already_closed");
    }

    [Fact]
    public void A_value_implicitly_converts_to_a_successful_result()
    {
        Result<string> result = "Frango na Brasa";

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Frango na Brasa");
    }

    [Fact]
    public void ToResult_discards_the_value_but_keeps_success()
    {
        var result = Result.Success(42).ToResult();

        result.IsSuccess.ShouldBeTrue();
        result.Error.ShouldBe(Error.None);
    }

    [Fact]
    public void ToResult_keeps_the_error_on_failure()
    {
        var error = Error.Validation("order.invalid_quantity", "Quantity must be positive.");

        var result = Result.Failure<int>(error).ToResult();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
    }

    [Fact]
    public void Match_invokes_the_success_branch_on_success()
    {
        var outcome = Result.Success(10).Match(
            onSuccess: value => $"got {value}",
            onFailure: error => $"failed: {error.Code}");

        outcome.ShouldBe("got 10");
    }

    [Fact]
    public void Match_invokes_the_failure_branch_on_failure()
    {
        var outcome = Result.Failure<int>(Error.NotFound("order.not_found", "Order not found.")).Match(
            onSuccess: value => $"got {value}",
            onFailure: error => $"failed: {error.Code}");

        outcome.ShouldBe("failed: order.not_found");
    }
}
