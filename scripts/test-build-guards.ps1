$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "build-prerequisites.ps1")
$dir = Join-Path ([IO.Path]::GetTempPath()) ("bookdesk-guards-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $dir | Out-Null
try {
    $file = Join-Path $dir "fixture.bin"
    [IO.File]::WriteAllBytes($file, [byte[]](0x4D,0x5A,1,2,3))
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
    Assert-FileHash $file $hash
    $rejected = $false
    try { Assert-FileHash $file ("0" * 64) } catch { $rejected = $_.Exception.Message -match "SHA-256 mismatch" }
    if (-not $rejected) { throw "Hash mismatch was not rejected." }
    $rejected = $false
    try { Ensure-Download "https://example.invalid/unused" $file 1 ("0" * 64) } catch { $rejected = $_.Exception.Message -match "SHA-256 mismatch" }
    if (-not $rejected) { throw "Cached prerequisite mismatch was not rejected." }
    if (-not (Test-Path -LiteralPath $file)) { throw "A rejected cache must be preserved for diagnosis." }
    Write-Host "Build guard tests passed: valid hash, corrupt download hash, corrupt cache hash."
}
finally { Remove-Item -Recurse -Force $dir }
