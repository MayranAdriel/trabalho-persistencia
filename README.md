# Two-Phase Commit (2PC) com PostgreSQL e MongoDB (.NET 10)

Aplicação desenvolvida em **.NET 10** seguindo os princípios de **Clean Architecture**, **Result Pattern**, Controllers tradicionais ASP.NET Core (sem Minimal APIs) e suporte completo a **Docker / Docker Compose**.

Esta aplicação demonstra na prática o funcionamento do protocolo de **Two-Phase Commit (2PC)** entre dois bancos de dados heterogêneos (**PostgreSQL** e **MongoDB**), garantindo consistência atômica entre eles.

---

## 🏛️ Arquitetura (Clean Architecture)

A solução está estruturada em 4 camadas bem delimitadas:

```
trabalho-persistencia/
├── src/
│   ├── TwoPhaseCommitDemo.Domain/          # Entidades, Enums, Interfaces dos Repositórios e Contratos 2PC
│   │   ├── Common/                         # Result Pattern (Result, Result<T>, Error) e TwoPhaseCommitResult
│   │   ├── Entities/                       # Product
│   │   ├── Enums/                          # TransactionState (Initialized, Preparing, Prepared, etc.)
│   │   └── Interfaces/                     # ITransactionParticipant, ITwoPhaseCommitCoordinator, Repositories
│   │
│   ├── TwoPhaseCommitDemo.Application/     # Casos de uso, DTOs e Interfaces de Serviços
│   │   ├── DTOs/                           # ProductDtos, ScenarioDtos
│   │   ├── Interfaces/                     # IScenario1Service, IScenario2Service, IScenario3Service, etc.
│   │   ├── Services/                       # Implementação dos serviços de negócio e orquestração dos cenários
│   │   └── Extensions/                     # ServicesExtensions (AddApplicationServices)
│   │
│   ├── TwoPhaseCommitDemo.Infrastructure/  # Implementação de banco, drivers e coordenador 2PC
│   │   ├── Data/                           # PostgresDbContext (EF Core) e MongoDbContext (MongoDB Driver)
│   │   ├── Repositories/                   # ProductPostgresRepository e ProductMongoRepository
│   │   ├── Transactions/                   # PostgresTransactionParticipant, MongoTransactionParticipant, TwoPhaseCommitCoordinator
│   │   ├── Services/                       # ConsistencyMonitorService (Polling a cada 10ms via BackgroundService)
│   │   └── Extensions/                     # ServicesExtensions (AddInfrastructureServices, AddDatabaseServices, etc.)
│   │
│   └── TwoPhaseCommitDemo.WebApi/          # Controllers tradicionais ASP.NET Core e Configurações
│       ├── Common/                         # ApiControllerBase (mapeador do Result Pattern para HTTP)
│       ├── Controllers/                    # ScenariosController, ProductsController, MonitorController
│       ├── Middlewares/                    # GlobalExceptionHandler (IExceptionHandler nativo do ASP.NET Core)
│       └── Program.cs                      # Configuração de DI via extensions, Swagger e Inicialização
│
├── tests/
│   └── TwoPhaseCommitDemo.Tests/           # Testes unitários do Coordenador 2PC e Cenários (xUnit, Moq, FluentAssertions)
│
├── docker-compose.yml                      # Orquestração do PostgreSQL, MongoDB (Replica Set rs0) e WebApi
├── Dockerfile                              # Multi-stage build para .NET 10
├── README.md                               # Guia de execução e documentação da API
└── RELATORIO.md                            # Relatório técnico completo da arquitetura e cenários
```

---

## 🔄 Como Funciona o Two-Phase Commit (2PC) nesta Aplicação

Como o PostgreSQL (Relacional SQL) e o MongoDB (NoSQL Document Store) não compartilham um gerenciador de transações distribuídas (DTC) nativo, o Two-Phase Commit é implementado através de um **Coordenador de Transações de Aplicação** (`TwoPhaseCommitCoordinator`) interagindo com participantes dedicados (`ITransactionParticipant`):

### Fase 1: Prepare (Votação e Pré-persistência)
1. O Coordenador solicita que o primeiro participante execute a operação em contexto transacional isolado (no PostgreSQL dentro de `IDbContextTransaction` com `SaveChangesAsync()`; no MongoDB dentro de uma `IClientSessionHandle` com `session.StartTransaction()`).
2. Se a operação for bem-sucedida, o participante vota **OK (Prepared)**.
3. O Coordenador prossegue para o próximo participante e repete a fase de Prepare.
4. **Se qualquer participante falhar**: O Coordenador interrompe imediatamente a fase 1, entra em estado `Aborting` e invoca `RollbackAsync()` em todos os participantes que já haviam preparado (e no que falhou). Nenhum dado é comitado.

### Fase 2: Commit (Efetivação Atômica)
- Se **todos** os participantes votaram com sucesso na Fase 1, o Coordenador muda o estado para `Committing` e comita a transação em ambos os bancos (`transaction.CommitAsync()` no PostgreSQL e `session.CommitTransactionAsync()` no MongoDB).

> **Nota sobre o MongoDB:** Transações multi-documento no MongoDB exigem a execução em modo **Replica Set** (`rs0`). O arquivo `docker-compose.yml` inclui a inicialização automática do replica set.

---

## 🧪 Os 3 Cenários Implementados

### 📌 Cenário 1: Inserir Postgres ➔ Erro MongoDB ➔ Rollback Postgres
- **Endpoint:** `POST /api/scenarios/scenario-1`
- **Fluxo:**
  1. A transação é iniciada no PostgreSQL e a inserção é executada dentro da transação.
  2. Em seguida, a operação tenta ser preparada no MongoDB, onde um erro é intencionalmente simulado.
  3. O Coordenador 2PC detecta a falha do MongoDB e aciona o **Rollback** no PostgreSQL.
  4. **Resultado:** Nenhum dado é salvo em nenhum dos bancos. Ambos permanecem consistentes no estado original.

