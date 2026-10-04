# RFC-001 — Capital-Majordomo: piattaforma di trading automatico su CFD guidata da forecasting

| Campo | Valore |
| --- | --- |
| Stato | Proposta |
| Autore | KDPCPG-SW-Architect |
| Task | KDP-20 |
| Data | 2026-10-04 |
| Revisori attesi | CEO, Product, DevOps, QA |

## 1. Sommario

Capital-Majordomo esegue in automatico "Trading Job": unità configurabili che, per un insieme
di strumenti (epic Capital.com), raccolgono prezzi, chiedono una previsione a un modello di
forecasting su serie temporali, trasformano la previsione in un segnale, dimensionano la
posizione in base al rischio ammesso e inviano ordini a Capital.com, sempre filtrati da un
risk engine con limiti rigidi. Obiettivo di business: far crescere il capitale iniziale/corrente
entro un profilo di rischio dichiarato dall'utente.

## 2. Obiettivi e non obiettivi

**Obiettivi**

1. Definire e gestire Trading Job con parametri di rischio e limiti operativi (JSON Schema
   versionato: `contracts/schemas/trading-job-parameters.v1.schema.json`).
2. Acquisire dati di mercato da Capital.com (storico via REST `GET /prices/{epic}`, tempo reale
   via WebSocket) e persisterli come serie temporali.
3. Ottenere previsioni con intervalli di confidenza (quantili) da modelli di base (TimesFM 3.0,
   con fallback su TimesFM 2.5 o modelli classici) e trasformarle in segnali.
4. Eseguire ordini/posizioni su Capital.com con stop loss obbligatorio, gestirne il ciclo di vita
   e riconciliare lo stato locale con quello del broker.
5. Garantire tracciabilità completa: ogni ordine deve essere spiegabile (prezzi → previsione →
   segnale → controllo di rischio → ordine → conferma).
6. Backtest e paper trading (conto Demo) prima di qualsiasi uso su conto Live.

**Non obiettivi (v1)**

- Trading ad alta frequenza o latenze sub-secondo: i limiti delle API (10 req/s, 1 ordine/0,1 s)
  e l'orizzonte dei modelli di forecasting non lo giustificano. Risoluzione minima: 1 minuto;
  risoluzione tipica: 15 minuti – 1 giorno.
- Multi-broker: Capital.com è l'unico broker; l'ACL (ADR-0004) rende possibile aggiungerne altri.
- Gestione di capitali di terzi / multi-tenant commerciale (implica obblighi regolamentari
  MiFID II / autorizzazioni: fuori perimetro). La v1 è mono-utente / mono-organizzazione.
- Addestramento di modelli propri: si usano modelli di base zero-shot; il fine-tuning è uno spike
  futuro.

## 3. Contesto e vincoli esterni

### 3.1 Capital.com Public API (verificato su open-api.capital.com, ottobre 2026)

| Aspetto | Valore | Impatto architetturale |
| --- | --- | --- |
| Base URL | Live `https://api-capital.backend-capital.com/`, Demo `https://demo-api-capital.backend-capital.com/` | Ambiente selezionato per configurazione; Demo di default |
| Autenticazione | `POST /session` con header `X-CAP-API-KEY` + identificativo + password (anche cifrata, `GET /session/encryptionKey`); risposta con token `CST` e `X-SECURITY-TOKEN` | Gestore di sessione dedicato; segreti in vault |
| Durata sessione | 10 minuti di inattività (REST e WebSocket) | Keep-alive (`GET /ping`) e rinnovo trasparente |
| Rate limit | 10 req/s per utente; 1 req/0,1 s per apertura posizioni/ordini; `POST /session` 1 req/s; Demo: 1000 `POST /positions` o `/workingorders` all'ora | Token bucket centralizzato in uscita, coda ordini serializzata |
| WebSocket | `wss://api-streaming-capital.backend-capital.com/connect`, max 40 strumenti, ping almeno ogni 10 min; si interrompe se cambia l'account con `PUT /session` | Massimo 40 epic in streaming per account; oltre si usa polling REST |
| Trading | `POST /positions`, `PUT/DELETE /positions/{dealId}`, `POST /workingorders`, conferma asincrona `GET /confirms/{dealReference}` | L'esito di un ordine è asincrono: macchina a stati con conferma e riconciliazione |
| Mercati | `GET /markets`, `GET /markets/{epic}`, `GET /marketnavigation` | Catalogo strumenti con regole (size minima, orari, leva) in cache |
| Storico | `GET /prices/{epic}?resolution=…&max=…&from=…&to=…` | Backfill con paginazione per intervallo |
| Conto | `GET /accounts`, `GET /history/activity`, `GET /history/transactions`, preferenze di leva per classe di asset | Fonte della verità per saldo, P&L e leva |

