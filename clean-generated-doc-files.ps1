# clean-generated-doc-files.ps1
# Cleans up generated documentation files in the specifications directory
# - Removes all *.htm, *.json, and *.docx files
# - Removes associated *_files directories (HTML resource folders)
# - Keeps only the highest version of each *.xml file

param(
    [string]$SpecificationsPath = ".\specifications",
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

# Resolve to absolute path
$SpecificationsPath = Resolve-Path $SpecificationsPath

Write-Host "Cleaning generated documentation files in: $SpecificationsPath" -ForegroundColor Cyan

# Track statistics
$stats = @{
    HtmFilesRemoved = 0
    JsonFilesRemoved = 0
    DocxFilesRemoved = 0
    XmlFilesRemoved = 0
    DirectoriesRemoved = 0
}

# 1. Remove all *.htm files
$htmFiles = Get-ChildItem -Path $SpecificationsPath -Filter "*.htm" -Recurse -File
foreach ($file in $htmFiles) {
    if ($WhatIf) {
        Write-Host "[WhatIf] Would remove HTM file: $($file.FullName)" -ForegroundColor Yellow
    } else {
        Write-Host "Removing HTM file: $($file.FullName)" -ForegroundColor Red
        Remove-Item -Path $file.FullName -Force
    }
    $stats.HtmFilesRemoved++
}

# 2. Remove all *.json files
$jsonFiles = Get-ChildItem -Path $SpecificationsPath -Filter "*.json" -Recurse -File
foreach ($file in $jsonFiles) {
    if ($WhatIf) {
        Write-Host "[WhatIf] Would remove JSON file: $($file.FullName)" -ForegroundColor Yellow
    } else {
        Write-Host "Removing JSON file: $($file.FullName)" -ForegroundColor Red
        Remove-Item -Path $file.FullName -Force
    }
    $stats.JsonFilesRemoved++
}

# 3. Remove all *.docx files
$docxFiles = Get-ChildItem -Path $SpecificationsPath -Filter "*.docx" -Recurse -File
foreach ($file in $docxFiles) {
    if ($WhatIf) {
        Write-Host "[WhatIf] Would remove DOCX file: $($file.FullName)" -ForegroundColor Yellow
    } else {
        Write-Host "Removing DOCX file: $($file.FullName)" -ForegroundColor Red
        Remove-Item -Path $file.FullName -Force
    }
    $stats.DocxFilesRemoved++
}

# 4. Remove *_files directories (HTML resource folders)
$filesDirectories = Get-ChildItem -Path $SpecificationsPath -Filter "*_files" -Recurse -Directory
foreach ($dir in $filesDirectories) {
    if ($WhatIf) {
        Write-Host "[WhatIf] Would remove directory: $($dir.FullName)" -ForegroundColor Yellow
    } else {
        Write-Host "Removing directory: $($dir.FullName)" -ForegroundColor Red
        Remove-Item -Path $dir.FullName -Recurse -Force
    }
    $stats.DirectoriesRemoved++
}

# 5. Keep only the highest version of each XML file
# Version format: X.YY.ZZ (e.g., 1.05.02, 1.05.06)
# Files follow pattern: "OPC 10000-X - UA Specification Part X - Name VERSION.xml"

function Parse-Version {
    param([string]$VersionString)

    # Parse version like "1.05.06" into comparable parts
    $parts = $VersionString -split '\.'
    if ($parts.Count -ge 3) {
        return @{
            Major = [int]$parts[0]
            Minor = [int]$parts[1]
            Patch = [int]$parts[2]
        }
    }
    return $null
}

function Compare-Versions {
    param($Version1, $Version2)

    # Returns: negative if v1 < v2, 0 if equal, positive if v1 > v2
    if ($Version1.Major -ne $Version2.Major) {
        return $Version1.Major - $Version2.Major
    }
    if ($Version1.Minor -ne $Version2.Minor) {
        return $Version1.Minor - $Version2.Minor
    }
    return $Version1.Patch - $Version2.Patch
}

$xmlFiles = Get-ChildItem -Path $SpecificationsPath -Filter "*.xml" -Recurse -File

# Group XML files by their base name (everything before the version number)
# Pattern: extract base name by removing the version suffix (e.g., " 1.05.06.xml")
$xmlGroups = @{}

foreach ($file in $xmlFiles) {
    $fileName = $file.Name

    # Match pattern: "... X.YY.ZZ.xml" at the end
    if ($fileName -match '^(.+?)\s+(\d+\.\d+\.\d+)\.xml$') {
        $baseName = $Matches[1]
        $versionString = $Matches[2]
        $version = Parse-Version -VersionString $versionString

        if ($version) {
            $key = "$($file.DirectoryName)|$baseName"

            if (-not $xmlGroups.ContainsKey($key)) {
                $xmlGroups[$key] = @()
            }

            $xmlGroups[$key] += @{
                File = $file
                Version = $version
                VersionString = $versionString
            }
        }
    }
}

# For each group, keep only the highest version
foreach ($key in $xmlGroups.Keys) {
    $files = $xmlGroups[$key]

    if ($files.Count -le 1) {
        continue
    }

    # Sort by version descending
    $sorted = $files | Sort-Object -Property @{
        Expression = {
            $_.Version.Major * 10000 + $_.Version.Minor * 100 + $_.Version.Patch
        }
    } -Descending

    $highest = $sorted[0]
    Write-Host "Keeping highest version: $($highest.File.Name)" -ForegroundColor Green

    # Remove all others
    for ($i = 1; $i -lt $sorted.Count; $i++) {
        $fileToRemove = $sorted[$i]
        if ($WhatIf) {
            Write-Host "[WhatIf] Would remove older XML: $($fileToRemove.File.FullName)" -ForegroundColor Yellow
        } else {
            Write-Host "Removing older XML: $($fileToRemove.File.FullName)" -ForegroundColor Red
            Remove-Item -Path $fileToRemove.File.FullName -Force
        }
        $stats.XmlFilesRemoved++
    }
}

# Print summary
Write-Host "`n=== Cleanup Summary ===" -ForegroundColor Cyan
Write-Host "HTM files removed: $($stats.HtmFilesRemoved)" -ForegroundColor White
Write-Host "JSON files removed: $($stats.JsonFilesRemoved)" -ForegroundColor White
Write-Host "DOCX files removed: $($stats.DocxFilesRemoved)" -ForegroundColor White
Write-Host "Older XML versions removed: $($stats.XmlFilesRemoved)" -ForegroundColor White
Write-Host "Directories removed: $($stats.DirectoriesRemoved)" -ForegroundColor White

if ($WhatIf) {
    Write-Host "`nThis was a dry run. No files were actually removed." -ForegroundColor Yellow
    Write-Host "Run without -WhatIf to perform the actual cleanup." -ForegroundColor Yellow
}
