$ErrorActionPreference = "Stop"

$apiPath = Join-Path $PSScriptRoot "src\Demo.WebApi"
$frontendPath = Join-Path $PSScriptRoot "src\Demo.Frontend"
$nodeModulesPath = Join-Path $frontendPath "node_modules"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK is required to run the demo. Install the .NET 10 SDK and try again."
}

if (-not (Get-Command npm.cmd -ErrorAction SilentlyContinue)) {
    throw "Node.js and npm are required to run the demo. Install Node.js and try again."
}

if (-not (Test-Path $nodeModulesPath)) {
    Write-Host "Installing frontend dependencies..."
    Push-Location $frontendPath
    try {
        & npm.cmd ci
        if ($LASTEXITCODE -ne 0) {
            throw "Installing frontend dependencies failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

Start-Process -FilePath "dotnet" -ArgumentList "run" -WorkingDirectory $apiPath
Start-Process -FilePath "npm.cmd" -ArgumentList "run dev" -WorkingDirectory $frontendPath

Write-Host "Dynamic SQL demo is starting."
Write-Host "Frontend: http://localhost:5173"
Write-Host "Backend Swagger: http://localhost:21210/swagger"
