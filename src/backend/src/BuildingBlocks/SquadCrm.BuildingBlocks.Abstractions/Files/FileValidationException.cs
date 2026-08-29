namespace SquadCrm.BuildingBlocks.Abstractions.Files;

public sealed class FileValidationException(string message) : Exception(message);
