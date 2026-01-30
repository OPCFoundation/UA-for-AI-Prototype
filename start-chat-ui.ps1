# Start OPC UA Specification ChatBot UI
# Launches the Electron desktop app that connects to the MCP server

$ErrorActionPreference = "Stop"
$chatAppPath = Join-Path $PSScriptRoot "Opc.Ua.McpChat"

# Verify the app directory exists
if (-not (Test-Path $chatAppPath)) {
    Write-Error "Chat app directory not found: $chatAppPath"
    exit 1
}

# Check if node_modules exists
$nodeModules = Join-Path $chatAppPath "node_modules"
if (-not (Test-Path $nodeModules)) {
    Write-Host "Installing dependencies..."
    Push-Location $chatAppPath
    npm install
    Pop-Location
}

# Launch the Electron app
Write-Host "Starting OPC UA Specification ChatBot..."
Write-Host "  App: $chatAppPath"
Write-Host ""
Push-Location $chatAppPath
npm start
Pop-Location