> Le API non offrono una chiave di idempotenza esplicita sulla creazione di posizioni: un retry
> cieco può aprire due posizioni. Per questo **le richieste di apertura non vengono mai
> ritentate automaticamente**; dopo un errore ambiguo si riconcilia leggendo `GET /positions` e
> `GET /history/activity` (ADR-0004).

### 3.2 Modelli di forecasting

- **Google TimesFM 3.0** (agosto 2026, checkpoint `google/timesfm-3.0-pytorch`): previsione
  zero-shot univariata e multivariata con covariate, output puntuale + quantili. Codice
  Apache-2.0; **la licenza dei pesi 3.0 va verificata** prima dell'uso (domanda aperta D1).
- **TimesFM 2.5**: disponibile anche come funzione gestita `AI.FORECAST` in BigQuery e AlloyDB
  (opzione "managed" senza GPU propria).
- Modelli classici (ARIMA/ETS via `statsforecast`) come baseline obbligatoria e fallback.

Ipotesi fondamentale e rischio principale: i modelli di base non sono addestrati per battere il
mercato; la loro utilità va **dimostrata con backtest walk-forward** sui costi reali (spread,
finanziamento overnight, slippage) prima di andare Live. L'architettura rende il modello un
componente sostituibile e misurato, non una certezza.

## 4. Requisiti

### 4.1 Funzionali

| ID | Requisito |
| --- | --- |
| RF-1 | CRUD dei Trading Job: strumenti, risoluzione, orizzonte, modello, strategia di segnale, parametri di rischio, limiti operativi, calendario operativo |
| RF-2 | Stati del job: `Draft → Backtesting → PaperTrading → Live`, più `Paused`, `Halted` (da risk engine o kill switch), `Archived` |
| RF-3 | Ciclo decisionale schedulato per job (es. a chiusura di ogni candela della risoluzione scelta) |
| RF-4 | Segnale = direzione + forza + confidenza derivati da previsione e quantili; nessun segnale se la confidenza è sotto soglia |
| RF-5 | Dimensionamento della posizione in base al rischio per trade (% equity) e alla distanza dello stop |
| RF-6 | Stop loss obbligatorio su ogni posizione; take profit e trailing stop opzionali |
| RF-7 | Kill switch globale e per job: chiude/annulla tutto e blocca nuove aperture |
| RF-8 | Riconciliazione periodica di posizioni, ordini e saldo con Capital.com |
| RF-9 | Backtest su dati storici con lo stesso codice di segnale e rischio usato in produzione |
| RF-10 | Report: equity curve, drawdown, P&L per job/strumento, hit rate, accuratezza delle previsioni (MAE/MASE, copertura dei quantili) |
| RF-11 | Notifiche (Teams/email) su eventi rilevanti: job fermato, limite raggiunto, errore di esecuzione |
| RF-12 | Audit: ogni decisione è registrata con i suoi input (snapshot) e riproducibile |

### 4.2 Non funzionali (valori obiettivo v1)

| Categoria | Obiettivo |
| --- | --- |
| Latenza decisione | p95 < 5 s dalla chiusura della candela all'invio dell'ordine (incluso forecast) |
| Latenza esecuzione | p95 < 2 s dall'invio alla conferma `GET /confirms` |
| Disponibilità | 99,5% mensile nelle ore di mercato; in caso di indisponibilità il sistema **non apre** posizioni (fail-safe) e lascia attivi gli stop lato broker |
| Correttezza | 0 posizioni non riconciliate per più di 2 minuti; 0 ordini senza stop loss |
| Volumi | ≤ 50 job attivi, ≤ 200 strumenti, risoluzione minima 1 min → ≤ ~300k candele/giorno |
| Retention | Candele: 5 anni (compresse); decisioni e ordini: 10 anni (audit) |
| RPO / RTO | RPO 5 min (WAL archiving), RTO 30 min; la fonte di verità delle posizioni resta il broker |
| Sicurezza | Credenziali Capital.com solo in vault; MFA sull'interfaccia di gestione; Live abilitabile solo con doppia conferma |
| Costi | Infrastruttura v1 ≤ 1 VM + DB gestito o self-hosted; GPU opzionale (TimesFM gira su CPU per batch piccoli; da misurare nello spike S2) |

