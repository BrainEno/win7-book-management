param(
    [string]$ProjectPath = "src\Win7BookManagement\Win7BookManagement.csproj"
)

$ErrorActionPreference = "Stop"

$project = (Resolve-Path $ProjectPath).Path
$projectDir = Split-Path -Parent $project

[xml]$xml = Get-Content -LiteralPath $project -Raw
$ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
$ns.AddNamespace("msb", "http://schemas.microsoft.com/developer/msbuild/2003")

$compileNodes = @($xml.SelectNodes("//msb:Compile", $ns))
if ($compileNodes.Count -eq 0) {
    throw "No <Compile Include=...> items found in $ProjectPath."
}

$includes = @()
foreach ($node in $compileNodes) {
    $value = [string]$node.Include
    if ([string]::IsNullOrWhiteSpace($value)) {
        continue
    }

    if ($value.Contains("*") -or $value.Contains("?")) {
        throw "Wildcard Compile item '$value' is forbidden in this legacy project. Add source files explicitly."
    }

    $normalized = $value.Replace("/", "\").TrimStart(".\").ToLowerInvariant()
    $includes += $normalized
}

$duplicates = $includes |
    Group-Object |
    Where-Object { $_.Count -gt 1 } |
    ForEach-Object { $_.Name }

if ($duplicates.Count -gt 0) {
    throw "Duplicate Compile items detected: $($duplicates -join ', ')"
}

$diskFiles = Get-ChildItem -LiteralPath $projectDir -Recurse -File -Filter *.cs |
    Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj|packages)[\\/]'
    } |
    ForEach-Object {
        $_.FullName.Substring($projectDir.Length).TrimStart("\", "/").Replace("/", "\").ToLowerInvariant()
    } |
    Sort-Object -Unique

$missingFromProject = @($diskFiles | Where-Object { $includes -notcontains $_ })
$missingOnDisk = @($includes | Where-Object {
    $candidate = Join-Path $projectDir $_
    -not (Test-Path -LiteralPath $candidate -PathType Leaf)
})

if ($missingFromProject.Count -gt 0 -or $missingOnDisk.Count -gt 0) {
    if ($missingFromProject.Count -gt 0) {
        Write-Host "C# files present on disk but missing from the .csproj:" -ForegroundColor Red
        $missingFromProject | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    }

    if ($missingOnDisk.Count -gt 0) {
        Write-Host "Compile items present in the .csproj but missing on disk:" -ForegroundColor Red
        $missingOnDisk | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    }

    throw "C# source manifest is out of sync. Update Win7BookManagement.csproj together with every added/removed .cs file."
}

Write-Host "C# source manifest OK: $($diskFiles.Count) files are explicitly included exactly once."
