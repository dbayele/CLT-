$ErrorActionPreference = 'Stop'

$RootDir = Split-Path -Parent $PSScriptRoot

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:CLTPP_DEV_SEED = 'true'

# Development intentionally stays on SQLite. Production database settings are not enabled here.
Remove-Item Env:CLTPP_PUBLIC_SAFETY_SQLSERVER_CONNECTION_STRING -ErrorAction SilentlyContinue
Remove-Item Env:CLTPP_RDS_ENABLED -ErrorAction SilentlyContinue
Remove-Item Env:CLTPP_RDS_CONNECTION_STRING -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Force -Path (Join-Path $RootDir 'employee/data') | Out-Null

Write-Host 'Starting CLT++ Employee in Development mode with idempotent sample data...'
Write-Host 'Demo login: police.demo / ChangeMe!'
Write-Host 'Supervisor login: supervisor.demo / ChangeMe!'

& dotnet run --project (Join-Path $RootDir 'employee/CltPlusPlus.Employee.csproj') -- @args
exit $LASTEXITCODE
