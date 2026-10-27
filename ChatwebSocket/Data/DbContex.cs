using Microsoft.EntityFrameworkCore;


namespace ChatwebSocket;

class DbContex(DbContextOptions<DbContex> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Messager> Messages { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().ToTable("users").HasKey(u => u.Uid);
        modelBuilder.Entity<Messager>().ToTable("messagers").HasKey(m => m.Uid);
        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=./storage/database.db");
        base.OnConfiguring(optionsBuilder);
    }
}
