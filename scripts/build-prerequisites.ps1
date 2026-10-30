# Dot-sourced by build-installer.ps1 and its offline guard tests.
$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Assert-FileHash {
    param([Parameter(Mandatory=$true)][string]$Path,
          [Parameter(Mandatory=$true)][string]$ExpectedSha256)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Prerequisite is missing: $Path" }
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    if ($actual -ne $ExpectedSha256.ToUpperInvariant()) {
        throw "SHA-256 mismatch for $Path. Expected $ExpectedSha256; got $actual. Refusing to execute or package this file. Delete the corrupted cache only after verifying the official source."
    }
}

function Test-PortableExe {
    param([Parameter(Mandatory=$true)][string]$Path)
    $stream = [System.IO.File]::OpenRead($Path)
    try { return ($stream.ReadByte() -eq 0x4D -and $stream.ReadByte() -eq 0x5A) }
    finally { $stream.Dispose() }
}

function Ensure-Download {
    param([Parameter(Mandatory=$true)][string]$Url,
          [Parameter(Mandatory=$true)][string]$Path,
          [Parameter(Mandatory=$true)][long]$MinimumBytes,
          [Parameter(Mandatory=$true)][string]$ExpectedSha256)
    if (Test-Path -LiteralPath $Path) {
        # Never silently replace a hash mismatch: fail closed, including cached files.
        Assert-FileHash -Path $Path -ExpectedSha256 $ExpectedSha256
        if ((Get-Item -LiteralPath $Path).Length -lt $MinimumBytes -or -not (Test-PortableExe $Path)) {
            throw "Prerequisite has the expected hash but failed its size/PE guard: $Path"
        }
        return
    }
    $parent = Split-Path $Path -Parent
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    $partial = $Path + ".partial-" + [Guid]::NewGuid().ToString("N")
    try {
        Write-Host "Downloading build prerequisite: $Url"
        Invoke-WebRequest -UseBasicParsing -Headers @{ "User-Agent" = "BookDesk-build" } -Uri $Url -OutFile $partial
        if ((Get-Item -LiteralPath $partial).Length -lt $MinimumBytes) { throw "Downloaded file is incomplete: $Path" }
        Assert-FileHash -Path $partial -ExpectedSha256 $ExpectedSha256
        if (-not (Test-PortableExe $partial)) { throw "Downloaded file is not a Windows executable: $Path" }
        Move-Item -LiteralPath $partial -Destination $Path
    }
    finally {
        if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial -Force }
    }
}
