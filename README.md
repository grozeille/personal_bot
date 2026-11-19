# Robot Compagnon Expressif

Tablette Dell Venue 8 Pro transformée en robot compagnon (tête expressive)
+ Raspberry Pi 1 + PCA9685 + 3 servos (cou Pan / Tilt / Roll).

La spécification complète est dans [spec.md](spec.md).

## Structure

- `src/TabletApp/` — application C# .NET 8 (`win-x86`) : yeux SkiaSharp, wake word, Gemini Live, suivi visage, cinématique.
- `src/PiServoServer/` — serveur Python du Raspberry Pi 1 (pilotage PCA9685).
- `src/Simulator3D/` — projet Godot 4, jumeau numérique 3-DOF.

## Build (PC de dev)

```powershell
dotnet publish src/TabletApp/TabletApp.csproj -c Release -r win-x86 --self-contained
```

L'exécutable 32 bits auto-contenu est produit dans
`src/TabletApp/bin/Release/net8.0-windows/win-x86/publish/` — copiable tel quel sur la tablette.
(Pas de CI : le build se fait en local, le binaire n'est jamais redistribué.)

## Tests BDD (Reqnroll + xUnit)

Scénarios Gherkin en français dans `tests/TabletApp.Tests/Features/`
(clignement, surveillance, sommeil). Chaque scénario simule le visage en
headless et produit **une capture finale** rendue offscreen.

```powershell
dotnet test tests/TabletApp.Tests/TabletApp.Tests.csproj
```

Le run génère `docs/rapport-tests/rapport.md` + PNG, consultables directement
sur GitHub (pense à les committer). Tâche VS Code : "tests + rapport".

## Contrôles

- `Échap` : quitter (PC de dev).
- Tap dans le coin haut-gauche : visage de confirmation, œil droit (✓) pour quitter (tablette tactile).
- `S` / `W` : s'endormir / se réveiller (provisoire, en attendant le wake word).
- `1`–`9`, `0` : comme le dessin de référence — neutre, mort, méchant, tournis, joie, fatigue, amour, rire, clin d'œil, dormir. `F11`/`F12` : réflexion, surprise.
- `U` : forcer une vérification de mise à jour (PC de dev).
- `I` : aperçu de l'icône de mise à jour, avec/sans (PC de dev, sans NAS).

## Mises à jour (Velopack + NAS)

Le robot vérifie `P:\RobotMaj` au démarrage puis toutes les 5 minutes
(silencieux si le NAS est hors-ligne). Si une maj existe, une icône
« flèches circulaires » (`docs/icons/update.svg`) apparaît en haut à droite :
un tap dessus affiche le visage de confirmation (`13-confirm.svg`, regard
figé), œil gauche (X) = annuler, œil droit (✓) = télécharger, installer et
redémarrer. (Pas d'appui long : Windows le promeut en clic droit.)
Le même visage sert à la sortie tactile (depuis le coin haut-gauche) :
l'icône reste visible pendant une confirmation de maj, pas de sortie.

```powershell
# Publier une maj sur le NAS (version croissante obligatoire) :
.\Publier-Maj.ps1 -Version 1.0.1
```

Première install sur la tablette : lancer `P:\RobotMaj\Setup.exe` une fois,
les maj suivantes sont 100 % autonomes. La source et l'intervalle se règlent
dans `appsettings.json` (section `Update`). Scénarios BDD : `Maj.feature`.

## Clé API Gemini (secret — jamais dans le dépôt)

Ne mets **jamais** la clé dans `appsettings.json` (fichier suivi par git) et ne la colle
**jamais** dans le chat. Deux options sûres, à taper toi-même dans ton terminal :

**Option A — User Secrets (recommandé)** : stocké hors dépôt dans
`%APPDATA%\Microsoft\UserSecrets\`, jamais committé.

```powershell
cd src/TabletApp
dotnet user-secrets set "Gemini:ApiKey" "TA_CLÉ"
```

**Option B — variable d'environnement** (visible uniquement de ta session) :

```powershell
$env:GEMINI_API_KEY = 'TA_CLÉ'
```

Le code lira la clé dans l'ordre : variable d'environnement → User Secrets →
`appsettings.json` (câblé dans l'issue #5). Pour vérifier que le secret est
enregistré (à faire toi-même, car la commande affiche la valeur en clair) :
`dotnet user-secrets list`.
