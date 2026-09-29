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
$nsisDir = Join-Path $toolsDir "NSIS"
$nsisZip = Join-Path $toolsDir "nsis-3.13.zip"
$nsiFile = Join-Path $installerDir "win7-book-management.nsi"
$dotnetInstaller = Join-Path $prereqDir "NDP48-x86-x64-AllOS-ENU.exe"

$dotnetUrl = "https://download.microsoft.com/download/f/3/a/f3a6af84-da23-40a5-8d1c-49cc10c8e76f/NDP48-x86-x64-AllOS-ENU.exe"
$nsisUrl = "https://downloads.sourceforge.net/project/nsis/NSIS%203/3.13/nsis-3.13.zip"

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

function Assert-NetFramework48TargetingPack {
    $referenceRoot = Join-Path ${env:ProgramFiles(x86)} "Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8"
    $frameworkList = Join-Path $referenceRoot "RedistList\FrameworkList.xml"

    if (-not (Test-Path $frameworkList)) {
        throw @"
.NET Framework 4.8 targeting pack was not found.

Install one of the following on this development PC:
  1. Visual Studio -> Modify -> Individual components -> ".NET Framework 4.8 targeting pack"
  2. Microsoft .NET Framework 4.8 Developer Pack

This is only required on the PC that builds the program. The final offline installer already contains the .NET Framework 4.8 runtime for the Windows 7 target PC.
"@
    }
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

function Ensure-Download {
    param(
        [Parameter(Mandatory=$true)][string]$Url,
        [Parameter(Mandatory=$true)][string]$Path,
        [Parameter(Mandatory=$true)][long]$MinimumBytes
    )

    if ((Test-Path $Path) -and ((Get-Item $Path).Length -ge $MinimumBytes)) {
        return
    }

    $parent = Split-Path $Path -Parent
    New-Item -ItemType Directory -Force -Path $parent | Out-Null

    Write-Host "Downloading build prerequisite: $Url"
    Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $Path

    if (-not (Test-Path $Path) -or ((Get-Item $Path).Length -lt $MinimumBytes)) {
        throw "Downloaded file is incomplete: $Path"
    }
}

function Find-MakeNSIS {
    $candidates = @(
        (Join-Path $nsisDir "makensis.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "NSIS\makensis.exe"),
        (Join-Path $env:ProgramFiles "NSIS\makensis.exe")
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path $candidate)) {
            return $candidate
        }
    }

    if (Test-Path $nsisDir) {
        $found = Get-ChildItem -Path $nsisDir -Filter "makensis.exe" -File -Recurse -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($found) { return $found.FullName }
    }

    return $null
}

Push-Location $root
try {
    if (-not $SkipBuild) {
        $msbuild = Find-MSBuild
        Assert-NetFramework48TargetingPack

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
    if (-not (Test-Path $exe)) {
        throw "Release executable was not found: $exe"
    }

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
        if (-not (Test-Path $full)) {
            throw "Required runtime file is missing: $relative"
        }
    }

    Write-Host "== Prepare .NET Framework 4.8 offline runtime =="
    Ensure-Download -Url $dotnetUrl -Path $dotnetInstaller -MinimumBytes 100000000

    $dotnetSignature = Get-AuthenticodeSignature $dotnetInstaller
    if ($dotnetSignature.Status -ne "Valid") {
        Write-Warning ".NET Framework 4.8 installer signature status: $($dotnetSignature.Status)"
    }

    $makeNsis = Find-MakeNSIS
    if (-not $makeNsis) {
        Write-Host "== Prepare portable NSIS 3.13 compiler =="
        New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
        Ensure-Download -Url $nsisUrl -Path $nsisZip -MinimumBytes 1500000

        if (Test-Path $nsisDir) {
            Remove-Item -Recurse -Force $nsisDir
        }
        New-Item -ItemType Directory -Force -Path $nsisDir | Out-Null
        Expand-Archive -Path $nsisZip -DestinationPath $nsisDir -Force

        $makeNsis = Find-MakeNSIS
        if (-not $makeNsis) {
            throw "makensis.exe was not found after extracting NSIS 3.13."
        }
    }

    Write-Host "== Build offline installer with NSIS =="
    New-Item -ItemType Directory -Force -Path (Join-Path $installerDir "output") | Out-Null
    Push-Location $installerDir
    try {
        Invoke-Checked $makeNsis @("/V2", (Split-Path $nsiFile -Leaf))
    }
    finally {
        Pop-Location
    }

    $output = Join-Path $installerDir "output\Win7BookManagement-Offline-Setup.exe"
    if (-not (Test-Path $output)) {
        throw "Installer output was not found: $output"
    }

    $outputSize = (Get-Item $output).Length
    if ($outputSize -lt 100000000) {
        throw "Installer is unexpectedly small and may not contain the .NET Framework offline runtime."
    }

    $sizeMb = [Math]::Round($outputSize / 1MB, 1)
    Write-Host ""
    Write-Host "==============================================="
    Write-Host "Installer ready: $output"
    Write-Host "Size: $sizeMb MB"
    Write-Host "The package includes app DLLs, SQLite native DLLs, NPOI dependencies, and the .NET Framework 4.8 offline runtime."
    Write-Host "==============================================="
}
finally {
    Pop-Location
}
