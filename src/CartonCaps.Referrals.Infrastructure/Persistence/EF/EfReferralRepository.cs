using CartonCaps.Referrals.Application.Abstractions;
using CartonCaps.Referrals.Domain.Referrals;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CartonCaps.Referrals.Infrastructure.Persistence.EF;

public sealed class EfReferralRepository : IReferralRepository
{
    private readonly ReferralsDbContext _referralsDbContext;
    private readonly IClock _clock;

    public EfReferralRepository(ReferralsDbContext referralsDbContext, IClock clock)
    {
        _referralsDbContext = referralsDbContext;
        _clock = clock;
    }

    public async Task AddAsync(Referral referral, CancellationToken cancellationToken)
    {
        _referralsDbContext.Referrals.Add(referral);
        await _referralsDbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(Referral referral, CancellationToken cancellationToken) => _referralsDbContext.SaveChangesAsync(cancellationToken);

    public Task<Referral?> GetByIdAsync(Guid referralId, CancellationToken cancellationToken) => _referralsDbContext.Referrals.FirstOrDefaultAsync(referral => referral.Id == referralId, cancellationToken);

    public Task<Referral?> GetByTokenAsync(string token, CancellationToken cancellationToken) => _referralsDbContext.Referrals.FirstOrDefaultAsync(referral => referral.LinkToken == token, cancellationToken);

    public async Task<IReadOnlyList<Referral>> ListByReferrerAsync(Guid referrerUserId, ReferralStatus? status, int skip, int take, CancellationToken cancellationToken)
    {
        int skipCount = Math.Max(0, skip);
        int pageSize = take <= 0 ? 25 : Math.Min(take, 200);

        IQueryable<Referral> referralsQuery = _referralsDbContext.Referrals.AsNoTracking().Where(referral => referral.ReferrerUserId == referrerUserId);

        if (status is not null)
            referralsQuery = referralsQuery.Where(referral => referral.Status == status);

        return await referralsQuery
            .OrderByDescending(referral => referral.CreatedAt)
            .Skip(skipCount)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountByReferrerAsync(Guid referrerUserId, ReferralStatus? status, CancellationToken cancellationToken)
    {
        IQueryable<Referral> referralsQuery = _referralsDbContext.Referrals.Where(referral => referral.ReferrerUserId == referrerUserId);
        if (status is not null) referralsQuery = referralsQuery.Where(referral => referral.Status == status);
        return referralsQuery.CountAsync(cancellationToken);
    }

    public Task<int> CountCreatedInWindowAsync(Guid referrerUserId, TimeSpan window, CancellationToken cancellationToken)
    {
        DateTimeOffset cutoffTimestamp = _clock.UtcNow.Subtract(window);
        return _referralsDbContext.Referrals.CountAsync(referral => referral.ReferrerUserId == referrerUserId && referral.CreatedAt >= cutoffTimestamp, cancellationToken);
    }

    public Task<bool> ExistsDuplicateAsync(Guid referrerUserId, string contactType, string normalizedContactValue, TimeSpan window, CancellationToken cancellationToken)
    {
        DateTimeOffset cutoffTimestamp = _clock.UtcNow.Subtract(window);

        return _referralsDbContext.Referrals.AsNoTracking().AnyAsync(referral =>
            referral.ReferrerUserId == referrerUserId &&
            referral.ContactType == contactType &&
            referral.ContactValue == normalizedContactValue &&
            referral.CreatedAt >= cutoffTimestamp, cancellationToken);
    }
}
