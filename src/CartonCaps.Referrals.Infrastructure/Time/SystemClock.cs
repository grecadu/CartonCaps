using CartonCaps.Referrals.Application.Abstractions;
using System;

namespace CartonCaps.Referrals.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
