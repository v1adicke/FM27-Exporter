# Usage: powershell -ExecutionPolicy Bypass -File uninstall-windows.ps1 [-All] [-DryRun] [-Game "<dir>"]
param([switch]$DryRun, [switch]$All, [string]$Game = "")
$ErrorActionPreference = "Stop"
if (-not $Game) {
    $steam = (Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -ErrorAction SilentlyContinue).SteamPath
    foreach ($r in @($steam, "${env:ProgramFiles(x86)}\Steam")) {
        if (-not $r) { continue }
        $vdf = Join-Path $r "steamapps\libraryfolders.vdf"
        if (-not (Test-Path $vdf)) { continue }
        foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) {
            $d = Join-Path ($m.Groups[1].Value -replace '\\\\', '\') "steamapps\common\Football Manager 27"
            if (Test-Path (Join-Path $d "fm.exe")) { $Game = $d }
        }
    }
}
if (-not $Game -or -not (Test-Path (Join-Path $Game "fm.exe"))) { throw "Football Manager 27 not found; pass -Game" }
function Remove-One($path) { if (Test-Path $path) { if ($DryRun) { Write-Host "would remove $path" } else { Remove-Item -LiteralPath $path -Recurse -Force } } }

$dest = Join-Path $Game "BepInEx\plugins\FM27Exporter"
$manifest = Join-Path $Game "BepInEx\fm27exporter-installed-files.txt"
$list = @(if (Test-Path $manifest) { Get-Content $manifest })
if ($All) {
    foreach ($rel in $list) { if ($rel) { Remove-One (Join-Path $Game ($rel.Replace('/', '\'))) } }
    foreach ($d in @("BepInEx\interop", "BepInEx\unity-libs", "BepInEx\cache")) { Remove-One (Join-Path $Game $d) }
    Remove-One (Join-Path $Game "BepInEx\config\local.fm27exporter.cfg")
    if ($list -contains "BepInEx/core/BepInEx.Core.dll") {
        foreach ($f in @("BepInEx\config\BepInEx.cfg", "BepInEx\LogOutput.log", "BepInEx\ErrorLog.log")) { Remove-One (Join-Path $Game $f) }
    }
}
Remove-One $dest
if ($All) { Remove-One $manifest }
if ($All -and -not $DryRun) {
    foreach ($top in @("BepInEx", "dotnet")) {
        $p = Join-Path $Game $top
        if (Test-Path $p) {
            Get-ChildItem -LiteralPath $p -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending |
                Where-Object { -not (Get-ChildItem -LiteralPath $_.FullName -Force) } | Remove-Item -Force
            if (-not (Get-ChildItem -LiteralPath $p -Force)) { Remove-Item -LiteralPath $p -Force }
        }
    }
}
Write-Host "Done."
