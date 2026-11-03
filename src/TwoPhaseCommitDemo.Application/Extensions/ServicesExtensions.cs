using Microsoft.Extensions.DependencyInjection;
using TwoPhaseCommitDemo.Application.Interfaces;
using TwoPhaseCommitDemo.Application.Services;

namespace TwoPhaseCommitDemo.Application.Extensions;

public static class ServicesExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IScenario1Service, Scenario1Service>();
        services.AddScoped<IScenario2Service, Scenario2Service>();
        services.AddScoped<IScenario3Service, Scenario3Service>();

        return services;
    }
}

