using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace SquadCrm.BuildingBlocks.Validation;

/// <summary>
/// Thin opt-in registration so a module can attach
/// <see cref="ValidationEndpointFilter{TArgument}"/> to one of its endpoints.
/// Nothing is applied globally: endpoints opt in explicitly.
/// </summary>
public static class ValidationEndpointFilterExtensions
{
    /// <summary>Validates the endpoint argument of type <typeparamref name="TArgument"/>.</summary>
    public static RouteHandlerBuilder ValidatesDataAnnotations<TArgument>(this RouteHandlerBuilder builder)
        where TArgument : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddEndpointFilter(new ValidationEndpointFilter<TArgument>());
        return builder;
    }
}
