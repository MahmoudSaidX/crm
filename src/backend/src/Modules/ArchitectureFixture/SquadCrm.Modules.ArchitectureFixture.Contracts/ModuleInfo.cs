namespace SquadCrm.Modules.ArchitectureFixture.Contracts;

/// <summary>
/// Infrastructure/demo-only contract. Exists to prove the contracts vs
/// implementation split and module endpoint registration. Not a CRM
/// capability; expect it to be deleted once real modules land.
/// </summary>
public sealed record ModuleInfoResponse(string Module, string Status);
