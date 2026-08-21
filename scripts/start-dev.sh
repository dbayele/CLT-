#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

export ASPNETCORE_ENVIRONMENT=Development
export DOTNET_ENVIRONMENT=Development
export CLTPP_DEV_SEED=true

# Development intentionally stays on SQLite. Production database settings are not enabled here.
unset CLTPP_PUBLIC_SAFETY_SQLSERVER_CONNECTION_STRING || true
unset CLTPP_RDS_ENABLED || true
unset CLTPP_RDS_CONNECTION_STRING || true

mkdir -p "$ROOT_DIR/employee/data"

echo "Starting CLT++ Employee in Development mode with idempotent sample data..."
echo "Demo login: police.demo / ChangeMe!"
echo "Supervisor login: supervisor.demo / ChangeMe!"

exec dotnet run --project "$ROOT_DIR/employee/CltPlusPlus.Employee.csproj" -- "$@"
