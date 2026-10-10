using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SyncLib.Core.Services.Storage;

public interface IStorageProvider : IDisposable
{
    Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default);
    Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetFilesAsync(string directoryPath, string searchPattern = "*.*", bool recursive = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetDirectoriesAsync(string directoryPath, CancellationToken cancellationToken = default);
    Task CreateDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);
    Task CopyFileAsync(string sourcePath, string destinationPath, bool overwrite = true, CancellationToken cancellationToken = default);
    Task<long> GetFileSizeAsync(string filePath, CancellationToken cancellationToken = default);
    Task<DateTime> GetLastModifiedAsync(string filePath, CancellationToken cancellationToken = default);
    Task<System.IO.Stream> OpenReadAsync(string filePath, CancellationToken cancellationToken = default);
    Task<System.IO.Stream> OpenWriteAsync(string filePath, CancellationToken cancellationToken = default);
    Task UploadFileAsync(System.IO.Stream sourceStream, string destinationPath, bool overwrite = true, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
