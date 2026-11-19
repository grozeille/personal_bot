# Projet Compagnon Robotique Expressif (Dell Venue 8 Pro + Raspberry Pi)

## 1. Vision & Objectifs du Projet

Transformer une tablette **Dell Venue 8 Pro 5830** en un robot compagnon de bureau interactif, expressif et ludique (inspiré de l'expressivité du *Reachy Mini* et de *Stack-Chan*) destiné à un enfant de 7 ans.

Le système repose sur une architecture distribuée légère :

- **La Tête (Tablette Windows 10 32 bits) :** Gère l'interface graphique native 60 FPS (yeux animés), la détection de mot-clé local (*Wake Word*), la capture audio/vidéo et la communication temps réel avec l'IA cloud (**Gemini Multimodal Live**).
- **Le Cou (Raspberry Pi 1 + PCA9685 + 3 Servos) :** Pilote une cinématique à **3 degrés de liberté (Pan / Tilt / Roll)** avec lissage d'accélération et mouvements organiques.

## 2. Architecture Matérielle & Logicielle

### Matériel Cible

- **Tablette :** Dell Venue 8 Pro 5830 (Intel Atom Z3745D, 2 Go RAM, SSD 58 Go, Windows 10 Home 32-bit `win-x86`, caméras avant/arrière, micro, haut-parleurs).
- **Contrôleur Moteurs :** Raspberry Pi 1 Model B (Raspberry Pi OS Lite) + carte PWM 16 canaux **PCA9685** (bus I2C).
- **Actionneurs (3-DOF) :**
  - `Canal 0` — **Pan** (Rotation gauche/droite) : Servomoteur MG996R.
  - `Canal 1` — **Tilt** (Inclinaison haut/bas) : Servomoteur DS3218 (20 kg·cm) ou MG996R.
  - `Canal 2` — **Roll** (Inclinaison latérale « tête penchée ») : Servomoteur MG996R.
- **Alimentation :** Bloc secteur AC-DC **5 V / 4 A** (20 W) avec condensateur tampon **1000 µF / 16 V** sur le bornier `V+` du PCA9685.

Schéma d'assemblage (tablette + Pan/Tilt/Roll + PCA9685 + Pi + alim 5V + base bois) : [docs/assemblage-robot.jfif](docs/assemblage-robot.jfif).

### Stack Technique

- **Application Tablette :** C# / **.NET 8** compilé en `win-x86` (`SelfContained = true`, `PublishSingleFile = true`).
  - *Rendu UI :* **SkiaSharp** (`SkiaSharp.Views.WindowsForms`) pour des yeux vectoriels fluides à 60 FPS (~25-35 Mo de RAM).
  - *Audio :* **NAudio** (capture PCM 16 kHz mono / lecture PCM 24 kHz).
  - *Wake Word local :* **Sherpa-ONNX** (`win-x86`, 100 % hors-ligne, 0 $ au repos).
  - *Vision locale :* **OpenCvSharp4** (`win-x86`) + API WinRT `MediaCapture`.
  - *IA & Function Calling :* WebSocket natif (`ClientWebSocket`) vers **Gemini Multimodal Live API**.
- **Serveur Moteurs (Pi 1) :** Python 3 (`FastAPI` ou `websockets` + `adafruit-circuitpython-servokit`) exposant une API locale sur le réseau Wi-Fi.

## 3. Feuille de Route par Étapes

### Étape 1 : Intelligence Vocale, Yeux Minimalistes -> Expressions & Wake Word (Tablette seule)

**Objectif :** Rendre la tablette vivante et capable de tenir une conversation vocale rigolote avec l'enfant, sans aucun matériel externe requis.

#### Sous-étape 1A : Moteur de rendu minimaliste (Yeux simples)

1. **Fenêtre & Canvas SkiaSharp (60 FPS) :**
   - Fenêtre plein écran sans bordure sur fond noir pur (`#000000`) pour masquer les bordures de l'écran.
   - **Design épuré initial (façon Stack-Chan / EVE) :** Deux grands yeux géométriques lumineux (cyan/blanc) avec pupilles mobiles, un clignement automatique périodique naturel et une légère pulsation lumineuse synchronisée au volume sonore (RMS) de la voix du robot.

#### Sous-étape 1B : Bibliothèque d'animations d'expressions (Style EVE + bouche)

Référence visuelle (2 rangées de 5, couleur `#79D7E3`) : [docs/reference-faces.jfif](docs/reference-faces.jfif).

1. **Yeux lumineux sans pupilles + bouche systématique, transition squash-swap-stretch (~300 ms par partie : la forme s'aplatit, bascule, se redéploie — sans transition si identique) :**
   - `neutral` : Deux ovales verticaux + petit sourire. **Cligne des yeux.**
   - `dead` : Yeux en croix `X X` + bouche plate (quand il "fait le mort" pour rire).
   - `mean` : Lames pleines en biais (épais en haut/extérieur, pointe en bas/intérieur) + petit sourire espiègle (faux méchant pour jouer).
   - `dizzy` (`>_<`) : Yeux en chevrons + sourire (quand il fait une blague ou fait semblant d'avoir le tournis).
   - `happy` : Deux ovales verticaux + bouche en demi-disque (joie). **Cligne des yeux.**
   - `laugh` : Yeux en arcs `^_^` + grande bouche en demi-disque (gros fou rire).
   - `love` (`♥`) : Yeux en forme de cœurs + sourire (quand l'enfant lui montre un beau dessin).
   - `wink` : Un ovale + un œil en chevron + sourire (clin d'œil complice, ne cligne pas).
   - `sleep` : Yeux fermés en U + bouche plate (expression dormir, éveillé).
   - `tired` : Dômes pleins (paupières lourdes) + bouche plate (fatigue).
   - `thinking` : Mêmes yeux que `neutral` + bouche en biais (réflexion).
   - `shocked` : Yeux dilatés + bouche en O (surprise totale).
   - Touches `1`–`9`, `0` : même ordre que le dessin — neutre, mort, méchant, tournis, joie, fatigue, amour, rire, clin d'œil, dormir. `F11`/`F12` : réflexion, surprise.
   - Seuls `neutral` et `happy` clignent ; le sommeil (bâillement/dodo/réveil) ferme tous les yeux.
   - Endormissement automatique (#16) : `tired` après 5 min d'inactivité, bâillement puis `sleep` après 5 min de plus ; toute touche ou toucher réveille et restaure `neutral` (seuils réglables dans `appsettings.json`, section `Idle`).
   - Sources : `docs/faces/parts/*.svg` (bibliothèque de 24 parties canoniques réutilisables : 9 yeux gauches, 9 yeux droits, 6 bouches, voir `docs/faces/README.md`), composées en 3 calques animés + glow SkiaSharp au rendu. Les `docs/faces/*.svg` restent la référence d'auteur.
   - Équivalences déclarées (pas d'animation entre elles) : œil gauche {neutral, happy, wink, thinking}, œil droit {neutral, happy, thinking} et {dizzy, wink}, bouche {neutral, mean, dizzy, love, wink} et {dead, tired, sleep}.
   - Regard fixe (pas de surveillance) pour `dead`, `dizzy`, `tired`, `wink`, `sleep` (yeux non ouverts).

#### Sous-étape 1C : Réveil vocal (*Wake Word*) & Conversation Gemini Live

1. **Mode Veille & Réveil vocal local :**
   - Écoute locale continue via **Sherpa-ONNX** sur le mot-clé choisi (ex. *"Coucou Robot"* ou prénom personnalisé).
   - Tant que le mot-clé n'est pas détecté (`- _ -`), 0 octet n'est envoyé sur Internet (0 $).
   - Au réveil : carillon sonore, transition `- _ -` -> `• _ •` et ouverture du WebSocket **Gemini Live**.
   - Retour automatique en veille après 15 secondes de silence ou sur commande vocale (*"Au revoir"*).
2. **Personnalité & Preprompt Système :**
   - Rôle : Petit robot compagnon curieux, bienveillant, un peu maladroit et très drôle pour un enfant de 7 ans.
   - Règles de langage : Phrases courtes, vocabulaire simple et imagé, ton enthousiaste, encouragements à la créativité, filtres de sécurité stricts adaptés aux enfants.
   - *Function Calling* initial : Gemini appelle `set_emotion(emotion)` en temps réel pendant qu'il parle pour déclencher les expressions (`happy`, `curious`, `sleepy`, `surprised`, `love`, `dizzy`, `neutral`).

#### Critères de validation (Étape 1)

- [ ] L'application `.exe` (`win-x86`) démarre en < 2 secondes et consomme < 60 Mo de RAM.
- [ ] Les yeux simples (`• _ •`) clignent naturellement à 60 FPS, puis basculent fluidement vers les expressions (`^ _ ^`, `o_O`, `-_-`, etc.).
- [ ] Le réveil vocal local fonctionne à 1,5 mètre et la conversation avec Gemini Live est fluide.

### Étape 2 : Vision par Caméra, Suivi du Visage & Capture Photo

**Objectif :** Permettre au robot de repérer la personne devant lui, de la suivre du regard (et bientôt de la tête) et de voir les objets/dessins qu'on lui montre.

#### Fonctionnalités

1. **Suivi du visage temps réel (Local — 15 FPS) :**
   - Capture de la caméra frontale via `Windows.Media.Capture` / `OpenCvSharp4` (résolution basse 320×240 pour économiser le CPU Atom).
   - Détection du visage le plus proche (modèle *YuNet* ou *Haar Cascade* ONNX).
   - Déplacement fluide des pupilles à l'écran vers les coordonnées `(X, Y)` du visage détecté.
   - Calcul des consignes d'angle cibles `(targetPan, targetTilt)` qui seront envoyées au cou motorisé.
2. **Vision IA & Prise de photo à la demande :**
   - Outil Gemini `take_photo_and_look()` déclenché quand l'enfant dit *"Regarde mon dessin !"*, *"Quelle couleur est mon pull ?"* ou *"Prends-nous en photo !"*.
   - Effet flash blanc à l'écran + bruit d'obturateur photo + expression `surprised` (`O_O`) ou `love` (`♥_♥`).
   - Envoi d'une image JPEG haute définition dans le flux WebSocket Gemini Live pour analyse immédiate, et sauvegarde locale facultative dans un dossier `PhotosRobot\`.

#### Critères de validation (Étape 2)

- [ ] Les pupilles suivent le visage de l'enfant dans la pièce sans faire saturer le CPU (< 45 % CPU total).
- [ ] Le robot décrit correctement un dessin ou un jouet présenté devant la caméra.

### Étape 3 : Simulation 3D & Développement du Pilote Hardware

**Objectif :** Valider la cinématique 3-DOF, les limites d'angles et la fluidité des animations dans un jumeau numérique 3D avant même le montage physique, tout en codant le protocole de pilotage réel.

#### Choix de l'outil 3D : **Godot Engine 4** (recommandé)

- *Pourquoi Godot plutôt que Blender pour ce cas d'usage :* Godot est léger, démarre instantanément sur votre PC fixe, intègre un serveur **UDP / WebSocket natif** en quelques lignes de GDScript, et permet de tester en temps réel les ordres envoyés par l'application C# de la tablette (avec contrôle de collisions et limites angulaires).

#### Fonctionnalités

1. **Jumeau numérique 3D (Godot 4) :**
   - Modélisation simplifiée du socle, des 3 pivots (`Pan` -> `Tilt` -> `Roll`) et de la tablette 8 pouces à l'échelle réelle (216 × 130 mm).
   - Réception en temps réel des trames JSON envoyées par l'app C# sur le réseau local (`UDP` à 30 Hz pour le suivi continu + `WebSocket/HTTP` pour les gestes).
2. **Moteur de cinématique & gestuelle (C# + Python Pi) :**
   - Architecture en **3 couches superposées** (inspirée de *Reachy Mini*) :
     1. *Couche Base (Suivi) :* Orientation `(Pan, Tilt)` vers le visage détecté à l'Étape 2.
     2. *Couche Vivante (Idle / Voix) :* Micro-respiration lente au repos + légères oscillations de tête synchronisées à la voix quand Gemini parle.
     3. *Couche Gestes (Tool Calling) :* Animations préprogrammées déclenchées par l'IA via `play_gesture(name)` : `nod_yes` (oui), `shake_no` (non), `curious_tilt` (pencher la tête `Roll = ±18°` couplé à l'expression `o_O`), `happy_dance` (couplé à `^_^`), `sleep_pose` (tête baissée couplée à `-_-`).
   - **Sécurité mécanique :** Bridage logiciel strict des angles min/max et profil d'accélération trapézoïdal / *ease-in-out* pour éviter tout à-coup brutal sur les engrenages.

#### Critères de validation (Étape 3)

- [ ] La tablette pilote en direct le modèle 3D sur votre PC fixe via Wi-Fi.
- [ ] Aucune collision entre les coins de la tablette et le socle sur toute l'amplitude des mouvements.

### Étape 4 : Assemblage Matériel & Déploiement sur Raspberry Pi 1

**Objectif :** Monter le support physique 3-DOF et basculer le flux de commandes du simulateur Godot vers le Raspberry Pi 1 réel.

#### Sous-étapes

1. **Préparation & Calibration sur table :**
   - Installation de Raspberry Pi OS Lite + activation du bus I2C (`raspi-config`).
   - Câblage du module **PCA9685** (SDA, SCL, 3.3V, GND) et de l'alimentation **5 V / 4 A** avec condensateur 1000 µF.
   - Mise au neutre (**90°**) des 3 servomoteurs avant vissage des palonniers aluminium 25T.
2. **Montage mécanique :**
   - Fixation du servo **Pan** sur la base bois lestée (20 × 20 cm).
   - Assemblage des équerres aluminium **Tilt** et **Roll** + pince universelle pour la Dell Venue 8 Pro.
   - Gestion des câbles (passage souple avec mou suffisant pour autoriser ±60° en Pan et ±25° en Tilt/Roll).
3. **Tests en conditions réelles :**
   - Bascule de l'adresse IP cible dans le fichier `appsettings.json` de l'app C# (IP du simulateur Godot -> `raspberrypi.local`).
   - Validation de la stabilité électrique (aucun redémarrage du Pi lors de mouvements combinés sur les 3 axes).
   - Test grandeur nature avec votre fille !

## 4. Structure Cible du Dépôt GitHub

```text
robot-companion/
├── README.md                          # Cette spécification
├── .gitignore                         # Exclusions .NET, Python, Godot
├── src/
│   ├── TabletApp/                     # Application C# .NET 8 (win-x86)
│   │   ├── TabletApp.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json           # Clé API, IP du Pi/Simulateur, WakeWord
│   │   ├── UI/
│   │   │   ├── RobotFaceRenderer.cs   # Rendu SkiaSharp 60 FPS (yeux simples -> expressions)
│   │   │   └── RobotExpression.cs     # États (^_^, o_O, -_-, O_O, etc.) + interpolation
│   │   ├── AI/
│   │   │   ├── GeminiLiveClient.cs    # WebSocket audio/vidéo + Tool Calling
│   │   │   └── WakeWordListener.cs    # Détection locale Sherpa-ONNX
│   │   ├── Vision/
│   │   │   └── FaceTracker.cs         # Suivi facial OpenCvSharp + capture photo
│   │   └── Motion/
│   │       └── MotionEngine.cs        # Cinématique 3 couches + client UDP/HTTP
│   ├── PiServoServer/                 # Serveur Python pour Raspberry Pi 1
│   │   ├── server.py                  # Réception UDP/HTTP + lissage PCA9685
│   │   ├── calibration.json           # Angles neutres et limites min/max
│   │   └── requirements.txt
│   └── Simulator3D/                   # Projet Godot 4 (Jumeau numérique 3-DOF)
│       ├── project.godot
│       ├── Main.tscn
│       └── RobotArm3DOF.gd            # Récepteur UDP et animation des 3 axes
└── .github/
    └── workflows/
        └── build-tablet-x86.yml       # Compilation automatique .exe win-x86
```
