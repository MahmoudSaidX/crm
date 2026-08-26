namespace SquadCrm.BuildingBlocks.Security;

/// <summary>
/// The seam CRM-110 (authentication/session) implements for real. Deliberately
/// narrow: exposes only whether the caller is authenticated and an opaque
/// handle for that caller. It carries no identity model — no user id, no
/// organizational scope, no subject-kind discriminator (staff vs. customer).
/// CRM-110 owns designing that model; this port must not pre-guess its shape.
/// <para>
/// Authorization = Permission + Organizational Scope + Resource Ownership
/// (CLAUDE.md). None of the three are represented here — they are policy
/// concerns CRM-110 and later stories add on top of whatever identity model
/// CRM-110 designs.
/// </para>
/// <para>
/// <b>No default implementation is registered</b> for this interface in this
/// story. A consumer that resolves <see cref="ICurrentUserAccessor"/> before
/// CRM-110 registers a real implementation gets a DI resolution failure
/// (fail-closed), not a plausible-looking anonymous answer — a missing
/// registration must be loud, not silently "safe-by-coincidence."
/// </para>
/// </summary>
public interface ICurrentUserAccessor
{
    bool IsAuthenticated { get; }

    /// <summary>
    /// Opaque handle identifying the current caller when <see cref="IsAuthenticated"/>
    /// is <see langword="true"/>; <see langword="null"/> otherwise. Carries no
    /// meaning beyond "the same handle means the same caller" — CRM-110 defines
    /// what it actually contains.
    /// </summary>
    string? Handle { get; }
}
