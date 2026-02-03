using CartonCaps.Referrals.Application.Abstractions;
using CartonCaps.Referrals.Domain.Referrals;
using CartonCaps.Referrals.Infrastructure.Persistence;
using FluentAssertions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CartonCaps.Referrals.Tests;

public sealed class InMemoryReferralRepositoryTests
{
    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now) => UtcNow = now;
        public DateTimeOffset UtcNow { get; private set; }
    }

    [Fact]
    public async Task AddAsync_then_GetByIdAsync_returns_entity()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var repo = new InMemoryReferralRepository(clock);

        var referral = Referral.Create(
            Guid.NewGuid(),
            "ABC12345",
            "sms",
            "+15555550123",
            "text",
            "tok1",
            clock.UtcNow);

        await repo.AddAsync(referral, CancellationToken.None);

        var loaded = await repo.GetByIdAsync(referral.Id, CancellationToken.None);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(referral.Id);
        loaded.LinkToken.Should().Be("tok1");
    }

    [Fact]
    public async Task AddAsync_then_GetByTokenAsync_returns_entity()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var repo = new InMemoryReferralRepository(clock);

        var referral = Referral.Create(
            Guid.NewGuid(),
            "ABC12345",
            "email",
            "friend@example.com",
            "share_sheet",
            "ToK-XYZ",
            clock.UtcNow);

        await repo.AddAsync(referral, CancellationToken.None);

        var loaded = await repo.GetByTokenAsync("ToK-XYZ", CancellationToken.None);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(referral.Id);
    }


    [Fact]
    public async Task ListByReferrerAsync_orders_by_CreatedAt_desc_and_applies_skip_take()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var repo = new InMemoryReferralRepository(clock);
        var userId = Guid.NewGuid();

        var r1 = Referral.Create(userId, "ABC12345", "sms", "+1", "text", "t1", DateTimeOffset.UtcNow.AddMinutes(-10));
        var r2 = Referral.Create(userId, "ABC12345", "sms", "+2", "text", "t2", DateTimeOffset.UtcNow.AddMinutes(-5));
        var r3 = Referral.Create(userId, "ABC12345", "sms", "+3", "text", "t3", DateTimeOffset.UtcNow.AddMinutes(-1));

        await repo.AddAsync(r1, CancellationToken.None);
        await repo.AddAsync(r2, CancellationToken.None);
        await repo.AddAsync(r3, CancellationToken.None);

        var page = await repo.ListByReferrerAsync(userId, status: null, skip: 1, take: 1, CancellationToken.None);

        page.Should().HaveCount(1);
        page[0].LinkToken.Should().Be("t2");
    }

    [Fact]
    public async Task ExistsDuplicateAsync_returns_true_when_same_user_contactType_contactValue_within_window()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new FixedClock(now);
        var repo = new InMemoryReferralRepository(clock);

        var userId = Guid.NewGuid();

        var r1 = Referral.Create(userId, "ABC12345", "email", "dup@example.com", "email", "t1", now.AddHours(-1));
        await repo.AddAsync(r1, CancellationToken.None);

        var exists = await repo.ExistsDuplicateAsync(
            userId,
            "email",
            "dup@example.com",
            TimeSpan.FromHours(24),
            CancellationToken.None);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task CountCreatedInWindowAsync_counts_only_recent()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var repo = new InMemoryReferralRepository(clock);
        var userId = Guid.NewGuid();

        var recent = Referral.Create(userId, "ABC12345", "sms", "+1", "text", "t1", DateTimeOffset.UtcNow.AddMinutes(-2));
        var old = Referral.Create(userId, "ABC12345", "sms", "+2", "text", "t2", DateTimeOffset.UtcNow.AddHours(-2));

        await repo.AddAsync(recent, CancellationToken.None);
        await repo.AddAsync(old, CancellationToken.None);

        var count = await repo.CountCreatedInWindowAsync(userId, TimeSpan.FromMinutes(10), CancellationToken.None);

        count.Should().Be(1);
    }
}
