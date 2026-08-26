using Microsoft.Extensions.DependencyInjection;

namespace SquadCrm.BuildingBlocks.Security;

/// <summary>
/// Registers the authorization extension point CRM-110 completes. Adds the
/// framework's authorization services with zero policies. Registers no
/// <see cref="ICurrentUserAccessor"/> implementation — CRM-110 is the first
/// story that registers one — and no authentication scheme, and maps no
/// <c>[Authorize]</c> endpoint. Any code that resolves
/// <see cref="ICurrentUserAccessor"/> before CRM-110 lands will fail DI
/// resolution loudly; that is intentional (fail-closed, see the interface's
/// doc comment).
/// </summary>
public static class SquadCrmAuthorizationExtensions
{
    public static IServiceCollection AddSquadCrmAuthorizationExtensionPoint(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthorization();

        return services;
    }
}
