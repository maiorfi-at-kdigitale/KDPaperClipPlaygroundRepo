# Capital Majordomo — Copy coordinato

Versione 1.0 · 28 ottobre 2026 · Consegna editoriale per Maria e CEO

Stato: testi completi per revisione e impaginazione. Pubblicazione, invio e campagne richiedono approvazione separata. Nome editoriale: Capital Majordomo; nei sorgenti: Capital-Majordomo.

## 1. Brief e direzione editoriale (interno)

**Pubblico ipotizzato:** interlocutori business e potenziali partner interessati a una presentazione del progetto; nessuna offerta a investitori retail o proposta di gestione di capitali di terzi.

**Obiettivo:** ottenere un confronto sul progetto e sui requisiti di una possibile sperimentazione. **CTA comune:** «Richiedi una presentazione del progetto». Nessun link, contatto o appuntamento inventato.

**Messaggio principale:** Capital Majordomo è un progetto di piattaforma per orchestrare operazioni su CFD tramite le API Capital.com, collegando previsioni, regole configurabili e controlli operativi. Oggi dispone di un backend skeleton e di un playground locale; l'operatività reale resta da validare.

**Tre direzioni proposte:**

1. «Automazione, con regole esplicite.» Beneficio: rendere leggibili configurazione e passaggi del processo.
2. «Dalla previsione a un processo verificabile.» Beneficio: discutere il percorso tra dato, decisione e ordine.
3. «Valutare prima di operare.» Beneficio: mettere al centro sperimentazione e criteri di avanzamento.

**Direzione sviluppata:** 1, come proposta editoriale, senza attribuire una scelta o approvazione al committente. Le altre due restano varianti utilizzabili in revisione.

**Benefici sostenuti dalle fonti:** organizzare strumenti, strategia e limiti in unità configurabili; consultare esiti e motivazioni registrati dal ciclo decisionale; esplorare le API e i ruoli tramite casi d'uso locali. Sono capacità del prototipo, non risultati commerciali misurati. La valutazione completa del rischio dipende anche da dati e integrazioni ancora incompleti.

**Logica delle scelte:** un unico messaggio lega i tre canali; i benefici riguardano chiarezza del processo e possibilità di valutazione, con stato di sviluppo e limiti visibili nel copy.

## 2. Brochure — testo completo per due pagine

### Pagina 1

**Capital Majordomo**

# Automazione, con regole esplicite.

### Un progetto per collegare previsioni, decisioni e controlli nel trading su CFD.

Automatizzare una strategia richiede più di un segnale di mercato. Occorre definire su quali strumenti operare, con quali parametri e attraverso quali passaggi arrivare a un ordine.

Capital Majordomo nasce per organizzare questo processo. La piattaforma è progettata per orchestrare operazioni su CFD tramite le API Capital.com, collegando dati di mercato, previsioni su serie temporali, regole di strategia e controlli operativi.

### Una configurazione da discutere e verificare

L'unità di lavoro è il «Trading Job»: una configurazione che riunisce strumenti, modello di previsione, strategia, parametri di rischio e limiti operativi. Un riferimento comune per confrontare le scelte tra responsabili business e team tecnico.

### Un percorso leggibile

Il backend skeleton contiene un ciclo che collega dati, previsione, segnale, valutazione dei limiti e invio dell'ordine. Registra inoltre esiti e motivazioni delle decisioni, per consentirne la consultazione.

### Una base concreta per il confronto

Sono presenti API di gestione e casi d'uso Postman per esplorare il prototipo in locale. Questo permette di partire da configurazioni e flussi documentati quando si discutono i requisiti del progetto.

**Il punto di partenza è un prototipo tecnico.** Il playground locale usa risposte simulate del broker e un servizio di previsione semplificato, senza modelli di machine learning reali. Non dimostra risultati di investimento né un servizio pronto per l'uso Live.

**Scopri il perimetro del prototipo e il percorso previsto: continua a pagina 2.**

### Pagina 2

# Valutare il processo, prima dell'operatività reale.

### Cosa è presente nel prototipo

Il backend include la gestione dei Trading Job, il versionamento dei parametri e la consultazione delle decisioni. Nel codice sono presenti controlli preliminari all'ordine e un comando di blocco operativo, il «kill switch», con autorizzazioni distinte per attivazione e rilascio.

