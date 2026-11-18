$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repositoryRoot
try {
    dotnet restore DispatchBoard.slnx
    Push-Location "ui"
    try {
        npm install
    }
    finally {
        Pop-Location
    }

    Write-Host "Setup complete. Run .\scripts\run.ps1"
}
finally {
    Pop-Location
}
