# Ferma i servizi accessori. Uso: .\deploy\local\down.ps1 [-Reset]   (-Reset elimina anche i volumi/dati)
param([switch]$Reset)
$ErrorActionPreference = "Stop"
$compose = Join-Path $PSScriptRoot "docker-compose.yml"
$args = @("-f", $compose, "--profile", "tools", "--profile", "messaging", "down")
if ($Reset) { $args += "-v" }
docker compose @args
