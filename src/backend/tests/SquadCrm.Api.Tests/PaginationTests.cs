using System.Net;
using System.Text.Json;

namespace SquadCrm.Api.Tests;

/// <summary>
/// Proves the shared <c>PagedResult&lt;T&gt;</c> envelope and
/// <c>ValidationEndpointFilter&lt;T&gt;</c> end to end through the fixture module's
/// paged demo endpoint (CRM-204).
/// </summary>
public sealed class PaginationTests
{
    private const string Route = "/api/v1/internal/architecture-fixture/module-info-page";

    [Fact]
    public async Task ModuleInfoPage_ReturnsPagedResult()
    {
        using var factory = new SquadCrmApiFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri(Route, UriKind.Relative));
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;

        Assert.Equal(1, root.GetProperty("items").GetArrayLength());
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(20, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ModuleInfoPage_WithOutOfRangeParameters_ProducesValidationProblemWithBothFields()
    {
        using var factory = new SquadCrmApiFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri($"{Route}?page=0&pageSize=500", UriKind.Relative));
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement errors = document.RootElement.GetProperty("errors");

        Assert.True(errors.TryGetProperty("Page", out _), body);
        Assert.True(errors.TryGetProperty("PageSize", out _), body);
    }
}
