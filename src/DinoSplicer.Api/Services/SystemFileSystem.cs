namespace DinoSplicer.Api.Services;

public class SystemFileSystem : IFileSystem
{
    public Task<bool> CreateDirectoryIfNotExistsAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (Directory.Exists(directoryPath))
            {
                return false;
            }

            Directory.CreateDirectory(directoryPath);
            return true;
        }, cancellationToken);
    }

    public Task<long> GetFileLengthAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => new FileInfo(filePath).Length, cancellationToken);
    }

    public Task DeleteFileIfExistsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }, cancellationToken);
    }

    public Task DeleteDirectoryRecursiveAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }, cancellationToken);
    }

    public Task DeleteDirectoryIfEmptyAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (Directory.Exists(directoryPath) && !Directory.EnumerateFileSystemEntries(directoryPath).Any())
            {
                Directory.Delete(directoryPath, recursive: false);
            }
        }, cancellationToken);
    }
}
