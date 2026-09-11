namespace SquadCrm.Tools.DemoDataSeeder;

internal static class DemoDataSeederEntryPoint
{
    public static Task<int> Main(string[] args) =>
        DemoSeedProgram.RunAsync(args, CancellationToken.None);
}
