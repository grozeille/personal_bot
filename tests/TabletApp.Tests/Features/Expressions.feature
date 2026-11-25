#language: fr
Fonctionnalité: Expressions du visage
  Le robot affiche 14 expressions (yeux sans pupilles + bouche) avec transition animée.

  Plan du scénario: Toutes les expressions
    Soit un visage éveillé
    Quand on affiche l'expression "<emotion>"
    Et 2 secondes s'écoulent
    Alors l'expression affichée est "<emotion>"
    Et la transition est terminée
    Et la transition est terminée

    Exemples:
      | emotion |
      | neutral |
      | dead    |
      | mean    |
      | dizzy   |
      | happy   |
      | tired   |
      | love    |
      | laugh   |
      | wink     |
      | sleep    |
      | thinking |
      | shocked  |
      | busy     |
      | confirm  |

  Scénario: Emotion inconnue ignorée
    Soit un visage éveillé
    Quand on affiche l'expression "nimportequoi"
    Et 2 secondes s'écoulent
    Alors l'expression affichée est "neutral"
