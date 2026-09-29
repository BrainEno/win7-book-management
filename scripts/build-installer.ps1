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

    if ((Get-Item $Path).Length -lt $MinimumBytes) {
        throw "Downloaded file is incomplete: $Path"
    }
}

function Find-ISCC {
    $candidates = @(
        (Join-Path $innoDir "ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path $candidate)) {
            return $candidate
        }
    }

    return $null
}

Push-Location $root
try {
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
        "x86\SQLite.Interop.dll"
    )

    foreach ($relative in $requiredFiles) {
        $full = Join-Path $releaseDir $relative
        if (-not (Test-Path $full)) {
            throw "Required runtime file is missing: $relative"
        }
    }

    $managedDlls = Get-ChildItem -Path $releaseDir -Filter "*.dll" -File -ErrorAction SilentlyContinue
    if ($managedDlls.Count -eq 0) {
        throw "No managed dependency DLLs were found in the Release directory."
    }

    Write-Host "== Prepare .NET Framework 4.8 offline runtime =="
    Ensure-Download -Url $dotnetUrl -Path $dotnetInstaller -MinimumBytes 100000000

    $dotnetSignature = Get-AuthenticodeSignature $dotnetInstaller
    if ($dotnetSignature.Status -ne "Valid") {
        Write-Warning ".NET Framework 4.8 installer signature status: $($dotnetSignature.Status)"
    }

    $iscc = Find-ISCC
    if (-not $iscc) {
        Write-Host "== Prepare Inno Setup 6.7.3 compiler =="
        New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
        Ensure-Download -Url $innoUrl -Path $innoInstaller -MinimumBytes 3000000

        $innoSignature = Get-AuthenticodeSignature $innoInstaller
        if ($innoSignature.Status -ne "Valid") {
            Write-Warning "Inno Setup installer signature status: $($innoSignature.Status)"
        }

        $dirArg = '/DIR="' + $innoDir + '"'
        $arguments = @(
            "/VERYSILENT",
            "/SUPPRESSMSGBOXES",
            "/NORESTART",
            $dirArg
        )
        $process = Start-Process -FilePath $innoInstaller -ArgumentList $arguments -Wait -PassThru
        if ($process.ExitCode -ne 0) {
            throw "Inno Setup compiler installation failed with exit code $($process.ExitCode)"
        }

        $iscc = Find-ISCC
        if (-not $iscc) {
            throw "ISCC.exe was not found after installing Inno Setup."
        }
    }

    Write-Host "== Build offline installer =="
    Invoke-Checked $iscc @($issFile)

    $output = Join-Path $installerDir "output\Win7BookManagement-Offline-Setup.exe"
    if (-not (Test-Path $output)) {
        throw "Installer output was not found: $output"
    }

    $sizeMb = [Math]::Round((Get-Item $output).Length / 1MB, 1)
    Write-Host ""
    Write-Host "==============================================="
    Write-Host "Installer ready: $output"
    Write-Host "Size: $sizeMb MB"
    Write-Host "The package includes app DLLs, SQLite native DLLs, and the .NET Framework 4.8 offline runtime."
    Write-Host "==============================================="
}
finally {
    Pop-Location
}
