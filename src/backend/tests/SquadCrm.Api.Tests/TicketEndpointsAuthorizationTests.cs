using System.Net;
using System.Net.Http.Json;

namespace SquadCrm.Api.Tests;

/// <summary>
/// Every ticket route requires an explicit permission policy while preserving
/// the authentication boundary's 401 response for anonymous callers
/// (CRM-133 create, CRM-134 list, CRM-135 detail).
/// </summary>
public sealed class TicketEndpointsAuthorizationTests
{
    [Fact]
    public async Task Create_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/tickets",
            new
            {
                customerId = Guid.NewGuid(),
                subject = "Cannot log in",
                description = "The customer cannot log in to the portal.",
                categoryId = Guid.NewGuid(),
                subcategoryId = (Guid?)null,
                priorityId = Guid.NewGuid(),
                departmentId = Guid.NewGuid(),
                branchId = Guid.NewGuid(),
                channel = "Agent",
                assignedAgentId = (Guid?)null,
            },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/tickets", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/tickets/{Guid.NewGuid()}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
