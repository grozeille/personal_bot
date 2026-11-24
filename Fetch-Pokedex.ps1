<#
.SYNOPSIS
  Telecharge le Pokedex FR depuis PokeAPI vers src/TabletApp/Data/pokedex.json.

.DESCRIPTION
  1 requete liste + 2 requetes/espece (species + forme par defaut) + chaines
  d'evolution dedupliquees. ~2600 requetes, ~15-20 min. Gratuit, sans cle
  (fair use : throttle + cache local). A relancer si besoin de fraicheur.

.EXAMPLE
  .\Fetch-Pokedex.ps1
#>
[CmdletBinding()]
param(
  [string]$Out = '',
  [int]$Limit = 0,
  [int]$ThrottleMs = 150
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSCommandPath
if ([string]::IsNullOrWhiteSpace($Out)) { $Out = Join-Path $repo 'src\TabletApp\Data\pokedex.json' }
New-Item -ItemType Directory (Split-Path -Parent $Out) -Force | Out-Null

$typeFr = @{
  normal = 'Normal'; fire = 'Feu'; water = 'Eau'; electric = 'Électrik'; grass = 'Plante'
  ice = 'Glace'; fighting = 'Combat'; poison = 'Poison'; ground = 'Sol'; flying = 'Vol'
  psychic = 'Psy'; bug = 'Insecte'; rock = 'Roche'; ghost = 'Spectre'; dragon = 'Dragon'
  dark = 'Ténèbres'; steel = 'Acier'; fairy = 'Fée'; stellar = 'Stellaire'; unknown = 'Inconnu'
}
$genNum = @{
  'generation-i' = 1; 'generation-ii' = 2; 'generation-iii' = 3; 'generation-iv' = 4
  'generation-v' = 5; 'generation-vi' = 6; 'generation-vii' = 7; 'generation-viii' = 8
  'generation-ix' = 9
}

function Get-Json($url) {
  for ($a = 0; $a -lt 3; $a++) {
    try {
      return Invoke-RestMethod -Uri $url -TimeoutSec 30
    }
    catch {
      if ($a -eq 2) { throw }
      Start-Sleep -Seconds (2 + $a * 2)
    }
  }
}

function Clean($s) {
  if ($null -eq $s) { return '' }
  return ($s -replace '[\r\n\f]+', ' ').Trim()
}

Write-Host 'Liste des especes...'
$list = Get-Json 'https://pokeapi.co/api/v2/pokemon-species?limit=100000'
$results = $list.results
if ($Limit -gt 0) { $results = $results | Select-Object -First $Limit }
Write-Host "$($results.Count) especes."

$nameFr = @{}
$dex = New-Object System.Collections.ArrayList
$i = 0
foreach ($r in $results) {
  $i++
  if ($i % 50 -eq 0) { Write-Host "$i / $($results.Count)..." }
  try {
    $sp = Get-Json $r.url
    Start-Sleep -Milliseconds $ThrottleMs
    $fr = ($sp.names | Where-Object { $_.language.name -eq 'fr' } | Select-Object -First 1).name
    if ([string]::IsNullOrWhiteSpace($fr)) { $fr = $sp.name }
    $nameFr[$sp.name] = $fr
    $genus = ($sp.genera | Where-Object { $_.language.name -eq 'fr' } | Select-Object -First 1).genus
    $flav = $sp.flavor_text_entries | Where-Object { $_.language.name -eq 'fr' } | Select-Object -Last 1
    $formUrl = ($sp.varieties | Where-Object { $_.is_default } | Select-Object -First 1).pokemon.url
    if ([string]::IsNullOrWhiteSpace($formUrl)) { $formUrl = $sp.varieties[0].pokemon.url }
    $pk = Get-Json $formUrl
    Start-Sleep -Milliseconds $ThrottleMs
    $types = @($pk.types | Sort-Object slot | ForEach-Object { $_.type.name })
    $typesFr = @($types | ForEach-Object { if ($typeFr.ContainsKey($_)) { $typeFr[$_] } else { $_ } })
    [void]$dex.Add([ordered]@{
      id = $sp.id; name_fr = $fr; name_en = $sp.name
      types = $types; types_fr = $typesFr
      generation = $genNum[$sp.generation.name]
      legendary = [bool]$sp.is_legendary; mythical = [bool]$sp.is_mythical
      category_fr = Clean($genus)
      height_m = [math]::Round($pk.height / 10.0, 2)
      weight_kg = [math]::Round($pk.weight / 10.0, 2)
      description_fr = Clean($flav.flavor_text)
      sprite = $pk.sprites.other.'official-artwork'.front_default
      chain_url = $sp.evolution_chain.url
    })
  }
  catch {
    Write-Warning "$($r.name) ignore : $($_.Exception.Message)"
  }
}

Write-Host 'Chaines evolution...'
$chainCache = @{}
$edges = @{}
function Add-Chain($node, $parent) {
  $en = $node.species.name
  $kids = @($node.evolves_to | ForEach-Object { $_.species.name })
  $edges[$en] = @{ from = $parent; to = $kids }
  foreach ($k in $node.evolves_to) { Add-Chain $k $en }
}
foreach ($e in $dex) {
  $url = $e.chain_url
  if (-not $chainCache.ContainsKey($url)) {
    $chainCache[$url] = Get-Json $url
    Start-Sleep -Milliseconds $ThrottleMs
  }
  Add-Chain $chainCache[$url].chain $null
}

$final = New-Object System.Collections.ArrayList
foreach ($e in $dex) {
  $from = $null
  $to = @()
  if ($edges.ContainsKey($e.name_en)) {
    $f = $edges[$e.name_en].from
    if ($null -ne $f) { $from = if ($nameFr.ContainsKey($f)) { $nameFr[$f] } else { $f } }
    $to = @($edges[$e.name_en].to | ForEach-Object { if ($nameFr.ContainsKey($_)) { $nameFr[$_] } else { $_ } })
  }
  [void]$final.Add([ordered]@{
    id = $e.id; name_fr = $e.name_fr; name_en = $e.name_en
    types = $e.types; types_fr = $e.types_fr
    generation = $e.generation; legendary = $e.legendary; mythical = $e.mythical
    category_fr = $e.category_fr; height_m = $e.height_m; weight_kg = $e.weight_kg
    description_fr = $e.description_fr; sprite = $e.sprite
    evolves_from = $from; evolves_to = $to
  })
}

$doc = [ordered]@{
  generated = (Get-Date -Format 'yyyy-MM-dd'); source = 'https://pokeapi.co/api/v2'
  count = $final.Count; pokemon = $final
}
[IO.File]::WriteAllText($Out, ($doc | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
Write-Host "OK : $($final.Count) Pokemon -> $Out"
