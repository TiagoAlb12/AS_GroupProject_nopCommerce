param(
    [string]$K6Script = "load-test/load-test-flow.js",
    [string]$OisContainer = "order_integration_service",
    [switch]$SkipDockerControl
)

$ErrorActionPreference = "Stop"

Write-Host "[checkout-flow] Starting k6 load test: $K6Script"
$k6 = Start-Process -FilePath "k6" -ArgumentList @("run", $K6Script) -NoNewWindow -PassThru

try {
    if (-not $SkipDockerControl) {
        Write-Host "[checkout-flow] 0:00-2:30 normal system"
        Start-Sleep -Seconds 150

        Write-Host "[checkout-flow] 2:30 stopping $OisContainer so RabbitMQ queue can accumulate"
        docker stop $OisContainer | Out-Host

        Start-Sleep -Seconds 60

        Write-Host "[checkout-flow] 3:30 starting $OisContainer so RabbitMQ queue can drain"
        docker start $OisContainer | Out-Host

        Start-Sleep -Seconds 90
    }

    $runningK6 = Get-Process -Id $k6.Id -ErrorAction SilentlyContinue
    if ($runningK6) {
        Wait-Process -Id $k6.Id
    }

    $k6.Refresh()
    if ($null -ne $k6.ExitCode -and $k6.ExitCode -ne 0) {
        throw "k6 exited with code $($k6.ExitCode)"
    }
}
finally {
    if (-not $SkipDockerControl) {
        $running = docker inspect -f "{{.State.Running}}" $OisContainer 2>$null
        if ($running -ne "true") {
            Write-Host "[checkout-flow] Restoring $OisContainer"
            docker start $OisContainer | Out-Host
        }
    }
}

Write-Host "[checkout-flow] Finished."
