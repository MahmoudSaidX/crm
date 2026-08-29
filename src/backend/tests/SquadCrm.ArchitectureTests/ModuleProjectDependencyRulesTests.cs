using System.Xml.Linq;

namespace SquadCrm.ArchitectureTests;

public sealed class ModuleProjectDependencyRulesTests
{
    [Fact]
    public void Modules_MayReferenceOtherModulesOnlyThroughContractsProjects()
    {
        string modulesDirectory = Path.Combine(RepositoryPaths.SrcDirectory, "Modules");
        string[] moduleProjects = Directory.GetFiles(
            modulesDirectory,
            "*.csproj",
            SearchOption.AllDirectories);

        Assert.NotEmpty(moduleProjects);

        List<string> violations = [];
        foreach (string moduleProject in moduleProjects.Where(
            path => !Path.GetFileNameWithoutExtension(path)
                .EndsWith(SquadCrmAssemblies.ContractsSuffix, StringComparison.Ordinal)))
        {
            string ownModuleDirectory = Directory.GetParent(Path.GetDirectoryName(moduleProject)!)!.FullName;
            XDocument project = XDocument.Load(moduleProject);

            foreach (string include in project.Descendants("ProjectReference")
                .Select(element => element.Attribute("Include")?.Value)
                .OfType<string>())
            {
                string referencedPath = Path.GetFullPath(
                    include.Replace('\\', Path.DirectorySeparatorChar),
                    Path.GetDirectoryName(moduleProject)!);

                if (!referencedPath.StartsWith(modulesDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    || referencedPath.StartsWith(ownModuleDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!Path.GetFileNameWithoutExtension(referencedPath)
                    .EndsWith(SquadCrmAssemblies.ContractsSuffix, StringComparison.Ordinal))
                {
                    violations.Add(
                        $"{Path.GetFileName(moduleProject)} references foreign module implementation "
                        + $"{Path.GetFileName(referencedPath)}.");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join(" ", violations));
    }
}
