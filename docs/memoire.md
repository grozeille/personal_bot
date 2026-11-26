# Mémoire des discussions

Le robot se souvient d'une session à l'autre : journal brut local +
carnet consolidé par le LLM, rappelés dans le prompt à chaque session.

## Fichiers (`%LOCALAPPDATA%\TabletApp\memory\`)

- `journal-AAAA-MM-JJ.md` : un tour = `- [HH:mm:ss] Enfant : ...` /
  `- [HH:mm:ss] Patate : ...`. Alimenté par le Live ET l'unaire.
  Élagué > 30 jours (à chaque démarrage).
- `MEMORY.md` : carnet curé (prénom, âge, goûts, jeux en cours,
  promesses). Réécrit par consolidation, jamais à la main.

## Cycle

1. Pendant la discussion : chaque tour entendu/dit est consigné.
2. Nouvelle session : le prompt reçoit le carnet + les échanges
   récents (hier + aujourd'hui, plafonnés à 4000 signes).
3. Fin de discussion : consolidation en tâche de fond (1 appel texte
   Flash, journal plafonné à 8000 signes, carnet à 4000). Rien si le
   journal est vide. Best effort : un échec ne bloque jamais.

## Vie privée et quotas

Local uniquement, jamais uploadé. Pas de données sensibles retenues
(consigne de consolidation : ni nom de famille, ni adresse, ni école,
ni téléphone). Coût : ~1 appel texte par discussion (quota gratuit
large). V2 envisagée : commande vocale « oublie tout ».
