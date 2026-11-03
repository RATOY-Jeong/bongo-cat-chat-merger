param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$app = Join-Path $repo 'build\BongoCatChatMerger.exe'
$bridge = Join-Path $repo 'build\BongoChatBridge.dll'
if (-not (Test-Path -LiteralPath $app) -or -not (Test-Path -LiteralPath $bridge)) { throw 'Run scripts/Build.ps1 first.' }
$run = Join-Path $repo ('build\checks-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run | Out-Null
Copy-Item -LiteralPath $app -Destination $run
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wpf = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF'
& $csc /nologo /target:winexe ("/out:$run\GameLaunchFixture.exe") (Join-Path $repo 'tests\GameLaunchFixture.cs')
if ($LASTEXITCODE -ne 0) { throw 'Fixture compilation failed.' }
foreach ($test in @('SetupTests','LocalizationTests','FeatureTests','ReconnectTests','TabletCompatibilityTests','ReliabilityTests','UpdateTests','MeowFilterTests','ConversationHistoryTests','OwnSendInputTests','ComposerEnterTests','AutoChestTests')) {
    & $csc /nologo /target:exe ("/out:$run\$test.exe") ("/reference:$run\BongoCatChatMerger.exe") /reference:System.dll /reference:System.Core.dll /reference:System.Xaml.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll ("/reference:$wpf\PresentationFramework.dll") ("/reference:$wpf\PresentationCore.dll") ("/reference:$wpf\WindowsBase.dll") (Join-Path $repo "tests\$test.cs")
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $test" }
    if ($test -eq 'SetupTests') {
        & (Join-Path $run "$test.exe") (Join-Path $run 'fixtures') $bridge (Join-Path $run 'GameLaunchFixture.exe')
    } else {
        if ($test -ne 'LocalizationTests') { [IO.File]::WriteAllText((Join-Path $run 'language.txt'), 'ko') }
        & (Join-Path $run "$test.exe") (Join-Path $run "$test-game")
    }
    if ($LASTEXITCODE -ne 0) { throw "Check failed: $test" }
}
& $csc /nologo /target:library ("/out:$run\Assembly-CSharp.dll") (Join-Path $repo 'tests\AutoChestGameFixture.cs')
if ($LASTEXITCODE -ne 0) { throw 'Chest model compilation failed.' }
& $csc /nologo /target:exe ("/out:$run\BridgeAutoChestTests.exe") ("/reference:$run\Assembly-CSharp.dll") ("/reference:$run\BongoCatChatMerger.exe") /reference:System.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll (Join-Path $repo 'tests\BridgeAutoChestTests.cs') (Join-Path $repo 'src\bridge\BridgeAutoChests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Bridge chest test compilation failed.' }
& (Join-Path $run 'BridgeAutoChestTests.exe') (Join-Path $run 'bridge-chest-fixtures')
if ($LASTEXITCODE -ne 0) { throw 'Bridge chest checks failed.' }
Write-Output "All checks passed. Isolated fixtures: $run"