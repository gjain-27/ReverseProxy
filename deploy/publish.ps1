$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $PSScriptRoot "out"

Push-Location $root
try {
    dotnet publish Proxy -c Release -r linux-x64 --self-contained true -o (Join-Path $out "proxy")
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Publishing Proxy failed"
        exit 1
    }

    dotnet publish TestBackend -c Release -r linux-x64 --self-contained true -o (Join-Path $out "backend")
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Publishing TestBackend failed"
        exit 1
    }

    Write-Host "Published to $out"
}
finally {
    Pop-Location
}
