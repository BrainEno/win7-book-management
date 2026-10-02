param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solution = Join-Path $root "Win7BookManagement.sln"
$releaseDir = Join-Path $root "src\Win7BookManagement\bin\x86\Release"
$installerDir = Join-Path $root "installer"
$prereqDir = Join-Path $installerDir "prerequisites"
$toolsDir = Join-Path $root ".tools"
$innoDir = Join-Path $toolsDir "InnoSetup6"
$innoInstaller = Join-Path $toolsDir "innosetup-6.7.3.exe"
$issFile = Join-Path $installerDir "win7-book-management.iss"
$dotnetInstaller = Join-Path $prereqDir "NDP48-x86-x64-AllOS-ENU.exe"
$iconGenerator = Join-Path $root "scripts\generate-app-icon.ps1"
$appIcon = Join-Path $root "src\Win7BookManagement\Resources\BookDesk.ico"

$dotnetUrl = "https://download.microsoft.com/download/f/3/a/f3a6af84-da23-40a5-8d1c-49cc10c8e76f/NDP48-x86-x64-AllOS-ENU.exe"
$innoUrl = "https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe"

function Find-MSBuild {
    $cmd = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $path = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" |
            Select-Object -First 1
        if ($path) { return $path }
    }

    throw "MSBuild was not found. Install the .NET desktop development workload in Visual Studio."
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory=$true)][string]$FilePath,
        [Parameter(Mandatory=$true)][string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE : $FilePath $($Arguments -join ' ')"
    }
}

function Test-PortableExe {
    param(
        [Parameter(Mandatory=$true)][string]$Path,
        [Parameter(Mandatory=$true)][long]$MinimumBytes
    )

    if (-not (Test-Path $Path)) { return $false }
    if ((Get-Item $Path).Length -lt $MinimumBytes) { return $false }

    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $first = $stream.ReadByte()
        $second = $stream.ReadByte()
        return ($first -eq 0x4D -and $second -eq 0x5A)
    }
    finally {
        $stream.Dispose()
    }
}

function Ensure-Download {
    param(
        [Parameter(Mandatory=$true)][string]$Url,
        [Parameter(Mandatory=$true)][string]$Path,
        [Parameter(Mandatory=$true)][long]$MinimumBytes,
        [switch]$RequireExe
    )

    if (Test-Path $Path) {
        $validSize = (Get-Item $Path).Length -ge $MinimumBytes
        $validType = (-not $RequireExe) -or (Test-PortableExe -Path $Path -MinimumBytes $MinimumBytes)
        if ($validSize -and $validType) { return }
        Remove-Item -Force $Path
    }

    $parent = Split-Path $Path -Parent
    New-Item -ItemType Directory -Force -Path $parent | Out-Null

    Write-Host "Downloading build prerequisite: $Url"
    Invoke-WebRequest -UseBasicParsing -Headers @{ "User-Agent" = "Wget" } -Uri $Url -OutFile $Path

    if (-not (Test-Path $Path) -or ((Get-Item $Path).Length -lt $MinimumBytes)) {
        throw "Downloaded file is incomplete: $Path"
    }

    if ($RequireExe -and -not (Test-PortableExe -Path $Path -MinimumBytes $MinimumBytes)) {
        throw "Downloaded file is not a valid Windows executable: $Path"
    }
}

function Find-ISCC {
    $candidate = Join-Path $innoDir "ISCC.exe"
    if (Test-Path $candidate) { return $candidate }
    return $null
}

