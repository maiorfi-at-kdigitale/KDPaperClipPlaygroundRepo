# ADR-0005 — Risk engine come gate obbligatorio, kill switch e Demo-first

- Stato: Proposto
- Data: 2026-10-04

## Contesto

L'obiettivo di business è far crescere il capitale, ma il sistema opera con leva su CFD sulla
base di previsioni incerte. Un errore software (loop di aperture, sizing errato, dati corrotti)
può causare perdite rapide e superiori a quelle di mercato. Il rischio deve essere governato da
regole deterministiche, separate dalla logica che genera i segnali.

## Decisione

1. **Gate obbligatorio:** nessun ordine di apertura raggiunge l'ACL senza un `RiskAssessment.Approved`
   persistito nella stessa transazione; l'ACL rifiuta comandi di apertura privi di riferimento
   a una valutazione approvata (difesa in profondità).
2. **Funzione pura e deterministica** (`PreTradeRiskEvaluator`, vedi `domain/reference/TradingJob.cs`)
   condivisa fra backtest, paper e live; copertura con test a tabella, property-based e mutation
   testing.
3. **Limiti a tre livelli:** per trade (rischio %, stop obbligatorio, distanza minima), per job
   (perdita giornaliera, drawdown sull'allocazione, numero ordini/posizioni, cooldown), di
   portafoglio (esposizione totale, leva effettiva, drawdown sul conto). Il più restrittivo vince.
4. **Stop loss sempre lato broker** (inviato con l'apertura), così le protezioni sopravvivono a
   un'indisponibilità della piattaforma. Stop garantito configurabile per strumenti soggetti a gap.
5. **Halt automatico** del job al raggiungimento dei limiti; ripartenza solo manuale e motivata,
   verso `Paused` (mai direttamente `Live`).
6. **Kill switch** globale e per job, via API e da runbook (anche con script indipendente dal
   worker che usa direttamente l'API Capital.com per chiudere tutto).
7. **Demo-first:** ambiente predefinito Demo; Live attivabile solo con (a) flag di configurazione
   dell'ambiente, (b) ruolo `risk-admin`, (c) evidenze di promozione (ADR-0006), (d) limiti di
   capitale allocato espliciti. Tutte le modifiche che aumentano il rischio sono auditate.
8. **Fail-closed:** qualsiasi incertezza (dati incompleti, forecast assente, ordine `Unknown`,
   riconciliazione in errore, DB non raggiungibile) blocca le aperture; chiusure e riduzioni di
   rischio restano sempre permesse.

## Alternative considerate

1. **Rischio incorporato in ogni strategia** — più flessibile ma non verificabile in modo
   uniforme; un bug di strategia diventa un bug di rischio.
2. **Affidarsi solo ai controlli del broker (margine, stop out)** — protegge il broker, non gli
   obiettivi dell'utente: interviene troppo tardi.
3. **Approvazione umana di ogni ordine** — massima sicurezza, ma vanifica l'automazione; resta
   come modalità opzionale (`requireManualApproval`) per i primi giorni di Live.

## Conseguenze

- Positive: perdita massima limitata per costruzione e misurabile; separazione chiara delle
  responsabilità; stesse regole in simulazione e produzione.
- Negative: alcune opportunità vengono scartate (falsi negativi del gate); più parametri da
  configurare (mitigato da preset di profilo); il modulo Risk diventa il componente più critico
  e richiede la massima copertura di test.
