# Tablette Dell Venue 8 Pro — accès SSH

- Hôte : 192.168.0.34 (DHCP, peut changer — hostname 2442V22), user : mathi
- Clé : C:\Users\mathias\.ssh\id_robot_ed25519 (Ed25519, sans passphrase), publique installée côté tablette
- Base : `ssh -o BatchMode=yes -o ConnectTimeout=10 -i C:\Users\mathias\.ssh\id_robot_ed25519 mathi@192.168.0.34 '<commande>'`
- Shell distant : PowerShell 5.1 (sshd en démarrage auto, configuré par Install-TabletSsh.ps1)

## Règles SSH (pièges constatés)

- Jamais de guillemets doubles dans la commande **transmise** (mangés en transit). En bash local,
  envelopper avec des doubles et ne mettre que des simples à l'intérieur :
  `ssh ... "Get-HotFix -Id 'KB890830'"`.
- Pas de `&&` (PS 5.1). Commandes simples, une par connexion.
- query/qwinsta absents → sessions via `(Get-Process X).SessionId`.
- P: n'existe pas en SSH ; NAS en UNC `\\nas\data\Personal_Bot`.
- `Get-HotFix` ne voit ni KB2267602 (Defender) ni KB890830 (MSRT) : vérifier via
  `(New-Object -ComObject Microsoft.Update.Session).CreateUpdateSearcher().Search('IsInstalled=0').Updates`.

## Repères

- App : C:\Users\mathi\AppData\Local\RobotCompanion\current\TabletApp.exe
- Log : `$env:TEMP\tabletapp-voice.log`
- Conf locale : `$env:LOCALAPPDATA\TabletApp\` (motion-override.json, wake-calibration.json, enroll\)
- Lancement visible : `schtasks /Run /TN RobotFace`
- Sondes : C:\Users\mathi\Test-Audio.ps1, C:\Users\mathi\wasapiprobe.exe
- Builds : win-x86. Règles : jamais commit/push/deploy/merge sans ordre explicite.
