#language: fr
Fonctionnalité: Regard de surveillance
  Le regard balaie l'environnement de gauche à droite.

  Scénario: Saccades régulières sur 30 secondes
    Soit un visage éveillé
    Quand 30 secondes s'écoulent
    Alors le regard a fait au moins 3 saccades
    Et le regard reste entre la gauche et la droite extrêmes
    Et le regard a visité la gauche et la droite
