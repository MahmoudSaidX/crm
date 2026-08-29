using System.Xml.Linq;

namespace SquadCrm.ArchitectureTests;

/// <summary>
/// File-content rules over <c>.csproj</c> XML — a deliberate complement to the
/// assembly-metadata rules in <see cref="PersistenceArchitectureRulesTests"/>.
/// <para>
/// <b>Why this file exists (CRM-198 blocking finding).</b> A rule built on
/// <c>Assembly.GetReferencedAssemblies()</c> only sees a reference the
/// compiler actually emitted into the assembly manifest. A
/// <c>&lt;FrameworkReference&gt;</c> never appears there regardless of use,
/// and a <c>&lt;PackageReference&gt;</c>/<c>&lt;ProjectReference&gt;</c> whose
/// types go unused in code doesn't either — the CLR simply never links it.
/// Proven empirically: adding
/// <c>&lt;FrameworkReference Include="Microsoft.AspNetCore.App" /&gt;</c> to
/// <c>SquadCrm.BuildingBlocks.Abstractions.csproj</c> left every
/// assembly-metadata-based test in this project green. Only reading the
/// <c>.csproj</c> file itself catches this class of violation — exactly the
/// leak that caused blocking finding B3 (ASP.NET Core crossing the module
/// contract boundary) upstream of this story.
/// </para>
/// </summary>
public sealed class CsprojDependencyRulesTests
{
    private const string AbstractionsProjectRelativePath =
        "BuildingBlocks/SquadCrm.BuildingBlocks.Abstractions/SquadCrm.BuildingBlocks.Abstractions.csproj";

    private const string AllowedContractsProjectReferenceSuffix =
        "SquadCrm.BuildingBlocks.Abstractions.csproj";

    /// <summary>
    /// Ruling 2: <c>SquadCrm.BuildingBlocks.Abstractions</c> has NO
    /// <c>FrameworkReference</c>, NO <c>PackageReference</c> and NO
    /// <c>ProjectReference</c> — not even an unused one. This is the direct
    /// fix for the blocking finding: reading the .csproj file itself is the
    /// only way to see a <c>FrameworkReference</c> at all.
    /// </summary>
    [Fact]
    public void Abstractions_CsprojMustDeclareNoFrameworkPackageOrProjectReferences()
    {
        string path = Path.Combine(RepositoryPaths.SrcDirectory, AbstractionsProjectRelativePath);
        Assert.True(File.Exists(path), $"Expected project file at '{path}'.");

        XDocument project = XDocument.Load(path);

        string[] violations = new[] { "FrameworkReference", "PackageReference", "ProjectReference" }
            .SelectMany(elementName => project.Descendants(elementName)
                .Select(element => Describe(elementName, element)))
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"'{AbstractionsProjectRelativePath}' must declare no FrameworkReference, PackageReference or "
            + "ProjectReference (Ruling 2). Offending elements: " + string.Join(", ", violations));
    }

    /// <summary>
    /// Every module's <c>*.Contracts.csproj</c>: no <c>FrameworkReference</c>
    /// at all (the same B3-style blind spot the Abstractions rule closes —
    /// this catches ASP.NET Core added to a contracts project even if no
    /// contract type ever uses it), no <c>PackageReference</c>, and any
    /// <c>ProjectReference</c> present must point only at
    /// <c>SquadCrm.BuildingBlocks.Abstractions.csproj</c> — never
    /// <c>SquadCrm.BuildingBlocks.csproj</c> or anything else.
    /// </summary>
    [Fact]
    public void ContractsCsprojFiles_MustDeclareOnlyAbstractionsProjectReference()
    {
        string[] contractsProjectFiles = Directory.GetFiles(
            Path.Combine(RepositoryPaths.SrcDirectory, "Modules"),
            "*.Contracts.csproj",
            SearchOption.AllDirectories);

        Assert.NotEmpty(contractsProjectFiles);

        foreach (string path in contractsProjectFiles)
        {
            XDocument project = XDocument.Load(path);
            string relativePath = Path.GetRelativePath(RepositoryPaths.SrcDirectory, path);

            string[] frameworkOrPackageViolations = new[] { "FrameworkReference", "PackageReference" }
                .SelectMany(elementName => project.Descendants(elementName)
                    .Select(element => Describe(elementName, element)))
                .ToArray();

            Assert.True(
                frameworkOrPackageViolations.Length == 0,
                $"'{relativePath}' must declare no FrameworkReference or PackageReference. "
                + "Offending elements: " + string.Join(", ", frameworkOrPackageViolations));

            string[] disallowedProjectReferences = project.Descendants("ProjectReference")
                .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
                .Where(include => !include.Replace('\\', '/')
                    .EndsWith(AllowedContractsProjectReferenceSuffix, StringComparison.Ordinal))
                .ToArray();

            Assert.True(
                disallowedProjectReferences.Length == 0,
                $"'{relativePath}' may only ProjectReference '{AllowedContractsProjectReferenceSuffix}'. "
                + "Offending references: " + string.Join(", ", disallowedProjectReferences));
        }
    }

    private static string Describe(string elementName, XElement element) =>
        $"<{elementName} Include=\"{element.Attribute("Include")?.Value}\" />";
}
