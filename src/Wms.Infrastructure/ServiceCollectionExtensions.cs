using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wms.Domain.Queries;
using Wms.Infrastructure.Seeding;

namespace Wms.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the database and the SQLite-backed <see cref="IInventoryQueries"/>.</summary>
    public static IServiceCollection AddWmsInventory(this IServiceCollection services, Action<DbContextOptionsBuilder> configureDb)
    {
        services.AddDbContext<WmsDbContext>(configureDb);
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IInventoryQueries, InventoryQueries>();
        return services;
    }

    /// <summary>
    /// Drops, recreates and seeds the database. Seed dates are relative to today, so a database
    /// seeded yesterday is wrong today; the data is read-only, so nothing is lost.
    /// </summary>
    public static void RecreateWmsDatabase(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        WmsSeeder.Seed(db, scope.ServiceProvider.GetRequiredService<TimeProvider>());
    }
}
