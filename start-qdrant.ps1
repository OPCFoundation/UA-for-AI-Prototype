# Start Qdrant Vector Database
# Requires Docker Desktop installed

$ErrorActionPreference = "Stop"
$containerName = "qdrant"
$dbPath = Join-Path $PSScriptRoot "db"

# Check if Qdrant is already running
Write-Host "Checking if Qdrant is already running..."
try {
    $response = Invoke-WebRequest -Uri "http://localhost:6333/readyz" -UseBasicParsing -TimeoutSec 2 -ErrorAction SilentlyContinue
    if ($response.StatusCode -eq 200) {
        Write-Host "Qdrant is already running!"
        Write-Host "Opening Qdrant Dashboard in browser..."
        Start-Process "http://localhost:6333/dashboard"
        Write-Host ""
        Write-Host "Qdrant is running:"
        Write-Host "  - HTTP API: http://localhost:6333"
        Write-Host "  - gRPC API: localhost:6334"
        Write-Host "  - Dashboard: http://localhost:6333/dashboard"
        exit 0
    }
}
catch {
    # Qdrant not running, continue with startup
}

# Ensure db directory exists
if (-not (Test-Path $dbPath)) {
    Write-Host "Creating database directory: $dbPath"
    New-Item -ItemType Directory -Path $dbPath | Out-Null
}

# Check if Docker is running
Write-Host "Checking Docker status..."
$dockerInfo = docker info 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Docker is not running. Starting Docker Desktop..."
    Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe"

    # Wait for Docker to be ready
    $maxWait = 60
    $waited = 0
    while ($waited -lt $maxWait) {
        Start-Sleep -Seconds 2
        $waited += 2
        $dockerInfo = docker info 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Docker is now running."
            break
        }
        Write-Host "Waiting for Docker to start... ($waited seconds)"
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Docker failed to start within $maxWait seconds."
        exit 1
    }
}
else {
    Write-Host "Docker is already running."
}

# Check if container already exists
$existingContainer = docker ps -a --filter "name=^${containerName}$" --format "{{.Names}}" 2>&1
if ($existingContainer -eq $containerName) {
    # Check if it's running
    $runningContainer = docker ps --filter "name=^${containerName}$" --format "{{.Names}}" 2>&1
    if ($runningContainer -eq $containerName) {
        Write-Host "Qdrant container is already running."
    }
    else {
        Write-Host "Starting existing Qdrant container..."
        docker start $containerName
    }
}
else {
    Write-Host "Creating and starting Qdrant container..."
    docker run -d `
        --name $containerName `
        -p 6333:6333 `
        -p 6334:6334 `
        -v "${dbPath}:/qdrant/storage:z" `
        qdrant/qdrant:latest
}

# Wait for Qdrant to be ready
Write-Host "Waiting for Qdrant to be ready..."
$maxWait = 30
$waited = 0
while ($waited -lt $maxWait) {
    Start-Sleep -Seconds 1
    $waited++
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:6333/readyz" -UseBasicParsing -TimeoutSec 2 -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            Write-Host "Qdrant is ready!"
            break
        }
    }
    catch {
        # Still starting up
    }
}

# Open browser with Qdrant UI
Write-Host "Opening Qdrant Dashboard in browser..."
Start-Process "http://localhost:6333/dashboard"

Write-Host ""
Write-Host "Qdrant is running:"
Write-Host "  - HTTP API: http://localhost:6333"
Write-Host "  - gRPC API: localhost:6334"
Write-Host "  - Dashboard: http://localhost:6333/dashboard"
Write-Host "  - Database:  $dbPath"
Write-Host ""
Write-Host "To stop: docker stop $containerName"
Write-Host "To remove: docker rm $containerName"
