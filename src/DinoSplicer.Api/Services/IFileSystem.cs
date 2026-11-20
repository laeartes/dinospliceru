namespace DinoSplicer.Api.Services;

public interface IFileSystem
{
    /// <summary>Creates the directory if it does not exist and returns whether it was newly created.</summary>
    Task<bool> CreateDirectoryIfNotExistsAsync(string directoryPath, CancellationToken cancellationToken = default);

    Task<long> GetFileLengthAsync(string filePath, CancellationToken cancellationToken = default);

    Task DeleteFileIfExistsAsync(string filePath, CancellationToken cancellationToken = default);

    Task DeleteDirectoryRecursiveAsync(string directoryPath, CancellationToken cancellationToken = default);

    Task DeleteDirectoryIfEmptyAsync(string directoryPath, CancellationToken cancellationToken = default);
}
