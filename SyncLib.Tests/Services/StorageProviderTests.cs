using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SyncLib.Core.Entities;
using SyncLib.Core.Enums;
using SyncLib.Infrastructure.Services.Storage;
using Xunit;

namespace SyncLib.Tests.Services;

public class StorageProviderTests : IDisposable
{
    private readonly string _testTempDir;

    public StorageProviderTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), "SyncLib_StorageTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testTempDir))
        {
            try
            {
                Directory.Delete(_testTempDir, true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task LocalStorageProvider_DirectoryAndFileOperations_WorkCorrectly()
    {
        using var provider = new LocalStorageProvider();

        var subDir = Path.Combine(_testTempDir, "SubDir");
        var testFile = Path.Combine(subDir, "test.txt");

        // DirectoryExists should be false initially
        Assert.False(await provider.DirectoryExistsAsync(subDir));

        // Create directory
        await provider.CreateDirectoryAsync(subDir);
        Assert.True(await provider.DirectoryExistsAsync(subDir));

        // FileExists false initially
        Assert.False(await provider.FileExistsAsync(testFile));

        // Write content to stream
        var content = "Hello SyncLib Storage!";
        using (var writeStream = await provider.OpenWriteAsync(testFile))
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            await writeStream.WriteAsync(bytes);
        }

        // FileExists should now be true
        Assert.True(await provider.FileExistsAsync(testFile));

        // Get file size
        var size = await provider.GetFileSizeAsync(testFile);
        Assert.Equal(Encoding.UTF8.GetByteCount(content), size);

        // Read content back
        using (var readStream = await provider.OpenReadAsync(testFile))
        using (var reader = new StreamReader(readStream, Encoding.UTF8))
        {
            var readContent = await reader.ReadToEndAsync();
            Assert.Equal(content, readContent);
        }

        // GetFiles
        var files = await provider.GetFilesAsync(_testTempDir, "*.txt", recursive: true);
        Assert.Single(files);
        Assert.Equal(testFile, files[0]);

        // GetDirectories
        var dirs = await provider.GetDirectoriesAsync(_testTempDir);
        Assert.Single(dirs);
        Assert.Equal(subDir, dirs[0]);
    }

    [Fact]
    public async Task StorageProviderFactory_TransferFile_TransfersDataCorrectly()
    {
        var factory = new StorageProviderFactory();

        var srcDir = Path.Combine(_testTempDir, "Source");
        var destDir = Path.Combine(_testTempDir, "Dest");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(destDir);

        var srcFile = Path.Combine(srcDir, "sample.cbz");
        var destFile = Path.Combine(destDir, "sample_copied.cbz");

        await File.WriteAllBytesAsync(srcFile, new byte[] { 1, 2, 3, 4, 5 });

        var srcConfig = new ConfigurationPath { Path = srcDir, ConnectionType = StorageConnectionType.Local };
        var destConfig = new ConfigurationPath { Path = destDir, ConnectionType = StorageConnectionType.Local };

        using var srcProvider = factory.CreateProvider(srcConfig);
        using var destProvider = factory.CreateProvider(destConfig);

        await factory.TransferFileAsync(srcProvider, srcFile, destProvider, destFile, overwrite: true);

        Assert.True(File.Exists(destFile));
        var destBytes = await File.ReadAllBytesAsync(destFile);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, destBytes);
    }

    [Fact]
    public void StorageProviderFactory_CreatesFtpProviderForFtpConfig()
    {
        var factory = new StorageProviderFactory();
        var ftpConfig = new ConfigurationPath
        {
            Path = "/mangas",
            ConnectionType = StorageConnectionType.Ftp,
            ServerHost = "ftp.example.com",
            ServerPort = 2121,
            Username = "user",
            Password = "pass"
        };

        using var provider = factory.CreateProvider(ftpConfig);
        Assert.IsType<FtpStorageProvider>(provider);
    }
}
