# generate-markdown-docs.ps1
# Generates markdown documentation, describes images, generates RAG chunks,
# or embeds specifications for each OPC UA specification XML file
# using the Opc.Ua.RagUtility tool

param(
    [ValidateSet("markdown", "images", "chunks", "embed")]
    [string]$Operation = "markdown",
    [string]$SpecificationsPath = ".\specifications",
    [string]$ProjectPath = ".\Opc.Ua.RagUtility",
    [string]$OllamaUrl = "http://localhost:11434",
    [string]$QdrantUrl = "http://localhost:6333",
    [string]$CollectionName = "opcua-specifications",
    [int]$Tokens = 400,  # Default token count for RAG chunks
    [string]$Filter,  # Filter to select only directories containing this string
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

# Resolve to absolute paths
$SpecificationsPath = Resolve-Path $SpecificationsPath
$ProjectPath = Resolve-Path $ProjectPath

$operationDescription = switch ($Operation) {
    "markdown" { "markdown documentation" }
    "images"   { "image descriptions" }
    "chunks"   { "RAG chunks" }
    "embed"    { "embeddings" }
}

Write-Host "Generating $operationDescription for files in: $SpecificationsPath" -ForegroundColor Cyan
Write-Host "Using RagUtility project at: $ProjectPath" -ForegroundColor Cyan
if ($Operation -eq "chunks") {
    Write-Host "Token count: $Tokens" -ForegroundColor Cyan
}
if ($Operation -eq "embed" -or $Operation -eq "images") {
    Write-Host "Ollama URL: $OllamaUrl" -ForegroundColor Cyan
}
if ($Operation -eq "embed") {
    Write-Host "Qdrant URL: $QdrantUrl" -ForegroundColor Cyan
    Write-Host "Collection Name: $CollectionName" -ForegroundColor Cyan
}
if ($Filter) {
    Write-Host "Filter: $Filter" -ForegroundColor Cyan
}

# Build the project first for better performance
Write-Host "`nBuilding Opc.Ua.RagUtility..." -ForegroundColor Yellow
dotnet build $ProjectPath --configuration Release --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build Opc.Ua.RagUtility" -ForegroundColor Red
    exit 1
}
Write-Host "Build successful." -ForegroundColor Green

# Find files based on operation
if ($Operation -eq "embed") {
    $inputFiles = Get-ChildItem -Path $SpecificationsPath -Filter "rag-chunks.json" -Recurse -File
    $fileType = "rag-chunks.json"
} else {
    $inputFiles = Get-ChildItem -Path $SpecificationsPath -Filter "*.xml" -Recurse -File
    $fileType = "XML"
}

# Apply directory filter if specified (exact match on directory name)
if ($Filter) {
    $inputFiles = $inputFiles | Where-Object {
        $dirParts = $_.DirectoryName -split '[/\\]'
        $dirParts -contains $Filter
    }
}

if ($inputFiles.Count -eq 0) {
    $filterMsg = if ($Filter) { " matching filter '$Filter'" } else { "" }
    Write-Host "No $fileType files found in $SpecificationsPath$filterMsg" -ForegroundColor Yellow
    exit 0
}

Write-Host "`nFound $($inputFiles.Count) $fileType file(s) to process." -ForegroundColor Cyan

# Track statistics
$stats = @{
    Processed = 0
    Failed = 0
}

foreach ($inputFile in $inputFiles) {
    $inputPath = $inputFile.FullName
    $outputDir = $inputFile.DirectoryName

    Write-Host "`n[$($stats.Processed + $stats.Failed + 1)/$($inputFiles.Count)] Processing: $($inputFile.Name)" -ForegroundColor White

    switch ($Operation) {
        "markdown" {
            $outputPath = $outputDir
            $commandArgs = @("markdown", "-i", $inputPath, "-o", $outputPath)
            $successMessage = "Generated README.md"
        }
        "images" {
            $outputPath = Join-Path $outputDir "image-descriptions.json"
            $commandArgs = @("describe-images", "-i", $inputPath, "-o", $outputPath, "-a", $OllamaUrl)
            $successMessage = "Generated image-descriptions.json"
        }
        "chunks" {
            $outputPath = Join-Path $outputDir "rag-chunks.json"
            $imageDescPath = Join-Path $outputDir "image-descriptions.json"
            $commandArgs = @("generate-chunks", "-i", $inputPath, "-o", $outputPath, "-t", $Tokens)

            # Check if image descriptions exist and add to arguments
            if (Test-Path $imageDescPath) {
                $commandArgs += @("-m", $imageDescPath)
                Write-Host "  Using image descriptions: $imageDescPath" -ForegroundColor DarkGray
            }

            $successMessage = "Generated rag-chunks.json"
        }
        "embed" {
            $outputPath = "Qdrant: $CollectionName"
            $commandArgs = @("embed", "-i", $inputPath, "-a", $OllamaUrl, "--db", $QdrantUrl, "-n", $CollectionName)
            $successMessage = "Embedded to Qdrant collection"
        }
    }

    if ($WhatIf) {
        Write-Host "  [WhatIf] Would run: dotnet run --project `"$ProjectPath`" -- $($commandArgs -join ' ')" -ForegroundColor Yellow
        $stats.Processed++
        continue
    }

    Write-Host "  Input:  $inputPath" -ForegroundColor Gray
    Write-Host "  Output: $outputPath" -ForegroundColor Gray

    # Run dotnet with the command arguments
    & dotnet run --project $ProjectPath --configuration Release --no-build -- @commandArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host "  Failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        $stats.Failed++
    } else {
        Write-Host "  Success: $successMessage" -ForegroundColor Green
        $stats.Processed++
    }
}

# Print summary
Write-Host "`n=== Generation Summary ===" -ForegroundColor Cyan
Write-Host "Operation: $Operation" -ForegroundColor White
if ($Filter) {
    Write-Host "Filter: $Filter" -ForegroundColor White
}
Write-Host "Successfully processed: $($stats.Processed)" -ForegroundColor Green
Write-Host "Failed: $($stats.Failed)" -ForegroundColor $(if ($stats.Failed -gt 0) { "Red" } else { "White" })

if ($WhatIf) {
    Write-Host "`nThis was a dry run. No files were actually generated." -ForegroundColor Yellow
    Write-Host "Run without -WhatIf to perform the actual generation." -ForegroundColor Yellow
}
