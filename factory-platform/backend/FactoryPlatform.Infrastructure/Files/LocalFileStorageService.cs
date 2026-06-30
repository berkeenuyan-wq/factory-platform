using FactoryPlatform.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace FactoryPlatform.Infrastructure.Files;

public sealed class LocalFileStorageService(IConfiguration configuration) : IFileStorageService
{
    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        var root = configuration["FileStorage:RootPath"] ?? "storage/files";
        Directory.CreateDirectory(root);

        var safeName = Path.GetFileName(fileName);
        var storageName = $"{Guid.NewGuid():N}-{safeName}";
        var path = Path.Combine(root, storageName);

        await using var output = File.Create(path);
        await content.CopyToAsync(output, cancellationToken);

        return path;
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        Stream stream = File.OpenRead(storagePath);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (File.Exists(storagePath))
        {
            File.Delete(storagePath);
        }

        return Task.CompletedTask;
    }
}
