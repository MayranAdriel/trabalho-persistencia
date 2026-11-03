using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using TwoPhaseCommitDemo.Application.Interfaces;
using TwoPhaseCommitDemo.Application.Services;
using TwoPhaseCommitDemo.Domain.Interfaces;
using TwoPhaseCommitDemo.Infrastructure.Data;
using TwoPhaseCommitDemo.Infrastructure.Repositories;
using TwoPhaseCommitDemo.Infrastructure.Services;
using TwoPhaseCommitDemo.Infrastructure.Transactions;

namespace TwoPhaseCommitDemo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureAndApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ==========================================
        // 1. PostgreSQL (Entity Framework Core)
        // ==========================================
        var pgConnectionString = configuration.GetConnectionString("PostgreSql")
            ?? "Host=localhost;Port=5432;Database=twophasecommit_db;Username=postgres;Password=postgres";

        services.AddDbContextFactory<PostgresDbContext>(options =>
        {
            options.UseNpgsql(pgConnectionString);
        });

        // ==========================================
        // 2. MongoDB
        // ==========================================
        var mongoConnectionString = configuration.GetConnectionString("MongoDb")
            ?? "mongodb://localhost:27017/?replicaSet=rs0&directConnection=true";

        var mongoDatabaseName = configuration["MongoDatabaseName"]
            ?? "twophasecommit_db";

        services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
        services.AddSingleton<MongoDbContext>(sp =>
        {
            var client = sp.GetRequiredService<IMongoClient>();
            return new MongoDbContext(client, mongoDatabaseName);
        });

        // ==========================================
        // 3. Repositories
        // ==========================================
        services.AddScoped<IProductPostgresRepository, ProductPostgresRepository>();
        services.AddScoped<IProductMongoRepository, ProductMongoRepository>();

        // ==========================================
        // 4. Two Phase Commit Coordinator
        // ==========================================
        services.AddScoped<ITwoPhaseCommitCoordinator, TwoPhaseCommitCoordinator>();

        // ==========================================
        // 5. Application Services
        // ==========================================
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IScenario1Service, Scenario1Service>();
        services.AddScoped<IScenario2Service, Scenario2Service>();
        services.AddScoped<IScenario3Service, Scenario3Service>();

        // ==========================================
        // 6. Consistency Monitor (Background & On-Demand)
        // ==========================================
        services.AddSingleton<ConsistencyMonitorService>();
        services.AddSingleton<IConsistencyMonitorService>(sp => sp.GetRequiredService<ConsistencyMonitorService>());
        services.AddHostedService(sp => sp.GetRequiredService<ConsistencyMonitorService>());

        return services;
    }
}
