using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SquadCrm.BuildingBlocks.Validation;

/// <summary>
/// Minimal validation extension point: runs the built-in
/// <see cref="Validator"/> (System.ComponentModel.DataAnnotations) over the
/// endpoint argument of type <typeparamref name="TArgument"/> and converts any
/// failures into <see cref="HttpValidationProblemDetails"/> — the RFC 9457
/// <c>errors</c> dictionary — so validation failures share the exact shape of
/// every other error response.
/// <para>
/// <b>Deliberately deferred:</b> CRM-105 establishes the <i>shape</i> of validation
/// only. The long-term business-validation strategy — where rules live, how they
/// compose, whether an abstraction layer is warranted — is an open decision for the
/// first stories that introduce real requests and endpoints. This foundation adds no
/// validation library and invents no business rules.
/// </para>
/// </summary>
/// <typeparam name="TArgument">The endpoint argument type to validate.</typeparam>
public sealed class ValidationEndpointFilter<TArgument> : IEndpointFilter
    where TArgument : notnull
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        TArgument? argument = context.Arguments.OfType<TArgument>().FirstOrDefault();

        if (argument is not null && !TryValidate(argument, out Dictionary<string, string[]> errors))
        {
            return TypedResults.ValidationProblem(errors);
        }

        return await next(context).ConfigureAwait(false);
    }

    private static bool TryValidate(TArgument argument, out Dictionary<string, string[]> errors)
    {
        var results = new List<ValidationResult>();
        var validationContext = new ValidationContext(argument);

        if (Validator.TryValidateObject(argument, validationContext, results, validateAllProperties: true))
        {
            errors = [];
            return true;
        }

        errors = results
            .SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty),
                (result, memberName) => (memberName, message: result.ErrorMessage ?? "Invalid value."))
            .GroupBy(entry => entry.memberName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.message).ToArray(),
                StringComparer.Ordinal);

        return false;
    }
}
