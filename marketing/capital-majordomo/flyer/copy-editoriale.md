# Copy flyer — Capital-Majordomo

## Direzioni creative

1. **Esplora il trading automatico.** Focus sulla scoperta del progetto.
2. **Dall’idea alla prova.** Focus sul percorso di sperimentazione.
3. **Un laboratorio per le strategie.** Focus sullo stato playground; direzione sviluppata per questa consegna.

La terza direzione chiarisce lo stato del progetto e offre una headline breve, adatta a un flyer dominato dalla grafica. Le prime due restano alternative editoriali; non richiedono un passaggio di approvazione del piano.

## Testi da impaginare

**Nome**  
Capital-Majordomo

**Etichetta di stato**  
PLAYGROUND · IN SVILUPPO

**Headline**  
Un laboratorio per le strategie.

**Sottotitolo**  
Esplora un progetto di trading automatico pensato per integrarsi con le API di Capital.com.

**Proposta di valore**  
Una base sperimentale per studiare il percorso dai dati alle decisioni di trading, prima di sviluppare una soluzione completa.

**Tre punti chiave**

- **Parti dal progetto.** Architettura e percorso di sviluppo documentati.
- **Esplora in locale.** Ambiente di prova con risposte simulate del broker.
- **Segui l’evoluzione.** Previsioni e backtest oggi dimostrativi; funzionalità avanzate da sviluppare.

**CTA**  
Scopri il progetto nel README.

**Indicazione sotto la CTA**  
Repository KDPaperClipPlaygroundRepo · README.md

**Microtesto di trasparenza**  
Prototipo sperimentale. Previsioni senza modelli di intelligenza artificiale attivi e backtest con risultati sintetici. Non pronto per operare con denaro reale.

## Varianti brevi

Sostituzioni singole per valutazione grafica o test, senza sommarle al testo principale:

- Headline B: **Dall’idea alla prova.**
- Headline C: **Esplora il trading automatico.**
- CTA B: **Leggi il README del progetto.**
- Sottotitolo breve: **Un playground per esplorare il trading automatico e l’integrazione con Capital.com.**

Logica delle scelte: invito alla scoperta, stato sperimentale visibile e beneficio conoscitivo; nessuna promessa di performance o disponibilità commerciale.

## Note redazionali — NON inserire nel flyer

### Fonti e limiti delle affermazioni

| Testo / concetto | Fonte nel repository | Limite da preservare |
| --- | --- | --- |
| Playground e base sperimentale | `README.md`, sezioni “Backend Capital-Majordomo: avvio in locale” e “Playground con Postman” | È uno scheletro .NET con strumenti di sviluppo, non una piattaforma completa o un servizio pubblico. |
| Architettura e percorso di sviluppo documentati | `docs/capital-majordomo/README.md`; `docs/capital-majordomo/roadmap.md` | Il pacchetto architetturale è una proposta v0.1; la roadmap non certifica funzionalità consegnate. |
| Progetto di integrazione con le API di Capital.com | `README.md`; `docs/capital-majordomo/adr/0004-capital-com-anti-corruption-layer.md`; `src/Hosts/Majordomo.Api/appsettings.Development.json` | Capital.com è il broker destinatario dell’integrazione. La configurazione locale punta a WireMock, non a un conto reale. Le fonti non documentano partnership o approvazione da parte di Capital.com: non suggerirle con testo o impaginazione. |
| Risposte simulate del broker | `README.md`, servizio WireMock e istruzioni Postman; `src/Hosts/Majordomo.Api/appsettings.Development.json` | Distinguere il mock locale dal conto Demo Capital.com. Non dire “conto Demo già connesso” o “demo online disponibile”. |
| Previsioni dimostrative, senza modelli IA attivi | `README.md`, riga ForecastingStub; `tools/Majordomo.ForecastingStub/NaiveForecastingService.cs` | Lo stub usa un calcolo semplice. Le etichette TimesFM presenti nel codice non dimostrano un modello reale in esecuzione. |
| Backtest con risultati sintetici | `src/Modules/Backtesting/Majordomo.Backtesting/BacktestingModule.cs`, classe BacktestRunner | Il codice indica esplicitamente “nessuna simulazione reale”. Non usare metriche di questo modulo come risultati di investimento. |
| Non pronto per denaro reale | Stato skeleton/stub del `README.md`, backtest sintetici e fasi future in `docs/capital-majordomo/roadmap.md` | È una qualificazione editoriale prudente dello stato osservato, non una certificazione tecnica o normativa. |
| CTA al README | `README.md` effettivamente presente nel repository | Non sono necessari contatti, form, landing o demo pubbliche inventati. |

### Indicazioni per Maria

- Impaginare solo la sezione “Testi da impaginare”. Titoli dei campi e note restano fuori dal flyer.
- Mantenere leggibili etichetta di stato e microtesto di trasparenza: sono parte del copy, non note opzionali.
- Collegare la CTA al README effettivo nel repository di consegna. Questo documento non introduce URL pubblici o QR code non verificati. Per una versione stampata, conservare l’indicazione testuale del repository e del file finché non è disponibile un indirizzo verificato.
- Lo stile grafico richiesto non deve far apparire il flyer come materiale ufficiale di Capital.com. Nessun claim di affiliazione, partnership o endorsement è documentato.
- Evitare “IA predittiva disponibile”, “backtest realistici”, “rendimenti”, “capitale protetto”, “inizia a investire” o inviti ad aprire un conto.
- La consegna riguarda il documento interno di copy; impaginazione, eventuale approvazione alla diffusione e push sul branch `kdp-54-flyer` restano nel task padre.

### Verifica editoriale

Italiano, tre punti chiave, CTA verso una risorsa esistente, stato playground esplicito, distinzione fra mock e conto Demo, assenza di dati di rendimento e di promesse finanziarie. Verifica basata sulla lettura delle fonti e del codice: non è stata eseguita una demo né attestata l’operatività delle integrazioni.
