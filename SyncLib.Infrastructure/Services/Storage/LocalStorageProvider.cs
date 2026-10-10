using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SyncLib.Core.Services.Storage;

namespace SyncLib.Infrastructure.Services.Storage;

public class LocalStorageProvider : IStorageProvider
{
    public Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(path)) return Task.FromResult(false);
        return Task.FromResult(Directory.Exists(path));
    }

    public Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(path)) return Task.FromResult(false);
        return Task.FromResult(File.Exists(path));
    }

    public Task<IReadOnlyList<string>> GetFilesAsync(string directoryPath, string searchPattern = "*.*", bool recursive = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(directoryPath, searchPattern, option);
        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    public Task<IReadOnlyList<string>> GetDirectoriesAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var dirs = Directory.GetDirectories(directoryPath);
        return Task.FromResult<IReadOnlyList<string>>(dirs);
    }

    public Task CreateDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
        return Task.CompletedTask;
    }

    public async Task CopyFileAsync(string sourcePath, string destinationPath, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var destDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        await using var destStream = new FileStream(destinationPath, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await sourceStream.CopyToAsync(destStream, cancellationToken);
    }

    public Task<long> GetFileSizeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(filePath)) return Task.FromResult(0L);
        var info = new FileInfo(filePath);
        return Task.FromResult(info.Length);
    }

    public Task<DateTime> GetLastModifiedAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(filePath)) return Task.FromResult(DateTime.MinValue);
        return Task.FromResult(File.GetLastWriteTime(filePath));
    }

    public Task<Stream> OpenReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        return Task.FromResult(stream);
    }

    public Task<Stream> OpenWriteAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var destDir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        Stream stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        return Task.FromResult(stream);
    }

    public async Task UploadFileAsync(Stream sourceStream, string destinationPath, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        var destDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        await using var destStream = new FileStream(destinationPath, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await sourceStream.CopyToAsync(destStream, cancellationToken);
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }

    public void Dispose()
    {
        // No resources to dispose for local storage
    }
}
