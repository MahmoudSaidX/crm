using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using SquadCrm.BuildingBlocks.Validation;

namespace SquadCrm.Api.Tests;

/// <summary>
/// The validation foundation proves <i>shape</i> only: a failure is expressed as
/// <see cref="HttpValidationProblemDetails"/> — the RFC 9457 <c>errors</c> dictionary —
/// rather than a bespoke envelope.
/// <para>
/// The annotated type below is declared <b>in the test project</b>. CRM-105
/// deliberately invents no business validation rules, so there is no production
/// request model to validate yet.
/// </para>
/// </summary>
public sealed class ValidationFoundationTests
{
    private sealed class TestOnlyRequest
    {
        [Required]
        public string? Value { get; init; }
    }

    [Fact]
    public async Task Filter_OnInvalidArgument_ProducesValidationProblemDetails()
    {
        var filter = new ValidationEndpointFilter<TestOnlyRequest>();
        var context = new DefaultEndpointFilterInvocationContext(
            new DefaultHttpContext(),
            new TestOnlyRequest { Value = null });

        object? result = await filter.InvokeAsync(context, static _ => ValueTask.FromResult<object?>(Results.Ok()));

        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.True(problem.ProblemDetails.Errors.ContainsKey(nameof(TestOnlyRequest.Value)));
    }

    [Fact]
    public async Task Filter_OnValidArgument_InvokesTheEndpoint()
    {
        var filter = new ValidationEndpointFilter<TestOnlyRequest>();
        var context = new DefaultEndpointFilterInvocationContext(
            new DefaultHttpContext(),
            new TestOnlyRequest { Value = "present" });

        bool invoked = false;

        await filter.InvokeAsync(context, _ =>
        {
            invoked = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        });

        Assert.True(invoked);
    }
}
