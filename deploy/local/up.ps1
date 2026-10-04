# Avvia i servizi accessori (Docker Desktop + WSL 2). Uso: .\deploy\local\up.ps1 [-Tools] [-Messaging]
param(
    [switch]$Tools,
    [switch]$Messaging
)
$ErrorActionPreference = "Stop"
$compose = Join-Path $PSScriptRoot "docker-compose.yml"
$profiles = @()
if ($Tools) { $profiles += "--profile", "tools" }
if ($Messaging) { $profiles += "--profile", "messaging" }
docker compose -f $compose @profiles up -d --wait
docker compose -f $compose ps
Write-Host ""
Write-Host "PostgreSQL : localhost:5432  (majordomo / majordomo-dev-only)"
Write-Host "Redis      : localhost:6379"
Write-Host "Capital mock (WireMock): http://localhost:8089/__admin/mappings"
Write-Host "Grafana    : http://localhost:3000  (admin / admin)"
if ($Tools) { Write-Host "pgAdmin    : http://localhost:5050" }
if ($Messaging) { Write-Host "RabbitMQ   : http://localhost:15672  (majordomo / majordomo-dev-only)" }
