$specRoot = Join-Path $PSScriptRoot "specifications"

Get-ChildItem -Path $specRoot -Filter "*.xml" -Recurse | ForEach-Object {
    $folder = $_.DirectoryName
    $suffix = $_.BaseName

    foreach ($file in @("rag-chunks.json", "image-descriptions.json", "README.md")) {
        $source = Join-Path $folder $file
        if (Test-Path $source) {
            $ext = [System.IO.Path]::GetExtension($file)
            $base = [System.IO.Path]::GetFileNameWithoutExtension($file)
            $newName = "$base-$suffix$ext"
            $dest = Join-Path $folder $newName
            Copy-Item -Path $source -Destination $dest
            Write-Host "Created: $dest"
        }
    }
}
