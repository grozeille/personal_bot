<#
.SYNOPSIS
  Publie une mise à jour du robot sur le NAS via Velopack.

.DESCRIPTION
  1. dotnet publish de TabletApp (win-x86 auto-contenu),
  2. vpk pack (nupkg + releases.stable.json + Setup.exe),
  3. copie vers le dossier source surveillé par la tablette (P:\RobotMaj).

  La tablette détecte la maj sous 5 min (ou touche U sur le PC de dev)
  et propose l'icône en haut à droite. Première install : lancer
  P:\RobotMaj\Setup.exe une fois sur la tablette, ensuite tout est autonome.

.EXAMPLE
  .\Publier-Maj.ps1 -Version 1.0.1
#>
[CmdletBinding()]
param(
  # Version Velopack (doit augmenter à chaque publication, ex. 1.0.1).
  [Parameter(Mandatory)]
  [string]$Version,

  # Dossier source Velopack sur le NAS (même chemin sur PC et tablette).
  [string]$Source = 'P:\RobotMaj',

  # Identifiant applicatif Velopack (stable dans le temps).
  [string]$AppId = 'RobotCompanion'
)

$ErrorActionPreference = 'Stop'

$vpk = Get-Command vpk -ErrorAction SilentlyContinue
if (-not $vpk) {
  Write-Host 'Outil vpk introuvable, installation via dotnet tool...'
  dotnet tool install -g vpk
  $vpk = Get-Command vpk -ErrorAction Stop
}

$repo = Split-Path -Parent $PSCommandPath
$publishDir = Join-Path ([IO.Path]::GetTempPath()) "robot-publish\$Version"
$releasesDir = Join-Path ([IO.Path]::GetTempPath()) "robot-releases\$Version"
Remove-Item $publishDir, $releasesDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "1/3 publish $Version..."
dotnet publish (Join-Path $repo 'src\TabletApp\TabletApp.csproj') `
  -c Release -r win-x86 --self-contained -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish a échoué ($LASTEXITCODE)." }

Write-Host '2/3 vpk pack...'
& $vpk.Source pack -u $AppId -v $Version -p $publishDir -e TabletApp.exe -o $releasesDir
if ($LASTEXITCODE -ne 0) { throw "vpk pack a échoué ($LASTEXITCODE)." }

Write-Host "3/3 copie vers $Source..."
New-Item -ItemType Directory $Source -Force | Out-Null
Copy-Item (Join-Path $releasesDir '*') $Source -Force

$index = Join-Path $Source 'releases.win.json'
if (-not (Test-Path $index)) { throw "Index introuvable après copie : $index" }
Write-Host "OK : $Version publiée. Index : $index"
Write-Host 'Sur la tablette : icône en haut à droite sous 5 min, appui 3 s pour installer.'
