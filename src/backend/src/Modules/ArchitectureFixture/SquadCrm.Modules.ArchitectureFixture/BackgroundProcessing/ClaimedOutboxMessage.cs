namespace SquadCrm.Modules.ArchitectureFixture.BackgroundProcessing;

internal sealed record ClaimedOutboxMessage(Guid Id, string Type, string Payload);
