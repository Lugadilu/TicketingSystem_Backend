#!/bin/bash
set -e
cd /app

dotnet restore Ticketing.sln
dotnet build Ticketing.sln --no-restore --configuration Release
dotnet test Ticketing.sln --no-build --logger "trx;LogFileName=/app/test_results.trx"

cat /app/test_results.trx