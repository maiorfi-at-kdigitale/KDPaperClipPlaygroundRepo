# ADR-0004 — Gateway Capital.com come Anti-Corruption Layer con rate limiting e riconciliazione

- Stato: Proposto
- Data: 2026-10-04

## Contesto

Capital.com espone REST e WebSocket con vincoli stringenti: sessione con token `CST` /
`X-SECURITY-TOKEN` che scade dopo 10 minuti di inattività; `POST /session` limitato a 1 req/s;
10 req/s per utente; aperture di posizioni/ordini al massimo 1 ogni 0,1 s; sul Demo 1000
aperture/ora; WebSocket con massimo 40 strumenti e ping almeno ogni 10 minuti. L'esito di un
ordine è asincrono (`dealReference` → `GET /confirms/{dealReference}`) e **non esiste una chiave
di idempotenza** per le aperture. Il modello dati del broker (epic, dealId, livelli) non deve
contaminare il dominio.

## Decisione

Un modulo `Majordomo.CapitalCom` che è l'**unico** punto di contatto con il broker:

1. **SessionManager:** crea la sessione (password cifrata con la chiave di `GET /session/encryptionKey`),
   conserva i token in memoria, `GET /ping` periodico (< 10 min), rinnovo su 401 con singolo
   flight (nessuna tempesta di `POST /session`), selezione esplicita dell'account (`PUT /session`
   solo all'avvio, perché interrompe lo streaming).
2. **RateLimiter centralizzato:** token bucket globale 10/s (margine operativo: 8/s), corsia
   dedicata alle aperture con spaziatura ≥ 100 ms, contatore orario per Demo; code a priorità
   `Close > AmendStop > Open > Read`.
3. **Client REST** tipizzati (DTO generati dalla Swagger ufficiale, mappati in tipi di dominio),
   pipeline di resilienza per tipo di operazione: retry con backoff + jitter solo per letture e
   per chiusure/modifiche dopo verifica dello stato; **nessun retry automatico per le aperture**.
4. **Streaming WebSocket** (`marketData.subscribe`, `OHLCMarketData.subscribe`) per al massimo 40
   epic; gli altri strumenti in polling REST entro il budget. Riconnessione con backoff e
   backfill REST dei buchi.
5. **Riconciliazione:** all'avvio e ogni 30 s (oltre che su ogni esito `Unknown`) confronta
   `GET /positions`, `GET /workingorders`, `GET /accounts` e `GET /history/activity` con lo stato
   locale; corregge le divergenze spiegabili (chiusure per stop lato broker) e allerta le altre
   (es. posizione non originata dal sistema).
6. **Ambiente** Demo/Live come configurazione, con guardia: il profilo Live richiede un flag di
   abilitazione esplicito e una API key diversa (ADR-0005).

## Alternative considerate

1. **Uso diretto dei client HTTP nei moduli** — meno codice, ma rate limit non coordinati,
   gestione della sessione duplicata e accoppiamento del dominio al modello del broker.
2. **Capital.com MCP server** — utile per esplorazione assistita da AI, non adatto al percorso
   critico (nessuna garanzia su latenza, controllo dei retry e audit).
3. **Libreria client di terze parti** — riduce il lavoro iniziale ma introduce dipendenza da
   manutentori esterni sul componente più critico; valutabile solo come generatore di DTO.

## Conseguenze

- Positive: rispetto dei limiti per costruzione; un solo posto per sicurezza delle credenziali,
  logging e metriche per endpoint; possibilità futura di aggiungere un secondo broker dietro le
  stesse porte (`IMarketDataPort`, `IExecutionPort`, `IAccountPort`).
- Negative: componente di complessità non banale (sessione, streaming, riconciliazione) da
  coprire con test di integrazione (WireMock.Net) e test notturni sul conto Demo reale; i
  vincoli del broker possono cambiare senza preavviso (monitoraggio errori 4xx/429 per endpoint).
