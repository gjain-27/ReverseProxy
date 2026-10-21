param([int[]]$Ports = @(5001, 5002, 5003))

$exe = Join-Path $PSScriptRoot "TestBackend\bin\Debug\net10.0\TestBackend.exe"

if (-not (Test-Path $exe)) {
    Write-Error "TestBackend.exe not found. Build the solution first (Ctrl+Shift+B)."
    exit 1
}

foreach ($port in $Ports) {
    Start-Process $exe -ArgumentList $port
    Write-Host "Started TestBackend on port $port"
}
