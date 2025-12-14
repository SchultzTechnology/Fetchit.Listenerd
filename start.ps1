# Fetchit MQTT Configuration Manager - PowerShell Start Script

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Fetchit MQTT Configuration Manager" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Check if Docker is installed
try {
    docker --version | Out-Null
} catch {
    Write-Host "Error: Docker is not installed or not in PATH." -ForegroundColor Red
    exit 1
}

# Check if Docker Compose is installed
try {
    docker-compose --version | Out-Null
} catch {
    Write-Host "Error: Docker Compose is not installed or not in PATH." -ForegroundColor Red
    exit 1
}

# Create data directory if it doesn't exist
if (-not (Test-Path "./data")) {
    Write-Host "Creating data directory..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path "./data" | Out-Null
}

Write-Host "Building and starting services..." -ForegroundColor Green
docker-compose up -d --build

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Services started successfully!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Container: " -NoNewline -ForegroundColor White
Write-Host "fetchit-combined" -ForegroundColor Yellow -NoNewline
Write-Host " (Running both services)" -ForegroundColor White
Write-Host "  - Web Application: " -NoNewline -ForegroundColor White
Write-Host "http://localhost:8080" -ForegroundColor Yellow
Write-Host "  - Listenerd Service: " -NoNewline -ForegroundColor White
Write-Host "Running in background" -ForegroundColor Yellow
Write-Host ""
Write-Host "Useful commands:" -ForegroundColor White
Write-Host "  View logs:              " -NoNewline -ForegroundColor White
Write-Host "docker-compose logs -f" -ForegroundColor Cyan
Write-Host "  Check process status:   " -NoNewline -ForegroundColor White
Write-Host "docker exec fetchit-combined supervisorctl status" -ForegroundColor Cyan
Write-Host "  Stop services:          " -NoNewline -ForegroundColor White
Write-Host "docker-compose down" -ForegroundColor Cyan
Write-Host "  Restart services:       " -NoNewline -ForegroundColor White
Write-Host "docker-compose restart" -ForegroundColor Cyan
Write-Host ""
Write-Host "View individual service logs:" -ForegroundColor White
Write-Host "  Listenerd:  " -NoNewline -ForegroundColor White
Write-Host "docker exec fetchit-combined tail -f /var/log/supervisor/listenerd.out.log" -ForegroundColor Cyan
Write-Host "  WebPage:    " -NoNewline -ForegroundColor White
Write-Host "docker exec fetchit-combined tail -f /var/log/supervisor/webpage.out.log" -ForegroundColor Cyan
Write-Host ""
Write-Host "Database location: " -NoNewline -ForegroundColor White
Write-Host "./data/mqttconfig.db" -ForegroundColor Yellow
Write-Host "==========================================" -ForegroundColor Cyan
