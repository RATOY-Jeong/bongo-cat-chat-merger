param([string]$GamePath, [string]$PackagePath = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Find-Game.ps1')
$GamePath = Resolve-BongoGamePath -GamePath $GamePath
$target = (Resolve-Path -LiteralPath $GamePath).Path.TrimEnd('\')
if (-not (Test-Path -LiteralPath (Join-Path $target 'BongoCat.exe'))) { throw 'Choose the folder containing BongoCat.exe.' }
if (Get-Process -Name BongoCat -ErrorAction SilentlyContinue) { throw 'Close Bongo Cat before installing.' }
$manifestPath = Join-Path $target 'BongoChat-install.json'
if (Test-Path -LiteralPath $manifestPath) { throw 'Bongo Chat is already installed. Uninstall the existing bridge first.' }
foreach ($name in @('winhttp.dll','version.dll','doorstop_config.ini','BepInEx\core\BepInEx.dll','MelonLoader')) {
    if (Test-Path -LiteralPath (Join-Path $target $name)) { throw "Existing mod loader detected: $name. Installation stopped to preserve it." }
}
$source = Join-Path $PackagePath 'runtime'
if (-not (Test-Path -LiteralPath (Join-Path $source 'winhttp.dll'))) { throw 'Runtime package is missing.' }
$pluginFile = Join-Path $PackagePath 'BongoChatBridge.dll'
if (-not (Test-Path -LiteralPath $pluginFile)) { $pluginFile = Join-Path $PackagePath 'build\BongoChatBridge.dll' }
if (-not (Test-Path -LiteralPath $pluginFile)) { throw 'Bridge DLL is missing.' }
$files = @()
foreach ($file in (Get-ChildItem -LiteralPath $source -File -Recurse)) {
    $relative = $file.FullName.Substring($source.Length + 1)
    $destination = [IO.Path]::GetFullPath((Join-Path $target $relative))
    if (-not $destination.StartsWith($target + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe installation path.' }
    if ($relative -eq 'BepInEx\config\BepInEx.cfg' -and (Test-Path -LiteralPath $destination)) {
        $existingConfig = Get-Content -LiteralPath $destination -Raw
        if ($existingConfig -notmatch '(?m)^UnityLogListening\s*=\s*false\s*$') {
            throw 'Existing BepInEx.cfg preserved. Set UnityLogListening = false before installing the bridge.'
        }
        Write-Output 'Existing compatible BepInEx.cfg preserved.'
        continue
    }
    $files += [PSCustomObject]@{ source=$file.FullName; relative=$relative; sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
}
$files += [PSCustomObject]@{ source=$pluginFile; relative='BepInEx\plugins\BongoChat\BongoChatBridge.dll'; sha256=(Get-FileHash -LiteralPath $pluginFile -Algorithm SHA256).Hash }
foreach ($file in $files) {
    if (Test-Path -LiteralPath (Join-Path $target $file.relative)) { throw ('Existing file preserved: ' + $file.relative) }
}
# Save the full intended manifest first so interrupted installs are recoverable.
$manifest = [PSCustomObject]@{ version='0.1.1'; gamePath=$target; installedUtc=[DateTime]::UtcNow.ToString('o'); files=@($files | Select-Object relative,sha256) }
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
foreach ($file in $files) {
    $destination = Join-Path $target $file.relative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    Copy-Item -LiteralPath $file.source -Destination $destination
}
Write-Output "Bongo Chat Bridge installed: $target"
Write-Output 'Start Bongo Cat from Steam, join a room, then run BongoChat.exe.'

