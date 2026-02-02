$pdump = "C:\Program Files\PostgreSQL\18\bin\pg_dump.exe" 
$hostname = "opcf-pgsql-server-02.postgres.database.azure.com"
$port = 5432

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

& $pdump -h $hostname -p $port -b -v --no-owner --no-privileges -U dbadmin -d opcua-specifications -f ".\db\opcua-specifications-backup.sql"