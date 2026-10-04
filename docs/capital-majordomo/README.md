# Capital-Majordomo — pacchetto di architettura

Task Paperclip: **KDP-20** — Progettazione piattaforma "Capital-Majordomo".
Autore: KDPCPG-SW-Architect. Stato: **Proposta (v0.1, 2026-10-04)**.

Capital-Majordomo è una piattaforma per l'esecuzione automatica di strategie di trading su
CFD tramite le API di Capital.com. Le decisioni di trading nascono da previsioni su serie
temporali prodotte da modelli di forecasting di base (riferimento: Google TimesFM 3.0) e sono
vincolate da parametri di rischio e limiti operativi configurati per ogni "Trading Job".

> Avvertenza: è un documento di progettazione tecnica, non una consulenza finanziaria.
> Il trading di CFD con leva comporta un rischio elevato di perdita del capitale. La piattaforma
> prevede come default l'uso del conto **Demo** di Capital.com; il passaggio a Live è una
> decisione esplicita del business (vedi ADR-0005 e RFC §12).

## Indice degli artefatti

| Artefatto | Percorso | Funzione |
| --- | --- | --- |
| RFC / documento di design | [`rfc/RFC-001-capital-majordomo.md`](rfc/RFC-001-capital-majordomo.md) | Visione d'insieme: obiettivi, requisiti, architettura, flussi, rischio, sicurezza, osservabilità, test, rilascio, rischi, domande aperte |
| Modello di dominio | [`domain/domain-model.md`](domain/domain-model.md) | Bounded context, aggregati, invarianti, eventi di dominio, linguaggio ubiquo |
| Codice di riferimento del dominio | [`domain/reference/TradingJob.cs`](domain/reference/TradingJob.cs) | Esempio C# dell'aggregato `TradingJob` e della policy di rischio (non compilato in CI: riferimento) |
| Diagrammi C4 e sequenze | [`diagrams/c4-and-sequences.md`](diagrams/c4-and-sequences.md) | Contesto, container, componenti e sequenze del ciclo decisionale (Mermaid) |
| ADR | [`adr/`](adr/) | Decisioni architetturali con alternative e compromessi |
| Contratto gRPC forecasting | [`contracts/proto/forecasting/v1/forecasting.proto`](contracts/proto/forecasting/v1/forecasting.proto) | Interfaccia fra core .NET e servizio Python di inferenza |
| API di gestione | [`contracts/openapi/management-api.v1.yaml`](contracts/openapi/management-api.v1.yaml) | REST per job, rischio, kill switch, report |
| Eventi di integrazione | [`contracts/asyncapi/integration-events.v1.yaml`](contracts/asyncapi/integration-events.v1.yaml) | Eventi pubblicati verso notifiche/analytics |
| Schema parametri job | [`contracts/schemas/trading-job-parameters.v1.schema.json`](contracts/schemas/trading-job-parameters.v1.schema.json) | Validazione dei parametri del "job/algoritmo di gestione delle transazioni" |
| Ambiente locale | [`deploy/docker-compose.yml`](deploy/docker-compose.yml) | PostgreSQL+TimescaleDB, OTel Collector, Grafana LGTM per sviluppo |
| Pipeline CI di riferimento | [`deploy/ci-reference.yml`](deploy/ci-reference.yml) | Schema della pipeline GitHub Actions (da attivare in `.github/workflows/` alla creazione del codice) |
| Roadmap e backlog | [`roadmap.md`](roadmap.md) | Fasi, task implementabili con criteri di accettazione, spike |

## ADR

| # | Titolo | Stato |
| --- | --- | --- |
| [0001](adr/0001-modular-monolith-dotnet.md) | Monolite modulare .NET 10 come core | Proposto |
| [0002](adr/0002-python-forecasting-service-grpc.md) | Servizio di forecasting in Python dietro contratto gRPC | Proposto |
| [0003](adr/0003-postgresql-timescaledb-outbox.md) | PostgreSQL + TimescaleDB, outbox transazionale, nessun broker iniziale | Proposto |
| [0004](adr/0004-capital-com-anti-corruption-layer.md) | Gateway Capital.com come Anti-Corruption Layer con rate limiting e riconciliazione | Proposto |
| [0005](adr/0005-risk-engine-hard-gate-kill-switch.md) | Risk engine come gate obbligatorio, kill switch e Demo-first | Proposto |
| [0006](adr/0006-backtesting-paper-trading-promotion.md) | Promozione strategie: backtest → paper (Demo) → Live | Proposto |

## Sintesi delle scelte

- **Stile:** monolite modulare .NET 10 LTS (ASP.NET Core + Worker) con confini per bounded
  context; un solo servizio separato, il **Forecasting Service** in Python, perché l'ecosistema
  dei modelli (TimesFM, PyTorch/JAX) è Python.
- **Integrazione:** gRPC sincrono verso il forecasting (con deadline, circuit breaker e
  degradazione "nessun segnale = nessun trade"); REST + WebSocket verso Capital.com isolati in un ACL.
- **Dati:** PostgreSQL con estensione TimescaleDB per le serie storiche; outbox transazionale
  per eventi e ordini; event log append-only delle decisioni per audit.
- **Sicurezza del capitale:** ogni ordine attraversa un risk engine deterministico con limiti
  rigidi (perdita giornaliera, drawdown, esposizione, leva, numero posizioni) e kill switch.
- **Osservabilità:** OpenTelemetry ovunque, SLO sulla latenza decisione→conferma e sulla
  riconciliazione posizioni.