## 5. Architettura proposta

### 5.1 Stile

Monolite modulare .NET 10 (ADR-0001) con un unico servizio esterno, il Forecasting Service
in Python (ADR-0002). Moduli = bounded context (vedi `domain/domain-model.md`):

| Modulo | Responsabilità |
| --- | --- |
| **Market Data** | Catalogo strumenti, backfill storico, streaming prezzi, aggregazione candele, qualità dati (gap, duplicati) |
| **Forecasting (client)** | Costruzione del contesto (finestre di storico, covariate), chiamata gRPC, persistenza previsioni e metriche di accuratezza |
| **Strategy** | Trading Job, pianificazione dei cicli, trasformazione previsione → segnale (strategie plug-in) |
| **Risk** | Policy di rischio, dimensionamento, limiti pre-trade, circuit breaker di portafoglio, kill switch |
| **Execution** | Ordini e posizioni, macchina a stati, coda serializzata verso il broker, conferme, riconciliazione |
| **Portfolio** | Saldo, equity, esposizione, P&L, drawdown (proiezione da eventi di Execution + dati del conto) |
| **Backtesting** | Simulatore di esecuzione (spread, slippage, finanziamento), walk-forward, report |
| **Capital.com Gateway (ACL)** | Sessione, rate limiting, REST/WebSocket, mappatura DTO ↔ modello interno |
| **Notifications** | Sottoscrittore di eventi di integrazione verso Teams/email |

### 5.2 Container

Vedi `diagrams/c4-and-sequences.md`.

| Container | Tecnologia | Note |
| --- | --- | --- |
| `majordomo-api` | ASP.NET Core 10 Minimal API | API di gestione (OpenAPI), autenticazione OIDC, SignalR per dashboard live |
| `majordomo-worker` | .NET 10 Worker Service | Scheduler cicli, streaming, esecuzione, riconciliazione, outbox dispatcher. **Istanza attiva singola** (leader election via advisory lock PostgreSQL) per garantire un solo esecutore di ordini |
| `forecasting-svc` | Python 3.12, gRPC (grpcio), TimesFM, statsforecast, uv | Stateless, scalabile orizzontalmente; modello caricato all'avvio |
| `majordomo-web` (opzionale v1.1) | Blazor o React | Dashboard; in v1 basta Grafana + API |
| `postgres` | PostgreSQL 17 + TimescaleDB | Dati di dominio, serie temporali, outbox, event log |
| `otel-collector` | OpenTelemetry Collector | Export verso Grafana LGTM (Loki/Tempo/Prometheus/Mimir) |

`majordomo-api` e `majordomo-worker` sono due host dello **stesso codice** (stessi moduli,
stesso artefatto container con entrypoint diverso): scalano e si riavviano indipendentemente
senza diventare microservizi.

### 5.3 Ciclo decisionale (caso d'uso principale)

