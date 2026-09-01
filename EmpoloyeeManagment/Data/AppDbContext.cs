using EmpoloyeeManagment.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EmpoloyeeManagment.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<ApiCallLog> ApiCallLogs => Set<ApiCallLog>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Location> Locations => Set<Location>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            // Identity's default EmailIndex isn't unique; tighten it since every account
            // maps 1:1 to an Employee record and must be addressable by a unique email.
            entity.HasIndex(u => u.NormalizedEmail).IsUnique();
        });

        builder.Entity<Employee>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Gender).HasConversion<string>().HasMaxLength(1);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.AttendanceStatus).HasConversion<string>().HasMaxLength(20);

            // One employee record per user account (where linked at all).
            entity.HasIndex(e => e.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<Location>()
                .WithMany()
                .HasForeignKey(e => e.AssignedLocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApiCallLog>(entity =>
        {
            entity.Property(e => e.Method).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Path).HasMaxLength(512).IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.IpAddress).HasMaxLength(64);
        });

        builder.Entity<TaskItem>(entity =>
        {
            entity.Property(t => t.Name).HasMaxLength(200).IsRequired();
            entity.Property(t => t.Notes).HasMaxLength(2000);
            entity.Property(t => t.RejectionReason).HasMaxLength(1000);
            entity.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

            entity.HasOne<Employee>()
                .WithMany()
                .HasForeignKey(t => t.AssigneeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(t => t.RelatedVisitId).IsUnique().HasFilter("[RelatedVisitId] IS NOT NULL");
            entity.HasOne<Visit>()
                .WithMany()
                .HasForeignKey(t => t.RelatedVisitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TaskComment>(entity =>
        {
            entity.Property(c => c.Text).IsRequired();

            entity.HasOne<TaskItem>()
                .WithMany()
                .HasForeignKey(c => c.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Employee>()
                .WithMany()
                .HasForeignKey(c => c.AuthorEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Client>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(200).IsRequired();
            entity.Property(c => c.Email).HasMaxLength(256).IsRequired();
            entity.Property(c => c.Contact).HasMaxLength(100).IsRequired();
        });

        builder.Entity<Location>(entity =>
        {
            entity.Property(l => l.Name).HasMaxLength(200).IsRequired();
            entity.Property(l => l.Email).HasMaxLength(256).IsRequired();
            entity.Property(l => l.Contact).HasMaxLength(100).IsRequired();

            entity.HasOne<Client>()
                .WithMany()
                .HasForeignKey(l => l.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Visit>(entity =>
        {
            entity.HasOne<Client>()
                .WithMany()
                .HasForeignKey(v => v.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Location>()
                .WithMany()
                .HasForeignKey(v => v.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
