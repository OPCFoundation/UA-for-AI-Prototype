$psql = "C:\Program Files\PostgreSQL\18\bin\psql.exe" 
$hostname = "localhost"
$port = 5433

if (-not $env:PGPASSWORD) {
    Write-Host 'No Password set. Call: $env:PGPASSWORD = <password>'
	exit 1
}

if ($env:PGHOST) {
    $hostname = $env:PGHOST
}

if ($env:PGPORT) {
    $port = $env:PGPORT
}

& $psql -h $hostname -p $port -b -v  -U postgres -d opcua-rag-vectors -f ".\db\opcua-specifications-backup.sql"