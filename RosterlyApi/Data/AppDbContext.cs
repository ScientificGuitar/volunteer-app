using Microsoft.EntityFrameworkCore;
using RosterlyApi.Entities;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
    public DbSet<Signup> Signups => Set<Signup>();
    public DbSet<InviteLink> InviteLinks => Set<InviteLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ClerkUserId).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.ClerkUserId);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(300).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.HasOne(e => e.Organization)
                .WithMany(o => o.Events)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.OrganizationId, e.Date });
        });

        modelBuilder.Entity<TimeSlot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).HasMaxLength(200).IsRequired();
            entity.HasOne(e => e.Event)
                .WithMany(o => o.TimeSlots)
                .HasForeignKey(e => e.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Signup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.VolunteerName).HasMaxLength(200).IsRequired();
            entity.HasOne(e => e.TimeSlot)
                .WithMany(o => o.Signups)
                .HasForeignKey(e => e.TimeSlotId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InviteLink>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasOne(e => e.Organization)
                .WithMany(o => o.InviteLinks)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
