# Casi d'uso funzionali end-to-end con la collezione Postman Majordomo

Questo documento collega la narrativa di dominio di **Capital-Majordomo** alle richieste disponibili nella collezione `tools/postman/Majordomo.postman_collection.json`.

Riferimenti principali usati:
- `docs/capital-majordomo/rfc/RFC-001-capital-majordomo.md`
- `docs/capital-majordomo/domain/domain-model.md`
- `docs/capital-majordomo/adr/0005-risk-engine-hard-gate-kill-switch.md`
- `docs/capital-majordomo/adr/0006-backtesting-paper-trading-promotion.md`
- `docs/capital-majordomo/contracts/openapi/management-api.v1.yaml`

---

## Caso d'uso 1 — Dal setup del TradingJob alla promozione con evidenza di backtest

### Scenario di business
- **Attore**: operatore (ruolo `operator`), con successivo eventuale intervento `risk-admin`.
- **Obiettivo**: configurare un `TradingJob`, validarlo tecnicamente, avviare un backtest e promuoverlo seguendo il gate di evidenza.
- **Precondizioni**:
  - ambiente locale attivo (`Majordomo.Api`, `Majordomo.Worker`, servizi `deploy/local`);
  - environment Postman `Majordomo - Local` selezionato;
  - header dev abilitati (`X-Dev-User`, `X-Dev-Roles`).
- **Concetti di dominio coinvolti**: `TradingJob`, `ParameterSetVersion`, `ETag` (concorrenza ottimistica), `Operation` (backtest), policy di promozione con `evidenceOperationId`.

### Flusso operativo Postman (narrativa ↔ request)

1. **Creazione del job in stato Draft**
   - Request: `Jobs (Strategy) / Crea job`
   - Metodo/endpoint: `POST {{baseUrl}}/v1/jobs`
   - Variabili/parametri rilevanti:
     - body con `parameters` (es. `universe`, `model`, `signal`, `risk`, `operationalLimits`, `mode`);
     - `Idempotency-Key: {{$guid}}`.
   - Risultato atteso:
     - **Dominio**: nuovo `TradingJob` in `Draft` con `ParameterSetVersion = 1`;
     - **Tecnico**: `201`, body con `id` ed `etag`; i test Postman salvano `jobId` e `etag` nelle collection variables.

2. **Controllo stato e acquisizione ETag aggiornato**
   - Request: `Jobs (Strategy) / Dettaglio job`
   - Metodo/endpoint: `GET {{baseUrl}}/v1/jobs/{{jobId}}`
   - Variabili rilevanti: `jobId` valorizzato dallo step precedente.
   - Risultato atteso:
     - **Dominio**: lettura dello stato corrente del job e del set parametri attivo;
     - **Tecnico**: `200`, header `ETag`; test Postman aggiorna variabile `etag`.

3. **Aggiornamento parametri con controllo concorrenza**
   - Request: `Jobs (Strategy) / Sostituisci parametri (If-Match)`
   - Metodo/endpoint: `PUT {{baseUrl}}/v1/jobs/{{jobId}}/parameters`
   - Variabili/parametri rilevanti:
     - header `If-Match: {{etag}}`;
     - body completo parametri (es. modifica `operationalLimits.maxOrdersPerDay`).
   - Risultato atteso:
     - **Dominio**: creazione nuova `ParameterSetVersion` (incremento versione parametri) se stato job modificabile;
     - **Tecnico**: `200`, nuovo `ETag` in risposta; test Postman aggiorna `etag`.

4. **Avvio backtest asincrono**
   - Request: `Backtesting / Avvia backtest`
   - Metodo/endpoint: `POST {{baseUrl}}/v1/jobs/{{jobId}}/backtests`
   - Variabili/parametri rilevanti: body con `from`, `to`, `slippagePoints`.
   - Risultato atteso:
     - **Dominio**: creazione `Operation` di tipo backtest in coda;
     - **Tecnico**: `202`, body con `id`; test Postman salva `operationId`.

5. **Verifica esito operazione**
   - Request: `Operations / Stato operazione`
   - Metodo/endpoint: `GET {{baseUrl}}/v1/operations/{{operationId}}`
   - Variabili rilevanti: `operationId` dallo step backtest.
   - Risultato atteso:
     - **Dominio**: operazione backtest passa a `running/succeeded/failed`;
     - **Tecnico**: test accetta `200` (trovata) o `404` (id non ancora valido/non trovato).

