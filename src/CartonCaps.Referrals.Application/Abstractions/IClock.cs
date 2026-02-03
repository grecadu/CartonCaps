using System;

namespace CartonCaps.Referrals.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
