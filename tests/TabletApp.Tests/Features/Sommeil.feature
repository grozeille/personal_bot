#language: fr
Fonctionnalité: Cycle de sommeil
  Le robot bâille et s'endort, puis se réveille en battant des paupières.

  Scénario: Endormissement et réveil
    Soit un visage éveillé
    Quand le robot s'endort
    Et 5 secondes s'écoulent
    Alors le robot dort paisiblement
    Quand le robot se réveille
    Et 3 secondes s'écoulent
    Alors les paupières ont battu avant l'ouverture complète
    Et les yeux sont ouverts

  Scénario: Endormissement automatique par inactivité
    Soit un visage éveillé
    Quand 6 minutes s'écoulent
    Alors l'expression affichée est "tired"
    Quand 5 minutes s'écoulent
    Alors le robot dort paisiblement
    Et l'expression affichée est "sleep"
