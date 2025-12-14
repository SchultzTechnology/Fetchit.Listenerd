# View Listenerd Logs - Multiple Options
# Usage: .\view-logs.ps1 [option]
# Options: all, follow, restart, live

param(
    [Parameter(Position=0)]
    [ValidateSet("all", "follow", "restart", "live", "stats")]
    [string]$Option = "follow"
)

$containerName = "fetchit-combined"

Write-Host "=== Fetchit Listenerd Logs ===" -ForegroundColor Cyan
Write-Host ""

switch ($Option) {
    "all" {
        Write-Host "Showing ALL logs from the beginning..." -ForegroundColor Yellow
        docker exec $containerName cat /var/log/supervisor/listenerd.out.log
    }
    
    "follow" {
        Write-Host "Showing last 50 lines and following new logs..." -ForegroundColor Yellow
        Write-Host "Press Ctrl+C to exit" -ForegroundColor Gray
        Write-Host ""
        docker exec $containerName tail -n 50 -f /var/log/supervisor/listenerd.out.log
    }
    
    "restart" {
        Write-Host "Restarting Listenerd service..." -ForegroundColor Yellow
        docker exec $containerName supervisorctl restart fetchit-listenerd
        Start-Sleep -Seconds 2
        Write-Host "Following logs from restart..." -ForegroundColor Green
        Write-Host "Press Ctrl+C to exit" -ForegroundColor Gray
        Write-Host ""
        docker exec $containerName tail -f /var/log/supervisor/listenerd.out.log
    }
    
    "live" {
        Write-Host "Showing initialization logs + live updates..." -ForegroundColor Yellow
        Write-Host "Press Ctrl+C to exit" -ForegroundColor Gray
        Write-Host ""
        
        # Show startup logs first
        Write-Host "`n--- STARTUP LOGS ---" -ForegroundColor Cyan
        docker exec $containerName head -n 80 /var/log/supervisor/listenerd.out.log
        
        Write-Host "`n--- LIVE LOGS ---" -ForegroundColor Cyan
        docker exec $containerName tail -f /var/log/supervisor/listenerd.out.log
    }
    
    "stats" {
        Write-Host "Showing statistics and recent activity..." -ForegroundColor Yellow
        Write-Host ""
        
        # Service status
        Write-Host "Service Status:" -ForegroundColor Cyan
        docker exec $containerName supervisorctl status fetchit-listenerd
        
        Write-Host "`nLast 30 lines:" -ForegroundColor Cyan
        docker exec $containerName tail -n 30 /var/log/supervisor/listenerd.out.log
        
        Write-Host "`nLog Statistics:" -ForegroundColor Cyan
        $lines = docker exec $containerName wc -l /var/log/supervisor/listenerd.out.log
        Write-Host "  Total log lines: $lines"
        
        $mqttConnected = docker exec $containerName grep -c "MQTT Connected" /var/log/supervisor/listenerd.out.log 2>$null
        Write-Host "  MQTT Connections: $mqttConnected"
        
        $sipPackets = docker exec $containerName grep -c "SIP Packet" /var/log/supervisor/listenerd.out.log 2>$null
        Write-Host "  SIP Packets Captured: $sipPackets"
    }
}

Write-Host ""
Write-Host "=== Options ===" -ForegroundColor Cyan
Write-Host "  .\view-logs.ps1 all      - Show complete log file" -ForegroundColor White
Write-Host "  .\view-logs.ps1 follow   - Last 50 lines + follow (default)" -ForegroundColor White
Write-Host "  .\view-logs.ps1 restart  - Restart service and watch" -ForegroundColor White
Write-Host "  .\view-logs.ps1 live     - Show startup + follow" -ForegroundColor White
Write-Host "  .\view-logs.ps1 stats    - Show statistics" -ForegroundColor White
