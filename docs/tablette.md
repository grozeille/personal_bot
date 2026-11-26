# Tablette : connexion, déploiement, auto-update

Dell Venue 8 Pro 5830 (`DESKTOP-T1C6RA8`, Win10 Home 22H2 32-bit),
toujours branchée, app en kiosque plein écran. Sortie sans clavier :
tap dans le coin haut-gauche (confirmation œil X / œil ✓).

## Connexion SSH

- `ssh -i ~/.ssh/id_robot_ed25519 mathi@192.168.0.34`
- Installation (une fois, sur la tablette, PowerShell **admin**) :
  `powershell -ExecutionPolicy Bypass -File P:\RobotSetup\Install-TabletSsh.ps1`
  (ou [Install-TabletSsh.ps1](../Install-TabletSsh.ps1)) — installe
  OpenSSH Server, ouvre le port 22, pose la clé publique.
- À distance : PowerShell 5.1, guillemets simples, pas de `&&`.
  Le lecteur `P:` n'existe pas en session SSH (montage local uniquement) :
  passer par `C:\...` et récupérer avec `scp`.
- Le jeton SSH est filtré (pas d'admin distant) : les actions admin se
  font à la main sur la tablette. Ne JAMAIS installer les pilotes Intel
  optionnels via Windows Update (brick tactile/boutons du 2026-11-22).

## Déployer (depuis le PC)

```powershell
.\Publier-Maj.ps1 -Version 1.0.X   # version strictement croissante
```

1. `dotnet publish` win-x86 auto-contenu, 2. `vpk pack`
   (si `vpk` introuvable : `$env:PATH = "$env:USERPROFILE\.dotnet\tools;" + $env:PATH`),
   3. copie vers `P:\RobotMaj` (= `\\nas\data\Personal_Bot\RobotMaj`,
   même chemin sur PC et tablette), 4. vérifie `releases.win.json`.
- Première installation : lancer une fois `P:\RobotMaj\Setup.exe`
  sur la tablette, ensuite tout est autonome.

## Auto-update (Velopack)

- Source : dossier `P:\RobotMaj` (`SimpleFileSource`), vérification
  toutes les 5 min (section `Update` de `appsettings.json`, min 60 s).
- Maj détectée → icône discrète en haut à droite → tap dessus →
  visage de confirmation (œil X = annuler, œil ✓ = installer) →
  `...` plein écran pendant le téléchargement → Velopack applique
  et relance l'app. NAS hors-ligne = silencieux, paquet corrompu = refusé.
- Touches dev (PC) : `U` force une vérification, `I` prévisualise l'icône.
- Sur tablette : tap icône puis œil ✓ par l'utilisateur (pas d'auto-install).
