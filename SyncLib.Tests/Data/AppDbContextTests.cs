using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SyncLib.Core.Entities;
using SyncLib.Core.Enums;
using SyncLib.Infrastructure.Data;
using Xunit;

namespace SyncLib.Tests.Data;

public class AppDbContextTests
{
    private AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"SyncLib_TestDb_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    #region ConfigurationPath Tests

    [Fact]
    public async Task ConfigurationPaths_CanAddAndRetrieve()
    {
        using var context = CreateInMemoryContext();

        var path = new ConfigurationPath
        {
            Path = "C:\\MangaSource",
            MediaType = MediaType.MangaPortugues,
            CustomSuffix = "_manga",
            CustomFolderSuffix = "_folder"
        };

        context.ConfigurationPaths.Add(path);
        await context.SaveChangesAsync();

        var retrieved = await context.ConfigurationPaths.FirstOrDefaultAsync(p => p.Path == "C:\\MangaSource");
        retrieved.Should().NotBeNull();
        retrieved!.MediaType.Should().Be(MediaType.MangaPortugues);
        retrieved.CustomSuffix.Should().Be("_manga");
        retrieved.CustomFolderSuffix.Should().Be("_folder");
    }

    [Fact]
    public async Task ConfigurationPaths_CanUpdateAndRemove()
    {
        using var context = CreateInMemoryContext();

        var path = new ConfigurationPath
        {
            Path = "C:\\EbookSource",
            MediaType = MediaType.EbookPortugues
        };

        context.ConfigurationPaths.Add(path);
        await context.SaveChangesAsync();

        path.Path = "C:\\NewEbookSource";
        await context.SaveChangesAsync();

        var updated = await context.ConfigurationPaths.FindAsync(path.Id);
        updated!.Path.Should().Be("C:\\NewEbookSource");

        context.ConfigurationPaths.Remove(updated);
        await context.SaveChangesAsync();

        var exists = await context.ConfigurationPaths.AnyAsync(p => p.Id == path.Id);
        exists.Should().BeFalse();
    }

    #endregion

    #region DirectoryCache Tests

    [Fact]
    public async Task DirectoryCaches_CanAddAndRetrieve()
    {
        using var context = CreateInMemoryContext();

        var cache = new DirectoryCache
        {
            RootPath = "D:\\Mangas",
            SeriesName = "Naruto",
            FolderPath = "D:\\Mangas\\Naruto",
            MediaType = MediaType.MangaPortugues,
            LastScanned = DateTime.UtcNow
        };

        context.DirectoryCaches.Add(cache);
        await context.SaveChangesAsync();

        var retrieved = await context.DirectoryCaches.FirstOrDefaultAsync(c => c.SeriesName == "Naruto");
        retrieved.Should().NotBeNull();
        retrieved!.FolderPath.Should().Be("D:\\Mangas\\Naruto");
        retrieved.MediaType.Should().Be(MediaType.MangaPortugues);
    }

    #endregion

    #region NamingPattern Tests

    [Fact]
    public async Task NamingPatterns_CanAddAndRetrieve()
    {
        using var context = CreateInMemoryContext();

        var pattern = new NamingPattern
        {
            OriginalRawSeries = "One Piece",
            CustomTemplate = "{Series} - Vol. {Volume:D2}{Extension}"
        };

        context.NamingPatterns.Add(pattern);
        await context.SaveChangesAsync();

        var retrieved = await context.NamingPatterns.FirstOrDefaultAsync(p => p.OriginalRawSeries == "One Piece");
        retrieved.Should().NotBeNull();
        retrieved!.CustomTemplate.Should().Be("{Series} - Vol. {Volume:D2}{Extension}");
    }

    #endregion
}
