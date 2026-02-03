using CartonCaps.Referrals.Infrastructure.Persistence.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;

namespace CartonCaps.Referrals.Infrastructure.Persistence.Ef;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ReferralsDbContext>
{
    public ReferralsDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("REFERRALS_DB_CONNECTION")
            ?? "Server=(localdb)\\mssqllocaldb;Database=CartonCaps.Referrals;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

        DbContextOptions<ReferralsDbContext> dbContextOptions =
            new DbContextOptionsBuilder<ReferralsDbContext>()
                .UseSqlServer(connectionString)
                .Options;

        return new ReferralsDbContext(dbContextOptions);
    }
}
