# ADR-0002 — Servizio di forecasting in Python dietro contratto gRPC

- Stato: Proposto
- Data: 2026-10-04

## Contesto

I modelli di forecasting di riferimento (Google TimesFM 3.0, TimesFM 2.5, baseline
`statsforecast`) sono distribuiti come librerie Python (PyTorch/JAX) con pesi su Hugging Face.
L'inferenza richiede memoria e, per volumi alti, GPU; ha un ciclo di vita (aggiornamento modelli,
dipendenze ML) diverso dal core transazionale. Il core deve poter sostituire o combinare modelli
senza modifiche al proprio codice.

## Decisione

- Un servizio **`forecasting-svc`** in Python 3.12 (gestione dipendenze con `uv`, immagine
  container con pesi fissati per hash), stateless, esposto via **gRPC** con il contratto
  `contracts/proto/forecasting/v1/forecasting.proto`.
- Adattatori di modello dietro un'interfaccia comune (`timesfm-3`, `timesfm-2.5`,
  `statsforecast-ets`, `statsforecast-arima`, opzionale `bigquery-ai-forecast`): il core
  seleziona il modello per `ModelRef` dal job.
- Chiamate unarie con **deadline** (default 3 s), batch di più serie per richiesta, 1 retry
  entro la deadline (operazione idempotente), circuit breaker lato client .NET.
- Degradazione: se il forecasting non risponde il ciclo produce `NoSignal` — mai un trade senza
  previsione.
- Il servizio restituisce sempre `model_version` e `input_fingerprint` per la riproducibilità.
- OpenTelemetry Python con propagazione del contesto gRPC; metriche di latenza per modello e
  dimensione del batch.

## Alternative considerate

1. **Inferenza in-process in .NET (ONNX Runtime)** — un servizio in meno, ma conversione ONNX
   dei modelli non ufficiale/da validare per ogni release, perdita dell'ecosistema Python e degli
   aggiornamenti rapidi. Da rivalutare come ottimizzazione se lo spike lo rende fattibile.
2. **BigQuery `AI.FORECAST` (TimesFM 2.5 gestito)** — zero infrastruttura ML, ma lock-in su GCP,
   latenza e costo per query, dati di mercato da replicare su BigQuery. Mantenuto come adattatore
   opzionale dietro lo stesso contratto.
3. **REST/JSON invece di gRPC** — più semplice da ispezionare, ma payload numerici grandi e
   contratto meno tipizzato; gRPC è adeguato per una comunicazione interna fra due componenti
   controllati.
4. **Integrazione asincrona via coda** — disaccoppia nel tempo, ma il ciclo decisionale ha bisogno
   della risposta entro pochi secondi: una richiesta sincrona con deadline è più semplice e
   sufficiente.

## Conseguenze

- Positive: modelli aggiornabili senza toccare il core; scalabilità indipendente (GPU solo dove
  serve); confronto fra modelli (ensemble, A/B) gestito dal core tramite `ModelRef`.
- Negative: un secondo linguaggio e runtime da mantenere (build, scansioni, osservabilità
  equivalenti); immagini voluminose (pesi); dipendenza sincrona nel percorso decisionale
  (mitigata da deadline, breaker e fail-safe `NoSignal`).
- Rischio da chiudere: licenza dei pesi TimesFM 3.0 (domanda aperta D1); fallback 2.5 Apache-2.0.
- Costo di competenze dichiarato: serve almeno una persona con esperienza Python/ML per
  manutenzione e valutazione dei modelli.
