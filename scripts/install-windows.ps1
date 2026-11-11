# Usage: powershell -ExecutionPolicy Bypass -File install-windows.ps1 [-DryRun] [-Game "<dir with fm.exe>"]
param([switch]$DryRun, [string]$Game = "")
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-Game {
    $steam = (Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -ErrorAction SilentlyContinue).SteamPath
    $roots = @()
    if ($steam) { $roots += $steam }
    $roots += "${env:ProgramFiles(x86)}\Steam"
    foreach ($r in $roots) {
        $vdf = Join-Path $r "steamapps\libraryfolders.vdf"
        if (-not (Test-Path $vdf)) { continue }
        foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) {
            $d = Join-Path ($m.Groups[1].Value -replace '\\\\', '\') "steamapps\common\Football Manager 27"
            if (Test-Path (Join-Path $d "fm.exe")) { return $d }
        }
    }
    return $null
}

if (-not $Game) { $Game = Find-Game }
if (-not $Game -or -not (Test-Path (Join-Path $Game "fm.exe"))) { throw "Football Manager 27 not found; pass -Game `"<dir with fm.exe>`"" }
Write-Host "Game: $Game"
if (Get-Process fm -ErrorAction SilentlyContinue) { throw "FM27 is running - close it first." }

$payload = Join-Path $here "payload"
$plugin = Join-Path $payload "BepInEx\plugins\FM27Exporter\FM27Exporter.dll"
if (-not (Test-Path $plugin)) {
    $plugin = Join-Path $here "..\src\FM27Exporter\bin\Release\net6.0\FM27Exporter.dll"
    $payload = Join-Path $here "..\vendor\bepinex"
}
if (-not (Test-Path $plugin)) { throw "plugin DLL not found" }

$added = New-Object System.Collections.Generic.List[string]
function Copy-One($src, $rel) {
    $dst = Join-Path $Game $rel
    if ($DryRun) { Write-Host "would copy $rel"; return }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dst) | Out-Null
    Copy-Item -LiteralPath $src -Destination $dst -Force
}

if (Test-Path (Join-Path $Game "BepInEx\core\BepInEx.Unity.IL2CPP.dll")) {
    Write-Host "BepInEx already installed - leaving it as it is."
} else {
    if ((Test-Path (Join-Path $Game "winhttp.dll")) -or (Test-Path (Join-Path $Game "doorstop_config.ini"))) {
        throw "winhttp.dll/doorstop_config.ini already present from something else - not touching it."
    }
    $root = (Resolve-Path $payload).Path
    foreach ($f in Get-ChildItem -LiteralPath $root -Recurse -File) {
        $rel = $f.FullName.Substring($root.Length + 1)
        if ($rel -like "BepInEx\plugins\FM27Exporter\*" -or $rel -like "install-*" -or $rel -like "uninstall-*" -or $rel -like "README*") { continue }
        if (-not ($rel -like "BepInEx\*" -or $rel -like "dotnet\*" -or $rel -in @("winhttp.dll", "doorstop_config.ini", "changelog.txt", ".doorstop_version"))) { continue }
        if (Test-Path (Join-Path $Game $rel)) { Write-Host "exists, skipped: $rel"; continue }
        Copy-One $f.FullName $rel
        $added.Add($rel.Replace('\', '/'))
    }
}
Copy-One $plugin "BepInEx\plugins\FM27Exporter\FM27Exporter.dll"
$added.Add("BepInEx/plugins/FM27Exporter/FM27Exporter.dll")

if (-not $DryRun) {
    $manifest = Join-Path $Game "BepInEx\fm27exporter-installed-files.txt"
    $old = @(if (Test-Path $manifest) { Get-Content $manifest })
    ($old + $added) | Sort-Object -Unique | Set-Content -Encoding UTF8 $manifest
}
Write-Host "Installed $($added.Count) file(s). Start FM27, open a squad / search / shortlist and press F8."
Write-Host "Exports: Documents\Sports Interactive\Football Manager 27\exports\   Log: $Game\BepInEx\LogOutput.log"
