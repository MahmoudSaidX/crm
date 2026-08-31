namespace SquadCrm.Tools.RoleManagementBootstrap;

internal static class BootstrapEntryPoint
{
    public static Task<int> Main(string[] args) =>
        BootstrapProgram.RunAsync(args, CancellationToken.None);
}
