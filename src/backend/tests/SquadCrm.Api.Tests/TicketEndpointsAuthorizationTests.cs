using System.Net;
using System.Net.Http.Json;

namespace SquadCrm.Api.Tests;

/// <summary>
/// Every ticket route requires an explicit permission policy while preserving
/// the authentication boundary's 401 response for anonymous callers
/// (CRM-133 create, CRM-134 list, CRM-135 detail, CRM-136 assign, CRM-137
/// status, CRM-138 escalate, CRM-139 history, CRM-147 collaboration).
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

    [Fact]
    public async Task History_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/tickets/{Guid.NewGuid()}/history", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Assign_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/tickets/{Guid.NewGuid()}/assign",
            new
            {
                targetAgentId = Guid.NewGuid(),
                reason = (string?)null,
                version = 1,
            },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/tickets/{Guid.NewGuid()}/status",
            new
            {
                targetStatus = "InProgress",
                reason = (string?)null,
                version = 1,
            },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Escalate_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/tickets/{Guid.NewGuid()}/escalate",
            new
            {
                targetType = "Agent",
                targetId = Guid.NewGuid(),
                reason = "Needs a senior agent.",
                version = 1,
            },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddNote_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/tickets/{Guid.NewGuid()}/notes",
            new
            {
                body = "Checked the customer's account, escalating internally.",
                mentionedUserIds = Array.Empty<Guid>(),
            },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListNotes_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/tickets/{Guid.NewGuid()}/notes", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddWatcher_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/tickets/{Guid.NewGuid()}/watchers",
            new { userId = Guid.NewGuid() },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListWatchers_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/tickets/{Guid.NewGuid()}/watchers", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RemoveWatcher_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.DeleteAsync(
            $"/api/v1/tickets/{Guid.NewGuid()}/watchers/{Guid.NewGuid()}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
