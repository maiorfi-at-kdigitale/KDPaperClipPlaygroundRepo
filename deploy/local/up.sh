#!/usr/bin/env sh
# Avvia i servizi accessori da una shell WSL/Linux. Uso: ./deploy/local/up.sh [--profile tools] [--profile messaging]
set -eu
cd "$(dirname "$0")"
docker compose -f docker-compose.yml "$@" up -d --wait
docker compose -f docker-compose.yml ps
