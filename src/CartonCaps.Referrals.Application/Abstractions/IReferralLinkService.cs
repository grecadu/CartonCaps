using System;

namespace CartonCaps.Referrals.Application.Abstractions;

public interface IReferralLinkService
{
    (string Token, Uri Url) GenerateLink(Guid referrerUserId, string referralCode);
}
