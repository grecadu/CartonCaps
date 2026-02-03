using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CartonCaps.Referrals.Application.Abstractions;
using CartonCaps.Referrals.Domain.Referrals;

namespace CartonCaps.Referrals.Infrastructure.Persistence;

public sealed class InMemoryReferralRepository : IReferralRepository
{
    private readonly ConcurrentDictionary<Guid, Referral> _referrals = new();
    private readonly IClock _clock;

    public InMemoryReferralRepository(IClock clock) => _clock = clock;

    public Task AddAsync(Referral referral, CancellationToken cancellationToken)
    {
        _referrals[referral.Id] = referral;
        return Task.CompletedTask;
    }

    public Task SaveAsync(Referral referral, CancellationToken cancellationToken)
    {
        _referrals[referral.Id] = referral;
        return Task.CompletedTask;
    }

    public Task<Referral?> GetByIdAsync(Guid referralId, CancellationToken cancellationToken)
    {
        _referrals.TryGetValue(referralId, out Referral? foundReferral);
        return Task.FromResult(foundReferral);
    }

    public Task<Referral?> GetByTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Task.FromResult<Referral?>(null);

        Referral? matchingReferral = _referrals.Values.FirstOrDefault(referral =>
            string.Equals(referral.LinkToken, token, StringComparison.Ordinal));

        return Task.FromResult(matchingReferral);
    }

    public Task<IReadOnlyList<Referral>> ListByReferrerAsync(Guid referrerUserId, ReferralStatus? status, int skip, int take, CancellationToken cancellationToken)
    {
        int skipCount = Math.Max(0, skip);
        int pageSize = take <= 0 ? 25 : Math.Min(take, 200);

        IEnumerable<Referral> referralsQuery = _referrals.Values.Where(referral => referral.ReferrerUserId == referrerUserId);

        if (status is not null)
            referralsQuery = referralsQuery.Where(referral => referral.Status == status);

        IReadOnlyList<Referral> referrals = referralsQuery
            .OrderByDescending(referral => referral.CreatedAt)
            .Skip(skipCount)
            .Take(pageSize)
            .ToList()
            .AsReadOnly();

        return Task.FromResult(referrals);
    }

    public Task<int> CountByReferrerAsync(Guid referrerUserId, ReferralStatus? status, CancellationToken cancellationToken)
    {
        IEnumerable<Referral> referralsQuery = _referrals.Values.Where(referral => referral.ReferrerUserId == referrerUserId);
        if (status is not null) referralsQuery = referralsQuery.Where(referral => referral.Status == status);
        return Task.FromResult(referralsQuery.Count());
    }

    public Task<int> CountCreatedInWindowAsync(Guid referrerUserId, TimeSpan window, CancellationToken cancellationToken)
    {
        DateTimeOffset cutoffTimestamp = _clock.UtcNow.Subtract(window);

        int createdCount = _referrals.Values.Count(referral =>
            referral.ReferrerUserId == referrerUserId &&
            referral.CreatedAt >= cutoffTimestamp);

        return Task.FromResult(createdCount);
    }

    public Task<bool> ExistsDuplicateAsync(Guid referrerUserId, string contactType, string normalizedContactValue, TimeSpan window, CancellationToken cancellationToken)
    {
        DateTimeOffset cutoffTimestamp = _clock.UtcNow.Subtract(window);

        bool existsDuplicate = _referrals.Values.Any(referral =>
            referral.ReferrerUserId == referrerUserId &&
            string.Equals(referral.ContactType, contactType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(referral.ContactValue, normalizedContactValue, StringComparison.OrdinalIgnoreCase) &&
            referral.CreatedAt >= cutoffTimestamp);

        return Task.FromResult(existsDuplicate);
    }
}
