param([switch]$Test, [switch]$Package, [switch]$Installer)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
& "$PSScriptRoot\bootstrap.ps1" -Installer:$Installer
$sdkPath = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
& $sdkPath build 'Stickel.slnx' -c Release -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if ($Test) {
    & $sdkPath run --project 'tests\Stickel.Integration\Stickel.Integration.csproj' -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Media integration tests failed.' }
    $fixturePath = Get-ChildItem 'artifacts\integration' -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'results.json') } | Sort-Object Name | Select-Object -Last 1 -ExpandProperty FullName
    & $sdkPath run --project 'tests\Stickel.ViewModel\Stickel.ViewModel.csproj' -c Release --no-build -- $fixturePath
    if ($LASTEXITCODE -ne 0) { throw 'View-model tests failed.' }
}
if ($Package -or $Installer) {
    $publishPath = Join-Path $projectRoot 'dist\Stickel-1.0.1-win-x64'
    & $sdkPath publish 'src\Stickel.Desktop\Stickel.Desktop.csproj' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $publishPath -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item 'README.md','THIRD-PARTY-NOTICES.md' $publishPath
    Copy-Item 'docs' $publishPath -Recurse -Force
    New-Item -ItemType Directory -Force -Path (Join-Path $publishPath 'licenses') | Out-Null
    Copy-Item '.tools\ffmpeg-export\ffmpeg-9.0.2-essentials_build\LICENSE' (Join-Path $publishPath 'licenses\FFmpeg-export-LICENSE.txt')
    Copy-Item '.tools\ffmpeg-export\ffmpeg-9.0.2-essentials_build\README.txt' (Join-Path $publishPath 'licenses\FFmpeg-build-README.txt')
    if (Test-Path -LiteralPath 'licenses') { Copy-Item 'licenses\*' (Join-Path $publishPath 'licenses') -Force }
    $sample = Get-ChildItem 'artifacts\integration' -Filter 'Starter.stickel' -Recurse -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
    if ($sample) { New-Item -ItemType Directory -Force -Path (Join-Path $publishPath 'Samples') | Out-Null; Copy-Item -LiteralPath $sample.FullName -Destination (Join-Path $publishPath 'Samples\Starter.stickel') }
    Compress-Archive -Path (Join-Path $publishPath '*') -DestinationPath 'dist\Stickel-1.0.1-win-x64.zip' -CompressionLevel Optimal -Force
}
if ($Installer) {
    & '.\.tools\inno\tools\ISCC.exe' '/Qp' 'installer\Stickel.iss'
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
}
