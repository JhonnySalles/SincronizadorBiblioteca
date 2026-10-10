using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SyncLib.Core.Entities;
using SyncLib.Core.Enums;
using SyncLib.Core.Services.Storage;

namespace SyncLib.Infrastructure.Services.Storage;

public class StorageProviderFactory : IStorageProviderFactory
{
    public IStorageProvider CreateProvider(ConfigurationPath config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        return config.ConnectionType switch
        {
            StorageConnectionType.Local or StorageConnectionType.Network => new LocalStorageProvider(),
            StorageConnectionType.Ftp => new FtpStorageProvider(
                config.ServerHost,
                config.ServerPort > 0 ? config.ServerPort : 21,
                config.Username,
                config.Password
            ),
            _ => new LocalStorageProvider()
        };
    }

    public async Task TransferFileAsync(IStorageProvider sourceProvider, string sourcePath, IStorageProvider destProvider, string destPath, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        if (sourceProvider is LocalStorageProvider && destProvider is LocalStorageProvider)
        {
            await sourceProvider.CopyFileAsync(sourcePath, destPath, overwrite, cancellationToken);
            return;
        }

        await using var sourceStream = await sourceProvider.OpenReadAsync(sourcePath, cancellationToken);
        await destProvider.UploadFileAsync(sourceStream, destPath, overwrite, cancellationToken);
    }
}
