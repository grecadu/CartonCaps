using CartonCaps.Referrals.Domain.Referrals;
using System;
using System.Collections.Generic;

namespace CartonCaps.Referrals.Application.Contracts;

public sealed record CreateReferralRequest(string ContactType, string ContactValue, string Channel);

public sealed record CreateReferralResponse(Guid ReferralId, ReferralStatus Status, string ShareMessage, Uri ShareUrl, string Token, DateTimeOffset CreatedAt);

public sealed record ReferralSummaryDto(Guid ReferralId, string ContactType, string ContactValue, string Channel, ReferralStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? LastUpdatedAt);

public sealed record ReferralListResponse(int TotalCount, int SkipCount, int PageSize, IReadOnlyList<ReferralSummaryDto> Referrals);

public sealed record ResolveReferralResponse(bool IsReferred, string? ReferralCode, string? Token, string? OnboardingVariant);
