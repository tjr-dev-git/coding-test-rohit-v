using Dispatch.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Api.Data;

public sealed class DispatchDbContext(DbContextOptions<DispatchDbContext> options) : DbContext(options)
{
    public DbSet<Movement> Movements => Set<Movement>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Equipment> Equipment => Set<Equipment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Movement>(entity =>
        {
            entity.ToContainer("Movements");
            entity.HasKey(x => new { x.Id, x.SiteId });
            entity.HasPartitionKey(x => x.SiteId);
            entity.Property(x => x.ETag).IsETagConcurrency();
            entity.Property(x => x.Priority).HasConversion<string>();
            entity.Property(x => x.Status).HasConversion<string>();
        });

        modelBuilder.Entity<Site>(entity =>
        {
            entity.ToContainer("Sites");
            entity.HasKey(x => x.Id);
            entity.HasPartitionKey(x => x.Id);
        });

        modelBuilder.Entity<Equipment>(entity =>
        {
            entity.ToContainer("Equipment");
            entity.HasKey(x => new { x.Id, x.SiteId });
            entity.HasPartitionKey(x => x.SiteId);
        });
    }
}

