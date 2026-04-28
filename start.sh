#!/bin/bash
set -e

SB_CONN="<SERVICE_BUS_CONNECTION_STRING>"
PG_CONN="<POSTGRES_CONNECTION_STRING>"

ROOT="$(cd "$(dirname "$0")" && pwd)"
LOGS="$ROOT/.logs"
mkdir -p "$LOGS"

echo "=== PCD Project - Starting all services ==="
echo ""

# Kill anything on our ports
echo "[*] Eliberare porturi 5000, 5001, 3000..."
lsof -ti:5000,5001,3000 | xargs kill -9 2>/dev/null || true
sleep 1

# Service A - RealWorld API
echo "[1] Pornire Service A (API) pe portul 5000..."
cd "$ROOT/src/Conduit"
ASPNETCORE_ENVIRONMENT=Development \
DatabaseProvider=postgresql \
"ConnectionStrings__DefaultConnection=$PG_CONN" \
"ConnectionStrings__ServiceBus=$SB_CONN" \
dotnet run --no-launch-profile --urls http://localhost:5000 > "$LOGS/serviceA.log" 2>&1 &
echo "    PID=$! → log: .logs/serviceA.log"

# Service B - Sentiment Processor (console)
echo "[2] Pornire Service B (Sentiment Processor)..."
cd "$ROOT/src/ServiceBConsole"
"ConnectionStrings__ServiceBus=$SB_CONN" \
"ConnectionStrings__DefaultConnection=$PG_CONN" \
dotnet run > "$LOGS/serviceB.log" 2>&1 &
echo "    PID=$! → log: .logs/serviceB.log"

# Service C - Notification Service
echo "[3] Pornire Service C (Notification Service) pe portul 5001..."
cd "$ROOT/src/NotificationService"
"ConnectionStrings__ServiceBus=$SB_CONN" \
dotnet run --urls http://localhost:5001 > "$LOGS/serviceC.log" 2>&1 &
echo "    PID=$! → log: .logs/serviceC.log"

# Frontend
echo "[4] Pornire Frontend pe portul 3000..."
cd "$ROOT/frontend"
python3 -m http.server 3000 > "$LOGS/frontend.log" 2>&1 &
echo "    PID=$! → log: .logs/frontend.log"

echo ""
echo "=== Asteptare pornire servicii (20 secunde)... ==="
sleep 20

echo ""
echo "=== GATA! Deschide browserul la: http://localhost:3000 ==="
echo ""
echo "Servicii active:"
echo "  - Service A (API):            http://localhost:5000"
echo "  - Service C (Notifications):  http://localhost:5001"
echo "  - Frontend:                   http://localhost:3000"
echo ""
echo "Apasa Ctrl+C pentru a opri toate serviciile."
echo ""

trap 'echo "Oprire..."; lsof -ti:5000,5001,3000 | xargs kill -9 2>/dev/null; exit' INT
wait
