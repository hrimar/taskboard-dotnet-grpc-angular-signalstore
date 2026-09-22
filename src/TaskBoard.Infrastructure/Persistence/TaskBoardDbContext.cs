using Microsoft.EntityFrameworkCore;
using TaskBoard.Domain;

namespace TaskBoard.Infrastructure.Persistence;

public class TaskBoardDbContext : DbContext
{
    public TaskBoardDbContext(DbContextOptions<TaskBoardDbContext> options) : base(options)
    {
    }

    public DbSet<Board> Boards => Set<Board>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    public DbSet<Label> Labels => Set<Label>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration<T> in this assembly (see Configurations/*) instead of configuring entities inline here.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TaskBoardDbContext).Assembly);
    }
}
