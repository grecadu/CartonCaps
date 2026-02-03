using CartonCaps.Referrals.Domain.Referrals;
using Microsoft.EntityFrameworkCore;


namespace CartonCaps.Referrals.Infrastructure.Persistence.EF;

public sealed class ReferralsDbContext : DbContext
{
    public ReferralsDbContext(DbContextOptions<ReferralsDbContext> options) : base(options) { }

    public DbSet<Referral> Referrals => Set<Referral>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<Referral>();

        e.ToTable("Referrals");
        e.HasKey(x => x.Id);

        e.Property(x => x.ReferrerUserId).IsRequired();
        e.Property(x => x.ReferrerReferralCode).HasMaxLength(32).IsRequired();

        e.Property(x => x.ContactType).HasMaxLength(16).IsRequired();
        e.Property(x => x.ContactValue).HasMaxLength(256).IsRequired();
        e.Property(x => x.Channel).HasMaxLength(32).IsRequired();

        e.Property(x => x.Status).HasConversion<int>().IsRequired();

        e.Property(x => x.LinkToken).HasMaxLength(128).IsRequired();
        e.HasIndex(x => x.LinkToken).IsUnique();

        e.Property(x => x.CreatedAt).IsRequired();
        e.Property(x => x.LastUpdatedAt);

        e.HasIndex(x => new { x.ReferrerUserId, x.CreatedAt });
        e.HasIndex(x => new { x.ReferrerUserId, x.Status });
    }
}
