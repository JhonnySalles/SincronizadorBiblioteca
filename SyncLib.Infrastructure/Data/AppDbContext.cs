using Microsoft.EntityFrameworkCore;
using SyncLib.Core.Entities;
using SyncLib.Core.Enums;
using System;
using System.IO;

namespace SyncLib.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public DbSet<ConfigurationPath> ConfigurationPaths { get; set; } = null!;
    public DbSet<DirectoryCache> DirectoryCaches { get; set; } = null!;
    public DbSet<NamingPattern> NamingPatterns { get; set; } = null!;

    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public static string DbPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SyncLib",
        "synclib.db"
    );

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var folder = Path.GetDirectoryName(DbPath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            optionsBuilder.UseSqlite($"Data Source={DbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ConfigurationPath>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Path).IsRequired();
            entity.Property(e => e.MediaType).HasConversion<string>();
            entity.Property(e => e.ConnectionType).HasConversion<string>().HasDefaultValue(StorageConnectionType.Local);
            entity.Property(e => e.ServerHost).HasDefaultValue("");
            entity.Property(e => e.ServerPort).HasDefaultValue(0);
            entity.Property(e => e.Username).HasDefaultValue("");
            entity.Property(e => e.Password).HasDefaultValue("");
            entity.Property(e => e.CustomSuffix).HasDefaultValue("");
            entity.Property(e => e.CustomFolderSuffix).HasDefaultValue("");
        });

        modelBuilder.Entity<DirectoryCache>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RootPath).IsRequired();
            entity.Property(e => e.SeriesName).IsRequired();
            entity.Property(e => e.FolderPath).IsRequired();
            entity.Property(e => e.MediaType).HasConversion<string>();
        });

        modelBuilder.Entity<NamingPattern>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OriginalRawSeries).IsRequired();
            entity.Property(e => e.CustomTemplate).IsRequired();
        });
    }
}