6. **Promozione del job con evidenza**
   - Request: `Jobs (Strategy) / Transizione - promote (con evidenza backtest)`
   - Metodo/endpoint: `POST {{baseUrl}}/v1/jobs/{{jobId}}/transitions`
   - Variabili/parametri rilevanti:
     - body: `{"action":"promote","reason":"Backtest superato","evidenceOperationId":"{{operationId}}"}`.
   - Risultato atteso:
     - **Dominio**: transizione consentita solo se l'evidenza è valida (ADR-0006);
     - **Tecnico**: test accetta `202`, `409` o `422` a seconda dello stato job/evidenza.

### Copertura mancante o parziale nella collezione
- Non c'è una request dedicata al caso **`412 Precondition Failed`** (ETag non corrispondente) su `PUT /v1/jobs/{jobId}/parameters`; è coperto solo il caso senza `If-Match` (`428`).
- Non c'è una request dedicata a promozione **PaperTrading → Live con ruolo risk-admin**; la transizione può essere provata riusando la request di promote cambiando `X-Dev-Roles`, ma non è esplicitata come scenario separato.

---

## Caso d'uso 2 — Intervento di emergenza con kill switch e ripristino controllato

### Scenario di business
- **Attore**: operatore per attivazione, `risk-admin` per rilascio/ripresa.
- **Obiettivo**: fermare immediatamente operatività rischiosa (globale o per job) e ripristinare solo con governance appropriata.
- **Precondizioni**:
  - almeno un job in stato operativo (`Backtesting`, `PaperTrading`, `Live` o `Paused`) per osservare l'effetto di halt;
  - `jobId` disponibile per lo scope `job`.
- **Concetti di dominio coinvolti**: `KillSwitch` (scope `job`/`global`), `Operation` asincrona di kill switch, stato job `Halted`, regola di autorizzazione `risk-admin` su release.

### Flusso operativo Postman (narrativa ↔ request)

1. **Verifica stato iniziale kill switch**
   - Request: `Risk - kill switch / Stato kill switch`
   - Metodo/endpoint: `GET {{baseUrl}}/v1/kill-switch`
   - Risultato atteso:
     - **Dominio**: stato corrente (`active`, `scope`, eventuali `jobIds`);
     - **Tecnico**: `200`.

2. **Attivazione kill switch per singolo job**
   - Request: `Risk - kill switch / Attiva kill switch (job)`
   - Metodo/endpoint: `POST {{baseUrl}}/v1/kill-switch`
   - Variabili/parametri rilevanti:
     - body con `scope: "job"`, `jobId: "{{jobId}}"`, `reason`, `closePositions`.
   - Risultato atteso:
     - **Dominio**: kill switch attivo sul job indicato; creata operazione di tipo kill-switch;
     - **Tecnico**: `202`.

3. **(Alternativa) attivazione kill switch globale**
   - Request: `Risk - kill switch / Attiva kill switch (globale)`
   - Metodo/endpoint: `POST {{baseUrl}}/v1/kill-switch`
   - Parametri rilevanti: body con `scope: "global"`, `reason`, `closePositions: true`.
   - Risultato atteso:
     - **Dominio**: blocco trading per tutti i job;
     - **Tecnico**: `202`.

4. **Rilascio kill switch con ruolo risk-admin**
   - Request: `Risk - kill switch / Rilascia kill switch (risk-admin)`
   - Metodo/endpoint: `POST {{baseUrl}}/v1/kill-switch/release`
   - Parametri rilevanti: body con `reason`.
   - Risultato atteso:
     - **Dominio**: kill switch disattivato (i job non vengono automaticamente ripromossi);
     - **Tecnico**: `200`.

5. **Controllo autorizzazione negativa su release**
   - Request: `Risk - kill switch / Rilascia kill switch - operator (403)`
   - Metodo/endpoint: `POST {{baseUrl}}/v1/kill-switch/release`
   - Risultato atteso:
     - **Dominio**: enforcement della segregazione ruoli su operazioni ad alto rischio;
     - **Tecnico**: `403`.

