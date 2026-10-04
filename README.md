# KDPaperClipPlaygroundRepo

Repository di riferimento del progetto **Progetto Playground**, gestito tramite Paperclip
dall'organizzazione **KD Paperclip Playground Org**.

## Stato attuale

| Task | Titolo | Stato |
| --- | --- | --- |
| KDP-20 | Progettazione piattaforma "Capital-Majordomo" | Proposta architetturale in [`docs/capital-majordomo/`](docs/capital-majordomo/README.md) |
| KDP-23/25/26 | Backend skeleton Capital-Majordomo | Branch `backend-skeleton` (vedi sotto) |

## Backend Capital-Majordomo: avvio in locale

Scheletro .NET 10 (modular monolith) che implementa l'architettura di
[`docs/capital-majordomo/`](docs/capital-majordomo/README.md).

| Percorso | Contenuto |
| --- | --- |
| `Majordomo.slnx` | Unica solution con tutti i progetti (aprire con Visual Studio 2026) |
| `Majordomo.slnLaunch` | Profili di avvio multiplo per VS: `Majordomo (tutto)`, `Majordomo (Api + Stub)` |
| `src/BuildingBlocks` | SharedKernel, Observability (OpenTelemetry, health check), Infrastructure (EF Core, outbox, idempotenza, auth) |
| `src/Integrations` | ACL Capital.com e client gRPC del Forecasting Service |
| `src/Modules` | Strategy, Risk, Execution, Portfolio, MarketData, Backtesting, Notifications |
| `src/Hosts` | `Majordomo.Api` (API di gestione v1) e `Majordomo.Worker` (ciclo decisionale, job in background) |
| `tools/Majordomo.ForecastingStub` | Stub gRPC del Forecasting Service (previsione naive, nessun modello ML) |
| `tools/postman` | Collezione Postman "playground" + environment locale |
| `deploy/local` | docker-compose dei servizi accessori per Docker Desktop su Windows/WSL 2 |
| `tests` | Test unitari e di architettura |

### Prerequisiti

- .NET SDK 10.0.100 o successivo (vedi `global.json`), Visual Studio 2026 con il workload "ASP.NET e sviluppo Web".
- Docker Desktop per Windows con backend WSL 2 (solo per i servizi accessori).
- Postman (desktop, oppure web con agent locale).

### 1. Servizi accessori (docker-compose)

Da PowerShell nella root del repository:

```powershell
.\deploy\local\up.ps1             # oppure: docker compose -f deploy/local/docker-compose.yml up -d
docker compose -f deploy/local/docker-compose.yml --profile tools up -d pgadmin   # facoltativo
.\deploy\local\down.ps1           # stop; "docker compose ... down -v" azzera anche i dati
```

Da WSL/Linux/macOS: `./deploy/local/up.sh`. Le porte si cambiano copiando `deploy/local/.env.example` in `.env`.

| Servizio | Indirizzo | Note |
| --- | --- | --- |
| PostgreSQL 17 + TimescaleDB | `localhost:5432` | db/utente `majordomo`, password `majordomo-dev-only` (solo sviluppo) |
| Redis | `localhost:6379` | cache per le Idempotency-Key |
| WireMock (mock Capital.com) | `http://localhost:8089` | mapping in `deploy/local/wiremock` |
| Grafana LGTM (OTLP) | `http://localhost:3000`, OTLP `4317/4318` | admin/admin |
| pgAdmin (profilo `tools`) | `http://localhost:5050` | |

I progetti .NET non girano in docker: si avviano "bare metal" dall'IDE o con `dotnet run`.

### 2. Migrazioni del database

In `Development` l'host Api applica all'avvio le migrazioni EF Core di tutti i DbContext
(`Database:ApplyMigrationsOnStartup=true` in `appsettings.Development.json`): basta avviare l'Api dopo i container.
Per crearne di nuove (ogni modulo ha il proprio schema e una design-time factory):

```powershell
dotnet tool install --global dotnet-ef
dotnet ef migrations add <Nome> --project src/Modules/Strategy/Majordomo.Strategy --context StrategyDbContext
```

DbContext disponibili: `InfraDbContext` (Infrastructure), `StrategyDbContext`, `RiskDbContext`, `ExecutionDbContext`,
`BacktestingDbContext`. In produzione le migrazioni le applica la pipeline, mai l'host.

### 3. Debug in Visual Studio 2026

1. Aprire `Majordomo.slnx`.
2. Nella barra degli strumenti scegliere il profilo di avvio **Majordomo (tutto)** (definito in `Majordomo.slnLaunch`)
   e premere F5: partono ForecastingStub, Api e Worker con il debugger agganciato a tutti e tre.
   In alternativa: tasto destro sulla solution → *Configura progetti di avvio…* → *Più progetti di avvio*.
3. Porte (da `Properties/launchSettings.json`): Api `http://localhost:5180` / `https://localhost:7180`
   (si apre `/scalar/v1`), ForecastingStub `http://localhost:50051` (gRPC su HTTP/2 in chiaro), Worker senza porta.
4. La configurazione di sviluppo (`appsettings.Development.json`) punta già ai container: Postgres 5432,
   Redis 6379, WireMock 8089, OTLP 4317. Nessuna credenziale reale: per Capital.com Demo usare `dotnet user-secrets`.

Da riga di comando: `dotnet run --project tools/Majordomo.ForecastingStub`, poi `src/Hosts/Majordomo.Api` e
`src/Hosts/Majordomo.Worker`. Build e test: `dotnet build Majordomo.slnx` e `dotnet test --solution Majordomo.slnx`.

### 4. Playground con Postman

1. Importare `tools/postman/Majordomo.postman_collection.json` e `tools/postman/Majordomo.Local.postman_environment.json`.
2. Selezionare l'environment **Majordomo - Local**.
3. Autenticazione di sviluppo: in `Development` l'Api accetta gli header `X-Dev-User` e `X-Dev-Roles`
   (`viewer`, `operator`, `risk-admin`), già impostati su ogni richiesta tramite le variabili `roles_*`.
   Fuori da Development questo schema è rifiutato all'avvio e si usa OIDC.
4. Eseguire le cartelle in ordine: *Health* → *Dev playground* (ACL Capital.com su WireMock, market data, forecasting
   tramite lo stub gRPC) → *Jobs* (`Crea job` salva `jobId` ed `etag`) → *Backtesting* (salva `operationId`) →
   *Operations* → *Risk - kill switch* → *Portfolio / Execution*. Ogni richiesta ha test sui codici di stato attesi,
   quindi la collezione si può lanciare anche con il Collection Runner.
5. Per chiamare lo stub gRPC direttamente: nuova richiesta gRPC su `localhost:50051` con *Using server reflection*.
