# Pause et reprise des discussions

« Attends », « pause », « tais-toi deux minutes » suspend la discussion
sans perdre le contexte ; la reprise sous 10 minutes dit « Oui ? » au
lieu de re-bonjour. Au-delà, discussion fraîche (le carnet mémoire,
lui, persiste toujours — voir [memoire.md](memoire.md)).

## Cycle

1. Pendant l'écoute, l'enfant dit « attends » → le modèle appelle
   `pause_conversation` et reste silencieux (bye/au revoir = vraie fin).
2. Earcon `pause`, visage `Tired`, session Live fermée (zéro coût à
   vide), horodatage de pause. Pas de consolidation (discussion non finie).
3. Reprise au wake word ou au tap : pause < 10 min → consigne « Oui ? /
   Je t'écoute, on reprend » + échanges récents du journal (qui contient
   déjà la session suspendue) ; sinon → « Bonjour » + carnet seul.

## Limites v1

- La pause vocale ne marche que pendant l'écoute (semi-duplex sourd
  pendant la parole : pour couper, tap-cancel puis « attends » si besoin).
- Pas de pause en unaire (repli réseau) : comportement inchangé.
- Seuil 10 min en dur (`PauseTracker.ResumeWindow`), pas de reprise
  après redémarrage de l'app (l'horodatage est en mémoire vive).
