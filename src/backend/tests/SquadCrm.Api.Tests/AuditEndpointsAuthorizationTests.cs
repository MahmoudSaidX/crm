using System.Net;

namespace SquadCrm.Api.Tests;

/// <summary>
/// Audit routes require authentication before any permission is evaluated —
/// same pattern as <see cref="RoleEndpointsAuthorizationTests"/>. This suite
/// hosts the API in-memory with no database (see
/// <see cref="SquadCrmApiFactory"/>'s "Database configuration" note), so it
/// can only prove the anonymous-request boundary (401); the 403/200
/// permission-gated behaviour for an authenticated caller is proven against a
/// real database by <c>SquadCrm.Persistence.IntegrationTests</c>' audit and
/// permission-catalog tests.
/// </summary>
public sealed class AuditEndpointsAuthorizationTests
{
    [Fact]
    public async Task List_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/audit-records", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/audit-records/1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
