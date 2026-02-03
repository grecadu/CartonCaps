using CartonCaps.Referrals.Infrastructure.Persistence.Ef;
using FluentAssertions;
using System;
using Xunit;

namespace CartonCaps.Referrals.Tests;

public sealed class DesignTimeDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_returns_context()
    {
        Environment.SetEnvironmentVariable("REFERRALS_DB_CONNECTION",
            "Server=(localdb)\\mssqllocaldb;Database=CartonCaps.Referrals;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False");

        var factory = new DesignTimeDbContextFactory();

        var ctx = factory.CreateDbContext(Array.Empty<string>());

        ctx.Should().NotBeNull();
    }
}
