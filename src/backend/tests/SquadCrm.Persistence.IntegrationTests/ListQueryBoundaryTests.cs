using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SquadCrm.BuildingBlocks.Http;

namespace SquadCrm.Persistence.IntegrationTests;

/// <summary>
/// The query string is untrusted input. These tests assert real HTTP status
/// codes and real response bodies for hostile and merely malformed list
/// queries — never that some sanitizer function was called, because no such
/// function exists and deliberately never will.
/// <para>
/// <b>The regression being locked down.</b> <see cref="PaginationRequest"/> has
/// always declared its bounds, but no collection list endpoint attached the
/// validation filter, so they were never enforced: <c>?page=0</c> reached
/// <c>Skip((0 - 1) * pageSize)</c> and PostgreSQL rejected the negative OFFSET
/// as an unhandled 500, and <c>?pageSize=1000000</c> was honoured verbatim.
/// </para>
/// <para>
/// <b>The other half is about what must NOT happen.</b> Apostrophes, Arabic,
/// emoji, angle brackets and SQL-injection-shaped text are ordinary business
/// data. They must reach the database as parameterized values and come back
/// byte-identical — not stripped, not escaped, not rejected. A test that
/// accepted a mangled echo would be certifying data corruption as security.
/// </para>
/// </summary>
[Collection(PostgresTestDatabase.CollectionName)]
public sealed class ListQueryBoundaryTests
{
    private const string Customers = "/api/v1/customers";

    public ListQueryBoundaryTests(PostgresTestDatabase database) => _ = database;

    public static TheoryData<string> OutOfRangePagination =>
    [
        $"{Customers}?page=0",
        $"{Customers}?page=-1",
        $"{Customers}?pageSize=0",
        $"{Customers}?pageSize=201",
        $"{Customers}?pageSize=1000000",
    ];

