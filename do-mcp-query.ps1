# Invoke-McpQuery.ps1
# A simple MCP client that queries the OPC UA specification using the Opc.Ua.McpServer
#
# Prerequisites:
#   - Ollama running (ollama serve) with mxbai-embed-large and llama3 models
#   - Qdrant running (.\start-qdrant.ps1) with opcua-specifications collection populated
#
# Usage:
#   .\Invoke-McpQuery.ps1                                    # Interactive mode
#   .\Invoke-McpQuery.ps1 -Question "What is a Session?"     # Single query
#   .\Invoke-McpQuery.ps1 -InputFile queries.txt             # Interactive with query suggestions

param(
    [string]$ProjectPath = ".\Opc.Ua.McpServer",
    [string]$Question,
    [string]$InputFile
)

# Load queries from input file if provided
$Queries = @()
$QueryIndex = 0

if ($InputFile -ne $null) {
    if (-not (Test-Path $InputFile)) {
        Write-Host "Input file not found: $InputFile" -ForegroundColor Red
        exit 1
    }
    # Read queries from file (skip empty lines)
    $Queries = Get-Content $InputFile | Where-Object { $_.Trim() -ne "" }
    if ($Queries.Count -eq 0) {
        Write-Host "No queries found in input file" -ForegroundColor Red
        exit 1
    }
    Write-Host "Loaded $($Queries.Count) queries from $InputFile" -ForegroundColor Green
}

$ErrorActionPreference = "Stop"

# Resolve project path
$ProjectPath = Resolve-Path $ProjectPath

# Build the project first
Write-Host "Building Opc.Ua.McpServer..." -ForegroundColor Yellow
dotnet build $ProjectPath --configuration Release --verbosity quiet 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build Opc.Ua.McpServer" -ForegroundColor Red
    exit 1
}
Write-Host "Build successful.`n" -ForegroundColor Green

# Start the MCP server process
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "dotnet"
$psi.Arguments = "run --project `"$ProjectPath`" --configuration Release --no-build"
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true
$psi.WorkingDirectory = (Get-Location).Path

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $psi
$process.Start() | Out-Null

# Helper function to send JSON-RPC message
function Send-McpMessage {
    param([hashtable]$Message)
    $json = $Message | ConvertTo-Json -Depth 10 -Compress
    $process.StandardInput.WriteLine($json)
    $process.StandardInput.Flush()
}

# Helper function to read JSON-RPC response
function Read-McpResponse {
    $line = $process.StandardOutput.ReadLine()
    if ($line) {
        return $line | ConvertFrom-Json
    }
    return $null
}

try {
    # Step 1: Initialize
    Write-Host "Connecting to MCP server..." -ForegroundColor Cyan

    $initRequest = @{
        jsonrpc = "2.0"
        id = 1
        method = "initialize"
        params = @{
            protocolVersion = "2024-11-05"
            capabilities = @{}
            clientInfo = @{
                name = "PowerShell MCP Client"
                version = "1.0"
            }
        }
    }
    Send-McpMessage $initRequest
    $initResponse = Read-McpResponse

    if ($initResponse.error) {
        throw "Initialize failed: $($initResponse.error.message)"
    }

    Write-Host "Connected to: $($initResponse.result.serverInfo.name) v$($initResponse.result.serverInfo.version)" -ForegroundColor Green

    # Step 2: Send initialized notification
    $initializedNotification = @{
        jsonrpc = "2.0"
        method = "notifications/initialized"
    }
    Send-McpMessage $initializedNotification

    # Interactive loop
    while ($true) {
        # Get question from user if not provided
        if (-not $Question) {
            Write-Host "`n" -NoNewline
            Write-Host "Enter your question about OPC UA (or 'quit' to exit):" -ForegroundColor Cyan

            # Show default query from input file if available
            if ($Queries.Count -gt 0) {
                $defaultQuery = $Queries[$QueryIndex]
                Write-Host "[Default: $defaultQuery]" -ForegroundColor Gray
                Write-Host "> " -NoNewline -ForegroundColor White
                $userInput = Read-Host

                # Use default if user just pressed enter
                if ([string]::IsNullOrWhiteSpace($userInput)) {
                    $userInput = $defaultQuery
                }

                # Advance to next query (wrap around)
                $QueryIndex = ($QueryIndex + 1) % $Queries.Count
            } else {
                Write-Host "> " -NoNewline -ForegroundColor White
                $userInput = Read-Host
            }

            if ($userInput -eq "quit" -or $userInput -eq "exit" -or $userInput -eq "q") {
                break
            }

            if ([string]::IsNullOrWhiteSpace($userInput)) {
                continue
            }

            $Question = $userInput
        }

        # Build the tool call
        $toolName = "specificationQuery"
        $toolArgs = @{ question = $Question }
        Write-Host "`nQuerying specification: $Question" -ForegroundColor Yellow

        Write-Host "Waiting for response (this may take a moment)..." -ForegroundColor Gray

        # Step 3: Call the tool
        $callRequest = @{
            jsonrpc = "2.0"
            id = 2
            method = "tools/call"
            params = @{
                name = $toolName
                arguments = $toolArgs
            }
        }
        Send-McpMessage $callRequest
        $callResponse = Read-McpResponse

        if ($callResponse.error) {
            Write-Host "`nError: $($callResponse.error.message)" -ForegroundColor Red
        } else {
            Write-Host "`n=== Response ===" -ForegroundColor Green
            foreach ($content in $callResponse.result.content) {
                if ($content.type -eq "text") {
                    Write-Host $content.text -ForegroundColor White
                }
            }
            Write-Host "================" -ForegroundColor Green
        }

        # Reset for next iteration if running interactively
        if ($PSBoundParameters.ContainsKey('Question')) {
            # If question was provided as parameter, exit after one query
            break
        }
        $Question = $null
    }

} finally {
    # Clean up
    Write-Host "`nClosing connection..." -ForegroundColor Gray
    if (-not $process.HasExited) {
        $process.StandardInput.Close()
        $process.WaitForExit(5000)
        if (-not $process.HasExited) {
            $process.Kill()
        }
    }
    $process.Dispose()
    Write-Host "Done." -ForegroundColor Green
}
