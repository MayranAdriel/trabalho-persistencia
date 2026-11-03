using Microsoft.EntityFrameworkCore;
using TwoPhaseCommitDemo.Infrastructure;
using TwoPhaseCommitDemo.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Controllers convencionais (sem Minimal APIs)
builder.Services.AddControllers();

// Swagger e OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registrar Clean Architecture Application & Infrastructure
builder.Services.AddInfrastructureAndApplication(builder.Configuration);

var app = builder.Build();

// Garantir a inicialização das tabelas no PostgreSQL com política simples de retry (útil para docker-compose)
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
            logger.LogWarning("Tentativa {Attempt}/10 de conectar ao PostgreSQL falhou: {Message}. Aguardando 2 segundos...", attempt, ex.Message);
            if (attempt == 10)
            {
                logger.LogError("Não foi possível conectar ao PostgreSQL após 10 tentativas. A aplicação continuará aguardando chamadas.");
                break;
            }
            await Task.Delay(2000);
        }
    }
}

// Configuração de Middleware
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "2PC Demo API v1");
    c.RoutePrefix = string.Empty; // Carrega a documentação do Swagger diretamente na raiz (http://localhost:5000/)
});

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.Run();
