using System.Threading;
using System.Threading.Tasks;
using SyncLib.Core.Entities;

namespace SyncLib.Core.Services.Storage;

public interface IStorageProviderFactory
{
    IStorageProvider CreateProvider(ConfigurationPath config);
    Task TransferFileAsync(IStorageProvider sourceProvider, string sourcePath, IStorageProvider destProvider, string destPath, bool overwrite = true, CancellationToken cancellationToken = default);
}
