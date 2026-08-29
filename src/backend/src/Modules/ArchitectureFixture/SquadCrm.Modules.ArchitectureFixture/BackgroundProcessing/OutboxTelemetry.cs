using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SquadCrm.Modules.ArchitectureFixture.BackgroundProcessing;

/// <summary>Provider-neutral diagnostics consumed by the host's OpenTelemetry pipeline.</summary>
public static class OutboxTelemetry
{
    public const string ActivitySourceName = "SquadCrm.Outbox";
    public const string MeterName = "SquadCrm.Outbox";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> ProcessedMessages =
        Meter.CreateCounter<long>("squadcrm.outbox.processed");
    public static readonly Counter<long> FailedMessages =
        Meter.CreateCounter<long>("squadcrm.outbox.failed");
}
