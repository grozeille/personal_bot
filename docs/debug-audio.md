# Debug audio des discussions

Quand le robot n'entend pas (ou entend de travers), il faut les faits :
ce qu'il a capté, ce qu'il a répondu, et les mesures du moment. Deux
outils, locaux uniquement, OFF par défaut (vie privée : enfant).

## Sonde micro (`--probe-mic`)

```powershell
dotnet run --project src/TabletApp/TabletApp.csproj -- --probe-mic
```

Liste les entrées WinMM, les défauts de capture par rôle, le volume et
le muet, puis mesure 6 s (parlez !) : parole ou non, RMS max, ZCR
moyen, crête système — avec les seuils en vigueur à côté. Comparez le
RMS max parlé au seuil de parole (500) : en dessous, le robot ne peut
pas vous entendre (seuil à régler ou micro à rapprocher/monter).
Réserve : en session SSH le micro est souvent inaccessible (pas de
session audio) — la sonde sert surtout en local.

## Enregistreur de tours (`--record-debug`)

```powershell
dotnet run --project src/TabletApp/TabletApp.csproj -- --record-debug
```

ou `"Debug": { "RecordAudio": true }` dans `appsettings.json`. Chaque
tour de discussion écrit, dans `%LOCALAPPDATA%\TabletApp\debug-audio\` :

- `turn-<session>-<n>-mic.wav` : le micro entendu (16 kHz mono),
- `turn-<session>-<n>-robot.wav` : la réplique jouée (24 kHz mono),
- `turn-<session>-<n>.json` : sidecar (chunks, parole/sourdine, RMS
  max, chunks envoyés, transcriptions entendues/dites).

Tampon tournant : purge par âge (48 h) puis par taille (200 Mo).
Récupération tablette : `scp` (le `P:` n'existe pas en SSH), ex.
`scp -i ~/.ssh/id_robot_ed25519 'mathi@192.168.0.34:"%LOCALAPPDATA%/TabletApp/debug-audio/turn-*.wav"' .`

## Analyser une surdité

1. Sidecar : `micPeakRms` vs seuil 500, `micSpeechChunks` vs 0,
   `micMutedChunks` (sourdine semi-duplex ?), `sentChunks`.
2. Écouter le `-mic.wav` : silence, souffle, TV, voix faible ?
3. `--probe-mic` : le micro par défaut est-il le bon, le volume suffit-il ?

Ces mêmes WAV servent de fixtures (bruit de pièce) et de données de
test pour l'empreinte vocale.
