using Brasa.Shared.Primitives;

namespace Brasa.Shared.Tests.Primitives;

/// <summary>
/// <see cref="Error"/> is how hard rule 5 (CLAUDE.md — "expected failures
/// return <c>Result</c>, not exceptions") actually carries a reason. Every
/// factory here has exactly one job: attach the right <see cref="ErrorType"/>
/// so <c>ErrorMapping.ToProblem()</c> (the only place that type becomes an
/// HTTP status) picks the right one.
/// </summary>
public sealed class ErrorTests
{
    [Fact]
    public void None_represents_the_absence_of_an_error()
    {
        Error.None.IsNone.ShouldBeTrue();
        Error.None.Code.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("order.not_open", "Order is not open.")]
    [InlineData("catalog.item_not_found", "Item not found.")]
    public void Validation_carries_the_given_code_and_description_and_is_not_none(string code, string description)
    {
        var error = Error.Validation(code, description);

        error.Code.ShouldBe(code);
        error.Description.ShouldBe(description);
        error.Type.ShouldBe(ErrorType.Validation);
        error.IsNone.ShouldBeFalse();
    }

    [Fact]
    public void NotFound_sets_the_NotFound_type()
    {
        Error.NotFound("order.not_found", "Order not found.").Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public void Conflict_sets_the_Conflict_type()
    {
        Error.Conflict("floor.table_not_free", "Table is not free.").Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public void Forbidden_sets_the_Forbidden_type()
    {
        Error.Forbidden("auth.not_permitted", "Not permitted.").Type.ShouldBe(ErrorType.Forbidden);
    }

    [Fact]
    public void Failure_sets_the_Failure_type()
    {
        Error.Failure("system.unexpected", "Something went wrong.").Type.ShouldBe(ErrorType.Failure);
    }

    [Fact]
    public void Two_errors_with_the_same_values_are_equal_since_it_is_a_record_struct()
    {
        var a = Error.Validation("order.not_open", "Order is not open.");
        var b = Error.Validation("order.not_open", "Order is not open.");

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
    }
}
