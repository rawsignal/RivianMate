using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RivianMate.Infrastructure.Data;

/// <summary>
/// Factory for creating DbContext at design time (for migrations).
/// Always uses PostgreSQL to ensure migrations are compatible with production.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RivianMateDbContext>
{
    public RivianMateDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<RivianMateDbContext>();

        // Use a dummy PostgreSQL connection string for design-time
        // This ensures migrations are generated with PostgreSQL types
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=rivianmate_design;Username=postgres;Password=postgres",
            npgsqlOptions => npgsqlOptions.MigrationsAssembly("RivianMate.Infrastructure"));

        return new RivianMateDbContext(optionsBuilder.Options);
    }
}
