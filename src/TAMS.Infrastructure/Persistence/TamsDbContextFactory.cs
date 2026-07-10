using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TAMS.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so EF Core tooling (migrations) can construct the context
/// without booting the full application host. Uses a LocalDB connection by default;
/// override via the TAMS_DESIGN_CONNECTION environment variable. (04 §14.)
/// </summary>
public sealed class TamsDbContextFactory : IDesignTimeDbContextFactory<TamsDbContext>
{
    public TamsDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("TAMS_DESIGN_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=TAMS;Trusted_Connection=True;TrustServerCertificate=true";

        var options = new DbContextOptionsBuilder<TamsDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsAssembly(typeof(TamsDbContext).Assembly.FullName))
            .Options;

        return new TamsDbContext(options);
    }
}
