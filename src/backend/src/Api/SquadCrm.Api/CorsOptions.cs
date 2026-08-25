namespace SquadCrm.Api;

/// <summary>
/// Configuration-driven CORS allow-list, bound from the <c>Cors</c> section.
/// An absent or empty section means no cross-origin request is allowed.
/// </summary>
internal sealed class CorsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Cors";

    /// <summary>Exact origins permitted to call the API. Empty by default.</summary>
    public string[] AllowedOrigins { get; init; } = [];
}
