using System.Net;
using System.Net.Http.Json;

namespace SquadCrm.Api.Tests;

/// <summary>
/// The ticket create route requires an explicit permission policy while
/// preserving the authentication boundary's 401 response for anonymous
/// callers (CRM-133, single route only — browse/view are CRM-134/135).
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
}