### 📌 Cenário 2: Inserir MongoDB ➔ Erro Postgres ➔ Rollback MongoDB
- **Endpoint:** `POST /api/scenarios/scenario-2`
- **Fluxo:**
  1. A transação é iniciada no MongoDB e o documento é inserido na sessão transacional.
  2. Em seguida, a operação tenta ser preparada no PostgreSQL, onde um erro é intencionalmente simulado.
  3. O Coordenador 2PC detecta a falha do PostgreSQL e aciona o **Rollback (Abort)** na sessão do MongoDB.
  4. **Resultado:** Nenhum documento é persistido no MongoDB nem no PostgreSQL. Atomicidade preservada.

### 📌 Cenário 3: Atualizações Concorrentes 2PC + Leitura Constante a cada 10ms
- **Endpoint:** `POST /api/scenarios/scenario-3?numberOfUpdates=5&delayBetweenUpdatesMs=150`
- **Fluxo:**
  1. Um serviço de monitoramento em tempo real (`ConsistencyMonitorService`) inicia um loop com precisão de **10 milissegundos** via `PeriodicTimer`.
  2. A cada 10ms, o serviço faz consultas simultâneas tanto no PostgreSQL quanto no MongoDB e compara quantidades, versões de registro e consistência de dados.
  3. Enquanto as leituras acontecem a cada 10ms, uma sequência de atualizações via 2PC é executada em ambos os bancos.
  4. O endpoint retorna o resumo da execução acompanhado de uma amostra detalhada dos snapshots capturados a cada 10ms demonstrando as transições de estado até a convergência total.

---

## 🎯 Result Pattern

Todas as operações de negócio e infraestrutura retornam objetos `Result` ou `Result<T>`:
- Evita o uso de exceções para fluxo de controle normal.
- Contém propriedades `IsSuccess`, `IsFailure`, `Value` e `Error` tipado (`Code`, `Message`, `Type`).
- A classe base de controller `ApiControllerBase` converte automaticamente o `Result` para os códigos HTTP adequados (`200 OK`, `400 BadRequest`, `404 NotFound`, `409 Conflict`, `500 InternalServerError`).

---

## 🚀 Como Executar o Projeto

### Pré-requisitos
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) e Docker Compose

---

### Opção 1: Executando tudo via Docker Compose (Aplicação + Bancos)

Basta rodar no diretório raiz do projeto:

```bash
docker compose up --build
```

Os seguintes serviços serão iniciados:
- **PostgreSQL 16**: Porta `5432`
- **MongoDB 7.0 (Replica Set rs0)**: Porta `27017`
- **Inicializador do Replica Set**: Container temporário `mongo-init-replica`
- **API .NET 10**: Porta `5000`

Acesse a interface interativa do **Swagger** em seu navegador:
👉 **[http://localhost:5000](http://localhost:5000)**

---

### Opção 2: Subir apenas os Bancos via Docker e rodar a API localmente

Caso prefira rodar ou debugar a aplicação .NET 10 na sua máquina host (via CLI ou IDE):

1. **Suba o PostgreSQL e o MongoDB com o docker-compose:**
```bash
docker compose up -d postgres mongodb mongo-init-replica
```

2. **Execute a WebApi via dotnet CLI:**
```bash
dotnet run --project src/TwoPhaseCommitDemo.WebApi/TwoPhaseCommitDemo.WebApi.csproj
```

3. Acesse a documentação Swagger no navegador:
👉 **[http://localhost:5000](http://localhost:5000)** ou **[http://localhost:5128](http://localhost:5128)** (conforme porta exibida no terminal).

---

## 🧪 Executando os Testes Automatizados

Para rodar a suíte de testes unitários que valida o Coordenador 2PC e os cenários de Rollback:

```bash
dotnet test
```

---

## 📡 Exemplos de Requisições via cURL

### 1. Consultar estado dos bancos
```bash
curl -X GET http://localhost:5000/api/products/state
```

### 2. Executar o Cenário 1 (Postgres -> Erro Mongo -> Rollback Postgres)
```bash
curl -X POST http://localhost:5000/api/scenarios/scenario-1 \
  -H "Content-Type: application/json" \
  -d '{"sku": "TEST-SC1", "name": "Produto Cenário 1", "price": 199.90, "stockQuantity": 10}'
```

### 3. Executar o Cenário 2 (Mongo -> Erro Postgres -> Rollback Mongo)
```bash
curl -X POST http://localhost:5000/api/scenarios/scenario-2 \
  -H "Content-Type: application/json" \
  -d '{"sku": "TEST-SC2", "name": "Produto Cenário 2", "price": 249.90, "stockQuantity": 15}'
```

### 4. Executar o Cenário 3 (Atualizações + Consulta a cada 10ms)
```bash
curl -X POST "http://localhost:5000/api/scenarios/scenario-3?numberOfUpdates=5&delayBetweenUpdatesMs=100"
```

### 5. Iniciar e parar monitoramento contínuo em background
```bash
# Iniciar monitor contínuo a cada 10ms
curl -X POST http://localhost:5000/api/monitor/start

# Ver snapshots recentes coletados a cada 10ms
curl -X GET http://localhost:5000/api/monitor/snapshots?limit=20

# Pausar monitor
curl -X POST http://localhost:5000/api/monitor/stop
```

### 6. Limpar dados dos dois bancos para novo teste
```bash
curl -X DELETE http://localhost:5000/api/products/clear
```

