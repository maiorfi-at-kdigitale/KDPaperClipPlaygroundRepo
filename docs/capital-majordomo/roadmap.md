# Roadmap e backlog — Capital-Majordomo

Ordine pensato per ridurre prima i rischi più alti (R1 valore predittivo, R2 esecuzione
corretta). Stime indicative per 1–2 sviluppatori. I task sono proposte: la creazione dei task
Paperclip corrispondenti richiede conferma.

## Fase 0 — Spike di riduzione del rischio (2 settimane)

| ID | Spike | Domanda a cui risponde | Uscita |
| --- | --- | --- | --- |
| S1 | Capital.com Demo end-to-end | Sessione, rate limit, `POST /positions` + `GET /confirms`, comportamento su timeout, chiusure da stop visibili in `GET /history/activity`? | Nota tecnica + collezione HTTP + mapping WireMock registrati dal Demo |
| S2 | TimesFM 3.0 vs 2.5 vs ETS su 10 epic | Latenza CPU/GPU per batch, accuratezza (MASE, copertura quantili) su 15m/1h/1d, licenza pesi | Notebook + tabella risultati + raccomandazione modello predefinito |
| S3 | Backtest grezzo della strategia ExpectedReturnThreshold | Esiste un edge netto dei costi su almeno una combinazione strumento/risoluzione? | Report; **go/no-go** del progetto oltre la fase 1 |

## Fase 1 — Fondamenta (3–4 settimane)

| ID | Task | Criteri di accettazione | Dipende da |
| --- | --- | --- | --- |
| T-01 | Scheletro solution .NET 10 (struttura ADR-0001, CPM, analizzatori, Aspire AppHost, CI attiva) | Build verde in CI con `-warnaserror`; test di architettura bloccanti presenti | — |
| T-02 | Persistenza: PostgreSQL+Timescale, schemi per modulo, migrazioni, outbox/inbox | Test Testcontainers: scrittura atomica stato+outbox; dispatcher consegna at-least-once; inbox deduplica | T-01 |
| T-03 | Capital.com ACL: SessionManager, RateLimiter, client mercati/prezzi/conto | Test con WireMock su scadenza sessione, 429, priorità; nessun superamento di 10 req/s sotto carico | T-01, S1 |
| T-04 | Market Data: catalogo strumenti, backfill storico, streaming WebSocket ≤ 40 epic, candele | Backfill di 2 anni per 5 epic senza buchi non spiegati; riconnessione WebSocket testata | T-02, T-03 |
| T-05 | forecasting-svc Python + contratto gRPC + client .NET con resilienza | Contract test client/server; deadline e breaker verificati; `ListModels` usato come readiness | S2 |
| T-06 | Osservabilità di base (OTel .NET/Python, dashboard, alert iniziali) | Una traccia unica dal trigger del ciclo alla risposta del forecasting | T-01 |

## Fase 2 — Decisione e rischio (3 settimane)

| ID | Task | Criteri di accettazione | Dipende da |
| --- | --- | --- | --- |
| T-07 | Strategy: TradingJob, ParameterSetVersion, validazione JSON Schema, scheduler cicli | Stati e transizioni come da domain model; parametri non validi rifiutati con 422 | T-02 |
| T-08 | Strategie di segnale (3 iniziali) come funzioni pure | Test a tabella e golden test riproducibili da decision log | T-05, T-07 |
| T-09 | Risk engine (pre-trade, Halt, kill switch) | Property test: nessun ordine senza stop o oltre i limiti; Stryker ≥ 80% sul modulo | T-07 |
| T-10 | Backtesting walk-forward con costi realistici | Stesso codice Strategy/Risk; report con metriche ADR-0006; nessun look-ahead (test dedicato) | T-04, T-08, T-09 |

## Fase 3 — Esecuzione e paper trading (3 settimane)

| ID | Task | Criteri di accettazione | Dipende da |
| --- | --- | --- | --- |
| T-11 | Execution: ordini, macchina a stati, conferme, nessun retry sulle aperture | Fault injection: timeout su `POST /positions` → `Unknown` → riconciliato senza doppie aperture | T-03, T-09 |
| T-12 | Riconciliazione periodica e all'avvio | Drift rilevato e notificato entro 60 s; chiusure da stop broker riflesse in Portfolio | T-11 |
| T-13 | Management API (OpenAPI v1) + OIDC + ruoli + audit | Contract test sull'OpenAPI; operazioni a rischio crescente richiedono risk-admin | T-07, T-09 |
| T-14 | Threat model STRIDE + gestione segreti in vault + runbook kill switch indipendente | Revisione sicurezza completata; script di emergenza testato sul Demo | T-11 |
| T-15 | Notifiche (eventi di integrazione → Teams bridge) | Evento `JobHalted` arriva su Teams entro 1 min | T-02 |
| T-16 | Ambiente `paper` continuo su conto Demo | 2 settimane di esecuzione senza drift e con SLO rispettati | T-11…T-15 |

## Fase 4 — Live controllato (dopo decisione del business)

- Revisione legale/fiscale (R7) e risposta alle domande aperte D1–D6.
- Primo job Live con allocazione ridotta e `requireManualApproval` opzionale (ADR-0005/0006).
- Revisione post-lancio a 2 e 6 settimane: metriche DORA, SLO, performance vs paper.

## Fitness function continue

- Test di architettura: nessuna dipendenza fra moduli fuori dai `.Contracts`.
- `buf breaking` e lint OpenAPI/AsyncAPI bloccanti in CI.
- Test notturno sul conto Demo (sessione, prezzi, apertura/chiusura minima) per rilevare cambi API.
- SLO: ciclo decisionale p95 < 5 s, esito ordini noto entro 30 s nel 99,9% dei casi, drift = 0.
