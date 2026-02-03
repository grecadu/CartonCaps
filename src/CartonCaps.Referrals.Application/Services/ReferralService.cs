using CartonCaps.Referrals.Application.Abstractions;
using CartonCaps.Referrals.Application.Contracts;
using CartonCaps.Referrals.Domain.Referrals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CartonCaps.Referrals.Application.Services;

public sealed class ReferralService
{
    private readonly IReferralRepository _referralRepository;
    private readonly IReferralLinkService _referralLinkService;
    private readonly IClock _clock;

    private static readonly TimeSpan CreateWindow = TimeSpan.FromMinutes(10);
    private const int MaxCreatesPerWindow = 20;

    public ReferralService(IReferralRepository referralRepository, IReferralLinkService referralLinkService, IClock clock)
    {
        _referralRepository = referralRepository;
        _referralLinkService = referralLinkService;
        _clock = clock;
    }

    public async Task<CreateReferralResponse> CreateAsync(Guid referrerUserId, string referrerReferralCode, CreateReferralRequest request, CancellationToken cancellationToken)
    {
        int createdInWindowCount = await _referralRepository.CountCreatedInWindowAsync(referrerUserId, CreateWindow, cancellationToken);
        if (createdInWindowCount >= MaxCreatesPerWindow)
            throw new ReferralAppException("rate_limited", $"Too many referrals created in the last {CreateWindow.TotalMinutes:0} minutes.");

        (string token, Uri url) = _referralLinkService.GenerateLink(referrerUserId, referrerReferralCode);

        DateTimeOffset now = _clock.UtcNow;

        Referral referral = Referral.Create(
            referrerUserId,
            referrerReferralCode,
            request.ContactType,
            request.ContactValue,
            request.Channel,
            token,
            now);

        referral.MarkSent(now);

        await _referralRepository.AddAsync(referral, cancellationToken);

        string shareMessage = BuildShareMessage(referrerReferralCode, url);

        return new CreateReferralResponse(
            ReferralId: referral.Id,
            Status: referral.Status,
            ShareMessage: shareMessage,
            ShareUrl: url,
            Token: token,
            CreatedAt: referral.CreatedAt);
    }

    public async Task<ReferralListResponse> ListAsync(Guid referrerUserId, ReferralStatus? status, int skip, int take, CancellationToken cancellationToken)
    {
        int pageSize = take <= 0 ? 25 : Math.Min(take, 100);
        int skipCount = Math.Max(0, skip);

        int totalCount = await _referralRepository.CountByReferrerAsync(referrerUserId, status, cancellationToken);
        IReadOnlyList<Referral> referrals = await _referralRepository.ListByReferrerAsync(referrerUserId, status, skipCount, pageSize, cancellationToken);

        List<ReferralSummaryDto> referralSummaries = referrals.Select(ToSummary).ToList();

        return new ReferralListResponse(TotalCount: totalCount, SkipCount: skipCount, PageSize: pageSize, Referrals: referralSummaries);
    }

    public async Task<ReferralSummaryDto?> GetAsync(Guid referrerUserId, Guid referralId, CancellationToken cancellationToken)
    {
        Referral? referral = await _referralRepository.GetByIdAsync(referralId, cancellationToken);
        if (referral is null) return null;
        if (referral.ReferrerUserId != referrerUserId) return null;
        return ToSummary(referral);
    }

    public async Task<ResolveReferralResponse> ResolveAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new ResolveReferralResponse(false, null, null, null);

        string trimmedToken = token.Trim();

        Referral? referral = await _referralRepository.GetByTokenAsync(trimmedToken, cancellationToken);
        if (referral is null)
            return new ResolveReferralResponse(false, null, trimmedToken, null);

        referral.MarkOpened(_clock.UtcNow);
        await _referralRepository.SaveAsync(referral, cancellationToken);

        return new ResolveReferralResponse(
            IsReferred: true,
            ReferralCode: referral.ReferrerReferralCode,
            Token: referral.LinkToken,
            OnboardingVariant: "referal");
    }

    public async Task TrackEventAsync(Guid referrerUserId, Guid referralId, ReferralEventType referralEventType, CancellationToken cancellationToken)
    {
        Referral? referral = await _referralRepository.GetByIdAsync(referralId, cancellationToken);
        if (referral is null) throw new ReferralAppException("not_found", "Referral not found.");
        if (referral.ReferrerUserId != referrerUserId) throw new ReferralAppException("forbidden", "Referral does not belong to the current user.");

        DateTimeOffset now = _clock.UtcNow;

        switch (referralEventType)
        {
            case ReferralEventType.Sent: referral.MarkSent(now); break;
            case ReferralEventType.Opened: referral.MarkOpened(now); break;
            case ReferralEventType.Installed: referral.MarkInstalled(now); break;
            case ReferralEventType.Registered: referral.MarkRegistered(now); break;
            case ReferralEventType.Cancelled: referral.Cancel(now); break;
            default: throw new ReferralAppException("invalid_event", "Unsupported referral event.");
        }

        await _referralRepository.SaveAsync(referral, cancellationToken);
    }

    private static ReferralSummaryDto ToSummary(Referral referral) => new(
        ReferralId: referral.Id,
        ContactType: referral.ContactType,
        ContactValue: referral.ContactValue,
        Channel: referral.Channel,
        Status: referral.Status,
        CreatedAt: referral.CreatedAt,
        LastUpdatedAt: referral.LastUpdatedAt);

    private static string BuildShareMessage(string referralCode, Uri url) => $"Join me on Carton Caps! Use my referral code {referralCode} or tap this link: {url}";

    public sealed class ReferralAppException : Exception
    {
        public string Code { get; }

        public ReferralAppException(string code, string message) : base(message) => Code = code;
    }
}