6. **Ripresa del job fermato (se applicabile)**
   - Request: `Jobs (Strategy) / Transizione - resume (risk-admin)`
   - Metodo/endpoint: `POST {{baseUrl}}/v1/jobs/{{jobId}}/transitions`
   - Parametri rilevanti: action `resume`.
   - Risultato atteso:
     - **Dominio**: da `Halted` il job torna tipicamente in `Paused` (poi può essere ulteriormente promosso);
     - **Tecnico**: test accetta `202`, `409`, `422` in base allo stato corrente.

### Copertura mancante o parziale nella collezione
- Non c'è una request esplicita di **verifica stato job post-kill-switch** filtrata a `Halted` come check finale obbligatorio; si può usare `Jobs (Strategy) / Lista job per stato` modificando il query param `status`, ma non è preconfigurata su `Halted`.
- La chiusura effettiva posizioni (`closePositions: true`) è indicata dal dominio operativo ma nello scheletro backend è marcata `TODO`; la collezione non offre una verifica dedicata di close-out.

---

## Caso d'uso 3 — Audit operativo: decisioni, posizioni, performance e dettaglio ordine

### Scenario di business
- **Attore**: utente con ruolo `viewer` (monitoraggio) e operatore/risk-admin per azioni correttive.
- **Obiettivo**: controllare cosa ha deciso il sistema, quali esposizioni risultano aperte e quale report operativo viene prodotto nel periodo.
- **Precondizioni**:
  - `jobId` valorizzato da creazione job;
  - idealmente ciclo worker attivo per generare decisioni/ordini.
- **Concetti di dominio coinvolti**: `Decision` (log audit), `Position`, `Order`, metriche performance, relazione tra esecuzione e rischio.

### Flusso operativo Postman (narrativa ↔ request)

1. **Lettura decision log del job**
   - Request: `Jobs (Strategy) / Decisioni (paginazione a cursore)`
   - Metodo/endpoint: `GET {{baseUrl}}/v1/jobs/{{jobId}}/decisions?limit=50`
   - Parametri rilevanti: `limit`, eventuale `cursor` per paginazione.
   - Risultato atteso:
     - **Dominio**: elenco decisioni con outcome (es. no-signal/rejected/accepted), riferimenti a segnale e motivazioni;
     - **Tecnico**: `200`, payload con `items` e `nextCursor`.

2. **Vista posizioni aperte**
   - Request: `Portfolio / Execution / Posizioni`
   - Metodo/endpoint: `GET {{baseUrl}}/v1/positions`
   - Risultato atteso:
     - **Dominio**: snapshot posizioni correnti, con possibile correlazione `JobId`↔`dealId`;
     - **Tecnico**: `200`, array di `Position`.

3. **Report performance del periodo**
   - Request: `Portfolio / Execution / Report performance`
   - Metodo/endpoint: `GET {{baseUrl}}/v1/reports/performance?jobId={{jobId}}&from=...&to=...`
   - Parametri rilevanti: `jobId`, `from`, `to`.
   - Risultato atteso:
     - **Dominio**: riepilogo KPI periodo (trade count e metriche del report nello stato corrente dello scheletro);
     - **Tecnico**: `200`.

4. **Drill-down su ordine specifico**
   - Request: `Portfolio / Execution / Dettaglio ordine`
   - Metodo/endpoint: `GET {{baseUrl}}/v1/orders/{{orderId}}`
   - Variabili rilevanti: `orderId` da valorizzare manualmente.
   - Risultato atteso:
     - **Dominio**: dettaglio ciclo di vita di un ordine (se esiste);
     - **Tecnico**: test accetta `200` o `404` (nessun ordine corrispondente).

### Copertura mancante o parziale nella collezione
- Non c'è una request che **genera deterministicamente un ordine** via API sincrona: la produzione ordini dipende dal ciclo asincrono del worker (`DecisionCycleService`) e dai gateway esterni/mock.
- `orderId` non viene popolato automaticamente dalla collezione: senza un id reale, il caso `Dettaglio ordine` resta spesso su percorso tecnico `404` (atteso dal test).

---

## Sintesi copertura complessiva della collezione

- La collezione copre tutti gli endpoint `v1` definiti nel contratto `management-api.v1.yaml` (jobs, backtest, operations, kill switch, positions, reports, orders), più endpoint di supporto `health` e `dev`.
- I gap principali sono nella **codifica di scenari avanzati** (es. `412` su ETag mismatch, promote a Live esplicita con risk-admin, verifiche mirate post kill-switch, gestione automatica `orderId`) più che nella disponibilità di endpoint base.
