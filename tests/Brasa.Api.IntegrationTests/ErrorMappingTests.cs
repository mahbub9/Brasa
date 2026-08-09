using Brasa.Shared.Primitives;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Brasa.Api.IntegrationTests;

/// <summary>
/// <c>ErrorMapping.ToProblem()</c> is, by its own doc comment, "the only
/// place <see cref="ErrorType"/> is translated to an HTTP status" — every
/// endpoint in the API goes through it. Despite that, nothing pinned the
/// full mapping table directly; each endpoint's tests only ever assert the
/// one status they happen to trigger. This is a plain <see cref="IResult"/>
/// built with no HTTP context at all, so it runs as fast as a unit test.
/// </summary>
public sealed class ErrorMappingTests
{
    [Theory]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorType.Failure, StatusCodes.Status500InternalServerError)]
    public void Every_ErrorType_maps_to_its_documented_HTTP_status(ErrorType type, int expectedStatus)
    {
        var error = new Error("some.code", "Some description.", type);

        var problem = error.ToProblem().ShouldBeOfType<ProblemHttpResult>();

        problem.StatusCode.ShouldBe(expectedStatus);
    }

    [Fact]
    public void The_error_code_travels_as_a_public_extension_named_code()
    {
        // Error.Code is a public contract (api-contract.md) — mobile clients
        // branch on the "code" extension by exactly this name.
        var error = Error.NotFound("order.not_found", "Order not found.");

        var problem = error.ToProblem().ShouldBeOfType<ProblemHttpResult>();

        problem.ProblemDetails.Extensions["code"].ShouldBe("order.not_found");
    }

    [Fact]
    public void The_description_becomes_the_problem_details_title()
    {
        var error = Error.Conflict("order.already_closed", "Order is already closed.");

        var problem = error.ToProblem().ShouldBeOfType<ProblemHttpResult>();

        problem.ProblemDetails.Title.ShouldBe("Order is already closed.");
    }
}
