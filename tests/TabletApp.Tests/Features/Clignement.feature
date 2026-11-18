#language: fr
Fonctionnalité: Clignement des yeux
  Le robot cligne des yeux à fréquence humaine, de façon aléatoire.

  Scénario: Clignement régulier sur 30 secondes
    Soit un visage éveillé
    Quand 30 secondes s'écoulent
    Alors le robot a cligné entre 3 et 16 fois
    Et tous les intervalles de clignement sont entre 1,9 et 6,5 secondes
    Et les yeux se ferment complètement
