$projectRoot = Split-Path $PSScriptRoot -Parent
$executable = Join-Path $projectRoot 'dist\Stickel-1.0.1-win-x64\Stickel.exe'
if (!(Test-Path -LiteralPath $executable)) { & "$PSScriptRoot\build.ps1" -Package }
Start-Process -FilePath $executable -WindowStyle Normal
