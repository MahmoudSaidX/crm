using SquadCrm.Modules.ArchitectureFixture.Contracts;

namespace SquadCrm.Modules.ArchitectureFixture;

/// <summary>
/// Module-internal implementation. Infrastructure scaffolding only: it reports
/// that the module was registered and holds no state, no persistence and no rules.
/// </summary>
internal sealed class ModuleInfoProvider : IModuleInfoProvider
{
    public ModuleInfoResponse Describe() =>
        new(ArchitectureFixtureModule.ModuleName, "registered");
}
