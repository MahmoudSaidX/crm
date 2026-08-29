using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SquadCrm.BuildingBlocks.Abstractions.Files;

namespace SquadCrm.Infrastructure.FileStorage;

public static class FileStorageConfiguration
{
    public static TBuilder AddSquadCrmFileStorage<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        FileStorageOptions configured =
            builder.Configuration.GetSection(FileStorageOptions.SectionName).Get<FileStorageOptions>()
            ?? new FileStorageOptions();

        if (configured.MaxSizeBytes <= 0)
        {
            throw new InvalidOperationException("FileStorage:MaxSizeBytes must be greater than zero.");
        }

        if (configured.AllowedContentTypes.Length == 0
            || configured.AllowedContentTypes.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("FileStorage:AllowedContentTypes must contain valid values.");
        }

        if (!string.Equals(configured.Provider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"File storage provider '{configured.Provider}' is not registered. Register an IFileStorage adapter in the composition root.");
        }

        string rootPath = Path.IsPathRooted(configured.LocalRootPath)
            ? configured.LocalRootPath
            : Path.Combine(builder.Environment.ContentRootPath, configured.LocalRootPath);

        FileStorageOptions options = new()
        {
            Provider = configured.Provider,
            LocalRootPath = rootPath,
            MaxSizeBytes = configured.MaxSizeBytes,
            AllowedContentTypes = configured.AllowedContentTypes,
        };

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IFileUploadValidator, ConfiguredFileUploadValidator>();
        builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

        return builder;
    }
}
