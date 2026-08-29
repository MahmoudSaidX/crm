namespace SquadCrm.BuildingBlocks.Abstractions.Files;

/// <summary>Metadata and content supplied by an already-authorized owning module.</summary>
public sealed record FileUpload(
    Stream Content,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string CreatedBy);
