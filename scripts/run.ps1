$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repositoryRoot
try {
    $logDirectory = Join-Path $repositoryRoot "artifacts"
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    $api = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--project", "src/Dispatch.Api/Dispatch.Api.csproj", "--launch-profile", "http") `
        -WorkingDirectory $repositoryRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $logDirectory "api.stdout.log") `
        -RedirectStandardError (Join-Path $logDirectory "api.stderr.log") `
        -PassThru
    Write-Host "API starting at http://localhost:5050 (process $($api.Id))"
    $apiReady = $false
    for ($attempt = 1; $attempt -le 30; $attempt++) {
        if ($api.HasExited) {
            throw "API stopped during startup. See artifacts/api.stderr.log."
        }
        try {
            $response = Invoke-WebRequest -Uri "http://localhost:5050/health" -UseBasicParsing -TimeoutSec 2
            if ($response.StatusCode -eq 200) {
                $apiReady = $true
                break
            }
        }
        catch {
            Start-Sleep -Seconds 1
        }
    }
    if (-not $apiReady) {
        throw "API did not become ready. See artifacts/api.stderr.log."
    }
    Write-Host "UI starting at http://localhost:4200. Press Ctrl+C to stop."
    Push-Location "ui"
    try {
        npm start
    }
    finally {
        Pop-Location
        if (-not $api.HasExited) {
            Stop-Process -Id $api.Id
        }
    }
}
finally {
    Pop-Location
}