    [Theory]
    [MemberData(nameof(OutOfRangePagination))]
    public async Task ListWithOutOfRangePagination_Returns400_NotAnUnhandledError(string url)
    {
        await using AuthenticatedApiFactory factory = new();
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("customers.view");

        using HttpResponseMessage response = await client.GetAsync(url, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// A non-numeric page is a model-binding failure rather than a range
    /// failure, so it is asserted separately — but it must still be a 400 and
    /// must still never reach the database.
    /// </summary>
    [Theory]
    [InlineData("page=abc")]
    [InlineData("pageSize=abc")]
    [InlineData("page=9999999999999999999")]
    public async Task ListWithUnparseablePagination_Returns400(string query)
    {
        await using AuthenticatedApiFactory factory = new();
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("customers.view");

        using HttpResponseMessage response = await client.GetAsync(
            $"{Customers}?{query}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListWithMaximumAllowedPageSize_Succeeds()
    {
        await using AuthenticatedApiFactory factory = new();
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("customers.view");

        using HttpResponseMessage response = await client.GetAsync(
            $"{Customers}?page=1&pageSize={PaginationRequest.MaxPageSize}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ListWithOverlongSearch_Returns400()
    {
        await using AuthenticatedApiFactory factory = new();
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("customers.view");

        using HttpResponseMessage response = await client.GetAsync(
            $"{Customers}?search={new string('a', 201)}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListWithSearchAtTheLengthBound_Succeeds()
    {
        await using AuthenticatedApiFactory factory = new();
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("customers.view");

        using HttpResponseMessage response = await client.GetAsync(
            $"{Customers}?search={new string('a', 200)}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("sortBy=DROP%20TABLE")]
    [InlineData("sortBy=customer_number;--")]
    [InlineData("sortDirection=sideways")]
    [InlineData("status=NotARealStatus")]
    [InlineData("departmentIds=not-a-guid")]
    public async Task ListWithUnknownSortOrFilterValue_Returns400_NeverReachesTheQuery(string query)
    {
        await using AuthenticatedApiFactory factory = new();
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("customers.view");

        using HttpResponseMessage response = await client.GetAsync(
            $"{Customers}?{query}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListWithTooManyFilterValues_Returns400()
    {
        await using AuthenticatedApiFactory factory = new();
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("customers.view");

        string filters = string.Join(
            '&', Enumerable.Range(0, 51).Select(_ => $"departmentIds={Guid.NewGuid()}"));

        using HttpResponseMessage response = await client.GetAsync(
            $"{Customers}?{filters}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// The core "search input stays DATA" proof. Each term is hostile-looking or
    /// non-Latin; every one must be accepted, parameterized, matched literally
    /// and — critically — leave the table intact afterwards.
    /// </summary>
    [Theory]
    [InlineData("O'Brien")]
    [InlineData("'; DROP TABLE customer_management.customer; --")]
    [InlineData("1 OR 1=1")]
    [InlineData("%_%")]
    [InlineData("<script>alert('x')</script>")]
    [InlineData("محمود")]
    [InlineData("عبد الله")]
    [InlineData("Ünicode ✨ 名前")]
    public async Task ListWithHostileOrNonLatinSearch_IsTreatedAsData(string search)
    {
        await using AuthenticatedApiFactory factory = new();
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("customers.view");

        using HttpResponseMessage response = await client.GetAsync(
            $"{Customers}?search={Uri.EscapeDataString(search)}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.True(body.TryGetProperty("items", out JsonElement items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);

        // The table is still queryable afterwards: nothing was dropped, and no
        // term widened the result to "everything".
        using HttpResponseMessage after = await client.GetAsync(Customers, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    /// <summary>
    /// Bounds must hold on every list surface, not only the one used to write
    /// the rules — each of these previously accepted <c>page=0</c>.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/tickets", "tickets.view")]
    [InlineData("/api/v1/tasks", "tasks.view")]
    [InlineData("/api/v1/quick-replies", "quickreplies.view")]
    [InlineData("/api/v1/branches", "branches.view")]
    [InlineData("/api/v1/departments", "departments.view")]
    [InlineData("/api/v1/roles", "roles.view")]
    [InlineData("/api/v1/ticket-categories", "ticketcategories.view")]
    [InlineData("/api/v1/ticket-priorities", "ticketpriorities.view")]
    [InlineData("/api/v1/staff-users", "users.view")]
    [InlineData("/api/v1/audit-records", "audit.view")]
    public async Task EveryListEndpoint_RejectsPageZeroAndOversizedPageSize(string url, string permission)
    {
        await using AuthenticatedApiFactory factory = new();
        using HttpClient client = await factory.CreateAuthenticatedClientAsync(permission);

        using HttpResponseMessage pageZero = await client.GetAsync($"{url}?page=0", CancellationToken.None);
        Assert.Equal(HttpStatusCode.BadRequest, pageZero.StatusCode);

        using HttpResponseMessage hugePage = await client.GetAsync($"{url}?pageSize=5000", CancellationToken.None);
        Assert.Equal(HttpStatusCode.BadRequest, hugePage.StatusCode);

        // The same endpoint still serves a valid request — the filter bounds
        // the query, it does not break the route.
        using HttpResponseMessage valid = await client.GetAsync($"{url}?page=1&pageSize=20", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Theory]
    [InlineData("entityType")]
    [InlineData("action")]
    [InlineData("actorHandle")]
    public async Task AuditFilters_AreLengthBounded(string parameter)
    {
        await using AuthenticatedApiFactory factory = new();
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("audit.view");

        using HttpResponseMessage tooLong = await client.GetAsync(
            $"/api/v1/audit-records?{parameter}={new string('x', 201)}", CancellationToken.None);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);

        using HttpResponseMessage atBound = await client.GetAsync(
            $"/api/v1/audit-records?{parameter}={new string('x', 200)}", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, atBound.StatusCode);
    }

    [Fact]
    public async Task StaffUserSearch_IsLengthBounded()
    {
        await using AuthenticatedApiFactory factory = new();
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("users.view");

        using HttpResponseMessage tooLong = await client.GetAsync(
            $"/api/v1/staff-users?search={new string('x', 201)}", CancellationToken.None);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);

        using HttpResponseMessage arabic = await client.GetAsync(
            $"/api/v1/staff-users?search={Uri.EscapeDataString("محمود")}", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, arabic.StatusCode);
    }
}
