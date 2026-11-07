@echo off
echo Project is running...
echo ------------------------------------------------------
echo Frontend UI:           http://localhost:3000
echo ------------------------------------------------------
echo API Gateways:
echo  - User Service:         http://localhost:5001
echo  - Tuition Service:      http://localhost:5002
echo  - OTP Service:          http://localhost:5003
echo  - Payment Orchestrator: http://localhost:5004
echo ------------------------------------------------------

echo Building and starting Docker containers...
docker compose down -v
docker compose up --build
pause
