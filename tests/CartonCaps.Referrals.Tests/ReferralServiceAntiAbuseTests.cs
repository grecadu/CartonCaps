using CartonCaps.Referrals.Application.Abstractions;
using CartonCaps.Referrals.Application.Contracts;
using CartonCaps.Referrals.Application.Services;
using CartonCaps.Referrals.Domain.Referrals;
using CartonCaps.Referrals.Infrastructure.Links;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CartonCaps.Referrals.Tests;

public sealed class ReferralServiceAntiAbuseTests
{
    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now) => UtcNow = now;
        public DateTimeOffset UtcNow { get; private set; }
        public void Set(DateTimeOffset now) => UtcNow = now;
    }

    private sealed class StubRepo : IReferralRepository
    {
        public int CreatedInWindow { get; set; }
        public bool HasDuplicate { get; set; }
        public Referral? Stored { get; private set; }

        public Task AddAsync(Referral referral, CancellationToken ct) { Stored = referral; return Task.CompletedTask; }
        public Task<Referral?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Referral?>(Stored?.Id == id ? Stored : null);
        public Task<Referral?> GetByTokenAsync(string token, CancellationToken ct) => Task.FromResult<Referral?>(Stored?.LinkToken.Equals(token, StringComparison.OrdinalIgnoreCase) == true ? Stored : null);
        public Task<IReadOnlyList<Referral>> ListByReferrerAsync(Guid referrerUserId, ReferralStatus? status, int skip, int take, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Referral>>(Array.Empty<Referral>());
        public Task<int> CountByReferrerAsync(Guid referrerUserId, ReferralStatus? status, CancellationToken ct) => Task.FromResult(0);
        public Task<int> CountCreatedInWindowAsync(Guid referrerUserId, TimeSpan window, CancellationToken ct) => Task.FromResult(CreatedInWindow);
        public Task<bool> ExistsDuplicateAsync(Guid referrerUserId, string contactType, string contactValue, TimeSpan window, CancellationToken ct) => Task.FromResult(HasDuplicate);
        public Task SaveAsync(Referral referral, CancellationToken ct) { Stored = referral; return Task.CompletedTask; }
    }

    [Fact]
    public async Task CreateAsync_when_rate_limited_throws()
    {
        var repo = new StubRepo { CreatedInWindow = 20, HasDuplicate = false };
        var links = new SimpleReferralLinkService(new Uri("http://localhost:5085"));
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var svc = new ReferralService(repo, links, clock);

        var act = async () => await svc.CreateAsync(
            Guid.NewGuid(),
            "ABC12345",
            new CreateReferralRequest("sms", "+15555550123", "text"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ReferralService.ReferralAppException>()
            .Where(e => e.Code == "rate_limited");
    }

    [Fact]
    public async Task CreateAsync_when_duplicate_flag_exists_still_creates_because_service_does_not_check_duplicates()
    {
        var repo = new StubRepo { CreatedInWindow = 0, HasDuplicate = true };
        var links = new SimpleReferralLinkService(new Uri("http://localhost:5085"));
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var svc = new ReferralService(repo, links, clock);

        var res = await svc.CreateAsync(
            Guid.NewGuid(),
            "ABC12345",
            new CreateReferralRequest("email", "dup@example.com", "email"),
            CancellationToken.None);

        res.ReferralId.Should().NotBe(Guid.Empty);
        repo.Stored.Should().NotBeNull();
    }

}
