namespace SquadCrm.Api.Tests;

/// <summary>A client-supplied correlation id is sanitised, never echoed verbatim.</summary>
public sealed class CorrelationIdTests
{
    private const string HeaderName = "X-Correlation-Id";
    private static readonly Uri HealthUri = new("/health", UriKind.Relative);

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

    private static async Task<HttpResponseMessage> SendAsync(string correlationId)
    {
        using var factory = new SquadCrmApiFactory();
        using HttpClient client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, HealthUri);
        request.Headers.TryAddWithoutValidation(HeaderName, correlationId);

        return await client.SendAsync(request);
    }
}
