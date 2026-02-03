using CartonCaps.Referrals.Application.Abstractions;
using CartonCaps.Referrals.Domain.Referrals;
using CartonCaps.Referrals.Infrastructure.Persistence.EF;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CartonCaps.Referrals.Tests;

public sealed class EfReferralRepositoryTests
{
    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now) => UtcNow = now;
        public DateTimeOffset UtcNow { get; }
    }

    private static ReferralsDbContext NewDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<ReferralsDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new ReferralsDbContext(options);
    }

    [Fact]
    public async Task AddAsync_then_GetByIdAsync_returns_entity()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        using var db = NewDb(Guid.NewGuid().ToString());
        var repo = new EfReferralRepository(db, clock);

        var referral = Referral.Create(Guid.NewGuid(), "ABC12345", "sms", "+1", "text", "tok1", clock.UtcNow);

        await repo.AddAsync(referral, CancellationToken.None);

        var loaded = await repo.GetByIdAsync(referral.Id, CancellationToken.None);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(referral.Id);
    }

    [Fact]
    public async Task AddAsync_then_GetByTokenAsync_returns_entity()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        using var db = NewDb(Guid.NewGuid().ToString());
        var repo = new EfReferralRepository(db, clock);

        var referral = Referral.Create(Guid.NewGuid(), "ABC12345", "email", "a@b.com", "share_sheet", "Tok-ABC", clock.UtcNow);

        await repo.AddAsync(referral, CancellationToken.None);

        var loaded = await repo.GetByTokenAsync("Tok-ABC", CancellationToken.None);

        loaded.Should().NotBeNull();
        loaded!.LinkToken.Should().Be("Tok-ABC");
    }

    [Fact]
    public async Task ExistsDuplicateAsync_returns_true_when_duplicate_within_window()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new FixedClock(now);
        using var db = NewDb(Guid.NewGuid().ToString());
        var repo = new EfReferralRepository(db, clock);

        var userId = Guid.NewGuid();
        var referral = Referral.Create(userId, "ABC12345", "email", "dup@example.com", "email", "t1", now.AddHours(-1));

        await repo.AddAsync(referral, CancellationToken.None);

        var exists = await repo.ExistsDuplicateAsync(
            userId,
            "email",
            "dup@example.com",
            TimeSpan.FromHours(24),
            CancellationToken.None);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task CountCreatedInWindowAsync_counts_recent_only()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new FixedClock(now);
        using var db = NewDb(Guid.NewGuid().ToString());
        var repo = new EfReferralRepository(db, clock);

        var userId = Guid.NewGuid();
        var recent = Referral.Create(userId, "ABC12345", "sms", "+1", "text", "t1", now.AddMinutes(-2));
        var old = Referral.Create(userId, "ABC12345", "sms", "+2", "text", "t2", now.AddHours(-2));

        await repo.AddAsync(recent, CancellationToken.None);
        await repo.AddAsync(old, CancellationToken.None);

        var count = await repo.CountCreatedInWindowAsync(userId, TimeSpan.FromMinutes(10), CancellationToken.None);

        count.Should().Be(1);
    }
}
