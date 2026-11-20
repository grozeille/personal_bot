# Protocole tête — simu Godot ↔ (futur) PiServoServer

Même contrat des deux côtés : la TabletApp parle pareil au simulateur
et au vrai bras, on bascule l'URL sans rien changer.

## Transport unique : TCP (gestes, commandes, suivi continu)

- TCP brut, une ligne = un JSON (`tcp://<PC>:9080`, simu — meme port cote Pi plus tard).
- Pas de WebSocket : Godot 4 ne fournit pas de serveur WebSocket (pas de classe
  `WebSocketServer`), et en LAN le TCP suffit — y compris pour le suivi continu
  a 30 Hz (petits messages, latence negligeable).
- Cote C# : `TcpClient` + `StreamReader/Writer`. Cote Python (Pi) : `socket`.

## Commande

```json
{"yaw": 30.0, "pitch": -10.0, "roll": 5.0}
```

- Degrés, tous les champs optionnels (on n'envoie que ce qui change).
- Butees appliquées côté réception (valeurs hors plage = clampées) :
  `yaw ±90`, `pitch -30/+45`, `roll ±30`.
- `{"ping": 1}` → `{"ok": true, "pong": true, "state": {...}}` (test de lien).

## Réponse

```json
{"ok": true, "state": {"yaw": 30.0, "pitch": -10.0, "roll": 5.0}}
{"ok": false, "error": "raison"}
```

`state` = cible acceptée (après clamp), pas la position instantanée
(le simu lisse vers la cible à ~8/s).

## Piste de mouvements (gestes)

La tablette envoie une liste de keyframes horodatées (ms) — c'est elle qui
détient la bibliothèque de gestes, le simu/Pi ne fait qu'exécuter :

```json
{"moves": [
  {"yaw": 0, "pitch": 0, "roll": 0, "t": 0},
  {"yaw": -25, "pitch": 5, "roll": 0, "t": 400},
  {"yaw": 25, "pitch": 5, "roll": 0, "t": 800},
  {"yaw": 0, "pitch": 0, "roll": 0, "t": 1200}
]}
```

- Axes omis = valeur courante conservée. `t` croissants obligatoires.
- Réponse : `{"ok": true, "track_accepted": N, ...}`.
- Une commande directe `yaw/pitch/roll` annule la piste en cours.

## Conventions d'axes (vue de face du robot)

- `yaw +` = tête vers SA gauche (rotation.y positive Godot).
- `pitch +` = tête levée (rotation.x négative Godot).
- `roll +` = penchée vers SA droite (rotation.z positive Godot).
