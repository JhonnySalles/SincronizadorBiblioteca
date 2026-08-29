using Microsoft.EntityFrameworkCore;
using SyncLib.Core.Entities;
using System;
using System.IO;

namespace SyncLib.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public DbSet<ConfigurationPath> ConfigurationPaths { get; set; } = null!;
    public DbSet<DirectoryCache> DirectoryCaches { get; set; } = null!;
    public DbSet<NamingPattern> NamingPatterns { get; set; } = null!;

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
            entity.Property(e => e.CustomSuffix).HasDefaultValue("");
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