1. **Trigger:** lo scheduler del modulo Strategy emette `DecisionCycleDue(jobId, barCloseTime)`
   alla chiusura della candela (con margine configurabile per l'arrivo dei dati).
2. **Dati:** Market Data fornisce la finestra di contesto (es. 512 candele) verificandone la
   completezza; se ci sono gap oltre soglia → ciclo saltato con motivo `InsufficientData`.
3. **Previsione:** chiamata gRPC `Forecast` con deadline (default 3 s). Errore, timeout o circuit
   breaker aperto → `NoSignal(ForecastUnavailable)`. **Mai** trade senza previsione valida.
4. **Segnale:** la strategia del job (es. `QuantileBreakout`, `ExpectedReturnThreshold`) calcola
   rendimento atteso a orizzonte H, la sua dispersione (q10–q90) e un punteggio di confidenza.
   Il rendimento atteso deve superare i costi stimati (spread + finanziamento) di un margine.
5. **Rischio:** Risk valuta la `TradeIntent` contro policy di job e di portafoglio e produce
   `Approved(size, stop, takeProfit)` o `Rejected(reason)`. Il dimensionamento usa:
   `size = (equity × rischioPerTrade) / (distanzaStop × valorePunto)`, arrotondato alle regole
   dello strumento e limitato da leva ed esposizione massime.
6. **Esecuzione:** Execution crea un `Order` in stato `Pending` **nella stessa transazione** della
   decisione e dell'evento in outbox; il dispatcher serializzato invia `POST /positions`,
   salva `dealReference`, poi interroga `GET /confirms/{dealReference}` → `Accepted`/`Rejected`.
7. **Post-trade:** Portfolio aggiorna esposizione ed equity; il risk engine rivaluta i limiti
   (es. perdita giornaliera) e può portare il job in `Halted`.
8. **Chiusura:** le posizioni si chiudono per stop/take profit lato broker, per segnale opposto,
   per scadenza dell'orizzonte (time stop) o per kill switch. La riconciliazione rileva le
   chiusure avvenute lato broker.

### 5.4 Parametri del Trading Job (sintesi)

Definiti formalmente nello schema JSON. Gruppi principali:

- **universe:** epic, risoluzione, orizzonte di previsione, lunghezza del contesto.
- **model:** famiglia (`timesfm-3`, `timesfm-2.5`, `statsforecast-ets`…), versione, quantili,
  covariate.
- **signal:** strategia, soglia di rendimento atteso, confidenza minima, consenso fra modelli.
- **risk:** profilo (`conservative|balanced|aggressive` come preset), rischio per trade (% equity),
  perdita giornaliera massima, drawdown massimo dal picco, leva massima, esposizione massima per
  strumento e totale, numero massimo di posizioni, stop obbligatorio (tipo e distanza minima),
  stop garantito sì/no.
- **operationalLimits:** finestre orarie, giorni, blocco a ridosso di eventi macro, numero
  massimo di ordini al giorno, cooldown dopo una perdita, durata massima della posizione,
  capitale allocato al job.
- **mode:** `backtest | paper | live` (live richiede abilitazione esplicita, ADR-0005).

I preset di profilo sono solo valori di partenza: i valori effettivi sono sempre espliciti e
versionati (ogni modifica crea una nuova `ParameterSetVersion`; le decisioni referenziano la
versione usata).

## 6. Contratti

| Confine | Stile | Contratto |
| --- | --- | --- |
| Core ↔ Forecasting | gRPC sincrono, unario, deadline | `contracts/proto/forecasting/v1/forecasting.proto` |
| Utente/Dashboard ↔ Core | REST/JSON + SignalR | `contracts/openapi/management-api.v1.yaml` |
| Core → Notifiche/Analytics | Eventi di integrazione (outbox → dispatcher; broker in futuro) | `contracts/asyncapi/integration-events.v1.yaml` |
| Parametri job | JSON Schema 2020-12 | `contracts/schemas/trading-job-parameters.v1.schema.json` |
| Core ↔ Capital.com | REST + WebSocket esterni | Incapsulati nell'ACL; DTO generati dalla Swagger ufficiale e coperti da contract test su Demo |

Regole: modifiche solo additive nelle versioni `v1`; breaking change → `v2` con coesistenza.
Gli eventi di dominio interni ai moduli non sono contratti; lo sono solo gli eventi di
integrazione elencati nell'AsyncAPI.

## 7. Dati

- **Schemi per modulo** nello stesso database (`market`, `strategy`, `risk`, `execution`,
  `portfolio`, `backtest`, `infra`): nessun modulo legge le tabelle di un altro (test di
  architettura + permessi per ruolo DB per schema).
- **Serie temporali:** hypertable TimescaleDB `market.candles(epic, resolution, ts, o,h,l,c bid/ask, volume)`
  con compressione dopo 7 giorni e continuous aggregate per le risoluzioni superiori.
- **Previsioni:** `forecasting.forecasts` con input fingerprint (hash della finestra), modello e
  versione, quantili, e successivamente l'errore realizzato (per le metriche di accuratezza).
- **Decision log** append-only (`strategy.decisions`): snapshot di input, segnale, esito rischio,
  ordine. Base per audit e riproducibilità (RF-12). Non è event sourcing dell'intero sistema.
- **Outbox/Inbox:** `infra.outbox_messages`, `infra.inbox_messages` (ADR-0003).
- **Concorrenza:** versioni ottimistiche (`xmin`/rowversion) sugli aggregati; un solo esecutore di
  ordini attivo (advisory lock) evita lock distribuiti.
- **Migrazioni:** EF Core migrations eseguite come step della pipeline (bundle), sempre
  expand/contract.

## 8. Resilienza

| Dipendenza | Timeout | Retry | Circuit breaker | Degradazione |
| --- | --- | --- | --- | --- |
| Capital.com lettura (prezzi, mercati, posizioni) | 5 s | 3, backoff esponenziale + jitter, solo GET | Sì, per host | Ciclo saltato; nessuna apertura |
| Capital.com apertura posizione/ordine | 10 s | **Nessuno** automatico | Sì | Stato `Unknown` → riconciliazione immediata |
| Capital.com chiusura/modifica | 10 s | Sì dopo riconciliazione (operazione verificabile su `dealId`) | Sì | Escalation + notifica; stop lato broker restano attivi |
| Capital.com sessione | 5 s | Rispettando 1 req/s | — | Pausa globale delle aperture |
| WebSocket prezzi | ping 5 min | Riconnessione con backoff | — | Fallback polling REST entro il budget di 10 req/s |
| Forecasting gRPC | deadline 3 s | 1 retry (idempotente) entro la deadline | Sì | `NoSignal` |
| PostgreSQL | 2 s comandi | Retry su errori transitori (Npgsql) | — | Worker si ferma in modo sicuro (fail-closed) |

Rate limiting in uscita: token bucket condiviso nell'ACL (10/s globali, 10/s per aperture con
spaziatura ≥ 100 ms, contatore orario per il Demo). Il backpressure si propaga al ciclo
decisionale (priorità: chiusure > modifiche stop > aperture > letture non critiche).

