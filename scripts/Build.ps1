param(
    [string]$GamePath,
    [string]$BepInExCore = (Join-Path (Split-Path -Parent $PSScriptRoot) 'runtime\BepInEx\core'),
    [string]$BridgeBinary
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Find-Game.ps1')
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wpf = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF'
$repoRoot = Split-Path -Parent $PSScriptRoot
$out = Join-Path $repoRoot 'build'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$appSources = @(Get-ChildItem (Join-Path $repoRoot 'src\app\*.cs') -File | Select-Object -ExpandProperty FullName)
$sharedSources = @(Get-ChildItem (Join-Path $repoRoot 'src\shared\*.cs') -File | Select-Object -ExpandProperty FullName)
$bridgeSources = @(Get-ChildItem (Join-Path $repoRoot 'src\bridge\*.cs') -File | Select-Object -ExpandProperty FullName)
if ($BridgeBinary) {
    Copy-Item -LiteralPath $BridgeBinary -Destination (Join-Path $out 'BongoChatBridge.dll') -Force
} else {
    $GamePath = Resolve-BongoGamePath -GamePath $GamePath
    $managed = Join-Path $GamePath 'BongoCat_Data\Managed'
    & $compiler /nologo /target:library /nowarn:0649,1684 ("/out:$out\BongoChatBridge.dll") /reference:System.dll /reference:System.Core.dll ("/reference:$BepInExCore\BepInEx.dll") ("/reference:$BepInExCore\0Harmony.dll") ("/reference:$managed\UnityEngine.dll") ("/reference:$managed\UnityEngine.CoreModule.dll") ("/reference:$managed\UnityEngine.JSONSerializeModule.dll") ("/reference:$managed\UnityEngine.ImageConversionModule.dll") ("/reference:$managed\UnityEngine.UIModule.dll") ("/reference:$managed\UnityEngine.UI.dll") ("/reference:$managed\Heathen.Steamworks.dll") ("/reference:$managed\com.rlabrecque.steamworks.net.dll") ("/reference:$managed\Newtonsoft.Json.dll") $bridgeSources $sharedSources
    if ($LASTEXITCODE -ne 0) { throw 'Bridge compilation failed.' }
}
Add-Type -AssemblyName System.IO.Compression
$runtime = Join-Path $repoRoot 'runtime'
if (-not (Test-Path -LiteralPath (Join-Path $runtime 'winhttp.dll'))) { throw 'Copy runtime from the full release ZIP into the source root first.' }
$payload = Join-Path $out 'bridge-payload.zip'
$stream = [IO.File]::Open($payload, [IO.FileMode]::Create)
try {
    $archive = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $files = @()
        foreach ($file in (Get-ChildItem -LiteralPath $runtime -File -Recurse)) {
            $files += [PSCustomObject]@{ source=$file.FullName; entry=('runtime/' + $file.FullName.Substring($runtime.Length+1).Replace('\','/')) }
        }
        $files += [PSCustomObject]@{ source=(Join-Path $out 'BongoChatBridge.dll'); entry='BongoChatBridge.dll' }
        foreach ($file in $files) {
            $entry = $archive.CreateEntry($file.entry, [IO.Compression.CompressionLevel]::Optimal)
            $inputStream = [IO.File]::OpenRead($file.source)
            $outputStream = $entry.Open()
            try { $inputStream.CopyTo($outputStream) } finally { $inputStream.Dispose(); $outputStream.Dispose() }
        }
    } finally { $archive.Dispose() }
} finally { $stream.Dispose() }
$noticeText = New-Object Text.StringBuilder
foreach ($license in (Get-ChildItem -LiteralPath (Join-Path $repoRoot 'licenses') -File | Sort-Object Name)) {
    [void]$noticeText.AppendLine('===== ' + $license.Name + ' =====')
    [void]$noticeText.AppendLine([IO.File]::ReadAllText($license.FullName))
    [void]$noticeText.AppendLine()
}
$noticePath = Join-Path $out 'THIRD-PARTY-NOTICES.txt'
[IO.File]::WriteAllText($noticePath, $noticeText.ToString(), (New-Object Text.UTF8Encoding($false)))
& $compiler /nologo /target:winexe /platform:x64 ("/win32manifest:$repoRoot\src\app\app.manifest") ("/resource:$noticePath,BongoChat.ThirdPartyNotices") ("/out:$out\BongoCatChatMerger.exe") ("/resource:$payload,BongoChat.SetupPayload") /reference:System.dll /reference:System.Core.dll /reference:System.Xaml.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ("/reference:$wpf\PresentationFramework.dll") ("/reference:$wpf\PresentationCore.dll") ("/reference:$wpf\WindowsBase.dll") $appSources $sharedSources
if ($LASTEXITCODE -ne 0) { throw 'App compilation failed.' }
Write-Output 'Build succeeded: the EXE includes all bridge installation files.'