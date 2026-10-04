-- Eseguito solo alla prima creazione del volume pgdata.
-- Le tabelle e gli schemi per modulo li creano le migrazioni EF Core (Majordomo.Api in Development).
CREATE EXTENSION IF NOT EXISTS timescaledb;
CREATE EXTENSION IF NOT EXISTS pgcrypto;