## 9. Sicurezza

- Credenziali Capital.com (API key, login, password) in vault (Azure Key Vault/HashiCorp Vault);
  caricate a runtime, mai su disco o log. API key separate per Demo e Live.
- Interfaccia di gestione: OIDC (Entra ID) con MFA; ruoli `viewer`, `operator`, `risk-admin`.
  Il passaggio a `live` e l'aumento dei limiti di rischio richiedono `risk-admin` + conferma
  di un secondo utente (four-eyes) in v1.1; in v1 almeno conferma esplicita e audit.
- Comunicazione interna gRPC su rete privata con mTLS (o almeno TLS + token di servizio).
- Threat model STRIDE da completare (task T-14). Rischi principali: furto di credenziali del
  broker (impatto finanziario diretto), manipolazione dei parametri di rischio, replay di
  comandi, dati di mercato avvelenati.
- Supply chain: SBOM, scansione dipendenze (.NET e Python), immagini firmate; pesi dei modelli
  scaricati da sorgente fissata e verificati per hash.

## 10. Osservabilità

- OpenTelemetry in .NET e Python, propagazione W3C Trace Context via gRPC e negli header dei
  messaggi in outbox. Una traccia = un ciclo decisionale (candela → ordine → conferma).
- Metriche principali:
  - tecniche: `majordomo.decision_cycle.duration`, `capital.api.requests{endpoint,status}`,
    `capital.api.rate_limit.wait`, `forecast.latency`, `outbox.lag`, `reconciliation.drift`;
  - di dominio: equity, drawdown corrente, esposizione per strumento, P&L giornaliero per job,
    ordini rifiutati dal risk engine per motivo, accuratezza previsioni (MASE, copertura q10–q90).
- SLO v1:
  - 99% dei cicli decisionali completati (anche come `NoSignal` motivato) entro 5 s;
  - 99,9% degli ordini con esito (accettato/rifiutato) noto entro 30 s;
  - drift di riconciliazione = 0 per il 99,9% delle verifiche.
- Alert su burn rate degli SLO e su eventi critici: kill switch attivato, job `Halted`,
  posizione senza stop rilevata, sessione broker non rinnovabile.
- Log strutturati con `traceId`, `jobId`, `epic`, `dealReference`; mai credenziali o token.

## 11. Strategia di test

| Livello | Strumenti | Copertura attesa |
| --- | --- | --- |
| Unit | xUnit, FluentAssertions/Shouldly; pytest | Risk policy (tabelle di casi), sizing, strategie di segnale, macchina a stati degli ordini |
| Property-based | FsCheck / CsCheck | Invarianti di rischio: nessuna combinazione di input produce size > limite o ordine senza stop |
| Mutation | Stryker.NET | Modulo Risk (soglia ≥ 80% mutanti uccisi) |
| Integrazione | Testcontainers (PostgreSQL/Timescale), WebApplicationFactory, WireMock.Net per Capital.com | Outbox, riconciliazione, rate limiter, ACL |
| Contratto | Buf breaking-check per il `.proto`; verifica OpenAPI/AsyncAPI in CI; test notturni su **conto Demo** reale | Compatibilità contratti e DTO broker |
| Architettura | NetArchTest/ArchUnitNET | Nessuna dipendenza fra moduli fuori dalle API pubbliche; dominio senza riferimenti infrastrutturali |
| Backtest come test | Scenari storici "dorati" (crash, gap, illiquidità) | Il risk engine deve fermare il job entro i limiti dichiarati |
| Resilienza | Fault injection (Polly chaos / Simmy) | Timeout broker, conferma persa, WebSocket chiuso |
| Carico | NBomber/k6 | 50 job con risoluzione 1 min entro gli SLO senza superare i rate limit |

