using SquadCrm.Modules.ArchitectureFixture.Contracts;

namespace SquadCrm.Modules.ArchitectureFixture;

/// <summary>
/// Module-internal service. Deliberately <c>internal</c>: the API host references
/// this assembly only to construct <see cref="ArchitectureFixtureModule"/>, and
/// cannot see or resolve this abstraction. That is the boundary being proven.
/// </summary>
internal interface IModuleInfoProvider
{
    ModuleInfoResponse Describe();
}
