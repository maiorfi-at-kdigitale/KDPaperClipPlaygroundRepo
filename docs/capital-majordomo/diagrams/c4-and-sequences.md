# Diagrammi C4 e di sequenza — Capital-Majordomo

Formato Mermaid (renderizzato da GitHub). Riferimento: RFC-001 §5.

## C4 — Livello 1: contesto

```mermaid
C4Context
    title Capital-Majordomo - Contesto
    Person(op, "Operatore / Risk admin", "Configura i Trading Job, monitora, attiva il kill switch")
    System(maj, "Capital-Majordomo", "Esegue strategie automatiche su CFD guidate da forecasting e vincolate dal rischio")
    System_Ext(cap, "Capital.com API", "REST + WebSocket: sessione, mercati, prezzi, posizioni, ordini, conto (Demo e Live)")
    System_Ext(hf, "Registry modelli (Hugging Face)", "Pesi TimesFM versionati")
    System_Ext(bq, "BigQuery AI.FORECAST (opzionale)", "Forecast TimesFM gestito")
    System_Ext(teams, "Microsoft Teams", "Notifiche tramite agente bridge")
    System_Ext(idp, "Entra ID", "Identità OIDC + MFA")

    Rel(op, maj, "Gestisce job e rischio", "HTTPS")
    Rel(maj, cap, "Dati di mercato e ordini", "HTTPS / WSS")
    Rel(maj, hf, "Scarica pesi (build time)", "HTTPS")
    Rel(maj, bq, "Forecast alternativo", "HTTPS")
    Rel(maj, teams, "Notifiche", "HTTPS")
    Rel(op, idp, "Autenticazione")
```

## C4 — Livello 2: container

```mermaid
C4Container
    title Capital-Majordomo - Container
    Person(op, "Operatore")
    System_Ext(cap, "Capital.com API")
    System_Ext(teams, "Teams bridge")

    System_Boundary(b, "Capital-Majordomo") {
        Container(api, "majordomo-api", ".NET 10 ASP.NET Core", "API di gestione REST/OpenAPI, SignalR")
        Container(wrk, "majordomo-worker", ".NET 10 Worker (leader singolo)", "Scheduler cicli, streaming, risk, esecuzione, riconciliazione, outbox dispatcher")
        Container(fc, "forecasting-svc", "Python 3.12, gRPC, TimesFM, statsforecast", "Inferenza stateless")
        ContainerDb(db, "PostgreSQL 17 + TimescaleDB", "Schemi per modulo", "Candele, previsioni, job, decisioni, ordini, outbox")
        Container(otel, "OpenTelemetry Collector", "otelcol", "Tracce, metriche, log -> Grafana LGTM")
    }

    Rel(op, api, "HTTPS, OIDC")
    Rel(api, db, "EF Core / Npgsql")
    Rel(wrk, db, "EF Core / Npgsql, advisory lock")
    Rel(wrk, fc, "Forecast()", "gRPC + deadline")
    Rel(wrk, cap, "REST + WebSocket", "via ACL con rate limiter")
    Rel(wrk, teams, "Eventi di integrazione", "HTTPS")
    Rel(api, otel, "OTLP")
    Rel(wrk, otel, "OTLP")
    Rel(fc, otel, "OTLP")
```

## C4 — Livello 3: componenti del worker

```mermaid
flowchart TB
    subgraph worker[majordomo-worker]
      SCH[DecisionScheduler<br/>BackgroundService] --> CYC[DecisionCycleHandler]
      CYC --> MDQ[MarketData.ContextWindowQuery]
      CYC --> FCC[Forecasting.GrpcClient<br/>resilience pipeline]
      CYC --> SIG[Strategy.ISignalStrategy]
      CYC --> RSK[Risk.PreTradeRiskEvaluator]
      RSK --> PFQ[Portfolio.RiskSnapshotQuery]
      CYC --> EXC[Execution.OrderService<br/>tx + outbox]
      OUT[Infra.OutboxDispatcher] --> ODQ[Execution.OrderDispatchQueue<br/>Channel serializzato]
      ODQ --> GW[CapitalCom.Gateway ACL]
      CNF[Execution.ConfirmationPoller] --> GW
      REC[Execution.Reconciler<br/>ogni 30 s + all'avvio] --> GW
      STR[MarketData.StreamingService<br/>WebSocket max 40 epic] --> GW
      GW --> RL[RateLimiter token bucket<br/>10/s, 100 ms aperture]
      GW --> SES[SessionManager<br/>CST + X-SECURITY-TOKEN, ping]
      LDR[LeaderElection<br/>pg advisory lock] -.abilita.-> SCH
      LDR -.abilita.-> OUT
      LDR -.abilita.-> REC
    end
```