Queste componenti costituiscono una base di lavoro. Alcuni dati necessari ai controlli sono ancora incompleti; il blocco operativo non include ancora la chiusura automatica delle posizioni. I controlli descritti non garantiscono l'assenza di perdite.

### Cosa si può esplorare in locale

I casi d'uso documentati guidano nella creazione di un job, nella modifica dei parametri, nella consultazione delle decisioni e nelle richieste di attivazione e rilascio del blocco. Il playground usa dati e risposte simulate: una dimostrazione locale è distinta da una prova sul conto Demo Capital.com.

### Cosa prevede il percorso di sviluppo

La roadmap prevede la validazione del forecasting, simulazioni storiche con costi realistici e prove sul conto Demo Capital.com prima di un eventuale passaggio Live. Il completamento e gli esiti di queste verifiche restano da accertare. Il passaggio Live richiede una decisione esplicita del business e le revisioni previste dal progetto.

### Il prossimo passo: una presentazione mirata

Confrontiamo obiettivi, configurazioni e requisiti per capire quale sperimentazione valutare. La presentazione può partire dal prototipo locale e chiarire quali elementi richiedono ulteriori verifiche.

**Richiedi una presentazione del progetto.**

*Capital Majordomo è un progetto in sviluppo. Il trading su CFD con leva comporta un rischio elevato di perdita del capitale. Le previsioni possono essere errate e i controlli operativi non eliminano il rischio. Il materiale presenta il progetto e non costituisce consulenza finanziaria.*

## 3. Pitch — 7 slide con note relatore

### Slide 1 — Automazione, con regole esplicite

**A schermo**

Capital Majordomo

Un progetto per collegare previsioni, decisioni e controlli nel trading su CFD.

Backend skeleton e playground locale disponibili nel repository.

**Note relatore:** Aprire sul processo. La disponibilità riguarda codice e materiali locali; non presentare un servizio Live o una piattaforma commerciale già validata.

### Slide 2 — Dal segnale a una scelta operativa

**A schermo**

Una previsione è un punto di partenza.

Servono strumenti definiti, parametri espliciti e passaggi verificabili prima dell'ordine.

**Note relatore:** Il progetto affronta l'organizzazione del flusso tra dato e ordine. Non sostenere che il forecasting abbia già dimostrato capacità predittive o rendimenti.

### Slide 3 — Una configurazione comune: il Trading Job

**A schermo**

Strumenti e strategia.

Modello di previsione e parametri.

Regole di rischio e limiti operativi.

**Note relatore:** Spiegare il job come una scheda di configurazione condivisa tra business e tecnici. Nel backend sono presenti gestione e versionamento dei parametri; ciò non attesta che ogni opzione del progetto sia completa.

### Slide 4 — Leggere le decisioni del prototipo

**A schermo**

Dati → previsione → segnale → controlli → ordine.

Il ciclo registra esiti e motivazioni consultabili.

**Note relatore:** Mostrare il percorso implementato nello skeleton, qualificando i limiti: forecasting locale semplificato, dati di rischio parziali e regole strumento predefinite. Il log non equivale a un audit certificato o a tracciabilità esaustiva validata.

### Slide 5 — Dove siamo oggi

**A schermo**

Presente: backend skeleton, API e casi d'uso Postman.

Playground locale: broker simulato e forecasting senza modelli ML reali.

Da validare: integrazione completa e prove sul conto Demo Capital.com.

**Note relatore:** Distinguere la demo locale dal conto Demo del broker. Non dedurre operatività reale dalle risposte Postman. I report includono metriche segnaposto; il backtest non prova performance della strategia.

### Slide 6 — Controlli operativi e percorso previsto

**A schermo**

Controlli pre-ordine e blocco operativo presenti nel prototipo.

Dati e funzioni ancora da completare.

Roadmap: validazione, simulazioni storiche, conto Demo, eventuale Live.

I controlli non eliminano il rischio di perdita.

**Note relatore:** La chiusura automatica delle posizioni tramite kill switch è ancora TODO. P&L realizzato, picco storico dell'equity e ultima perdita non sono pienamente alimentati. Non promettere soglie di perdita garantite, tempi di chiusura o conformità normativa. Il Live richiede decisioni e revisioni dedicate.

### Slide 7 — Valutiamo il prossimo passo

**A schermo**

Partiamo da obiettivi e requisiti.

