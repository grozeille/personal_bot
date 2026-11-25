<#
.SYNOPSIS
  Telecharge le Pokedex FR depuis PokeAPI vers src/TabletApp/Data/pokedex.json.

.DESCRIPTION
  1 requete liste + 3 requetes/espece (species + forme par defaut + intro
  Wikipedia FR) + chaines d'evolution dedupliquees. ~3600 requetes, ~25 min.
  Gratuit, sans cle (fair use : throttle + cache local). Accents geres en
  UTF-8 explicite (PS 5.1 corrompt sinon). A relancer si besoin de fraicheur.

.EXAMPLE
  .\Fetch-Pokedex.ps1
#>
[CmdletBinding()]
param(
  [string]$Out = '',
  [int]$Limit = 0,
  [int]$ThrottleMs = 150,
  [switch]$SkipWiki,
  [switch]$WikiOnly,
  [switch]$PokedexOnly
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSCommandPath
if ([string]::IsNullOrWhiteSpace($Out)) { $Out = Join-Path $repo 'src\TabletApp\Data\pokedex.json' }
New-Item -ItemType Directory (Split-Path -Parent $Out) -Force | Out-Null

$typeFr = @{
  # Accents en échappements : PS 5.1 lit les scripts sans BOM en ANSI.
  normal = 'Normal'; fire = 'Feu'; water = 'Eau'; electric = "$([char]0xC9)lectrik"; grass = 'Plante'
  ice = 'Glace'; fighting = 'Combat'; poison = 'Poison'; ground = 'Sol'; flying = 'Vol'
  psychic = 'Psy'; bug = 'Insecte'; rock = 'Roche'; ghost = 'Spectre'; dragon = 'Dragon'
  dark = "T$([char]0xE9)n$([char]0xE8)bres"; steel = 'Acier'; fairy = "F$([char]0xE9)e"; stellar = 'Stellaire'; unknown = 'Inconnu'
}
$genNum = @{
  'generation-i' = 1; 'generation-ii' = 2; 'generation-iii' = 3; 'generation-iv' = 4
  'generation-v' = 5; 'generation-vi' = 6; 'generation-vii' = 7; 'generation-viii' = 8
  'generation-ix' = 9
}

Add-Type -AssemblyName System.Net.Http
$http = New-Object Net.Http.HttpClient
$http.Timeout = [TimeSpan]::FromSeconds(30)
$http.DefaultRequestHeaders.Add('User-Agent', 'PersonalBot/1.0')

function Get-Json($url) {
  # Octets + UTF-8 explicite : Invoke-RestMethod PS 5.1 décode en Latin-1.
  for ($a = 0; $a -lt 3; $a++) {
    try {
      $resp = $http.GetAsync($url).Result
      [void]$resp.EnsureSuccessStatusCode()
      $bytes = $resp.Content.ReadAsByteArrayAsync().Result
      return [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json
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

function Get-WikiIntro($fr) {
  $wiki = ''
  try {
    $wurl = 'https://fr.wikipedia.org/w/api.php?action=query&format=json&prop=extracts&exintro&explaintext&redirects=1&titles=' + [uri]::EscapeDataString($fr)
    $wresp = $http.GetAsync($wurl).Result
    [void]$wresp.EnsureSuccessStatusCode()
    $wjson = [Text.Encoding]::UTF8.GetString($wresp.Content.ReadAsByteArrayAsync().Result) | ConvertFrom-Json
    foreach ($pg in $wjson.query.pages.PSObject.Properties) {
      if ($pg.Value.extract) { $wiki = Clean($pg.Value.extract); break }
    }
    if ($wiki.Length -gt 800) { $wiki = $wiki.Substring(0, 797) + '...' }
  }
  catch {
    $wiki = ''
  }
  return $wiki
}

function Strip-Wikitext($wt) {
  $h = $wt.IndexOf("`n==")
  if ($h -ge 0) { $wt = $wt.Substring(0, $h) }
  $sb = New-Object Text.StringBuilder
  $depth = 0
  $i = 0
  while ($i -lt $wt.Length) {
    if ($i + 1 -lt $wt.Length -and $wt[$i] -eq '{' -and $wt[$i + 1] -eq '{') { $depth++; $i += 2; continue }
    if ($i + 1 -lt $wt.Length -and $wt[$i] -eq '}' -and $wt[$i + 1] -eq '}') { if ($depth -gt 0) { $depth-- }; $i += 2; continue }
    if ($depth -eq 0) { [void]$sb.Append($wt[$i]) }
    $i++
  }
  $t = $sb.ToString()
  $t = $t -replace '(?s)<!--.*?-->', ' '
  $t = $t -replace '(?s)<ref[^>]*>.*?</ref>', ' '
  $t = $t -replace '<ref[^>]*/>', ' '
  $t = $t -replace '\[\[(Fichier|File|Image):[^\]]*\]\]', ' '
  $t = $t -replace '\[\[[^\]:|]*:[^\]]*\]\]', ' '
  $t = $t -replace '\[\[([^|\]]+)\|([^\]]*)\]\]', '$2'
  $t = $t -replace '\[\[([^\]]+)\]\]', '$1'
  $t = $t -replace "'{2,3}", ''
  $t = $t -replace '__[A-Z]+__', ' '
  $t = $t -replace '<[^>]+>', ' '
  $t = $t -replace '&nbsp;|&#160;', ' '
  $t = $t -replace '&amp;', '&'
  $t = $t -replace '&quot;|&#34;', '"'
  $t = $t -replace '&lt;', '<'
  $t = $t -replace '&gt;', '>'
  $t = $t -replace '\s+', ' '
  return $t.Trim()
}

function Get-PokedexIntro($fr) {
  try {
    $url = 'https://www.pokepedia.fr/api.php?action=query&format=json&prop=revisions&rvprop=content&rvslots=main&titles=' + [uri]::EscapeDataString($fr)
    $resp = $http.GetAsync($url).Result
    [void]$resp.EnsureSuccessStatusCode()
    $wjson = [Text.Encoding]::UTF8.GetString($resp.Content.ReadAsByteArrayAsync().Result) | ConvertFrom-Json
    foreach ($pg in $wjson.query.pages.PSObject.Properties) {
      $wt = $pg.Value.revisions[0].slots.main.'*'
      if ([string]::IsNullOrWhiteSpace($wt)) { continue }
      $text = Strip-Wikitext $wt
      if ($text.Length -gt 800) { $text = $text.Substring(0, 797) + '...' }
      if ($text.Length -ge 40) { return $text }
    }
    return ''
  }
  catch {
    return ''
  }
}

if ($PokedexOnly) {
  if (-not (Test-Path $Out)) { throw "Fichier absent : $Out" }
  $doc = Get-Content $Out -Raw -Encoding utf8 | ConvertFrom-Json
  $n = 0
  foreach ($p in $doc.pokemon) {
    if ($Limit -gt 0 -and $n -ge $Limit) { break }
    if ([string]::IsNullOrWhiteSpace($p.pokedex_intro_fr)) {
      $p | Add-Member -NotePropertyName pokedex_intro_fr -NotePropertyValue (Get-PokedexIntro $p.name_fr) -Force
      $n++
      if ($n % 50 -eq 0) { Write-Host "pokedex $n..." }
      Start-Sleep -Milliseconds ([Math]::Max($ThrottleMs, 300))
    }
  }
  [IO.File]::WriteAllText($Out, ($doc | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
  Write-Host "OK : $n intros Pokedex."
  return
}

if ($WikiOnly) {
  if (-not (Test-Path $Out)) { throw "Fichier absent : $Out" }
  $doc = Get-Content $Out -Raw -Encoding utf8 | ConvertFrom-Json
  $n = 0
  foreach ($p in $doc.pokemon) {
    if ([string]::IsNullOrWhiteSpace($p.wiki_intro_fr)) {
      $p | Add-Member -NotePropertyName wiki_intro_fr -NotePropertyValue (Get-WikiIntro $p.name_fr) -Force
      $n++
      if ($n % 50 -eq 0) { Write-Host "wiki $n..." }
      Start-Sleep -Milliseconds $ThrottleMs
    }
  }
  [IO.File]::WriteAllText($Out, ($doc | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
  Write-Host "OK : $n intros remplies."
  return
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
    $wiki = ''
    if (-not $SkipWiki) {
      $wiki = Get-WikiIntro $fr
      Start-Sleep -Milliseconds $ThrottleMs
    }
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
      wiki_fr = 'https://fr.wikipedia.org/wiki/' + [uri]::EscapeDataString($fr)
      pokepedia = 'https://www.pokepedia.fr/' + [uri]::EscapeDataString($fr)
      wiki_intro_fr = $wiki
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
    wiki_fr = $e.wiki_fr; pokepedia = $e.pokepedia; wiki_intro_fr = $e.wiki_intro_fr
    evolves_from = $from; evolves_to = $to
  })
}

$doc = [ordered]@{
  generated = (Get-Date -Format 'yyyy-MM-dd'); source = 'https://pokeapi.co/api/v2'
  count = $final.Count; pokemon = $final
}
[IO.File]::WriteAllText($Out, ($doc | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
Write-Host "OK : $($final.Count) Pokemon -> $Out"
