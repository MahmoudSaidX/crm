namespace SquadCrm.ArchitectureTests;

/// <summary>
/// Locates source files on disk for the handful of rules that must inspect a
/// <c>.csproj</c> file's actual XML content rather than compiled assembly
/// metadata (see <see cref="CsprojDependencyRulesTests"/> for why: a
/// <c>FrameworkReference</c> — and a <c>PackageReference</c>/<c>ProjectReference</c>
/// whose types go unused — never appears in
/// <see cref="System.Reflection.Assembly.GetReferencedAssemblies"/>, so a
/// purely reflection-based test cannot see one).
/// <para>
/// Walks up from <see cref="AppContext.BaseDirectory"/> (the test's own
/// <c>bin/Debug/net10.0</c> output folder) until it finds the directory
/// containing <c>SquadCrm.sln</c> — no hard-coded absolute path, so this works
/// unchanged in any checkout location or CI runner.
/// </para>
/// </summary>
internal static class RepositoryPaths
{
    private const string SolutionFileName = "SquadCrm.sln";

    public static string BackendDirectory { get; } = FindBackendDirectory();

    public static string SrcDirectory { get; } = Path.Combine(BackendDirectory, "src");

    private static string FindBackendDirectory()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, SolutionFileName)))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate '{SolutionFileName}' by walking up from '{AppContext.BaseDirectory}'. "
            + "This test project must run from within the repository checkout.");
    }
}
