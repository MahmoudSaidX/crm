using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.ArchitectureFixture.BackgroundProcessing;

public sealed class OutboxProcessingOptions
{
    public const string SectionName = "BackgroundProcessing:Outbox";

    [Range(1, 100)]
    public int BatchSize { get; init; } = 20;

    [Range(1, 20)]
    public int RetryCeiling { get; init; } = 5;

    [Range(10, 3600)]
    public int LeaseSeconds { get; init; } = 120;

    [Range(1, 3600)]
    public int BackoffSeconds { get; init; } = 30;
}
