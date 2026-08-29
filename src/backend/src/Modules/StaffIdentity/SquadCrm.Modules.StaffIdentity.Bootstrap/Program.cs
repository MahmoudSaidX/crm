namespace SquadCrm.Modules.StaffIdentity.Bootstrap;

internal static class BootstrapEntryPoint
{
    public static Task<int> Main(string[] args) =>
        BootstrapProgram.RunAsync(args, CancellationToken.None);
}
