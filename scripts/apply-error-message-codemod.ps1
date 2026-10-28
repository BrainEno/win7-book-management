param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [switch]$DryRun,
    [switch]$IncludeMessagesOutsideMessageBox
)

$ErrorActionPreference = "Stop"
$formsDir = Join-Path $RepoRoot "src/Win7BookManagement/Forms"
$backupRoot = Join-Path $RepoRoot ("artifacts/codemod-backup-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
$changedFiles = 0
$changedLines = 0
$report = New-Object System.Collections.Generic.List[string]

Get-ChildItem $formsDir -Filter *.cs -Recurse | ForEach-Object {
    $path = $_.FullName
    $lines = Get-Content -Encoding UTF8 $path
    $touched = $false

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -notmatch 'ex\.Message') { continue }

        $isMessageBoxLine = $line -match 'MessageBox\.Show' -or ($i + 1 -lt $lines.Count -and $lines[$i + 1] -match 'MessageBox\.Show')
        if (-not $isMessageBoxLine -and -not $IncludeMessagesOutsideMessageBox) { continue }

        $newLine = $line -replace 'ex\.Message', 'UserErrorText.For(ex)'
        if ($newLine -ne $line) {
            $report.Add(($_.Name + ":" + ($i + 1) + "  " + $line.Trim() + "  ->  " + $newLine.Trim()))
            $lines[$i] = $newLine
            $touched = $true
            $changedLines++
        }
    }

    if ($touched) {
        $changedFiles++

        $hasUsing = ($lines | Where-Object { $_ -match '^using Win7BookManagement.Infrastructure;' }).Count -gt 0
        if (-not $hasUsing) {
            $lastUsing = -1
            for ($u = 0; $u -lt $lines.Count; $u++) { if ($lines[$u] -match '^using ') { $lastUsing = $u } }
            if ($lastUsing -ge 0) {
                $lines = @($lines[0..$lastUsing]) + 'using Win7BookManagement.Infrastructure;' + @($lines[($lastUsing + 1)..($lines.Count - 1)])
                $report.Add(($_.Name + "  + using Win7BookManagement.Infrastructure;"))
            }
        }

        if (-not $DryRun) {
            $target = Join-Path $backupRoot ($_.Directory.Name + "-" + $_.Name)
            New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
            Copy-Item $path $target
            [System.IO.File]::WriteAllLines($path, $lines, (New-Object System.Text.UTF8Encoding($true)))
        }
    }
}

$report | ForEach-Object { Write-Host "  $_" }
Write-Host ""
Write-Host ("涉及文件 " + $changedFiles + " 个，替换 " + $changedLines + " 处。") -ForegroundColor Cyan

if ($DryRun) {
    Write-Host "DryRun：未修改任何文件。确认后去掉 -DryRun 执行，备份写入 artifacts/。" -ForegroundColor Yellow
} else {
    Write-Host "已修改并备份到：$backupRoot" -ForegroundColor Green
    Write-Host "请把 UserErrorText 所在命名空间加入对应窗体的 using 列表，然后重新编译。" -ForegroundColor Yellow
}
