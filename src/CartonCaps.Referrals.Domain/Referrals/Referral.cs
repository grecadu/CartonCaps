using System;
using System.Text.RegularExpressions;

namespace CartonCaps.Referrals.Domain.Referrals;


public sealed class Referral
{
    private Referral() { }
    private Referral(Guid id) => Id = id;

    public Guid Id { get; private set; }
    public Guid ReferrerUserId { get; private set; }
    public string ReferrerReferralCode { get; private set; } = string.Empty;

    public string ContactType { get; private set; } = string.Empty;     // "sms" | "email"
    public string ContactValue { get; private set; } = string.Empty;    // phone or email (stored as provided in mock)

    public string Channel { get; private set; } = string.Empty;         // "text" | "email" | "share_sheet"
    public ReferralStatus Status { get; private set; }

    public string LinkToken { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastUpdatedAt { get; private set; }

    public static Referral Create(
        Guid referrerUserId,
        string referrerReferralCode,
        string contactType,
        string contactValue,
        string channel,
        string linkToken,
        DateTimeOffset now)
    {
        if (referrerUserId == Guid.Empty) throw new ArgumentException("Referrer user id is required.", nameof(referrerUserId));
        if (string.IsNullOrWhiteSpace(referrerReferralCode)) throw new ArgumentException("Referral code is required.", nameof(referrerReferralCode));

        if (!IsValidReferralCode(referrerReferralCode))
            throw new ArgumentException("Referral code format is invalid.", nameof(referrerReferralCode));

        if (string.IsNullOrWhiteSpace(contactType)) throw new ArgumentException("Contact type is required.", nameof(contactType));
        if (string.IsNullOrWhiteSpace(contactValue)) throw new ArgumentException("Contact value is required.", nameof(contactValue));
        if (string.IsNullOrWhiteSpace(channel)) throw new ArgumentException("Channel is required.", nameof(channel));
        if (string.IsNullOrWhiteSpace(linkToken)) throw new ArgumentException("Link token is required.", nameof(linkToken));

        var ct = contactType.Trim().ToLowerInvariant();
        if (ct is not ("email" or "sms"))
            throw new ArgumentException("Contact type must be 'email' or 'sms'.", nameof(contactType));

        return new Referral(Guid.NewGuid())
        {
            ReferrerUserId = referrerUserId,
            ReferrerReferralCode = referrerReferralCode.Trim(),
            ContactType = ct,
            ContactValue = contactValue.Trim(),
            Channel = channel.Trim().ToLowerInvariant(),
            LinkToken = linkToken.Trim(),
            Status = ReferralStatus.Created,
            CreatedAt = now,
            LastUpdatedAt = now
        };
    }

    public void MarkSent(DateTimeOffset now)
    {
        if (Status is ReferralStatus.Cancelled) return;
        Status = ReferralStatus.Sent;
        LastUpdatedAt = now;
    }

    public void MarkOpened(DateTimeOffset now)
    {
        if (Status is ReferralStatus.Cancelled) return;
        if (Status < ReferralStatus.Sent) Status = ReferralStatus.Sent;
        Status = ReferralStatus.Opened;
        LastUpdatedAt = now;
    }

    public void MarkInstalled(DateTimeOffset now)
    {
        if (Status is ReferralStatus.Cancelled) return;
        if (Status < ReferralStatus.Opened) Status = ReferralStatus.Opened;
        Status = ReferralStatus.Installed;
        LastUpdatedAt = now;
    }

    public void MarkRegistered(DateTimeOffset now)
    {
        if (Status is ReferralStatus.Cancelled) return;
        if (Status < ReferralStatus.Installed) Status = ReferralStatus.Installed;
        Status = ReferralStatus.Registered;
        LastUpdatedAt = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        Status = ReferralStatus.Cancelled;
        LastUpdatedAt = now;
    }

    private static bool IsValidReferralCode(string code)
    {
        return Regex.IsMatch(code.Trim(), "^[A-Za-z0-9]{6,16}$");
    }
}