## 12. Rilascio e ambienti

- Trunk-based, PR obbligatorie, Conventional Commits, SemVer; pipeline di riferimento in
  `deploy/ci-reference.yml`.
- Un'immagine per `majordomo` (api/worker) e una per `forecasting-svc`, promosse invariate fra
  ambienti `dev` → `paper` → `live`.
- Ambiente `paper` = conto Demo Capital.com, sempre attivo: ogni versione vi gira almeno 2
  settimane prima della promozione a `live` (ADR-0006).
- Deployment v1: Docker Compose o Azure Container Apps su un singolo ambiente per stadio; nessun
  Kubernetes finché il numero di servizi resta 2.
- IaC con OpenTofu/Bicep; migrazioni DB come step controllato.
- Deploy del worker in finestre senza posizioni aperte oppure con shutdown graceful: il nuovo
  worker riconcilia all'avvio prima di riprendere i cicli.

## 13. Rischi

| # | Rischio | Prob. | Impatto | Mitigazione |
| --- | --- | --- | --- | --- |
| R1 | Le previsioni non hanno potere predittivo netto dei costi | Alta | Alto | Backtest walk-forward obbligatorio, baseline classiche, promozione basata su metriche (ADR-0006) |
| R2 | Doppia apertura per retry su errore ambiguo | Media | Alto | Nessun retry sulle aperture, stato `Unknown` + riconciliazione |
| R3 | Superamento rate limit → ordini rifiutati | Media | Medio | Token bucket centralizzato e priorità |
| R4 | Gap di mercato oltre lo stop | Media | Alto | Stop garantito opzionale, limiti di esposizione, blocco su eventi macro e weekend |
| R5 | Compromissione credenziali broker | Bassa | Molto alto | Vault, rotazione, IP allowlist se disponibile, alert su attività non originate dal sistema |
| R6 | Licenza pesi TimesFM 3.0 incompatibile | Media | Medio | Verifica legale; fallback 2.5 (Apache-2.0) o BigQuery `AI.FORECAST` |
| R7 | Vincoli regolamentari/fiscali sul trading automatico | Media | Alto | Revisione legale prima del Live; v1 solo conto proprio |
| R8 | Cambiamenti non annunciati delle API Capital.com | Media | Medio | ACL, contract test notturni su Demo, monitoraggio errori per endpoint |

## 14. Alternative considerate (sintesi)

- **Microservizi per bounded context** — scartato in v1: complessità operativa senza bisogno di
  scalabilità indipendente; i confini restano pronti per un'estrazione (ADR-0001).
- **Tutto in Python** — scartato: possibile, ma il core transazionale (stato ordini, outbox,
  concorrenza, tipizzazione forte) è più robusto in .NET con le competenze dell'organizzazione;
  Python resta dove porta valore reale (modelli) (ADR-0002).
- **Kafka/RabbitMQ dal primo giorno** — rinviato: volumi bassi, un solo consumatore esterno;
  outbox su PostgreSQL basta (ADR-0003).
- **BigQuery `AI.FORECAST` al posto del servizio Python** — valida come opzione "managed"
  (zero GPU), ma introduce lock-in, latenza e costi per query; mantenuta come adattatore
  alternativo dietro lo stesso contratto gRPC.

## 15. Domande aperte

| # | Domanda | Chi decide |
| --- | --- | --- |
| D1 | Licenza dei pesi TimesFM 3.0 compatibile con uso commerciale interno? | Legale / CEO |
| D2 | Capitale iniziale, profilo di rischio e perdita massima accettabile per il primo Live? | Utente / CEO |
| D3 | Cloud target (Azure Container Apps vs VPS attuale) e disponibilità GPU? | CEO / DevOps |
| D4 | Classi di asset in perimetro (indici, forex, materie prime, crypto, azioni)? | Utente |
| D5 | Requisiti di reporting fiscale/contabile? | Amministrazione |
| D6 | Canale notifiche: Teams via agente bridge è sufficiente? | CEO |
