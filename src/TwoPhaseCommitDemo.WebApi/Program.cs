using Microsoft.EntityFrameworkCore;
using TwoPhaseCommitDemo.Application.Extensions;
using TwoPhaseCommitDemo.Infrastructure.Data;
using TwoPhaseCommitDemo.Infrastructure.Extensions;
using TwoPhaseCommitDemo.WebApi.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Controllers tradicionais ASP.NET Core (sem Minimal APIs)
builder.Services.AddControllers();

// Global Exception Handling & ProblemDetails
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Swagger e OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Injeção de dependências no padrão ServicesExtensions por camada
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

// Pipeline de Middleware: Tratamento global de exceções no topo
app.UseExceptionHandler();

// Inicialização e validação das tabelas do PostgreSQL com retry amigável
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PostgresDbContext>>();

    for (var attempt = 1; attempt <= 10; attempt++)
    {
        try
        {
            await using var dbContext = await contextFactory.CreateDbContextAsync();
            await dbContext.Database.EnsureCreatedAsync();
            logger.LogInformation("Conexão e esquema do PostgreSQL validados com sucesso.");
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Tentativa {Attempt}/10 de conectar ao PostgreSQL falhou: {Message}. Aguardando 2s...", attempt, ex.Message);
            if (attempt == 10)
            {
                logger.LogError("Não foi possível conectar ao PostgreSQL após 10 tentativas. Aguardando chamadas da API.");
                break;
            }
            await Task.Delay(2000);
        }
    }
}

// Configuração de Documentação (Swagger)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "2PC Demo API v1");
    c.RoutePrefix = string.Empty; // Swagger na raiz (http://localhost:5000/)
});

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.Run();
