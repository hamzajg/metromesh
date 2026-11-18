#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

EVENT_ID="evt_smoke_$(date +%s)"

docker compose up --build -d
trap 'docker compose down' EXIT

echo "Waiting for ingest readiness..."
for _ in $(seq 1 60); do
  if curl -sf http://localhost:8080/health/ready >/dev/null 2>&1; then
    break
  fi
  sleep 2
done

echo "== health =="
curl -sf http://localhost:8080/health/live
echo
curl -sf http://localhost:8080/health/ready
echo

echo "== submit telemetry =="
curl -sf -X POST http://localhost:8080/v1/events \
  -H 'Content-Type: application/json' \
  -H 'X-MetroMesh-Api-Key: dev-api-key' \
  -H 'X-Correlation-Id: corr-smoke' \
  -d "{\"specversion\":\"1.0\",\"id\":\"${EVENT_ID}\",\"type\":\"equipment.telemetry.turnstile.count\",\"source\":\"gateway-01\",\"subject\":\"turnstile/t-1\",\"time\":\"2026-11-18T00:00:00Z\",\"datacontenttype\":\"application/json\",\"data\":{\"equipmentId\":\"t-1\",\"stationId\":\"s-1\",\"eventType\":\"turnstile.count.incremented\",\"occurredAt\":\"2026-11-18T00:00:00Z\",\"payload\":{\"count\":1}}}"
echo

echo "== worker health =="
for _ in $(seq 1 30); do
  if curl -sf http://localhost:8081/health/ready >/dev/null 2>&1; then
    break
  fi
  sleep 2
done
curl -sf http://localhost:8081/health/ready
echo

echo "== status =="
sleep 3
curl -sf "http://localhost:8080/v1/events/${EVENT_ID}" -H 'X-MetroMesh-Api-Key: dev-api-key'
echo

echo "smoke ok"
