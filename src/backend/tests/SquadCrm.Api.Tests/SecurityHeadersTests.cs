namespace SquadCrm.Api.Tests;

/// <summary>
/// The conventional, low-risk security-header baseline (CRM-204). HSTS is gated on
/// <c>IHostEnvironment.IsProduction()</c>, not the individual request's scheme.
/// </summary>
public sealed class SecurityHeadersTests
{
    private static readonly Uri HealthUri = new("/health", UriKind.Relative);

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Response_AlwaysCarriesTheBaselineHeaders(string environment)
    {
        using var factory = new SquadCrmApiFactory(environment);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(HealthUri);

        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal(
            "strict-origin-when-cross-origin",
            Assert.Single(response.Headers.GetValues("Referrer-Policy")));
    }

    [Fact]
    public async Task Development_DoesNotCarryStrictTransportSecurity()
    {
        using var factory = new SquadCrmApiFactory("Development");
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(HealthUri);

        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Production_CarriesStrictTransportSecurity()
    {
        using var factory = new SquadCrmApiFactory("Production");
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(HealthUri);

        Assert.Equal(
            "max-age=31536000; includeSubDomains",
            Assert.Single(response.Headers.GetValues("Strict-Transport-Security")));
    }

    [Fact]
    public async Task UnhandledExceptionResponse_StillCarriesTheBaselineHeaders()
    {
        using var factory = new SquadCrmApiFactory("Production", injectFault: true);
        using HttpClient client = factory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(SquadCrmApiFactory.FaultRoute, UriKind.Relative));
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost:4200");

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal(
            "strict-origin-when-cross-origin",
            Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal(
            "max-age=31536000; includeSubDomains",
            Assert.Single(response.Headers.GetValues("Strict-Transport-Security")));
    }
}
