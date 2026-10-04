# ADR-0006 — Promozione delle strategie: backtest → paper (Demo) → Live

- Stato: Proposto
- Data: 2026-10-04

## Contesto

Il valore dei modelli di forecasting zero-shot per il trading non è garantito (rischio R1). Le
metriche di accuratezza statistica (MAE, MASE) non implicano profitto netto dopo spread,
finanziamento overnight e slippage. Serve un processo ripetibile e misurabile per decidere se un
Trading Job può usare capitale reale.

## Decisione

Ogni `TradingJob` attraversa tre stadi con criteri espliciti, registrati come `PromotionEvidence`:

1. **Backtest walk-forward** sullo stesso codice di segnale e rischio:
   - almeno 2 anni di storico alla risoluzione del job (o il massimo disponibile), finestre
     rolling senza look-ahead (il contesto del modello termina sempre alla candela chiusa);
   - costi realistici: spread bid/ask storico, finanziamento overnight, slippage configurabile;
   - confronto con baseline (buy & hold, ETS/ARIMA, segnale casuale con lo stesso rischio);
   - criteri minimi suggeriti (da confermare con il business): profit factor > 1,2, max drawdown
     < limite della policy, Sharpe > baseline, almeno 100 trade, risultato stabile su più
     sotto-periodi.
2. **Paper trading su conto Demo Capital.com** per almeno 2 settimane e 30 trade, con
   esecuzione reale: lo scostamento fra risultati paper e backtest dello stesso periodo deve
   restare entro una tolleranza (es. ±30% sul P&L per trade), altrimenti il simulatore è
   ottimistico e va corretto.
3. **Live** con capitale allocato ridotto (es. 10% dell'allocazione prevista per il primo mese),
   aumentato a scaglioni solo se le metriche restano entro le bande del paper.

Retrocessione automatica: se in Live le metriche escono dalle bande (es. drawdown > 70% del
limite, hit rate sotto la banda per 2 settimane) il job torna `Paused` con notifica.

## Alternative considerate

1. **Solo backtest** — rapido ma esposto a overfitting e a differenze di esecuzione reale.
2. **Solo paper trading** — realistico ma lento per esplorare molte configurazioni.
3. **Ottimizzazione automatica dei parametri (grid/bayesian)** — utile, ma aumenta il rischio di
   overfitting; ammessa solo nello stadio di backtest con validazione out-of-sample.

## Conseguenze

- Positive: decisioni di allocazione basate su evidenze versionate e riproducibili; protezione
  dall'overfitting; chiaro punto di controllo per il business.
- Negative: time-to-live più lungo (minimo ~3–4 settimane per job); il simulatore di backtest è
  un componente da mantenere fedele all'esecuzione reale.
