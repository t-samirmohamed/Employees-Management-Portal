using EmpoloyeeManagment.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EmpoloyeeManagment.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<ApiCallLog> ApiCallLogs => Set<ApiCallLog>();

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
        });

        builder.Entity<ApiCallLog>(entity =>
        {
            entity.Property(e => e.Method).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Path).HasMaxLength(512).IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.IpAddress).HasMaxLength(64);
        });
    }
}
