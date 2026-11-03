using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using TwoPhaseCommitDemo.Application.Interfaces;
using TwoPhaseCommitDemo.Domain.Interfaces;
using TwoPhaseCommitDemo.Infrastructure.Data;
using TwoPhaseCommitDemo.Infrastructure.Repositories;
using TwoPhaseCommitDemo.Infrastructure.Services;
using TwoPhaseCommitDemo.Infrastructure.Transactions;

namespace TwoPhaseCommitDemo.Infrastructure.Extensions;

public static class ServicesExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDatabaseServices(configuration);
        services.AddRepositories();
        services.AddTwoPhaseCommitServices();
        services.AddMonitoringServices();

        return services;
    }

    public static IServiceCollection AddDatabaseServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. PostgreSQL (EF Core DbContextFactory)
        var pgConnectionString = configuration.GetConnectionString("PostgreSql")
            ?? "Host=localhost;Port=5432;Database=twophasecommit_db;Username=postgres;Password=postgres";

        services.AddDbContextFactory<PostgresDbContext>(options =>
        {
            options.UseNpgsql(pgConnectionString);
        });

        // 2. MongoDB
        var mongoConnectionString = configuration.GetConnectionString("MongoDb")
            ?? "mongodb://localhost:27017/?replicaSet=rs0&directConnection=true";

        var mongoDatabaseName = configuration["MongoDatabaseName"]
            ?? "twophasecommit_db";

        services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
        services.AddSingleton<MongoDbContext>(sp =>
        {
            var client = sp.GetRequiredService<IMongoClient>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MongoDbContext>>();
            return new MongoDbContext(client, mongoDatabaseName, logger);
        });

        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IProductPostgresRepository, ProductPostgresRepository>();
        services.AddScoped<IProductMongoRepository, ProductMongoRepository>();

        return services;
    }

    public static IServiceCollection AddTwoPhaseCommitServices(this IServiceCollection services)
    {
        services.AddScoped<ITwoPhaseCommitCoordinator, TwoPhaseCommitCoordinator>();

        return services;
    }

    public static IServiceCollection AddMonitoringServices(this IServiceCollection services)
    {
        services.AddSingleton<ConsistencyMonitorService>();
        services.AddSingleton<IConsistencyMonitorService>(sp => sp.GetRequiredService<ConsistencyMonitorService>());
        services.AddHostedService(sp => sp.GetRequiredService<ConsistencyMonitorService>());

        return services;
    }
}
