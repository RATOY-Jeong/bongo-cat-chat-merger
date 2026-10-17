param(
    [string]$GamePath,
    [string]$BepInExCore = (Join-Path (Split-Path -Parent $PSScriptRoot) 'runtime\BepInEx\core')
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Find-Game.ps1')
$GamePath = Resolve-BongoGamePath -GamePath $GamePath
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wpf = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF'
$repoRoot = Split-Path -Parent $PSScriptRoot
$out = Join-Path $repoRoot 'build'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$appSources = @(Get-ChildItem (Join-Path $repoRoot 'src\app\*.cs') -File | Select-Object -ExpandProperty FullName)
$sharedSources = @(Get-ChildItem (Join-Path $repoRoot 'src\shared\*.cs') -File | Select-Object -ExpandProperty FullName)
$bridgeSources = @(Get-ChildItem (Join-Path $repoRoot 'src\bridge\*.cs') -File | Select-Object -ExpandProperty FullName)
& $compiler /nologo /target:winexe ("/out:$out\BongoCatChatMerger.exe") /reference:System.dll /reference:System.Core.dll /reference:System.Xaml.dll /reference:System.Web.Extensions.dll ("/reference:$wpf\PresentationFramework.dll") ("/reference:$wpf\PresentationCore.dll") ("/reference:$wpf\WindowsBase.dll") $appSources $sharedSources
if ($LASTEXITCODE -ne 0) { throw 'App compilation failed.' }
$managed = Join-Path $GamePath 'BongoCat_Data\Managed'
& $compiler /nologo /target:library /nowarn:0649,1684 ("/out:$out\BongoChatBridge.dll") /reference:System.dll /reference:System.Core.dll ("/reference:$BepInExCore\BepInEx.dll") ("/reference:$BepInExCore\0Harmony.dll") ("/reference:$managed\UnityEngine.dll") ("/reference:$managed\UnityEngine.CoreModule.dll") ("/reference:$managed\UnityEngine.JSONSerializeModule.dll") ("/reference:$managed\UnityEngine.ImageConversionModule.dll") ("/reference:$managed\UnityEngine.UIModule.dll") ("/reference:$managed\UnityEngine.UI.dll") ("/reference:$managed\Heathen.Steamworks.dll") ("/reference:$managed\com.rlabrecque.steamworks.net.dll") ("/reference:$managed\Newtonsoft.Json.dll") $bridgeSources $sharedSources
if ($LASTEXITCODE -ne 0) { throw 'Bridge compilation failed.' }
Write-Output 'Build succeeded.'




