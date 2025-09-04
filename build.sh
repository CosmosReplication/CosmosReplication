#!/usr/bin/env bash
set -euo pipefail

dotnet clean -c Release
dotnet clean -c Debug
rm -rf ./artifacts
rm -rf **/bin
rm -rf **/obj
dotnet restore
dotnet format --verify-no-changes --severity info
dotnet build -c Release --no-restore
dotnet test -c Release --no-build /p:CollectCoverage=true /p:Threshold=75 /p:CoverletOutput="../../artifacts/"
dotnet pack -c Release --no-build --include-symbols --include-source -p:SymbolPackageFormat=snupkg -o ./artifacts
dotnet publish -c Release --no-build
docker build -t cosmosreplication -f CosmosReplication.Host.Dockerfile .
docker build -t cosmosreplication-estimator -f CosmosReplication.Estimator.Host.Dockerfile .
