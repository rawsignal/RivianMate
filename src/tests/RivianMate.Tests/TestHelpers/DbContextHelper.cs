using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Tests.TestHelpers;

/// <summary>
/// Helper for creating in-memory database contexts for testing.
/// </summary>
public static class DbContextHelper
{
    /// <summary>
    /// Creates an in-memory RivianMateDbContext with a unique database name.
    /// </summary>
    public static RivianMateDbContext CreateInMemory(string? dbName = null)
    {
        var name = dbName ?? Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<RivianMateDbContext>()
            .UseInMemoryDatabase(name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var db = new RivianMateDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    /// <summary>
    /// Creates a factory that returns in-memory contexts sharing the same database.
    /// </summary>
    public static IDbContextFactory<RivianMateDbContext> CreateFactory(string? dbName = null)
    {
        var name = dbName ?? Guid.NewGuid().ToString();
        return new InMemoryDbContextFactory(name);
    }

    private class InMemoryDbContextFactory : IDbContextFactory<RivianMateDbContext>
    {
        private readonly string _dbName;

        public InMemoryDbContextFactory(string dbName)
        {
            _dbName = dbName;
        }

        public RivianMateDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<RivianMateDbContext>()
                .UseInMemoryDatabase(_dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new RivianMateDbContext(options);
        }
    }
}