Esploriamo il prototipo locale.

Definiamo le verifiche di una possibile sperimentazione.

**Richiedi una presentazione del progetto.**

Progetto in sviluppo. I CFD con leva comportano un rischio elevato di perdita del capitale. Nessuna previsione o controllo garantisce risultati. Materiale informativo, non consulenza finanziaria.

**Note relatore:** Invitare al confronto. Non chiedere depositi, adesioni a un servizio o autorizzazioni a operare. Mantenere l'avvertenza leggibile nel deck finale.

## 4. Versione parlata — circa due minuti

Circa 260 parole; durata indicativa a ritmo di 130 parole al minuto, da verificare in prova.

Capital Majordomo nasce da una domanda: come rendere esplicito il percorso che collega una previsione a una decisione operativa?

Nel trading su CFD, un segnale è solo una parte del processo. Servono strumenti definiti, parametri condivisi e criteri per valutare se procedere oppure fermarsi.

Il progetto organizza queste scelte in un Trading Job: una configurazione che riunisce strumenti, modello di previsione, strategia, regole di rischio e limiti operativi. È un riferimento comune per il confronto tra chi definisce gli obiettivi e chi sviluppa il sistema.

Il backend skeleton contiene un ciclo che collega dati di mercato, previsione, segnale, controlli preliminari e invio dell'ordine attraverso l'integrazione con le API Capital.com. Registra anche esiti e motivazioni delle decisioni, consultabili tramite API.

Oggi sono presenti il backend, un playground locale e casi d'uso Postman. È importante chiarire il perimetro: il playground usa risposte simulate del broker e un servizio di previsione semplificato, senza modelli di machine learning reali. Non equivale a una validazione sul conto Demo Capital.com.

Anche i controlli richiedono ulteriori sviluppi. Alcuni dati di rischio sono incompleti e il comando di blocco non include ancora la chiusura automatica delle posizioni.

La roadmap prevede validazione del forecasting, simulazioni storiche con costi realistici e prove sul conto Demo, prima di un eventuale passaggio Live deciso dal business.

I CFD con leva comportano un rischio elevato di perdita del capitale. Previsioni e controlli non garantiscono risultati.

Il prossimo passo è un confronto su obiettivi e requisiti, partendo dal prototipo e dalle verifiche ancora necessarie. Richiedi una presentazione del progetto.

## 5. Post LinkedIn — pronto da copiare dopo approvazione

Automatizzare il trading richiede più di una previsione: servono regole esplicite e decisioni leggibili.

Capital Majordomo è un progetto di piattaforma per orchestrare operazioni su CFD tramite le API Capital.com. Il punto di partenza è il Trading Job: una configurazione che riunisce strumenti, strategia, parametri di rischio e limiti operativi.

Oggi il repository contiene un backend skeleton, API di gestione e casi d'uso per esplorare il prototipo in locale. Il ciclo decisionale registra esiti e motivazioni consultabili.

Il playground usa un broker simulato e previsioni semplificate, senza modelli di machine learning reali. Alcuni dati e controlli sono ancora da completare; le prove sul conto Demo Capital.com e l'eventuale operatività Live restano da validare.

Vuoi approfondire il progetto e i requisiti di una possibile sperimentazione? Richiedi una presentazione del progetto.

I CFD con leva comportano un rischio elevato di perdita del capitale. Previsioni e controlli operativi non garantiscono risultati. Materiale informativo, non consulenza finanziaria.

#TradingAutomatico #SviluppoSoftware #Fintech

## 6. Varianti brevi per revisione o futuri test

| Elemento | Variante A | Variante B |
| --- | --- | --- |
| Titolo brochure / slide 1 | Dalla previsione a un processo verificabile. | Valutare prima di operare. |
| Apertura LinkedIn | Come si passa da una previsione a un ordine? Il progetto Capital Majordomo parte dalle regole del processo. | Strumenti, parametri, decisioni: Capital Majordomo organizza il percorso del trading automatico in unità configurabili. |
| CTA | Richiedi una presentazione del progetto. | Parliamo dei requisiti della tua possibile sperimentazione. |

Varianti non testate: nessun risultato A/B disponibile. Conservare qualifiche di sviluppo e avvertenze in qualunque variante.

## 7. Registro interno claim → fonte → stato

