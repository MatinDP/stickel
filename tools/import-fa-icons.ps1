param([Parameter(Mandatory=$true)][string]$SourceDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $projectRoot 'src\Stickel.Desktop\Assets\FontAwesome'
$names = @('rotate-left','rotate-right','play','pause','stop','backward-step','forward-step','backward','forward','bullseye','arrow-left','arrow-right','arrow-up','arrow-down','magnifying-glass-minus','magnifying-glass-plus','moon','sun','chevron-down','chevron-up','file','folder-open','floppy-disk','file-export','plus','trash','scissors','crop-simple','image','video','folder','layer-group','stamp','xmark','sliders','arrows-rotate','rotate','volume-high','volume-low','volume-xmark')
New-Item -ItemType Directory -Path (Join-Path $destination 'solid') -Force | Out-Null
$icons = [ordered]@{}
foreach ($name in $names) {
    $source = Join-Path $SourceDirectory "Desktop\svgs\solid\$name.svg"
    [xml]$svg = Get-Content -LiteralPath $source -Raw
    $box = $svg.DocumentElement.GetAttribute('viewBox').Split(' ')
    $paths = $svg.DocumentElement.SelectNodes("*[local-name()='path']")
    $icons[$name] = @{ Width = [double]$box[2]; Height = [double]$box[3]; Path = (($paths | ForEach-Object { $_.GetAttribute('d') }) -join ' ') }
    Copy-Item -LiteralPath $source -Destination (Join-Path $destination "solid\$name.svg") -Force
}
$icons | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $destination 'icons.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $SourceDirectory 'Desktop\LICENSE.txt') -Destination (Join-Path $destination 'LICENSE.txt') -Force
Write-Output "Imported $($names.Count) Font Awesome icons."
