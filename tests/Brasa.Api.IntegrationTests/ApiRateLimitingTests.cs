using Brasa.Api.ClientVersioning;
using Brasa.Api.RateLimiting;
using Brasa.Shared.Tenancy;
using Microsoft.AspNetCore.Http;

namespace Brasa.Api.IntegrationTests;

/// <summary>
/// API-12 — partition-key resolution for the global rate limiter. Pure
/// logic, no HTTP pipeline or DI container needed — same shape as
/// <c>ApiDeprecationMiddlewareTests</c>.
/// </summary>
public sealed class ApiRateLimitingTests
{
    private static TenantContext ResolvedTenant()
    {
        var tenant = new TenantContext();
        tenant.Resolve(Guid.NewGuid());
        return tenant;
    }

    [Fact]
    public void Requests_outside_api_are_unmetered()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/health";

        ApiRateLimiting.ResolvePartitionKey(context, ResolvedTenant())
            .ShouldBe(ApiRateLimiting.UnmeteredPartitionKey);
    }

    [Fact]
    public void A_request_with_no_client_info_is_grouped_under_the_unknown_client_id()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/menu";
        var tenant = ResolvedTenant();

        var key = ApiRateLimiting.ResolvePartitionKey(context, tenant);

        key.ShouldBe($"{tenant.TenantId:N}:{ApiRateLimiting.UnknownClientId}");
    }

    [Fact]
    public void A_request_with_client_info_is_keyed_by_tenant_and_client_id()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/menu";
        context.Items[ClientVersionMiddleware.HttpContextItemKey] = new ClientInfo("pos-web", "1.2.3", "web");
        var tenant = ResolvedTenant();

        var key = ApiRateLimiting.ResolvePartitionKey(context, tenant);

        key.ShouldBe($"{tenant.TenantId:N}:pos-web");
    }

    [Fact]
    public void Two_tenants_calling_with_the_same_client_id_land_in_different_partitions()
    {
        var contextA = new DefaultHttpContext { Request = { Path = "/api/v1/menu" } };
        contextA.Items[ClientVersionMiddleware.HttpContextItemKey] = new ClientInfo("pos-web", "1.0.0", "web");
        var contextB = new DefaultHttpContext { Request = { Path = "/api/v1/menu" } };
        contextB.Items[ClientVersionMiddleware.HttpContextItemKey] = new ClientInfo("pos-web", "1.0.0", "web");

        var keyA = ApiRateLimiting.ResolvePartitionKey(contextA, ResolvedTenant());
        var keyB = ApiRateLimiting.ResolvePartitionKey(contextB, ResolvedTenant());

        keyA.ShouldNotBe(keyB);
    }
}
