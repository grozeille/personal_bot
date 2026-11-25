# Skills du robot

Une skill = des règles nommées que le robot charge à la demande (outil
`load_skill`) au lieu de les réexpliquer à chaque fois. Exemple :
`devinette-animaux` (jeu de devinettes par oui/non).

## Ajouter une skill

1. Créer `src/TabletApp/Skills/<slug>.md` (le nom du fichier = le slug) :
   ```markdown
   # Titre lisible
   Triggers: mot-clé, autre indice

   Règles, une par ligne, style impératif...
   ```
   - 1re ligne `# ...` : titre (sinon le slug).
   - Ligne `Triggers: ...` : mots qui aident le modèle à choisir (sinon vide).
   - Le reste : les règles envoyées au modèle.
2. Rebuild + publier (les `*.md` partent avec l'app).
3. Tester en Live : proposer l'activité, vérifier `Skill : <slug>.` dans le log.

## Surcharge locale (sans recompiler)

Déposer un `.md` de même nom (ou nouveau) dans
`%LOCALAPPDATA%\TabletApp\skills\` : il remplace (ou complète) la version
livrée, et survit aux mises à jour.

## Fonctionnement

- Au démarrage de session, le prompt liste les skills (`slug (déclencheurs)`).
- Quand l'enfant propose une activité couverte, le modèle appelle
  `load_skill` avec le slug et reçoit les règles (contexte de la session).
- Slug inconnu : le robot reçoit la liste des slugs valides pour se reprendre.
- Périmètre : Live uniquement (l'unaire n'a pas d'outils).
