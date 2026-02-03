using System;

namespace CartonCaps.Referrals.Application.Abstractions;

public interface ICurrentUserContext
{
    Guid UserId { get; }
    string ReferralCode { get; }
}
