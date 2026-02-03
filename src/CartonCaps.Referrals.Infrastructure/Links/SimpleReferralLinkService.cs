using System;
using System.Security.Cryptography;
using System.Text;
using CartonCaps.Referrals.Application.Abstractions;

namespace CartonCaps.Referrals.Infrastructure.Links;

public sealed class SimpleReferralLinkService : IReferralLinkService
{
    private readonly Uri _baseUri;

    public SimpleReferralLinkService(Uri baseUri)
    {
        _baseUri = baseUri;
    }

    public (string Token, Uri Url) GenerateLink(Guid referrerUserId, string referralCode)
    {
        var token = CreateToken(referrerUserId, referralCode);
        var url = new Uri(_baseUri, $"/r/{token}");
        return (token, url);
    }

    private static string CreateToken(Guid userId, string referralCode)
    {
        var input = $"{userId:N}:{referralCode}:{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..12];
    }
}
