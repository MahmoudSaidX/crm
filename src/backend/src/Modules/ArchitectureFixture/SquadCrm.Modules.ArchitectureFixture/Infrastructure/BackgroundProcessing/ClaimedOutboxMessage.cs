namespace SquadCrm.Modules.ArchitectureFixture.Infrastructure.BackgroundProcessing;

internal sealed record ClaimedOutboxMessage(Guid Id, string Type, string Payload, string CorrelationId);
