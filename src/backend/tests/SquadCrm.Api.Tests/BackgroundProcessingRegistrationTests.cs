using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SquadCrm.Modules.ArchitectureFixture.BackgroundProcessing;

namespace SquadCrm.Api.Tests;

public sealed class BackgroundProcessingRegistrationTests
{
    [Fact]
    public void Host_RegistersHangfireSchedulingAndScopedOutboxJob()
    {
        using SquadCrmApiFactory factory = new();

        Assert.NotNull(factory.Services.GetRequiredService<IBackgroundJobClient>());
        Assert.NotNull(factory.Services.GetRequiredService<IRecurringJobManager>());

        using IServiceScope scope = factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ArchitectureFixtureOutboxJob>());

        OutboxProcessingOptions options = scope.ServiceProvider
            .GetRequiredService<IOptions<OutboxProcessingOptions>>()
            .Value;
        Assert.True(options.BatchSize > 0);
        Assert.True(options.RetryCeiling > 0);
    }

    [Fact]
    public async Task Dashboard_IsAbsentOutsideDevelopment()
    {
        using SquadCrmApiFactory factory = new(Environments.Production);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/hangfire");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
