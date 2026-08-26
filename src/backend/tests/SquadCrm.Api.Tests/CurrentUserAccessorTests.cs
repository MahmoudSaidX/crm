using Microsoft.Extensions.DependencyInjection;
using SquadCrm.BuildingBlocks.Security;

namespace SquadCrm.Api.Tests;

/// <summary>
/// Proves the D3 fail-closed behavior: no default <see cref="ICurrentUserAccessor"/>
/// implementation is registered, so resolving it before CRM-110 registers a real one
/// fails DI resolution loudly instead of returning a plausible anonymous answer.
/// </summary>
public sealed class CurrentUserAccessorTests
{
    [Fact]
    public void ResolvingCurrentUserAccessor_BeforeARealImplementationIsRegistered_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddSquadCrmAuthorizationExtensionPoint();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<ICurrentUserAccessor>());
    }
}