«Disponibile» indica presenza in codice/documentazione al commit esaminato, non verifica di esecuzione in questo incarico. «Progettato» indica intento o roadmap. «Da verificare» indica assenza di prova sufficiente o implementazione parziale. I riferimenti F sono risolti nella sezione 8.

| Claim utilizzabile / limite | Fonte e riscontro | Stato |
| --- | --- | --- |
| Progetto di orchestrazione CFD tramite API Capital.com | F1, F2; F10 gateway e invio ordini | Progettato come piattaforma completa; integrazione disponibile nello skeleton, esecuzione reale da verificare |
| Gestione job e versionamento parametri | F3; F5 StrategyService e TradingJob; F12 TradingJobTests | Disponibile nel codice; runtime non rieseguito |
| Ciclo dati → previsione → segnale → rischio → ordine | F6 DecisionCycleService | Disponibile nello skeleton, con dipendenze parziali; non descriverlo come ciclo Live validato |
| Consultazione esiti e motivazioni | F6 Decision e salvataggio; F5 endpoint decisioni | Disponibile nel codice; completezza di audit da verificare |
| Controlli pre-ordine su stop, esposizione, leva e limiti | F7 PreTradeRiskEvaluator; F12 test unitari | Disponibile come logica; efficacia integrata da verificare: F9 usa dati parziali e F6 InstrumentRules.Default |
| Blocco operativo e ruoli distinti | F8 RiskEndpoints e RiskService; F11 casi d'uso | Disponibile nello skeleton; chiusura posizioni TODO, nessuna promessa di liquidazione immediata |
| Playground locale con broker simulato | F4 README e guida Postman; deploy/local/wiremock | Disponibile come ambiente documentato; non eseguito in questa consegna |
| Forecasting locale senza modelli ML reali | F13 NaiveForecastingService, versione stub e formula naive | Disponibile come stub; nomi TimesFM nel catalogo non provano uso dei modelli |
| Backtest realistico e metriche di investimento | F14 BacktestingModule; F9 report con zeri; F15 roadmap | Progettato / da verificare; non usare metriche segnaposto come risultati |
| Riconciliazione completa | F10 OrderReconciliationService: TODO per ordine senza riferimento | Da verificare / parziale; nessuna promessa di assenza di ordini duplicati o drift |
| Prove su conto Demo e passaggio Live | F15; F2 §12–15; F16 ADR | Progettato; nessuna prova di validazione completa esaminata |
| Rischio elevato dei CFD con leva | F1 avvertenza del progetto | Avvertenza presente nelle fonti; adeguatezza della formulazione finale da revisionare per canale e destinatari |
| Partnership, certificazioni, conformità, rendimenti, clienti, risparmi misurati | Nessuna evidenza verificata nelle fonti esaminate | Da verificare; esclusi dal copy |

## 8. Fonti consultate e metodo

Revisione locale: `fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650` (HEAD). Consultazione in sola lettura; nessuna modifica al software, al branch main o merge. I link seguenti puntano al commit per rendere ripetibile la verifica.

