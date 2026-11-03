using System.Xml;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ChatwebSocket;

public class DbContex(DbContextOptions<DbContex> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Messager> Messages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var uniqueIdConverter = new ValueConverter<UniqueId, string>(
            v => v.ToString(),
            v => new UniqueId(v));

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users").HasKey(u => u.Uid);
            entity.Property(u => u.Uid).HasConversion(uniqueIdConverter);
        });

        modelBuilder.Entity<Messager>(entity =>
        {
            entity.ToTable("messagers").HasKey(m => m.Uid);
            entity.Property(m => m.Uid).HasConversion(uniqueIdConverter);
            entity.Property(m => m.UserId).HasConversion(uniqueIdConverter);
        });

        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=./storage/database.db");
        }
        base.OnConfiguring(optionsBuilder);
    }
}
