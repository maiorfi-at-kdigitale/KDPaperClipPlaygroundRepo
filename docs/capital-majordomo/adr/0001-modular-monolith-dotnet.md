# ADR-0001 — Monolite modulare .NET 10 come core della piattaforma

- Stato: Proposto
- Data: 2026-10-04
- Decisori: SW Architect (proposta), CEO (approvazione)

## Contesto

Capital-Majordomo ha 8–9 bounded context (Market Data, Forecasting client, Strategy, Risk,
Execution, Portfolio, Backtesting, Gateway, Notifications), un carico modesto (≤ 50 job, ≤ 10
req/s verso il broker per vincolo esterno) e un requisito forte di correttezza transazionale fra
decisione, ordine ed evento. Il team è piccolo e l'ecosistema di riferimento è .NET.

## Decisione

Un **monolite modulare** in .NET 10 LTS, con un progetto per modulo (`Majordomo.<Modulo>` +
`Majordomo.<Modulo>.Contracts`), host separati `majordomo-api` e `majordomo-worker` costruiti
dallo stesso artefatto, comunicazione fra moduli tramite interfacce pubbliche e eventi in-process
(con outbox per quelli che devono uscire). Confini protetti da test di architettura e da schemi
DB separati per modulo. Il worker esegue in **istanza attiva singola** (leader election con
advisory lock PostgreSQL) per tutto ciò che invia ordini.

Struttura indicativa:

```
src/
  Majordomo.Api/                 # host ASP.NET Core
  Majordomo.Worker/              # host Worker Service
  Modules/
    MarketData/ Strategy/ Risk/ Execution/ Portfolio/ Backtesting/ Notifications/
      Majordomo.<M>/             # dominio + applicazione + infrastruttura del modulo
      Majordomo.<M>.Contracts/   # API pubblica del modulo (query, comandi, eventi)
  Integrations/
    Majordomo.CapitalCom/        # ACL (ADR-0004)
    Majordomo.Forecasting.Client/ # client gRPC (ADR-0002)
  BuildingBlocks/
    Majordomo.Outbox/ Majordomo.Observability/
tests/  (Unit, Integration, Architecture, Contract)
Directory.Build.props  Directory.Packages.props  .editorconfig
```

Standard: nullable abilitato, `TreatWarningsAsErrors`, Central Package Management, analizzatori
Roslyn, `Microsoft.Extensions.Http.Resilience`, OpenTelemetry, .NET Aspire per lo sviluppo
locale.

## Alternative considerate

1. **Microservizi per bounded context** — scalabilità e rilascio indipendenti, ma: transazioni
   distribuite fra Risk ed Execution (saga dove oggi basta una transazione locale), più pipeline,
   più superficie operativa, nessun bisogno attuale di scala indipendente.
2. **Monolite non modulare** — più veloce all'inizio, ma i confini erodono e l'estrazione futura
   (es. Backtesting su capacità di calcolo separata) diventa costosa.
3. **Core in Python** — unico linguaggio con il forecasting, ma tipizzazione e strumenti per
   concorrenza, stato e transazioni meno solidi per il percorso critico degli ordini.

## Conseguenze

- Positive: una transazione ACID per decisione + ordine + evento; un solo deploy del core;
  debugging e tracing semplici; estrazione possibile lungo confini già definiti (primo candidato:
  Backtesting come job batch separato).
- Negative: api e worker condividono il ciclo di rilascio; un bug in un modulo può impattare il
  processo (mitigato da bulkhead, test e dal fatto che gli stop lato broker restano attivi);
  serve disciplina per non attraversare i confini (test di architettura bloccanti in CI).
- Fitness function: test NetArchTest "nessun riferimento da `Majordomo.X` a `Majordomo.Y`
  eccetto `Majordomo.Y.Contracts`"; nessuna query cross-schema (verifica sui permessi DB).
