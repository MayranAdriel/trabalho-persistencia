# Estágio de Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copiar arquivos de projeto e restaurar dependências
COPY TwoPhaseCommitDemo.slnx ./
COPY src/TwoPhaseCommitDemo.Domain/*.csproj ./src/TwoPhaseCommitDemo.Domain/
COPY src/TwoPhaseCommitDemo.Application/*.csproj ./src/TwoPhaseCommitDemo.Application/
COPY src/TwoPhaseCommitDemo.Infrastructure/*.csproj ./src/TwoPhaseCommitDemo.Infrastructure/
COPY src/TwoPhaseCommitDemo.WebApi/*.csproj ./src/TwoPhaseCommitDemo.WebApi/

RUN dotnet restore ./src/TwoPhaseCommitDemo.WebApi/TwoPhaseCommitDemo.WebApi.csproj

# Copiar o restante do código-fonte e compilar
COPY . ./
RUN dotnet publish ./src/TwoPhaseCommitDemo.WebApi/TwoPhaseCommitDemo.WebApi.csproj -c Release -o /app/publish /p:UseAppHost=false

# Estágio de Execução (Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development
EXPOSE 8080

ENTRYPOINT ["dotnet", "TwoPhaseCommitDemo.WebApi.dll"]

