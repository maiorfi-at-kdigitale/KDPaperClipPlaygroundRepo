# Guida passo-passo: test backend in locale con Postman (branch `main`)

Questa guida descrive come testare il backend **nello stato attuale del branch `main`** usando i file già presenti nel repository:

- Collezione: `tools/postman/Majordomo.postman_collection.json`
- Environment: `tools/postman/Majordomo.Local.postman_environment.json`

## 1) Prerequisiti

- **.NET SDK 10.0.100+** (vedi `global.json`)
- **Visual Studio 2026** con workload ASP.NET e sviluppo Web (oppure CLI `dotnet`)
- **Docker Desktop** (WSL2 su Windows) per i servizi accessori
- **Postman** desktop (o web + local agent)

## 2) Avvio servizi accessori in locale

Dalla root del repo:

### Windows / PowerShell

```powershell
.\deploy\local\up.ps1
# facoltativo: pgAdmin
# docker compose -f deploy/local/docker-compose.yml --profile tools up -d pgadmin
```

### Linux/macOS/WSL

```bash
./deploy/local/up.sh
```

Servizi attesi:

- PostgreSQL/TimescaleDB: `localhost:5432`
- Redis: `localhost:6379`
- WireMock (Capital mock): `http://localhost:8089`
- Grafana LGTM: `http://localhost:3000`
- (Opzionale) pgAdmin: `http://localhost:5050`

## 3) Avvio backend (host applicativi)

I progetti .NET **non** girano in docker: vanno avviati localmente.

### Opzione consigliata (Visual Studio)

1. Apri `Majordomo.slnx`
2. Seleziona il profilo di startup **Majordomo (tutto)** (in `Majordomo.slnLaunch`)
3. Avvia in debug (F5)

Questo avvia tipicamente:

- `Majordomo.Api` (`http://localhost:5180`, `https://localhost:7180`)
- `Majordomo.Worker`
- `Majordomo.ForecastingStub` (`localhost:50051`, gRPC)

### Opzione CLI (alternativa)

Avvia in terminali separati:

```bash
dotnet run --project tools/Majordomo.ForecastingStub
dotnet run --project src/Hosts/Majordomo.Api
dotnet run --project src/Hosts/Majordomo.Worker
```

## 4) Import in Postman

1. Apri Postman
2. Importa la collezione `tools/postman/Majordomo.postman_collection.json`
3. Importa l'environment `tools/postman/Majordomo.Local.postman_environment.json`
4. Seleziona environment **Majordomo - Local**

## 5) Variabili da impostare/verificare

### Variabili environment (Majordomo - Local)

Verifica che siano coerenti con il tuo setup locale:

- `baseUrl = http://localhost:5180`
- `baseUrlHttps = https://localhost:7180`
- `forecastingGrpc = localhost:50051`
- `capitalMockUrl = http://localhost:8089`
- `grafanaUrl = http://localhost:3000`
- `devUser = dev@majordomo.local`
- `roles_viewer = viewer`
- `roles_operator = operator`
- `roles_riskadmin = risk-admin,operator`
- `epic = US100`
- `resolution = MINUTE_15`

### Variabili collezione (aggiornate automaticamente)

La collezione contiene:

- `jobId`
- `etag`
- `operationId`
- `orderId`

Durante l'esecuzione:

- **Crea job** salva `jobId` e `etag`
- **Sostituisci parametri (If-Match)** può aggiornare `etag` da response header
- **Avvia backtest** salva `operationId`

`orderId` va impostato manualmente se vuoi testare in modo realistico **Dettaglio ordine** (in assenza di un id reale, è normale ottenere `404` su quella request).

## 6) Ordine di esecuzione richieste (consigliato)

Esegui le cartelle in questo ordine:

1. **Health**
2. **Dev playground (solo Development)**
3. **Jobs (Strategy)**
4. **Backtesting**
5. **Operations**
6. **Risk - kill switch**
7. **Portfolio / Execution**

### Richieste principali per cartella

#### Health

- Liveness
- Readiness (Postgres, Redis, ...)
- OpenAPI v1 (JSON)

#### Dev playground (solo Development)

- Who am I (operator)
- Who am I (anonimo)
- Capital.com ping (WireMock)
- Market data - candele
- Forecasting - modelli (stub gRPC)
- Forecasting - previsione

#### Jobs (Strategy)

- Crea job
- Crea job - parametri non validi (422)
- Crea job - solo viewer (403)
- Crea job - anonimo (401)
- Lista job
- Lista job per stato
- Dettaglio job
- Sostituisci parametri (If-Match)
- Sostituisci parametri - senza If-Match (428)
- Transizione - promote (con evidenza backtest)
- Transizione - pause
- Transizione - resume (risk-admin)
- Transizione - archive
- Decisioni (paginazione a cursore)

#### Backtesting

- Avvia backtest

#### Operations

- Stato operazione

#### Risk - kill switch

- Stato kill switch
- Attiva kill switch (job)
- Attiva kill switch (globale)
- Rilascia kill switch (risk-admin)
- Rilascia kill switch - operator (403)

#### Portfolio / Execution

- Posizioni
- Report performance
- Dettaglio ordine

## 7) Risultati attesi

La collezione include test automatici sui codici HTTP attesi. In particolare:

- Health/Liveness/OpenAPI devono rispondere `200`
- Readiness accetta `200` oppure `503` (se dipendenze non disponibili)
- Alcune request sono **negative test** voluti e devono restituire:
  - `401` (anonimo)
  - `403` (ruolo non autorizzato)
  - `422` (payload invalido)
  - `428` (assenza `If-Match`)
- `Avvia backtest` atteso `202` con valorizzazione di `operationId`
- `Stato operazione` può essere `200` oppure `404` se `operationId` non valido/non ancora disponibile
- `Dettaglio ordine` può essere `200` o `404` in base a `orderId`

Se lanci la collezione con Collection Runner, considera **corretti** anche i casi 401/403/422/428 previsti dalla request specifica.

## 8) Troubleshooting

### `Readiness` risponde `503`

- Verifica che i container siano attivi:
  - `docker compose -f deploy/local/docker-compose.yml ps`
- Riavvia i servizi locali:
  - `deploy/local/up.ps1` (Windows)
  - `./deploy/local/up.sh` (Linux/macOS/WSL)

### Errori di connessione (`ECONNREFUSED`, timeout)

- Controlla che `Majordomo.Api` sia attiva su `http://localhost:5180`
- Controlla che `forecastingGrpc` punti a `localhost:50051`
- Controlla che WireMock sia su `http://localhost:8089`

### Request dev (`/dev/*`) non funzionano

- Verifica di essere in ambiente `Development`
- Verifica header `X-Dev-User` e `X-Dev-Roles`

### Errori su request con `If-Match`

- Esegui prima **Crea job** o **Dettaglio job** per valorizzare `etag`
- Non cancellare il valore `etag` nelle variabili di collezione

### `Dettaglio ordine` restituisce `404`

- Comportamento possibile/atteso senza `orderId` reale
- Imposta `orderId` da una decisione/ordine realmente creato

## 9) Arresto ambiente locale

- Stop servizi accessori:
  - Windows: `deploy/local/down.ps1`
  - Oppure: `docker compose -f deploy/local/docker-compose.yml down`
- (Opzionale) reset completo dati: `docker compose -f deploy/local/docker-compose.yml down -v`
