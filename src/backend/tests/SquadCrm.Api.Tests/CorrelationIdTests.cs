using System.Diagnostics;
using System.Text.Json;

namespace SquadCrm.Api.Tests;

/// <summary>
/// A client-supplied correlation id is sanitised, never echoed verbatim. Also
/// proves the D2 distinctness of <c>correlationId</c> and <c>traceId</c>: they are
/// sourced consistently across response paths and legitimately DIVERGE once an
/// ambient <see cref="Activity"/> is active — they are never asserted to always match.
/// </summary>
public sealed class CorrelationIdTests
{
    private const string HeaderName = "X-Correlation-Id";
    private static readonly Uri HealthUri = new("/health", UriKind.Relative);
    private static readonly Uri ValidationFailingUri =
        new("/api/v1/internal/architecture-fixture/module-info-page?page=0", UriKind.Relative);

    [Fact]
    public async Task WellFormedCorrelationId_IsEchoed()
    {
        const string CorrelationId = "abc-123-def";

        using HttpResponseMessage response = await SendAsync(CorrelationId);

        Assert.Equal(CorrelationId, Assert.Single(response.Headers.GetValues(HeaderName)));
    }

    [Fact]
    public async Task OversizedCorrelationId_IsNotEchoedVerbatim()
    {
        string oversized = new('a', 129);

        using HttpResponseMessage response = await SendAsync(oversized);
        string echoed = Assert.Single(response.Headers.GetValues(HeaderName));

        Assert.NotEqual(oversized, echoed);
        Assert.True(echoed.Length <= 128);
    }

    [Fact]
    public async Task CorrelationIdWithControlCharacters_IsNotEchoedVerbatim()
    {
        // Bell character embedded in an otherwise plausible value.
        const string Malformed = "bad\u0007value";

        using HttpResponseMessage response = await SendAsync(Malformed);
        string echoed = Assert.Single(response.Headers.GetValues(HeaderName));

        Assert.NotEqual(Malformed, echoed);
        Assert.DoesNotContain(echoed, static character => char.IsControl(character));
    }

    [Fact]
    public async Task MissingCorrelationId_IsGenerated()
    {
        using var factory = new SquadCrmApiFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(HealthUri);

        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(response.Headers.GetValues(HeaderName))));
    }

    [Fact]
    public async Task ValidationProblemResponse_CorrelationIdBody_MatchesTheResponseHeader()
    {
        using var factory = new SquadCrmApiFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(ValidationFailingUri);
        string body = await response.Content.ReadAsStringAsync();

        string headerValue = Assert.Single(response.Headers.GetValues(HeaderName));

        using JsonDocument document = JsonDocument.Parse(body);
        string? bodyCorrelationId = document.RootElement.GetProperty("correlationId").GetString();

        Assert.Equal(headerValue, bodyCorrelationId);
    }

    [Fact]
    public async Task CorrelationIdAndTraceId_DivergeWhenAnActivityIsActive()
    {
        using var factory = new SquadCrmApiFactory();
        using HttpClient client = factory.CreateClient();

        using var activity = new Activity("test-correlation-divergence").Start();

        using HttpResponseMessage response = await client.GetAsync(ValidationFailingUri);
        string body = await response.Content.ReadAsStringAsync();

        activity.Stop();

        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;

        string? correlationId = root.GetProperty("correlationId").GetString();
        string? traceId = root.GetProperty("traceId").GetString();

        Assert.NotNull(correlationId);
        Assert.NotNull(traceId);
        Assert.NotEqual(correlationId, traceId);
    }

    private static async Task<HttpResponseMessage> SendAsync(string correlationId)
    {
        using var factory = new SquadCrmApiFactory();
        using HttpClient client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, HealthUri);
        request.Headers.TryAddWithoutValidation(HeaderName, correlationId);

        return await client.SendAsync(request);
    }
}
