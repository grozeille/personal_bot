#language: fr
Fonctionnalité: Quitter sans clavier
  Un tap dans le coin haut-gauche affiche la confirmation
  (œil X = rester, œil ✓ = quitter). Pas d'appui long :
  Windows le promeut en clic droit sur la tablette.

  Scénario: Tap dans le coin puis œil droit quitte
    Soit un dialogue de sortie
    Quand on appuie dans le coin haut-gauche
    Alors le visage de sortie est affiché
    Quand on appuie sur l'œil droit pour quitter
    Alors la fermeture est demandée

  Scénario: Œil gauche reste
    Soit un dialogue de sortie
    Quand on appuie dans le coin haut-gauche
    Et on appuie sur l'œil gauche pour rester
    Alors la fermeture n'est pas demandée
    Et l'application reste ouverte

  Scénario: Tap sur la bouche ignoré
    Soit un dialogue de sortie
    Quand on appuie dans le coin haut-gauche
    Et on appuie sur la bouche
    Alors la sortie reste affichée
    Et la fermeture n'est pas demandée

  Scénario: Tap hors du coin ne propose pas de sortir
    Soit un dialogue de sortie
    Quand on appuie au milieu de l'écran
    Alors aucune sortie n'est proposée
    Et la fermeture n'est pas demandée
