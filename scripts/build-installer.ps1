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

    throw "找不到 MSBuild。请安装 Visual Studio 的“.NET 桌面开发”工作负载。"
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory=$true)][string]$FilePath,
        [Parameter(Mandatory=$true)][string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "命令执行失败（退出码 $LASTEXITCODE）：$FilePath $($Arguments -join ' ')"
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

    Write-Host "下载构建依赖：$Url"
    Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $Path

    if ((Get-Item $Path).Length -lt $MinimumBytes) {
        throw "下载文件异常或不完整：$Path"
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

        Write-Host "== 还原 NuGet =="
        Invoke-Checked $msbuild @(
            $solution,
            "/t:Restore",
            "/p:Configuration=Release",
            "/p:Platform=x86"
        )

        Write-Host "== 编译 Release x86 =="
        Invoke-Checked $msbuild @(
            $solution,
            "/m",
            "/p:Configuration=Release",
            "/p:Platform=x86"
        )
    }

    $exe = Join-Path $releaseDir "Win7BookManagement.exe"
    if (-not (Test-Path $exe)) {
        throw "未找到 Release EXE：$exe"
    }

    Write-Host "== 运行应用自检 =="
    $selfTest = Start-Process -FilePath $exe -ArgumentList "--self-test" -Wait -PassThru
    if ($selfTest.ExitCode -ne 0) {
        throw "应用自检失败，退出码：$($selfTest.ExitCode)"
    }

    Write-Host "== 验证离线运行文件 =="
    $requiredFiles = @(
        "Win7BookManagement.exe",
        "Win7BookManagement.exe.config",
        "System.Data.SQLite.dll",
        "x86\SQLite.Interop.dll"
    )

    foreach ($relative in $requiredFiles) {
        $full = Join-Path $releaseDir $relative
        if (-not (Test-Path $full)) {
            throw "运行依赖缺失：$relative"
        }
    }

    $managedDlls = Get-ChildItem -Path $releaseDir -Filter "*.dll" -File -ErrorAction SilentlyContinue
    if ($managedDlls.Count -eq 0) {
        throw "Release 目录没有发现托管依赖 DLL。"
    }

    Write-Host "== 准备 .NET Framework 4.8 离线运行时 =="
    Ensure-Download -Url $dotnetUrl -Path $dotnetInstaller -MinimumBytes 100000000

    $dotnetSignature = Get-AuthenticodeSignature $dotnetInstaller
    if ($dotnetSignature.Status -ne "Valid") {
        Write-Warning ".NET 4.8 离线安装包签名状态：$($dotnetSignature.Status)。请在正式发布前人工核验。"
    }

    $iscc = Find-ISCC
    if (-not $iscc) {
        Write-Host "== 准备 Inno Setup 6.7.3 编译器 =="
        New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
        Ensure-Download -Url $innoUrl -Path $innoInstaller -MinimumBytes 3000000

        $innoSignature = Get-AuthenticodeSignature $innoInstaller
        if ($innoSignature.Status -ne "Valid") {
            Write-Warning "Inno Setup 安装包签名状态：$($innoSignature.Status)。"
        }

        $arguments = @(
            "/VERYSILENT",
            "/SUPPRESSMSGBOXES",
            "/NORESTART",
            "/DIR=$innoDir"
        )
        $process = Start-Process -FilePath $innoInstaller -ArgumentList $arguments -Wait -PassThru
        if ($process.ExitCode -ne 0) {
            throw "Inno Setup 编译器安装失败，退出码：$($process.ExitCode)"
        }

        $iscc = Find-ISCC
        if (-not $iscc) {
            throw "Inno Setup 已运行安装，但仍找不到 ISCC.exe。"
        }
    }

    Write-Host "== 生成离线 installer =="
    Invoke-Checked $iscc @($issFile)

    $output = Join-Path $installerDir "output\Win7BookManagement-Offline-Setup.exe"
    if (-not (Test-Path $output)) {
        throw "安装包生成后未找到：$output"
    }

    $sizeMb = [Math]::Round((Get-Item $output).Length / 1MB, 1)
    Write-Host ""
    Write-Host "==============================================="
    Write-Host "完成：$output"
    Write-Host "大小：$sizeMb MB"
    Write-Host "该安装包已包含应用 DLL、SQLite 原生 DLL 和 .NET Framework 4.8 离线运行时。"
    Write-Host "==============================================="
}
finally {
    Pop-Location
}
