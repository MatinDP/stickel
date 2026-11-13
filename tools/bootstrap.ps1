param([switch]$Installer)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
New-Item -ItemType Directory -Force -Path '.tools','runtime\export','runtime\playback' | Out-Null
function Download-Verified([string]$Url, [string]$Path, [string]$Sha256) {
    if (!(Test-Path -LiteralPath $Path)) { Invoke-WebRequest $Url -OutFile $Path }
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Sha256) { throw "Checksum mismatch: $Path" }
}
if (!(Test-Path -LiteralPath '.tools\dotnet\dotnet.exe')) {
    Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile '.tools\dotnet-install.ps1'
    & '.\.tools\dotnet-install.ps1' -Version '10.0.404' -InstallDir (Join-Path $projectRoot '.tools\dotnet') -NoPath
}
if (!(Test-Path -LiteralPath 'runtime\export\ffmpeg.exe') -or !(Test-Path -LiteralPath '.tools\ffmpeg-export\ffmpeg-9.0.2-essentials_build\LICENSE')) {
    Download-Verified 'https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.2-essentials_build.zip' '.tools\ffmpeg.zip' '60F467265B1E312373DBCD92200C2618A74850F98D3D078E94296BB3FA2047BA'
    Expand-Archive -LiteralPath '.tools\ffmpeg.zip' -DestinationPath '.tools\ffmpeg-export' -Force
    Copy-Item '.tools\ffmpeg-export\ffmpeg-9.0.2-essentials_build\bin\ffmpeg.exe','.tools\ffmpeg-export\ffmpeg-9.0.2-essentials_build\bin\ffprobe.exe' 'runtime\export'
}
if (!(Test-Path -LiteralPath 'runtime\playback\FFmpeg\avcodec-63.dll')) {
    Download-Verified 'https://github.com/SuRGeoNix/Flyleaf/releases/download/v3.11.18/Flyleaf_v3.11.18.7z' '.tools\flyleaf.7z' '26E9150D31C1E1DF43FC9B0B7B32A0EF990DEC05DEE91EAAE43BEE9B98848A96'
    tar -xf '.tools\flyleaf.7z' -C 'runtime\playback' 'FFmpeg'
    if ($LASTEXITCODE -ne 0) { throw 'Cannot extract FFmpeg playback libraries.' }
}
if ($Installer -and !(Test-Path -LiteralPath '.tools\inno\tools\ISCC.exe')) {
    Download-Verified 'https://api.nuget.org/v3-flatcontainer/tools.innosetup/6.7.3/tools.innosetup.6.7.3.nupkg' '.tools\inno.zip' 'F780898E402FF80612CC8D9FCB8C6E02932BD1CB4C900FFDAA31F9341CFB49F4'
    Expand-Archive -LiteralPath '.tools\inno.zip' -DestinationPath '.tools\inno' -Force
}
Write-Host 'Stickel build tools are ready in the project directory.'
