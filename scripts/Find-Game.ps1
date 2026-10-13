function Resolve-BongoGamePath {
    param([string]$GamePath)
    if (-not [string]::IsNullOrWhiteSpace($GamePath)) {
        return (Resolve-Path -LiteralPath $GamePath -ErrorAction Stop).Path.TrimEnd('\')
    }
    $roots = [Collections.Generic.List[string]]::new()
    $steam = (Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
    if ($steam) { $roots.Add($steam) }
    $roots.Add((Join-Path ${env:ProgramFiles(x86)} 'Steam'))
    foreach ($root in @($roots.ToArray())) {
        $libraries = Join-Path $root 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $libraries) {
            foreach ($line in (Get-Content -LiteralPath $libraries)) {
                if ($line -match '"path"\s+"([^"]+)"') { $roots.Add($Matches[1].Replace('\\','\')) }
            }
        }
    }
    foreach ($root in $roots) {
        $candidate = Join-Path $root 'steamapps\common\BongoCat'
        if (Test-Path -LiteralPath (Join-Path $candidate 'BongoCat.exe')) { return $candidate }
    }
    Add-Type -AssemblyName System.Windows.Forms
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Title = 'Select BongoCat.exe in your Steam game folder'
    $dialog.Filter = 'Bongo Cat|BongoCat.exe'
    if ($dialog.ShowDialog() -ne [Windows.Forms.DialogResult]::OK) { throw 'Game folder selection cancelled.' }
    return [IO.Path]::GetDirectoryName($dialog.FileName)
}
