#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APPSETTINGS_SRC="$ROOT_DIR/src/CosmosReplication.Host/appsettings.Development.json"

docker container rm -f cosmosreplication cosmosreplication-estimator

docker run --rm -d \
  -p "8080:8080" \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ReplicationConfiguration__CosmosAccounts__0__ConnectionString="${COSMOS_CONNECTION_STRING}" \
  -e APPLICATIONINSIGHTS_CONNECTION_STRING="${APPLICATIONINSIGHTS_CONNECTION_STRING}" \
  -v "$APPSETTINGS_SRC:/app/appsettings.Development.json:ro" \
  --name "cosmosreplication" \
  "cosmosreplication"

APPSETTINGS_SRC="$ROOT_DIR/src/CosmosReplication.Estimator.Host/appsettings.Development.json"

docker run --rm -d \
  -p "8081:8080" \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ReplicationConfiguration__CosmosAccounts__0__ConnectionString="${COSMOS_CONNECTION_STRING}" \
  -e APPLICATIONINSIGHTS_CONNECTION_STRING="${APPLICATIONINSIGHTS_CONNECTION_STRING}" \
  -e ReplicationHost__ReadyUrl="http://localhost:8080/healthz/ready" \
  -v "$APPSETTINGS_SRC:/app/appsettings.Development.json:ro" \
  --name "cosmosreplication-estimator" \
  "cosmosreplication-estimator"
