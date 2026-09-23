using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

public class ApplicationDbContext : DbContext
{
    /// <summary>Partial unique index that stops two active bookings on the same court/date/start.</summary>
    public const string ActiveSlotIndexName = "IX_Bookings_ActiveSlot";
    public const string ReferenceCodeIndexName = "IX_Bookings_ReferenceCode";

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Futsal> Futsals => Set<Futsal>();
    public DbSet<FutsalImage> FutsalImages => Set<FutsalImage>();
    public DbSet<Court> Courts => Set<Court>();
    public DbSet<SlotBlock> SlotBlocks => Set<SlotBlock>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        /* ---------------- Users ---------------- */
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        /* ---------------- Futsals ---------------- */
        modelBuilder.Entity<Futsal>(entity =>
        {
            entity.HasOne(f => f.Owner)
                .WithOne(u => u.Futsal)
                .HasForeignKey<Futsal>(f => f.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(f => f.OwnerId).IsUnique();
            entity.HasIndex(f => f.City);
            entity.HasIndex(f => new { f.IsActive, f.IsProfileComplete });
        });

        /* ---------------- Futsal images ---------------- */
        modelBuilder.Entity<FutsalImage>(entity =>
        {
            entity.HasOne(i => i.Futsal)
                .WithMany(f => f.Images)
                .HasForeignKey(i => i.FutsalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(i => new { i.FutsalId, i.SortOrder });
        });

        /* ---------------- Courts ---------------- */
        modelBuilder.Entity<Court>(entity =>
        {
            entity.HasOne(c => c.Futsal)
                .WithMany(f => f.Courts)
                .HasForeignKey(c => c.FutsalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(c => c.PricePerHour).HasPrecision(10, 2);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Courts_SlotDuration", "\"SlotDurationMinutes\" > 0");
                t.HasCheckConstraint("CK_Courts_Hours", "\"ClosingTime\" > \"OpeningTime\"");
                t.HasCheckConstraint("CK_Courts_Price", "\"PricePerHour\" >= 0");
            });
        });

        /* ---------------- Slot blocks ---------------- */
        modelBuilder.Entity<SlotBlock>(entity =>
        {
            entity.HasOne(s => s.Court)
                .WithMany(c => c.SlotBlocks)
                .HasForeignKey(s => s.CourtId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(s => new { s.CourtId, s.Date });
        });

        /* ---------------- Bookings ---------------- */
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasOne(b => b.Court)
                .WithMany(c => c.Bookings)
                .HasForeignKey(b => b.CourtId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(b => b.PaymentMethod).HasConversion<string>().HasMaxLength(20);
            entity.Property(b => b.PaymentStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(b => b.BookingStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(b => b.Source).HasConversion<string>().HasMaxLength(20);
            entity.Property(b => b.TotalAmount).HasPrecision(10, 2);

            entity.HasIndex(b => b.ReferenceCode)
                .IsUnique()
                .HasDatabaseName(ReferenceCodeIndexName);

            // Race-condition guard: only one non-cancelled booking per court/date/start time
            entity.HasIndex(b => new { b.CourtId, b.BookingDate, b.StartTime })
                .IsUnique()
                .HasFilter("\"BookingStatus\" <> 'Cancelled'")
                .HasDatabaseName(ActiveSlotIndexName);

            entity.HasIndex(b => b.BookingDate);
            entity.HasIndex(b => b.PlayerContact);

            entity.ToTable(t => t.HasCheckConstraint("CK_Bookings_Times", "\"EndTime\" > \"StartTime\""));
        });

        /* ---------------- Payments ---------------- */
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasOne(p => p.Booking)
                .WithMany(b => b.Payments)
                .HasForeignKey(p => p.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(p => p.Method).HasConversion<string>().HasMaxLength(20);
            entity.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(p => p.Amount).HasPrecision(10, 2);
            entity.Property(p => p.RawGatewayResponse).HasColumnType("text");

            entity.HasIndex(p => p.GatewayTransactionUuid);
            entity.HasIndex(p => p.CreatedAt);
        });

        /* ---------------- Reviews ---------------- */
        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasOne(r => r.Futsal)
                .WithMany(f => f.Reviews)
                .HasForeignKey(r => r.FutsalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Booking)
                .WithOne(b => b.Review)
                .HasForeignKey<Review>(r => r.BookingId)
                .OnDelete(DeleteBehavior.SetNull);

            // One review per booking
            entity.HasIndex(r => r.BookingId)
                .IsUnique()
                .HasFilter("\"BookingId\" IS NOT NULL");

            entity.HasIndex(r => new { r.FutsalId, r.CreatedAt });

            entity.ToTable(t => t.HasCheckConstraint("CK_Reviews_Rating", "\"Rating\" BETWEEN 1 AND 5"));
        });
    }
}