- F1: [docs/capital-majordomo/README.md](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/docs/capital-majordomo/README.md)
- F2: [docs/capital-majordomo/rfc/RFC-001-capital-majordomo.md](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/docs/capital-majordomo/rfc/RFC-001-capital-majordomo.md)
- F3: [docs/capital-majordomo/domain/domain-model.md](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/docs/capital-majordomo/domain/domain-model.md)
- F4: [README.md](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/README.md)
- F4b: [tools/postman/Guida-test-backend-con-Postman.md](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/tools/postman/Guida-test-backend-con-Postman.md)
- F5: [src/Modules/Strategy/Majordomo.Strategy/Application/StrategyService.cs](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/src/Modules/Strategy/Majordomo.Strategy/Application/StrategyService.cs)
- F5b: [src/Modules/Strategy/Majordomo.Strategy/Domain/TradingJob.cs](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/src/Modules/Strategy/Majordomo.Strategy/Domain/TradingJob.cs)
- F5c: [src/Modules/Strategy/Majordomo.Strategy/Endpoints/StrategyEndpoints.cs](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/src/Modules/Strategy/Majordomo.Strategy/Endpoints/StrategyEndpoints.cs)
- F6: [src/Modules/Strategy/Majordomo.Strategy/Application/DecisionCycleService.cs](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/src/Modules/Strategy/Majordomo.Strategy/Application/DecisionCycleService.cs)
- F7: [src/Modules/Risk/Majordomo.Risk/Domain/PreTradeRiskEvaluator.cs](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/src/Modules/Risk/Majordomo.Risk/Domain/PreTradeRiskEvaluator.cs)
- F8: [src/Modules/Risk/Majordomo.Risk/Endpoints/RiskEndpoints.cs](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/src/Modules/Risk/Majordomo.Risk/Endpoints/RiskEndpoints.cs)
- F8b: [src/Modules/Risk/Majordomo.Risk/Application/RiskService.cs](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/src/Modules/Risk/Majordomo.Risk/Application/RiskService.cs)
- F9: [src/Modules/Portfolio/Majordomo.Portfolio/PortfolioModule.cs](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/src/Modules/Portfolio/Majordomo.Portfolio/PortfolioModule.cs)
- F10: [src/Modules/Execution/Majordomo.Execution/Application/ExecutionService.cs](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/src/Modules/Execution/Majordomo.Execution/Application/ExecutionService.cs)
- F11: [tools/postman/Casi-d-uso-funzionali-end-to-end-Postman.md](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/tools/postman/Casi-d-uso-funzionali-end-to-end-Postman.md)
- F12: [tests/Majordomo.UnitTests/PreTradeRiskEvaluatorTests.cs](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/tests/Majordomo.UnitTests/PreTradeRiskEvaluatorTests.cs)
- F12b: [tests/Majordomo.UnitTests/TradingJobTests.cs](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/tests/Majordomo.UnitTests/TradingJobTests.cs)
- F13: [tools/Majordomo.ForecastingStub/NaiveForecastingService.cs](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/tools/Majordomo.ForecastingStub/NaiveForecastingService.cs)
- F14: [src/Modules/Backtesting/Majordomo.Backtesting/BacktestingModule.cs](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/src/Modules/Backtesting/Majordomo.Backtesting/BacktestingModule.cs)
- F15: [docs/capital-majordomo/roadmap.md](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/docs/capital-majordomo/roadmap.md)
- F16: [docs/capital-majordomo/adr/0005-risk-engine-hard-gate-kill-switch.md](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/docs/capital-majordomo/adr/0005-risk-engine-hard-gate-kill-switch.md)
- F16b: [docs/capital-majordomo/adr/0006-backtesting-paper-trading-promotion.md](https://github.com/maiorfi-at-kdigitale/KDPaperClipPlaygroundRepo/blob/fa9d41ad0bec8b9f8af4061c0ce4a2cb974e3650/docs/capital-majordomo/adr/0006-backtesting-paper-trading-promotion.md)

Metodo: confronto tra proposta architetturale, sorgenti, test presenti e guide Postman. I test sono stati letti, non eseguiti; non è stata avviata una sessione broker o una demo. Il codice più recente prevale sulle descrizioni architetturali quando mostra limiti concreti. Nessuna affermazione di conformità o consulenza finanziaria è ricavata dalla presenza di ruoli o log.

## 9. Lacune e passaggio a Maria (interno, fuori dal copy)

- Confermare pubblico prioritario, obiettivo commerciale, contatto e modalità effettiva per richiedere una presentazione. La CTA attuale non presume un canale non fornito.
- Confermare la disponibilità di una dimostrazione prima di fissarla o annunciarla. Il playground documentato non prova un ambiente attivo.
- Validare forecasting reale, qualità dei dati e controlli integrati; completare P&L realizzato, picco equity, ultima perdita, regole strumento, chiusura posizioni e riconciliazione.
- Verificare simulazioni storiche, costi, metriche e risultati prima di usarli in comunicazione. Nessun risultato economico è incluso.
- Confermare stato del percorso Demo/Live, destinatari ammessi e revisioni legali applicabili. Non utilizzare il materiale come offerta di gestione di capitali altrui.
- Maria: impaginare brochure in due pagine e pitch in sette slide; mantenere visibili stato di sviluppo, distinzione playground/Demo e avvertenze. Verificare lunghezze sulla grafica finale e provare il parlato.
- Maria e CEO: revisionare messaggi, contesto advertising e formulazione delle avvertenze prima di qualsiasi pubblicazione o invio. La chiusura della produzione copy non costituisce approvazione alla diffusione.

Consegna prevista sulla presente issue; nessuna pubblicazione social, campagna o spesa eseguita.
