namespace SquadCrm.Modules.BrandingManagement;

/// <summary>
/// Only allow-listed theme tokens are configurable (BR) — arbitrary
/// CSS/HTML/script injection through branding is not supported.
/// </summary>
internal static class ThemeTokenCatalog
{
    public static readonly IReadOnlySet<string> AllowedKeys = new HashSet<string>(
        ["primaryColor", "accentColor"],
        StringComparer.Ordinal);

    public static bool IsValidValue(string value) =>
        value.Length is > 0 and <= 32 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '#' or '-');
}