function Ensure-InnoSetupCompiler {
    $iscc = Find-ISCC
    if ($iscc) { return $iscc }

    Write-Host "== Prepare Inno Setup 6.7.3 compiler =="
    New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null

    Ensure-Download -Url $innoUrl -Path $innoInstaller -MinimumBytes 3000000 -RequireExe

    $signature = Get-AuthenticodeSignature $innoInstaller
    if ($signature.Status -ne "Valid") {
        throw "Inno Setup installer Authenticode signature is not valid: $($signature.Status)"
    }

    if (Test-Path $innoDir) { Remove-Item -Recurse -Force $innoDir }
    New-Item -ItemType Directory -Force -Path $innoDir | Out-Null

    Write-Host "Installing Inno Setup 6.7.3 into project tools directory..."
    $arguments = @(
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "/PORTABLE=1",
        "/CURRENTUSER",
        ("/DIR=" + $innoDir)
    )

    $process = Start-Process -FilePath $innoInstaller -ArgumentList $arguments -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Inno Setup compiler installation failed with exit code $($process.ExitCode)"
    }

    $iscc = Find-ISCC
    if (-not $iscc) {
        throw "ISCC.exe was not found after preparing Inno Setup 6.7.3."
    }

    return $iscc
}

Push-Location $root
try {
    Write-Host "== Generate BOOK DESK application icon =="
    Invoke-Checked "powershell.exe" @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $iconGenerator,
        "-OutputPath",
        $appIcon
    )

    if (-not $SkipBuild) {
        $msbuild = Find-MSBuild

        Write-Host "== Restore NuGet packages =="
        Invoke-Checked $msbuild @(
            $solution,
            "/t:Restore",
            "/p:Configuration=Release",
            "/p:Platform=x86"
        )

        Write-Host "== Build Release x86 =="
        Invoke-Checked $msbuild @(
            $solution,
            "/m",
            "/p:Configuration=Release",
            "/p:Platform=x86"
        )
    }

    $exe = Join-Path $releaseDir "Win7BookManagement.exe"
    if (-not (Test-Path $exe)) { throw "Release executable was not found: $exe" }

    Write-Host "== Run application self-test =="
    $selfTest = Start-Process -FilePath $exe -ArgumentList "--self-test" -Wait -PassThru
    if ($selfTest.ExitCode -ne 0) {
        throw "Application self-test failed with exit code $($selfTest.ExitCode)"
    }

    Write-Host "== Validate offline runtime files =="
    $requiredFiles = @(
        "Win7BookManagement.exe",
        "Win7BookManagement.exe.config",
        "System.Data.SQLite.dll",
        "x86\SQLite.Interop.dll",
        "NPOI.Core.dll",
        "NPOI.OOXML.dll"
    )

    foreach ($relative in $requiredFiles) {
        $full = Join-Path $releaseDir $relative
        if (-not (Test-Path $full)) { throw "Required runtime file is missing: $relative" }
    }

    Write-Host "== Prepare .NET Framework 4.8 offline runtime =="
    Ensure-Download -Url $dotnetUrl -Path $dotnetInstaller -MinimumBytes 100000000 -RequireExe

    $dotnetSignature = Get-AuthenticodeSignature $dotnetInstaller
    if ($dotnetSignature.Status -ne "Valid") {
        Write-Warning ".NET Framework 4.8 installer signature status: $($dotnetSignature.Status)"
    }

    $iscc = Ensure-InnoSetupCompiler

    Write-Host "== Build offline installer with Inno Setup 6 =="
    New-Item -ItemType Directory -Force -Path (Join-Path $installerDir "output") | Out-Null

    Push-Location $installerDir
    try {
        Invoke-Checked $iscc @((Split-Path $issFile -Leaf))
    }
    finally {
        Pop-Location
    }

    $output = Join-Path $installerDir "output\Win7BookManagement-Offline-Setup.exe"
    if (-not (Test-Path $output)) { throw "Installer output was not found: $output" }

    $outputSize = (Get-Item $output).Length
    if ($outputSize -lt 100000000) {
        throw "Installer is unexpectedly small and may not contain the .NET Framework 4.8 offline runtime."
    }

    $sizeMb = [Math]::Round($outputSize / 1MB, 1)
    Write-Host ""
    Write-Host "==============================================="
    Write-Host "Installer ready: $output"
    Write-Host "Size: $sizeMb MB"
    Write-Host "Builder: Inno Setup 6.7.3"
    Write-Host "Minimum target OS: Windows 7 SP1"
    Write-Host "The package includes app DLLs, SQLite native DLLs, NPOI dependencies, and the .NET Framework 4.8 offline runtime."
    Write-Host "==============================================="
}
finally {
    Pop-Location
}