## Sequenza — ciclo decisionale con apertura posizione

```mermaid
sequenceDiagram
    autonumber
    participant S as DecisionScheduler
    participant H as DecisionCycleHandler
    participant MD as MarketData
    participant F as forecasting-svc (gRPC)
    participant ST as SignalStrategy
    participant R as RiskEvaluator
    participant DB as PostgreSQL
    participant O as OutboxDispatcher
    participant G as Capital.com Gateway
    participant C as Capital.com

    S->>H: DecisionCycleDue(jobId, barClose)
    H->>MD: GetContextWindow(epic, res, 512)
    MD-->>H: candele (completezza verificata)
    H->>F: Forecast(context, horizon, quantiles) [deadline 3 s]
    alt timeout / errore / breaker aperto
        F-->>H: errore
        H->>DB: Decision(NoSignal: ForecastUnavailable)
    else previsione valida
        F-->>H: point + quantili
        H->>ST: Evaluate(forecast, snapshot, params)
        ST-->>H: Signal(Long, strength, confidence) + TradeIntent
        H->>R: Evaluate(intent, policy, rules, riskSnapshot)
        alt Rejected
            R-->>H: Rejected(reason)
            H->>DB: Decision(Rejected)
        else Approved
            R-->>H: Approved(size, stop, tp)
            H->>DB: TX { Decision + Order(Pending) + OutboxMessage }
            O->>DB: legge outbox
            O->>G: OpenPosition(order)
            G->>C: POST /positions (rate limited)
            C-->>G: dealReference
            G-->>O: Submitted(dealReference)
            O->>DB: Order=Submitted
            loop fino a esito o 30 s
                G->>C: GET /confirms/{dealReference}
            end
            C-->>G: ACCEPTED (dealId) / REJECTED
            G-->>DB: Order=Accepted/Rejected, Position(Open), OutboxMessage(PositionOpened)
        end
    end
```

## Sequenza — errore ambiguo e riconciliazione

```mermaid
sequenceDiagram
    autonumber
    participant O as OutboxDispatcher
    participant G as Gateway
    participant C as Capital.com
    participant X as Reconciler
    participant DB as PostgreSQL

    O->>G: OpenPosition(order)
    G->>C: POST /positions
    C--xG: timeout (esito ignoto)
    G-->>O: Ambiguous
    O->>DB: Order=Unknown (blocca aperture su epic/job)
    Note over O: NESSUN retry automatico dell'apertura
    X->>C: GET /positions + GET /history/activity (finestra ordine)
    alt posizione trovata (epic, direzione, size, timestamp)
        X->>DB: Order=Accepted, Position(Open) collegata
    else nessuna attività
        X->>DB: Order=Rejected(reason=NotExecuted)
    end
    X->>DB: OutboxMessage(ReconciliationResolved)
```

## Sequenza — kill switch

```mermaid
sequenceDiagram
    autonumber
    actor U as Risk admin
    participant A as majordomo-api
    participant DB as PostgreSQL
    participant W as worker
    participant C as Capital.com
    U->>A: POST /v1/kill-switch {scope: global, reason}
    A->>DB: KillSwitch=Active + outbox(KillSwitchActivated)
    A-->>U: 202 Accepted (operationId)
    W->>DB: rileva KillSwitch (poll 1 s / LISTEN-NOTIFY)
    W->>W: blocca nuove aperture (gate del risk engine)
    W->>C: DELETE /workingorders/{id} per ogni ordine pendente
    W->>C: DELETE /positions/{dealId} per ogni posizione (priorità massima nel rate limiter)
    W->>DB: job -> Halted, esiti registrati
    W-->>U: notifica Teams con riepilogo
```
