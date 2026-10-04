# ADR-0003 — PostgreSQL + TimescaleDB, outbox transazionale, nessun broker iniziale

- Stato: Proposto
- Data: 2026-10-04

## Contesto

Servono: dati relazionali di dominio (job, parametri, ordini, posizioni), serie temporali
(candele, previsioni) per ~300k righe/giorno, un log di decisioni append-only e la garanzia che
la decisione, l'ordine e gli eventi relativi siano scritti in modo atomico. I consumatori esterni
di eventi (notifiche, analytics) sono pochi e i volumi bassi.

## Decisione

- **PostgreSQL 17** come unico database, con estensione **TimescaleDB** per `market.candles` e
  `forecasting.forecasts` (hypertable, compressione, continuous aggregate).
- Uno schema per modulo, ruoli DB con privilegi limitati allo schema del modulo.
- **Transactional Outbox** (`infra.outbox_messages`) scritto nella stessa transazione dei
  cambiamenti di stato; un dispatcher nel worker (leader) consegna i messaggi ai gestori interni
  (es. invio ordini al broker) e ai consumatori esterni (webhook/notifiche). Consegna
  at-least-once; **Inbox** (`infra.inbox_messages`) per deduplicare in ricezione.
- Nessun message broker in v1. Punto di estensione: il dispatcher pubblica tramite
  un'astrazione (`IIntegrationEventPublisher`) sostituibile con RabbitMQ/NATS/Azure Service Bus
  (o MassTransit/Wolverine con outbox nativa) quando i consumatori aumentano.
- Backup: WAL archiving continuo (pgBackRest o servizio gestito), test di ripristino mensile.

## Alternative considerate

1. **Database time series dedicato (InfluxDB/QuestDB) + PostgreSQL** — prestazioni superiori su
   grandi volumi, ma due database, nessuna transazione comune e volumi attuali non giustificano.
2. **Kafka come log di eventi e fonte delle serie** — replay e scalabilità eccellenti, costo
   operativo elevato per un sistema con un solo produttore e pochi consumatori.
3. **Event sourcing completo dello stato di trading** — audit nativo, ma complessità di proiezioni
   e versionamento; il decision log append-only copre il requisito di audit con meno costo.

## Conseguenze

- Positive: una sola tecnologia di persistenza da operare; atomicità fra stato e messaggi;
  query analitiche SQL su candele e previsioni; migrazione a un broker non invasiva.
- Negative: TimescaleDB limita alcune offerte gestite (verificare disponibilità sul cloud scelto,
  domanda D3; in alternativa partizionamento nativo PostgreSQL); il dispatcher di outbox è un
  componente da scrivere e testare (o da adottare tramite libreria); latenza di consegna pari al
  ciclo di polling (mitigata con `LISTEN/NOTIFY`).
