using Brasa.Api.ClientVersioning;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Brasa.Api.IntegrationTests;

/// <summary>
/// API-08 — RFC 8594 <c>Deprecation</c>/<c>Sunset</c> headers
/// (<c>docs/architecture/api-contract.md</c> §3). Runs the middleware
/// directly against a <see cref="DefaultHttpContext"/>, no real HTTP
/// pipeline needed — same shape as <c>ErrorMappingTests</c>.
/// </summary>
public sealed class ApiDeprecationMiddlewareTests
{
    private static async Task<HttpContext> InvokeAsync(ApiDeprecationOptions options)
    {
        var context = new DefaultHttpContext();
        var middleware = new ApiDeprecationMiddleware(_ => Task.CompletedTask, Options.Create(options));
        await middleware.InvokeAsync(context);
        return context;
    }

    [Fact]
    public async Task Nothing_configured_adds_no_headers()
    {
        var context = await InvokeAsync(new ApiDeprecationOptions());

        context.Response.Headers.ContainsKey("Deprecation").ShouldBeFalse();
        context.Response.Headers.ContainsKey("Sunset").ShouldBeFalse();
        context.Response.Headers.ContainsKey("Link").ShouldBeFalse();
    }

    [Fact]
    public async Task DeprecatedAt_is_sent_as_an_RFC_7231_IMF_fixdate()
    {
        var context = await InvokeAsync(new ApiDeprecationOptions
        {
            DeprecatedAt = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
        });

        context.Response.Headers["Deprecation"].ToString().ShouldBe("Fri, 01 Jan 2027 00:00:00 GMT");
    }

    [Fact]
    public async Task SunsetAt_is_sent_as_an_RFC_7231_IMF_fixdate()
    {
        var context = await InvokeAsync(new ApiDeprecationOptions
        {
            SunsetAt = new DateTimeOffset(2027, 6, 1, 12, 30, 0, TimeSpan.Zero),
        });

        context.Response.Headers["Sunset"].ToString().ShouldBe("Tue, 01 Jun 2027 12:30:00 GMT");
    }

    [Fact]
    public async Task A_non_UTC_offset_is_converted_before_formatting()
    {
        // Lisbon in July is UTC+1 (WEST) — the header must carry the
        // equivalent UTC instant, not the local wall-clock time with a "GMT"
        // label slapped on it.
        var context = await InvokeAsync(new ApiDeprecationOptions
        {
            SunsetAt = new DateTimeOffset(2027, 7, 1, 13, 0, 0, TimeSpan.FromHours(1)),
        });

        context.Response.Headers["Sunset"].ToString().ShouldBe("Thu, 01 Jul 2027 12:00:00 GMT");
    }

    [Fact]
    public async Task Link_is_sent_with_rel_sunset_per_RFC_8594()
    {
        var context = await InvokeAsync(new ApiDeprecationOptions
        {
            Link = "https://example.com/docs/api-v2-migration",
        });

        context.Response.Headers["Link"].ToString().ShouldBe("<https://example.com/docs/api-v2-migration>; rel=\"sunset\"");
    }

    [Fact]
    public async Task All_three_can_be_configured_together()
    {
        var context = await InvokeAsync(new ApiDeprecationOptions
        {
            DeprecatedAt = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
            SunsetAt = new DateTimeOffset(2027, 6, 1, 0, 0, 0, TimeSpan.Zero),
            Link = "https://example.com/docs/api-v2-migration",
        });

        context.Response.Headers.ContainsKey("Deprecation").ShouldBeTrue();
        context.Response.Headers.ContainsKey("Sunset").ShouldBeTrue();
        context.Response.Headers.ContainsKey("Link").ShouldBeTrue();
    }
}
