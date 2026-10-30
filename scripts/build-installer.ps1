param(
    [switch]$SkipBuild,
    [string]$SignCertificatePath,
    [string]$SignCertificatePassword,
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "build-prerequisites.ps1")
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

$dotnetUrl = "https://download.visualstudio.microsoft.com/download/pr/2d6bb6b2-226a-4baa-bdec-798822606ff1/8494001c276a4b96804cde7829c04d7f/ndp48-x86-x64-allos-enu.exe"
$innoUrl = "https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe"
# Pinned official artifacts. .NET hash is the published Microsoft 4.8 offline installer;
# Inno hash also matches the GitHub release asset digest for is-6_7_3.
$dotnetSha256 = "68C9986A8DCC0214D909AA1F31BEE9FB5461BB839EDCA996A75B08DDFFC1483F"
$innoSha256 = "9C73C3BAE7ED48D44112A0F48E66742C00090BDB5BEF71D9D3C056C66E97B732"
$version = (Get-Content -LiteralPath (Join-Path $root "VERSION") -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "VERSION must be a stable semantic version." }
if ($env:GITHUB_REF -match '^refs/tags/v(.+)$' -and $Matches[1] -ne $version) {
    throw "Release tag v$($Matches[1]) does not match VERSION $version."
}

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

function Invoke-CodeSign {
    param([Parameter(Mandatory=$true)][string]$Path)
    if ([string]::IsNullOrWhiteSpace($SignCertificatePath)) { return }
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $signtool) { throw "Signing was requested but signtool.exe was not found." }
    Invoke-Checked $signtool.FullName @("sign", "/fd", "SHA256", "/tr", $TimestampUrl, "/td", "SHA256", "/f", $SignCertificatePath, "/p", $SignCertificatePassword, $Path)
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne "Valid") { throw "Code signature is not valid: $Path ($($signature.Status))" }
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

    Ensure-Download -Url $innoUrl -Path $innoInstaller -MinimumBytes 3000000 -ExpectedSha256 $innoSha256

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
    if (-not (Test-Path -LiteralPath $appIcon)) { throw "The checked-in application icon is missing: $appIcon. Run scripts/generate-app-icon.ps1 explicitly to regenerate it." }

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

    Invoke-CodeSign $exe
    if ([string]::IsNullOrWhiteSpace($SignCertificatePath)) { Write-Warning "No signing certificate configured; this build is UNSIGNED." }

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
    Ensure-Download -Url $dotnetUrl -Path $dotnetInstaller -MinimumBytes 100000000 -ExpectedSha256 $dotnetSha256

    $dotnetSignature = Get-AuthenticodeSignature $dotnetInstaller
    if ($dotnetSignature.Status -ne "Valid") {
        throw ".NET Framework 4.8 installer signature is not valid: $($dotnetSignature.Status)"
    }
    if ($dotnetSignature.SignerCertificate.Subject -notmatch "Microsoft") {
        throw ".NET Framework 4.8 installer is not signed by Microsoft: $($dotnetSignature.SignerCertificate.Subject)"
    }

    $iscc = Ensure-InnoSetupCompiler

    Write-Host "== Build offline installer with Inno Setup 6 =="
    New-Item -ItemType Directory -Force -Path (Join-Path $installerDir "output") | Out-Null

    Push-Location $installerDir
    try {
        Invoke-Checked $iscc @("/DAppVersion=$version", (Split-Path $issFile -Leaf))
    }
    finally {
        Pop-Location
    }

    $output = Join-Path $installerDir "output\Win7BookManagement-Offline-Setup.exe"
    if (-not (Test-Path $output)) { throw "Installer output was not found: $output" }

    Invoke-CodeSign $output
    $outputSize = (Get-Item $output).Length
    if ($outputSize -lt 100000000) {
        throw "Installer is unexpectedly small and may not contain the .NET Framework 4.8 offline runtime."
    }

    $sizeMb = [Math]::Round($outputSize / 1MB, 1)
    Write-Host ""
    Write-Host "==============================================="
    Write-Host "Installer ready: $output"
    Write-Host "Version: $version"
    Write-Host "SHA-256: $((Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash)"
    Write-Host "Size: $sizeMb MB"
    Write-Host "Builder: Inno Setup 6.7.3"
    Write-Host "Minimum target OS: Windows 7 SP1"
    Write-Host "The package includes app DLLs, SQLite native DLLs, NPOI dependencies, and the .NET Framework 4.8 offline runtime."
    Write-Host "==============================================="
}
finally {
    Pop-Location
}
