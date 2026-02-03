using CartonCaps.Referrals.Domain.Referrals;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CartonCaps.Referrals.Application.Abstractions;

public interface IReferralRepository
{
    Task AddAsync(Referral referral, CancellationToken ct);
    Task<Referral?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Referral?> GetByTokenAsync(string token, CancellationToken ct);
    Task<IReadOnlyList<Referral>> ListByReferrerAsync(Guid referrerUserId, ReferralStatus? status, int skip, int take, CancellationToken ct);
    Task<int> CountByReferrerAsync(Guid referrerUserId, ReferralStatus? status, CancellationToken ct);
    Task<int> CountCreatedInWindowAsync(Guid referrerUserId, TimeSpan window, CancellationToken ct);
    Task SaveAsync(Referral referral, CancellationToken ct);
    Task<bool> ExistsDuplicateAsync( Guid referrerUserId, string contactType, string normalizedContactValue, TimeSpan window, CancellationToken ct);

}
