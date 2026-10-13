param([string]$GamePath)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Find-Game.ps1')
$GamePath = Resolve-BongoGamePath -GamePath $GamePath
$target = (Resolve-Path -LiteralPath $GamePath).Path.TrimEnd('\')
if (-not (Test-Path -LiteralPath (Join-Path $target 'BongoCat.exe'))) { throw 'Choose the folder containing BongoCat.exe.' }
if (Get-Process -Name BongoCat -ErrorAction SilentlyContinue) { throw 'Close Bongo Cat before uninstalling.' }
$manifestPath = Join-Path $target 'BongoChat-install.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.gamePath -ne $target) { throw 'Installation directory does not match the manifest.' }
$modified = @()
foreach ($file in $manifest.files) {
    $destination = [IO.Path]::GetFullPath((Join-Path $target $file.relative))
    if (-not $destination.StartsWith($target + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe uninstall path.' }
    if (Test-Path -LiteralPath $destination -PathType Leaf) {
        if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $file.sha256) { $modified += $file.relative; continue }
        Remove-Item -LiteralPath $destination
    }
}
if ($modified.Count -gt 0) { Write-Warning ('Modified files preserved: ' + ($modified -join ', ')) }
$modifiedRuntime = @($modified | Where-Object { $_ -ne 'BepInEx\config\BepInEx.cfg' })
if ($modifiedRuntime.Count -eq 0) { Remove-Item -LiteralPath $manifestPath }
Write-Output 'Bridge removed. Chat records and generated configuration files were preserved.'
