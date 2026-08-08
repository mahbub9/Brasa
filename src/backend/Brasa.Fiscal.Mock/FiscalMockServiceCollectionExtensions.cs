using Brasa.Modules.Fiscal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Brasa.Fiscal.Mock;

/// <summary>Registration for <see cref="MockFiscalProvider"/>.</summary>
public static class FiscalMockServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="MockFiscalProvider"/> as <see cref="IFiscalProvider"/>.
    /// </summary>
    /// <remarks>
    /// FIS-03: the production guard. <paramref name="environment"/> is required,
    /// not optional, precisely so a call site cannot skip the check by omitting
    /// an argument. A mock fiscal document reaching a real restaurant's guest is
    /// a certification failure, not a bug — see <c>docs/fiscal/README.md</c>.
    /// </remarks>
    /// <exception cref="InvalidOperationException"><paramref name="environment"/> reports Production.</exception>
    public static IServiceCollection AddMockFiscalProvider(this IServiceCollection services, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsProduction())
        {
            throw new InvalidOperationException(
                "MockFiscalProvider must never be registered in Production. " +
                "It produces structurally valid but fiscally meaningless documents. " +
                "See docs/architecture/decisions/0002-own-fiscal-engine.md.");
        }

        services.AddSingleton<IFiscalProvider, MockFiscalProvider>();
        return services;
    }
}
