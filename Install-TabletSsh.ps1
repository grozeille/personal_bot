<#
.SYNOPSIS
  Prepare la tablette au pilotage distant : installe OpenSSH Server,
  demarre sshd, ouvre le firewall (port 22), installe la cle publique du PC.
.DESCRIPTION
  A executer SUR LA TABLETTE, dans un PowerShell ADMINISTRATEUR :
    powershell -ExecutionPolicy Bypass -File P:\RobotSetup\Install-TabletSsh.ps1
  La cle publique est lue a cote du script (P:\RobotSetup\id_robot_ed25519.pub).
  A la fin, le script affiche le username et les IP : tester depuis le PC avec
    ssh -i ~/.ssh/id_robot_ed25519 USER@IP
#>
[CmdletBinding()]
param(
  [string]$KeyPath = ''
)
$ErrorActionPreference = 'Stop'

# Cle a cote du script ; $PSScriptRoot est vide si le contenu a ete colle
# dans la console -> repli sur le dossier courant (faire cd avant).
if ([string]::IsNullOrWhiteSpace($KeyPath))
{
  $here = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
  $KeyPath = Join-Path $here 'id_robot_ed25519.pub'
}

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
  ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin)
{
  Write-Error "Relance ce script dans un PowerShell ADMINISTRATEUR (clic droit -> 'Executer en tant qu''administrateur')."
  exit 1
}

Write-Host '== 1/6 : OpenSSH Server...'
$cap = Get-WindowsCapability -Online | Where-Object { $_.Name -like 'OpenSSH.Server*' } | Select-Object -First 1
if ($null -eq $cap)
{
  throw 'Capacite OpenSSH.Server introuvable (Windows trop ancien ou catalogue hs).'
}
if ($cap.State -ne 'Installed')
{
  Add-WindowsCapability -Online -Name $cap.Name
  Write-Host '  installe.'
}
else
{
  Write-Host '  deja installe.'
}

Write-Host '== 2/6 : shell par defaut = PowerShell...'
New-ItemProperty -Path 'HKLM:\SOFTWARE\OpenSSH' -Name DefaultShell `
  -Value 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' `
  -PropertyType String -Force | Out-Null

Write-Host '== 3/6 : service sshd...'
Start-Service sshd
Set-Service sshd -StartupType Automatic

Write-Host '== 4/6 : firewall port 22...'
$fw = Get-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -ErrorAction SilentlyContinue
if ($fw)
{
  Enable-NetFirewallRule -Name 'OpenSSH-Server-In-TCP'
}
else
{
  New-NetFirewallRule -Name 'sshd-tablette' -DisplayName 'OpenSSH Server (tablette)' `
    -Direction Inbound -Protocol TCP -LocalPort 22 -Action Allow | Out-Null
}

Write-Host '== 5/6 : cle publique...'
if (-not (Test-Path $KeyPath))
{
  throw "Cle publique introuvable : $KeyPath"
}
$destDir = 'C:\ProgramData\ssh'
New-Item -ItemType Directory -Force -Path $destDir | Out-Null
$dest = Join-Path $destDir 'administrators_authorized_keys'
Copy-Item $KeyPath $dest -Force
icacls $dest /inheritance:r /grant '*S-1-5-18:F' /grant '*S-1-5-32-544:F' | Out-Null
if ($LASTEXITCODE -ne 0)
{
  throw "icacls a echoue sur $dest"
}
$lines = (Get-Content $dest | Measure-Object -Line).Lines
Write-Host "  $lines ligne(s) installee(s) dans $dest"

Write-Host '== 6/6 : redemarrage + verification...'
Restart-Service sshd
Start-Sleep 2
$svc = Get-Service sshd
Write-Host ("  service sshd : " + $svc.Status)
Write-Host '  ACL :'
icacls $dest
Write-Host '  IPv4 :'
ipconfig | Select-String 'IPv4' | ForEach-Object { Write-Host ("   " + $_.Line.Trim()) }
Write-Host ''
Write-Host 'Termine. Depuis le PC, tester avec :'
Write-Host ("  ssh -i ~/.ssh/id_robot_ed25519 $env:USERNAME@<IP>")
Write-Host 'Premiere connexion : accepter la cle d''hote (yes).'
