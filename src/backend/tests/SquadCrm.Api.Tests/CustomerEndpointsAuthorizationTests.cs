using System.Net;
using System.Net.Http.Json;

namespace SquadCrm.Api.Tests;

/// <summary>
/// Customer routes require an explicit manage permission policy while
/// preserving the authentication boundary's 401 response for anonymous callers.
/// </summary>
public sealed class CustomerEndpointsAuthorizationTests
{
    [Fact]
    public async Task Create_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/customers",
            new { firstName = "Sara", lastName = "Ahmed", preferredLanguage = (string?)null, departmentId = (Guid?)null, branchId = (Guid?)null },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
