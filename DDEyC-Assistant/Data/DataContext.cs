using DDEyC_Assistant.Models;
using Microsoft.EntityFrameworkCore;

namespace DDEyC_Assistant.Data
{
   // DataContext.cs
public class DataContext : DbContext
{
    public DataContext(DbContextOptions<DataContext> options) : base(options) { }

    public DbSet<UserThread> UserThreads { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<ConversationStateEntity> ConversationStates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserThread>()
            .HasIndex(ut => new { ut.UserId, ut.IsActive })
            .IsUnique()
            .HasFilter("IsActive = 1");

        modelBuilder.Entity<Message>()
            .HasOne(m => m.UserThread)
            .WithMany()
            .HasForeignKey(m => m.UserThreadId);

        modelBuilder.Entity<ConversationStateEntity>()
            .HasIndex(s => s.ConversationId)
            .IsUnique();
    }
}
}