namespace SquadCrm.Infrastructure.Postgres;

/// <summary>
/// The PostgreSQL coordinates, read from the single operator-facing contract
/// owned by CRM-197 (<c>POSTGRES_*</c> in <c>env/backend.env</c>).
/// <para>
/// The keys are deliberately flat (<c>POSTGRES_HOST</c>, not
/// <c>Postgres__Host</c>) because that is the contract Docker Compose and the
/// developer environment file already use. No second externally configured set
/// of database values is introduced.
/// </para>
/// </summary>
public sealed record PostgresOptions(
    string Host,
    int Port,
    string Database,
    string Username,
    string Password)
{
    /// <summary>Configuration key for the host-side hostname.</summary>
    public const string HostKey = "POSTGRES_HOST";

    /// <summary>Configuration key for the published host port.</summary>
    public const string PortKey = "POSTGRES_PORT";

    /// <summary>Configuration key for the database name.</summary>
    public const string DatabaseKey = "POSTGRES_DB";

    /// <summary>Configuration key for the user name.</summary>
    public const string UsernameKey = "POSTGRES_USER";

    /// <summary>Configuration key for the password. Its value is never logged or thrown.</summary>
    public const string PasswordKey = "POSTGRES_PASSWORD";

    /// <summary>
    /// Internal, conventional connection-string name derived from the keys above.
    /// It is an application-internal name, never an operator-facing setting: it is
    /// produced at composition time and is not read from any file or environment.
    /// </summary>
    public const string ConnectionStringName = "SquadCrmPostgres";

    /// <summary>Lowest valid TCP port.</summary>
    public const int MinimumPort = 1;

    /// <summary>Highest valid TCP port.</summary>
    public const int MaximumPort = 65535;
}
